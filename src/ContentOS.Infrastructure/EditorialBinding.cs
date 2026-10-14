using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace ContentOS.Infrastructure;

internal sealed record EditorialStorage(Guid Id, string Sha256, long Length, string Owner);
internal static class EditorialBinding
{
    internal static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    internal static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
    internal static string Json<T>(T value) => JsonSerializer.Serialize(value);
    internal static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(value))));
    internal static async Task<EditorialStorage?> Storage(CoreDbContext db, Guid id, CancellationToken ct)
    {
        var opened = db.Database.GetDbConnection().State != ConnectionState.Open; if (opened) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "SELECT [Id],[Sha256],[Length],[Owner] FROM [runtime].[Assets] WHERE [Id]=@id"; var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);
            await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetString(1), r.GetInt64(2), r.GetString(3)) : null;
        }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
    }
    internal static async Task<ContentRevision> Revision(CoreDbContext db, EditorialItem item, CancellationToken ct)
    {
        var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == item.ContentId, ct);
        return await db.Revisions.AsNoTracking().SingleAsync(x => x.ContentId == head.Id && x.Version == head.CurrentVersion, ct);
    }
    internal static async Task<string> Fingerprint(CoreDbContext db, EditorialItem item, CancellationToken ct)
    {
        var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == item.ContentId, ct); var revision = await Revision(db, item, ct);
        var lineage = await new SourceRightsGuard(db).LineageAsync(revision.Id, ct);
        var sources = new List<object>();
        foreach (var id in lineage.Snapshots.Order())
        {
            var snap = await db.SourceSnapshots.AsNoTracking().SingleAsync(x => x.Id == id, ct); var edition = await db.SourceEditions.AsNoTracking().SingleAsync(x => x.Id == snap.EditionId, ct);
            var work = await db.SourceWorks.AsNoTracking().SingleAsync(x => x.Id == edition.WorkId, ct); var permission = edition.PermissionId.HasValue ? await db.SourcePermissions.AsNoTracking().SingleAsync(x => x.Id == edition.PermissionId, ct) : null;
            sources.Add(new
            {
                snap.Id,
                snap.Sha256,
                snap.ReviewStatus,
                snap.RowVersion,
                Edition = edition,
                WorkVersion = work.RowVersion,
                work.Revoked,
                Permission = permission,
                Languages = await db.PermissionLanguages.Where(x => x.PermissionId == edition.PermissionId).OrderBy(x => x.Value).Select(x => x.Value).ToArrayAsync(ct),
                Territories = await db.PermissionTerritories.Where(x => x.PermissionId == edition.PermissionId).OrderBy(x => x.Value).Select(x => x.Value).ToArrayAsync(ct),
                Formats = await db.PermissionFormats.Where(x => x.PermissionId == edition.PermissionId).OrderBy(x => x.Value).Select(x => x.Value).ToArrayAsync(ct)
            });
        }
        var assets = new List<object>(); foreach (var a in await db.Assets.AsNoTracking().Where(x => x.ContentRevisionId == revision.Id).OrderBy(x => x.Id).ToListAsync(ct)) assets.Add(new { Reference = a, Storage = await Storage(db, a.StorageAssetId, ct) });
        var claims = await db.ClaimLinks.AsNoTracking().Where(x => x.ContentRevisionId == revision.Id || lineage.Edges.Select(e => e.ParentRevisionId).Contains(x.ContentRevisionId)).OrderBy(x => x.Id).ToListAsync(ct);
        var claimIds = claims.Select(x => x.ClaimRevisionId).ToArray();
        var pack = await db.Packs.AsNoTracking().Where(x => x.UniverseId == item.UniverseId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        var policies = await db.Policies.AsNoTracking().Where(x => pack != null && x.PackId == pack.Id).OrderBy(x => x.Key).ToListAsync(ct);
        var reports = item.EvaluationRunId.HasValue ? await db.EvaluationRuns.AsNoTracking().Where(x => x.Id == item.EvaluationRunId).ToListAsync(ct) : [];
        var dataset = reports.Select(x => x.DatasetId).ToArray();
        var reviews = await db.EvaluationReviews.AsNoTracking().Where(x => dataset.Contains(x.DatasetId)).OrderBy(x => x.Id).ToListAsync(ct);
        var familyRevisionIds = lineage.Edges.Where(x => x.ParentRevisionId.HasValue).Select(x => x.ParentRevisionId!.Value).Append(revision.Id).ToArray();
        var factory = await db.FactoryArtifacts.AsNoTracking().Where(x => x.TenantId == item.TenantId && x.UniverseId == item.UniverseId && familyRevisionIds.Contains(x.CoreRevisionId)).OrderBy(x => x.Id).Select(x => x.SnapshotJson).ToListAsync(ct);
        var storyRows = await db.RegistryRevisions.AsNoTracking().Where(x => x.TenantId == item.TenantId && x.UniverseId == item.UniverseId && x.Kind == "Story").OrderBy(x => x.Id).ToListAsync(ct);
        var stories = storyRows.Where(x => { var story = Read<RegistryEpisodeSnapshot>(x.RulesJson); return story.ContentRevisionId.HasValue && familyRevisionIds.Contains(story.ContentRevisionId.Value); }).ToArray();
        var characterIds = factory.SelectMany(x => Read<FactoryArtifactSnapshot>(x).Input.Cast.Cast.Select(c => c.Character.CharacterId))
            .Concat(reports.SelectMany(x => Read<EvaluationReport>(x.SnapshotJson).Results.SelectMany(r => r.Output?.Cast.Select(c => c.CharacterId) ?? [])))
            .Concat(stories.SelectMany(x => Read<RegistryEpisodeSnapshot>(x.RulesJson).Cast.Select(c => c.Character.CharacterId))).Distinct().ToArray();
        var bibles = await db.RegistryRevisions.AsNoTracking().Where(x => x.TenantId == item.TenantId && x.UniverseId == item.UniverseId && x.Kind == "Bible").OrderBy(x => x.Id).ToListAsync(ct);
        var characters = await db.Characters.AsNoTracking().Where(x => characterIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        var profiles = await db.RegistryRevisions.AsNoTracking().Where(x => x.CharacterId != null && characterIds.Contains(x.CharacterId.Value)).OrderBy(x => x.Id).ToListAsync(ct);
        var corrections = await db.ClaimCorrections.AsNoTracking().Where(x => claimIds.Contains(x.ClaimRevisionId)).OrderBy(x => x.Id).ToListAsync(ct);
        var pending = await db.EditorialCorrectionTasks.AsNoTracking().Where(x => x.ContentId == head.Id && x.Kind == "Internal" && x.State != "Resolved").OrderBy(x => x.Id).ToListAsync(ct);
        var baseline = Hash(new
        {
            Policy = "editorial.v2",
            head.Id,
            head.AudienceId,
            head.LocaleId,
            Revision = revision,
            Assets = assets,
            Sources = sources,
            Lineage = lineage.Edges.OrderBy(x => x.Id),
            Claims = await db.ClaimRevisions.AsNoTracking().Where(x => claimIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct),
            Corrections = corrections,
            Pending = pending,
            Pack = pack,
            Policies = policies,
            Audience = await db.Audiences.AsNoTracking().SingleAsync(x => x.Id == head.AudienceId, ct),
            Reports = reports,
            Reviews = reviews,
            Factory = factory,
            Characters = characters,
            Profiles = profiles,
            Stories = stories,
            Bibles = bibles,
            item.EvaluationCaseKey
        });
        var trends = await TrendFence.Revisions(db, revision.Id, ct);
        var withTrends = trends.Length == 0 ? baseline : Hash(new { baseline, Trends = trends.Select(x => new { x.Id, x.State, x.SnapshotJson, x.RowVersion }) });
        var presentationIds = await MediaRightsGuard.CastPresentations(db, new(item.TenantId, "editorial-fingerprint"), item.UniverseId, familyRevisionIds, ct);
        var mediaPackages = await db.MediaPackages.AsNoTracking().Where(x => x.PresentationRevisionId != null && presentationIds.Contains(x.PresentationRevisionId.Value)).OrderBy(x => x.Id).ToListAsync(ct);
        var media = await (from m in db.MediaVersions.AsNoTracking() join a in db.Assets on m.AssetReferenceId equals a.Id where familyRevisionIds.Contains(a.ContentRevisionId) select m).OrderBy(x => x.Id).ToListAsync(ct);
        return mediaPackages.Count == 0 && media.Count == 0 ? withTrends : Hash(new { withTrends, MediaPackages = mediaPackages, Media = media });
    }
    internal static async Task AssertExport(CoreDbContext db, CoreScope s, Guid revision, string purpose, CancellationToken ct)
    {
        await TrendFence.AssertRevision(db, revision, purpose, ct);
        await WatchFence.AssertExport(db, s, revision, purpose, ct);
        await MediaRightsGuard.Revision(db, s, revision, purpose, ct);
        var content = await db.Revisions.Where(x => x.Id == revision).Select(x => x.ContentId).SingleAsync(ct);
        if (await db.EditorialCorrectionTasks.AnyAsync(x => x.ContentId == content && x.Kind == "Internal" && x.State != "Resolved", ct)) throw new EvidenceBlockedException("UnresolvedEditorialCorrection");
        var parents = (await new SourceRightsGuard(db).LineageAsync(revision, ct)).Edges.Where(x => x.ParentRevisionId.HasValue).Select(x => x.ParentRevisionId!.Value).Append(revision).Distinct().ToArray();
        var items = await db.EditorialItems.AsNoTracking().Where(x => db.Revisions.Any(r => parents.Contains(r.Id) && r.ContentId == x.ContentId)).ToListAsync(ct);
        foreach (var item in items)
        {
            var boundRevision = await Revision(db, item, ct); if (!parents.Contains(boundRevision.Id)) throw new EvidenceBlockedException("EditorialAncestorRevisionChanged");
            revision = boundRevision.Id;
            if (item.TenantId != s.TenantId) throw new CoreAccessException();
            if (item.State is not ("Approved" or "Scheduled" or "Published") || !item.ApprovalId.HasValue || (await Revision(db, item, ct)).Id != revision) throw new EvidenceBlockedException("CurrentEditorialApprovalRequired");
            var approval = await db.EditorialApprovals.AsNoTracking().SingleAsync(x => x.Id == item.ApprovalId, ct);
            if (approval.RevisionId != revision || approval.Fingerprint != await Fingerprint(db, item, ct)) throw new EvidenceBlockedException("EditorialApprovalExpired");
            if (purpose == "Publish" && approval.Mode != "Manual") throw new EvidenceBlockedException("FixtureApprovalCannotPublish");
        }
    }
}
