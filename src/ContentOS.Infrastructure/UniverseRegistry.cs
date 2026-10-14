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

public sealed partial class UniverseRegistry(CoreDbContext db, IAssetStore assets, ILessonEngine lessons) : IUniverseRegistry
{
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value)!;
    private static RegistryRevisionView<T> View<T>(RegistryRevision r) => new(r.Id, r.Key, r.Version, Read<T>(r.RulesJson), r.ReviewStatus, r.ReviewEvidence, Convert.ToBase64String(r.RowVersion));
    private async Task Access(CoreScope s, Guid u, bool write, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct) ||
           !await db.Members.AnyAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor && (!write || x.Permission == "Administrator"), ct)) throw new CoreAccessException();
    }
    private async Task<Guid> Command<T>(CoreScope s, Guid u, Guid op, string kind, T request, Func<Task<Guid>> action, CancellationToken ct)
    {
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable operation required.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Registry:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Registry lock unavailable',1;", ct);
        await Access(s, u, true, ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(new { u, request }))));
        var old = await db.RegistryOperations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (old != null) { if (old.Actor != s.Actor || old.Kind != kind || old.Fingerprint != hash) throw new RuntimeConflictException("Registry replay differs."); return old.ResultId; }
        var id = await action();
        db.RegistryOperations.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = op, Actor = s.Actor, Kind = kind, Fingerprint = hash, ResultId = id });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = "registry." + kind });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
        await tx.CommitAsync(ct); return id;
    }
    private async Task<RegistryRevision> Revision(CoreScope s, Guid u, Guid id, string kind, CancellationToken ct, bool approved = false)
    {
        var row = await db.RegistryRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == kind, ct) ?? throw new CoreAccessException();
        if (approved && row.ReviewStatus != "Approved") throw new RuntimeValidationException("Artifact needs review.", "UnreviewedRegistryArtifact");
        return row;
    }
    private async Task<Guid> AddRevision(CoreScope s, Guid u, string key, string kind, int version, object rules, CancellationToken ct,
        Guid? character = null, Guid? coreRevision = null, Guid? other = null, Guid? episode = null)
    {
        CoreRules.Text(key, 100, "Registry key");
        if (version < 1) throw new RuntimeValidationException("Positive version required.");
        var prior = await db.RegistryRevisions.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == kind && x.Key == key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (prior != null && (version != prior.Version + 1 || prior.CharacterId != character || prior.EpisodeId != episode)) throw new RuntimeConflictException("Version must append to the same scoped identity.");
        if (prior == null && version != 1) throw new RuntimeConflictException("First version must be one.");
        var row = new RegistryRevision { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = key, Kind = kind, Version = version, RulesJson = Json(rules), CharacterId = character, CoreRevisionId = coreRevision, OtherCharacterId = other, EpisodeId = episode };
        db.RegistryRevisions.Add(row); return row.Id;
    }
    public Task<Guid> RegisterBibleAsync(CoreScope s, Guid u, RegisterBible r, CancellationToken ct) => Command(s, u, r.OperationId, "bible", r, async () =>
    {
        RegistryRules.Bible(r.Rules); foreach (var a in r.Rules.Audiences) await Audience(u, a.AudienceId, ct);
        return await AddRevision(s, u, r.Key, "Bible", r.Version, r.Rules, ct);
    }, ct);
    private async Task Audience(Guid u, Guid id, CancellationToken ct) { if (!await db.Audiences.AnyAsync(x => x.Id == id && x.UniverseId == u, ct)) throw new CoreAccessException(); }
    private async Task<Character> Character(Guid u, Guid id, CancellationToken ct) => await db.Characters.SingleOrDefaultAsync(x => x.Id == id && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<RegistryEpisode> Episode(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.RegistryEpisodes.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<RegistryRevision> Profile(CoreScope s, Guid u, Guid character, CancellationToken ct) => await db.RegistryRevisions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == "Character" && x.CharacterId == character).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct) ?? throw new CoreAccessException();
    private async Task<RegistryCharacterView> CharacterView(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        var c = await Character(u, id, ct); var p = await Profile(s, u, id, ct);
        return new(c.Id, p.CoreRevisionId!.Value, c.Key, c.PersistenceScope, c.Status, Convert.ToBase64String(c.RowVersion), View<RegistryCharacterRules>(p), p.OtherCharacterId);
    }
    public async Task<RegistryCharacterView> RegisterCharacterAsync(CoreScope s, Guid u, RegisterRegistryCharacter r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "character", r, async () =>
        {
            RegistryRules.Character(r.Rules, r.PersistenceScope); CoreRules.Text(r.Key, 100, "Character key");
            foreach (var a in r.Rules.AudienceIds) await Audience(u, a, ct);
            foreach (var locale in r.Rules.Locales) if (!await db.Locales.AnyAsync(x => x.Id == locale, ct)) throw new RuntimeValidationException("Register the locale before character eligibility.");
            if (r.Rules.EpisodeId is Guid e) await Episode(s, u, e, ct);
            Character c; RegistryRevision? prior = null;
            if (r.CharacterId is Guid existing)
            {
                c = await Character(u, existing, ct);
                if (Convert.ToBase64String(c.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
                if (c.Key != r.Key || c.PersistenceScope != r.PersistenceScope) throw new RuntimeConflictException("Identity key/scope changes require lifecycle operations.");
                prior = await db.RegistryRevisions.Where(x => x.Kind == "Character" && x.CharacterId == existing).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
                var core = await db.CharacterRevisions.Where(x => x.CharacterId == existing).OrderByDescending(x => x.Version).FirstAsync(ct);
                if (core.LifeStage != r.Rules.LifeStage || core.FictionalStatus != r.Rules.FictionalStatus) throw new RuntimeConflictException("Canonical life stage/identity cannot be rewritten; duplicate as a distinct reviewed identity.");
                if (prior != null) { var old = Read<RegistryCharacterRules>(prior.RulesJson); if (old.VisualAnchor != r.Rules.VisualAnchor || old.VoiceAnchor != r.Rules.VoiceAnchor || old.EpisodeId != r.Rules.EpisodeId) throw new RuntimeConflictException("Stable identity anchors and original episode scope cannot change."); }
                if (prior != null) c.Status = "Draft";
            }
            else
            {
                if (await db.Characters.AnyAsync(x => x.UniverseId == u && x.Key == r.Key, ct) || await db.Aliases.AnyAsync(x => x.UniverseId == u && x.Name == r.Key, ct)) throw new RuntimeConflictException("Character key/alias already exists.");
                c = new() { Id = Guid.NewGuid(), UniverseId = u, Key = r.Key, PersistenceScope = r.PersistenceScope }; db.Characters.Add(c);
            }
            if (r.DistinctFromCharacterId is Guid from) { await Character(u, from, ct); if (from == c.Id) throw new RuntimeValidationException("Distinct identity cannot derive from itself."); }
            foreach (var alias in r.Rules.Aliases.Append(r.Rules.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                CoreRules.Text(alias, 200, "Alias");
                if (await db.Characters.AnyAsync(x => x.UniverseId == u && x.Key == alias && x.Id != c.Id, ct) || await db.Aliases.AnyAsync(x => x.UniverseId == u && x.Name == alias && x.CharacterId != c.Id, ct)) throw new RuntimeConflictException("Alias belongs to another identity.");
                if (!await db.Aliases.AnyAsync(x => x.UniverseId == u && x.Name == alias, ct) && !db.Aliases.Local.Any(x => x.UniverseId == u && x.Name.Equals(alias, StringComparison.OrdinalIgnoreCase))) db.Aliases.Add(new() { UniverseId = u, CharacterId = c.Id, Name = alias });
            }
            var latest = await db.CharacterRevisions.Where(x => x.CharacterId == c.Id).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (latest == null || latest.DisplayName != r.Rules.Name || latest.Biography != r.Rules.Biography)
            {
                latest = new() { Id = Guid.NewGuid(), CharacterId = c.Id, Version = (latest?.Version ?? 0) + 1, DisplayName = r.Rules.Name, LifeStage = r.Rules.LifeStage, Biography = r.Rules.Biography, FictionalStatus = r.Rules.FictionalStatus }; db.CharacterRevisions.Add(latest);
            }
            await AddRevision(s, u, r.Key, "Character", (prior?.Version ?? 0) + 1, r.Rules, ct, c.Id, latest.Id, prior?.OtherCharacterId ?? r.DistinctFromCharacterId);
            return c.Id;
        }, ct);
        return await CharacterView(s, u, id, ct);
    }
    public async Task ControlCharacterAsync(CoreScope s, Guid u, Guid id, CharacterLifecycle r, CancellationToken ct) =>
        _ = await Command(s, u, r.OperationId, "lifecycle", new { id, r }, async () =>
        {
            RegistryRules.ReviewEvidence(r.EvidenceReference); var c = await Character(u, id, ct);
            if (Convert.ToBase64String(c.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
            var profile = await Profile(s, u, id, ct);
            if (r.Action == "Retire") { if (c.Status == "Retired") throw new RuntimeConflictException("Already retired."); c.Status = "Retired"; }
            else if (r.Action == "Activate")
            {
                if (profile.ReviewStatus != "Approved") throw new RuntimeValidationException("Review the current profile before activation.");
                await Identity(s, u, Read<RegistryCharacterRules>(profile.RulesJson), ct); c.Status = "Active";
            }
            else if (r.Action == "PromoteGuest")
            {
                if (c.PersistenceScope is not ("Guest" or "EpisodeOnly") || c.Status != "Active" || profile.ReviewStatus != "Approved") throw new RuntimeValidationException("Only an active reviewed guest/episode-only identity can be promoted.");
                c.PersistenceScope = "Recurring";
            }
            else throw new RuntimeValidationException("Unsupported lifecycle action.");
            return id;
        }, ct);
    private async Task Identity(CoreScope s, Guid u, RegistryCharacterRules p, CancellationToken ct, bool release = false)
    {
        if (p.FictionalStatus == "Fictional") return;
        if (p.IdentityEvidence == null || p.IdentityEvidence.Length == 0 || p.IdentityPermissionReference?.StartsWith("manual:") != true) throw new RuntimeValidationException("Real/historical profiles require reviewed evidence and explicit identity/likeness/voice/history permissions; a familiar name is insufficient.");
        foreach (var id in p.IdentityEvidence) { var claim = await new EvidenceEngine(db).RequireLessonClaimAsync(s, u, id, new(release ? "Publish" : "Read", p.Locales[0], "IN", "Text"), ct); if (release && claim.ReviewEvidence?.StartsWith("manual:") != true) throw new RuntimeValidationException("Fixture identity evidence cannot qualify release."); }
    }
    public Task<Guid> RegisterPresentationAsync(CoreScope s, Guid u, RegisterRegistryPresentation r, CancellationToken ct) => Command(s, u, r.OperationId, "presentation", r, async () =>
    {
        var p = await Revision(s, u, r.Rules.ProfileRevisionId, "Character", ct); var rules = Read<RegistryCharacterRules>(p.RulesJson);
        await Audience(u, r.Rules.AudienceId, ct);
        if (!rules.AudienceIds.Contains(r.Rules.AudienceId) || !rules.Locales.Contains(r.Rules.Locale) || rules.VisualAnchor != r.Rules.VisualAnchor || rules.VoiceAnchor != r.Rules.VoiceAnchor ||
           r.Rules.EstimatedCost < 0 || r.Rules.EstimatedCost > 100000) throw new RuntimeValidationException("Presentation must preserve profile identity, locale/audience and bounded cost.");
        CoreRules.Text(r.Rules.Style, 200, "Presentation style"); CoreRules.Text(r.Rules.PronunciationGuide, 2000, "Pronunciation guide"); CoreRules.Text(r.Rules.Currency, 10, "Currency");
        foreach (var asset in new[] { r.Rules.VisualAssetReferenceId, r.Rules.VoiceAssetReferenceId }) if (asset is Guid a) await Asset(s, u, a, r.Rules.Locale, false, ct);
        return await AddRevision(s, u, r.Key, "Presentation", r.Version, r.Rules, ct, p.CharacterId, p.CoreRevisionId);
    }, ct);
    public Task<Guid> RegisterRelationshipAsync(CoreScope s, Guid u, RegisterRegistryRelationship r, CancellationToken ct) => Command(s, u, r.OperationId, "relationship", r, async () =>
    {
        await Character(u, r.Rules.FromCharacterId, ct); await Character(u, r.Rules.ToCharacterId, ct);
        if (r.Rules.FromCharacterId == r.Rules.ToCharacterId) throw new RuntimeValidationException("Relationship needs distinct identities.");
        CoreRules.Text(r.Rules.Kind, 200, "Relationship"); CoreRules.Text(r.Rules.Continuity, 2000, "Continuity"); CoreRules.Text(r.Rules.ArcState, 2000, "Arc state");
        return await AddRevision(s, u, r.Key, "Relationship", r.Version, r.Rules, ct, r.Rules.FromCharacterId, other: r.Rules.ToCharacterId);
    }, ct);
    public Task<Guid> RegisterEpisodeAsync(CoreScope s, Guid u, RegisterRegistryEpisode r, CancellationToken ct) => Command(s, u, r.OperationId, "episode", r, async () =>
    {
        CoreRules.Text(r.Key, 100, "Episode key"); if (await db.RegistryEpisodes.AnyAsync(x => x.UniverseId == u && x.Key == r.Key, ct)) throw new RuntimeConflictException("Episode exists; append an explicit revision.");
        var episode = new RegistryEpisode { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key }; db.RegistryEpisodes.Add(episode); return episode.Id;
    }, ct);
    private async Task<IReadOnlyList<RegistryRevisionView<T>>> List<T>(CoreScope s, Guid u, string kind, CancellationToken ct)
    {
        await Access(s, u, false, ct); return (await db.RegistryRevisions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == kind).OrderBy(x => x.Key).ThenBy(x => x.Version).Take(200).ToListAsync(ct)).Select(View<T>).ToArray();
    }
    public Task<IReadOnlyList<RegistryRevisionView<UniverseBibleRules>>> BiblesAsync(CoreScope s, Guid u, CancellationToken ct) => List<UniverseBibleRules>(s, u, "Bible", ct);
    public Task<IReadOnlyList<RegistryRevisionView<RegistryPresentationRules>>> PresentationsAsync(CoreScope s, Guid u, CancellationToken ct) => List<RegistryPresentationRules>(s, u, "Presentation", ct);
    public Task<IReadOnlyList<RegistryRevisionView<RegistryRelationshipRules>>> RelationshipsAsync(CoreScope s, Guid u, CancellationToken ct) => List<RegistryRelationshipRules>(s, u, "Relationship", ct);
    public async Task<IReadOnlyList<RegistryCharacterView>> CharactersAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var result = new List<RegistryCharacterView>();
        foreach (var id in await db.Characters.AsNoTracking().Where(x => x.UniverseId == u && db.RegistryRevisions.Any(p => p.CharacterId == x.Id && p.Kind == "Character")).OrderBy(x => x.Key).Take(200).Select(x => x.Id).ToListAsync(ct)) result.Add(await CharacterView(s, u, id, ct));
        return result;
    }
    public async Task<IReadOnlyList<RegistryEpisodeView>> EpisodesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); return (await db.RegistryEpisodes.AsNoTracking().Where(x => x.UniverseId == u && x.TenantId == s.TenantId).OrderBy(x => x.Key).Take(200).ToListAsync(ct)).Select(x => new RegistryEpisodeView(x.Id, x.Key, x.CurrentVersion, Convert.ToBase64String(x.RowVersion))).ToArray();
    }
    public async Task<RegistryCastPlan> CastAsync(CoreScope s, Guid u, CastingBrief r, CancellationToken ct)
    {
        await Access(s, u, false, ct); RegistryRules.Brief(r); await Audience(u, r.AudienceId, ct); await Episode(s, u, r.EpisodeId, ct);
        await Revision(s, u, r.BibleRevisionId, "Bible", ct, true);
        var all = new List<RegistryCastCandidate>(); var selected = new List<RegistryCastCandidate>();
        var presentations = await PresentationsAsync(s, u, ct);
        foreach (var role in r.Roles)
        {
            foreach (var c in await CharactersAsync(s, u, ct))
            {
                var p = c.Profile.Rules; var reasons = new List<string>();
                var pinned = r.PinnedChoices.SingleOrDefault(x => x.CharacterId == c.CharacterId && x.Role == role);
                if (c.Status == "Retired") reasons.Add("Retired");
                if (c.PersistenceScope == "EpisodeOnly" && p.EpisodeId != r.EpisodeId) reasons.Add("Different episode scope");
                if (!p.AudienceIds.Contains(r.AudienceId) || !p.Locales.Contains(r.Locale) || !p.Roles.Contains(role)) reasons.Add("Audience, locale or role incompatible");
                if (r.SituationTags.Length != 0 && !r.SituationTags.Any(p.SituationTags.Contains) || !r.SkillTags.All(p.SkillTags.Contains) ||
                   !r.LearningObjectives.All(p.LearningObjectives.Contains) || !r.ContinuityTags.All(p.ContinuityTags.Contains)) reasons.Add("Situation, skills, objective or continuity incompatible");
                if ((c.Status != "Active" || c.Profile.ReviewStatus != "Approved") && !(r.Mode == "Draft" && pinned != null)) reasons.Add("Needs current profile approval/activation");
                if (c.Profile.ReviewStatus == "Rejected") reasons.Add("Rejected profile");
                var presentation = presentations.Where(x => x.ReviewStatus == "Approved" && !presentations.Any(y => y.Key == x.Key && y.Version > x.Version) && x.Rules.ProfileRevisionId == c.Profile.Id && x.Rules.AudienceId == r.AudienceId && x.Rules.Locale == r.Locale)
                    .OrderBy(x => x.Key).ThenByDescending(x => x.Version).FirstOrDefault(x => pinned?.PresentationRevisionId == null || x.Id == pinned.PresentationRevisionId);
                if (pinned?.PresentationRevisionId != null && presentation == null) reasons.Add("Pinned presentation incompatible or unreviewed");
                if (presentation != null) try { await MediaRightsGuard.Presentation(db, s, u, presentation.Id, "Reuse", ct); } catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException) { reasons.Add("Media package unavailable"); }
                if (r.Budget.RequireMedia)
                {
                    if (presentation == null || presentation.Rules.VisualAssetReferenceId == null || presentation.Rules.VoiceAssetReferenceId == null) reasons.Add("Approved media package missing");
                    else try { await Asset(s, u, presentation.Rules.VisualAssetReferenceId.Value, r.Locale, true, ct); await Asset(s, u, presentation.Rules.VoiceAssetReferenceId.Value, r.Locale, true, ct); } catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException) { reasons.Add("Media rights or review unavailable"); }
                }
                if (r.Mode == "Production") try { await Identity(s, u, p, ct); } catch (RuntimeValidationException) { reasons.Add("Real/historical identity permission/evidence missing"); }
                var cost = presentation?.Rules.EstimatedCost ?? 0;
                if (presentation != null && presentation.Rules.Currency != r.Budget.Currency || cost > r.Budget.MaxEstimatedCost) reasons.Add("Budget/currency incompatible");
                if (reasons.Count == 0) reasons.Add(c.Profile.ReviewStatus == "Approved" ? "Eligible: reviewed role/situation/audience/continuity" : "Eligible for an explicitly pinned draft only");
                all.Add(new(c, role, reasons.All(x => x.StartsWith("Eligible")), reasons.ToArray(), presentation, cost));
            }
        }
        if (r.PinnedChoices.Any(x => !r.Roles.Contains(x.Role))) throw new RuntimeValidationException("Pinned role not requested.");
        // Solve distinct role assignments together, reserving pinned identities and respecting the total budget.
        // Bound pathological editor briefs rather than silently falling back to an incompatible cast.
        var options = r.Roles.ToDictionary(role => role, role => all.Where(c => c.Role == role && c.Eligible &&
            !r.PinnedChoices.Any(p => p.CharacterId == c.Character.CharacterId && p.Role != role) &&
            !r.PinnedChoices.Any(p => p.Role == role && p.CharacterId != c.Character.CharacterId))
            .OrderBy(c => c.EstimatedCost).ThenBy(c => c.Character.Key).ToArray());
        foreach (var pin in r.PinnedChoices) if (options[pin.Role].Length == 0) throw new RuntimeValidationException("Pinned choice is unavailable/incompatible; overrides cannot bypass eligibility.");
        var order = r.Roles.OrderBy(role => r.PinnedChoices.Any(p => p.Role == role) ? 0 : 1).ThenBy(role => options[role].Length).ToArray();
        var current = new List<RegistryCastCandidate>(); var best = new List<RegistryCastCandidate>(); var visits = 0;
        bool Assign(int index, decimal cost)
        {
            if (++visits > 10000) throw new RuntimeValidationException("Casting search limit reached; narrow roles or pin eligible choices.");
            if (index == order.Length) { if (current.Count > best.Count) best = [.. current]; return current.Count == order.Length; }
            var role = order[index];
            foreach (var candidate in options[role])
            {
                if (cost + candidate.EstimatedCost > r.Budget.MaxEstimatedCost || current.Any(c => c.Character.CharacterId == candidate.Character.CharacterId)) continue;
                current.Add(candidate); if (Assign(index + 1, cost + candidate.EstimatedCost)) return true; current.RemoveAt(current.Count - 1);
            }
            if (r.PinnedChoices.Any(p => p.Role == role)) return false;
            return Assign(index + 1, cost);
        }
        Assign(0, 0);
        selected = r.Roles.Select(role => best.SingleOrDefault(c => c.Role == role)).OfType<RegistryCastCandidate>().ToList();
        if (r.PinnedChoices.Any(p => !selected.Any(c => c.Character.CharacterId == p.CharacterId && c.Role == p.Role))) throw new RuntimeValidationException("Pinned cast exceeds total budget or distinct-role capacity.");
        var missing = r.Roles.Where(role => !selected.Any(x => x.Role == role)).ToArray();
        var proposals = missing.Select(role => new RegistryCharacterRules("Proposed " + role, "Editor-defined", "Unregistered suggestion; choose canonical life stage and review before use.", "Fictional", ["editor-defined"], ["serve the learning objective"], [], "Editor to review", [role], r.SituationTags, r.SkillTags, r.LearningObjectives, [r.AudienceId], [r.Locale], r.ContinuityTags, "Proposed", "Review pronunciation", "new-identity-required", "new-voice-required", [], r.EpisodeId)).ToArray();
        return new(all.ToArray(), selected.ToArray(), proposals, missing);
    }
    private async Task Asset(CoreScope s, Guid u, Guid id, string locale, bool qualified, CancellationToken ct)
    {
        var a = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && db.Revisions.Any(r => r.Id == x.ContentRevisionId && db.Contents.Any(c => c.Id == r.ContentId && c.UniverseId == u)), ct) ?? throw new CoreAccessException();
        if (!qualified) return;
        var media = await MediaRightsGuard.Reference(db, s, u, id, "Reuse", true, ct);
        if (media != null) { if (await assets.GetAsync(a.StorageAssetId, media.StorageOwner, ct) == null) throw new RightsBlockedException("MediaBytesUnavailable"); return; }
        if (a.RightsStatus is not ("Owned" or "Licensed" or "PublicDomain") || await assets.GetAsync(a.StorageAssetId, s.Actor, ct) == null) throw new RightsBlockedException("UnqualifiedMediaAsset");
        await new SourceRightsGuard(db).CheckAsync(s, a.ContentRevisionId, new("Publish", locale, "IN", "Text"), ct);
        var evidence = await new EvidenceEngine(db).CheckContentAsync(s, a.ContentRevisionId, new("Publish", locale, "IN", "Text"), ct);
        if (!evidence.Eligible) throw new EvidenceBlockedException(evidence.Reason);
    }
    private async Task CheckStoryEvidence(CoreScope s, Guid u, RegistryEpisodeSnapshot r, bool release, CancellationToken ct)
    {
        if (r.LessonVariantId is Guid lesson)
        {
            var v = await lessons.InspectAsync(s, u, lesson, ct);
            if (v.OutputWithheld || v.Snapshot.Audience.Rules.Audience!.AudienceId != r.Brief.AudienceId || v.Snapshot.Locale.Rules.Locale!.Locale != r.Brief.Locale) throw new EvidenceBlockedException("LessonUnavailableOrIncompatible");
            if (release) await lessons.ExportAsync(s, u, lesson, ct);
        }
        if (r.ContentRevisionId is Guid content)
        {
            var rev = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == content && db.Contents.Any(c => c.Id == x.ContentId && c.UniverseId == u && c.TenantId == s.TenantId && c.LocaleId == r.Brief.Locale && c.AudienceId == r.Brief.AudienceId), ct) ?? throw new CoreAccessException();
            var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == rev.ContentId, ct);
            if (release && (head.EditorialState != "Approved" || head.CurrentVersion != rev.Version)) throw new EvidenceBlockedException("CurrentApprovedContentRequired");
            await new SourceRightsGuard(db).CheckAsync(s, content, new(release ? "Publish" : "Read", r.Brief.Locale, "IN", "Text"), ct);
            var gate = await new EvidenceEngine(db).CheckContentAsync(s, content, new(release ? "Publish" : "Read", r.Brief.Locale, "IN", "Text"), ct);
            if (!gate.Eligible) throw new EvidenceBlockedException(gate.Reason);
            if (release && string.Join("\n", r.Beats.Select(b => b.Text)) != rev.Body) throw new EvidenceBlockedException("EpisodeContentTextMismatch");
        }
        else if (release) throw new RuntimeValidationException("Release requires an approved bound content revision.", "ContentReleaseQualificationRequired");
    }
    public async Task<RegistryRevisionView<RegistryEpisodeSnapshot>> ComposeAsync(CoreScope s, Guid u, ComposeRegistryEpisode r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "story", r, async () =>
        {
            var episode = await Episode(s, u, r.Brief.EpisodeId, ct); if (Convert.ToBase64String(episode.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
            var cast = await CastAsync(s, u, r.Brief, ct); if (cast.MissingRoles.Length != 0) throw new RuntimeValidationException("Register/select proposed guests before composing; every speaker needs a stable identity.");
            var bible = View<UniverseBibleRules>(await Revision(s, u, r.Brief.BibleRevisionId, "Bible", ct, true));
            if (r.RelationshipRevisionIds == null || r.RelationshipRevisionIds.Length > 30) throw new RuntimeValidationException("Bounded relationships required.");
            var relationships = new List<RegistryRevisionView<RegistryRelationshipRules>>();
            foreach (var relation in r.RelationshipRevisionIds.Distinct())
            {
                var v = View<RegistryRelationshipRules>(await Revision(s, u, relation, "Relationship", ct, true));
                if (!cast.Selected.Any(x => x.Character.CharacterId == v.Rules.FromCharacterId) || !cast.Selected.Any(x => x.Character.CharacterId == v.Rules.ToCharacterId)) throw new RuntimeValidationException("Relationship must bind selected identities.");
                relationships.Add(v);
            }
            var snapshot = new RegistryEpisodeSnapshot(r.Brief, r.HumorStyle, bible, cast.Selected, relationships.ToArray(), r.Beats, r.ReferencesRealPeople, r.ReferencesCurrentEvents, r.LessonVariantId, r.ContentRevisionId, false, "Original fiction — dialogue is not scripture or history.", r.HumorStyle == "Satire" ? bible.Rules.SatireLabel : "");
            snapshot = snapshot with { EditorialEscalationRequired = RegistryRules.Story(snapshot) };
            await CheckStoryEvidence(s, u, snapshot, false, ct);
            episode.CurrentVersion++;
            return await AddRevision(s, u, episode.Key, "Story", episode.CurrentVersion, snapshot, ct, episode: episode.Id);
        }, ct); return View<RegistryEpisodeSnapshot>(await Revision(s, u, id, "Story", ct));
    }
    public async Task ReviewAsync(CoreScope s, Guid u, Guid id, RegistryReview r, CancellationToken ct) =>
        _ = await Command(s, u, r.OperationId, "review", new { id, r }, async () =>
        {
            RegistryRules.ReviewEvidence(r.EvidenceReference);
            var row = await db.RegistryRevisions.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
            if (Convert.ToBase64String(row.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
            if (row.ReviewStatus != "Draft") throw new RuntimeConflictException("Review is frozen; append a new revision.");
            if (r.Approve)
            {
                if (row.Kind == "Character") { var p = Read<RegistryCharacterRules>(row.RulesJson); RegistryRules.Character(p, (await Character(u, row.CharacterId!.Value, ct)).PersistenceScope); await Identity(s, u, p, ct); }
                else if (row.Kind == "Bible") RegistryRules.Bible(Read<UniverseBibleRules>(row.RulesJson));
                else if (row.Kind == "Presentation")
                {
                    var p = Read<RegistryPresentationRules>(row.RulesJson); await Revision(s, u, p.ProfileRevisionId, "Character", ct, true);
                    await MediaRightsGuard.Presentation(db, s, u, row.Id, "Reuse", ct);
                    foreach (var a in new[] { p.VisualAssetReferenceId, p.VoiceAssetReferenceId }) if (a is Guid asset) await Asset(s, u, asset, p.Locale, true, ct);
                }
                else if (row.Kind == "Story")
                {
                    var snapshot = Read<RegistryEpisodeSnapshot>(row.RulesJson); var escalation = RegistryRules.Story(snapshot);
                    if (escalation) { if (r.EscalationEvidence == null) throw new RuntimeValidationException("Real-person/current-event/satire review requires separate editorial escalation."); RegistryRules.ReviewEvidence(r.EscalationEvidence); }
                    foreach (var c in snapshot.Cast) { if (c.Character.Profile.ReviewStatus != "Approved") throw new RuntimeValidationException("Review selected profile before story approval."); await Identity(s, u, c.Character.Profile.Rules, ct); }
                    await CheckStoryEvidence(s, u, snapshot, false, ct);
                }
            }
            row.ReviewStatus = r.Approve ? "Approved" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; row.EscalationEvidence = r.EscalationEvidence; return id;
        }, ct);
    public async Task<IReadOnlyList<RegistryRevisionView<RegistryEpisodeSnapshot>>> StoriesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        var rows = await List<RegistryEpisodeSnapshot>(s, u, "Story", ct); var result = new List<RegistryRevisionView<RegistryEpisodeSnapshot>>();
        foreach (var r in rows) try { await CheckStoryEvidence(s, u, r.Rules, false, ct); result.Add(r); } catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException) { result.Add(r with { Rules = r.Rules with { Beats = [], FictionLabel = "Withheld: current evidence/rights unavailable." } }); }
        return result;
    }
    public async Task<RegistryRevisionView<RegistryEpisodeSnapshot>> ExportAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Access(s, u, false, ct);
        var row = await Revision(s, u, id, "Story", ct, true); var v = View<RegistryEpisodeSnapshot>(row); RegistryRules.Story(v.Rules);
        if (row.ReviewEvidence?.StartsWith("manual:") != true || v.Rules.Bible.ReviewEvidence?.StartsWith("manual:") != true ||
           v.Rules.Cast.Any(c => c.Character.Profile.ReviewEvidence?.StartsWith("manual:") != true || c.Presentation?.ReviewEvidence?.StartsWith("manual:") != true) ||
           v.Rules.Relationships.Any(x => x.ReviewEvidence?.StartsWith("manual:") != true) ||
           v.Rules.EditorialEscalationRequired && row.EscalationEvidence?.StartsWith("manual:") != true) throw new RuntimeValidationException("Fixtures and incomplete human reviews cannot qualify release.", "HumanReviewRequired");
        foreach (var c in v.Rules.Cast)
        {
            await MediaRightsGuard.Presentation(db, s, u, c.Presentation!.Id, "Publish", ct);
            await Identity(s, u, c.Character.Profile.Rules, ct, true); var p = c.Presentation!.Rules;
            if (p.VisualAssetReferenceId == null || p.VoiceAssetReferenceId == null) throw new RightsBlockedException("ApprovedMediaPackageRequired");
            await Asset(s, u, p.VisualAssetReferenceId.Value, v.Rules.Brief.Locale, true, ct); await Asset(s, u, p.VoiceAssetReferenceId.Value, v.Rules.Brief.Locale, true, ct);
        }
        await CheckStoryEvidence(s, u, v.Rules, true, ct); await tx.CommitAsync(ct); return v; // No publishing operation.
    }
}
public static class RegistryServices
{
    public static IServiceCollection AddUniverseRegistry(this IServiceCollection services) => services.AddScoped<IUniverseRegistry, UniverseRegistry>();
}
