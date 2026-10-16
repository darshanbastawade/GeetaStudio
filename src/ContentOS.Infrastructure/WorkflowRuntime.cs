using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed partial class WorkflowRuntime(CoreDbContext db, IToolInvoker tools, IAiTaskRouter ai, IAiAdministration aiAdmin, IDbContextFactory<CoreDbContext> factory, IServiceProvider services) : IWorkflowRuntime, IWorkflowWorker
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Decode<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)!;
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Encode(value))));
    private async Task Access(CoreScope scope, Guid? universe, bool admin, CancellationToken ct)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == scope.TenantId && x.Actor == scope.Actor, ct);
        if (member == null || admin && member.Permission != "Administrator" ||
            universe.HasValue && !await db.Universes.AnyAsync(x => x.Id == universe && x.TenantId == scope.TenantId, ct)) throw new CoreAccessException();
    }
    private async Task Lock(CancellationToken ct) => await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='ContentOS.Workflows', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000,'Workflow lock unavailable',1;", ct);
    private void Audit(CoreScope s, Guid id, string action) => db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = "workflow." + action });
    private async Task Save(CancellationToken ct) { try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); } }
    public async Task<Guid> RegisterPromptAsync(CoreScope s, Guid u, RegisterPrompt r, CancellationToken ct)
    {
        WorkflowRules.Prompt(r); await Access(s, u, true, ct);
        if (!await db.Audiences.AnyAsync(x => x.Id == r.AudienceId && x.UniverseId == u, ct) || !await db.Locales.AnyAsync(x => x.Id == r.Locale, ct)) throw new RuntimeValidationException("Unknown locale/audience binding.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(ct);
        var old = await db.PromptRevisions.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key && x.Version == r.Version, ct);
        if (old != null) { if (old.SettingsJson != Encode(r)) throw new RuntimeConflictException("Prompt version differs."); return old.Id; }
        var row = new PromptRevision { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SettingsJson = Encode(r) };
        db.PromptRevisions.Add(row); Audit(s, row.Id, "prompt-registered"); await Save(ct); await tx.CommitAsync(ct); return row.Id;
    }
    public async Task<IReadOnlyList<PromptView>> PromptsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); return (await db.PromptRevisions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)).Select(x => new PromptView(x.Id, Decode<RegisterPrompt>(x.SettingsJson))).ToArray();
    }
    internal async Task<WorkflowSnapshot> Snapshot(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        var row = await db.WorkflowDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        var def = Decode<RegisterWorkflow>(row.SettingsJson);
        var prompt = await db.PromptRevisions.AsNoTracking().SingleAsync(x => x.Id == def.PromptId, ct);
        var p = Decode<RegisterPrompt>(prompt.SettingsJson);
        var route = await db.AiRoutes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == def.RouteId && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        var r = Decode<AiRouteSettings>(route.SettingsJson);
        var profiles = await db.AiProfiles.AsNoTracking().Where(x => r.Profiles.Contains(x.Id) && x.TenantId == s.TenantId).ToListAsync(ct);
        var list = r.Profiles.Select(x => profiles.Single(y => y.Id == x)).Select(x => new AiProfileView(x.Id, x.ConnectionId, Decode<AiProviderSettings>(x.SettingsJson))).ToArray();
        return new(new(prompt.Id, p), new(row.Id, def), new(route.Id, r, false), list);
    }
    public async Task<Guid> RegisterDefinitionAsync(CoreScope s, Guid u, RegisterWorkflow r, CancellationToken ct)
    {
        WorkflowRules.Definition(r); await Access(s, u, true, ct);
        var prompt = await db.PromptRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.PromptId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        var p = Decode<RegisterPrompt>(prompt.SettingsJson);
        var route = await db.AiRoutes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.RouteId && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        var setting = Decode<AiRouteSettings>(route.SettingsJson);
        if (setting.Task != "draft" || !setting.Structured || setting.Tools || setting.Streaming || setting.Currency != r.Currency ||
            setting.RunLimit > r.MaxCost || setting.DeadlineSeconds + 10 >= r.LeaseSeconds || setting.PromptVersion != p.Key + ".v" + p.Version)
            throw new RuntimeValidationException("Workflow route/prompt/budget/lease bindings differ.");
        var profiles = await db.AiProfiles.AsNoTracking().Where(x => setting.Profiles.Contains(x.Id) && x.TenantId == s.TenantId).ToListAsync(ct);
        if (profiles.Count != setting.Profiles.Length || profiles.Sum(x => { var v = Decode<AiProviderSettings>(x.SettingsJson); return v.MaxInputBytes + v.MaxOutputTokens; }) > r.MaxTokens)
            throw new RuntimeValidationException("Worst-case fallback token bound exceeds workflow.");
        if (!await db.AiRouteHeads.AnyAsync(x => x.RouteId == route.Id && x.TenantId == s.TenantId, ct)) throw new RuntimeValidationException("Initially select an activated qualified route.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(ct);
        var old = await db.WorkflowDefinitions.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key && x.Version == r.Version, ct);
        if (old != null) { if (old.SettingsJson != Encode(r)) throw new RuntimeConflictException("Workflow version differs."); return old.Id; }
        var row = new WorkflowDefinition { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, PromptId = r.PromptId, Key = r.Key, Version = r.Version, SettingsJson = Encode(r) };
        db.WorkflowDefinitions.Add(row); Audit(s, row.Id, "definition-registered"); await Save(ct); await tx.CommitAsync(ct); return row.Id;
    }
    public async Task<IReadOnlyList<WorkflowDefinitionView>> DefinitionsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); return (await db.WorkflowDefinitions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)).Select(x => new WorkflowDefinitionView(x.Id, Decode<RegisterWorkflow>(x.SettingsJson))).ToArray();
    }
    public async Task<DurableWorkflowView> StartAsync(CoreScope s, Guid u, StartWorkflow r, CancellationToken ct)
    {
        await Access(s, u, true, ct);
        if (r.OperationId == Guid.Empty || r.SourceSnapshotIds?.Length > 10) throw new RuntimeValidationException("Invalid operation/evidence bound.");
        var snap = await Snapshot(s, u, r.DefinitionId, ct); WorkflowRules.Render(snap.Prompt.Settings, r.Inputs);
        foreach (var id in r.SourceSnapshotIds ?? [])
            if (!await (from source in db.SourceSnapshots join edition in db.SourceEditions on source.EditionId equals edition.Id join work in db.SourceWorks on edition.WorkId equals work.Id where source.Id == id && work.TenantId == s.TenantId && work.UniverseId == u select source.Id).AnyAsync(ct)) throw new CoreAccessException();
        if (r.PlanJson?.Length > 1000) throw new RuntimeValidationException("Plan exceeds bound.");
        // Canonical JSON property ordering makes request replay independent of input dictionary order.
        r = r with { Inputs = r.Inputs.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value) };
        if (r.EvidenceContentRevisionId != null && !await (from rev in db.Revisions join c in db.Contents on rev.ContentId equals c.Id where rev.Id == r.EvidenceContentRevisionId && c.TenantId == s.TenantId && c.UniverseId == u select rev.Id).AnyAsync(ct)) throw new CoreAccessException();
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(ct); await Access(s, u, true, ct);
        var fingerprint = Hash(new { u, r }); var old = await db.DurableWorkflows.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct);
        if (old != null) { if (old.Actor != s.Actor || old.Fingerprint != fingerprint) throw new RuntimeConflictException("Workflow replay differs."); await tx.CommitAsync(ct); return await InspectAsync(s, old.Id, ct); }
        var run = new DurableWorkflow
        {
            Id = Guid.NewGuid(),
            TenantId = s.TenantId,
            UniverseId = u,
            DefinitionId = r.DefinitionId,
            OperationId = r.OperationId,
            Actor = s.Actor,
            Fingerprint = fingerprint,
            SnapshotJson = Encode(snap),
            InputsJson = Encode(r),
            DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(snap.Definition.Settings.DeadlineSeconds)
        };
        if (r.FactoryAdmission != null) await FactoryWorkflowFence.Bind(db, services, s, u, run, snap, r.FactoryAdmission, ct);
        db.DurableWorkflows.Add(run);
        for (var i = 0; i < 5; i++) db.WorkflowCheckpoints.Add(new() { Id = Guid.NewGuid(), RunId = run.Id, Number = i, Kind = WorkflowRules.Sequence[i], OperationId = Guid.NewGuid() });
        Audit(s, run.Id, "queued"); await Save(ct); await tx.CommitAsync(ct); return await InspectAsync(s, run.Id, ct);
    }
    private async Task<bool> Rights(CoreScope s, DurableWorkflow run, CancellationToken ct)
    {
        try { await FactoryWorkflowFence.Validate(db, services, new(run.TenantId, run.Actor), run.Id, ct); }
        catch (Exception e) when (e is RuntimeValidationException or RightsBlockedException or EvidenceBlockedException or CoreAccessException) { return false; }
        var snap = Decode<WorkflowSnapshot>(run.SnapshotJson);
        var retrieve = await db.WorkflowCheckpoints.AsNoTracking().SingleAsync(x => x.RunId == run.Id && x.Number == 0, ct);
        var ids = retrieve.ResultJson == null ? Decode<StartWorkflow>(run.InputsJson).SourceSnapshotIds ?? [] : Decode<WorkflowEvidence>(retrieve.ResultJson).SourceIds;
        foreach (var id in ids)
        {
            var result = await new SourceRightsGuard(db).DecideAsync(s, id, new("Generate", snap.Prompt.Settings.Locale, "IN", "Text", Adaptation: true, AiProcessing: true), ct);
            if (!result.Decision.Allowed) return false;
        }
        var input = Decode<StartWorkflow>(run.InputsJson);
        if (input.EvidenceContentRevisionId is Guid content)
        {
            var use = new IntendedUse("Generate", snap.Prompt.Settings.Locale, "IN", "Text", Adaptation: true, AiProcessing: true);
            try { await new SourceRightsGuard(db).CheckAsync(s, content, use, ct); } catch (RightsBlockedException) { return false; } catch (CoreAccessException) { return false; }
            // Reuse M05's transaction-aware business gate, retaining the parent lease/review transaction.
            var gate = await new EvidenceEngine(db).CheckContentAsync(s, content, use, ct);
            if (!gate.Eligible) return false;
        }
        return true;
    }
    public async Task<DurableWorkflowView> InspectAsync(CoreScope s, Guid id, CancellationToken ct)
    {
        await Access(s, null, false, ct);
        var run = await db.DurableWorkflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        var allowed = await Rights(s, run, ct); var rows = await db.WorkflowCheckpoints.AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.Number).ToListAsync(ct);
        // Resolve a persisted operation binding even if the workflow process died before checkpoint completion.
        var draft = rows.Single(x => x.Kind == "draft");
        draft.AiRunId ??= await db.AiRuns.Where(x => x.TenantId == s.TenantId && x.OperationId == draft.OperationId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var decisions = await db.WorkflowDecisions.AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.CreatedUtc).ToListAsync(ct);
        return new(run.Id, run.OperationId, run.State, run.Outcome, run.NextStep, run.DeadlineUtc, Decode<WorkflowSnapshot>(run.SnapshotJson),
            Decode<StartWorkflow>(run.InputsJson), rows.Select(x => new WorkflowStepView(x.Id, x.Number, x.Kind, x.OperationId, x.AiRunId, x.State, x.Outcome, x.Attempts, x.LeaseUntilUtc, allowed ? x.ResultJson : null)).ToArray(),
            decisions.Select(x => new WorkflowDecisionView(x.Id, x.Action, x.Actor, x.EvidenceReference)).ToArray(), Convert.ToBase64String(run.RowVersion), !allowed);
    }
    public async Task<IReadOnlyList<DurableWorkflowView>> RunsAsync(CoreScope s, CancellationToken ct)
    {
        await Access(s, null, false, ct); var ids = await db.DurableWorkflows.AsNoTracking().Where(x => x.TenantId == s.TenantId).OrderByDescending(x => x.CreatedUtc).Take(30).Select(x => x.Id).ToListAsync(ct);
        var result = new List<DurableWorkflowView>(); foreach (var id in ids) result.Add(await InspectAsync(s, id, ct)); return result;
    }
    public async Task<DurableWorkflowView> ExportCheckpointAsync(CoreScope s, Guid id, CancellationToken ct)
    {
        var view = await InspectAsync(s, id, ct);
        if (view.Steps.Any(x => x.State == "Running") || view.State is "Queued" or "Running") throw new RuntimeConflictException("Pause or drain before exporting compatible checkpoints.");
        return view;
    }
    public async Task<DurableWorkflowView> ControlAsync(CoreScope s, Guid id, string action, ControlWorkflow r, CancellationToken ct)
    {
        if (action is not ("pause" or "resume" or "cancel" or "review") || r.OperationId == Guid.Empty ||
            r.EvidenceReference is not { Length: > 0 and <= 1000 } || !r.EvidenceReference.StartsWith("manual:") && !r.EvidenceReference.StartsWith("fixture:"))
            throw new RuntimeValidationException("Explicit control operation/evidence required.");
        await Access(s, null, true, ct);
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(ct);
        var run = await db.DurableWorkflows.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        var fingerprint = Hash(new { action, r.Approve, r.EvidenceReference }); var old = await db.WorkflowDecisions.SingleOrDefaultAsync(x => x.RunId == id && x.OperationId == r.OperationId, ct);
        if (old != null) { if (old.Actor != s.Actor || old.Fingerprint != fingerprint) throw new RuntimeConflictException("Control replay differs."); await tx.CommitAsync(ct); return await InspectAsync(s, id, ct); }
        if (Convert.ToBase64String(run.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
        if (run.State is "Completed" or "Cancelled" or "Rejected" or "Failed") throw new RuntimeConflictException("Run is terminal.");
        if (action == "cancel") { await CancelRun(db, run, ct); }
        else if (action == "pause")
        {
            if (run.State != "Queued") throw new RuntimeConflictException("Only pause at a completed step boundary.");
            run.State = "Paused"; run.Outcome = "OperatorPaused";
        }
        else if (action == "resume")
        {
            if (run.State is not ("Paused" or "Blocked") || DateTimeOffset.UtcNow >= run.DeadlineUtc) throw new RuntimeConflictException("Only a nonexpired boundary pause resumes.");
            if (!await Rights(s, run, ct)) throw new RightsBlockedException("Current workflow evidence is ineligible.");
            if (run.State == "Blocked")
            {
                var blocked = await db.WorkflowCheckpoints.SingleAsync(x => x.RunId == id && x.Number == run.NextStep, ct);
                var bound = await db.AiRuns.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == blocked.OperationId, ct);
                if (bound == null || bound.State != "Completed" || bound.Outcome != "Success" || bound.OutputJson == null) throw new RuntimeConflictException("Reconcile original AI operation; no new submission allowed.");
                blocked.State = "Pending";
            }
            run.State = "Queued"; run.Outcome = "";
        }
        else
        {
            if (run.State != "AwaitingReview" || run.NextStep != 4) throw new RuntimeConflictException("Workflow has not reached mandatory review.");
            if (DateTimeOffset.UtcNow >= run.DeadlineUtc) throw new RuntimeConflictException("Original workflow deadline expired.");
            if (r.Approve && !await Rights(s, run, ct)) throw new RightsBlockedException("Current workflow evidence is ineligible.");
            var step = await db.WorkflowCheckpoints.SingleAsync(x => x.RunId == id && x.Number == 4, ct);
            step.State = "Completed"; step.Outcome = r.Approve ? "HumanApproved" : "HumanRejected"; step.ResultJson = Encode(new { approved = r.Approve, reviewer = s.Actor, evidence = r.EvidenceReference });
            run.NextStep = 5; run.State = r.Approve ? "Completed" : "Rejected"; run.Outcome = step.Outcome;
        }
        db.WorkflowDecisions.Add(new() { Id = Guid.NewGuid(), RunId = id, OperationId = r.OperationId, Actor = s.Actor, Fingerprint = fingerprint, Action = action, EvidenceReference = r.EvidenceReference });
        Audit(s, id, action); await Save(ct); await tx.CommitAsync(ct); return await InspectAsync(s, id, ct);
    }
    internal static async Task CancelRun(CoreDbContext db, DurableWorkflow run, CancellationToken ct)
    {
        run.State = "Cancelled"; run.Outcome = "Cancelled";
        foreach (var step in await db.WorkflowCheckpoints.Where(x => x.RunId == run.Id && x.State != "Completed").ToListAsync(ct))
        { step.State = "Cancelled"; step.Outcome = "Cancelled"; step.LeaseToken = null; step.LeaseUntilUtc = null; }
    }

}
