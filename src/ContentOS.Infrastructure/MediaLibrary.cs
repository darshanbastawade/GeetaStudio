using System.Data;
using System.Security.Cryptography;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Infrastructure;

public sealed partial class MediaLibrary(CoreDbContext db, IAssetStore store, IMediaStorageMover mover,
    IUniverseRegistry registry, IMediaFileInspector inspector, IEnumerable<IImageGenerationProvider> images, IEnumerable<ISpeechSynthesisProvider> voices) : IMediaLibrary
{
    internal sealed record Material(ImportMedia Request, MediaInfo Info);
    private static string Json<T>(T x) => EditorialBinding.Json(x);
    private static T Read<T>(string x) => EditorialBinding.Read<T>(x);
    private static Guid Derived(Guid id, string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(id + ":" + key)).AsSpan(0, 16));
    private async Task Access(CoreScope s, Guid u, string role, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
        var permission = await db.Members.Where(x => x.TenantId == s.TenantId && x.Actor == s.Actor).Select(x => x.Permission).SingleOrDefaultAsync(ct);
        if (permission == null || role == "Admin" && permission != "Administrator" || role == "Review" && permission is not ("Administrator" or "Editor") || role == "Write" && permission is not ("Administrator" or "Editor" or "Author")) throw new CoreAccessException();
    }
    private async Task<T> Command<T, R>(CoreScope s, Guid u, Guid op, string kind, R request, string role, Func<Task<T>> action, CancellationToken ct)
    {
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable media operation ID required.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Media:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Media lock unavailable',1;", ct);
        await Access(s, u, role, ct); var hash = EditorialBinding.Hash(new { u, request }); var old = await db.MediaCommands.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (old != null) { if (old.Actor != s.Actor || old.Kind != kind || old.Fingerprint != hash) throw new RuntimeConflictException("Media operation replay differs."); return Read<T>(old.ResultJson); }
        var result = await action(); object auditRequest = request is ImportMedia upload ? upload with { Bytes = [] } : request!;
        db.MediaCommands.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = op, Actor = s.Actor, Kind = kind, Fingerprint = hash, RequestJson = Json(auditRequest), ResultJson = Json(result) });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = op, Action = "media." + kind });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
        await tx.CommitAsync(ct); return result;
    }
    private static void Expected(CoreRecord row, string rv) { if (Convert.ToBase64String(row.RowVersion) != rv) throw new CoreConcurrencyException(); }
    private async Task<MediaVersion> Version(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.MediaVersions.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    public async Task<MediaVersionView> ImportAsync(CoreScope s, Guid u, ImportMedia r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "import", r, "Write", async () =>
        {
            MediaRules.Input(r); var info = inspector.Inspect(r.Bytes);
            if (r.Kind == "Voice" && info.Mime != "audio/wav" || r.Kind is "CharacterSheet" or "Pose" or "Scene" && info.Mime != "image/png") throw new RuntimeValidationException("Asset kind and actual format differ.");
            if (!await db.Audiences.AnyAsync(x => x.Id == r.AudienceId && x.UniverseId == u, ct) || !await db.Locales.AnyAsync(x => x.Id == r.Locale, ct)) throw new CoreAccessException();
            if (r.CharacterProfileRevisionId.HasValue)
            {
                var profile = await db.RegistryRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.CharacterProfileRevisionId && x.UniverseId == u && x.TenantId == s.TenantId && x.Kind == "Character", ct) ?? throw new CoreAccessException(); var character = Read<RegistryCharacterRules>(profile.RulesJson);
                if (character.FictionalStatus != "Fictional") { var consent = r.Kind == "Voice" ? r.License.VoiceConsentReference : r.License.LikenessConsentReference; MediaRules.Attest("Manual", consent ?? ""); if (r.License.SourceSnapshotIds.Length == 0) throw new EvidenceBlockedException("RealIdentityMediaNeedsConsentAndSourceEvidence"); }
            }
            foreach (var parent in r.ParentVersionIds ?? []) await MediaRightsGuard.Assert(db, s, u, parent, "Read", false, ct);
            foreach (var source in r.License.SourceSnapshotIds) { await MediaRightsGuard.Source(db, s, u, source, r.Locale, info.Mime, "Read", ct); }
            if (r.GenerationId.HasValue)
            {
                var generation = await db.MediaGenerations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.GenerationId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                var pinned = Read<MediaGenerationSnapshot>(generation.SnapshotJson); await WatchAdmissionGuard.Assert(db, s, u, pinned.Request.Admission, ct); await MediaRightsGuard.Generation(db, s, u, pinned.Request, pinned.Profile.Kind, ct);
                if (generation.State != "Accepting") throw new RuntimeConflictException("Generated outputs enter only through their bound acceptance operation.");
            }
            var asset = await db.MediaAssets.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key, ct); if (asset == null) { asset = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key }; db.MediaAssets.Add(asset); }
            if (r.Version != asset.CurrentVersion + 1) throw new RuntimeConflictException("Append the next media version; stable identity is preserved.");
            Guid storage; string owner = s.Actor; var duplicate = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Sha256 == info.Sha256 && x.Length == info.Length, ct);
            if (duplicate != null) { storage = duplicate.StorageAssetId; owner = duplicate.StorageOwner; var bytes = await store.GetAsync(storage, owner, ct); if (bytes == null || !bytes.AsSpan().SequenceEqual(r.Bytes)) throw new RuntimeConflictException("Deduplication checksum/bytes unavailable."); }
            else storage = await store.PutAsync(r.Bytes, s.Actor, ct);
            var subject = await db.Subjects.FirstOrDefaultAsync(x => x.UniverseId == u && x.Key == "media-library", ct); if (subject == null) { subject = new() { Id = Guid.NewGuid(), UniverseId = u, Key = "media-library", Type = "media", Name = "Owned media library" }; db.Subjects.Add(subject); }
            var content = new ContentItem { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, SubjectId = subject.Id, AudienceId = r.AudienceId, LocaleId = r.Locale, CurrentVersion = 1 };
            var revision = new ContentRevision { Id = Guid.NewGuid(), ContentId = content.Id, Version = 1, OperationId = Derived(r.OperationId, "container"), Title = r.Key, Body = "Media metadata, not scripture or a factual teaching: " + r.AccessibilityDescription, Kind = "Synthetic", RightsStatus = r.License.Kind is "OwnedOriginal" or "Fixture" ? "Owned" : "Licensed", Provenance = "M16 " + r.License.EvidenceReference };
            db.Contents.Add(content); db.Revisions.Add(revision); await db.SaveChangesAsync(ct);
            var reference = new CoreAssetReference { Id = Guid.NewGuid(), TenantId = s.TenantId, ContentRevisionId = revision.Id, StorageAssetId = storage, RightsStatus = revision.RightsStatus }; db.Assets.Add(reference);
            foreach (var source in r.License.SourceSnapshotIds) db.Provenance.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, AssetReferenceId = reference.Id, SnapshotId = source, Relation = "Adaptation" });
            foreach (var parent in r.ParentVersionIds ?? []) { var pv = await Version(s, u, parent, ct); var pa = await db.Assets.SingleAsync(x => x.Id == pv.AssetReferenceId, ct); db.Provenance.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, AssetReferenceId = reference.Id, ParentRevisionId = pa.ContentRevisionId, Relation = "Adaptation" }); }
            var version = new MediaVersion { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, AssetId = asset.Id, Version = r.Version, AssetReferenceId = reference.Id, StorageAssetId = storage, StorageOwner = owner, Sha256 = info.Sha256, Length = info.Length, SnapshotJson = Json(new Material(r with { Bytes = [] }, info)) }; db.MediaVersions.Add(version); asset.CurrentVersion = r.Version; return version.Id;
        }, ct); return await View(s, u, await Version(s, u, id, ct), ct);
    }
    private async Task<MediaVersionView> View(CoreScope s, Guid u, MediaVersion row, CancellationToken ct)
    {
        var p = Read<Material>(row.SnapshotJson); var blocks = new List<string>(); try { await MediaRightsGuard.Assert(db, s, u, row.Id, "Read", false, ct); } catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { blocks.Add(e.Message); }
        return new(row.Id, row.AssetId, row.Version, row.AssetReferenceId, row.StorageAssetId, p.Request.Kind, p.Request.AccessibilityDescription, p.Info, p.Request.License, p.Request.CharacterProfileRevisionId, p.Request.ParentVersionIds ?? [], p.Request.GenerationId, row.ReviewStatus, row.ReviewEvidence, Convert.ToBase64String(row.RowVersion), blocks.ToArray());
    }
    public async Task<MediaAssetView[]> AssetsAsync(CoreScope s, Guid u, CancellationToken ct)
    { await Access(s, u, "Read", ct); var result = new List<MediaAssetView>(); foreach (var a in await db.MediaAssets.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)) { var versions = new List<MediaVersionView>(); foreach (var v in await db.MediaVersions.AsNoTracking().Where(x => x.AssetId == a.Id).OrderByDescending(x => x.Version).Take(20).ToListAsync(ct)) versions.Add(await View(s, u, v, ct)); result.Add(new(a.Id, a.Key, a.CurrentVersion, Convert.ToBase64String(a.RowVersion), versions.ToArray())); } return result.ToArray(); }
    public async Task ReviewAsync(CoreScope s, Guid u, Guid id, ReviewMedia r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "review", new { id, r }, "Review", async () =>
    {
        var row = await Version(s, u, id, ct); Expected(row, r.ExpectedRowVersion); MediaRules.Attest(r.Mode, r.EvidenceReference);
        if (r.Decision is not ("Approve" or "Reject" or "Withdraw") || r.Deviations.Length > 20) throw new RuntimeValidationException("Explicit bounded media review required.");
        if (r.Decision == "Approve") { if (row.ReviewStatus != "Draft" || r.Deviations.Length > 0) throw new EvidenceBlockedException("Append corrected media and review unresolved identity deviations."); await MediaRightsGuard.Assert(db, s, u, id, "Read", false, ct); var p = Read<Material>(row.SnapshotJson); if (r.Mode == "Fixture" && p.Request.License.Kind != "Fixture") throw new EvidenceBlockedException("NonFixtureMediaRequiresManualQualityReview"); }
        row.ReviewStatus = r.Decision == "Approve" ? "Approved" : r.Decision == "Reject" ? "Rejected" : "Withdrawn"; row.ReviewEvidence = r.EvidenceReference; row.ReviewJson = Json(r); if (r.Decision is "Withdraw" or "Reject") { var reference = await db.Assets.SingleAsync(x => x.Id == row.AssetReferenceId, ct); reference.RightsStatus = "Unverified"; }
        return id;
    }, ct);
    public async Task<MediaExport> ExportAsync(CoreScope s, Guid u, Guid id, string purpose, CancellationToken ct)
    { await Access(s, u, "Read", ct); if (purpose is not ("Read" or "Export" or "Reuse" or "Publish")) throw new RuntimeValidationException("Explicit media use required."); var row = await Version(s, u, id, ct); await MediaRightsGuard.Assert(db, s, u, id, purpose, purpose != "Read", ct); var bytes = await store.GetAsync(row.StorageAssetId, row.StorageOwner, ct) ?? throw new RuntimeConflictException("Media storage unavailable."); inspector.Inspect(bytes); return new(1, await View(s, u, row, ct), bytes, await mover.LocationAsync(row.StorageAssetId, row.StorageOwner, ct)); }
    public Task<MediaStoreMoveResult> MoveAsync(CoreScope s, Guid u, MoveMedia r, CancellationToken ct) => Command(s, u, r.OperationId, "move", r, "Admin", async () => { var row = await Version(s, u, r.VersionId, ct); await MediaRightsGuard.Assert(db, s, u, row.Id, "Export", true, ct); return await mover.MoveAsync(row.StorageAssetId, row.StorageOwner, r.StoreKey, ct); }, ct);
}
public static class MediaServices
{
    public static IServiceCollection AddMediaLibrary(this IServiceCollection s) { s.AddScoped<IMediaStorageMover, MediaStorageMover>(); s.AddScoped<IMediaLibrary, MediaLibrary>(); return s; }
}

