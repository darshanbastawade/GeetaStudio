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

public sealed partial class DraftFactory(CoreDbContext db, ILessonEngine lessons, IUniverseRegistry registry, IEnumerable<IFactoryTemplate> templates, IServiceProvider services) : IDraftFactory
{
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value)!;
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(value))));
    private async Task Access(CoreScope s, Guid u, bool write, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct) || !await db.Members.AnyAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor && (!write || x.Permission == "Administrator"), ct)) throw new CoreAccessException();
    }
    private async Task Lock(Guid tenant, CancellationToken ct)
    {
        await FactoryWorkflowFence.WorkflowLock(db, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Factory:" + tenant},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Factory lock unavailable',1;", ct);
    }
    private async Task<Guid> Command<T>(CoreScope s, Guid u, Guid op, string kind, T request, Func<Task<Guid>> action, CancellationToken ct)
    {
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable operation required.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(s.TenantId, ct); await Access(s, u, true, ct);
        var fingerprint = Hash(new { u, request }); var old = await db.FactoryOperations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (old != null) { if (old.Actor != s.Actor || old.Kind != kind || old.Fingerprint != fingerprint) throw new RuntimeConflictException("Factory replay differs."); return old.ResultId; }
        var id = await action(); db.FactoryOperations.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = op, Actor = s.Actor, Kind = kind, Fingerprint = fingerprint, ResultId = id });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = "factory." + kind });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
        await tx.CommitAsync(ct); return id;
    }
    private async Task<FactoryPlan> Plan(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.FactoryPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<LessonVariantView> Lesson(CoreScope s, Guid u, Guid id, bool generation, CancellationToken ct)
    {
        var v = await lessons.InspectAsync(s, u, id, ct); if (v.OutputWithheld || v.Sections == null || v.ReviewStatus != "Approved") throw new EvidenceBlockedException("ReviewedLessonRequired");
        foreach (var source in v.Snapshot.Bundle.SourceIds) { var decision = await new SourceRightsGuard(db).DecideAsync(s, source, new(generation ? "Adapt" : "Read", v.Snapshot.Locale.Rules.Locale!.Locale, "IN", "Text", Adaptation: generation), ct); if (!decision.Decision.Allowed) throw new RightsBlockedException(decision.Decision.Reason); }
        return v;
    }
    private async Task<FactoryBriefSnapshot> Resolve(CoreScope s, Guid u, FactoryBrief b, CancellationToken ct)
    {
        var lesson = await Lesson(s, u, b.LessonVariantId, true, ct); var ls = lesson.Snapshot;
        if (ls.Audience.Rules.Audience!.AudienceId != b.Cast.AudienceId || ls.Locale.Rules.Locale!.Locale != b.Cast.Locale || !b.Concepts.All(lesson.Sections!.Concepts.Contains) || !lesson.Sections!.LearningGoals.Contains(b.LearningGoal)) throw new RuntimeValidationException("Brief must bind the reviewed lesson's audience, locale, concepts and learning goal.");
        var text = ls.Bundle.Request.SanskritTextId ?? await db.ClaimSpans.Where(x => x.ClaimRevisionId == ls.Bundle.Request.AnchorClaimRevisionId && x.CorpusTextId != null).Select(x => x.CorpusTextId).FirstOrDefaultAsync(ct);
        if (text.HasValue)
        {
            var verse = await (from t in db.CorpusTexts join v in db.CorpusVerses on t.VerseId equals v.Id where t.Id == text select v).SingleAsync(ct);
            if (verse.Chapter != b.Chapter || verse.Verse != b.Verse) throw new RuntimeValidationException("Chapter and verse must come from approved evidence.");
        }
        else
        {
            var anchorType = await db.ClaimRevisions.Where(x => x.Id == ls.Bundle.Request.AnchorClaimRevisionId).Select(x => x.Type).SingleAsync(ct);
            var bound = await (from span in db.ClaimSpans
                               join t in db.CorpusTexts on new { span.SnapshotId, span.Sha256 } equals new { t.SnapshotId, t.Sha256 }
                               join v in db.CorpusVerses on t.VerseId equals v.Id
                               join manifest in db.CorpusManifests on v.ManifestId equals manifest.Id
                               where span.ClaimRevisionId == ls.Bundle.Request.AnchorClaimRevisionId && span.Relation == "Supporting" && t.Kind == "HistoricalSource" && v.ReviewStatus == "Approved"
                               && manifest.MaterialKind == "History" && manifest.UniverseId == u && manifest.TenantId == s.TenantId && v.Chapter == b.Chapter && v.Verse == b.Verse
                               select v.Id).AnyAsync(ct);
            if (anchorType is not ("SourceQuotation" or "HistoricalClaim") || !bound) throw new EvidenceBlockedException("HistoricalFactoryOrdinalMustBindReviewedSourceEvidence");
        }
        var plan = await registry.CastAsync(s, u, b.Cast, ct); if (plan.MissingRoles.Length != 0 || plan.Selected.Any(c => c.Character.Status != "Active" || c.Character.Profile.ReviewStatus != "Approved" || c.Role == "Lead" && !ls.Writing.Rules.Writing!.EligibleLifeStages.Contains(c.Character.Profile.Rules.LifeStage))) throw new RuntimeValidationException("Register/activate compatible characters; use the approved writing life stages.");
        if (plan.Selected.Length > ls.Writing.Rules.Writing!.MaxCast || ls.Writing.Rules.Writing.EligibleCharacters.Length > 0 && plan.Selected.Any(c => !ls.Writing.Rules.Writing.EligibleCharacters.Contains(c.Character.CharacterId))) throw new RuntimeValidationException("Cast exceeds the writing profile bounds.");
        var relationships = (await registry.RelationshipsAsync(s, u, ct)).Where(x => b.RelationshipRevisionIds.Contains(x.Id)).ToArray();
        if (relationships.Length != b.RelationshipRevisionIds.Distinct().Count() || relationships.Any(x => x.ReviewStatus != "Approved" || !plan.Selected.Any(c => c.Character.CharacterId == x.Rules.FromCharacterId) || !plan.Selected.Any(c => c.Character.CharacterId == x.Rules.ToCharacterId))) throw new RuntimeValidationException("Relationships must be approved and bind selected characters.");
        var bible = (await registry.BiblesAsync(s, u, ct)).Single(x => x.Id == b.Cast.BibleRevisionId);
        var pinned = b.Cast with { PinnedChoices = plan.Selected.Select(c => new PinnedCastChoice(c.Character.CharacterId, c.Role, c.Presentation?.Id)).ToArray() };
        var beats = plan.Selected.Select(c => new RegistryStoryBeat(c.Character.CharacterId, "FictionalDialogue", lesson.Sections!.Example + " " + lesson.Sections.PracticalAction, [], [], c.Role == "Lead")).ToArray();
        var snapshot = new RegistryEpisodeSnapshot(pinned, "None", bible, plan.Selected, relationships, beats, false, false, b.LessonVariantId, null, false, "Original fictional treatment, not scripture/history.", "");
        RegistryRules.Story(snapshot);
        foreach (var format in b.Formats) RegistryRules.Story(snapshot with { Beats = snapshot.Beats.Select(beat => beat with { Text = beat.Text + " " + format.Instruction }).ToArray() });
        return new(b with { Cast = pinned }, lesson, snapshot);
    }
    private async Task Recheck(CoreScope s, Guid u, FactoryBriefSnapshot pinned, bool generation, bool cast, CancellationToken ct)
    {
        await Lesson(s, u, pinned.Brief.LessonVariantId, generation, ct);
        if (cast) { var current = await registry.CastAsync(s, u, pinned.Brief.Cast, ct); if (current.MissingRoles.Length != 0 || current.Selected.Any(c => !pinned.Cast.Cast.Any(old => old.Character.Profile.Id == c.Character.Profile.Id && old.Character.CharacterId == c.Character.CharacterId))) throw new RuntimeValidationException("Pinned cast changed or retired; append a reviewed plan revision."); }
    }
    private async Task<FactoryPlanView> View(CoreScope s, FactoryPlan row, CancellationToken ct)
    {
        var snap = Read<FactoryPlanSnapshot>(row.SnapshotJson); try { foreach (var b in snap.Briefs) await Recheck(s, row.UniverseId, b, false, false, ct); return new(row.Id, snap, row.ReviewStatus, row.ReviewEvidence, Convert.ToBase64String(row.RowVersion), false); }
        catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException) { return new(row.Id, null, row.ReviewStatus, row.ReviewEvidence, Convert.ToBase64String(row.RowVersion), true); }
    }
    public async Task<FactoryPlanView> RegisterPlanAsync(CoreScope s, Guid u, RegisterFactoryPlan r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "plan", r, async () =>
        {
            FactoryRules.Plan(r); if (r.WorkflowDefinitionId == null && !templates.Any(t => t.Key == r.Adapter) || r.WorkflowDefinitionId != null && r.Adapter != "ai-workflow.v1") throw new RuntimeValidationException("Unsupported deployed local template.");
            var prior = await db.FactoryPlans.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (r.Version != (prior?.Version ?? 0) + 1) throw new RuntimeConflictException("Append an explicit plan version.");
            var inputs = new List<FactoryBriefSnapshot>(); foreach (var b in r.Briefs) inputs.Add(await Resolve(s, u, b, ct));
            var generation = r.WorkflowDefinitionId is Guid definition ? await services.GetRequiredService<WorkflowRuntime>().Snapshot(s, u, definition, ct) : null;
            if (generation != null) ValidateGenerationPlan(generation, inputs);
            var row = new FactoryPlan { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(new FactoryPlanSnapshot(r, inputs.ToArray()) { GenerationWorkflow = generation }) }; db.FactoryPlans.Add(row); return row.Id;
        }, ct); return await View(s, await Plan(s, u, id, ct), ct);
    }
    public async Task<IReadOnlyList<FactoryPlanView>> PlansAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var result = new List<FactoryPlanView>(); foreach (var row in await db.FactoryPlans.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderBy(x => x.Key).ThenBy(x => x.Version).Take(200).ToListAsync(ct)) result.Add(await View(s, row, ct)); return result;
    }
    public async Task ReviewPlanAsync(CoreScope s, Guid u, Guid id, FactoryReview r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "plan-review", new { id, r }, async () =>
    {
        RegistryRules.ReviewEvidence(r.EvidenceReference); var row = await db.FactoryPlans.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        if (Convert.ToBase64String(row.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException(); if (row.ReviewStatus != "Draft") throw new RuntimeConflictException("Plan review frozen.");
        foreach (var b in Read<FactoryPlanSnapshot>(row.SnapshotJson).Briefs) await Recheck(s, u, b, true, true, ct);
        row.ReviewStatus = r.Approve ? "Approved" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; return id;
    }, ct);
    public async Task<FactoryEstimate> EstimateAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        await Access(s, u, false, ct); var p = Read<FactoryPlanSnapshot>((await Plan(s, u, id, ct)).SnapshotJson); foreach (var b in p.Briefs) await Recheck(s, u, b, true, true, ct);
        var formats = p.Briefs.SelectMany(b => b.Brief.Formats).ToArray(); return new(id, formats.Length, (p.GenerationWorkflow?.Route.Settings.RunLimit ?? 0) * formats.Length, formats.Sum(f => f.EstimatedMediaCost), formats.Sum(f => f.ReviewMinutes), formats.Sum(f => f.MediaMinutes), formats.Sum(f => f.MaxCharacters)) { GenerationCurrency = p.GenerationWorkflow?.Route.Settings.Currency, EstimatedMediaCurrency = p.Briefs.First().Brief.Cast.Budget.Currency };
    }
    private async Task<FactoryBatchView> BatchView(FactoryBatch r, CancellationToken ct)
    {
        var rows = await db.FactoryWorks.AsNoTracking().Where(x => x.BatchId == r.Id).OrderBy(x => x.BriefKey).ThenBy(x => x.Format).ToArrayAsync(ct);
        var usage = await (from w in db.FactoryWorks join step in db.WorkflowCheckpoints on w.WorkflowId equals (Guid?)step.RunId join ai in db.AiRuns on step.OperationId equals ai.OperationId where w.BatchId == r.Id && ai.TenantId == r.TenantId && step.Kind == "draft" select new { ai.Spent, ai.Reserved }).ToListAsync(ct);
        var states = await db.DurableWorkflows.AsNoTracking().Where(x => rows.Select(w => w.WorkflowId).Contains(x.Id)).Select(x => new { x.Id, x.State }).ToDictionaryAsync(x => x.Id, x => x.State, ct);
        var plan = Read<FactoryPlanSnapshot>((await db.FactoryPlans.AsNoTracking().SingleAsync(x => x.Id == r.PlanId, ct)).SnapshotJson);
        return new(r.Id, r.BusinessKey, r.PlanId, r.State, r.Outcome, r.GeneratedCharacters, usage.Sum(x => x.Spent), Read<FactoryBudget>(r.BudgetJson), r.DeadlineUtc, Convert.ToBase64String(r.RowVersion),
            rows.Select(x => new FactoryWorkView(x.Id, x.BriefKey, x.Format, x.State, x.Attempts, x.ArtifactId, x.Outcome) { WorkflowId = x.WorkflowId, WorkflowState = x.WorkflowId.HasValue ? states.GetValueOrDefault(x.WorkflowId.Value) : null }).ToArray())
        { GenerationReserved = usage.Sum(x => x.Reserved), EstimatedGenerationCost = (plan.GenerationWorkflow?.Route.Settings.RunLimit ?? 0) * rows.Length };
    }
    public async Task<FactoryBatchView> StartAsync(CoreScope s, Guid u, StartFactoryBatch r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "start", r, async () =>
        {
            await TrendFence.AssertPlan(db, r.PlanId, ct); CoreRules.Text(r.BusinessKey, 100, "Business request"); FactoryRules.Budget(r.Budget); var p = await Plan(s, u, r.PlanId, ct); if (p.ReviewStatus != "Approved") throw new RuntimeValidationException("Review plan before starting.");
            var fingerprint = Hash(new { r.PlanId, r.Budget }); var old = await db.FactoryBatches.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.BusinessKey == r.BusinessKey, ct);
            if (old != null) { if (old.Actor != s.Actor || old.Fingerprint != fingerprint) throw new RuntimeConflictException("Business request changed; use an explicit new key/version."); return old.Id; }
            var estimate = await EstimateAsync(s, u, p.Id, ct); if (estimate.Items > r.Budget.MaxItems || estimate.MaximumCharacters > r.Budget.MaxGeneratedCharacters || estimate.GenerationCost > r.Budget.MaxGenerationCost) throw new RuntimeValidationException("Plan exceeds bounded generation budget.");
            var snap = Read<FactoryPlanSnapshot>(p.SnapshotJson); if (snap.GenerationWorkflow != null ? snap.GenerationWorkflow.Route.Settings.Currency != r.Budget.Currency : snap.Briefs.Any(b => b.Brief.Cast.Budget.Currency != r.Budget.Currency)) throw new RuntimeValidationException("Budget currency must match casting estimates.");
            var row = new FactoryBatch { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Actor = s.Actor, PlanId = p.Id, BusinessKey = r.BusinessKey, Fingerprint = fingerprint, BudgetJson = Json(r.Budget), DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(r.Budget.DeadlineSeconds) }; db.FactoryBatches.Add(row);
            foreach (var b in snap.Briefs.OrderByDescending(x => x.Brief.Priority).ThenBy(x => x.Brief.Position)) foreach (var f in b.Brief.Formats) db.FactoryWorks.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, BatchId = row.Id, BriefKey = b.Brief.Key, Format = f.Kind });
            return row.Id;
        }, ct); return await BatchView(await db.FactoryBatches.AsNoTracking().SingleAsync(x => x.Id == id, ct), ct);
    }
    public async Task<IReadOnlyList<FactoryBatchView>> BatchesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var result = new List<FactoryBatchView>(); foreach (var row in await db.FactoryBatches.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(200).ToListAsync(ct)) result.Add(await BatchView(row, ct)); return result;
    }
    public async Task ControlAsync(CoreScope s, Guid u, Guid id, FactoryControl r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "control", new { id, r }, async () =>
    {
        var row = await db.FactoryBatches.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException(); if (Convert.ToBase64String(row.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
        if (r.Action == "Pause" && row.State is "Queued" or "Running") row.State = "Paused";
        else if (r.Action == "Resume" && row.State == "Paused" && DateTimeOffset.UtcNow < row.DeadlineUtc) row.State = "Queued";
        else if (r.Action == "Cancel" && row.State is "Queued" or "Running" or "Paused") row.State = "Cancelled";
        else throw new RuntimeConflictException("Invalid batch transition; deadlines do not reset.");
        if (row.State == "Cancelled") foreach (var work in await db.FactoryWorks.Where(w => w.BatchId == id && (w.State == "Pending" || w.State == "Running" || w.State == "AwaitingWorkflow")).ToListAsync(ct)) { work.State = "Cancelled"; work.Outcome = "BatchCancelled"; work.LeaseToken = null; work.LeaseUntilUtc = null; }
        if (row.State == "Cancelled")
        {
            var children = await db.DurableWorkflows.Where(x => db.FactoryWorks.Any(w => w.BatchId == row.Id && w.WorkflowId == x.Id) && x.State != "Completed" && x.State != "Failed" && x.State != "Rejected" && x.State != "Cancelled").ToListAsync(ct);
            foreach (var child in children)
            {
                await WorkflowRuntime.CancelRun(db, child, ct);
                db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = child.Id, Action = "workflow.factory-cancelled" });
            }
        }
        row.Outcome = r.Action; return id;
    }, ct);
}
public static class FactoryServices { public static IServiceCollection AddDraftFactory(this IServiceCollection s) => s.AddScoped<IDraftFactory, DraftFactory>(); }
