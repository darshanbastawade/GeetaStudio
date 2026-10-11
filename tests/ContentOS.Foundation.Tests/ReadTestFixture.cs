using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class ReadTestFixture
{
    internal sealed record Fixture(CoreScope Owner, Guid Universe, Guid Collection, string Key, Guid SourceWork, EditorialPacket[] Items, Guid[] Pieces);
    internal static ServiceProvider Services()
    {
        using var existing = EditorialTestFixture.Services(); var config = existing.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var services = new ServiceCollection().AddLogging().AddSingleton(config);
        services.AddContentRuntime(config, true); services.AddContentCore(config); services.AddAiControlPlane(config); services.AddLessons(); services.AddUniverseRegistry(); services.AddDraftFactory(); services.AddQualityEvaluation(); services.AddEditorialWorkbench(); services.AddReadWorkbench();
        services.AddSingleton<ITranslationAdapter, ContentOS.Integrations.ReviewedMemoryTranslationA>(); services.AddSingleton<ITranslationAdapter, ContentOS.Integrations.ReviewedMemoryTranslationB>();
        services.AddSingleton<IFactoryTemplate, ContentOS.Integrations.LocalDraftTemplateA>(); services.AddSingleton<IFactoryTemplate, ContentOS.Integrations.LocalDraftTemplateB>();
        services.AddSingleton<IEvaluationAdapter>(new ContentOS.Integrations.EvaluationFixtureAdapter("evaluation.fixture.a.v1"));
        services.AddSingleton<ITextGenerationProvider, ContentOS.Integrations.FixtureTextA>(); services.AddSingleton<ITextGenerationProvider, ContentOS.Integrations.FixtureTextB>();
        services.AddSingleton<IEmbeddingProvider, ContentOS.Integrations.ReviewedConceptEmbedding>(); services.AddSingleton<IKnowledgeRankingAdapter, ContentOS.Integrations.SqlHybridRanking>();
        return services.BuildServiceProvider();
    }
    internal static async Task<Fixture> Setup(IServiceProvider s, string actor = "m14-owner")
    {
        var f = await EditorialTestFixture.Setup(s, actor); var db = s.GetRequiredService<CoreDbContext>(); var lessons = s.GetRequiredService<ILessonEngine>(); var corpus = s.GetRequiredService<ICanonicalCorpus>(); var sources = s.GetRequiredService<ISourceRegistry>();
        await lessons.ProposeDefaultsAsync(f.Owner, f.Universe, default);
        foreach (var profile in await lessons.ProfilesAsync(f.Owner, f.Universe, default))
            await lessons.ReviewProfileAsync(f.Owner, f.Universe, profile.Id, new(Guid.NewGuid(), profile.RowVersion, true, "fixture:read-profile-not-human-review"), default);
        var profiles = (await lessons.ProfilesAsync(f.Owner, f.Universe, default)).ToArray();
        var samples = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M04", "sample.json"))).RootElement.EnumerateArray().Take(2).ToArray();
        var binding = new List<CorpusSourceBindingInput>(); var inputs = new Dictionary<int, List<CorpusTextInput>> { [1] = [], [2] = [] }; Guid sourceWork = default;
        foreach (var locale in new[] { "en-IN", "sa-IN" })
        {
            var work = await sources.RegisterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "read-" + locale, "M14 exact stored samples — fixture permissions only", locale == "sa-IN" ? "Canonical" : "Translation"), default);
            if (locale == "en-IN") sourceWork = work;
            var edition = await sources.EditionAsync(f.Owner, f.Universe, work, new(Guid.NewGuid(), "read-two-verses", "M04 stored sample", "Annie Besant (1922) sample", "M14 isolated fixture", locale, "Discourse 1, verses 1–2"), default);
            var permission = await sources.PermissionAsync(f.Owner, f.Universe, edition, new(Guid.NewGuid(), "fixture:M14-not-production-permission", ["en-IN", "sa-IN", "mr-IN", "hi-IN"], ["IN"], ["Text", "Json"], false, true, true, false, true, "Fixture: exact M04 stored sample; genuine release review pending."), default);
            var detail = await sources.InspectAsync(f.Owner, f.Universe, work, default); await sources.ReviewPermissionAsync(f.Owner, f.Universe, permission, new(detail.Permissions.Single().RowVersion, "Approved"), default);
            for (var i = 0; i < 2; i++)
            {
                var text = samples[i].GetProperty(locale == "sa-IN" ? "sanskrit" : "translation").GetString()!; var locator = samples[i].GetProperty("sourceLocator").GetString()!; var bytes = Encoding.UTF8.GetBytes(text); var hash = EvidenceTestFixture.Hash(text);
                var snapshot = await sources.ImportAsync(f.Owner, f.Universe, edition, new(Guid.NewGuid(), "fixture.txt", bytes, hash, "fixture:M14-exact-source", true), default);
                detail = await sources.InspectAsync(f.Owner, f.Universe, work, default); await sources.ReviewSnapshotAsync(f.Owner, f.Universe, snapshot, new(detail.Snapshots.Single(x => x.Id == snapshot).RowVersion, "Approved"), default);
                var kind = locale == "sa-IN" ? "DirectQuotation" : "Translation"; inputs[i + 1].Add(new(kind, locale, text, hash, snapshot, locator));
                binding.Add(new(1, i + 1, "", kind, locale, snapshot, 1, i + 1, hash, locator));
            }
        }
        var manifest = await corpus.RegisterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "read-pilot", 1, "Two exact stored source samples; fixture edition, not human qualified", 2, [new(1, 1, "", 1, "Fixture"), new(1, 2, "", 2, "Fixture")], binding), default);
        await corpus.ImportAsync(f.Owner, f.Universe, manifest, new(Guid.NewGuid(), inputs.Select(x => new CorpusVerseInput(1, x.Key, "", x.Value)).ToArray()), default);
        foreach (var verse in await corpus.ChapterAsync(f.Owner, f.Universe, manifest, 1, "IN", default)) await corpus.ReviewAsync(f.Owner, f.Universe, manifest, verse.Id, new(verse.RowVersion, "Approved", "fixture:exact-read-samples"), default);
        var evidence = s.GetRequiredService<IEvidenceEngine>(); var rubric = await EvidenceTestFixture.RubricAsync(evidence, f.Owner, f.Universe);
        var bundles = new List<Guid>(); var variants = new List<LessonVariantView>();
        for (var i = 1; i <= 2; i++)
        {
            var verse = await db.CorpusVerses.AsNoTracking().SingleAsync(x => x.ManifestId == manifest && x.Verse == i);
            var en = await db.CorpusTexts.AsNoTracking().SingleAsync(x => x.VerseId == verse.Id && x.Locale == "en-IN"); var sa = await db.CorpusTexts.AsNoTracking().SingleAsync(x => x.VerseId == verse.Id && x.Locale == "sa-IN");
            var span = new EvidenceSpanInput(en.SnapshotId, 0, en.ExactText.Length, en.ExactText, en.Sha256, en.SourceLocator, "Supporting", en.Id);
            var anchor = await evidence.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "read-anchor-" + i, "", "Translation", "Translation", "en-IN", en.ExactText, "Annie Besant", "Besant 1922", "SourceExact", rubric, [span]), default);
            await EvidenceTestFixture.ApproveAsync(evidence, f.Owner, f.Universe, anchor, "DirectSource", "Supported");
            var meaning = await evidence.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "read-meaning-" + i, "", "Interpretation", "Interpretation", "en-IN", "Fixture interpretation: understand the context before making a decision. This is an attributed example, not doctrinal consensus.", "M14 fixture critic", "Fixture viewpoint", "Contested", rubric, [span]), default);
            await EvidenceTestFixture.ApproveAsync(evidence, f.Owner, f.Universe, meaning, "Attributed", "Contested");
            var bundle = await lessons.RegisterBundleAsync(f.Owner, f.Universe, new(Guid.NewGuid(), anchor, meaning, sa.Id, ["understand-context"], [], ["context"]), default); bundles.Add(bundle.Id);
            var lf = new LessonTestFixture.Fixture(f.Owner, f.Universe, bundle.Id, sourceWork, profiles);
            var variant = await lessons.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(lf), default); await lessons.ReviewVariantAsync(f.Owner, f.Universe, variant.Id, new(Guid.NewGuid(), variant.RowVersion, true, "fixture:read-child"), default);
            variants.Add(await lessons.InspectAsync(f.Owner, f.Universe, variant.Id, default));
        }
        var first = variants[0]; var second = variants[1]; var firstFixture = new LessonTestFixture.Fixture(f.Owner, f.Universe, bundles[0], sourceWork, profiles);
        var youth = await lessons.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(firstFixture, "youth", "writing-15-18"), default); await lessons.ReviewVariantAsync(f.Owner, f.Universe, youth.Id, new(Guid.NewGuid(), youth.RowVersion, true, "fixture:read-youth"), default); youth = await lessons.InspectAsync(f.Owner, f.Universe, youth.Id, default);
        var target = first.Sections! with { Situation = "Fixture only: शाळेतील प्रसंग.", Interpretation = "Fixture only: निर्णयाआधी संदर्भ पाहा. हे मत आहे.", Example = "Fixture only: मित्रांच्या खेळाचा संदर्भ पाहा.", PracticalAction = "Fixture only: थांबा आणि संदर्भ तपासा.", Reflection = "Fixture only: संदर्भ कसा बदलतो?" };
        var memory = await lessons.RegisterMemoryAsync(f.Owner, f.Universe, new(Guid.NewGuid(), profiles.Single(x => x.Rules.Key == "mr-IN").Id, first.BundleId, first.Sections!, target, "Fixture only; genuine native-language review pending."), default);
        var mv = (await lessons.MemoryAsync(f.Owner, f.Universe, default)).Single(x => x.Id == memory); await lessons.ReviewMemoryAsync(f.Owner, f.Universe, memory, new(Guid.NewGuid(), mv.RowVersion, true, "fixture:read-memory"), default);
        var mr = await lessons.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(firstFixture, locale: "mr-IN"), default); await lessons.ReviewVariantAsync(f.Owner, f.Universe, mr.Id, new(Guid.NewGuid(), mr.RowVersion, true, "fixture:read-mr"), default); mr = await lessons.InspectAsync(f.Owner, f.Universe, mr.Id, default);
        var verses = await db.CorpusVerses.AsNoTracking().Where(x => x.ManifestId == manifest).ToArrayAsync();
        var subject = Guid.NewGuid(); db.Subjects.Add(new() { Id = subject, UniverseId = f.Universe, Key = "read-pilot", Type = "Theme", Name = "Fixture reading" }); await db.SaveChangesAsync();
        var read = s.GetRequiredService<IReadWorkbench>(); var key = "read-" + Guid.NewGuid().ToString("N");
        var collection = await read.RegisterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), key, 1, "Geeta reading — private fixture", manifest, "Fixture", "Two verses/four variants; genuine release reviews pending."), default);
        var editorial = s.GetRequiredService<IEditorialWorkbench>(); var items = new List<EditorialPacket>(); var pieces = new List<Guid>();
        foreach (var variant in new[] { first, youth, mr, second })
        {
            var audience = variant.Snapshot.Audience.Rules.Audience!.AudienceId; var locale = variant.Snapshot.Locale.Rules.Locale!.Locale;
            var packet = await editorial.StartAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "read-" + variant.Id, "Article", "Private fixture explanation",
                new(Guid.NewGuid(), subject, audience, locale, "A context-aware reading — " + variant.Snapshot.Audience.Rules.Key + " " + locale,
                    variant.Sections!.Interpretation + "\n" + variant.Sections.Example, "Fiction", "Owned", "fixture:read-original-treatment")), default);
            foreach (var snapshot in variant.Snapshot.Bundle.SourceIds)
                await s.GetRequiredService<ISourceRegistry>().LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), snapshot, null, packet.RevisionId, null, "adaptation"), default);
            packet = await editorial.InspectAsync(f.Owner, f.Universe, packet.Id, default);
            packet = await Approved(s, f.Owner, f.Universe, packet, audience, locale);
            items.Add(packet);
            pieces.Add(await read.AddAsync(f.Owner, f.Universe, new(Guid.NewGuid(), collection, verses.Single(x => x.Verse == (variant.Id == second.Id ? 2 : 1)).Id, packet.Id, variant.Id, "chronological", variant.Id == second.Id ? 2 : 1), default));
        }
        return new(f.Owner, f.Universe, collection, key, sourceWork, items.ToArray(), pieces.ToArray());
    }
    private static async Task<EditorialPacket> Approved(IServiceProvider s, CoreScope owner, Guid u, EditorialPacket p, Guid audience, string locale)
    {
        var editorial = s.GetRequiredService<IEditorialWorkbench>(); p = await editorial.ActAsync(owner, u, p.Id, EditorialTestFixture.Action(p, "Draft"), default);
        var q = s.GetRequiredService<IQualityEvaluation>(); var c = ContentOS.Integrations.EvaluationFixtureCatalog.Create(audience).Cases.First() with { Locale = locale, RequiredProperties = [], ForbiddenProperties = [], ExpectedTextIds = [] };
        var d = await q.RegisterDatasetAsync(owner, u, new(Guid.NewGuid(), "read-body-" + Guid.NewGuid(), 1, "Private fixture current-body validation; not human acceptance.", [c]), default);
        var candidate = await q.RegisterCandidateAsync(owner, u, new(Guid.NewGuid(), "read-candidate-" + Guid.NewGuid(), 1, new("recorded.v1", "generated-text.v1", "none.v1", "read.v1", "fixture.v1")), default);
        var report = await q.RunAsync(owner, u, new(Guid.NewGuid(), d.Id, candidate.Id, "Development", RecordedOutputs: [new(c.Key, new(p.Current!.Body, locale, [], false, [], [], null, [], 0, "INR"))]), default);
        p = await editorial.ActAsync(owner, u, p.Id, EditorialTestFixture.Action(p, "Evaluate", new(report.Id, c.Key)), default);
        p = await editorial.ActAsync(owner, u, p.Id, EditorialTestFixture.Action(p, "Review"), default);
        return await editorial.ActAsync(owner, u, p.Id, EditorialTestFixture.Action(p, "Approve"), default);
    }
}
