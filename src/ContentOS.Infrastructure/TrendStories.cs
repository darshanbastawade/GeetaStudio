using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class TrendWorkbench
{
    public async Task<TrendStoryView> PrepareAsync(CoreScope s, Guid u, PrepareTrendStory r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "prepare", r, "Admin", async () =>
        {
            if (r.Format is not ("OriginalFiction" or "SourcedEventExplainer" or "CalendarLesson") || r.Channels.Length is < 1 or > 10 || r.Version < 1) throw new RuntimeValidationException("Explicit bounded story format/version/channels required.");
            CoreRules.Text(r.Key, 100, "Brief key"); CoreRules.Text(r.Analogy, 2000, "Analogy"); CoreRules.Text(r.FictionalHumor, 2000, "Fictional humor");
            var teaching = await Teaching(s, u, r.TeachingId, ct); if (teaching.ReviewStatus != "Approved" || teaching.Outcome != "Matched") throw new EvidenceBlockedException("ReviewedSuitableTeachingRequired"); await TrendFence.AssertTeaching(db, teaching, ct);
            var cluster = await Cluster(s, u, teaching.ClusterId, ct); await TrendFence.AssertCluster(db, cluster, ct);
            var old = await db.TrendStoryBriefs.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (r.Version != (old?.Version ?? 0) + 1) throw new RuntimeConflictException("Append the next brief version.");
            if (r.Plan.Briefs.Length is < 1 or > 4 || r.Plan.Briefs.Any(x => x.Formats.Length != 1)) throw new RuntimeValidationException("Up to four useful audience packages; one explicit format per package.");
            var row = new TrendStoryBrief { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ClusterId = cluster.Id, TeachingId = teaching.Id, Key = r.Key, Version = r.Version, RequestJson = Json(r) }; db.TrendStoryBriefs.Add(row); return row.Id;
        }, ct);
        var staged = await Story(s, u, id, ct);
        if (staged.State != "Preparing") return await StoryView(s, u, staged, ct);
        var stagedCluster = await Cluster(s, u, staged.ClusterId, ct); await TrendFence.AssertCluster(db, stagedCluster, ct);
        // Shared M11 operation has its own durable replay. A crash leaves Preparing; retry resumes the same plan.
        var plan = await factory.RegisterPlanAsync(s, u, r.Plan with { OperationId = Derived(id, "plan"), Key = "m15-" + id.ToString("N"), Version = 1 }, ct);
        await Command(s, u, Derived(r.OperationId, "complete"), "prepare-complete", new { BriefId = id, PlanId = plan.Id }, "Admin", async () =>
        {
            var row = await Story(s, u, id, ct); if (row.State != "Preparing") return id;
            var cluster = await Cluster(s, u, row.ClusterId, ct); await TrendFence.AssertCluster(db, cluster, ct);
            var teaching = await Teaching(s, u, row.TeachingId, ct); await TrendFence.AssertTeaching(db, teaching, ct); var match = Read<TeachingSnapshot>(teaching.SnapshotJson);
            var baseLesson = await lessons.InspectAsync(s, u, teaching.LessonVariantId, ct);
            var claim = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == cluster.ClaimRevisionId, ct);
            var signals = (await db.TrendCandidates.AsNoTracking().Where(x => x.ClusterId == cluster.Id).ToListAsync(ct)).Select(x => Read<TrendSignal>(x.SnapshotJson)).ToArray();
            if (r.Format == "CalendarLesson" && !signals.Any(x => x.Kind == "EventCalendar")) throw new EvidenceBlockedException("ApprovedCalendarContextRequired");
            var events = await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == claim.Id).Select(x => x.SnapshotId).Distinct().ToArrayAsync(ct);
            var packages = new List<TrendPackage>(); var template = plan.Snapshot!.GenerationWorkflow == null ? templates.Single(x => x.Key == plan.Snapshot.Rules.Adapter) : null;
            foreach (var input in plan.Snapshot!.Briefs)
            {
                if (claim.Locale != input.Brief.Cast.Locale) throw new EvidenceBlockedException("ReviewedLocalizedEventClaimRequired");
                if (input.Lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId != baseLesson.Snapshot.Bundle.Request.AnchorClaimRevisionId ||
                    input.Lesson.Snapshot.Bundle.Request.InterpretationClaimRevisionId != baseLesson.Snapshot.Bundle.Request.InterpretationClaimRevisionId)
                    throw new EvidenceBlockedException("AudiencePackagesMustPreserveTheReviewedTeachingConnection");
                foreach (var source in events) if (!(await new SourceRightsGuard(db).DecideAsync(s, source, new("Adapt", input.Brief.Cast.Locale, "IN", "Text", Adaptation: true), ct)).Decision.Allowed)
                    throw new RightsBlockedException("TrendAdaptationRightsRequired");
                var output = template?.Render(new(input, input.Brief.Formats[0]), ct) ?? new FactoryRenderResult("Original script pending shared AI factory generation and editorial review.", input.Brief.LearningGoal, "ai-workflow.v1");
                packages.Add(new(input.Brief.Key, input.Brief.Cast.AudienceId, input.Brief.Cast.Locale, input.Brief.LearningGoal, input.Cast,
                    output.OriginalTreatment, output.Body, input.Brief.Formats[0].EstimatedMediaCost));
            }
            if (packages.Select(x => (x.AudienceId, x.Locale)).Distinct().Count() != packages.Count) throw new RuntimeValidationException("Only distinct useful audience/locale variants.");
            var snapshot = new TrendStorySnapshot(cluster.Id, teaching.Id, cluster.CheckedFingerprint, r.Format, claim.Statement, claim.Uncertainty, r.Analogy,
                baseLesson.Snapshot.Bundle.Interpretation, baseLesson.Snapshot.Bundle.InterpretationAuthor, baseLesson.Snapshot.Bundle.Tradition, r.FictionalHumor,
                events, baseLesson.Snapshot.Bundle.SourceIds, match.Retrieval, cluster.CheckedUtc!.Value, cluster.ExpiresUtc!.Value, r.Channels, packages.ToArray(), []);
            snapshot = snapshot with { Packages = snapshot.Packages.Select(x => x with { Script = TrendFence.Render(snapshot, new(x.Script, x.Hook, plan.Snapshot.Rules.Adapter)).Body }).ToArray() };
            if (snapshot.Packages.Where((x, i) => x.Script.Length > plan.Snapshot.Briefs[i].Brief.Formats[0].MaxCharacters).Any()) throw new RuntimeValidationException("Factual context and script exceed the pinned format budget.");
            row.FactoryPlanId = plan.Id; row.SnapshotJson = Json(snapshot); row.State = "Draft"; return id;
        }, ct);
        return await StoryView(s, u, await Story(s, u, id, ct), ct);
    }
    private Task<TrendStoryView> StoryView(CoreScope s, Guid u, TrendStoryBrief row, CancellationToken ct) => Project(async () =>
    {
        row = await Story(s, u, row.Id, ct);
        var blocks = new List<string>(); try { await TrendFence.AssertBrief(db, row, false, ct); } catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { blocks.Add(e.Message); }
        var snapshot = row.SnapshotJson.Length == 0 || blocks.Count > 0 ? null : Read<TrendStorySnapshot>(row.SnapshotJson);
        if (snapshot != null)
            foreach (var id in snapshot.EventSources.Concat(snapshot.ScriptureSources))
                if (!(await new SourceRightsGuard(db).DecideAsync(s, id, new("Read", snapshot.Packages[0].Locale, "IN", "Text"), ct)).Decision.Allowed) { snapshot = null; blocks.Add("SourceOutputWithheld"); break; }
        var guestProposals = new List<string>();
        if (row.State == "Preparing")
            foreach (var brief in Read<PrepareTrendStory>(row.RequestJson).Plan.Briefs)
                guestProposals.AddRange((await registry.CastAsync(s, u, brief.Cast, ct)).MissingRoles);
        return new TrendStoryView(row.Id, row.FactoryPlanId, row.Version, row.State, snapshot, Convert.ToBase64String(row.RowVersion), blocks.ToArray(), guestProposals.Distinct().ToArray());
    }, ct);
    public Task<TrendStoryView[]> StoriesAsync(CoreScope s, Guid u, CancellationToken ct)
        => Project(async () => { await Access(s, u, "Read", ct); var result = new List<TrendStoryView>(); foreach (var row in await db.TrendStoryBriefs.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)) result.Add(await StoryView(s, u, row, ct)); return result.ToArray(); }, ct);
    public async Task ReviewStoryAsync(CoreScope s, Guid u, Guid id, TrendReview r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "brief-review", new { id, r }, "Review", async () =>
    {
        var row = await Story(s, u, id, ct); Expected(row, r.ExpectedRowVersion); TrendRules.Attest(r.Mode, r.EvidenceReference);
        if (r.Decision is not ("Approve" or "Reject" or "Hold")) throw new RuntimeValidationException("Approve/Hold/Reject required.");
        if (r.Decision == "Approve")
        {
            await TrendFence.AssertBrief(db, row, false, ct);
            var cluster = await Cluster(s, u, row.ClusterId, ct);
            var configurations = await (from candidate in db.TrendCandidates join connector in db.TrendConnectors on candidate.ConnectorId equals connector.Id where candidate.ClusterId == cluster.Id select connector.SnapshotJson).ToListAsync(ct);
            var snapshot = Read<TrendStorySnapshot>(row.SnapshotJson);
            if (r.Mode == "Fixture" && configurations.Any(x => Read<RegisterTrendConnector>(x).Mode != "Fixture"))
                throw new EvidenceBlockedException("HumanBriefReviewRequiredForNonFixtureEvents");
            if (snapshot.Packages.Any(x => x.Cast.Cast.Length == 0)) throw new EvidenceBlockedException("SuitableCastRequired");
        }
        row.State = r.Decision == "Approve" ? "Approved" : r.Decision == "Hold" ? "Held" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; return id;
    }, ct);
    public async Task BindAsync(CoreScope s, Guid u, BindTrendStory r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "bind", r, "Admin", async () =>
    {
        var row = await Story(s, u, r.BriefId, ct); await TrendFence.AssertBrief(db, row, true, ct);
        var item = await db.EditorialItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.EditorialItemId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        var revision = await EditorialBinding.Revision(db, item, ct);
        if (!await db.TrendBindings.AnyAsync(x => x.BriefId == row.Id && x.ContentRevisionId == revision.Id, ct)) TrendFence.Lineage(db, row, revision.Id, revision.Body); return revision.Id;
    }, ct);
    private async Task Invalidate(TrendCluster row, string reason, CancellationToken ct)
    {
        foreach (var brief in await db.TrendStoryBriefs.Where(x => x.ClusterId == row.Id).ToListAsync(ct))
        {
            if (brief.State is not ("Preparing" or "Rejected")) brief.State = "Held";
            if (!db.TrendRemediations.Local.Any(x => x.ClusterId == row.Id && x.BriefId == brief.Id && x.State == "Open") && !await db.TrendRemediations.AnyAsync(x => x.ClusterId == row.Id && x.BriefId == brief.Id && x.State == "Open", ct))
                db.TrendRemediations.Add(new() { Id = Guid.NewGuid(), TenantId = row.TenantId, UniverseId = row.UniverseId, ClusterId = row.Id, BriefId = brief.Id, Reason = reason });
        }
    }
    public async Task ChangeAsync(CoreScope s, Guid u, Guid id, TrendChange r, CancellationToken ct)
    {
        await Command(s, u, r.OperationId, "change", new { id, r }, "Review", async () =>
        {
            var row = await Cluster(s, u, id, ct); Expected(row, r.ExpectedRowVersion); CoreRules.Text(r.Reason, 2000, "Correction reason");
            TrendRules.Attest(r.EvidenceReference.StartsWith("fixture:") ? "Fixture" : "Manual", r.EvidenceReference);
            if (r.Action is not ("Correct" or "Withdraw")) throw new RuntimeValidationException("Correct or Withdraw required.");
            row.State = r.Action == "Correct" ? "Corrected" : "Withdrawn"; await Invalidate(row, r.Reason, ct); return id;
        }, ct);
        await DeliverRemediations(s, u, ct);
    }
    private async Task DeliverRemediations(CoreScope s, Guid u, CancellationToken ct)
    {
        var tasks = await db.TrendRemediations.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.State == "Open").Take(100).ToListAsync(ct);
        foreach (var task in tasks)
        {
            var cluster = await Cluster(s, u, task.ClusterId, ct); if (!cluster.ClaimRevisionId.HasValue) continue;
            var brief = await Story(s, u, task.BriefId, ct); var teaching = await Teaching(s, u, brief.TeachingId, ct); var lesson = await db.LessonVariants.AsNoTracking().SingleAsync(x => x.Id == teaching.LessonVariantId, ct); var bundle = Read<LessonVariantSnapshot>(lesson.SnapshotJson).Bundle; var cause = cluster.ClaimRevisionId.Value;
            var selectedClaimChanged = false;
            foreach (var claim in new[] { bundle.Request.AnchorClaimRevisionId, bundle.Request.InterpretationClaimRevisionId }) { try { await new EvidenceEngine(db).RequireLessonClaimAsync(s, u, claim, new("Read", "en-IN", "IN", "Text"), ct); } catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { cause = claim; selectedClaimChanged = true; break; } }
            if (!selectedClaimChanged && (task.Reason is "SourceObservationChanged" or "SourceObservationCapacityReached" || cluster.State == "Verified" && !await TrendFence.TeachingRetrievalAllowed(db, teaching, ct)))
            {
                // A changed extra search hit requires review without falsely correcting the valid event claim.
                await Command(s, u, Derived(task.Id, "retrieval-review"), "retrieval-review-required", new { task.Id }, "Review", async () =>
                { var row = await db.TrendRemediations.SingleAsync(x => x.Id == task.Id, ct); row.State = "EditorialReview"; return task.Id; }, ct);
                continue;
            }
            var correction = await editorial.CorrectAsync(s, u, new(Derived(task.Id, "editorial"), cause, null, task.Reason, "fixture:trend-remediation-not-external-takedown"), ct);
            await Command(s, u, Derived(task.Id, "delivered"), "remediation-delivered", new { task.Id, correction }, "Review", async () =>
            { var row = await db.TrendRemediations.SingleAsync(x => x.Id == task.Id, ct); row.State = "EditorialReview"; row.EditorialCorrectionId = correction; return task.Id; }, ct);
        }
    }
    public async Task RecheckAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, "Review", ct);
        var ids = await db.TrendClusters.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.State == "Verified").Select(x => x.Id).Take(100).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            var row = await Cluster(s, u, id, ct); var proof = await TrendFence.Cluster(db, row, ct);
            if (proof.Blocks.Length == 0 && proof.Fingerprint == row.CheckedFingerprint) continue;
            await Command(s, u, Derived(id, "recheck:" + proof.Fingerprint + row.ExpiresUtc), "automatic-recheck", new { id, proof.Fingerprint }, "Review", async () =>
            {
                var current = await Cluster(s, u, id, ct); var now = await TrendFence.Cluster(db, current, ct);
                if (now.Blocks.Length > 0 || now.Fingerprint != current.CheckedFingerprint) { current.State = "Held"; await Invalidate(current, "Evidence changed or expired: " + string.Join(", ", now.Blocks), ct); }
                return id;
            }, ct);
        }
        foreach (var brief in await db.TrendStoryBriefs.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && (x.State == "Approved" || x.State == "Draft")).Take(100).ToArrayAsync(ct))
        {
            var teaching = await Teaching(s, u, brief.TeachingId, ct); string? reason = null; try { await TrendFence.AssertTeaching(db, teaching, ct); } catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { reason = "Teaching evidence changed: " + e.Message; }
            if (reason == null) continue;
            await Command(s, u, Derived(brief.Id, "teaching-held"), "teaching-invalidated", new { brief.Id, reason }, "Review", async () => { var current = await Story(s, u, brief.Id, ct); current.State = "Held"; if (!await db.TrendRemediations.AnyAsync(x => x.BriefId == brief.Id && x.State == "Open", ct)) db.TrendRemediations.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ClusterId = current.ClusterId, BriefId = current.Id, Reason = reason }); return current.Id; }, ct);
        }
        await DeliverRemediations(s, u, ct);
    }
    public async Task<TrendRemediationView[]> RemediationsAsync(CoreScope s, Guid u, CancellationToken ct)
    { await Access(s, u, "Read", ct); return await db.TrendRemediations.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => new TrendRemediationView(x.Id, x.ClusterId, x.BriefId, x.Reason, x.State, x.EditorialCorrectionId)).ToArrayAsync(ct); }
    public async Task<TrendStats> StatsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, "Read", ct); var clusters = await db.TrendClusters.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToListAsync(ct);
        var briefs = await db.TrendStoryBriefs.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToListAsync(ct);
        var paid = await (from batch in db.FactoryBatches join work in db.FactoryWorks on batch.Id equals work.BatchId join step in db.WorkflowCheckpoints on work.WorkflowId equals (Guid?)step.RunId join ai in db.AiRuns on step.OperationId equals ai.OperationId where batch.TenantId == s.TenantId && batch.UniverseId == u && briefs.Select(x => x.FactoryPlanId).Contains(batch.PlanId) && ai.TenantId == s.TenantId && step.Kind == "draft" select new { ai.Spent, ai.Reserved, ai.SnapshotJson }).ToListAsync(ct);
        var currencies = paid.Select(x => Read<AiExecutionSnapshot>(x.SnapshotJson).Route.Currency).Distinct().ToArray();
        if (currencies.Length > 1) throw new RuntimeValidationException("Trend text costs span currencies; use the currency-scoped analytics report.");
        return new(await db.TrendCandidates.CountAsync(x => x.TenantId == s.TenantId && x.UniverseId == u, ct), clusters.Count(x => x.State == "Verified"), clusters.Count(x => x.State == "Held"),
            clusters.Count(x => x.State == "Rejected"), briefs.Count(x => x.State == "Approved"), briefs.Count(x => x.State == "Rejected"),
            briefs.Where(x => x.SnapshotJson.Length > 0).Sum(x => Read<TrendStorySnapshot>(x.SnapshotJson).Packages.Sum(p => p.EstimatedProductionCost)),
            await db.TrendRemediations.CountAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.State != "Resolved", ct),
            paid.Sum(x => x.Spent), null)
        { OutstandingTextGenerationCost = paid.Sum(x => x.Reserved), TextGenerationCurrency = currencies.SingleOrDefault() };
    }
}
