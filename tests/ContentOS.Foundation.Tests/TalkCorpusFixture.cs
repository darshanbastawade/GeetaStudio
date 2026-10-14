using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class TalkCorpusFixture
{
    private sealed record CorpusRow(int Chapter, int Verse, string VariantKey, string Sanskrit, string Translation, string SanskritHash, string TranslationHash, string SourceLocator);
    internal static async Task<Guid> FirstVerse(IServiceProvider s, CustomerTestFixture.Fixture c)
    {
        var folder = Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M04"); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var row = JsonSerializer.Deserialize<CorpusRow[]>(await File.ReadAllTextAsync(Path.Combine(folder, "sample.json")), json)![0]; var registry = s.GetRequiredService<ISourceRegistry>(); var texts = new List<CorpusTextInput>();
        foreach (var field in new[] { "sanskrit", "translation" })
        {
            var locale = field == "sanskrit" ? "sa-IN" : "en-IN"; var work = await registry.RegisterAsync(c.Admin, c.Universe, new(Guid.NewGuid(), "talk-source-" + Guid.NewGuid(), "M04 licensed one-verse TALK fixture", field == "sanskrit" ? "Canonical" : "Translation"), default);
            var edition = await registry.EditionAsync(c.Admin, c.Universe, work, new(Guid.NewGuid(), "Wikisource revision 9204505", "Traditional Sanskrit text", field == "translation" ? "Annie Besant" : null, "G. A. Natesan & Co.; Wikisource", locale, "https://en.wikisource.org/w/index.php?title=Bhagavad-Gita_(Besant_4th)/Discourse_1&oldid=9204505"), default);
            var permission = await registry.PermissionAsync(c.Admin, c.Universe, edition, new(Guid.NewGuid(), "fixture:M21-controlled-CC-BY-SA-4.0", [locale], ["IN"], ["Text", "Json"], false, true, false, true, true, "Besant 1922; Wikisource, CC BY-SA 4.0"), default); var detail = await registry.InspectAsync(c.Admin, c.Universe, work, default); await registry.ReviewPermissionAsync(c.Admin, c.Universe, permission, new(detail.Permissions.Single().RowVersion, "Approved"), default);
            var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, field + "-sample.txt")); var snapshot = await registry.ImportAsync(c.Admin, c.Universe, edition, new(Guid.NewGuid(), field + ".txt", bytes, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), "evidence:M04-preserved-source-file", true), default); detail = await registry.InspectAsync(c.Admin, c.Universe, work, default); await registry.ReviewSnapshotAsync(c.Admin, c.Universe, snapshot, new(detail.Snapshots.Single().RowVersion, "Approved"), default);
            texts.Add(new(field == "sanskrit" ? "DirectQuotation" : "Translation", locale, field == "sanskrit" ? row.Sanskrit : row.Translation, field == "sanskrit" ? row.SanskritHash : row.TranslationHash, snapshot, row.SourceLocator));
        }
        var corpus = s.GetRequiredService<ICanonicalCorpus>(); var manifest = await corpus.RegisterAsync(c.Admin, c.Universe, new(Guid.NewGuid(), "talk-corpus-" + Guid.NewGuid(), 1, "Besant 1922 one-verse controlled fixture; not complete corpus", 1, [new(1, 1, "", 1, "M21 focused positive/no-match fixture")], texts.Select(t => new CorpusSourceBindingInput(1, 1, "", t.Kind, t.Locale, t.SnapshotId, 1, 1, t.Sha256, t.SourceLocator)).ToArray()), default);
        await corpus.ImportAsync(c.Admin, c.Universe, manifest, new(Guid.NewGuid(), [new(1, 1, "", texts)]), default); var verse = (await corpus.ChapterAsync(c.Admin, c.Universe, manifest, 1, "IN", default)).Single(); await corpus.ReviewAsync(c.Admin, c.Universe, manifest, verse.Id, new(verse.RowVersion, "Approved", "fixture:M21-license/hash-check-not-human-review"), default);
        var db = s.GetRequiredService<CoreDbContext>(); return await db.CorpusTexts.AsNoTracking().Where(t => t.VerseId == verse.Id && t.Locale == "en-IN").Select(t => t.Id).SingleAsync();
    }
}


