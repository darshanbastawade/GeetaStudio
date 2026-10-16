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

internal static class TrendTestFixture
{
    internal sealed record Fixture(CoreScope Owner, Guid Universe, Guid Connector, Guid Cluster, Guid NewsClaim, Guid[] NewsSources, Guid ChildLesson, Guid YouthLesson,
        Guid ChildAudience, Guid YouthAudience, Guid Bible, Guid Episode, Guid ChildCharacter, Guid YouthCharacter, KnowledgeQuery Query);
    internal static ServiceProvider Services(IEmbeddingProvider? embedding = null, Action<IServiceCollection>? configure = null)
    {
        using var basis = ReadTestFixture.Services(); var config = basis.GetRequiredService<IConfiguration>();
        var services = new ServiceCollection().AddLogging().AddSingleton(config);
        services.AddContentRuntime(config, true); services.AddContentCore(config); services.AddAiControlPlane(config); services.AddLessons(); services.AddUniverseRegistry();
        services.AddDraftFactory(); services.AddQualityEvaluation(); services.AddEditorialWorkbench(); services.AddReadWorkbench(); services.AddTrendWorkbench();
        services.AddSingleton<ITextGenerationProvider, FixtureTextA>(); services.AddSingleton<ITextGenerationProvider, FixtureTextB>();
        services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationA>(); services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationB>();
        services.AddSingleton<IFactoryTemplate, LocalDraftTemplateA>(); services.AddSingleton<IFactoryTemplate, LocalDraftTemplateB>();
        services.AddSingleton<IEmbeddingProvider>(embedding ?? new ReviewedConceptEmbedding()); services.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>();
        services.AddSingleton<IEvaluationAdapter>(new EvaluationFixtureAdapter("evaluation.fixture.a.v1"));
        services.AddSingleton<ITrendSource, FixtureTrendSourceA>(); services.AddSingleton<ITrendSource, FixtureTrendSourceB>(); services.AddSingleton<ITrendSource, UploadedRssTrendSource>();
        services.AddSingleton<ITrendExtractor, OwnedTrendExtractorA>(); services.AddSingleton<ITrendExtractor, OwnedTrendExtractorB>();
        configure?.Invoke(services); return services.BuildServiceProvider();
    }
    internal const string Fact = "A fictional community team gathered in a hall and checked the context before deciding.";
    internal static async Task<Guid> Source(IServiceProvider s, CoreScope owner, Guid u, string key, string text, bool video = false)
    {
        var sources = s.GetRequiredService<ISourceRegistry>();
        var work = await sources.RegisterAsync(owner, u, new(Guid.NewGuid(), key, "M15 controlled fixture — not real reporting", "CurrentEvent"), default);
        var edition = await sources.EditionAsync(owner, u, work, new(Guid.NewGuid(), "v1", "Fixture primary document", "Fixture author", "Owned test harness", "en-IN", "fixture:" + key), default);
        var permission = await sources.PermissionAsync(owner, u, edition, new(Guid.NewGuid(), video ? "fixture:owned-synthetic-watch-video-and-ai-test-only" : "fixture:owned-test-text", ["en-IN"], ["IN"], (video ? new[] { "Text", "Json", "Video" } : new[] { "Text", "Json" }), false, true, true, video, true, "Fixture access only; no live/public rights qualification."), default);
        var detail = await sources.InspectAsync(owner, u, work, default); await sources.ReviewPermissionAsync(owner, u, permission, new(detail.Permissions.Single().RowVersion, "Approved"), default);
        var id = await sources.ImportAsync(owner, u, edition, new(Guid.NewGuid(), "fixture.txt", Encoding.UTF8.GetBytes(text), EvidenceTestFixture.Hash(text), "fixture:M15-synthetic-event", true), default);
        detail = await sources.InspectAsync(owner, u, work, default); await sources.ReviewSnapshotAsync(owner, u, id, new(detail.Snapshots.Single().RowVersion, "Approved"), default); return id;
    }
    internal static TrendSignal Signal(Guid[] ids, string eventKey = "useful", string origin = "fixture-primary", bool primary = true) =>
        new("fixture-" + origin, eventKey, origin, primary, "News", "Fixture team checks the context", "https://example.invalid/fixture/" + origin, "Teamwork", "IN", "en-IN",
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(-1), null, DateTimeOffset.UtcNow, ids, null, false, [], []);
    internal static async Task<Fixture> Setup(IServiceProvider s, string actor = "m15-owner", bool video = false, bool generationFixture = false)
    {
        var f = await ReadTestFixture.Setup(s, actor, generationFixture); var db = s.GetRequiredService<CoreDbContext>(); var lessons = s.GetRequiredService<ILessonEngine>();
        var candidates = new List<LessonVariantView>(); foreach (var row in await db.LessonVariants.AsNoTracking().Where(x => x.UniverseId == f.Universe && x.TenantId == f.Owner.TenantId && x.ReviewStatus == "Approved").ToListAsync())
            candidates.Add(await lessons.InspectAsync(f.Owner, f.Universe, row.Id, default));
        var youth = candidates.First(x => x.Snapshot.Audience.Rules.Key == "youth" && x.Snapshot.Locale.Rules.Locale!.Locale == "en-IN");
        var child = candidates.First(x => x.Snapshot.Audience.Rules.Key == "children" && x.Snapshot.Locale.Rules.Locale!.Locale == "en-IN" && x.Snapshot.Bundle.Request.AnchorClaimRevisionId == youth.Snapshot.Bundle.Request.AnchorClaimRevisionId);
        var ca = child.Snapshot.Audience.Rules.Audience!.AudienceId; var ya = youth.Snapshot.Audience.Rules.Audience!.AudienceId;
        var registry = s.GetRequiredService<IUniverseRegistry>(); var rules = RegistryTestFixture.Bible(ca, "M15 controlled original-fiction bible");
        var bible = await registry.RegisterBibleAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-bible", 1, rules with { Audiences = [new(ca, true, false, "Gentle"), new(ya, true, false, "Respectful")] }), default);
        await RegistryTestFixture.Review(s, f.Owner, f.Universe, bible);
        var episode = await registry.RegisterEpisodeAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-episode"), default);
        async Task<Guid> Guest(LessonVariantView lesson, Guid audience, string name)
        {
            var rf = new RegistryTestFixture.Fixture(f.Owner, f.Universe, audience, bible, episode);
            var cr = RegistryTestFixture.Rules(rf, name, stage: lesson.Snapshot.Writing.Rules.Writing!.EligibleLifeStages.First());
            var guest = await registry.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-" + audience.ToString("N"), "Guest", cr), default);
            await RegistryTestFixture.Review(s, f.Owner, f.Universe, guest.Profile.Id);
            await registry.ControlCharacterAsync(f.Owner, f.Universe, guest.CharacterId, new(Guid.NewGuid(), guest.RowVersion, "Activate", "fixture:M15-new-compatible-guest"), default); return guest.CharacterId;
        }
        var childChar = await Guest(child, ca, "New child context-checker"); var youthChar = await Guest(youth, ya, "New youth discussion organizer");
        var sources = new[]{await Source(s,f.Owner,f.Universe,"m15-primary",Fact+"\nControlled primary material, fictional event.", video),
            await Source(s,f.Owner,f.Universe,"m15-independent",Fact+"\nControlled independent observation, fictional event.", video)};
        var evidence = s.GetRequiredService<IEvidenceEngine>(); var rubric = await evidence.RegisterRubricAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-event-rubric", 1, "Controlled primary/independent synthetic reports", 2), default);
        var spans = new List<EvidenceSpanInput>(); foreach (var id in sources) { var snap = await db.SourceSnapshots.AsNoTracking().SingleAsync(x => x.Id == id); spans.Add(new(id, 0, Fact.Length, Fact, EvidenceTestFixture.Hash(Fact), "fixture:M15-report", "Supporting")); }
        var news = await evidence.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-news", "", "NewsClaim", "NewsClaim", "en-IN", Fact, "Fixture reviewers", "", "Limited", rubric, spans), default);
        var head = (await evidence.ListAsync(f.Owner, f.Universe, default)).Single(x => x.Revisions.Any(v => v.Id == news)); var revision = head.Revisions.Single(x => x.Id == news);
        await evidence.ReviewAsync(f.Owner, f.Universe, news, new(revision.RowVersion, "Approved", "Corroborated", "Supported", "fixture:controlled-event-not-human-release"), default);
        var trends = s.GetRequiredService<ITrendWorkbench>(); var config = new RegisterTrendConnector(Guid.NewGuid(), "fixture-source", 1, "trend.fixture.a.v1", "trend.owned-text.a.v1", "Fixture", "fixture:source-contract", [Signal([sources[0]]), Signal([sources[1]], origin: "fixture-independent", primary: false)]);
        var connector = await trends.ConnectorAsync(f.Owner, f.Universe, config, default); await trends.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), connector), default);
        var cluster = (await trends.ClustersAsync(f.Owner, f.Universe, default)).Single();
        await trends.ReviewClusterAsync(f.Owner, f.Universe, cluster.Id, new(Guid.NewGuid(), cluster.RowVersion, "Approve", news, "How can we understand the setting before reacting?", DateTimeOffset.UtcNow.AddHours(6), "Fixture", "fixture:checked-synthetic-facts"), default);
        var text = await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == child.Snapshot.Bundle.Request.AnchorClaimRevisionId && x.CorpusTextId != null).Select(x => x.CorpusTextId!.Value).FirstAsync();
        var corpus = await db.CorpusTexts.AsNoTracking().SingleAsync(x => x.Id == text); var verse = await db.CorpusVerses.AsNoTracking().SingleAsync(x => x.Id == corpus.VerseId);
        var admin = s.GetRequiredService<IKnowledgeAdministration>();
        var concept = await admin.ConceptAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "gathering-context", corpus.SnapshotId, [new("en-IN", "gather", "Name"), new("en-IN", "armies", "Alias")], child.Snapshot.Bundle.Request.AnchorClaimRevisionId), default);
        var graph = await admin.GraphAsync(f.Owner, f.Universe, default); await admin.ReviewAsync(f.Owner, f.Universe, concept, new(graph.Concepts.Single(x => x.Id == concept).RowVersion, "Approved", "fixture:M15-concept"), default);
        var indexId = await admin.QueueAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-index", verse.ManifestId, "en-IN", "IN", "sql-hybrid-v1", "concept-projection-v1"), default);
        for (var i = 0; i < 100; i++) { var item = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == indexId); if (item.State == "Ready") break; await s.GetRequiredService<IKnowledgeIndexWorker>().ProcessNextAsync(default); }
        var index = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == indexId);
        await admin.ValidateAsync(f.Owner, f.Universe, indexId, new(index.RowVersion, "manual:M06-owner-approved-two-labels-reused-on-private-fixture", [new("Where did the opposing armies gather?", text), new("How do I bake a pizza?", null)]), default);
        index = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == indexId);
        await admin.ActivateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-teaching", indexId, "", index.EmbeddingSpaceId), default);
        return new(f.Owner, f.Universe, connector, cluster.Id, news, sources, child.Id, youth.Id, ca, ya, bible, episode, childChar, youthChar, new("m15-teaching", "Where did the opposing armies gather?", "en-IN", "IN", index.EmbeddingSpaceId));
    }
    internal static async Task<TrendTeachingView> Match(IServiceProvider s, Fixture f, string? query = null)
    {
        var w = s.GetRequiredService<ITrendWorkbench>(); var match = await w.LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Cluster, f.ChildLesson, f.Query with { Query = query ?? f.Query.Query },
            "The verified gathering theme supports checking context; not a verse endorsing the event.", ["No suitable connection if the theme is unrelated."]), default);
        if (match.Outcome == "Matched") { await w.ReviewTeachingAsync(f.Owner, f.Universe, match.Id, new(Guid.NewGuid(), match.RowVersion, "Approve", "Fixture", "fixture:controlled-context-mapping"), default); match = (await w.TeachingsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == match.Id); }
        return match;
    }
    internal static async Task<TrendStoryView> Package(IServiceProvider s, Fixture f, TrendTeachingView match)
    {
        var lessons = s.GetRequiredService<ILessonEngine>();
        async Task<FactoryBrief> Brief(Guid lesson, Guid audience, Guid character, string key)
        {
            var view = await lessons.InspectAsync(f.Owner, f.Universe, lesson, default);
            var cast = new CastingBrief(f.Bible, f.Episode, audience, "en-IN", ["teamwork"], ["reflection"], ["understand-context"], ["Lead"], ["original-timeline"], [], new(2, 0, "INR"), [new(character, "Lead")]);
            return new(key, lesson, cast, [], view.Sections!.Concepts, view.Sections.LearningGoals.First(), 1, 1, "context-checking", key == "child" ? 1 : 2, 50, [new("ShortVideoScript", "Create an original age-appropriate story about checking the setting before reacting.", 12000)]);
        }
        var plan = new RegisterFactoryPlan(Guid.NewGuid(), "m15-preparation", 1, "Two controlled context-checking stories", "PracticalTheme", "local-template.a.v1", [await Brief(f.ChildLesson, f.ChildAudience, f.ChildCharacter, "child"), await Brief(f.YouthLesson, f.YouthAudience, f.YouthCharacter, "youth")]);
        return await s.GetRequiredService<ITrendWorkbench>().PrepareAsync(f.Owner, f.Universe, new(Guid.NewGuid(), match.Id, "m15-two-audiences", 1, "OriginalFiction",
            "A fictional decision about where a team gathers; no identifiable real person.", "Gentle self-correction, never victims or identity groups.", ["LocalPreview"], plan), default);
    }
    internal static async Task<EditorialPacket> Schedule(IServiceProvider s, Fixture f, FactoryArtifactView artifact)
    {
        var db = s.GetRequiredService<CoreDbContext>(); var editorial = s.GetRequiredService<IEditorialWorkbench>();
        var lineage = await s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, artifact.CoreContentId, default);
        var packet = await editorial.StartAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "trend-scheduled", "Article", "Controlled original story; factual context separately cited", ContentId: artifact.CoreContentId, PublicIntended: false), default);
        // Existing factory content enters the workbench in Draft state.
        var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == packet.ContentId);
        var related = lineage.Provenance!.Where(x => x.ParentRevisionId.HasValue).Select(x => x.ParentRevisionId!.Value).Append(packet.Current!.Id).Distinct().ToArray();
        var registryStories = await db.RegistryRevisions.AsNoTracking().Where(x => x.TenantId == f.Owner.TenantId && x.UniverseId == f.Universe && x.Kind == "Story").Select(x => x.RulesJson).ToArrayAsync();
        var boundFactory = await db.FactoryArtifacts.AsNoTracking().Where(x => x.TenantId == f.Owner.TenantId && x.UniverseId == f.Universe && related.Contains(x.CoreRevisionId)).Select(x => x.SnapshotJson).ToArrayAsync();
        var casts = registryStories.Select(x => JsonSerializer.Deserialize<RegistryEpisodeSnapshot>(x)!).Where(x => x.ContentRevisionId.HasValue && related.Contains(x.ContentRevisionId.Value)).Concat(boundFactory.Select(x => JsonSerializer.Deserialize<FactoryArtifactSnapshot>(x)!.Input.Cast));
        var cast = casts.SelectMany(x => x.Cast).Select(x => new EvaluationCastMember(x.Character.CharacterId, x.Character.Profile.Id, x.Role, x.Presentation?.Rules.VoiceAnchor ?? x.Character.Profile.Rules.VoiceAnchor, x.Character.Profile.Rules.LifeStage)).Distinct().ToArray();
        var c = EvaluationFixtureCatalog.Create(head.AudienceId, cast: artifact.Snapshot!.Input.Brief.Cast).Cases.First() with
        {
            Locale = head.LocaleId,
            Cast = artifact.Snapshot!.Input.Brief.Cast,
            RequiredProperties = [],
            ForbiddenProperties = [],
            ExpectedTextIds = [],
            AllowedLifeStages = cast.Select(x => x.LifeStage).Distinct().ToArray(),
            Continuity = [],
            VerifiedClaims = [Fact]
        };
        var q = s.GetRequiredService<IQualityEvaluation>(); var dataset = await q.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-body-" + Guid.NewGuid(), 1, "Fixture schedule evaluation, not human release", [c]), default);
        var candidate = await q.RegisterCandidateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m15-recorded-" + Guid.NewGuid(), 1, new("recorded.v1", "script.v1", "none.v1", "trend.v1", "fixture.v1")), default);
        var report = await q.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development", RecordedOutputs: [new(c.Key, new(packet.Current!.Body, head.LocaleId, [], false, cast, [Fact], null, [], 0, "INR"), artifact.Id)]), default);
        packet = await editorial.ActAsync(f.Owner, f.Universe, packet.Id, EditorialTestFixture.Action(packet, "Evaluate", new(report.Id, c.Key)), default);
        packet = await editorial.ActAsync(f.Owner, f.Universe, packet.Id, EditorialTestFixture.Action(packet, "Review"), default);
        packet = await editorial.ActAsync(f.Owner, f.Universe, packet.Id, EditorialTestFixture.Action(packet, "Approve"), default);
        return await editorial.ActAsync(f.Owner, f.Universe, packet.Id, new(Guid.NewGuid(), packet.RowVersion, "Schedule", "fixture:M15-scheduled-recheck", "Fixture", ScheduledUtc: DateTimeOffset.UtcNow.AddHours(1)), default);
    }

}


