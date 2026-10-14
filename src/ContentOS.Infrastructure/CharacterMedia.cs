using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class MediaLibrary
{
    private async Task<CharacterMediaPackage> Package(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.MediaPackages.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task CheckPackage(CoreScope s, Guid u, CharacterMediaRules r, CancellationToken ct)
    {
        var profile = await db.RegistryRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.ProfileRevisionId && x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == "Character", ct) ?? throw new CoreAccessException();
        var character = Read<RegistryCharacterRules>(profile.RulesJson); var head = await db.Characters.AsNoTracking().SingleAsync(x => x.Id == profile.CharacterId, ct);
        if (profile.ReviewStatus != "Approved" || head.Status == "Retired" || await db.RegistryRevisions.AnyAsync(x => x.CharacterId == head.Id && x.Kind == "Character" && x.Version > profile.Version, ct)) throw new EvidenceBlockedException("CurrentReviewedCharacterRequired");
        if (character.VisualAnchor != r.VisualAnchor || character.VoiceAnchor != r.VoiceAnchor || !character.AudienceIds.Contains(r.AudienceId) || !character.Locales.Contains(r.Locale)) throw new EvidenceBlockedException("MediaPackageMustPreserveCharacterIdentityAndAudience");
        CoreRules.Text(r.PresentationStyle, 200, "Presentation style");
        if (r.Palette.Length is < 1 or > 8 || r.Palette.Any(x => !System.Text.RegularExpressions.Regex.IsMatch(x, "^#[0-9a-fA-F]{6}$")) || r.StyleRules.Length is < 1 or > 20 || r.Poses.Length is < 1 or > 12 || r.SceneReferences.Length > 12 || r.AdditionalVersionIds.Length > 20 || r.Pronunciation.Length > 20) throw new RuntimeValidationException("Bounded palette/style/pose/scene/pronunciation package required.");
        foreach (var text in r.StyleRules.Concat(r.Poses).Concat(r.SceneReferences)) CoreRules.Text(text, 500, "Reference rule");
        foreach (var pronunciation in r.Pronunciation) { CoreRules.Text(pronunciation.Word, 100, "Pronunciation word"); CoreRules.Text(pronunciation.SpeakAs, 100, "Pronunciation substitution"); }
        var visual = await MediaRightsGuard.Assert(db, s, u, r.VisualVersionId, "Reuse", true, ct); var voice = await MediaRightsGuard.Assert(db, s, u, r.VoiceVersionId, "Reuse", true, ct);
        var v = Read<Material>(visual.SnapshotJson); var audio = Read<Material>(voice.SnapshotJson);
        if (v.Info.Mime != "image/png" || audio.Info.Mime != "audio/wav" || v.Request.CharacterProfileRevisionId != profile.Id || audio.Request.CharacterProfileRevisionId != profile.Id || audio.Request.Locale != r.Locale) throw new EvidenceBlockedException("ActualImageAndVoiceMustBindTheExactCharacterRevision");
        foreach (var id in r.AdditionalVersionIds) await MediaRightsGuard.Assert(db, s, u, id, "Reuse", true, ct);
        if (audio.Request.GenerationId.HasValue) { var run = await db.MediaGenerations.AsNoTracking().SingleAsync(x => x.Id == audio.Request.GenerationId, ct); var snap = Read<MediaGenerationSnapshot>(run.SnapshotJson); if (snap.Profile.Kind != "Speech" || snap.Profile.Key != r.VoiceProfileKey || snap.Profile.ResourceKey != r.VoiceResourceKey) throw new EvidenceBlockedException("VoiceMappingIsBoundToItsOriginalProviderProfile"); }
        else if (r.VoiceProfileKey != "owned-recording.v1" || r.VoiceResourceKey != voice.Id.ToString("D")) throw new EvidenceBlockedException("ImportedVoiceUsesOwnedRecordingIdentityNotAnInventedProviderVoice");
    }
    public Task<Guid> PackageAsync(CoreScope s, Guid u, RegisterCharacterMedia r, CancellationToken ct) => Command(s, u, r.OperationId, "package", r, "Admin", async () =>
    {
        await CheckPackage(s, u, r.Rules, ct); CoreRules.Text(r.Key, 100, "Package key"); var old = await db.MediaPackages.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (r.Version != (old?.Version ?? 0) + 1) throw new RuntimeConflictException("Append the next reviewed media package version.");
        var row = new CharacterMediaPackage { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r.Rules) }; db.MediaPackages.Add(row); return row.Id;
    }, ct);
    public async Task<CharacterMediaView[]> PackagesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, "Read", ct); var result = new List<CharacterMediaView>(); foreach (var row in await db.MediaPackages.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)) { var blocks = new List<string>(); var rules = Read<CharacterMediaRules>(row.SnapshotJson); try { await CheckPackage(s, u, rules, ct); } catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { blocks.Add(e.Message); } result.Add(new(row.Id, row.Key, row.Version, rules, row.ReviewStatus, row.ReviewEvidence, row.PresentationRevisionId, Convert.ToBase64String(row.RowVersion), blocks.ToArray())); }
        return result.ToArray();
    }
    public async Task ReviewPackageAsync(CoreScope s, Guid u, Guid id, ReviewMedia r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "package-review", new { id, r }, "Review", async () =>
    {
        var row = await Package(s, u, id, ct); Expected(row, r.ExpectedRowVersion); MediaRules.Attest(r.Mode, r.EvidenceReference);
        if (r.Decision is not ("Approve" or "Reject" or "Withdraw") || r.Deviations.Length > 20) throw new RuntimeValidationException("Explicit package review required.");
        if (r.Decision == "Approve") { if (row.ReviewStatus != "Draft" || r.Deviations.Length != 0) throw new EvidenceBlockedException("ChangedProviderOrPresentationNeedsNewQualityReview"); var rules = Read<CharacterMediaRules>(row.SnapshotJson); await CheckPackage(s, u, rules, ct); if (r.Mode == "Fixture") { foreach (var asset in rules.AdditionalVersionIds.Append(rules.VisualVersionId).Append(rules.VoiceVersionId)) { var m = Read<Material>((await Version(s, u, asset, ct)).SnapshotJson); if (m.Request.License.Kind != "Fixture") throw new EvidenceBlockedException("NonFixturePackageRequiresManualQualityReview"); } } }
        row.ReviewStatus = r.Decision == "Approve" ? "Approved" : r.Decision == "Reject" ? "Rejected" : "Withdrawn"; row.ReviewEvidence = r.EvidenceReference; row.ReviewJson = Json(r); return id;
    }, ct);
    public async Task<Guid> BindPackageAsync(CoreScope s, Guid u, Guid id, Guid operation, CancellationToken ct)
    {
        await Access(s, u, "Admin", ct); var row = await Package(s, u, id, ct); if (row.ReviewStatus != "Approved") throw new EvidenceBlockedException("ReviewPackageBeforeBinding"); var rules = Read<CharacterMediaRules>(row.SnapshotJson); await CheckPackage(s, u, rules, ct);

        var image = await Version(s, u, rules.VisualVersionId, ct); var voice = await Version(s, u, rules.VoiceVersionId, ct);
        var profile = await db.RegistryRevisions.AsNoTracking().SingleAsync(x => x.Id == rules.ProfileRevisionId, ct);
        // Each package revision creates its own immutable presentation; provider/style changes never edit an approved binding.
        var presentation = row.PresentationRevisionId ?? await registry.RegisterPresentationAsync(s, u, new(Derived(id, "presentation"), "media-" + id.ToString("N"), 1, new(profile.Id, rules.AudienceId, rules.Locale, rules.PresentationStyle, rules.VisualAnchor, rules.VoiceAnchor, rules.Pronunciation.Length == 0 ? Read<RegistryCharacterRules>(profile.RulesJson).PronunciationGuide : string.Join("; ", rules.Pronunciation.Select(x => x.Word + "=" + x.SpeakAs)), image.AssetReferenceId, voice.AssetReferenceId, 0, "INR")), ct);
        return await Command(s, u, operation, "package-bind", new { id, presentation }, "Admin", async () => { var current = await Package(s, u, id, ct); if (current.ReviewStatus != "Approved") throw new EvidenceBlockedException("PackageChangedDuringBinding"); await CheckPackage(s, u, Read<CharacterMediaRules>(current.SnapshotJson), ct); current.PresentationRevisionId = presentation; return presentation; }, ct);
    }
}
