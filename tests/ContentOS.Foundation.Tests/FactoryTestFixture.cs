using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class FactoryTestFixture
{
    internal sealed record Fixture(CoreScope Owner, Guid Universe, Guid Lesson, Guid SecondLesson, Guid SourceWork, Guid Character, Guid Bible, Guid Episode, Guid Audience);
    internal static ServiceProvider Services(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection(); using var baseServices = LessonTestFixture.ServicesWithRegistry();
        var config = baseServices.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(); services.AddSingleton(config); services.AddLogging(); services.AddContentRuntime(config, true); services.AddContentCore(config); services.AddLessons(); services.AddUniverseRegistry(); services.AddDraftFactory();
        services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationA>(); services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationB>(); services.AddSingleton<IFactoryTemplate, LocalDraftTemplateA>(); services.AddSingleton<IFactoryTemplate, LocalDraftTemplateB>(); configure?.Invoke(services); return services.BuildServiceProvider();
    }
    internal static async Task<Fixture> Setup(IServiceProvider s, string actor = "m11-owner", bool aiProcessing = false)
    {
        var f = await LessonTestFixture.Setup(s, actor, aiProcessing); var lessons = s.GetRequiredService<ILessonEngine>(); var variant = await lessons.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f), default); await ReviewLesson(s, f, variant);
        var second = await Second(s, f); var registry = s.GetRequiredService<IUniverseRegistry>(); var audience = variant.Snapshot.Audience.Rules.Audience!.AudienceId;
        var bible = await registry.RegisterBibleAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m11-bible", 1, RegistryTestFixture.Bible(audience)), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, bible);
        var episode = await registry.RegisterEpisodeAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m11-episode"), default);
        var rf = new RegistryTestFixture.Fixture(f.Owner, f.Universe, audience, bible, episode);
        var rules = RegistryTestFixture.Rules(rf, "New fixture learner") with { SituationTags = ["collaboration"], SkillTags = [], LearningObjectives = ["understand-context"] };
        var character = await registry.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "new-m11-learner", "Guest", rules), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, character.Profile.Id);
        await registry.ControlCharacterAsync(f.Owner, f.Universe, character.CharacterId, new(Guid.NewGuid(), character.RowVersion, "Activate", "fixture:compatible-character"), default);
        return new(f.Owner, f.Universe, variant.Id, second, f.SourceWork, character.CharacterId, bible, episode, audience);
    }
    private static async Task ReviewLesson(IServiceProvider s, LessonTestFixture.Fixture f, LessonVariantView v) => await s.GetRequiredService<ILessonEngine>().ReviewVariantAsync(f.Owner, f.Universe, v.Id, new(Guid.NewGuid(), v.RowVersion, true, "fixture:reviewed-input-not-human-qualification"), default);
    private static async Task<Guid> Second(IServiceProvider s, LessonTestFixture.Fixture f)
    {
        var owner = f.Owner; var u = f.Universe; var engine = s.GetRequiredService<ILessonEngine>(); var row = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M04", "sample.json"))).RootElement[1];
        var english = row.GetProperty("translation").GetString()!; var sanskrit = row.GetProperty("sanskrit").GetString()!; var locator = row.GetProperty("sourceLocator").GetString()!;
        var sources = s.GetRequiredService<ISourceRegistry>(); var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var snapshots = new Dictionary<string, Guid>(); Guid workId = default;
        foreach (var part in new[] { (Locale: "en-IN", Text: english, Kind: "Translation"), (Locale: "sa-IN", Text: sanskrit, Kind: "Canonical") })
        {
            var work = await sources.RegisterAsync(owner, u, new(Guid.NewGuid(), part.Locale + "-second", "M11 fixture source — test permissions only", part.Kind), default); if (part.Locale == "en-IN") workId = work;
            var edition = await sources.EditionAsync(owner, u, work, new(Guid.NewGuid(), "fixture-1.2", "M04 stored sample", "Annie Besant (1922) sample", "M09 test harness", part.Locale, locator), default);
            var permission = await sources.PermissionAsync(owner, u, edition, new(Guid.NewGuid(), "fixture:M09-permission-not-a-production-license", ["en-IN", "sa-IN", "mr-IN", "hi-IN"], ["IN"], ["Text", "Json"], false, true, true, false, true, "Fixture: M04 stored Besant/Wikisource sample; no publication permission asserted"), default);
            var detail = await sources.InspectAsync(owner, u, work, default); await sources.ReviewPermissionAsync(owner, u, permission, new(detail.Permissions.Single().RowVersion, "Approved"), default);
            var bytes = Encoding.UTF8.GetBytes(part.Text);
            var snapshot = await sources.ImportAsync(owner, u, edition, new(Guid.NewGuid(), "fixture.txt", bytes, Convert.ToHexString(SHA256.HashData(bytes)), "fixture:M04-exact-one-verse", true), default);
            detail = await sources.InspectAsync(owner, u, work, default); await sources.ReviewSnapshotAsync(owner, u, snapshot, new(detail.Snapshots.Single().RowVersion, "Approved"), default);
            snapshots[part.Locale] = snapshot;
        }
        var texts = new CorpusTextInput[] { new("Translation", "en-IN", english, EvidenceTestFixture.Hash(english), snapshots["en-IN"], locator), new("DirectQuotation", "sa-IN", sanskrit, EvidenceTestFixture.Hash(sanskrit), snapshots["sa-IN"], locator) };
        var manifest = await corpus.RegisterAsync(owner, u, new(Guid.NewGuid(), "fixture-second", 1, "M04 second verse in isolated fixture universe; first inventory entry is explicitly missing here", 2,
            [new(1, 1, "", 1, "Explicitly missing from this second manifest"), new(1, 2, "", 2, "Fixture sample only")], texts.Select(t => new CorpusSourceBindingInput(1, 2, "", t.Kind, t.Locale, t.SnapshotId, 1, 2, t.Sha256, locator)).ToArray()), default);
        await corpus.ImportAsync(owner, u, manifest, new(Guid.NewGuid(), [new(1, 2, "", texts)]), default);
        var verse = (await corpus.ChapterAsync(owner, u, manifest, 1, "IN", default)).Single();
        await corpus.ReviewAsync(owner, u, manifest, verse.Id, new(verse.RowVersion, "Approved", "fixture:exact-source-behavior-only"), default);
        verse = (await corpus.ChapterAsync(owner, u, manifest, 1, "IN", default)).Single();
        var enText = verse.Texts.Single(x => x.Kind == "Translation");
        var evidence = s.GetRequiredService<IEvidenceEngine>(); var rubric = await EvidenceTestFixture.RubricAsync(evidence, owner, u);
        var span = new EvidenceSpanInput(snapshots["en-IN"], 0, english.Length, english, EvidenceTestFixture.Hash(english), locator, "Supporting", enText.Id);
        var anchor = await evidence.SubmitAsync(owner, u, new(Guid.NewGuid(), "anchor-second", "", "Translation", "Translation", "en-IN", english, "Annie Besant", "Besant 1922", "SourceExact", rubric, [span]), default);
        await EvidenceTestFixture.ApproveAsync(evidence, owner, u, anchor, "DirectSource", "Supported");
        var meaning = await evidence.SubmitAsync(owner, u, new(Guid.NewGuid(), "meaning-second", "", "Interpretation", "Interpretation", "en-IN",
            "Fixture interpretation: check the context of a situation before deciding what to do. This is an attributed teaching example, not a doctrinal consensus.",
            "M09 fixture critic", "Fixture viewpoint", "Contested", rubric, [span]), default);
        await EvidenceTestFixture.ApproveAsync(evidence, owner, u, meaning, "Attributed", "Contested");
        var bundle = await engine.RegisterBundleAsync(owner, u, new(Guid.NewGuid(), anchor, meaning, verse.Texts.Single(x => x.Kind == "DirectQuotation").Id,
            ["understand-context"], [], ["context"]), default);
        var profiles = await engine.ProfilesAsync(owner, u, default); var old = profiles.Single(p => p.Rules.Key == "children"); var audienceRules = old.Rules.Audience! with { Example = "At a school gathering, notice the available information and ask a respectful question before making a group decision.", PracticalAction = "Describe what you observed, invite another view, and choose a calm question.", Reflection = "Which observation would you verify before deciding?" };
        var aid = await engine.RegisterProfileAsync(owner, u, old.Rules with { OperationId = Guid.NewGuid(), Version = 2, Audience = audienceRules }, default);
        var ap = (await engine.ProfilesAsync(owner, u, default)).Single(p => p.Id == aid); await engine.ReviewProfileAsync(owner, u, aid, new(Guid.NewGuid(), ap.RowVersion, true, "fixture:second-reviewed-treatment"), default);
        var writing = profiles.Single(p => p.Rules.Key == "writing-children"); var wid = await engine.RegisterProfileAsync(owner, u, writing.Rules with { OperationId = Guid.NewGuid(), Version = 2, Writing = writing.Rules.Writing! with { AudienceProfileId = aid } }, default);
        var wp = (await engine.ProfilesAsync(owner, u, default)).Single(p => p.Id == wid); await engine.ReviewProfileAsync(owner, u, wid, new(Guid.NewGuid(), wp.RowVersion, true, "fixture:second-writing"), default);
        var v = await engine.AdaptAsync(owner, u, new(Guid.NewGuid(), bundle.Id, aid, wid, profiles.Single(p => p.Rules.Key == "en-IN").Id, profiles.Single(p => p.Rules.Key == "translation-a").Id), default); await ReviewLesson(s, f, v); return v.Id;
    }
    internal static CastingBrief Cast(Fixture f) => new(f.Bible, f.Episode, f.Audience, "en-IN", ["collaboration"], [], ["understand-context"], ["Lead"], ["original-timeline"], [], new(4, 0, "INR"), []);
    internal static RegisterFactoryPlan Request(Fixture f, string key = "pilot", bool second = true) => new(Guid.NewGuid(), key, 1, "Reviewed fixture pilot", "Chronological", "local-template.a.v1",
        (second ? new[] { f.Lesson, f.SecondLesson } : new[] { f.Lesson }).Select((id, i) => new FactoryBrief("verse-" + (i + 1), id, Cast(f), [], ["context"], "understand-context", 1, i + 1, "Chronological fixture", i + 1, 50, FactoryRules.Formats.Select(k => new FactoryFormat(k, "Preserve labels and show a practical reflection.")).ToArray())).ToArray());
    internal static async Task<FactoryPlanView> Approved(IServiceProvider s, Fixture f, RegisterFactoryPlan? request = null)
    {
        var factory = s.GetRequiredService<IDraftFactory>(); var p = await factory.RegisterPlanAsync(f.Owner, f.Universe, request ?? Request(f), default); await factory.ReviewPlanAsync(f.Owner, f.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, true, "fixture:plan-not-human-quality"), default);
        return (await factory.PlansAsync(f.Owner, f.Universe, default)).Single(x => x.Id == p.Id);
    }
    internal static StartFactoryBatch Start(FactoryPlanView p, string key = "request") => new(Guid.NewGuid(), key, p.Id, new(0, "INR", 200000, 600, 60));
    internal static async Task Drain(IServiceProvider s)
    {
        for (var i = 0; i < 100; i++)
        {
            if (await s.GetRequiredService<IDraftFactory>().ProcessNextAsync(default)) continue;
            var now = DateTimeOffset.UtcNow; var db = s.GetRequiredService<CoreDbContext>();
            if (!await db.FactoryBatches.AnyAsync(b => (b.State == "Queued" || b.State == "Running") && db.FactoryWorks.Any(w => w.BatchId == b.Id && (w.State == "Pending" || w.State == "Running" && w.LeaseUntilUtc <= now)))) return;
        }
        throw new Exception("Bounded fixture drain exceeded.");
    }
}
