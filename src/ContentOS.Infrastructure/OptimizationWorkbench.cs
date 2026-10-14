using System.Data;
using System.Diagnostics.Metrics;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed partial class OptimizationWorkbench(CoreDbContext db, IQualityEvaluation evaluation, IAiTaskRouter ai,
    IEditorialWorkbench editorial, IAnalyticsWorkbench analytics) : IOptimizationWorkbench
{
    private static readonly Meter Meter = new("ContentOS.Optimization", "1.0");
    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>("optimization.decision.requests");
    private static string Json<T>(T x) => EditorialBinding.Json(x);
    private static T Read<T>(string x) => EditorialBinding.Read<T>(x);
    private static string Hash(object x) => EditorialBinding.Hash(x);
    private async Task Access(CoreScope s, Guid u, bool write, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
        var role = await db.Members.Where(x => x.TenantId == s.TenantId && x.Actor == s.Actor).Select(x => x.Permission).SingleOrDefaultAsync(ct);
        if (role == null || write && role != "Administrator") throw new CoreAccessException();
    }
    private async Task Lock(CoreScope s, CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Optimization:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Optimization lock unavailable',1;", ct);
    private async Task<OptimizationExperiment> Row(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.OptimizationExperiments.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private void Event(CoreScope s, OptimizationExperiment row, Guid op, string kind, object evidence, object? result = null, string? fingerprint = null)
    {
        db.OptimizationEvents.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = row.UniverseId, ExperimentId = row.Id, OperationId = op, Kind = kind, Actor = s.Actor, Fingerprint = fingerprint ?? Hash(evidence), EvidenceJson = Json(evidence), ResultJson = Json(result) });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = row.Id, Action = "optimization." + kind });
        Decisions.Add(1, new KeyValuePair<string, object?>("kind", kind));
    }
    private async Task<T> Command<T>(CoreScope s, Guid u, Guid id, Guid op, string kind, object request, Func<OptimizationExperiment, Task<T>> act, CancellationToken ct)
    {
        OptimizationRules.Require(op != Guid.Empty, "Stable operation required."); db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(s, ct); await Access(s, u, true, ct);
        var old = await db.OptimizationEvents.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (old != null) { if (old.UniverseId != u || old.Actor != s.Actor || old.Kind != kind || old.ExperimentId != id || old.Fingerprint != Hash(request)) throw new RuntimeConflictException("Optimization replay differs."); return Read<T>(old.ResultJson); }
        var row = await Row(s, u, id, ct); var result = await act(row);
        Event(s, row, op, kind, kind == "dispatch" ? new { OperationId = op, RequestHash = Hash(request), InputRetention = "NoRawGenerationInputs" } : request, result, Hash(request));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<Guid> ProposeAsync(CoreScope s, Guid u, OptimizationProposal r, CancellationToken ct)
    {
        OptimizationRules.Proposal(r); db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await Lock(s, ct); await Access(s, u, true, ct);
        var old = await db.OptimizationEvents.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct);
        if (old != null) { if (old.UniverseId != u || old.Actor != s.Actor || old.Kind != "propose" || old.Fingerprint != Hash(r)) throw new RuntimeConflictException("Proposal replay differs."); return old.ExperimentId; }
        OptimizationRules.Require(await db.Audiences.AnyAsync(x => x.Id == r.AudienceId && x.UniverseId == u && x.MinAge >= 18, ct), "Scoped adult audience required; child policy changes need human review.");
        foreach (var candidate in new[] { r.BaselineCandidateId, r.CandidateId })
            if (!await db.EvaluationCandidates.AnyAsync(x => x.Id == candidate && x.TenantId == s.TenantId && x.UniverseId == u, ct)) throw new CoreAccessException();
        OptimizationRules.Require(!await db.OptimizationExperiments.AnyAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key && x.Version >= r.Version, ct), "Append a higher experiment version.");
        if (r.BaselineRouteId.HasValue || r.CandidateRouteId.HasValue)
        {
            OptimizationRules.Require(r.BaselineRouteId.HasValue && r.CandidateRouteId.HasValue && r.Dimension is "Prompt" or "Provider", "Both qualified routes required for prompt/provider dispatch.");
            await Route(s, r.BaselineRouteId!.Value, r, ct); await Route(s, r.CandidateRouteId!.Value, r, ct);
        }
        if (r.Dimension == "Prompt")
        {
            OptimizationRules.Require(r.BaselinePromptId.HasValue && r.CandidatePromptId.HasValue && r.BaselineRouteId.HasValue && r.CandidateRouteId.HasValue, "Prompt experiments require two owned template revisions and exact routes.");
            await Prompt(s, u, r.BaselinePromptId!.Value, r.BaselineRouteId!.Value, r, ct); await Prompt(s, u, r.CandidatePromptId!.Value, r.CandidateRouteId!.Value, r, ct);
        }
        else OptimizationRules.Require(r.BaselinePromptId == null && r.CandidatePromptId == null, "Prompt template bindings belong to prompt experiments.");
        var row = new OptimizationExperiment { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, Owner = s.Actor, ProposalJson = Json(r) };
        db.OptimizationExperiments.Add(row); Event(s, row, r.OperationId, "propose", r, row.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return row.Id;
    }
    private async Task<RegisterPrompt> Prompt(CoreScope s, Guid u, Guid id, Guid routeId, OptimizationProposal p, CancellationToken ct)
    {
        var row = await db.PromptRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        var prompt = Read<RegisterPrompt>(row.SettingsJson); var route = await Route(s, routeId, p, ct);
        OptimizationRules.Require(prompt.AudienceId == p.AudienceId && prompt.Locale == p.Locale && route.PromptVersion == prompt.Key + ".v" + prompt.Version, "Exact owned prompt version/audience/locale must match the evaluated route.");
        OptimizationRules.Require(!prompt.RequireEvidence || route.Task == "answer-with-evidence", "An evidence-required template must use the evidence-answer route; generation cannot bypass its retrieval requirement.");
        return prompt;
    }
    private async Task<AiRouteSettings> Route(CoreScope s, Guid id, OptimizationProposal p, CancellationToken ct)
    {
        var row = await db.AiRoutes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        var route = Read<AiRouteSettings>(row.SettingsJson);
        OptimizationRules.Require(route.Task is "draft" or "summarize" or "translate" or "answer-with-evidence" && !route.Tools && !route.Streaming && route.Currency == p.Currency, "Read/generation-only, nonstreaming matching-currency routes required.");
        foreach (var profileId in route.Profiles)
        {
            var profile = await db.AiProfiles.AsNoTracking().SingleAsync(x => x.Id == profileId && x.TenantId == s.TenantId, ct); var settings = Read<AiProviderSettings>(profile.SettingsJson);
            OptimizationRules.Require(settings.Adapter is "fixture.text.a" or "fixture.text.b" && settings.Qualification == "OfflineVerified" && settings.Locale == p.Locale && settings.Pricing.InputPerMillion == 0 && settings.Pricing.CachedInputPerMillion == 0 && settings.Pricing.OutputPerMillion == 0, "Only zero-price qualified fixture adapters are enabled for speculative routing; live qualification remains a release gate.");
        }
        return route;
    }
    public async Task<OptimizationView> InspectAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        await Access(s, u, false, ct); var row = await Row(s, u, id, ct);
        var history = await db.OptimizationEvents.AsNoTracking().Where(x => x.ExperimentId == id).OrderBy(x => x.CreatedUtc).Select(x => new OptimizationEventView(x.Id, x.Kind, x.Actor, x.EvidenceJson, x.CreatedUtc)).ToArrayAsync(ct);
        return new(row.Id, Read<OptimizationProposal>(row.ProposalJson), row.State, Convert.ToBase64String(row.RowVersion), history);
    }
    public async Task<OptimizationView[]> ListAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var ids = await db.OptimizationExperiments.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(101).Select(x => x.Id).ToArrayAsync(ct);
        OptimizationRules.Require(ids.Length <= 100, "Narrow/archive the experiment history before exceeding list capacity100.");
        var result = new List<OptimizationView>(); foreach (var id in ids) result.Add(await InspectAsync(s, u, id, ct)); return result.ToArray();
    }
    private async Task CheckEvaluation(CoreScope s, Guid u, OptimizationProposal p, OptimizationEvaluation r, CancellationToken ct)
    {
        var baseline = await evaluation.InspectAsync(s, u, r.BaselineRunId, ct); var development = await evaluation.InspectAsync(s, u, r.DevelopmentRunId, ct); var holdout = await evaluation.InspectAsync(s, u, r.HeldOutRunId, ct);
        OptimizationRules.Require(baseline.CandidateId == p.BaselineCandidateId && development.CandidateId == p.CandidateId && holdout.CandidateId == p.CandidateId && baseline.Split == "Development" && development.Split == "Development" && holdout.Split == "HeldOut" && development.BaselineRunId == baseline.Id && holdout.BaselineRunId == baseline.Id && baseline.DatasetId == development.DatasetId && baseline.DatasetId == holdout.DatasetId, "Exact baseline/candidate, dataset and unused held-out lineage required.");
        OptimizationRules.Require(baseline.Results.Length >= p.MinimumDevelopmentCases && development.Results.Length >= p.MinimumDevelopmentCases && holdout.Results.Length >= p.MinimumHeldOutCases, "Minimum recorded evaluation evidence required.");
        OptimizationRules.Require(new[] { baseline, development, holdout }.All(report => report.Results.All(x => x.Passed && x.Case?.AudienceId == p.AudienceId && x.Locale == p.Locale && x.Output != null)), "Current rights, deterministic quality and audience/locale checks required.");
        OptimizationRules.Require(development.FailureRate <= baseline.FailureRate && holdout.FailureRate <= baseline.FailureRate, "Candidate regression violates stop rule.");
        if (p.CandidateRouteId.HasValue)
        {
            OptimizationRules.Require(baseline.Configuration.AiRouteId == p.BaselineRouteId && development.Configuration.AiRouteId == p.CandidateRouteId && holdout.Configuration.AiRouteId == p.CandidateRouteId, "Evaluation must bind the exact dispatched route.");
            var a = await Route(s, p.BaselineRouteId!.Value, p, ct); var b = await Route(s, p.CandidateRouteId.Value, p, ct);
            OptimizationRules.Require(a.Task == b.Task && baseline.Configuration.PromptVersion == a.PromptVersion && development.Configuration.PromptVersion == b.PromptVersion && holdout.Configuration.PromptVersion == b.PromptVersion, "Prompt/task snapshots must match qualified routes.");
        }
    }
    public async Task<OptimizationView> EvaluateAsync(CoreScope s, Guid u, Guid id, OptimizationEvaluation r, CancellationToken ct)
    {
        await Command(s, u, id, r.OperationId, "evaluate", r, async row => { OptimizationRules.Require(row.State == "Proposed", "A proposal can be evaluated once; changed evidence needs a new experiment version."); await CheckEvaluation(s, u, Read<OptimizationProposal>(row.ProposalJson), r, ct); row.State = "Evaluated"; return id; }, ct); return await InspectAsync(s, u, id, ct);
    }
    public async Task<OptimizationView> MeasureAsync(CoreScope s, Guid u, Guid id, OptimizationMeasurement r, CancellationToken ct)
    {
        await Command(s, u, id, r.OperationId, "measure", r, row =>
        {
            var p = Read<OptimizationProposal>(row.ProposalJson); OptimizationRules.Evidence(r.EvidenceReference);
            OptimizationRules.Require(OptimizationRules.Metrics.Contains(r.Metric) && r.Origin == "Fixture" && r.Samples is > 0 and <= 100000 && r.ObservedUtc <= DateTimeOffset.UtcNow && r.ObservedUtc >= row.CreatedUtc && r.Value >= 0 && (r.Metric == "Cost" || r.Value <= 1), "Bounded current fixture measurement required; self-assessment and popularity cannot grant approval.");
            if (OptimizationRules.Breach(p, r) && row.State is "Active" or "Evaluated") { row.State = "RolledBack"; Event(s, row, Guid.NewGuid(), "guardrail-rollback", new { r.Metric, r.Value, r.Samples, r.EvidenceReference }); }
            return Task.FromResult(id);
        }, ct); return await InspectAsync(s, u, id, ct);
    }
    public async Task<OptimizationView> ControlAsync(CoreScope s, Guid u, Guid id, OptimizationControl r, CancellationToken ct)
    {
        await Command(s, u, id, r.OperationId, "control", r, async row =>
        {
            OptimizationRules.Evidence(r.EvidenceReference); if (Convert.ToBase64String(row.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
            if (r.Action == "Rollback") { OptimizationRules.Require(row.State is "Active" or "Evaluated", "Active/evaluated experiment required."); row.State = "RolledBack"; return id; }
            OptimizationRules.Require(r.Action == "Activate" && row.State == "Evaluated", "Only evaluated configurations can activate; publishing approval and stateful migrations are excluded.");
            var p = Read<OptimizationProposal>(row.ProposalJson); OptimizationRules.Require(p.EndsUtc > DateTimeOffset.UtcNow, "Experiment expired.");
            var evidence = Read<OptimizationEvaluation>((await db.OptimizationEvents.SingleAsync(x => x.ExperimentId == id && x.Kind == "evaluate", ct)).EvidenceJson); await CheckEvaluation(s, u, p, evidence, ct);
            var observations = (await db.OptimizationEvents.Where(x => x.ExperimentId == id && x.Kind == "measure").OrderByDescending(x => x.CreatedUtc).ToArrayAsync(ct)).Select(x => Read<OptimizationMeasurement>(x.EvidenceJson)).ToArray();
            OptimizationRules.Require(p.Guardrails.All(g => observations.FirstOrDefault(x => x.Metric == g.Metric) is { } m && m.Samples >= g.MinimumSamples && !OptimizationRules.Breach(p, m)), "Every guardrail needs sufficient passing recorded measurements.");
            row.State = "Active"; return id;
        }, ct); return await InspectAsync(s, u, id, ct);
    }
}
public static class OptimizationRegistration
{
    public static IServiceCollection AddOptimization(this IServiceCollection s) => s.AddScoped<IOptimizationWorkbench, OptimizationWorkbench>();
}
