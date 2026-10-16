using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

// Isolated synthetic test universe. Permissions/reviews below qualify fixtures only, never the M04 development corpus.
internal static class LessonTestFixture
{
    internal const string Connection = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
    internal sealed record Fixture(CoreScope Owner, Guid Universe, Guid Bundle, Guid SourceWork, LessonProfileView[] Profiles);
    internal static ServiceProvider ServicesWithRegistry() => BuildServices(true);
    internal static ServiceProvider Services() => BuildServices(false);
    private static ServiceProvider BuildServices(bool registry)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ContentOS"] = Connection, ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets") }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddSingleton<IConfiguration>(config);
        services.AddContentRuntime(config, true); services.AddContentCore(config); services.AddLessons();
        if (registry) services.AddUniverseRegistry();
        services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationA>(); services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationB>();
        return services.BuildServiceProvider();
    }
    internal static async Task<Fixture> Setup(IServiceProvider s, string actor = "m09-owner", bool aiProcessing = false)
    {
        var db = s.GetRequiredService<CoreDbContext>(); var org = Guid.NewGuid(); var tenant = Guid.NewGuid(); var u = Guid.NewGuid();
        db.Organizations.Add(new() { Id = org, Name = "M09 synthetic fixture" }); db.Tenants.Add(new() { Id = tenant, OrganizationId = org, Key = "m09-" + tenant, Name = "M09 fixture — not release qualified" });
        db.Universes.Add(new() { Id = u, TenantId = tenant, Key = "fixture", Name = "M09 isolated test teaching" });
        foreach (var name in new[] { actor, "integration-test-Administrator" }.Distinct()) db.Members.Add(new() { TenantId = tenant, Actor = name, Permission = "Administrator" });
        db.Members.Add(new() { TenantId = tenant, Actor = "m09-reader", Permission = "Reader" });
        foreach (var locale in new[] { "en-IN", "sa-IN", "mr-IN", "hi-IN" }) if (!await db.Locales.AnyAsync(x => x.Id == locale)) db.Locales.Add(new() { Id = locale, Name = locale });
        await db.SaveChangesAsync(); var owner = new CoreScope(tenant, actor);
        var engine = s.GetRequiredService<ILessonEngine>(); await engine.ProposeDefaultsAsync(owner, u, default);
        foreach (var p in await engine.ProfilesAsync(owner, u, default)) await engine.ReviewProfileAsync(owner, u, p.Id, new(Guid.NewGuid(), p.RowVersion, true, "fixture:rule-behavior-only-not-human-quality"), default);
        var row = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M04", "sample.json"))).RootElement[0];
        var english = row.GetProperty("translation").GetString()!; var sanskrit = row.GetProperty("sanskrit").GetString()!; var locator = row.GetProperty("sourceLocator").GetString()!;
        var sources = s.GetRequiredService<ISourceRegistry>(); var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var snapshots = new Dictionary<string, Guid>(); Guid workId = default;
        foreach (var part in new[] { (Locale: "en-IN", Text: english, Kind: "Translation"), (Locale: "sa-IN", Text: sanskrit, Kind: "Canonical") })
        {
            var work = await sources.RegisterAsync(owner, u, new(Guid.NewGuid(), part.Locale, "M09 fixture source — test permissions only", part.Kind), default); if (part.Locale == "en-IN") workId = work;
            var edition = await sources.EditionAsync(owner, u, work, new(Guid.NewGuid(), "fixture-1.1", "M04 stored sample", "Annie Besant (1922) sample", "M09 test harness", part.Locale, locator), default);
            var permission = await sources.PermissionAsync(owner, u, edition, new(Guid.NewGuid(), "fixture:M09-permission-not-a-production-license", ["en-IN", "sa-IN", "mr-IN", "hi-IN"], ["IN"], ["Text", "Json"], false, true, true, aiProcessing, true, "Fixture: M04 stored Besant/Wikisource sample; no publication permission asserted"), default);
            var detail = await sources.InspectAsync(owner, u, work, default); await sources.ReviewPermissionAsync(owner, u, permission, new(detail.Permissions.Single().RowVersion, "Approved"), default);
            var bytes = Encoding.UTF8.GetBytes(part.Text);
            var snapshot = await sources.ImportAsync(owner, u, edition, new(Guid.NewGuid(), "fixture.txt", bytes, Convert.ToHexString(SHA256.HashData(bytes)), "fixture:M04-exact-one-verse", true), default);
            detail = await sources.InspectAsync(owner, u, work, default); await sources.ReviewSnapshotAsync(owner, u, snapshot, new(detail.Snapshots.Single().RowVersion, "Approved"), default);
            snapshots[part.Locale] = snapshot;
        }
        var texts = new CorpusTextInput[] { new("Translation", "en-IN", english, EvidenceTestFixture.Hash(english), snapshots["en-IN"], locator), new("DirectQuotation", "sa-IN", sanskrit, EvidenceTestFixture.Hash(sanskrit), snapshots["sa-IN"], locator) };
        var manifest = await corpus.RegisterAsync(owner, u, new(Guid.NewGuid(), "fixture", 1, "M04 single verse in isolated fixture universe", 1,
            [new(1, 1, "", 1, "Fixture sample only")], texts.Select(t => new CorpusSourceBindingInput(1, 1, "", t.Kind, t.Locale, t.SnapshotId, 1, 1, t.Sha256, locator)).ToArray()), default);
        await corpus.ImportAsync(owner, u, manifest, new(Guid.NewGuid(), [new(1, 1, "", texts)]), default);
        var verse = (await corpus.ChapterAsync(owner, u, manifest, 1, "IN", default)).Single();
        await corpus.ReviewAsync(owner, u, manifest, verse.Id, new(verse.RowVersion, "Approved", "fixture:exact-source-behavior-only"), default);
        verse = (await corpus.ChapterAsync(owner, u, manifest, 1, "IN", default)).Single();
        var enText = verse.Texts.Single(x => x.Kind == "Translation");
        var evidence = s.GetRequiredService<IEvidenceEngine>(); var rubric = await EvidenceTestFixture.RubricAsync(evidence, owner, u);
        var span = new EvidenceSpanInput(snapshots["en-IN"], 0, english.Length, english, EvidenceTestFixture.Hash(english), locator, "Supporting", enText.Id);
        var anchor = await evidence.SubmitAsync(owner, u, new(Guid.NewGuid(), "anchor", "", "Translation", "Translation", "en-IN", english, "Annie Besant", "Besant 1922", "SourceExact", rubric, [span]), default);
        await EvidenceTestFixture.ApproveAsync(evidence, owner, u, anchor, "DirectSource", "Supported");
        var meaning = await evidence.SubmitAsync(owner, u, new(Guid.NewGuid(), "meaning", "", "Interpretation", "Interpretation", "en-IN",
            "Fixture interpretation: check the context of a situation before deciding what to do. This is an attributed teaching example, not a doctrinal consensus.",
            "M09 fixture critic", "Fixture viewpoint", "Contested", rubric, [span]), default);
        await EvidenceTestFixture.ApproveAsync(evidence, owner, u, meaning, "Attributed", "Contested");
        var bundle = await engine.RegisterBundleAsync(owner, u, new(Guid.NewGuid(), anchor, meaning, verse.Texts.Single(x => x.Kind == "DirectQuotation").Id,
            ["understand-context"], [], ["context"]), default);
        return new(owner, u, bundle.Id, workId, (await engine.ProfilesAsync(owner, u, default)).ToArray());
    }
    internal static AdaptLesson Request(Fixture f, string audience = "children", string writing = "writing-children", string locale = "en-IN", string route = "translation-a") =>
        new(Guid.NewGuid(), f.Bundle, f.Profiles.Single(x => x.Rules.Key == audience).Id, f.Profiles.Single(x => x.Rules.Key == writing).Id,
            f.Profiles.Single(x => x.Rules.Key == locale).Id, f.Profiles.Single(x => x.Rules.Key == route).Id);
}
