using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed class CanonicalCorpus(CoreDbContext db) : ICanonicalCorpus
{
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string TextHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private async Task AccessAsync(CoreScope s, Guid universe, int level, CancellationToken ct)
    {
        var m = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.TenantId == s.TenantId && m.Actor == s.Actor, ct);
        if (m == null || level == 2 && m.Permission != "Administrator" || level == 1 && m.Permission is not ("Editor" or "Administrator") ||
            !await db.Universes.AnyAsync(u => u.Id == universe && u.TenantId == s.TenantId, ct)) throw new CoreAccessException();
    }
    private async Task<CorpusManifest> ManifestAsync(Guid universe, Guid id, CancellationToken ct) =>
        await db.CorpusManifests.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id && m.UniverseId == universe, ct) ?? throw new CoreAccessException();
    private async Task<CorpusManifestView> ViewAsync(CorpusManifest m, CancellationToken ct) =>
        new(m.Id, m.Key, m.Version, m.Edition, m.BaselineTarget, await db.CorpusLocators.CountAsync(l => l.ManifestId == m.Id, ct), m.MaterialKind);
    private void Audit(CoreScope s, Guid id, string action) => db.SourceAudits.Add(new SourceAudit { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = action });
    private async Task<Guid> CommandAsync<T>(CoreScope s, Guid u, Guid operation, string kind, T request, Func<Task<Guid>> execute, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(s, u, 1, ct);
        if (operation == Guid.Empty) throw new RuntimeValidationException("A stable import operation ID is required.");
        object replay = request is RegisterCorpus c && c.MaterialKind == "Scripture"
            ? new { c.OperationId, c.Key, c.Version, c.Edition, c.BaselineTarget, c.Inventory, c.Bindings } : request!;
        var fingerprint = Hash(new { u, request = replay });
        var prior = await db.CorpusCommands.FromSqlInterpolated($"SELECT * FROM [corpus].[CorpusCommand] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {s.TenantId} AND [OperationId] = {operation}").SingleOrDefaultAsync(ct);
        if (prior != null)
        {
            if (prior.Actor != s.Actor || prior.Kind != kind || prior.Fingerprint != fingerprint) throw new RuntimeConflictException("Operation ID already belongs to another corpus command.");
            return prior.ResultId;
        }
        var result = await execute();
        db.CorpusCommands.Add(new CorpusCommand { Id = Guid.NewGuid(), TenantId = s.TenantId, OperationId = operation, Kind = kind, Actor = s.Actor, Fingerprint = fingerprint, ResultId = result });
        Audit(s, result, kind); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }
    public Task<Guid> RegisterAsync(CoreScope scope, Guid universe, RegisterCorpus r, CancellationToken ct) =>
        CommandAsync(scope, universe, r.OperationId, "corpus.registered", r, async () =>
        {
            if (r.MaterialKind is not ("Scripture" or "History")) throw new RuntimeValidationException("Explicit supported corpus material kind required.");
            CoreRules.Text(r.Key, 200, "Manifest key"); CoreRules.Text(r.Edition, 1000, "Edition");
            if (r.Version < 1 || r.BaselineTarget is < 1 or > 1000 || r.Inventory == null || r.Inventory.Count is < 1 or > 2000 ||
                r.Inventory.Count(x => x.BaselineVerse != null) != r.BaselineTarget) throw new RuntimeValidationException("Inventory must explicitly reconcile the declared baseline and variants.");
            var keys = new HashSet<(int, int, string)>(); var baselines = new HashSet<(int, int)>();
            foreach (var l in r.Inventory)
            {
                CorpusRules.Locator(l.Chapter, l.Verse);
                if (l.VariantKey == null || l.VariantKey.Length > 100 || l.Note == null || l.Note.Length > 1000 ||
                    !keys.Add((l.Chapter, l.Verse, l.VariantKey)) || l.BaselineVerse is int b && (b < 1 || !baselines.Add((l.Chapter, b))) ||
                    l.BaselineVerse == null && (string.IsNullOrWhiteSpace(l.VariantKey) || string.IsNullOrWhiteSpace(l.Note)))
                    throw new RuntimeValidationException("Duplicate or inconsistent manifest locator/variant.");
            }
            foreach (var chapter in r.Inventory.Where(l => l.BaselineVerse != null).GroupBy(l => l.Chapter))
                if (!chapter.Select(l => l.BaselineVerse!.Value).Order().SequenceEqual(Enumerable.Range(1, chapter.Count())))
                    throw new RuntimeValidationException("Baseline chapter inventory must be contiguous.");
            if (r.Bindings != null)
            {
                await AccessAsync(scope, universe, 2, ct);
                if (r.Bindings.Count > 20000) throw new RuntimeValidationException("Source bindings exceed the bounded inventory.");
                var bindingKeys = new HashSet<(int, int, string, string, string)>();
                var editions = new Dictionary<(string, string), Guid>();
                foreach (var b in r.Bindings)
                {
                    if (!keys.Contains((b.Chapter, b.Verse, b.VariantKey)) || b.SourceChapter != b.Chapter || b.SourceVerse != b.Verse ||
                        (r.MaterialKind == "History" ? b.Kind != "HistoricalSource" : b.Kind is not ("DirectQuotation" or "Translation" or "Transliteration" or "WordMeaning")) ||
                        !bindingKeys.Add((b.Chapter, b.Verse, b.VariantKey, b.Kind, b.Locale)))
                        throw new RuntimeValidationException("Source bindings must match declared edition chapter/verse locators.");
                    CoreRules.Text(b.SourceLocator, 1000, "Source locator");
                    if (b.Sha256 == null || b.Sha256.Length != 64 || !b.Sha256.All(Uri.IsHexDigit))
                        throw new RuntimeValidationException("Binding requires an exact UTF-8 SHA-256.");
                    var source = await SourceAsync(scope, universe, b.SnapshotId, b.Locale, ct);
                    if (source.Text == null) throw new RuntimeValidationException("Bindings require retained source evidence.");
                    if (r.MaterialKind == "Scripture" && editions.TryGetValue((b.Kind, b.Locale), out var edition) && edition != source.EditionId)
                        throw new RuntimeValidationException("A component locale must bind one source edition per manifest version.");
                    editions[(b.Kind, b.Locale)] = source.EditionId;
                }
            }
            var fingerprint = r.Bindings == null ? Hash(new { r.Key, r.Version, r.Edition, r.BaselineTarget, r.Inventory })
                : Hash(new { r.Key, r.Version, r.Edition, r.BaselineTarget, r.Inventory, r.Bindings });
            if (r.MaterialKind == "History") fingerprint = Hash(new { r.MaterialKind, fingerprint });
            var existing = await db.CorpusManifests.SingleOrDefaultAsync(m => m.UniverseId == universe && m.Key == r.Key && m.Version == r.Version, ct);
            if (existing != null)
            {
                if (existing.Fingerprint != fingerprint) throw new RuntimeConflictException("Manifest version is immutable; register a new version.");
                return existing.Id;
            }
            var m = new CorpusManifest { Id = Guid.NewGuid(), TenantId = scope.TenantId, UniverseId = universe, Key = r.Key, Version = r.Version, Edition = r.Edition, MaterialKind = r.MaterialKind, BaselineTarget = r.BaselineTarget, Fingerprint = fingerprint };
            db.CorpusManifests.Add(m);
            foreach (var l in r.Inventory) db.CorpusLocators.Add(new CorpusLocator { ManifestId = m.Id, Chapter = l.Chapter, Verse = l.Verse, VariantKey = l.VariantKey, BaselineVerse = l.BaselineVerse, Note = l.Note });
            if (r.Bindings != null)
                foreach (var b in r.Bindings) db.CorpusBindings.Add(new CorpusSourceBinding
                {
                    ManifestId = m.Id,
                    Chapter = b.Chapter,
                    Verse = b.Verse,
                    VariantKey = b.VariantKey,
                    Kind = b.Kind,
                    Locale = b.Locale,
                    SnapshotId = b.SnapshotId,
                    SourceChapter = b.SourceChapter,
                    SourceVerse = b.SourceVerse,
                    Sha256 = b.Sha256.ToUpperInvariant(),
                    SourceLocator = b.SourceLocator
                });
            return m.Id;
        }, ct);
    public async Task<IReadOnlyList<CorpusManifestView>> ListAsync(CoreScope scope, Guid universe, CancellationToken ct)
    {
        await AccessAsync(scope, universe, 0, ct); var result = new List<CorpusManifestView>();
        foreach (var m in await db.CorpusManifests.AsNoTracking().Where(m => m.UniverseId == universe).OrderByDescending(m => m.CreatedUtc).Take(100).ToListAsync(ct)) result.Add(await ViewAsync(m, ct));
        return result;
    }
    private async Task<SourceSnapshot> SourceAsync(CoreScope scope, Guid universe, Guid snapshot, string locale, CancellationToken ct)
    {
        var source = await (from s in db.SourceSnapshots
                            join e in db.SourceEditions on s.EditionId equals e.Id
                            join w in db.SourceWorks on e.WorkId equals w.Id
                            where s.Id == snapshot && w.TenantId == scope.TenantId && w.UniverseId == universe && e.Language == locale
                            select s).SingleOrDefaultAsync(ct)
            ?? throw new CoreAccessException();
        return source;
    }
    private Task<bool> BoundAsync(Guid manifest, int chapter, int verse, string variant, string kind, string locale,
        Guid snapshot, string hash, string locator, CancellationToken ct) =>
        db.CorpusBindings.AnyAsync(b => b.ManifestId == manifest && b.Chapter == chapter && b.Verse == verse && b.VariantKey == variant &&
            b.Kind == kind && b.Locale == locale && b.SnapshotId == snapshot && b.Sha256 == hash &&
            b.SourceLocator == locator && b.SourceChapter == chapter && b.SourceVerse == verse, ct);
    private Task<bool> BoundAsync(CorpusVerse v, CorpusText t, CancellationToken ct) =>
        BoundAsync(v.ManifestId, v.Chapter, v.Verse, v.VariantKey, t.Kind, t.Locale, t.SnapshotId, t.Sha256, t.SourceLocator, ct);
    public async Task<CorpusImportResult> ImportAsync(CoreScope scope, Guid universe, Guid manifest, ImportCorpus r, CancellationToken ct)
    {
        var added = 0; var unchanged = 0;
        await CommandAsync(scope, universe, r.OperationId, "corpus.imported", new { manifest, r }, async () =>
        {
            var definition = await ManifestAsync(universe, manifest, ct);
            if (r.Verses == null || r.Verses.Count is < 1 or > 100) throw new RuntimeValidationException("Import a bounded batch of 1–100 verses.");
            var keys = new HashSet<(int, int, string)>();
            foreach (var verse in r.Verses)
            {
                CorpusRules.Locator(verse.Chapter, verse.Verse);
                if (verse.VariantKey == null || !keys.Add((verse.Chapter, verse.Verse, verse.VariantKey)) ||
                    !await db.CorpusLocators.AnyAsync(l => l.ManifestId == manifest && l.Chapter == verse.Chapter && l.Verse == verse.Verse && l.VariantKey == verse.VariantKey, ct))
                    throw new RuntimeValidationException("Duplicate or undeclared chapter/verse locator.");
                var fingerprint = Hash(verse);
                var prior = await db.CorpusVerses.SingleOrDefaultAsync(v => v.ManifestId == manifest && v.Chapter == verse.Chapter && v.Verse == verse.Verse && v.VariantKey == verse.VariantKey, ct);
                if (prior != null && prior.Fingerprint != fingerprint)
                    throw new RuntimeConflictException("Cited verse data is immutable; create a new manifest/edition version.");
                if (verse.Texts == null || verse.Texts.Count is < 1 or > 10 || (definition.MaterialKind == "History" ? verse.Texts.Count != 1 || verse.Texts[0].Kind != "HistoricalSource" : verse.Texts.Count(t => t.Kind == "DirectQuotation") != 1))
                    throw new RuntimeValidationException("Exactly one Sanskrit direct quotation is required; absent translations remain explicit.");
                var components = new HashSet<(string, string)>();
                foreach (var t in verse.Texts)
                {
                    if ((definition.MaterialKind == "History" ? t.Kind != "HistoricalSource" : t.Kind is not ("DirectQuotation" or "Translation" or "Transliteration" or "WordMeaning")) || !components.Add((t.Kind, t.Locale)))
                        throw new RuntimeValidationException("Invalid or duplicate corpus text component.");
                    CorpusRules.Exact(t.ExactText); CoreRules.Text(t.SourceLocator, 1000, "Source locator");
                    if (!TextHash(t.ExactText).Equals(t.Sha256, StringComparison.OrdinalIgnoreCase)) throw new RuntimeValidationException("Source text hash mismatch.");
                    if (t.Kind == "DirectQuotation" && (!t.Locale.StartsWith("sa", StringComparison.Ordinal) || !t.ExactText.Any(c => c is >= '\u0900' and <= '\u097F')))
                        throw new RuntimeValidationException("Sanskrit quotation must contain source Devanagari.");
                    if (t.Kind == "DirectQuotation") CorpusRules.QuotationLocator(t.ExactText, verse.Verse);
                    var source = await SourceAsync(scope, universe, t.SnapshotId, t.Locale, ct);
                    if (source.Text == null || !source.Text.Contains(t.ExactText, StringComparison.Ordinal))
                        throw new RuntimeValidationException("Exact source text must occur unchanged in its permitted retained snapshot.");
                    if (!await BoundAsync(manifest, verse.Chapter, verse.Verse, verse.VariantKey, t.Kind, t.Locale,
                        t.SnapshotId, TextHash(t.ExactText), t.SourceLocator, ct))
                        throw new RuntimeValidationException("Text does not match its pinned manifest source binding; register a bound version.");
                }
                if (prior != null) { unchanged++; continue; }
                var v = new CorpusVerse { Id = Guid.NewGuid(), ManifestId = manifest, Chapter = verse.Chapter, Verse = verse.Verse, VariantKey = verse.VariantKey, Fingerprint = fingerprint };
                db.CorpusVerses.Add(v);
                foreach (var t in verse.Texts) db.CorpusTexts.Add(new CorpusText
                {
                    Id = Guid.NewGuid(),
                    VerseId = v.Id,
                    Kind = t.Kind,
                    Locale = t.Locale,
                    ExactText = t.ExactText,
                    Sha256 = TextHash(t.ExactText),
                    SearchText = CorpusRules.Normalize(t.ExactText),
                    SnapshotId = t.SnapshotId,
                    SourceLocator = t.SourceLocator
                });
                added++;
            }
            return manifest;
        }, ct);
        // Operation replay reports already stored rows without claiming another write.
        if (added + unchanged == 0) unchanged = r.Verses.Count;
        return new(manifest, r.Verses.Count, added, unchanged);
    }
    private static void Expected(CoreRecord row, CorpusReview r)
    {
        if (r.Status is not ("Approved" or "Rejected")) throw new RuntimeValidationException("Explicit review outcome is required.");
        CoreRules.Text(r.EvidenceReference, 1000, "Review evidence");
        if (!r.EvidenceReference.StartsWith("evidence:", StringComparison.Ordinal) && !r.EvidenceReference.StartsWith("manual:", StringComparison.Ordinal) && !r.EvidenceReference.StartsWith("fixture:", StringComparison.Ordinal))
            throw new RuntimeValidationException("Review evidence reference is required.");
        byte[] expected;
        try { expected = Convert.FromBase64String(r.ExpectedRowVersion); } catch (Exception e) when (e is FormatException or ArgumentNullException) { throw new RuntimeValidationException("Invalid row version."); }
        if (!row.RowVersion.SequenceEqual(expected)) throw new CoreConcurrencyException();
    }
    public async Task ReviewAsync(CoreScope scope, Guid universe, Guid manifest, Guid verse, CorpusReview r, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, 2, ct); await ManifestAsync(universe, manifest, ct);
        var v = await db.CorpusVerses.SingleOrDefaultAsync(v => v.Id == verse && v.ManifestId == manifest, ct) ?? throw new CoreAccessException();
        Expected(v, r); if (v.ReviewStatus != "Pending") throw new RuntimeConflictException("Review is frozen; register a corrected version.");
        if (r.Status == "Approved")
            foreach (var t in await db.CorpusTexts.Where(t => t.VerseId == verse).ToListAsync(ct))
            {
                if (!await BoundAsync(v, t, ct)) throw new RightsBlockedException("UnboundEvidence");
                var result = await new SourceRightsGuard(db).DecideAsync(scope, t.SnapshotId, new("Read", t.Locale, "IN", "Text"), ct);
                if (!result.Decision.Allowed) throw new RightsBlockedException(result.Decision.Reason);
                db.RightsDecisions.Add(result.Decision);
            }
        v.ReviewStatus = r.Status; v.Reviewer = scope.Actor; v.ReviewEvidence = r.EvidenceReference;
        Audit(scope, verse, "corpus.verse-reviewed"); await SaveAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
    }
    private async Task<(RightsDecision Decision, CitationExport? Citation)> RightsAsync(CoreScope scope, CorpusText t,
        string territory, Dictionary<(Guid, string), (RightsDecision, CitationExport?)> cache, CancellationToken ct)
    {
        if (cache.TryGetValue((t.SnapshotId, t.Locale), out var cached)) return cached;
        var result = await new SourceRightsGuard(db).DecideAsync(scope, t.SnapshotId, new("Read", t.Locale, territory, "Text"), ct);
        db.RightsDecisions.Add(result.Decision); cache.Add((t.SnapshotId, t.Locale), result); return result;
    }
    public async Task<CorpusReport> ReportAsync(CoreScope scope, Guid universe, Guid manifest, string territory, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, 0, ct); var m = await ManifestAsync(universe, manifest, ct);
        CoreRules.Text(territory, 40, "Territory");
        var verses = await db.CorpusVerses.AsNoTracking().Where(v => v.ManifestId == manifest).ToListAsync(ct);
        var ids = verses.Select(v => v.Id).ToArray(); var texts = await db.CorpusTexts.AsNoTracking().Where(t => ids.Contains(t.VerseId)).ToListAsync(ct);
        var cache = new Dictionary<(Guid, string), (RightsDecision, CitationExport?)>(); var entries = new List<CorpusEntryReport>();
        foreach (var l in await db.CorpusLocators.AsNoTracking().Where(l => l.ManifestId == manifest).OrderBy(l => l.Chapter).ThenBy(l => l.Verse).ToListAsync(ct))
        {
            var v = verses.SingleOrDefault(v => v.Chapter == l.Chapter && v.Verse == l.Verse && v.VariantKey == l.VariantKey);
            var available = texts.Where(t => t.VerseId == v?.Id).ToArray();
            var missing = (m.MaterialKind == "History" ? new[] { "HistoricalSource" } : new[] { "DirectQuotation", "Translation", "Transliteration", "WordMeaning" }).Where(kind => !available.Any(t => t.Kind == kind)).ToArray();
            var restricted = new List<string>();
            foreach (var t in available)
            {
                if (v != null && !await BoundAsync(v, t, ct)) restricted.Add(t.Kind + ":" + t.Locale + ":UnboundEvidence");
                var rights = await RightsAsync(scope, t, territory, cache, ct);
                if (!rights.Decision.Allowed) restricted.Add(t.Kind + ":" + t.Locale + ":" + rights.Decision.Reason);
            }
            var state = v == null ? "Missing" : restricted.Count > 0 ? "Restricted" : v.ReviewStatus == "Approved" ? "Approved" : v.ReviewStatus;
            entries.Add(new(l.Chapter, l.Verse, l.VariantKey, l.BaselineVerse, l.Note, state, missing, restricted));
        }
        await SaveAsync(ct); await tx.CommitAsync(ct);
        return new(await ViewAsync(m, ct), verses.Count, entries.Count(e => e.State == "Approved"), entries.Count(e => e.State == "Missing"), entries.Count(e => e.State == "Restricted"), entries);
    }
    public async Task<IReadOnlyList<CorpusVerseView>> ChapterAsync(CoreScope scope, Guid universe, Guid manifest, int chapter, string territory, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, 0, ct); await ManifestAsync(universe, manifest, ct); CorpusRules.Locator(chapter, 1);
        var result = new List<CorpusVerseView>(); var cache = new Dictionary<(Guid, string), (RightsDecision, CitationExport?)>();
        foreach (var v in await db.CorpusVerses.AsNoTracking().Where(v => v.ManifestId == manifest && v.Chapter == chapter).OrderBy(v => v.Verse).ToListAsync(ct))
        {
            var views = new List<CorpusTextView>();
            foreach (var t in await db.CorpusTexts.AsNoTracking().Where(t => t.VerseId == v.Id).ToListAsync(ct))
            {
                var rights = await RightsAsync(scope, t, territory, cache, ct);
                var bound = await BoundAsync(v, t, ct);
                views.Add(new(t.Kind, t.Locale, bound && rights.Decision.Allowed ? t.ExactText : "", t.Sha256, t.SourceLocator,
                    !bound ? "UnboundEvidence" : rights.Decision.Allowed ? v.ReviewStatus == "Pending" ? "PendingReview" : v.ReviewStatus : rights.Decision.Reason, rights.Citation, t.Id));
            }
            var annotations = new List<CorpusAnnotationView>();
            foreach (var a in await db.CorpusAnnotations.AsNoTracking().Where(a => a.VerseId == v.Id).ToListAsync(ct))
            {
                var rights = await new SourceRightsGuard(db).DecideAsync(scope, a.SnapshotId, new("Read", a.Locale, territory, "Text"), ct);
                db.RightsDecisions.Add(rights.Decision);
                annotations.Add(new(a.Id, a.Key, a.Kind, a.Locale, a.ReviewStatus == "Approved" && rights.Decision.Allowed ? a.Text : "",
                    rights.Decision.Allowed ? a.ReviewStatus : rights.Decision.Reason, Convert.ToBase64String(a.RowVersion), rights.Citation));
            }
            result.Add(new(v.Id, v.Chapter, v.Verse, v.VariantKey, v.ReviewStatus, Convert.ToBase64String(v.RowVersion), views, annotations));
        }
        await SaveAsync(ct); await tx.CommitAsync(ct); return result;
    }
    public Task<Guid> AnnotateAsync(CoreScope scope, Guid universe, Guid manifest, Guid verse, CorpusAnnotationInput r, CancellationToken ct) =>
        CommandAsync(scope, universe, r.OperationId, "corpus.annotation", new { manifest, verse, r }, async () =>
        {
            await ManifestAsync(universe, manifest, ct);
            if (!await db.CorpusVerses.AnyAsync(v => v.Id == verse && v.ManifestId == manifest, ct)) throw new CoreAccessException();
            CoreRules.Text(r.Key, 200, "Annotation key"); CorpusRules.Exact(r.Text);
            if (r.Kind is not ("Concept" or "Interpretation" or "GeneratedParaphrase")) throw new RuntimeValidationException("Annotation kind must be explicit.");
            var source = await SourceAsync(scope, universe, r.SnapshotId, r.Locale, ct);
            if (source.Text == null || !source.Text.Contains(r.Text, StringComparison.Ordinal)) throw new RuntimeValidationException("Annotation must be backed by its own retained source evidence.");
            if (await db.CorpusAnnotations.AnyAsync(a => a.VerseId == verse && a.Key == r.Key, ct)) throw new RuntimeConflictException("Annotation key already exists; preserve reviewed history.");
            var a = new CorpusAnnotation { Id = Guid.NewGuid(), VerseId = verse, Key = r.Key, Kind = r.Kind, Locale = r.Locale, Text = r.Text, SnapshotId = r.SnapshotId };
            db.CorpusAnnotations.Add(a); return a.Id;
        }, ct);
    public async Task ReviewAnnotationAsync(CoreScope scope, Guid universe, Guid manifest, Guid annotation, CorpusReview r, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, 2, ct); await ManifestAsync(universe, manifest, ct);
        var a = await db.CorpusAnnotations.SingleOrDefaultAsync(a => a.Id == annotation && db.CorpusVerses.Any(v => v.Id == a.VerseId && v.ManifestId == manifest), ct) ?? throw new CoreAccessException();
        Expected(a, r); if (a.ReviewStatus != "Pending") throw new RuntimeConflictException("Annotation review is frozen.");
        if (r.Status == "Approved")
        {
            var rights = await new SourceRightsGuard(db).DecideAsync(scope, a.SnapshotId, new("Read", a.Locale, "IN", "Text"), ct);
            if (!rights.Decision.Allowed) throw new RightsBlockedException(rights.Decision.Reason);
            db.RightsDecisions.Add(rights.Decision);
        }
        a.ReviewStatus = r.Status; a.Reviewer = scope.Actor; a.ReviewEvidence = r.EvidenceReference;
        Audit(scope, annotation, "corpus.annotation-reviewed"); await SaveAsync(ct); await tx.CommitAsync(ct);
    }
}

