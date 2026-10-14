using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ContentOS.Application.CustomerRules;
namespace ContentOS.Foundation.Tests;

internal static class TalkTestFixture
{
    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    internal static ServiceProvider Services(ITextGenerationProvider? replacement = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ContentOS"] = CustomerTestFixture.Connection }).Build();
        var s = new ServiceCollection().AddLogging(); s.AddSingleton<IConfiguration>(config); s.AddContentRuntime(config, true); s.AddContentCore(config); s.AddCustomerWorkbench(); s.AddTalkWorkbench();
        s.AddSingleton<IIdentityProvider, HostIdentityProvider>(); s.AddSingleton<INotificationProvider, InboxNotificationProvider>(); s.AddSingleton<ITextGenerationProvider>(replacement ?? new FixtureTextA()); s.AddSingleton<ITextGenerationProvider, FixtureTextB>(); s.AddSingleton<IAiEmbeddingAdapter, LocalAiEmbedding>(); s.AddSingleton<ILegacyEmbeddingProvider, ReviewedConceptEmbedding>(); s.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>(); s.AddAiControlPlane(config); return s.BuildServiceProvider();
    }
    internal sealed record Fixture(CustomerTestFixture.Fixture Customer, Guid Policy, Guid Snapshot, Guid ProfileA, Guid ProfileB, Guid RouteA, Guid RouteB);
    internal static async Task<Fixture> Setup(IServiceProvider s, string scenario = "success", string? actor = null)
    {
        var c = await CustomerTestFixture.Setup(s, actor); var db = s.GetRequiredService<CoreDbContext>();
        foreach (var locale in new[] { "en-IN", "sa-IN" }) if (!await db.Locales.AnyAsync(x => x.Id == locale)) db.Locales.Add(new() { Id = locale, Name = locale }); await db.SaveChangesAsync();
        var textId = await TalkCorpusFixture.FirstVerse(s, c); var text = await db.CorpusTexts.AsNoTracking().SingleAsync(x => x.Id == textId); var verse = await db.CorpusVerses.AsNoTracking().SingleAsync(x => x.Id == text.VerseId);
        var k = s.GetRequiredService<IKnowledgeAdministration>(); var concept = await k.ConceptAsync(c.Admin, c.Universe, new(Guid.NewGuid(), "fixture-battlefield", text.SnapshotId, [new("en-IN", "armies", "Alias"), new("en-IN", "battle", "Alias"), new("en-IN", "field", "Alias"), new("en-IN", "gather", "Alias")]), default); var graph = await k.GraphAsync(c.Admin, c.Universe, default); await k.ReviewAsync(c.Admin, c.Universe, concept, new(graph.Concepts.Single(x => x.Id == concept).RowVersion, "Approved", "fixture:M21-battlefield-concept-not-human-review"), default); var indexId = await k.QueueAsync(c.Admin, c.Universe, new(Guid.NewGuid(), "talk-" + Guid.NewGuid(), verse.ManifestId, "en-IN", "IN", "sql-hybrid-v1", "concept-projection-v1"), default); await s.GetRequiredService<IKnowledgeIndexWorker>().ProcessAsync(c.Admin, c.Universe, indexId, default); var index = (await k.IndicesAsync(c.Admin, c.Universe, default)).Single(x => x.Id == indexId);
        await k.ValidateAsync(c.Admin, c.Universe, indexId, new(index.RowVersion, "fixture:M21-two-M06-owner-approved-labels", [new("Where did the opposing armies gather?", text.Id), new("How do I bake a pizza?", null)]), default); index = (await k.IndicesAsync(c.Admin, c.Universe, default)).Single(x => x.Id == indexId); var alias = "talk-" + Guid.NewGuid(); await k.ActivateAsync(c.Admin, c.Universe, new(Guid.NewGuid(), alias, indexId, "", index.EmbeddingSpaceId), default);
        var audience = new AudienceProfile { Id = Guid.NewGuid(), UniverseId = c.Universe, Key = "all-fixture", Name = "Fixture ages", MinAge = 5, MaxAge = 120 }; db.Audiences.Add(audience);
        var character = new Character { Id = Guid.NewGuid(), UniverseId = c.Universe, Key = "fixture-mentor", Status = "Active" }; db.Characters.Add(character); var revision = new CharacterRevision { Id = Guid.NewGuid(), CharacterId = character.Id, Version = 1, DisplayName = "Dadaji fixture", LifeStage = "Elder", Biography = "Warm fictional mentor; controlled test only", FictionalStatus = "Fictional" }; db.CharacterRevisions.Add(revision);
        var persona = new RegistryRevision { Id = Guid.NewGuid(), TenantId = c.Tenant, UniverseId = c.Universe, Key = "fixture-mentor", Version = 1, Kind = "Character", CharacterId = character.Id, CoreRevisionId = revision.Id, ReviewStatus = "Approved", ReviewEvidence = "fixture:M21-not-human-review", RulesJson = Json(new RegistryCharacterRules("Dadaji fixture", "Elder", revision.Biography, "Fictional", ["Warm"], ["Learning"], ["Mistakes"], "Calm", ["Mentor"], [], [], [], [audience.Id], ["en-IN"], [], "Learning", "Dadaji", "none", "none", [])) }; db.RegistryRevisions.Add(persona);
        var ap = new LessonProfile { Id = Guid.NewGuid(), TenantId = c.Tenant, UniverseId = c.Universe, Key = "talk-audience", Version = 1, Kind = "Audience", ReviewStatus = "Approved", ReviewEvidence = "fixture:M21-not-human-review", RulesJson = Json(new RegisterLessonProfile(Guid.NewGuid(), "talk-audience", 1, "Audience", Audience: new(audience.Id, 5, 120, "Plain", [], 2000, "Calm", [], ["Scripture learning"], "Read", "Reflect", "Ask", "Consider meaning", new()))) }; db.LessonProfiles.Add(ap);
        var lp = new LessonProfile { Id = Guid.NewGuid(), TenantId = c.Tenant, UniverseId = c.Universe, Key = "talk-English", Version = 1, Kind = "Locale", ReviewStatus = "Approved", ReviewEvidence = "fixture:M21-not-human-review", RulesJson = Json(new RegisterLessonProfile(Guid.NewGuid(), "talk-English", 1, "Locale", Locale: new("en-IN", "en-IN", [], "Preserve", "Preserve", [], []))) }; db.LessonProfiles.Add(lp); await db.SaveChangesAsync();
        var ai = s.GetRequiredService<IAiAdministration>(); await ai.BudgetAsync(c.Admin, new("ai.text", "USD", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), 1), default); var a = await ai.RegisterAsync(c.Admin, new(SqlAiTests.Settings(scenario: scenario)), default); var b = await ai.RegisterAsync(c.Admin, new(SqlAiTests.Settings("fixture.text.b")), default);
        async Task<Guid> Route(Guid profile, string key) => await ai.RouteAsync(c.Admin, new("answer-with-evidence", key, [profile], .1m, "USD", Classification: "Internal"), default);
        var ra = await Route(a, "talk-a.v1"); var rb = await Route(b, "talk-b.v1"); await ai.ActivateAsync(c.Admin, ra, default); var policy = await s.GetRequiredService<ITalkWorkbench>().ConfigureAsync(c.Admin, new(Guid.NewGuid(), c.Universe, "talk-english", 1, persona.Id, ap.Id, lp.Id, "Adult", alias, index.EmbeddingSpaceId), default);
        await s.GetRequiredService<ICustomerWorkbench>().GrantAsync(c.Admin, new(Guid.NewGuid(), c.User, 10, DateTimeOffset.UtcNow.AddHours(1), "fixture:M21-test-credits"), default); return new(c, policy, text.SnapshotId, a, b, ra, rb);
    }
}


