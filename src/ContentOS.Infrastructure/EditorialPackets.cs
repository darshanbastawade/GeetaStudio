using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class EditorialWorkbench
{
    private async Task<EditorialLineageNode[]> Lineage(CoreScope s, Guid u, Guid revision, CancellationToken ct)
    {
        var ids = new HashSet<Guid> { revision }; var queue = new Queue<Guid>(); queue.Enqueue(revision);
        while (queue.TryDequeue(out var id))
        {
            var edges = await (from edge in db.Provenance.AsNoTracking()
                               join asset in db.Assets on edge.AssetReferenceId equals (Guid?)asset.Id into assets
                               from asset in assets.DefaultIfEmpty()
                               let target = edge.ContentRevisionId ?? (Guid?)asset.ContentRevisionId
                               where edge.TenantId == s.TenantId && (target == id || edge.ParentRevisionId == id)
                                   && (edge.AssetReferenceId == null || asset.TenantId == s.TenantId)
                                   && db.Revisions.Any(r => r.Id == target && db.Contents.Any(c => c.Id == r.ContentId && c.UniverseId == u && c.TenantId == s.TenantId))
                                   && (edge.ParentRevisionId == null || db.Revisions.Any(r => r.Id == edge.ParentRevisionId && db.Contents.Any(c => c.Id == r.ContentId && c.UniverseId == u && c.TenantId == s.TenantId)))
                               select new { ContentRevisionId = target, edge.ParentRevisionId }).ToListAsync(ct);
            foreach (var other in edges.SelectMany(x => new[] { x.ContentRevisionId, x.ParentRevisionId }).Where(x => x.HasValue).Select(x => x!.Value))
                if (ids.Add(other)) { if (ids.Count > 500) throw new RuntimeValidationException("Lineage capacity500 exceeded."); queue.Enqueue(other); }
            var family = await db.Revisions.Where(x => x.Id == id).Select(x => x.ContentId).SingleOrDefaultAsync(ct);
            foreach (var other in await db.Revisions.Where(x => x.ContentId == family).Select(x => x.Id).ToListAsync(ct)) if (ids.Add(other)) { if (ids.Count > 500) throw new RuntimeValidationException("Lineage capacity500 exceeded."); queue.Enqueue(other); }
        }
        return await (from r in db.Revisions.AsNoTracking()
                      join c in db.Contents on r.ContentId equals c.Id
                      where ids.Contains(r.Id) && c.TenantId == s.TenantId && c.UniverseId == u
                      select new EditorialLineageNode(c.Id, r.Id, r.Version, db.EditorialItems.Where(i => i.ContentId == c.Id).Select(i => i.Format).FirstOrDefault() ?? r.Kind, c.EditorialState, c.LocaleId, c.AudienceId)).ToArrayAsync(ct);
    }
    public async Task<EditorialPacket> InspectAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        await Access(s, u, "Read", ct); var item = await Item(s, u, id, ct); var head = await Head(item, ct); var revision = await EditorialBinding.Revision(db, item, ct);
        var previous = await db.Revisions.AsNoTracking().Where(x => x.ContentId == head.Id && x.Version < revision.Version).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        RevisionContract View(ContentRevision x) => new(x.Id, x.Version, x.OperationId, x.Title, x.Body, x.Kind, x.RightsStatus, x.Provenance, x.CreatedUtc);
        var rights = await Rights(s, item, "Read", ct); var export = await Rights(s, item, "Export", ct); var withheld = rights.Blocks.Any(x => x.StartsWith("Rights:"));
        var fingerprint = await EditorialBinding.Fingerprint(db, item, ct);
        var approvals = await db.EditorialApprovals.AsNoTracking().Where(x => x.ItemId == id).OrderByDescending(x => x.CreatedUtc).ToListAsync(ct);
        var approvalViews = approvals.Select(x => new EditorialApprovalView(x.Id, x.RevisionId, x.Fingerprint, x.Mode, x.Reviewer, x.EvidenceReference, x.CreatedUtc, x.Id == item.ApprovalId && x.Fingerprint == fingerprint && x.RevisionId == revision.Id)).ToArray();
        var blocks = export.Blocks.ToList(); if (item.ApprovalId.HasValue && !approvalViews.Any(x => x.Current)) blocks.Add("ApprovalExpired");
        if (!approvalViews.Any(x => x.Current && x.Mode == "Manual")) blocks.Add("PublicReleaseNotApproved"); blocks.Add("ExternalPublishingAdapterNotImplemented");
        var assetViews = new List<EditorialAssetView>();
        foreach (var slot in await db.EditorialAssets.AsNoTracking().Where(x => x.ItemId == id && x.RevisionId == revision.Id).OrderByDescending(x => x.CreatedUtc).ToListAsync(ct))
        {
            if (assetViews.Any(x => x.Slot == slot.Slot)) continue; var a = await db.Assets.AsNoTracking().SingleAsync(x => x.Id == slot.AssetReferenceId, ct); var storage = await EditorialBinding.Storage(db, a.StorageAssetId, ct);
            assetViews.Add(new(slot.Id, slot.Slot, slot.MediaType, a.Id, a.StorageAssetId, storage?.Sha256 ?? "Missing", storage?.Length ?? 0, a.RightsStatus, slot.EvidenceReference));
        }
        var lineage = await Lineage(s, u, revision.Id, ct); var lineageIds = lineage.Select(x => x.RevisionId).ToArray();
        var guard = await new SourceRightsGuard(db).LineageAsync(revision.Id, ct);
        var labels = await new EvidenceEngine(db).CheckContentAsync(s, revision.Id, new("Read", head.LocaleId, "IN", "Text"), ct);
        var disagreements = await db.ClaimDisagreements.AsNoTracking().Where(x => labels.Labels.Select(l => l.ClaimRevisionId).Contains(x.LeftRevisionId) || labels.Labels.Select(l => l.ClaimRevisionId).Contains(x.RightRevisionId)).Select(x => new DisagreementView(x.Id, x.LeftRevisionId, x.RightRevisionId, x.Reason, x.EvidenceReference)).ToArrayAsync(ct);
        var productionIds = guard.Edges.Where(x => x.ParentRevisionId.HasValue).Select(x => x.ParentRevisionId!.Value).Append(revision.Id).ToArray();
        var artifacts = await db.FactoryArtifacts.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && (x.CoreContentId == head.Id || productionIds.Contains(x.CoreRevisionId))).OrderBy(x => x.CreatedUtc).Select(x => x.SnapshotJson).ToListAsync(ct);
        var factoryHistory = artifacts.Select(Read<FactoryArtifactSnapshot>).ToArray();
        var variants = new List<LessonVariantView>(); foreach (var v in factoryHistory.Select(x => x.LessonVariantId).Distinct()) variants.Add(await lessons.InspectAsync(s, u, v, ct));
        var episodes = (await registry.StoriesAsync(s, u, ct)).Where(x => x.Rules.ContentRevisionId.HasValue && lineageIds.Contains(x.Rules.ContentRevisionId.Value)).Select(x => x.Rules).Concat(factoryHistory.Select(x => x.Input.Cast)).ToArray();
        var runs = new List<AiRunView>(); var reportViews = item.EvaluationRunId.HasValue ? new[] { await evaluation.InspectAsync(s, u, item.EvaluationRunId.Value, ct) } : Array.Empty<EvaluationReport>();
        foreach (var run in reportViews.SelectMany(x => x.Results).Where(x => x.AiRunId.HasValue).Select(x => x.AiRunId!.Value).Distinct()) runs.Add(await ai.InspectAsync(s, run, ct));
        var copies = await db.EditorialCopies.AsNoTracking().Where(x => x.ItemId == id).Select(x => new EditorialCopyView(x.Id, x.RevisionId, x.Channel, x.Mode, x.Reference, x.State, Convert.ToBase64String(x.RowVersion))).ToArrayAsync(ct);
        var history = await db.EditorialHistories.AsNoTracking().Where(x => x.ItemId == id).OrderBy(x => x.CreatedUtc).Select(x => new EditorialEventView(x.Id, x.Action, x.Actor, x.EvidenceReference, x.CreatedUtc)).ToArrayAsync(ct);
        var audience = await db.Audiences.AsNoTracking().SingleAsync(x => x.Id == head.AudienceId, ct); var highRisk = audience.MinAge < 18 || labels.Labels.Any(x => x.Label is "HistoricalClaim" or "NewsClaim");
        return new(id, item.Key, item.Format, item.Brief, item.Author, head.Id, revision.Id, blocks.Contains("ApprovalExpired") ? "Review" : item.State, Convert.ToBase64String(item.RowVersion), item.PublicIntended, highRisk,
            withheld ? null : View(revision), withheld || previous == null ? null : View(previous), assetViews.ToArray(), approvalViews, fingerprint, blocks.Distinct().ToArray(),
            withheld ? [] : rights.Citations, labels.Labels.ToArray(), disagreements, guard.Snapshots.ToArray(), withheld ? [] : factoryHistory, withheld ? [] : variants.ToArray(), withheld ? [] : episodes, withheld ? [] : runs.ToArray(), withheld ? [] : reportViews, lineage, copies, history, withheld);
    }
    public Task<Guid> CorrectAsync(CoreScope s, Guid u, EditorialCorrection r, CancellationToken ct) => Command(s, u, r.OperationId, "correct", r, "Review", async () =>
    {
        CoreRules.Text(r.Reason, 2000, "Correction reason"); EditorialRules.Evidence(r.EvidenceReference, r.EvidenceReference.Trim().StartsWith("fixture:") ? "Fixture" : "Manual");
        if (r.ClaimRevisionId.HasValue == r.SnapshotId.HasValue) throw new RuntimeValidationException("Choose one owned claim or snapshot.");
        Guid[] initial;
        if (r.ClaimRevisionId.HasValue)
        {
            if (!await db.ClaimRevisions.AnyAsync(x => x.Id == r.ClaimRevisionId && db.Claims.Any(c => c.Id == x.ClaimId && c.TenantId == s.TenantId && c.UniverseId == u), ct)) throw new CoreAccessException();
            initial = await db.ClaimLinks.Where(x => x.ClaimRevisionId == r.ClaimRevisionId).Select(x => x.ContentRevisionId).ToArrayAsync(ct);
        }
        else
        {
            if (!await (from snap in db.SourceSnapshots join ed in db.SourceEditions on snap.EditionId equals ed.Id join work in db.SourceWorks on ed.WorkId equals work.Id where snap.Id == r.SnapshotId && work.TenantId == s.TenantId && work.UniverseId == u select snap.Id).AnyAsync(ct)) throw new CoreAccessException();
            initial = await db.Provenance.Where(x => x.SnapshotId == r.SnapshotId).Select(x => x.ContentRevisionId ?? db.Assets.Where(a => a.Id == x.AssetReferenceId).Select(a => a.ContentRevisionId).First()).ToArrayAsync(ct);
        }
        var affected = new HashSet<Guid>(); foreach (var root in initial) foreach (var node in await Lineage(s, u, root, ct)) affected.Add(node.RevisionId);
        var correction = new EditorialCorrectionRecord { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ClaimRevisionId = r.ClaimRevisionId, SnapshotId = r.SnapshotId, Reason = r.Reason, EvidenceReference = r.EvidenceReference }; db.EditorialCorrections.Add(correction);
        var contentIds = await db.Revisions.Where(x => affected.Contains(x.Id) && db.Contents.Any(c => c.Id == x.ContentId && c.TenantId == s.TenantId && c.UniverseId == u)).Select(x => x.ContentId).Distinct().ToArrayAsync(ct);
        foreach (var content in await db.Contents.Where(x => contentIds.Contains(x.Id)).ToListAsync(ct))
        {
            var current = await db.Revisions.SingleAsync(x => x.ContentId == content.Id && x.Version == content.CurrentVersion, ct);
            db.EditorialCorrectionTasks.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, CorrectionId = correction.Id, ContentId = content.Id, RevisionId = current.Id, Kind = "Internal", Reason = r.Reason });
            content.EditorialState = "Review"; content.PublicKnowledge = false;
            var item = await db.EditorialItems.SingleOrDefaultAsync(x => x.ContentId == content.Id, ct);
            if (item != null) { item.State = "Corrected"; item.ApprovalId = null; item.ScheduledUtc = null; History(s, item, "Source correction; approval invalidated", r.EvidenceReference); }
            var copies = await db.EditorialCopies.Where(x => affected.Contains(x.RevisionId) && x.TenantId == s.TenantId && x.UniverseId == u).ToListAsync(ct);
            foreach (var copy in copies.Where(x => db.Revisions.AsNoTracking().Any(v => v.Id == x.RevisionId && v.ContentId == content.Id)))
                db.EditorialCorrectionTasks.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, CorrectionId = correction.Id, ContentId = content.Id, RevisionId = copy.RevisionId, CopyId = copy.Id, Kind = "External", Reason = "Copy needs manual edit/takedown; M18 platform delivery unavailable. " + r.Reason[..Math.Min(1800, r.Reason.Length)] });
            foreach (var use in await db.SourceUses.Where(x => affected.Contains(x.ContentRevisionId) && x.TenantId == s.TenantId && x.Published).ToListAsync(ct))
                if (await db.Revisions.AnyAsync(x => x.Id == use.ContentRevisionId && x.ContentId == content.Id, ct))
                    db.EditorialCorrectionTasks.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, CorrectionId = correction.Id, ContentId = content.Id, RevisionId = use.ContentRevisionId, Kind = "RecordedExternal", Reason = "Recorded external source use " + use.ExternalReference + "; delivery/takedown not independently verified." });
        }
        return correction.Id;
    }, ct);
    public async Task<IReadOnlyList<EditorialCorrectionTaskView>> CorrectionsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, "Read", ct); var rows = await db.EditorialCorrectionTasks.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(1001).ToListAsync(ct);
        if (rows.Count > 1000) throw new RuntimeValidationException("Correction queue capacity1000 exceeded.");
        var taskIds = rows.Select(x => x.Id).ToArray();
        var attempts = await db.EditorialAttempts.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && taskIds.Contains(x.TaskId)).OrderBy(x => x.CreatedUtc).ToListAsync(ct);
        return rows.Select(x => new EditorialCorrectionTaskView(x.Id, x.CorrectionId, x.ContentId, x.RevisionId, x.CopyId, x.Kind, x.State, x.Reason, x.EvidenceReference, Convert.ToBase64String(x.RowVersion),
            attempts.Where(a => a.TaskId == x.Id).Select(a => new EditorialEventView(a.Id, a.Action + (a.ReplacementContentId.HasValue ? " replacement " + a.ReplacementContentId : ""), a.Actor, a.EvidenceReference, a.CreatedUtc)).ToArray())).ToArray();
    }
    public async Task ResolveCorrectionAsync(CoreScope s, Guid u, Guid id, ResolveEditorialCorrection r, CancellationToken ct)
    {
        await Command(s, u, r.OperationId, "resolve", new { id, r }, "Read", async () =>
        {
            var task = await db.EditorialCorrectionTasks.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException(); Expected(task, r.ExpectedRowVersion);
            EditorialRules.Evidence(r.EvidenceReference, r.EvidenceReference.Trim().StartsWith("fixture:") ? "Fixture" : "Manual");
            if (task.Kind == "Internal")
            {
                await Access(s, u, "Review", ct);
                var head = await db.Contents.SingleAsync(x => x.Id == task.ContentId, ct); var item = await db.EditorialItems.SingleOrDefaultAsync(x => x.ContentId == head.Id, ct);
                if (r.Action == "Withdraw") { head.EditorialState = "Archived"; head.PublicKnowledge = false; task.State = "Withdrawn"; if (item != null) { item.State = "Withdrawn"; item.ApprovalId = null; } }
                else if (r.Action == "Resolve" && r.ReplacementContentId.HasValue)
                {
                    if (r.ReplacementContentId == head.Id) throw new RuntimeValidationException("Choose an independent replacement content.");
                    var replacement = await db.Contents.SingleOrDefaultAsync(x => x.Id == r.ReplacementContentId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                    var revised = await db.Revisions.SingleAsync(x => x.ContentId == replacement.Id && x.Version == replacement.CurrentVersion, ct);
                    await new SourceRightsGuard(db).CheckAsync(s, revised.Id, new("Export", replacement.LocaleId, "IN", "Text"), ct);
                    var gate = await new EvidenceEngine(db).CheckContentAsync(s, revised.Id, new("Export", replacement.LocaleId, "IN", "Text"), ct);
                    if (!gate.Eligible || !await db.EditorialItems.AnyAsync(x => x.ContentId == replacement.Id && x.ApprovalId != null, ct)) throw new EvidenceBlockedException("ReviewedReplacementRequired");
                    task.State = "Resolved"; head.EditorialState = "Archived"; head.PublicKnowledge = false;
                    if (item != null) { item.State = "Withdrawn"; item.ApprovalId = null; History(s, item, "Replacement content " + replacement.Id + " revision " + revised.Id, r.EvidenceReference); }
                }
                else if (r.Action == "Resolve")
                {
                    var current = await db.Revisions.SingleAsync(x => x.ContentId == head.Id && x.Version == head.CurrentVersion, ct);
                    if (current.Id == task.RevisionId) throw new EvidenceBlockedException("NewVerifiedCorrectionRevisionRequired");
                    var gate = await new EvidenceEngine(db).CheckContentAsync(s, current.Id, new("Export", head.LocaleId, "IN", "Text"), ct); if (!gate.Eligible) throw new EvidenceBlockedException(gate.Reason);
                    task.State = "Resolved"; if (item != null) { item.State = "Draft"; item.ApprovalId = null; item.EvaluationRunId = null; }
                }
                else throw new RuntimeValidationException("Internal correction action:Resolve or Withdraw.");
            }
            else
            {
                await Access(s, u, "Publish", ct); if (r.Action is not ("Attempted" or "Unknown" or "Unsupported" or "Confirmed")) throw new RuntimeValidationException("External outcome must remain explicit.");
                if (r.Action == "Confirmed" && !r.EvidenceReference.StartsWith("manual:")) throw new RuntimeValidationException("External confirmation needs real manual evidence.");
                task.State = r.Action; if (task.CopyId.HasValue) { var copy = await db.EditorialCopies.SingleAsync(x => x.Id == task.CopyId, ct); copy.State = r.Action; }
            }
            db.EditorialAttempts.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, TaskId = task.Id, Actor = s.Actor, Action = r.Action, EvidenceReference = r.EvidenceReference, ReplacementContentId = r.ReplacementContentId });
            task.EvidenceReference = r.EvidenceReference; var trackedItem = await db.EditorialItems.SingleOrDefaultAsync(x => x.ContentId == task.ContentId, ct); if (trackedItem != null) History(s, trackedItem, "Correction " + task.Id + ": " + r.Action, r.EvidenceReference); return id;
        }, ct);
    }
}
