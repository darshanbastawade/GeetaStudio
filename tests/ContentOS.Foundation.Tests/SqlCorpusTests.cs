using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlCorpusTests : IAsyncLifetime
{
    private const string Connection = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
    private const string Actor = "m04-test-owner";
    private static readonly Guid Tenant = CoreSeed.StableId("tenant:local");
    private static readonly Guid Universe = CoreSeed.StableId("universe:geeta");
    private static CoreScope Owner => new(Tenant, Actor);
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static string Fixture => Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M04");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static ServiceProvider Services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ContentOS"] = Connection }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddContentCore(config); return services.BuildServiceProvider();
    }
    public async Task InitializeAsync()
    {
        using var root = Services(); using var scope = root.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await CoreSeed.SeedAsync(db, Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M02-universes.json"), Actor);
        if (!await db.Locales.AnyAsync(l => l.Id == "sa-IN")) db.Locales.Add(new ContentLocale { Id = "sa-IN", Name = "Sanskrit (India)" });
        foreach (var pair in new[] { ("m04-reader", "Reader"), ("integration-test-Administrator", "Administrator"), ("integration-test-Reader", "Reader") })
            if (!await db.Members.AnyAsync(m => m.TenantId == Tenant && m.Actor == pair.Item1)) db.Members.Add(new TenantMember { TenantId = Tenant, Actor = pair.Item1, Permission = pair.Item2 });
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private sealed record Row(int Chapter, int Verse, string VariantKey, string Sanskrit, string Translation, string SanskritHash, string TranslationHash, string SourceLocator);
    private static async Task<RegisterCorpus> ManifestAsync() =>
        (JsonSerializer.Deserialize<RegisterCorpus>(await File.ReadAllTextAsync(Path.Combine(Fixture, "manifest.json")), Json)!) with { OperationId = Guid.NewGuid(), Key = "test-" + Guid.NewGuid().ToString("N") };
    private static async Task<(Guid Work, Guid Snapshot)> SourceAsync(ISourceRegistry sources, string field, string locale)
    {
        var work = await sources.RegisterAsync(Owner, Universe, new(Guid.NewGuid(), "m04-test-" + Guid.NewGuid(), "Selected licensed M04 test source", field == "sanskrit" ? "Canonical" : "Translation"), Ct);
        var edition = await sources.EditionAsync(Owner, Universe, work, new(Guid.NewGuid(), "Wikisource revision 9204505", "Traditional Sanskrit text", field == "translation" ? "Annie Besant" : null, "G. A. Natesan & Co.; Wikisource", locale, "Discourse 1, verses 1-25"), Ct);
        var permission = await sources.PermissionAsync(Owner, Universe, edition, new(Guid.NewGuid(), "evidence:M04-stored-CC-BY-SA-4.0-license", [locale], ["IN"], ["Text"], false, false, false, false, true, "Besant 1922; Wikisource, CC BY-SA 4.0"), Ct);
        var detail = await sources.InspectAsync(Owner, Universe, work, Ct);
        await sources.ReviewPermissionAsync(Owner, Universe, permission, new(detail.Permissions.Single().RowVersion, "Approved"), Ct);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(Fixture, field + "-sample.txt"));
        var snapshot = await sources.ImportAsync(Owner, Universe, edition, new(Guid.NewGuid(), field + ".txt", bytes, Convert.ToHexString(SHA256.HashData(bytes)), "evidence:M04-source-file", true), Ct);
        detail = await sources.InspectAsync(Owner, Universe, work, Ct);
        await sources.ReviewSnapshotAsync(Owner, Universe, snapshot, new(detail.Snapshots.Single().RowVersion, "Approved"), Ct);
        return (work, snapshot);
    }
    private static async Task<(Guid Manifest, ImportCorpus Import, Guid Work)> SetupAsync(IServiceProvider s)
    {
        var corpus = s.GetRequiredService<ICanonicalCorpus>(); var sources = s.GetRequiredService<ISourceRegistry>();
        var sa = await SourceAsync(sources, "sanskrit", "sa-IN"); var en = await SourceAsync(sources, "translation", "en-IN");

        var rows = JsonSerializer.Deserialize<Row[]>(await File.ReadAllTextAsync(Path.Combine(Fixture, "sample.json")), Json)!;
        var request = new ImportCorpus(Guid.NewGuid(), rows.Select(r => new CorpusVerseInput(r.Chapter, r.Verse, r.VariantKey,
            [new("DirectQuotation", "sa-IN", r.Sanskrit, r.SanskritHash, sa.Snapshot, r.SourceLocator),
             new("Translation", "en-IN", r.Translation, r.TranslationHash, en.Snapshot, r.SourceLocator)])).ToArray());
        var manifest = await corpus.RegisterAsync(Owner, Universe, (await ManifestAsync()) with
        {
            Bindings = request.Verses.SelectMany(v => v.Texts.Select(t => new CorpusSourceBindingInput(v.Chapter, v.Verse, v.VariantKey, t.Kind, t.Locale, t.SnapshotId, v.Chapter, v.Verse, t.Sha256, t.SourceLocator))).ToArray()
        }, Ct);
        return (manifest, request, en.Work);
    }
    [SqlFact]
    public async Task Real_sample_reconciles_every_baseline_variant_gap_and_preserves_exact_reader_text()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var setup = await SetupAsync(s);
        Assert.Equal(25, (await corpus.ImportAsync(Owner, Universe, setup.Manifest, setup.Import, Ct)).Added);
        var before = await corpus.ReportAsync(Owner, Universe, setup.Manifest, "IN", Ct);
        Assert.Equal(700, before.Manifest.BaselineTarget); Assert.Equal(701, before.Entries.Count);
        Assert.Equal(25, before.Imported); Assert.Equal(676, before.Missing); Assert.Equal(0, before.ApprovedReadable);
        Assert.Single(before.Entries, e => e.VariantKey == "opening-arjuna-question" && e.Chapter == 13 && e.Verse == 1 && e.State == "Missing");
        Assert.Equal(1, before.Entries.Single(e => e.Chapter == 13 && e.Verse == 2).BaselineVerse);
        var rows = await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "IN", Ct);
        foreach (var v in rows) await corpus.ReviewAsync(Owner, Universe, setup.Manifest, v.Id, new(v.RowVersion, "Approved", "evidence:M04-automated-source-label-hash-check"), Ct);
        var approved = await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "IN", Ct);
        foreach (var v in approved)
        {
            Assert.Equal("Approved", v.ReviewStatus);
            var input = setup.Import.Verses.Single(r => r.Verse == v.Verse);
            foreach (var text in v.Texts)
            {
                var exact = input.Texts.Single(t => t.Kind == text.Kind);
                Assert.Equal(exact.ExactText, text.ExactText); Assert.Equal(exact.Sha256, text.Sha256);
                Assert.Contains("Discourse 1", text.SourceLocator); Assert.NotNull(text.Citation);
                Assert.Contains("CC BY-SA", text.Citation!.RequiredAttribution);
            }
        }
        var report = await corpus.ReportAsync(Owner, Universe, setup.Manifest, "IN", Ct);
        Assert.Equal(25, report.ApprovedReadable);
        Assert.All(report.Entries.Where(e => e.State == "Approved"), e => { Assert.Contains("Transliteration", e.MissingComponents); Assert.Contains("WordMeaning", e.MissingComponents); });
    }
    [SqlFact]
    public async Task Imports_replay_without_overwrite_and_fail_atomic_on_duplicate_or_invalid_boundaries_hash_unicode()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var setup = await SetupAsync(s); var first = setup.Import.Verses[0];
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [first, first]), Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [first, first with { Chapter = 2, Verse = 100 }]), Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [first with { Texts = [first.Texts[0] with { Sha256 = "bad" }] }]), Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [first with { Texts = [first.Texts[0] with { ExactText = "bad\uD800" }] }]), Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest,
            new(Guid.NewGuid(), [first with { Verse = 2 }]), Ct));
        Assert.Equal(0, (await corpus.ReportAsync(Owner, Universe, setup.Manifest, "IN", Ct)).Imported);
        await corpus.ImportAsync(Owner, Universe, setup.Manifest, setup.Import, Ct);
        Assert.Equal(0, (await corpus.ImportAsync(Owner, Universe, setup.Manifest, setup.Import, Ct)).Added);
        Assert.Equal(25, (await corpus.ImportAsync(Owner, Universe, setup.Manifest, setup.Import with { OperationId = Guid.NewGuid() }, Ct)).Unchanged);
        var changed = first with { Texts = [first.Texts[0] with { SourceLocator = "Different locator" }, first.Texts[1]] };
        await Assert.ThrowsAsync<RuntimeConflictException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [changed]), Ct));
        await Assert.ThrowsAsync<RuntimeConflictException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, setup.Import with { Verses = [first] }, Ct));
    }
    [SqlFact]
    public async Task Missing_translation_is_explicit_and_revocation_blocks_reader_and_lists_corpus_dependency()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var setup = await SetupAsync(s); var first = setup.Import.Verses[0];
        await corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [first with { Texts = [first.Texts[0]] }, setup.Import.Verses[1]]), Ct);
        var report = await corpus.ReportAsync(Owner, Universe, setup.Manifest, "IN", Ct);
        Assert.Contains("Translation", report.Entries.Single(e => e.Chapter == 1 && e.Verse == 1).MissingComponents);
        var sources = s.GetRequiredService<ISourceRegistry>(); var detail = await sources.InspectAsync(Owner, Universe, setup.Work, Ct);
        var impact = await sources.RevokeAsync(Owner, Universe, setup.Work, new(Guid.NewGuid(), null, "M04 test takedown", detail.Work.RowVersion), Ct);
        Assert.Contains(impact.Corpus!, d => d.ManifestId == setup.Manifest && d.Kind == "Translation");
        var chapter = await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "IN", Ct);
        var translation = chapter.Single(v => v.Verse == 2).Texts.Single(t => t.Kind == "Translation");
        Assert.Equal("", translation.ExactText); Assert.Equal("Revoked", translation.Availability);
        Assert.NotEmpty(chapter.Single(v => v.Verse == 2).Texts.Single(t => t.Kind == "DirectQuotation").ExactText);
        Assert.Equal(1, (await corpus.ReportAsync(Owner, Universe, setup.Manifest, "IN", Ct)).Restricted);
        Assert.All(await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "US", Ct), v => Assert.All(v.Texts, t => Assert.Equal("", t.ExactText)));
    }
    [SqlFact]
    public async Task Corpus_scope_rejects_reader_mutation_forged_tenant_and_cross_universe_snapshot()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var setup = await SetupAsync(s);
        await Assert.ThrowsAsync<CoreAccessException>(() => corpus.ReportAsync(new(Tenant, "unknown"), Universe, setup.Manifest, "IN", Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => corpus.ReportAsync(new(CoreSeed.StableId("tenant:isolated"), Actor), Universe, setup.Manifest, "IN", Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => corpus.ImportAsync(new(Tenant, "m04-reader"), Universe, setup.Manifest, setup.Import, Ct));
        var other = CoreSeed.StableId("universe:synthetic"); var m = await corpus.RegisterAsync(Owner, other, await ManifestAsync(), Ct);
        await Assert.ThrowsAsync<CoreAccessException>(() => corpus.ImportAsync(Owner, other, m, setup.Import, Ct));
    }
    [SqlFact]
    public async Task Source_normalization_is_separate_frozen_text_and_reviews_have_concurrency_guards()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var setup = await SetupAsync(s); await corpus.ImportAsync(Owner, Universe, setup.Manifest, setup.Import, Ct);
        var verse = (await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "IN", Ct))[0];
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => corpus.ReviewAsync(Owner, Universe, setup.Manifest, verse.Id, new(Convert.ToBase64String([1]), "Approved", "fixture:stale"), Ct));
        await corpus.ReviewAsync(Owner, Universe, setup.Manifest, verse.Id, new(verse.RowVersion, "Rejected", "fixture:review-rejection"), Ct);
        var rejected = (await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "IN", Ct)).Single(v => v.Id == verse.Id);
        Assert.Equal("Rejected", rejected.ReviewStatus); Assert.All(rejected.Texts, t => Assert.Equal("Rejected", t.Availability));
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear();
        var text = await db.CorpusTexts.FirstAsync(t => t.VerseId == verse.Id && t.Kind == "DirectQuotation");
        Assert.Equal(CorpusRules.Normalize(text.ExactText), text.SearchText); Assert.NotEqual(text.ExactText, text.SearchText);
        text.ExactText = "overwrite"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task Annotations_are_separate_pending_records_and_do_not_rewrite_canonical_quotation()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var setup = await SetupAsync(s); await corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [setup.Import.Verses[0]]), Ct);
        var verse = (await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "IN", Ct)).Single();
        var source = await SourceTestFixture.CreateAsync(s.GetRequiredService<ISourceRegistry>(), Owner, Universe);
        var annotationText = Encoding.UTF8.GetString(SourceTestFixture.Bytes);
        foreach (var kind in new[] { "Concept", "Interpretation", "GeneratedParaphrase" })
        {
            var request = new CorpusAnnotationInput(Guid.NewGuid(), "test-" + kind, kind, "en-IN", annotationText, source.Snapshot);
            var id = await corpus.AnnotateAsync(Owner, Universe, setup.Manifest, verse.Id, request, Ct);
            Assert.Equal(id, await corpus.AnnotateAsync(Owner, Universe, setup.Manifest, verse.Id, request, Ct));
            var pending = (await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "IN", Ct)).Single().Annotations.Single(a => a.Id == id);
            Assert.Equal("", pending.Text); Assert.Equal("Pending", pending.ReviewStatus);
            await corpus.ReviewAnnotationAsync(Owner, Universe, setup.Manifest, id, new(pending.RowVersion, "Approved", "fixture:original-test-annotation"), Ct);
        }
        var after = (await corpus.ChapterAsync(Owner, Universe, setup.Manifest, 1, "IN", Ct)).Single();
        Assert.Equal(3, after.Annotations.Count);
        Assert.Equal(setup.Import.Verses[0].Texts[0].ExactText, after.Texts.Single(t => t.Kind == "DirectQuotation").ExactText);
        Assert.All(after.Annotations, a => { Assert.Equal("Approved", a.ReviewStatus); Assert.Equal(annotationText, a.Text); Assert.NotNull(a.Citation); });
    }
    [SqlFact]
    public async Task Concurrent_import_replay_uses_one_command_and_one_set_of_verse_records()
    {
        using var root = Services(); using var setupScope = root.CreateScope();
        var setup = await SetupAsync(setupScope.ServiceProvider); var request = setup.Import with { Verses = [setup.Import.Verses[0]] };
        using var a = root.CreateScope(); using var b = root.CreateScope();
        var results = await Task.WhenAll(a.ServiceProvider.GetRequiredService<ICanonicalCorpus>().ImportAsync(Owner, Universe, setup.Manifest, request, Ct),
            b.ServiceProvider.GetRequiredService<ICanonicalCorpus>().ImportAsync(Owner, Universe, setup.Manifest, request, Ct));
        Assert.Equal(1, results.Sum(r => r.Added));
        var db = setupScope.ServiceProvider.GetRequiredService<CoreDbContext>();
        Assert.Equal(1, await db.CorpusVerses.CountAsync(v => v.ManifestId == setup.Manifest));
        Assert.Equal(1, await db.CorpusCommands.CountAsync(c => c.OperationId == request.OperationId && c.TenantId == Tenant));
    }
    [SqlFact]
    public async Task Api_enforces_staff_and_action_header_without_ai_services()
    {
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Staff:Administrator:0"] = "integration-test-Administrator",
                ["Staff:Reader:0"] = "integration-test-Reader",
                ["Runtime:Enabled"] = "true",
                ["ConnectionStrings:ContentOS"] = Connection,
                ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets")
            }));
            b.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = factory.CreateClient(); var path = $"/api/v1/corpus/tenants/{Tenant}/universes/{Universe}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/staff/read")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/staff/read")).StatusCode);
        var manifest = await ManifestAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/manifests", manifest)).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/manifests", manifest)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/manifests", await ManifestAsync())).StatusCode);
    }

    [SqlFact]
    public async Task Manifest_bindings_reject_wrong_chapter_translation_snapshot_and_mapping()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var corpus = s.GetRequiredService<ICanonicalCorpus>(); var setup = await SetupAsync(s);
        var first = setup.Import.Verses[0];
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [first with { Chapter = 2 }]), Ct));
        var wrongTranslation = first with { Texts = [first.Texts[0], setup.Import.Verses[1].Texts[1]] };
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [wrongTranslation]), Ct));
        var other = await SourceAsync(s.GetRequiredService<ISourceRegistry>(), "translation", "en-IN");
        var wrongSnapshot = first with { Texts = [first.Texts[0], first.Texts[1] with { SnapshotId = other.Snapshot }] };
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [wrongSnapshot]), Ct));
        var binding = new CorpusSourceBindingInput(2, 1, "", "DirectQuotation", "sa-IN", first.Texts[0].SnapshotId, 1, 1, first.Texts[0].Sha256, first.Texts[0].SourceLocator);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => corpus.RegisterAsync(Owner, Universe, (awaitableManifest()) with { Bindings = [binding] }, Ct));
        Assert.Equal(0, (await corpus.ReportAsync(Owner, Universe, setup.Manifest, "IN", Ct)).Imported);
        Assert.Equal(1, (await corpus.ImportAsync(Owner, Universe, setup.Manifest, new(Guid.NewGuid(), [first]), Ct)).Added);
        RegisterCorpus awaitableManifest() => new(Guid.NewGuid(), "bad-map-" + Guid.NewGuid(), 1, "Fixture wrong mapping", 1, [new(2, 1, "", 1, "")]);
    }
    [SqlFact]
    public async Task Legacy_unbound_evidence_is_preserved_but_cannot_be_approved_or_read()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var setup = await SetupAsync(s); var corpus = s.GetRequiredService<ICanonicalCorpus>(); var db = s.GetRequiredService<CoreDbContext>();
        var legacy = await corpus.RegisterAsync(Owner, Universe, await ManifestAsync(), Ct);
        var first = setup.Import.Verses[0]; var v = new CorpusVerse { Id = Guid.NewGuid(), ManifestId = legacy, Chapter = 1, Verse = 1 };
        db.CorpusVerses.Add(v);
        foreach (var t in first.Texts) db.CorpusTexts.Add(new CorpusText
        {
            Id = Guid.NewGuid(),
            VerseId = v.Id,
            Kind = t.Kind,
            Locale = t.Locale,
            ExactText = t.ExactText,
            SearchText = CorpusRules.Normalize(t.ExactText),
            Sha256 = t.Sha256,
            SnapshotId = t.SnapshotId,
            SourceLocator = t.SourceLocator
        });
        await db.SaveChangesAsync();
        var row = (await corpus.ChapterAsync(Owner, Universe, legacy, 1, "IN", Ct)).Single();
        Assert.All(row.Texts, t => { Assert.Equal("", t.ExactText); Assert.Equal("UnboundEvidence", t.Availability); });
        await Assert.ThrowsAsync<RightsBlockedException>(() => corpus.ReviewAsync(Owner, Universe, legacy, row.Id, new(row.RowVersion, "Approved", "fixture:unbound"), Ct));
        var report = await corpus.ReportAsync(Owner, Universe, legacy, "IN", Ct);
        Assert.Equal(1, report.Imported); Assert.Equal(0, report.ApprovedReadable); Assert.Equal(1, report.Restricted);
    }
}


