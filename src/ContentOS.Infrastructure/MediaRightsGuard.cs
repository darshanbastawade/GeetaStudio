using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

internal static class MediaRightsGuard
{
    internal static async Task Source(CoreDbContext db, CoreScope s, Guid u, Guid id, string locale, string mime, string purpose, CancellationToken ct)
    {
        if (!await (from snap in db.SourceSnapshots join edition in db.SourceEditions on snap.EditionId equals edition.Id join work in db.SourceWorks on edition.WorkId equals work.Id where snap.Id == id && work.TenantId == s.TenantId && work.UniverseId == u select snap.Id).AnyAsync(ct)) throw new CoreAccessException();
        var decision = await new SourceRightsGuard(db).DecideAsync(s, id, new(purpose, locale, "IN", mime.StartsWith("image/") ? "Image" : "Audio", Adaptation: purpose is "Adapt" or "Generate"), ct);
        if (!decision.Decision.Allowed) throw new RightsBlockedException(decision.Decision.Reason);
    }
    internal static Task<MediaVersion> Assert(CoreDbContext db, CoreScope s, Guid u, Guid id, string purpose, bool approved, CancellationToken ct) => Assert(db, s, u, id, purpose, approved, ct, new HashSet<Guid>());
    private static async Task<MediaVersion> Assert(CoreDbContext db, CoreScope s, Guid u, Guid id, string purpose, bool approved, CancellationToken ct, HashSet<Guid> visited)
    {
        if (visited.Count >= 100) throw new EvidenceBlockedException("MediaLineageLimit");
        var row = await db.MediaVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        if (!visited.Add(id)) return row;
        if (row.ReviewStatus is "Withdrawn" or "Rejected" || approved && row.ReviewStatus != "Approved") throw new EvidenceBlockedException("ApprovedCurrentMediaReviewRequired");
        var p = EditorialBinding.Read<MediaLibrary.Material>(row.SnapshotJson); var l = p.Request.License;
        if (l.ExpiresUtc <= DateTimeOffset.UtcNow || purpose is "Reuse" or "Publish" && !l.Adaptation || purpose == "Publish" && !l.Commercial) throw new RightsBlockedException("MediaLicenseExpiredOrUseNotPermitted");
        if (purpose == "Publish" && (l.Kind == "Fixture" || row.ReviewEvidence?.StartsWith("manual:", StringComparison.OrdinalIgnoreCase) != true)) throw new EvidenceBlockedException("FixtureMediaCannotQualifyPublication");
        if (purpose == "Publish" && p.Request.GenerationId.HasValue) { var generation = await db.MediaGenerations.AsNoTracking().SingleAsync(x => x.Id == p.Request.GenerationId, ct); if (EditorialBinding.Read<MediaGenerationSnapshot>(generation.SnapshotJson).Capabilities.Qualification != "LiveSandboxVerified") throw new EvidenceBlockedException("MediaProviderPublicQualificationRequired"); }
        var reference = await db.Assets.AsNoTracking().SingleAsync(x => x.Id == row.AssetReferenceId, ct); if (reference.RightsStatus is not ("Owned" or "Licensed" or "PublicDomain")) throw new RightsBlockedException("MediaRightsRevoked");
        foreach (var source in l.SourceSnapshotIds) await Source(db, s, u, source, p.Request.Locale, p.Info.Mime, purpose == "Reuse" ? "Adapt" : purpose, ct);
        foreach (var parent in p.Request.ParentVersionIds ?? []) await Assert(db, s, u, parent, purpose, approved, ct, visited);
        if (p.Request.CharacterProfileRevisionId.HasValue) { var profile = await db.RegistryRevisions.AsNoTracking().SingleAsync(x => x.Id == p.Request.CharacterProfileRevisionId, ct); var c = await db.Characters.AsNoTracking().SingleAsync(x => x.Id == profile.CharacterId, ct); if (c.Status == "Retired" || profile.ReviewStatus != "Approved" || await db.RegistryRevisions.AnyAsync(x => x.CharacterId == c.Id && x.Kind == "Character" && x.Version > profile.Version, ct)) throw new EvidenceBlockedException("MediaCharacterProfileChanged"); }
        return row;
    }
    internal static async Task VoiceProfile(CoreDbContext db, CoreScope s, Guid u, CharacterMediaRules rules, Guid proposed, CancellationToken ct)
    {
        var original = await db.MediaVersions.AsNoTracking().SingleAsync(x => x.Id == rules.VoiceVersionId && x.TenantId == s.TenantId && x.UniverseId == u, ct);
        var source = EditorialBinding.Read<MediaLibrary.Material>(original.SnapshotJson);
        if (!source.Request.GenerationId.HasValue) throw new EvidenceBlockedException("ImportedRecordingRequiresNewReviewedPackageForGeneratedVoice");
        var generation = await db.MediaGenerations.AsNoTracking().SingleAsync(x => x.Id == source.Request.GenerationId && x.TenantId == s.TenantId && x.UniverseId == u, ct);
        var profile = await db.MediaProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == proposed && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        var pinned = EditorialBinding.Read<MediaGenerationSnapshot>(generation.SnapshotJson).Profile;
        if (generation.ProfileId != profile.Id || EditorialBinding.Json(pinned) != profile.SnapshotJson) throw new EvidenceBlockedException("ExactOriginalVoiceProviderAccountProfileVersionAndResourceRequired");
    }

    internal static async Task WatchVoice(CoreDbContext db, CoreScope s, Guid u, WatchScene scene, CancellationToken ct)
    {
        var package = await db.MediaPackages.AsNoTracking().SingleAsync(x => x.PresentationRevisionId == scene.PresentationId && x.TenantId == s.TenantId && x.UniverseId == u, ct); var rules = EditorialBinding.Read<CharacterMediaRules>(package.SnapshotJson);
        if (scene.Voice.Id == rules.VoiceVersionId || rules.AdditionalVersionIds.Contains(scene.Voice.Id)) return;
        var version = await db.MediaVersions.AsNoTracking().SingleAsync(x => x.Id == scene.Voice.Id && x.TenantId == s.TenantId && x.UniverseId == u, ct); var material = EditorialBinding.Read<MediaLibrary.Material>(version.SnapshotJson);
        if (!material.Request.GenerationId.HasValue) throw new EvidenceBlockedException("SceneVoiceMustBelongToReviewedPackage");
        var generation = await db.MediaGenerations.AsNoTracking().SingleAsync(x => x.Id == material.Request.GenerationId, ct); await VoiceProfile(db, s, u, rules, generation.ProfileId, ct);
    }
    internal static async Task Generation(CoreDbContext db, CoreScope s, Guid u, GenerateMedia request, string kind, CancellationToken ct)
    {
        var mime = kind == "Speech" ? "audio/wav" : "image/png";
        foreach (var snapshot in request.Output.License.SourceSnapshotIds) await Source(db, s, u, snapshot, request.Output.Locale, mime, "Generate", ct);
        foreach (var id in request.ReferenceVersionIds.Concat(request.Output.ParentVersionIds ?? []).Distinct())
        {
            var version = await Assert(db, s, u, id, "Generate", true, ct);
            var material = EditorialBinding.Read<MediaLibrary.Material>(version.SnapshotJson);
            if (!material.Request.License.Adaptation) throw new RightsBlockedException("MediaAdaptationNotPermitted");
            var asset = await db.Assets.AsNoTracking().SingleAsync(x => x.Id == version.AssetReferenceId, ct);
            await new SourceRightsGuard(db).CheckAsync(s, asset.ContentRevisionId, new("Generate", material.Request.Locale, "IN", material.Info.Mime.StartsWith("image/") ? "Image" : "Audio", Adaptation: true), ct);
        }
    }
    internal static async Task<MediaVersion?> Reference(CoreDbContext db, CoreScope s, Guid u, Guid reference, string purpose, bool approved, CancellationToken ct)
    { var row = await db.MediaVersions.AsNoTracking().SingleOrDefaultAsync(x => x.AssetReferenceId == reference, ct); return row == null ? null : await Assert(db, s, u, row.Id, purpose, approved, ct); }
    internal static async Task Revision(CoreDbContext db, CoreScope s, Guid revision, string purpose, CancellationToken ct)
    {
        var lineage = await new SourceRightsGuard(db).LineageAsync(revision, ct); var ids = lineage.Edges.Where(x => x.ParentRevisionId.HasValue).Select(x => x.ParentRevisionId!.Value).Append(revision).ToArray();
        foreach (var row in await (from media in db.MediaVersions.AsNoTracking() join asset in db.Assets on media.AssetReferenceId equals asset.Id where ids.Contains(asset.ContentRevisionId) || db.Provenance.Any(x => x.AssetReferenceId == asset.Id && ids.Contains(x.ContentRevisionId ?? Guid.Empty)) select media).ToListAsync(ct)) await Assert(db, s, row.UniverseId, row.Id, purpose, true, ct);
        var owner = await (from r in db.Revisions join c in db.Contents on r.ContentId equals c.Id where r.Id == revision select new { c.UniverseId, c.TenantId }).SingleAsync(ct);
        foreach (var presentation in await CastPresentations(db, new(owner.TenantId, s.Actor), owner.UniverseId, ids, ct))
        {
            if (owner.TenantId != s.TenantId && !await db.MediaPackages.AnyAsync(x => x.PresentationRevisionId == presentation, ct)) continue;
            await Presentation(db, s, owner.UniverseId, presentation, purpose, ct);
        }
    }
    internal static async Task<Guid[]> CastPresentations(CoreDbContext db, CoreScope s, Guid u, Guid[] ids, CancellationToken ct)
    {
        var factory = await db.FactoryArtifacts.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && ids.Contains(x.CoreRevisionId)).Select(x => x.SnapshotJson).ToListAsync(ct);
        var stories = await db.RegistryRevisions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == "Story").Select(x => x.RulesJson).ToListAsync(ct);
        return factory.SelectMany(x => EditorialBinding.Read<FactoryArtifactSnapshot>(x).Input.Cast.Cast)
            .Concat(stories.Select(x => EditorialBinding.Read<RegistryEpisodeSnapshot>(x)).Where(x => x.ContentRevisionId.HasValue && ids.Contains(x.ContentRevisionId.Value)).SelectMany(x => x.Cast))
            .Where(x => x.Presentation != null).Select(x => x.Presentation!.Id).Distinct().ToArray();
    }
    internal static async Task Presentation(CoreDbContext db, CoreScope s, Guid u, Guid id, string purpose, CancellationToken ct)
    {
        var presentation = await db.RegistryRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == "Presentation", ct) ?? throw new CoreAccessException();
        var rules = EditorialBinding.Read<RegistryPresentationRules>(presentation.RulesJson);
        var package = await db.MediaPackages.AsNoTracking().SingleOrDefaultAsync(x => x.PresentationRevisionId == id, ct);
        if (package == null) { if (await db.MediaVersions.AnyAsync(x => x.AssetReferenceId == rules.VisualAssetReferenceId || x.AssetReferenceId == rules.VoiceAssetReferenceId, ct)) throw new EvidenceBlockedException("ApprovedMediaPackageBindingRequired"); return; }
        if (package.ReviewStatus != "Approved") throw new EvidenceBlockedException("CharacterMediaPackageWithdrawnOrUnreviewed"); var p = EditorialBinding.Read<CharacterMediaRules>(package.SnapshotJson);
        foreach (var version in p.AdditionalVersionIds.Append(p.VisualVersionId).Append(p.VoiceVersionId)) await Assert(db, s, u, version, purpose, true, ct);
    }
}


