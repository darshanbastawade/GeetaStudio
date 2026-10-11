using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class EditorialTestFixture
{
    internal sealed record Fixture(CoreScope Owner, Guid Universe, Guid Audience, Guid Subject, Guid Bible, Guid Episode);
    internal static ServiceProvider Services()
    {
        using var old = EvaluationTestFixture.Services(); var config = old.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(); var services = new ServiceCollection().AddLogging().AddSingleton(config);
        services.AddContentRuntime(config, true); services.AddContentCore(config); services.AddAiControlPlane(config); services.AddLessons(); services.AddUniverseRegistry(); services.AddDraftFactory(); services.AddQualityEvaluation(); services.AddEditorialWorkbench();
        services.AddSingleton<ITextGenerationProvider, FixtureTextA>(); services.AddSingleton<ITextGenerationProvider, FixtureTextB>(); services.AddSingleton<IEmbeddingProvider, ReviewedConceptEmbedding>(); services.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>();
        services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationA>(); services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationB>(); services.AddSingleton<IFactoryTemplate, LocalDraftTemplateA>(); services.AddSingleton<IFactoryTemplate, LocalDraftTemplateB>();
        services.AddSingleton<IEvaluationAdapter>(new EvaluationFixtureAdapter("evaluation.fixture.a.v1")); return services.BuildServiceProvider();
    }
    internal static async Task<Fixture> Setup(IServiceProvider s, string actor = "m13-owner")
    {
        var f = await RegistryTestFixture.Setup(s, actor); var db = s.GetRequiredService<CoreDbContext>(); var subject = Guid.NewGuid(); var pack = Guid.NewGuid(); db.Packs.Add(new() { Id = pack, UniverseId = f.Universe, Version = 1 }); db.SubjectTypes.Add(new() { PackId = pack, Key = "learning" }); db.PackAudiences.Add(new() { PackId = pack, AudienceId = f.Audience }); db.Policies.Add(new() { PackId = pack, Key = "human-review", Rule = "Fixture policy; genuine human approval still required." }); db.Subjects.Add(new() { Id = subject, UniverseId = f.Universe, Key = "m13", Type = "learning", Name = "M13 original fiction" });
        foreach (var role in new[] { "Author", "Editor", "Publisher" }) foreach (var who in new[] { "m13-" + role.ToLowerInvariant(), "integration-test-" + role }) db.Members.Add(new() { TenantId = f.Owner.TenantId, Actor = who, Permission = role });
        await db.SaveChangesAsync(); return new(f.Owner, f.Universe, f.Audience, subject, f.Bible, f.Episode);
    }
    internal static StartEditorial Request(Fixture f, string key = "fixture") => new(Guid.NewGuid(), key, "Article", "An original fictional learning brief.",
        new(Guid.NewGuid(), f.Subject, f.Audience, "en-IN", "Original fictional draft", "An original learner pauses before choosing.", "Fiction", "Owned", "fixture:owned-original"));
    internal static EditorialAction Action(EditorialPacket p, string action, EditorialEvaluationBinding? evaluation = null) => new(Guid.NewGuid(), p.RowVersion, action, "fixture:editorial-check", "Fixture", evaluation, action == "Schedule" ? DateTimeOffset.UtcNow : null);
    internal static async Task<EditorialEvaluationBinding> Report(IServiceProvider s, Fixture f, EditorialPacket p, Guid? audience = null, bool omitCast = false)
    {
        var e = s.GetRequiredService<IQualityEvaluation>(); var production = omitCast ? null : p.Episodes.FirstOrDefault();
        var c = EvaluationFixtureCatalog.Create(audience ?? f.Audience).Cases.First() with { RequiredProperties = [], ForbiddenProperties = [], ExpectedTextIds = [], Cast = production?.Brief };
        var d = await e.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "body-" + Guid.NewGuid(), 1, "Fixture check of current owned body.", [c]), default);
        var candidate = await e.RegisterCandidateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "candidate-" + Guid.NewGuid(), 1, new("recorded.v1", "generated-text.v1", "none.v1", "editorial.v1", "fixture.v1")), default);
        var cast = omitCast ? [] : p.Episodes.SelectMany(x => x.Cast).Select(x => new EvaluationCastMember(x.Character.CharacterId, x.Character.Profile.Id, x.Role,
            x.Presentation?.Rules.VoiceAnchor ?? x.Character.Profile.Rules.VoiceAnchor, x.Character.Profile.Rules.LifeStage)).Distinct().ToArray();
        var output = new EvaluationOutput(p.Current!.Body, "en-IN", [], false, cast, [], null, [], 0, "INR");
        var report = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), d.Id, candidate.Id, "Development", RecordedOutputs: [new(c.Key, output)]), default);
        return new(report.Id, c.Key);
    }
    internal static async Task<EditorialPacket> Reviewed(IServiceProvider s, Fixture f, EditorialPacket p)
    {
        var editor = s.GetRequiredService<IEditorialWorkbench>(); if (p.State == "Brief") p = await editor.ActAsync(f.Owner, f.Universe, p.Id, Action(p, "Draft"), default);
        p = await editor.ActAsync(f.Owner, f.Universe, p.Id, Action(p, "Evaluate", await Report(s, f, p)), default);
        return await editor.ActAsync(f.Owner, f.Universe, p.Id, Action(p, "Review"), default);
    }
    internal static async Task<EditorialPacket> Approved(IServiceProvider s, Fixture f, EditorialPacket p) => await s.GetRequiredService<IEditorialWorkbench>().ActAsync(f.Owner, f.Universe, p.Id, Action(await Reviewed(s, f, p), "Approve"), default);
    internal static byte[] Wav(short sample = 100)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream); w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(40); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(8000); w.Write(16000); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(4); w.Write(sample); w.Write(sample); return stream.ToArray();
    }
}
