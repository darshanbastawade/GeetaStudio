using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

internal static class EvidenceTestFixture
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static async Task<Guid> RubricAsync(IEvidenceEngine engine, CoreScope owner, Guid universe) =>
        await engine.RegisterRubricAsync(owner, universe, new(Guid.NewGuid(), "fixture-rubric", 1,
            "Exact approved corpus for quotations; attributed interpretations retain disagreement; history/news require two independent exact supporting sources and staff assessment.", 2), Ct);
    public static async Task<Guid> InterpretationAsync(IServiceProvider s, CoreScope owner, Guid universe, Guid snapshot, string statement)
    {
        var engine = s.GetRequiredService<IEvidenceEngine>();
        var text = Encoding.UTF8.GetString(SourceTestFixture.Bytes);
        var revision = await engine.SubmitAsync(owner, universe, new(Guid.NewGuid(), "test-" + Guid.NewGuid(), "", "Interpretation", "Interpretation",
            "en-IN", statement, "Original fixture critic", "Original fixture viewpoint", "Contested", await RubricAsync(engine, owner, universe),
            [new(snapshot, 0, text.Length, text, Hash(text), "sentence 1", "Supporting")]), Ct);
        await ApproveAsync(engine, owner, universe, revision, "Attributed", "Contested");
        return revision;
    }
    public static async Task ApproveAsync(IEvidenceEngine engine, CoreScope owner, Guid universe, Guid revision, string quality, string outcome)
    {
        var claim = (await engine.ListAsync(owner, universe, Ct)).Single(c => c.Revisions.Any(r => r.Id == revision));
        await engine.ReviewAsync(owner, universe, revision, new(claim.Revisions.Single(r => r.Id == revision).RowVersion, "Approved", quality, outcome, "fixture:explicit-test-review"), Ct);
    }
    public static async Task<(Guid Revision, string Text)> TranslationAsync(IServiceProvider s, CoreScope owner, Guid universe)
    {
        var sources = s.GetRequiredService<ISourceRegistry>(); var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var engine = s.GetRequiredService<IEvidenceEngine>();
        var folder = Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M04");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var rows = JsonSerializer.Deserialize<CorpusRow[]>(await File.ReadAllTextAsync(Path.Combine(folder, "sample.json")), json)!;
        var snapshots = new Dictionary<string, Guid>();
        foreach (var field in new[] { "sanskrit", "translation" })
        {
            var locale = field == "sanskrit" ? "sa-IN" : "en-IN";
            var work = await sources.RegisterAsync(owner, universe, new(Guid.NewGuid(), "m05-corpus-" + Guid.NewGuid(), "M04 licensed sample", field == "sanskrit" ? "Canonical" : "Translation"), Ct);
            var edition = await sources.EditionAsync(owner, universe, work, new(Guid.NewGuid(), "Wikisource revision 9204505", "Traditional Sanskrit text", field == "translation" ? "Annie Besant" : null, "G. A. Natesan & Co.; Wikisource", locale, "Discourse 1, verses 1-25"), Ct);
            var permission = await sources.PermissionAsync(owner, universe, edition, new(Guid.NewGuid(), "evidence:M04-stored-CC-BY-SA-4.0-license", [locale], ["IN"], ["Text", "Json"], false, false, false, false, true, "Besant 1922; Wikisource, CC BY-SA 4.0"), Ct);
            var detail = await sources.InspectAsync(owner, universe, work, Ct);
            await sources.ReviewPermissionAsync(owner, universe, permission, new(detail.Permissions.Single().RowVersion, "Approved"), Ct);
            var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, field + "-sample.txt"));
            var snapshot = await sources.ImportAsync(owner, universe, edition, new(Guid.NewGuid(), field + ".txt", bytes, Convert.ToHexString(SHA256.HashData(bytes)), "evidence:M04-source-file", true), Ct);
            detail = await sources.InspectAsync(owner, universe, work, Ct);
            await sources.ReviewSnapshotAsync(owner, universe, snapshot, new(detail.Snapshots.Single().RowVersion, "Approved"), Ct);
            snapshots[field] = snapshot;
        }
        var manifest = JsonSerializer.Deserialize<RegisterCorpus>(await File.ReadAllTextAsync(Path.Combine(folder, "manifest.json")), json)!;
        var inputs = rows.Select(r => new CorpusVerseInput(r.Chapter, r.Verse, r.VariantKey,
            [new("DirectQuotation", "sa-IN", r.Sanskrit, r.SanskritHash, snapshots["sanskrit"], r.SourceLocator),
             new("Translation", "en-IN", r.Translation, r.TranslationHash, snapshots["translation"], r.SourceLocator)])).ToArray();
        var id = await corpus.RegisterAsync(owner, universe, manifest with
        {
            OperationId = Guid.NewGuid(),
            Key = "m05-" + Guid.NewGuid(),
            Bindings = inputs.SelectMany(v => v.Texts.Select(t => new CorpusSourceBindingInput(v.Chapter, v.Verse, v.VariantKey, t.Kind, t.Locale, t.SnapshotId, v.Chapter, v.Verse, t.Sha256, t.SourceLocator))).ToArray()
        }, Ct);
        await corpus.ImportAsync(owner, universe, id, new(Guid.NewGuid(), inputs), Ct);
        var verse = (await corpus.ChapterAsync(owner, universe, id, 1, "IN", Ct)).Single(v => v.Verse == 1);
        await corpus.ReviewAsync(owner, universe, id, verse.Id, new(verse.RowVersion, "Approved", "evidence:M04-automated-source-label-hash-check"), Ct);
        verse = (await corpus.ChapterAsync(owner, universe, id, 1, "IN", Ct)).Single(v => v.Verse == 1);
        var text = verse.Texts.Single(t => t.Kind == "Translation");
        var file = await File.ReadAllTextAsync(Path.Combine(folder, "translation-sample.txt"));
        var revision = await engine.SubmitAsync(owner, universe, new(Guid.NewGuid(), "translation-" + Guid.NewGuid(), "", "Translation", "Translation", "en-IN",
            text.ExactText, "Annie Besant", "Besant 1922", "SourceExact", await RubricAsync(engine, owner, universe),
            [new(snapshots["translation"], file.IndexOf(text.ExactText, StringComparison.Ordinal), text.ExactText.Length, text.ExactText, text.Sha256, text.SourceLocator, "Supporting", text.Id)]), Ct);
        await ApproveAsync(engine, owner, universe, revision, "DirectSource", "Supported");
        return (revision, text.ExactText);
    }
    private sealed record CorpusRow(int Chapter, int Verse, string VariantKey, string Sanskrit, string Translation, string SanskritHash, string TranslationHash, string SourceLocator);
}
