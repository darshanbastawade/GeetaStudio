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

public sealed partial class LessonEngine(CoreDbContext db, ITranslationProvider translation) : ILessonEngine
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Decode<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)!;
    private async Task Access(CoreScope s, Guid u, bool write, CancellationToken ct)
    {
        var m = await db.Members.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor, ct);
        if (m == null || write && m.Permission != "Administrator" || !await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
    }
    private async Task<Guid> Command<T>(CoreScope s, Guid u, Guid op, string kind, T request, Func<Task<Guid>> action, CancellationToken ct)
    {
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable operation required.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Lessons:" + s.TenantId}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000,'Lesson lock unavailable',1;", ct);
        await Access(s, u, true, ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Encode(new { u, request }))));
        var old = await db.LessonCommands.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (old != null)
        {
            if (old.Actor != s.Actor || old.Kind != kind || old.Fingerprint != hash) throw new RuntimeConflictException("Lesson operation replay differs.");
            return old.ResultId;
        }
        var id = await action();
        db.LessonCommands.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = op, Actor = s.Actor, Kind = kind, Fingerprint = hash, ResultId = id });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = "lessons." + kind });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
        await tx.CommitAsync(ct); return id;
    }
    private static void Review(CoreRecord row, LessonReview r)
    {
        if (Convert.ToBase64String(row.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
        CoreRules.Text(r.EvidenceReference, 1000, "Human review evidence");
        if (!r.EvidenceReference.StartsWith("manual:") && !r.EvidenceReference.StartsWith("fixture:")) throw new RuntimeValidationException("Explicit human or controlled fixture review required.");
    }
    private static LessonProfileView View(LessonProfile p) => new(p.Id, Decode<RegisterLessonProfile>(p.RulesJson), p.ReviewStatus, p.ReviewEvidence, Convert.ToBase64String(p.RowVersion));
    private async Task<LessonProfile> Profile(CoreScope s, Guid u, Guid id, string kind, CancellationToken ct, bool approved = true)
    {
        var p = await db.LessonProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == kind, ct) ?? throw new CoreAccessException();
        if (approved && p.ReviewStatus != "Approved") throw new RuntimeValidationException("Profile requires human review.", "UnreviewedLessonProfile");
        return p;
    }
    public Task<Guid> RegisterProfileAsync(CoreScope s, Guid u, RegisterLessonProfile r, CancellationToken ct) =>
        Command(s, u, r.OperationId, "profile", r, async () =>
        {
            LessonRules.Profile(r);
            if (r.Audience != null && !await db.Audiences.AnyAsync(x => x.Id == r.Audience.AudienceId && x.UniverseId == u, ct)) throw new CoreAccessException();
            if (r.Writing is { } w)
            {
                var parent = Decode<RegisterLessonProfile>((await Profile(s, u, w.AudienceProfileId, "Audience", ct, false)).RulesJson).Audience!;
                if (w.MinAge < parent.MinAge || w.MaxAge > parent.MaxAge) throw new RuntimeValidationException("Writing band must fit its audience band; these are not consent ages.");
                foreach (var id in w.EligibleCharacters) if (!await db.Characters.AnyAsync(x => x.Id == id && x.UniverseId == u, ct)) throw new CoreAccessException();
            }
            if (r.Locale is { } l && !await db.Locales.AnyAsync(x => x.Id == l.Locale, ct))
            {
                if (!System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.SpecificCultures).Any(x => x.Name.Equals(l.Locale, StringComparison.OrdinalIgnoreCase)))
                    throw new RuntimeValidationException("Locale is not recognized by the installed language catalog.", "UnsupportedLocale");
                db.Locales.Add(new() { Id = l.Locale, Name = r.Key + " (proposed delivery locale)" });
            }
            if (await db.LessonProfiles.AnyAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key && x.Version == r.Version, ct)) throw new RuntimeConflictException("Create a new profile version or replay the original operation.");
            var p = new LessonProfile { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, Kind = r.Kind, RulesJson = Encode(r) };
            db.LessonProfiles.Add(p); return p.Id;
        }, ct);
    public async Task<IReadOnlyList<LessonProfileView>> ProfilesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); return (await db.LessonProfiles.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderBy(x => x.Key).ThenBy(x => x.Version).Take(200).ToListAsync(ct)).Select(View).ToArray();
    }
    public async Task ReviewProfileAsync(CoreScope s, Guid u, Guid id, LessonReview r, CancellationToken ct) =>
        _ = await Command(s, u, r.OperationId, "profile-review", new { id, r }, async () =>
        {
            var p = await db.LessonProfiles.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
            Review(p, r); if (p.ReviewStatus != "Draft") throw new RuntimeConflictException("Review is frozen; register a new version.");
            p.ReviewStatus = r.Approve ? "Approved" : "Rejected"; p.ReviewEvidence = r.EvidenceReference; return p.Id;
        }, ct);
    private async Task<LessonBundle> Bundle(CoreScope s, Guid u, Guid id, CancellationToken ct) =>
        await db.LessonBundles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task Recheck(CoreScope s, Guid u, LessonBundleSnapshot bundle, string locale, string purpose, CancellationToken ct)
    {
        var engine = new EvidenceEngine(db);
        await engine.RequireLessonClaimAsync(s, u, bundle.Request.AnchorClaimRevisionId, new("Read", "en-IN", "IN", "Text"), ct);
        await engine.RequireLessonClaimAsync(s, u, bundle.Request.InterpretationClaimRevisionId, new("Read", "en-IN", "IN", "Text"), ct);
        if (bundle.Request.SanskritTextId is Guid text)
        {
            var t = await db.CorpusTexts.AsNoTracking().SingleAsync(x => x.Id == text, ct);
            if (!await db.CorpusVerses.AnyAsync(x => x.Id == t.VerseId && x.ReviewStatus == "Approved", ct)) throw new EvidenceBlockedException("UnreviewedCanonicalEvidence");
        }
        foreach (var id in bundle.SourceIds)
        {
            var actualLocale = purpose == "Read" ? await (from snap in db.SourceSnapshots join edition in db.SourceEditions on snap.EditionId equals edition.Id where snap.Id == id select edition.Language).SingleAsync(ct) : locale;
            var rights = await new SourceRightsGuard(db).DecideAsync(s, id, new(purpose, actualLocale, "IN", "Text", Adaptation: purpose != "Read"), ct);
            if (!rights.Decision.Allowed) throw new RightsBlockedException(rights.Decision.Reason);
        }
    }
    public async Task<LessonBundleView> RegisterBundleAsync(CoreScope s, Guid u, RegisterLessonBundle r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "bundle", r, async () =>
        {
            LessonRules.Words(r.LearningGoals, 10, true); LessonRules.Words(r.Names, 20); LessonRules.Words(r.Concepts, 20, true);
            var engine = new EvidenceEngine(db);
            var anchor = await engine.RequireLessonClaimAsync(s, u, r.AnchorClaimRevisionId, new("Read", "en-IN", "IN", "Text"), ct);
            var meaning = await engine.RequireLessonClaimAsync(s, u, r.InterpretationClaimRevisionId, new("Read", "en-IN", "IN", "Text"), ct);
            if (anchor.Type is not ("Translation" or "Quotation") || anchor.Locale != "en-IN" || meaning.Type != "Interpretation" || meaning.Locale != "en-IN") throw new RuntimeValidationException("Bundle needs an approved exact English source anchor and separately attributed interpretation.");
            var anchorSpan = await db.ClaimSpans.AsNoTracking().SingleAsync(x => x.ClaimRevisionId == anchor.Id && x.Relation == "Supporting", ct);
            var anchorText = await db.CorpusTexts.AsNoTracking().SingleAsync(x => x.Id == anchorSpan.CorpusTextId, ct);
            if (!await db.ClaimSpans.AnyAsync(x => x.ClaimRevisionId == meaning.Id && x.Relation == "Supporting" && x.CorpusTextId == anchorText.Id && x.SnapshotId == anchorSpan.SnapshotId, ct)) throw new EvidenceBlockedException("InterpretationNotBoundToAnchor");
            string? sanskrit = null;
            var sources = await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == anchor.Id || x.ClaimRevisionId == meaning.Id).Select(x => x.SnapshotId).Distinct().ToListAsync(ct);
            if (r.SanskritTextId is Guid sa)
            {
                var t = await db.CorpusTexts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sa && x.VerseId == anchorText.VerseId && x.Kind == "DirectQuotation" && x.Locale == "sa-IN", ct) ?? throw new CoreAccessException();
                sanskrit = t.ExactText; sources.Add(t.SnapshotId);
            }
            var citations = new List<CitationExport>();
            foreach (var source in sources.Distinct())
            {
                var language = await (from snap in db.SourceSnapshots join e in db.SourceEditions on snap.EditionId equals e.Id where snap.Id == source select e.Language).SingleAsync(ct);
                var decision = await new SourceRightsGuard(db).DecideAsync(s, source, new("Read", language, "IN", "Text"), ct);
                if (!decision.Decision.Allowed || decision.Citation == null) throw new RightsBlockedException(decision.Decision.Reason);
                citations.Add(decision.Citation);
            }
            var snapshot = new LessonBundleSnapshot(r, anchor.Statement, anchorSpan.Locator, meaning.Statement, meaning.Author, meaning.Tradition, meaning.Uncertainty, sanskrit, sources.Distinct().ToArray(), citations.ToArray());
            var bundle = new LessonBundle { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, AnchorClaimRevisionId = anchor.Id, InterpretationClaimRevisionId = meaning.Id, SnapshotJson = Encode(snapshot) };
            db.LessonBundles.Add(bundle); return bundle.Id;
        }, ct);
        var stored = Decode<LessonBundleSnapshot>((await Bundle(s, u, id, ct)).SnapshotJson);
        try { await Recheck(s, u, stored, "en-IN", "Read", ct); }
        catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException) { stored = Withheld(stored); }
        return new(id, stored);
    }
    private static LessonBundleSnapshot Withheld(LessonBundleSnapshot b) => b with { Anchor = "[Withheld]", Interpretation = "[Withheld]", Sanskrit = null, Citations = [] };
    public async Task<IReadOnlyList<LessonBundleView>> BundlesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var result = new List<LessonBundleView>();
        foreach (var row in await db.LessonBundles.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct))
        {
            var b = Decode<LessonBundleSnapshot>(row.SnapshotJson);
            try { await Recheck(s, u, b, "en-IN", "Read", ct); }
            catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException) { b = Withheld(b); }
            result.Add(new(row.Id, b));
        }
        return result;
    }
    public Task<Guid> RegisterMemoryAsync(CoreScope s, Guid u, RegisterLessonMemory r, CancellationToken ct) =>
        Command(s, u, r.OperationId, "memory", r, async () =>
        {
            var l = Decode<RegisterLessonProfile>((await Profile(s, u, r.LocaleProfileId, "Locale", ct)).RulesJson).Locale!;
            var b = Decode<LessonBundleSnapshot>((await Bundle(s, u, r.BundleId, ct)).SnapshotJson);
            await Recheck(s, u, b, l.Locale, "Adapt", ct);
            if (r.Source.SourceAnchor != Anchor(b) || r.Source.Interpretation != Meaning(b) || !r.Source.Names.SequenceEqual(b.Request.Names) || !r.Source.Concepts.SequenceEqual(b.Request.Concepts)) throw new RuntimeValidationException("Memory source must match the pinned evidence bundle.");
            LessonRules.Memory(r.Source, r.Target, l); CoreRules.Text(r.MeaningReviewNotes, 2000, "Translation meaning review notes");
            var entry = new LessonMemory { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, LocaleProfileId = r.LocaleProfileId, BundleId = r.BundleId, EntryJson = Encode(r) }; db.LessonMemories.Add(entry); return entry.Id;
        }, ct);
    public async Task<IReadOnlyList<LessonMemoryView>> MemoryAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var result = new List<LessonMemoryView>();
        foreach (var row in await db.LessonMemories.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).Take(100).ToListAsync(ct))
        {
            var entry = Decode<RegisterLessonMemory>(row.EntryJson);
            var b = Decode<LessonBundleSnapshot>((await Bundle(s, u, entry.BundleId, ct)).SnapshotJson);
            var l = Decode<RegisterLessonProfile>((await Profile(s, u, entry.LocaleProfileId, "Locale", ct, false)).RulesJson).Locale!;
            try { await Recheck(s, u, b, l.Locale, "Adapt", ct); }
            catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException) { continue; }
            result.Add(new(row.Id, entry, row.ReviewStatus, row.ReviewEvidence, Convert.ToBase64String(row.RowVersion)));
        }
        return result;
    }
    public async Task ReviewMemoryAsync(CoreScope s, Guid u, Guid id, LessonReview r, CancellationToken ct) =>
        _ = await Command(s, u, r.OperationId, "memory-review", new { id, r }, async () =>
        {
            var m = await db.LessonMemories.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
            Review(m, r); if (m.ReviewStatus != "Draft") throw new RuntimeConflictException("Memory review is frozen; create a new entry.");
            var entry = Decode<RegisterLessonMemory>(m.EntryJson);
            var l = Decode<RegisterLessonProfile>((await Profile(s, u, m.LocaleProfileId, "Locale", ct)).RulesJson).Locale!;
            await Recheck(s, u, Decode<LessonBundleSnapshot>((await Bundle(s, u, m.BundleId, ct)).SnapshotJson), l.Locale, "Adapt", ct);
            LessonRules.Memory(entry.Source, entry.Target, l);
            m.ReviewStatus = r.Approve ? "Approved" : "Rejected"; m.ReviewEvidence = r.EvidenceReference; return m.Id;
        }, ct);
    private static string Anchor(LessonBundleSnapshot b) => b.Anchor + "\nSource locator: " + b.Locator + "\n" + string.Join("\n", b.Citations.Select(x => x.Citation + "; " + x.RequiredAttribution));
    private static string Meaning(LessonBundleSnapshot b) => b.Interpretation + "\nInterpretation: " + b.InterpretationAuthor + "; " + b.Tradition + "; " + b.Uncertainty;
    public async Task<IReadOnlyList<LessonCastCandidate>> CandidatesAsync(CoreScope s, Guid u, Guid writingProfile, CancellationToken ct)
    {
        await Access(s, u, false, ct);
        var w = Decode<RegisterLessonProfile>((await Profile(s, u, writingProfile, "Writing", ct)).RulesJson).Writing!;
        return (await CharactersAsync(s, u, ct)).Where(x => w.EligibleLifeStages.Contains(x.LifeStage) && (w.EligibleCharacters.Length == 0 || w.EligibleCharacters.Contains(x.CharacterId)))
            .Select(x => x with { Role = w.LeadRoles[0] }).ToArray();
    }
    public async Task<IReadOnlyList<LessonCastCandidate>> CharactersAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct);
        return await (from c in db.Characters
                      join r in db.CharacterRevisions on c.Id equals r.CharacterId
                      where c.UniverseId == u && c.Status == "Active" && c.PersistenceScope != "EpisodeOnly" && r.FictionalStatus == "Fictional" &&
                      !db.RegistryRevisions.Any(p => p.Kind == "Character" && p.CharacterId == c.Id && p.ReviewStatus != "Approved" &&
                          !db.RegistryRevisions.Any(n => n.Kind == "Character" && n.CharacterId == c.Id && n.Version > p.Version)) &&
                      !db.CharacterRevisions.Any(x => x.CharacterId == c.Id && x.Version > r.Version)
                      orderby c.Key
                      select new LessonCastCandidate(c.Id, r.Id, r.DisplayName, r.LifeStage, "Available")).Take(200).ToListAsync(ct);
    }
    public Task<Guid> RegisterCharacterAsync(CoreScope s, Guid u, RegisterLessonCharacter r, CancellationToken ct) =>
        Command(s, u, r.OperationId, "character-activated", r, async () =>
        {
            CoreRules.Text(r.Key, 100, "Character key"); CoreRules.Text(r.Name, 200, "Character name"); CoreRules.Text(r.LifeStage, 200, "Canonical life stage"); CoreRules.Text(r.Biography, 2000, "Biography");
            if (!r.EvidenceReference.StartsWith("manual:") && !r.EvidenceReference.StartsWith("fixture:")) throw new RuntimeValidationException("Explicit identity/audience suitability review required.");
            if (await db.Characters.AnyAsync(x => x.UniverseId == u && x.Key == r.Key, ct)) throw new RuntimeConflictException("Identity exists; M09 does not rewrite canonical life stage.");
            var character = new Character { Id = Guid.NewGuid(), UniverseId = u, Key = r.Key, Status = "Active", PersistenceScope = "Guest" };
            db.Characters.Add(character); db.CharacterRevisions.Add(new() { Id = Guid.NewGuid(), CharacterId = character.Id, Version = 1, DisplayName = r.Name, LifeStage = r.LifeStage, Biography = r.Biography });
            return character.Id;
        }, ct);
    public async Task<LessonVariantView> AdaptAsync(CoreScope s, Guid u, AdaptLesson r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "variant", r, async () =>
        {
            var a = View(await Profile(s, u, r.AudienceProfileId, "Audience", ct)); var w = View(await Profile(s, u, r.WritingProfileId, "Writing", ct));
            var l = View(await Profile(s, u, r.LocaleProfileId, "Locale", ct)); var route = View(await Profile(s, u, r.TranslationRouteId, "TranslationRoute", ct));
            var audience = a.Rules.Audience!; var writing = w.Rules.Writing!; var locale = l.Rules.Locale!;
            if (writing.AudienceProfileId != a.Id) throw new RuntimeValidationException("Writing profile belongs to a different audience version.");
            var bundle = Decode<LessonBundleSnapshot>((await Bundle(s, u, r.BundleId, ct)).SnapshotJson);
            await Recheck(s, u, bundle, locale.Locale, "Adapt", ct);
            var parent = r.ParentVariantId == null ? null : await db.LessonVariants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.ParentVariantId && x.TenantId == s.TenantId && x.UniverseId == u && x.BundleId == r.BundleId, ct) ?? (r.ParentVariantId == null ? null : throw new CoreAccessException());
            var source = new LessonSections(audience.Situation + " Setting: " + writing.Setting + ". " + writing.ConflictComplexity + ".", Anchor(bundle), Meaning(bundle), audience.Example, audience.PracticalAction, audience.Reflection, bundle.Request.LearningGoals, bundle.Request.Names, bundle.Request.Concepts);
            LessonRules.Gate(source, audience, l.Rules.Locale! with { RequiredConcepts = [] });
            var memoryRows = await db.LessonMemories.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u &&
                x.BundleId == r.BundleId && x.LocaleProfileId == l.Id && x.ReviewStatus == "Approved").OrderBy(x => x.Id).Take(101).ToListAsync(ct);
            if (memoryRows.Count > 100) throw new RuntimeValidationException("Translation memory exceeds the per-bundle/profile limit.");
            var memory = memoryRows.Select(x => new LessonMemoryView(x.Id, Decode<RegisterLessonMemory>(x.EntryJson), x.ReviewStatus, x.ReviewEvidence, Convert.ToBase64String(x.RowVersion))).ToArray();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(route.Rules.Route!.DeadlineSeconds));
            var result = await translation.TranslateAsync(new(source, locale, route.Rules.Route, memory), timeout.Token);
            LessonRules.Memory(source, result.Sections, locale); LessonRules.Gate(result.Sections, audience, locale);
            if (locale.Locale == locale.SourceLocale && Encode(result.Sections) != Encode(source))
                throw new RuntimeValidationException("Source-locale output must preserve the approved treatment.");
            if (result.Adapter != route.Rules.Route.Adapter || locale.Locale != locale.SourceLocale && !memory.Any(x => x.Id == result.MemoryId && Encode(x.Entry.Source) == Encode(source) && Encode(x.Entry.Target) == Encode(result.Sections))) throw new RuntimeValidationException("Translation adapter returned unapproved or rebound output.");
            var cast = (await CandidatesAsync(s, u, w.Id, ct)).Take(writing.MaxCast).Select((x, i) => x with { Role = i == 0 ? writing.LeadRoles[0] : writing.SupportingRoles.FirstOrDefault() ?? writing.LeadRoles[0] }).ToArray();
            var snapshot = new LessonVariantSnapshot(bundle, a, w, l, route, cast, result.MemoryId);
            var variant = new LessonVariant { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, BundleId = r.BundleId, ParentVariantId = parent?.Id, Version = (parent?.Version ?? 0) + 1, SnapshotJson = Encode(snapshot), SectionsJson = Encode(result.Sections) };
            db.LessonVariants.Add(variant); return variant.Id;
        }, ct);
        return await InspectAsync(s, u, id, ct);
    }
    public async Task<LessonVariantView> InspectAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        await Access(s, u, false, ct);
        var row = await db.LessonVariants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        var snap = Decode<LessonVariantSnapshot>(row.SnapshotJson); var withheld = false;
        try { await Recheck(s, u, snap.Bundle, snap.Locale.Rules.Locale!.Locale, "Adapt", ct); }
        catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException) { withheld = true; snap = snap with { Bundle = Withheld(snap.Bundle) }; }
        return new(row.Id, row.BundleId, row.ParentVariantId, row.Version, row.ReviewStatus, withheld ? null : Decode<LessonSections>(row.SectionsJson), snap, withheld, Convert.ToBase64String(row.RowVersion), row.ReviewEvidence);
    }
    public async Task<IReadOnlyList<LessonVariantView>> VariantsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var result = new List<LessonVariantView>();
        foreach (var id in await db.LessonVariants.Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => x.Id).ToListAsync(ct)) result.Add(await InspectAsync(s, u, id, ct));
        return result;
    }
    public async Task ReviewVariantAsync(CoreScope s, Guid u, Guid id, LessonReview r, CancellationToken ct) =>
        _ = await Command(s, u, r.OperationId, "variant-review", new { id, r }, async () =>
        {
            var row = await db.LessonVariants.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
            Review(row, r); if (row.ReviewStatus != "Draft") throw new RuntimeConflictException("Variant review is frozen; adapt a linked revision.");
            var snap = Decode<LessonVariantSnapshot>(row.SnapshotJson);
            await Recheck(s, u, snap.Bundle, snap.Locale.Rules.Locale!.Locale, "Adapt", ct);
            LessonRules.Gate(Decode<LessonSections>(row.SectionsJson), snap.Audience.Rules.Audience!, snap.Locale.Rules.Locale!);
            row.ReviewStatus = r.Approve ? "Approved" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; return id;
        }, ct);
    public async Task<LessonVariantView> ExportAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        var ownsTransaction = db.Database.CurrentTransaction == null;
        if (ownsTransaction) db.ChangeTracker.Clear();
        await using var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var view = await InspectAsync(s, u, id, ct);
        if (view.OutputWithheld) throw new RightsBlockedException("LessonEvidenceIneligible");
        if (view.ReviewStatus != "Approved" || view.ReviewEvidence?.StartsWith("manual:") != true ||
            new[] { view.Snapshot.Audience, view.Snapshot.Writing, view.Snapshot.Locale, view.Snapshot.Route }.Any(x => x.ReviewEvidence?.StartsWith("manual:") != true))
            throw new RuntimeValidationException("Release requires human-reviewed locale, audience, writing, route and variant; fixtures cannot qualify publication.", "HumanReviewRequired");
        if (view.Snapshot.TranslationMemoryId is Guid memory)
        {
            var m = await db.LessonMemories.AsNoTracking().SingleAsync(x => x.Id == memory, ct);
            if (m.ReviewEvidence?.StartsWith("manual:") != true) throw new RuntimeValidationException("Translation requires human review.", "HumanReviewRequired");
        }
        var engine = new EvidenceEngine(db);
        foreach (var claim in new[] { view.Snapshot.Bundle.Request.AnchorClaimRevisionId, view.Snapshot.Bundle.Request.InterpretationClaimRevisionId })
        {
            var verified = await engine.RequireLessonClaimAsync(s, u, claim, new("Read", "en-IN", "IN", "Text"), ct);
            if (verified.ReviewEvidence?.StartsWith("manual:") != true) throw new RuntimeValidationException("Fixture claims cannot qualify a lesson release.", "HumanReviewRequired");
        }
        await Recheck(s, u, view.Snapshot.Bundle, view.Snapshot.Locale.Rules.Locale!.Locale, "Export", ct);
        if (tx != null) await tx.CommitAsync(ct);
        return view; // This is an export gate, never a publishing operation.
    }
}
public static class LessonServices
{
    public static IServiceCollection AddLessons(this IServiceCollection services)
    {
        services.AddScoped<ILessonEngine, LessonEngine>(); services.AddSingleton<ITranslationProvider, ReviewedTranslationProvider>(); return services;
    }
}
public sealed class ReviewedTranslationProvider(IEnumerable<ITranslationAdapter> adapters) : ITranslationProvider
{
    public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (JsonSerializer.Serialize(request.Source).Length > request.Route.MaxCharacters) throw new RuntimeValidationException("Translation input exceeds route bound.");
        var adapter = adapters.SingleOrDefault(x => x.Key == request.Route.Adapter) ?? throw new RuntimeValidationException("Translation adapter not deployed.", "UnsupportedLocale");
        var result = adapter.Translate(request);
        ct.ThrowIfCancellationRequested();
        if (JsonSerializer.Serialize(result.Sections).Length > request.Route.MaxCharacters) throw new RuntimeValidationException("Translation output exceeds route bound.");
        return Task.FromResult(result);
    }
}
