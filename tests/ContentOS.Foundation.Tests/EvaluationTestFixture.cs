using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class EvaluationTestFixture
{
    internal static ServiceProvider Services()
    {
        using var baseServices = FactoryTestFixture.Services(); var config = baseServices.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var services = new ServiceCollection().AddLogging().AddSingleton(config); services.AddContentRuntime(config, true); services.AddContentCore(config);
        services.AddAiControlPlane(config); services.AddSingleton<ITextGenerationProvider, FixtureTextA>(); services.AddSingleton<ITextGenerationProvider, FixtureTextB>(); services.AddLessons(); services.AddUniverseRegistry(); services.AddDraftFactory(); services.AddQualityEvaluation(); services.AddSingleton<IEmbeddingProvider, ReviewedConceptEmbedding>(); services.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>(); services.AddSingleton<IKnowledgeRankingAdapter, IndependentFixtureRanking>();
        services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationA>(); services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationB>();
        services.AddSingleton<IFactoryTemplate, LocalDraftTemplateA>(); services.AddSingleton<IFactoryTemplate, LocalDraftTemplateB>();
        foreach (var key in new[] { "evaluation.fixture.a.v1", "evaluation.fixture.b.v1", "evaluation.fabricated.v1", "evaluation.unsafe.v1", "evaluation.fluent-failure.v1" }) services.AddSingleton<IEvaluationAdapter>(new EvaluationFixtureAdapter(key));
        return services.BuildServiceProvider();
    }
    internal static RegisterEvaluationCandidate Candidate(string key = "candidate", string adapter = "evaluation.fixture.a.v1") =>
        new(Guid.NewGuid(), key, 1, new(adapter, "prompt.v1", "retrieval.v1", key + ".v1", "release.v1"));
    internal static EvaluationHumanReview Review(Guid dataset, string key, Guid? run = null, string mode = "Fixture") =>
        new(Guid.NewGuid(), dataset, key, run, mode, 4, 4, 4, 4, 4, 4, true, "fixture:behavior-not-human", "rubric.v1", "Controlled fixture only; no human qualification.");
}
