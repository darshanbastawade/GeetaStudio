using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

internal sealed record OptimizationDispatchReceipt(Guid Id, Guid PrimaryOperation, Guid ShadowOperation, Guid PrimaryRoute, Guid? ShadowRoute, bool Candidate, DateTimeOffset Deadline);

public sealed partial class OptimizationWorkbench
{
    public async Task<OptimizationGeneration> GenerateAsync(CoreScope s, Guid u, Guid id, OptimizationGenerate r, CancellationToken ct)
    {
        var dispatch = await Command(s, u, id, r.OperationId, "dispatch", r, async row =>
        {
            var p = Read<OptimizationProposal>(row.ProposalJson);
            OptimizationRules.Require(p.EndsUtc > DateTimeOffset.UtcNow, "Experiment deadline exceeded; use the normal reviewed route outside this experiment.");
            OptimizationRules.Require(r.Request.OperationId == r.OperationId && r.Request.WorkflowAdmission == null && r.Request.TalkAdmission == null && r.Request.PinnedRouteId == null && !r.Request.Streaming && !r.Request.Tools && r.AudienceId == p.AudienceId && r.Request.Locale == p.Locale, "Exact audience/locale and owned generation operation required.");
            OptimizationRules.Require(p.BaselineRouteId.HasValue && p.CandidateRouteId.HasValue && row.State is "Active" or "RolledBack", "Qualified experiment routes required.");
            var candidate = row.State == "Active" && p.EndsUtc > DateTimeOffset.UtcNow && r.Cohort == p.Cohort;
            if (candidate)
            {
                var evidence = Read<OptimizationEvaluation>((await db.OptimizationEvents.SingleAsync(x => x.ExperimentId == id && x.Kind == "evaluate", ct)).EvidenceJson);
                try { await CheckEvaluation(s, u, p, evidence, ct); }
                catch (ContentOS.Domain.RuntimeValidationException) { row.State = "RolledBack"; candidate = false; Event(s, row, Guid.NewGuid(), "evidence-rollback", new { Reason = "Current evaluation no longer passes" }); }
            }
            var primary = candidate && p.Mode == "Canary" ? p.CandidateRouteId!.Value : p.BaselineRouteId!.Value;
            var shadow = candidate && p.Mode == "Shadow" ? p.CandidateRouteId : null;
            if (p.Dimension == "Prompt")
            {
                OptimizationRules.Require(r.Request.Prompt == "" && r.Inputs != null, "Template experiments accept bounded declared inputs, never an arbitrary replacement prompt.");
                var a = await Prompt(s, u, p.BaselinePromptId!.Value, p.BaselineRouteId!.Value, p, ct); var b = await Prompt(s, u, p.CandidatePromptId!.Value, p.CandidateRouteId!.Value, p, ct);
                _ = WorkflowRules.Render(a, r.Inputs!); _ = WorkflowRules.Render(b, r.Inputs!);
            }
            else OptimizationRules.Require(r.Inputs == null, "Declared template inputs belong to prompt experiments.");
            var baseline = await Route(s, p.BaselineRouteId!.Value, p, ct); var selected = await Route(s, primary, p, ct);
            OptimizationRules.Require(selected.Task == r.Request.Task && baseline.Task == selected.Task, "Only the evaluated generation task is allowed.");
            var cost = selected.RunLimit + (shadow.HasValue ? (await Route(s, shadow.Value, p, ct)).RunLimit : 0);
            // Reserve full ceilings permanently. Unknown/abandoned dispatches cannot recycle credits.
            OptimizationRules.Require(row.Dispatches < p.MaxDispatches && row.ReservedCost + cost <= p.MaxCost, "Experiment dispatch/cost limit exhausted.");
            row.Dispatches++; row.ReservedCost += cost;
            return new OptimizationDispatchReceipt(Guid.NewGuid(), r.OperationId, Guid.NewGuid(), primary, shadow, candidate, p.EndsUtc);
        }, ct);
        var proposal = Read<OptimizationProposal>((await Row(s, u, id, ct)).ProposalJson);
        var primaryPrompt = r.Request.Prompt; var shadowPrompt = r.Request.Prompt;
        if (proposal.Dimension == "Prompt")
        {
            var primaryPromptId = dispatch.PrimaryRoute == proposal.CandidateRouteId ? proposal.CandidatePromptId : proposal.BaselinePromptId;
            primaryPrompt = WorkflowRules.Render(await Prompt(s, u, primaryPromptId!.Value, dispatch.PrimaryRoute, proposal, ct), r.Inputs!);
            if (dispatch.ShadowRoute.HasValue) shadowPrompt = WorkflowRules.Render(await Prompt(s, u, proposal.CandidatePromptId!.Value, dispatch.ShadowRoute.Value, proposal, ct), r.Inputs!);
        }
        var primaryRequest = r.Request with { OperationId = dispatch.PrimaryOperation, PinnedRouteId = dispatch.PrimaryRoute, Prompt = primaryPrompt };
        if (dispatch.Candidate && dispatch.ShadowRoute == null) primaryRequest = primaryRequest with { OptimizationAdmission = new(id, dispatch.Id) };
        var result = await ai.GenerateAsync(s, u, primaryRequest, ct);
        AiRunView? shadowResult = null;
        if (dispatch.ShadowRoute is { } shadowRoute)
            shadowResult = await ai.GenerateAsync(s, u, r.Request with { OperationId = dispatch.ShadowOperation, PinnedRouteId = shadowRoute, Prompt = shadowPrompt, OptimizationAdmission = new(id, dispatch.Id) }, ct);
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct); await Lock(s, ct); await Access(s, u, true, ct);
        var current = await Row(s, u, id, ct);
        var accepted = dispatch.Candidate && current.State == "Active" && dispatch.Deadline > DateTimeOffset.UtcNow && (shadowResult ?? result).Outcome == "Success";
        if (dispatch.Candidate && (shadowResult ?? result).Outcome != "Success" && current.State == "Active")
        {
            current.State = "RolledBack"; Event(s, current, Guid.NewGuid(), "provider-rollback", new { dispatch.Id, Outcome = (shadowResult ?? result).Outcome });
        }
        if (dispatch.Candidate && !accepted && dispatch.ShadowRoute == null) result = result with { Output = null };
        Event(s, current, Guid.NewGuid(), "dispatch-observed", new { dispatch.Id, PrimaryRun = result.Id, ShadowRun = shadowResult?.Id, accepted, result.Outcome });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(dispatch.Id, dispatch.PrimaryRoute, result, shadowResult, accepted);
    }
}

public sealed partial class AiService
{
    private async Task WatchOptimization(CoreScope s, OptimizationAiAdmission? claim, CancellationTokenSource execution, CancellationToken stop)
    {
        if (claim == null) return;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await timer.WaitForNextTickAsync(stop))
            {
                await using var check = new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                var row = await check.OptimizationExperiments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == claim.ExperimentId && x.TenantId == s.TenantId, stop);
                if (row == null || row.State != "Active" || EditorialBinding.Read<OptimizationProposal>(row.ProposalJson).EndsUtc <= DateTimeOffset.UtcNow || !await check.Members.AnyAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor && x.Permission == "Administrator", stop)) { execution.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception) { execution.Cancel(); } // A failed fence check cannot authorize continued candidate spend.
    }
    private async Task OptimizationAdmission(CoreScope s, Guid universe, Guid operation, Guid? route, OptimizationAiAdmission? claim, CancellationToken ct)
    {
        if (claim == null) return;
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Optimization:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Optimization admission unavailable',1;", ct);
        var row = await db.OptimizationExperiments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == claim.ExperimentId && x.TenantId == s.TenantId && x.UniverseId == universe, ct);
        if (row == null || row.State != "Active") throw new OperationCanceledException("Experiment no longer admits candidate generation.");
        var proposal = EditorialBinding.Read<OptimizationProposal>(row.ProposalJson);
        var receipts = await db.OptimizationEvents.AsNoTracking().Where(x => x.ExperimentId == row.Id && x.Kind == "dispatch" && x.Actor == s.Actor).Select(x => x.ResultJson).ToArrayAsync(ct);
        var proof = receipts.Select(EditorialBinding.Read<OptimizationDispatchReceipt>);
        var valid = proposal.EndsUtc > DateTimeOffset.UtcNow && route == proposal.CandidateRouteId && proof.Any(d =>
            d.Id == claim.DispatchId && d.Candidate && (d.PrimaryOperation == operation && d.PrimaryRoute == route || d.ShadowOperation == operation && d.ShadowRoute == route));
        if (!valid) throw new OperationCanceledException("Exact reserved candidate operation/route required.");
    }
}
