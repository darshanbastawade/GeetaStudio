using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

internal static class TrendFence
{
    internal static async Task<(string Fingerprint, string[] Blocks, int Independent)> Cluster(CoreDbContext db, TrendCluster row, CancellationToken ct)
    {
        var candidates = await db.TrendCandidates.AsNoTracking().Where(x => x.ClusterId == row.Id).OrderBy(x => x.Id).ToListAsync(ct);
        var signals = candidates.Select(x => EditorialBinding.Read<TrendSignal>(x.SnapshotJson)).ToArray();
        var extracted = candidates.Select(x => EditorialBinding.Read<TrendExtraction>(x.ExtractionJson)).ToArray();
        var ids = extracted.SelectMany(x => x.SnapshotIds).Distinct().ToArray();
        var snapshots = await db.SourceSnapshots.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        var blocks = new List<string>();
        if (ids.Length == 0) blocks.Add("SubstanceUnavailable");
        if (signals.Any(x => x.PublishedUtc == null && x.UpdatedUtc == null)) blocks.Add("PublicationTimeUnavailable");
        if (signals.Where((x, i) => x.RequiresTranscript && extracted[i].TranscriptAccess != "AuthorizedTranscriptAvailable").Any()) blocks.Add("TranscriptUnavailable");
        if (signals.Any(x => (x.UpdatedUtc ?? x.PublishedUtc) < DateTimeOffset.UtcNow.AddDays(-7) || x.Kind != "EventCalendar" && x.EventUtc < DateTimeOffset.UtcNow.AddDays(-7))) blocks.Add("StaleStory");
        if (!row.ExpiresUtc.HasValue || row.ExpiresUtc <= DateTimeOffset.UtcNow) blocks.Add("RecheckRequired");
        if (signals.Any(x => x.ReportStatus != "Current")) blocks.Add("SourceCorrectedOrRetracted");
        if (signals.Any(x => x.Risks.Contains("Allegation"))) blocks.Add("UnsupportedAllegation");
        if (await db.TrendConnectors.AnyAsync(x => candidates.Select(c => c.ConnectorId).Contains(x.Id) && !x.Enabled, ct)) blocks.Add("SourceDisabled");
        var groups = new List<HashSet<string>>();
        for (var i = 0; i < signals.Length; i++)
        {
            var signal = signals[i]; if (extracted[i].SnapshotIds.Length == 0 || signal.ReportOrigin.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) continue;
            var keys = new HashSet<string> { "origin:" + signal.ReportOrigin.ToLowerInvariant() };
            foreach (var snapshot in snapshots.Where(x => extracted[i].SnapshotIds.Contains(x.Id))) keys.Add("document:" + snapshot.Sha256);
            var related = groups.Where(g => g.Overlaps(keys)).ToArray();
            foreach (var g in related) { keys.UnionWith(g); groups.Remove(g); }
            groups.Add(keys);
        }
        var primary = signals.Where((x, i) => x.Primary && extracted[i].SnapshotIds.Length > 0 && !x.ReportOrigin.Equals("Unknown", StringComparison.OrdinalIgnoreCase)).Any();
        if (!primary && groups.Count < 2) blocks.Add("IndependentCorroborationRequired");
        ClaimRevision? claim = null; EvidenceClaim? head = null;
        if (row.ClaimRevisionId.HasValue)
        {
            claim = await db.ClaimRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.ClaimRevisionId, ct);
            if (claim != null) head = await db.Claims.AsNoTracking().SingleOrDefaultAsync(x => x.Id == claim.ClaimId && x.TenantId == row.TenantId && x.UniverseId == row.UniverseId, ct);
        }
        if (claim == null || head == null || claim.Type != "NewsClaim" || claim.ReviewStatus != "Approved" || claim.Outcome != "Supported" || claim.Quality != "Corroborated" ||
            head.CurrentVersion != claim.Version || await db.ClaimCorrections.AnyAsync(x => x.ClaimRevisionId == row.ClaimRevisionId && x.Status == "Open", ct) ||
            await db.EditorialCorrections.AnyAsync(x => x.ClaimRevisionId == row.ClaimRevisionId, ct)) blocks.Add("CurrentVerifiedCentralClaimRequired");
        if (claim != null)
        {
            var spans = await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == claim.Id).ToListAsync(ct);
            if (spans.Count == 0 || spans.Any(x => !ids.Contains(x.SnapshotId))) blocks.Add("FactEvidenceDoesNotMatchSubstance");
        }
        var actor = await db.Members.Where(x => x.TenantId == row.TenantId && (x.Permission == "Administrator" || x.Permission == "Editor")).Select(x => x.Actor).FirstOrDefaultAsync(ct);
        if (actor == null) blocks.Add("OwnedReviewerUnavailable");
        else foreach (var signal in signals)
            foreach (var id in ids)
            {
                var decision = await new SourceRightsGuard(db).DecideAsync(new(row.TenantId, actor), id, new("Read", signal.Locale, signal.Region, "Text"), ct);
                if (!decision.Decision.Allowed) blocks.Add("Rights:" + decision.Decision.Reason);
            }
        if (snapshots.Any(x => x.ReviewStatus != "Approved" || x.Text == null)) blocks.Add("ReviewedSubstanceRequired");
        return (EditorialBinding.Hash(new { Candidates = candidates.Select(x => new { x.Id, x.SnapshotJson, x.ExtractionJson }), Sources = snapshots.Select(x => new { x.Id, x.RowVersion }), Claim = claim, Head = head?.CurrentVersion }), blocks.Distinct().ToArray(), groups.Count);
    }
    internal static async Task AssertCluster(CoreDbContext db, TrendCluster row, CancellationToken ct)
    {
        var proof = await Cluster(db, row, ct);
        if (row.State != "Verified" || proof.Blocks.Length != 0 || proof.Fingerprint != row.CheckedFingerprint)
            throw new EvidenceBlockedException("TrendRecheckRequired: " + string.Join(", ", proof.Blocks));
    }
    internal static async Task AssertBrief(CoreDbContext db, TrendStoryBrief row, bool approved, CancellationToken ct)
    {
        if (row.State is "Preparing" or "Held" or "Rejected" || approved && row.State != "Approved") throw new EvidenceBlockedException("TrendBriefReviewRequired");
        var cluster = await db.TrendClusters.AsNoTracking().SingleAsync(x => x.Id == row.ClusterId, ct);
        await AssertCluster(db, cluster, ct);
        var snapshot = EditorialBinding.Read<TrendStorySnapshot>(row.SnapshotJson);
        if (snapshot.ClusterFingerprint != cluster.CheckedFingerprint) throw new EvidenceBlockedException("TrendBriefEvidenceChanged");
        var teaching = await db.TrendTeachings.AsNoTracking().SingleAsync(x => x.Id == row.TeachingId, ct);
        if (teaching.ReviewStatus != "Approved" || teaching.Outcome != "Matched") throw new EvidenceBlockedException("TrendTeachingReviewRequired"); await AssertTeaching(db, teaching, ct);
    }
    internal static async Task AssertTeaching(CoreDbContext db, TrendTeaching teaching, CancellationToken ct)
    {
        var lesson = await db.LessonVariants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == teaching.LessonVariantId && x.TenantId == teaching.TenantId && x.UniverseId == teaching.UniverseId, ct) ?? throw new CoreAccessException();
        if (lesson.ReviewStatus != "Approved") throw new EvidenceBlockedException("TrendCurrentReviewedTeachingRequired");
        var snapshot = EditorialBinding.Read<LessonVariantSnapshot>(lesson.SnapshotJson);
        var actor = await db.Members.Where(x => x.TenantId == teaching.TenantId && (x.Permission == "Administrator" || x.Permission == "Editor")).Select(x => x.Actor).FirstOrDefaultAsync(ct) ?? throw new EvidenceBlockedException("OwnedTeachingReviewerUnavailable");
        await LessonEngine.RecheckBundle(db, new(teaching.TenantId, actor), teaching.UniverseId, snapshot.Bundle, snapshot.Locale.Rules.Locale!.Locale, "Read", ct);
    }
    internal static async Task<Guid[]> ClaimDependents(CoreDbContext db, CoreScope scope, Guid universe, Guid claim, CancellationToken ct)
    {
        var briefs = await (from b in db.TrendStoryBriefs.AsNoTracking() join t in db.TrendTeachings on b.TeachingId equals t.Id join v in db.LessonVariants on t.LessonVariantId equals v.Id join bundle in db.LessonBundles on v.BundleId equals bundle.Id where b.TenantId == scope.TenantId && b.UniverseId == universe && (bundle.AnchorClaimRevisionId == claim || bundle.InterpretationClaimRevisionId == claim) select new { b.Id, b.FactoryPlanId }).ToArrayAsync(ct);
        var ids = briefs.Select(x => x.Id).ToArray(); var plans = briefs.Where(x => x.FactoryPlanId.HasValue).Select(x => x.FactoryPlanId!.Value).ToArray();
        return (await db.TrendBindings.Where(x => x.TenantId == scope.TenantId && x.UniverseId == universe && ids.Contains(x.BriefId)).Select(x => x.ContentRevisionId).ToArrayAsync(ct)).Concat(await db.FactoryArtifacts.Where(x => x.TenantId == scope.TenantId && x.UniverseId == universe && plans.Contains(x.PlanId)).Select(x => x.CoreRevisionId).ToArrayAsync(ct)).Distinct().ToArray();
    }
    internal static async Task<TrendStoryBrief?> Plan(CoreDbContext db, Guid plan, CancellationToken ct) => await db.TrendStoryBriefs.AsNoTracking().SingleOrDefaultAsync(x => x.FactoryPlanId == plan, ct);
    internal static async Task AssertPlan(CoreDbContext db, Guid plan, CancellationToken ct) { var row = await Plan(db, plan, ct); if (row != null) await AssertBrief(db, row, true, ct); else if (await db.FactoryPlans.AnyAsync(x => x.Id == plan && x.Key.StartsWith("m15-"), ct)) throw new EvidenceBlockedException("TrendBriefBindingMissing"); }
    internal static async Task<TrendStoryBrief[]> Revisions(CoreDbContext db, Guid revision, CancellationToken ct)
    {
        var lineage = await new SourceRightsGuard(db).LineageAsync(revision, ct);
        var ids = lineage.Edges.SelectMany(x => new[] { x.ContentRevisionId, x.ParentRevisionId }).Where(x => x.HasValue).Select(x => x!.Value).Append(revision).ToHashSet();
        foreach (var asset in await db.Assets.AsNoTracking().Where(a => lineage.Edges.Select(x => x.AssetReferenceId).Contains(a.Id)).ToListAsync(ct)) ids.Add(asset.ContentRevisionId);
        var plans = await db.FactoryArtifacts.AsNoTracking().Where(x => ids.Contains(x.CoreRevisionId)).Select(x => x.PlanId).ToArrayAsync(ct);
        var briefs = await db.TrendBindings.AsNoTracking().Where(x => ids.Contains(x.ContentRevisionId)).Select(x => x.BriefId).ToArrayAsync(ct);
        return await db.TrendStoryBriefs.AsNoTracking().Where(x => briefs.Contains(x.Id) || x.FactoryPlanId != null && plans.Contains(x.FactoryPlanId.Value)).OrderBy(x => x.Id).ToArrayAsync(ct);
    }
    internal static async Task AssertRevision(CoreDbContext db, Guid revision, string purpose, CancellationToken ct)
    {
        foreach (var row in await Revisions(db, revision, ct))
        {
            await AssertBrief(db, row, true, ct);
            if (purpose == "Publish")
            {
                var cluster = await db.TrendClusters.AsNoTracking().SingleAsync(x => x.Id == row.ClusterId, ct);
                var claim = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == cluster.ClaimRevisionId, ct);
                var teaching = await db.TrendTeachings.AsNoTracking().SingleAsync(x => x.Id == row.TeachingId, ct);
                var connectorIds = await db.TrendCandidates.AsNoTracking().Where(x => x.ClusterId == cluster.Id).Select(x => x.ConnectorId).Distinct().ToArrayAsync(ct);
                if ((await db.TrendConnectors.AsNoTracking().Where(x => connectorIds.Contains(x.Id)).ToListAsync(ct)).Any(x => EditorialBinding.Read<RegisterTrendConnector>(x.SnapshotJson).Mode == "Fixture")) throw new EvidenceBlockedException("TrendFixtureNotQualifiedForPublication");
                if (!row.ReviewEvidence.StartsWith("manual:") || !cluster.ReviewEvidence.StartsWith("manual:") || !teaching.ReviewEvidence.StartsWith("manual:") || claim.ReviewEvidence?.StartsWith("manual:") != true)
                    throw new EvidenceBlockedException("TrendHumanReleaseReviewRequired");
            }
        }
    }
    internal static FactoryRenderResult Render(TrendStorySnapshot snapshot, FactoryRenderResult original) =>
        original with { Body = "Verified factual context (separate from the story):\n" + snapshot.FactualSummary + "\n\n" + (snapshot.Format == "OriginalFiction" ? "Original fictional analogy; not a portrayal, endorsement or prediction of real people/events:" : snapshot.Format == "CalendarLesson" ? "Calendar-themed lesson and original illustration; not scripture or a prediction:" : "Sourced event explainer followed by a separately labeled interpretation and original illustration:") + "\n" + original.Body };
    internal static void Lineage(CoreDbContext db, TrendStoryBrief row, Guid revision, string body)
    {
        var snapshot = EditorialBinding.Read<TrendStorySnapshot>(row.SnapshotJson);
        var prefix = "Verified factual context (separate from the story):\n";
        if (!body.StartsWith(prefix + snapshot.FactualSummary, StringComparison.Ordinal))
            throw new RuntimeValidationException("Bound factual context must remain separate from the original story.");
        foreach (var source in snapshot.EventSources) db.Provenance.Add(new() { Id = Guid.NewGuid(), TenantId = row.TenantId, ContentRevisionId = revision, SnapshotId = source, Relation = "Adaptation" });
        db.TrendBindings.Add(new() { Id = Guid.NewGuid(), TenantId = row.TenantId, UniverseId = row.UniverseId, BriefId = row.Id, ContentRevisionId = revision });
        var cluster = db.TrendClusters.AsNoTracking().Single(x => x.Id == row.ClusterId);
        var fact = db.ClaimRevisions.AsNoTracking().Single(x => x.Id == cluster.ClaimRevisionId);
        var reviewer = db.TrendOperations.AsNoTracking().Where(x => x.Kind == "brief-review" && x.ResultJson == EditorialBinding.Json(row.Id)).OrderByDescending(x => x.CreatedUtc).Select(x => x.Actor).First();
        // Exact segments are projections of the already-reviewed M15 script, not invented factual approvals.
        void Label(int start, int length, string type, string uncertainty)
        {
            var claim = new EvidenceClaim { Id = Guid.NewGuid(), TenantId = row.TenantId, UniverseId = row.UniverseId, Key = "m15-" + revision.ToString("N") + "-" + type, CurrentVersion = 1 };
            db.Claims.Add(claim);
            var projected = new ClaimRevision
            {
                Id = Guid.NewGuid(),
                ClaimId = claim.Id,
                Version = 1,
                Type = type,
                Label = type,
                Locale = fact.Locale,
                Statement = body.Substring(start, length),
                Author = "Reviewed original trend script",
                Tradition = "",
                Uncertainty = uncertainty,
                RubricId = fact.RubricId,
                ReviewStatus = "Approved",
                Quality = "Attributed",
                Outcome = "Supported",
                Reviewer = reviewer,
                ReviewEvidence = row.ReviewEvidence
            };
            db.ClaimRevisions.Add(projected); db.ClaimLinks.Add(new() { Id = Guid.NewGuid(), ClaimRevisionId = projected.Id, ContentRevisionId = revision, Start = start, Length = length });
        }
        Label(0, prefix.Length, "Suggestion", "Limited");
        db.ClaimLinks.Add(new() { Id = Guid.NewGuid(), ClaimRevisionId = fact.Id, ContentRevisionId = revision, Start = prefix.Length, Length = snapshot.FactualSummary.Length });
        var end = prefix.Length + snapshot.FactualSummary.Length;
        Label(end, body.Length - end, "FictionalScene", "Fictional");
    }

}

