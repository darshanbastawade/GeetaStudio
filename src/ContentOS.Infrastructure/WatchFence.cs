using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

internal static class WatchFence
{
    internal static async Task VideoRights(CoreDbContext db, CoreScope s, WatchProduction row, string purpose, CancellationToken ct)
    { var rights = new SourceRightsGuard(db); var lineage = await rights.LineageAsync(row.CoreRevisionId, ct); var snap = EditorialBinding.Read<WatchSnapshot>(row.SnapshotJson); var locale = await db.Contents.Where(x => x.Id == snap.Script.ContentId).Select(x => x.LocaleId).SingleAsync(ct); foreach (var id in lineage.Snapshots) { var result = await rights.DecideAsync(s, id, new(purpose, locale, "IN", "Video", Adaptation: true), ct); if (!result.Decision.Allowed) throw new RightsBlockedException(result.Decision.Reason); } }
    internal static async Task AssertExport(CoreDbContext db, CoreScope s, Guid revision, string purpose, CancellationToken ct)
    {
        var lineage = await new SourceRightsGuard(db).LineageAsync(revision, ct); var ids = lineage.Edges.Where(x => x.ParentRevisionId.HasValue).Select(x => x.ParentRevisionId!.Value).Append(revision).ToArray();
        foreach (var row in await db.WatchProductions.AsNoTracking().Where(x => ids.Contains(x.CoreRevisionId)).ToArrayAsync(ct))
        {
            if (row.TenantId != s.TenantId) throw new CoreAccessException(); if (row.State != "Approved" || row.TimelineJson == null || row.ReviewJson == null) throw new EvidenceBlockedException("CurrentFinalWATCHEpisodeApprovalRequired"); var snap = EditorialBinding.Read<WatchSnapshot>(row.SnapshotJson); var review = EditorialBinding.Read<WatchReview>(row.ReviewJson);
            if (!await db.WatchTemplates.AnyAsync(x => x.Id == snap.Template.Id && x.ReviewStatus == "Approved", ct)) throw new EvidenceBlockedException("WATCHLayoutApprovalWithdrawn"); if (purpose == "Publish" && (review.Mode != "Manual" || snap.Profile.Capabilities.Qualification == "OfflineFixture")) throw new EvidenceBlockedException("FixtureWATCHEpisodeCannotPublish");
            await VideoRights(db, s, row, purpose, ct); foreach (var scene in EditorialBinding.Read<WatchTimeline>(row.TimelineJson).Scenes) { await MediaRightsGuard.WatchVoice(db, s, row.UniverseId, scene, ct); await MediaRightsGuard.Assert(db, s, row.UniverseId, scene.Image.Id, purpose == "Publish" ? "Publish" : "Reuse", true, ct); await MediaRightsGuard.Assert(db, s, row.UniverseId, scene.Voice.Id, purpose == "Publish" ? "Publish" : "Reuse", true, ct); if (scene.Music != null) await MediaRightsGuard.Assert(db, s, row.UniverseId, scene.Music.Id, purpose == "Publish" ? "Publish" : "Reuse", true, ct); }
        }
    }
}


