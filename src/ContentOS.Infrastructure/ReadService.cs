using System.Data;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed class ReadService(CoreDbContext db, IEditorialWorkbench editorial, ILessonEngine lessons, IConfiguration configuration) : IReadWorkbench, IPublicRead
{
    private static string Json<T>(T value) => EditorialBinding.Json(value);
    private async Task Access(CoreScope s, Guid u, bool write, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct) ||
            !await db.Members.AnyAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor && (!write || x.Permission == "Administrator"), ct)) throw new CoreAccessException();
    }
    private async Task<ReadCollection> Collection(CoreScope s, Guid id, bool write, CancellationToken ct)
    {
        var c = await db.ReadCollections.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        await Access(s, c.UniverseId, write, ct); return c;
    }
    private async Task<Guid> Command<T>(CoreScope s, Guid u, Guid op, string kind, T r, Func<Task<Guid>> action, CancellationToken ct)
    {
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable operation required.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Access(s, u, true, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Read:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'READ lock unavailable',1;", ct);
        var hash = EditorialBinding.Hash(new { u, r }); var prior = await db.ReadOperations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (prior != null) { if (prior.Actor != s.Actor || prior.Kind != kind || prior.Fingerprint != hash) throw new RuntimeConflictException("Read replay differs."); return prior.ResultId; }
        var id = await action(); db.ReadOperations.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = op, Kind = kind, Actor = s.Actor, Fingerprint = hash, ResultId = id });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = "read." + kind });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return id;
    }
    public Task<Guid> RegisterAsync(CoreScope s, Guid u, RegisterReadCollection r, CancellationToken ct) => Command(s, u, r.OperationId, "register", r, async () =>
    {
        ReadRules.Collection(r); if (!await db.CorpusManifests.AnyAsync(x => x.Id == r.ManifestId && x.TenantId == s.TenantId && x.UniverseId == u, ct)) throw new CoreAccessException();
        var previous = await db.ReadCollections.Where(x => x.Key == r.Key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (previous != null && (previous.TenantId != s.TenantId || previous.UniverseId != u)) throw new CoreAccessException();
        if (r.Version != (previous?.Version ?? 0) + 1) throw new RuntimeConflictException("Append the next collection version.");
        var c = new ReadCollection { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ManifestId = r.ManifestId, Key = r.Key, Version = r.Version, Title = r.Title, Mode = r.Mode, Rationale = r.Rationale, Actor = s.Actor };
        db.ReadCollections.Add(c); return c.Id;
    }, ct);
    public Task<Guid> AddAsync(CoreScope s, Guid u, AddReadPiece r, CancellationToken ct) => Command(s, u, r.OperationId, "bind", r, async () =>
    {
        var c = await Collection(s, r.CollectionId, true, ct); if (c.UniverseId != u) throw new CoreAccessException();
        if (c.State != "Draft" || await db.ReadPieces.CountAsync(x => x.CollectionId == c.Id, ct) >= 50) throw new RuntimeConflictException("Draft collection capacity50; version before editing active bindings.");
        var verse = await db.CorpusVerses.SingleOrDefaultAsync(x => x.Id == r.CorpusVerseId && x.ManifestId == c.ManifestId, ct) ?? throw new CoreAccessException();
        var p = await editorial.InspectAsync(s, u, r.EditorialItemId, ct); var lesson = await lessons.InspectAsync(s, u, r.LessonVariantId, ct);
        if (p.SourceOutputWithheld || lesson.OutputWithheld || lesson.Sections == null || lesson.ReviewStatus != "Approved" ||
            !p.Approvals.Any(x => x.Current) || p.Format is not ("Article" or "Translation")) throw new EvidenceBlockedException("CurrentReviewedReadInputsRequired");
        var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == p.ContentId, ct);
        var anchors = await (from span in db.ClaimSpans.AsNoTracking()
                             join text in db.CorpusTexts.AsNoTracking() on span.CorpusTextId equals text.Id
                             join originalVerse in db.CorpusVerses.AsNoTracking() on text.VerseId equals originalVerse.Id
                             where span.ClaimRevisionId == lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId
                                 && originalVerse.Chapter == verse.Chapter && originalVerse.Verse == verse.Verse && originalVerse.VariantKey == verse.VariantKey
                             select new { text.SnapshotId, text.Sha256, text.Kind, text.Locale, text.SourceLocator }).ToListAsync(ct);
        var targets = await db.CorpusTexts.AsNoTracking().Where(x => x.VerseId == verse.Id).ToListAsync(ct);
        if (head.AudienceId != lesson.Snapshot.Audience.Rules.Audience!.AudienceId || head.LocaleId != lesson.Snapshot.Locale.Rules.Locale!.Locale ||
            !anchors.Any(anchor => targets.Any(target => target.SnapshotId == anchor.SnapshotId && target.Sha256 == anchor.Sha256 && target.Kind == anchor.Kind && target.Locale == anchor.Locale && target.SourceLocator == anchor.SourceLocator)))
            throw new EvidenceBlockedException("LessonAndVerseBindingMismatch");
        var sources = lesson.Snapshot.Bundle.SourceIds;
        if (sources.Any(x => !p.SourceSnapshotIds.Contains(x))) throw new EvidenceBlockedException("EditorialSourceLineageRequired");
        CoreRules.Text(r.Journey, 200, "Journey"); if (r.Position is < 1 or > 1000) throw new RuntimeValidationException("Bounded journey position required.");
        if (await db.ReadPieces.AnyAsync(x => x.CollectionId == c.Id && x.EditorialItemId == r.EditorialItemId, ct)) throw new RuntimeConflictException("Piece already bound.");
        var row = new ReadPiece { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, CollectionId = c.Id, CorpusVerseId = verse.Id, EditorialItemId = p.Id, RevisionId = p.RevisionId, LessonVariantId = r.LessonVariantId, Journey = r.Journey, Position = r.Position };
        db.ReadPieces.Add(row); return row.Id;
    }, ct);
    private async Task<ReadPieceView?> Piece(CoreScope s, ReadCollection c, ReadPiece row, bool release, CancellationToken ct)
    {
        try
        {
            var p = await editorial.InspectAsync(s, c.UniverseId, row.EditorialItemId, ct);
            if (p.RevisionId != row.RevisionId || p.SourceOutputWithheld || p.Current == null || !p.Approvals.Any(x => x.Current && (!release || x.Mode == "Manual")) || p.State is not ("Approved" or "Scheduled" or "Published") ||
                p.Blocks.Any(x => x != "PublicReleaseNotApproved" && x != "ExternalPublishingAdapterNotImplemented")) return null;
            if (release && (p.Evaluations.Length == 0 || p.Evaluations.Any(x => x.Qualification.Status != "TestedConditionsAccepted"))) return null;
            var lesson = release ? await lessons.ExportAsync(s, c.UniverseId, row.LessonVariantId, ct) : await lessons.InspectAsync(s, c.UniverseId, row.LessonVariantId, ct);
            if (lesson.OutputWithheld || lesson.ReviewStatus != "Approved" || lesson.Sections == null) return null;
            if (release && !ReadRules.CoversReaderSections(p.Current.Body, lesson.Sections)) return null;
            var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == p.ContentId, ct);
            if (release && head.EditorialState != "Approved") return null;
            if (head.AudienceId != lesson.Snapshot.Audience.Rules.Audience!.AudienceId || head.LocaleId != lesson.Snapshot.Locale.Rules.Locale!.Locale) return null;
            await new SourceRightsGuard(db).CheckAsync(s, row.RevisionId, new(release ? "Publish" : "Read", head.LocaleId, "IN", "Text"), ct);
            var verse = await db.CorpusVerses.AsNoTracking().SingleAsync(x => x.Id == row.CorpusVerseId && x.ManifestId == c.ManifestId, ct);
            if (verse.ReviewStatus != "Approved" || release && verse.ReviewEvidence?.StartsWith("manual:") != true) return null;
            var texts = new List<ReadSource>();
            foreach (var text in await db.CorpusTexts.AsNoTracking().Where(x => x.VerseId == verse.Id && (x.Locale == head.LocaleId || x.Locale == "sa-IN" || x.Kind == "Transliteration")).ToListAsync(ct))
            {
                if (!await db.CorpusBindings.AnyAsync(x => x.ManifestId == c.ManifestId && x.Chapter == verse.Chapter && x.Verse == verse.Verse && x.VariantKey == verse.VariantKey && x.Kind == text.Kind && x.Locale == text.Locale && x.SnapshotId == text.SnapshotId && x.Sha256 == text.Sha256 && x.SourceLocator == text.SourceLocator, ct)) continue;
                var rights = await new SourceRightsGuard(db).DecideAsync(s, text.SnapshotId, new(release ? "Publish" : "Read", text.Locale, "IN", "Text"), ct);
                if (rights.Decision.Allowed && rights.Citation != null) texts.Add(new(text.Kind, text.Locale, text.ExactText, text.SourceLocator, rights.Citation.Citation + "; " + rights.Citation.RequiredAttribution));
            }
            if (!texts.Any(x => x.Kind == "DirectQuotation" && x.Locale == "sa-IN")) return null;
            foreach (var source in lesson.Snapshot.Bundle.SourceIds)
                if (!(await new SourceRightsGuard(db).DecideAsync(s, source, new(release ? "Publish" : "Read", head.LocaleId, "IN", "Text"), ct)).Decision.Allowed) return null;
            return new(row.Id, verse.Chapter, verse.Verse, head.LocaleId, head.AudienceId, lesson.Snapshot.Audience.Rules.Key, p.Current.Title, p.Current.Body,
                lesson.Sections.PracticalAction, lesson.Sections.Reflection, texts.ToArray(), lesson.Sections.Concepts, row.Journey, row.Position);
        }
        catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException or RuntimeValidationException or RuntimeConflictException or CoreAccessException) { return null; }
    }
    private async Task<(ReadBook Book, string Fingerprint, string[] ReleaseBlocks)> Build(CoreScope s, ReadCollection c, bool release, CancellationToken ct)
    {
        await Access(s, c.UniverseId, false, ct);
        var rows = await db.ReadPieces.AsNoTracking().Where(x => x.CollectionId == c.Id).OrderBy(x => x.Position).ThenBy(x => x.Id).Take(51).ToListAsync(ct);
        if (rows.Count > 50) throw new RuntimeValidationException("READ collection capacity50 exceeded.");
        var pieces = new List<ReadPieceView>(); var dependencies = new List<object>();
        foreach (var row in rows)
        {
            var view = await Piece(s, c, row, release, ct); if (view != null) pieces.Add(view);
            var packet = await editorial.InspectAsync(s, c.UniverseId, row.EditorialItemId, ct);
            var verseVersion = await db.CorpusVerses.AsNoTracking().Where(x => x.Id == row.CorpusVerseId).Select(x => x.RowVersion).SingleAsync(ct);
            var lessonVersion = await db.LessonVariants.AsNoTracking().Where(x => x.Id == row.LessonVariantId).Select(x => x.RowVersion).SingleAsync(ct);
            dependencies.Add(new { row.Id, packet.Fingerprint, row.LessonVariantId, row.CorpusVerseId, VerseVersion = verseVersion, LessonVersion = lessonVersion });
        }
        var manifest = await db.CorpusManifests.AsNoTracking().SingleAsync(x => x.Id == c.ManifestId, ct);
        var fingerprint = EditorialBinding.Hash(new { Policy = "read.v1", c.Id, c.ManifestId, c.Version, Dependencies = dependencies });
        var evidence = await db.ReadEvidenceEntries.AsNoTracking().Where(x => x.CollectionId == c.Id && x.Mode == "Manual" && x.Fingerprint == fingerprint && x.ExpiresUtc > DateTimeOffset.UtcNow).Select(x => x.Kind).ToArrayAsync(ct);
        var blocks = ReadRules.Gates.Except(evidence).Select(x => x + "EvidenceRequired").ToList();
        if (c.Mode != "Public") blocks.Add("FixtureCollectionCannotRollOut");
        if (!(bool.TryParse(configuration["Read:PublicEnabled"], out var enabled) && enabled)) blocks.Add("PublicRolloutDisabled");
        if (rows.Count == 0 || pieces.Count != rows.Count) blocks.Add("EveryPieceRequiresCurrentHumanQualifiedInputs");
        var book = new ReadBook(new(c.Id, c.Key, c.Version, c.Title, pieces.Count, c.State), pieces.ToArray(), manifest.BaselineTarget,
            await db.CorpusVerses.CountAsync(x => x.ManifestId == c.ManifestId, ct), pieces.Select(x => (x.Chapter, x.Verse)).Distinct().Count(), c.Mode, release ? blocks.ToArray() : ["Private fixture preview; not public release qualification."], await db.Universes.Where(x => x.Id == c.UniverseId).Select(x => x.Name).SingleAsync(ct));
        return (book, fingerprint, blocks.ToArray());
    }
    public async Task<ReadStaffView> InspectAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        var c = await Collection(s, id, false, ct); if (c.UniverseId != u) throw new CoreAccessException(); var preview = await Build(s, c, false, ct); var release = await Build(s, c, true, ct);
        return new(preview.Book, Convert.ToBase64String(c.RowVersion), release.Fingerprint, release.ReleaseBlocks, await db.ReadEvidenceEntries.Where(x => x.CollectionId == id).Select(x => x.Kind + ":" + x.Mode).ToArrayAsync(ct));
    }
    public async Task EvidenceAsync(CoreScope s, Guid u, Guid id, ReadGateEvidence r, CancellationToken ct)
    {
        var c = await Collection(s, id, true, ct); if (c.UniverseId != u) throw new CoreAccessException();
        await Command(s, c.UniverseId, r.OperationId, "evidence", new { id, r }, async () =>
        {
            c = await Collection(s, id, true, ct); ReadRules.Evidence(r); var build = await Build(s, c, true, ct);
            db.ReadEvidenceEntries.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = c.UniverseId, CollectionId = id, Kind = r.Kind, Mode = r.Mode, Reference = r.Reference, Notes = r.Notes, Fingerprint = build.Fingerprint, Actor = s.Actor, ExpiresUtc = r.ExpiresUtc }); return id;
        }, ct);
    }
    public async Task ControlAsync(CoreScope s, Guid u, Guid id, ControlReadCollection r, CancellationToken ct)
    {
        var c = await Collection(s, id, true, ct); if (c.UniverseId != u) throw new CoreAccessException();
        await Command(s, c.UniverseId, r.OperationId, "control." + r.Action, new { id, r }, async () =>
        {
            c = await Collection(s, id, true, ct); if (Convert.ToBase64String(c.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
            if (r.Action == "Disable") c.State = "Disabled";
            else if (r.Action is "Activate" or "Rollback")
            {
                var build = await Build(s, c, true, ct); if (build.ReleaseBlocks.Length > 0) throw new EvidenceBlockedException(string.Join(", ", build.ReleaseBlocks));
                foreach (var prior in await db.ReadCollections.Where(x => x.Key == c.Key && x.Id != id && x.State == "Active").ToListAsync(ct)) prior.State = "Disabled";
                await db.SaveChangesAsync(ct); c.State = "Active";
            }
            else throw new RuntimeValidationException("Allowed actions:Activate,Rollback,Disable.");
            return id;
        }, ct);
    }
    public async Task<ReadBook?> PreviewAsync(string actor, Guid id, CancellationToken ct)
    {
        var c = await db.ReadCollections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (c == null || c.State == "Disabled") return null;
        var s = new CoreScope(c.TenantId, actor); await Access(s, c.UniverseId, false, ct);
        return (await Build(s, c, false, ct)).Book;
    }
    public async Task<ReadBook?> GetAsync(string key, CancellationToken ct)
    {
        if (!(bool.TryParse(configuration["Read:PublicEnabled"], out var enabled) && enabled)) return null;
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await db.ReadCollections.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key && x.State == "Active" && x.Mode == "Public", ct);
        if (c == null) return null;
        try { var build = await Build(new(c.TenantId, c.Actor), c, true, ct); await tx.CommitAsync(ct); return build.ReleaseBlocks.Length == 0 ? build.Book : null; }
        catch (CoreAccessException) { return null; }
    }
    public async Task<ReadCollectionSummary[]> DirectoryAsync(CancellationToken ct)
    {
        if (!(bool.TryParse(configuration["Read:PublicEnabled"], out var enabled) && enabled)) return [];
        var keys = await db.ReadCollections.AsNoTracking().Where(x => x.State == "Active" && x.Mode == "Public").Select(x => x.Key).Take(21).ToArrayAsync(ct);
        if (keys.Length > 20) throw new RuntimeValidationException("Public directory capacity20 exceeded.");
        var result = new List<ReadCollectionSummary>(); foreach (var key in keys) { var book = await GetAsync(key, ct); if (book != null) result.Add(book.Collection); }
        return result.ToArray();
    }
    public async Task EventAsync(string key, ReadEngagement r, CancellationToken ct)
    {
        ReadRules.Engagement(r); var book = await GetAsync(key, ct); if (book == null || !book.Pieces.Any(x => x.Id == r.PieceId)) throw new CoreAccessException();
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Read.Events:" + book.Collection.Id},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000; IF @r<0 THROW 51000,'Event admission busy',1;", ct);
        var current = await db.ReadCollections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == book.Collection.Id && x.State == "Active" && x.Mode == "Public", ct);
        if (current == null || !(bool.TryParse(configuration["Read:PublicEnabled"], out var enabled) && enabled)) throw new CoreAccessException();
        var eligible = await Build(new(current.TenantId, current.Actor), current, true, ct);
        if (eligible.ReleaseBlocks.Length != 0 || !eligible.Book.Pieces.Any(x => x.Id == r.PieceId)) throw new CoreAccessException();
        var day = DateTime.UtcNow.Date; var old = await db.ReadEventCounts.Where(x => x.Day < day.AddDays(-6)).ToListAsync(ct); db.ReadEventCounts.RemoveRange(old);
        var row = await db.ReadEventCounts.SingleOrDefaultAsync(x => x.CollectionId == book.Collection.Id && x.PieceId == r.PieceId && x.Day == day && x.Version == r.Version && x.Name == r.Name, ct);
        if (row == null) { row = new() { Id = Guid.NewGuid(), CollectionId = book.Collection.Id, PieceId = r.PieceId, Day = day, Version = r.Version, Name = r.Name }; db.ReadEventCounts.Add(row); }
        row.Count = Math.Min(10000, row.Count + 1); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
public static class ReadServices
{
    public static IServiceCollection AddReadWorkbench(this IServiceCollection s) { s.AddScoped<ReadService>(); s.AddScoped<IReadWorkbench>(p => p.GetRequiredService<ReadService>()); s.AddScoped<IPublicRead>(p => p.GetRequiredService<ReadService>()); return s; }
}
