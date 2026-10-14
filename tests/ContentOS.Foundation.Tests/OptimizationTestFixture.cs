using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class OptimizationTestFixture
{
    internal sealed record Fixture(CoreScope Owner, Guid Universe, Guid Audience, Guid Experiment, Guid BaselineRoute, Guid CandidateRoute, OptimizationEvaluation Evaluation, EditorialTestFixture.Fixture Editorial);
    internal static ServiceProvider Services(bool slow = false)
    {
        using var old = EditorialTestFixture.Services(); var config = old.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(); var services = new ServiceCollection().AddLogging().AddSingleton(config);
        services.AddContentRuntime(config, true); services.AddContentCore(config); services.AddAiControlPlane(config); services.AddOwnedWorkflows(); services.AddLessons(); services.AddUniverseRegistry(); services.AddDraftFactory(); services.AddQualityEvaluation(); services.AddEditorialWorkbench(); services.AddOptimization(); services.AddAnalyticsWorkbench(); services.AddSingleton<IChannelAnalyticsProvider>(new AnalyticsImportProvider("analytics.fixture.a.v1"));
        services.AddSingleton<ITextGenerationProvider, FixtureTextA>();
        if (slow) { services.AddSingleton<ControlledSlowFixtureB>(); services.AddSingleton<ITextGenerationProvider>(s => s.GetRequiredService<ControlledSlowFixtureB>()); } else services.AddSingleton<ITextGenerationProvider, FixtureTextB>();
        services.AddSingleton<IEmbeddingProvider, ReviewedConceptEmbedding>(); services.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>();
        services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationA>(); services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationB>(); services.AddSingleton<IFactoryTemplate, LocalDraftTemplateA>(); services.AddSingleton<IFactoryTemplate, LocalDraftTemplateB>();
        services.AddSingleton<IEvaluationAdapter>(new EvaluationFixtureAdapter("evaluation.fixture.a.v1")); services.AddSingleton<IEvaluationAdapter>(new EvaluationFixtureAdapter("evaluation.fixture.b.v1"));
        return services.BuildServiceProvider();
    }
    internal static OptimizationProposal Proposal(Guid audience, Guid a, Guid b, Guid? routeA = null, Guid? routeB = null, string mode = "Canary") =>
        new(Guid.NewGuid(), "experiment-" + Guid.NewGuid(), 1, "Provider", "Candidate wording may improve usefulness without increasing corrections or cost.", audience, "en-IN", "PrivateFixture", mode, a, b, routeA, routeB,
            OptimizationRules.Metrics.Select(m => new OptimizationGuardrail(m, 0, m is "CorrectionRate" or "OperationalFailure" or "EditorialRework" ? .1m : 1m, 2)).ToArray(), 2, 1, 4, .1m, "USD", DateTimeOffset.UtcNow.AddHours(1), "fixture:M27-proposal");
    internal static async Task<Fixture> Setup(IServiceProvider s, string mode = "Canary", string actor = "m27-owner", bool promptExperiment = false)
    {
        var f = await EditorialTestFixture.Setup(s, actor); var db = s.GetRequiredService<CoreDbContext>();
        var audience = Guid.NewGuid(); db.Audiences.Add(new() { Id = audience, UniverseId = f.Universe, Key = "m27-adult", Name = "Private adults", MinAge = 18, MaxAge = 120 }); await db.SaveChangesAsync();
        var admin = s.GetRequiredService<IAiAdministration>(); var routes = new List<Guid>(); var promptIds = new List<Guid>(); var promptVersions = new List<string>();
        foreach (var adapter in new[] { "fixture.text.a", "fixture.text.b" })
        {
            var promptVersion = "generated-text.v1";
            if (promptExperiment)
            {
                var p = new RegisterPrompt("m27-" + Guid.NewGuid().ToString("N"), 1, adapter == "fixture.text.a" ? "Baseline reflection: {{question}}" : "Candidate reflection: {{question}}", [new("question", 200)], "en-IN", audience);
                promptIds.Add(await s.GetRequiredService<IWorkflowRuntime>().RegisterPromptAsync(f.Owner, f.Universe, p, default)); promptVersion = p.Key + ".v1";
            }
            promptVersions.Add(promptVersion);
            var settings = new AiProviderSettings(adapter, adapter == "fixture.text.a" ? "fixture-a.v1" : "fixture-b.v1", "ai.text", "en-IN", true, false, false, 32768, 128,
                new("fixture-zero.v1", "USD", 0, 0, 0), new(["Public", "Internal"], "local", 0, false, "owned-policy.v1"), "OfflineVerified", "fixture:M27-zero-price");
            var profile = await admin.RegisterAsync(f.Owner, new(settings), default); routes.Add(await admin.RouteAsync(f.Owner, new("draft", "m27-" + Guid.NewGuid(), [profile], .01m, "USD", PromptVersion: promptVersion), default));
        }
        await admin.BudgetAsync(f.Owner, new("ai.text", "USD", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(2), .2m), default);
        // Keep the existing task head untouched: every experiment dispatch pins its own immutable route.
        var eval = s.GetRequiredService<IQualityEvaluation>(); var source = EvaluationFixtureCatalog.Create(audience).Cases.First(); var token = Guid.NewGuid().ToString();
        var cases = Enumerable.Range(0, 3).Select(n => source with { Key = "m27-" + n, Split = n == 2 ? "HeldOut" : "Development", Input = "An original private learning scene " + token + " " + n, RequiredProperties = [], ForbiddenProperties = [], ExpectedTextIds = [], AllowedLifeStages = ["Adult"] }).ToArray();
        var dataset = await eval.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m27-" + token, 1, "Independent fixture plumbing only; human learning acceptance pending.", cases), default);
        var a = await eval.RegisterCandidateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "baseline-" + token, 1, new("evaluation.fixture.a.v1", promptVersions[0], "none.v1", "m27-a.v1", "fixture.v1", AiRouteId: routes[0])), default);
        var b = await eval.RegisterCandidateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "candidate-" + token, 1, new("evaluation.fixture.b.v1", promptVersions[1], "none.v1", "m27-b.v1", "fixture.v1", AiRouteId: routes[1])), default);
        var baseline = await eval.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, a.Id, "Development"), default);
        var development = await eval.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, b.Id, "Development", baseline.Id), default);
        var heldOut = await eval.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, b.Id, "HeldOut", baseline.Id), default);
        var evidence = new OptimizationEvaluation(Guid.NewGuid(), baseline.Id, development.Id, heldOut.Id);
        var workbench = s.GetRequiredService<IOptimizationWorkbench>(); var proposal = Proposal(audience, a.Id, b.Id, routes[0], routes[1], mode);
        if (promptExperiment) proposal = proposal with { Dimension = "Prompt", BaselinePromptId = promptIds[0], CandidatePromptId = promptIds[1] };
        var id = await workbench.ProposeAsync(f.Owner, f.Universe, proposal, default);
        await workbench.EvaluateAsync(f.Owner, f.Universe, id, evidence, default);
        foreach (var metric in OptimizationRules.Metrics) await workbench.MeasureAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), metric, metric is "CorrectionRate" or "EditorialRework" or "OperationalFailure" or "Cost" ? 0 : .8m, 2, "Fixture", "fixture:measured-M27", DateTimeOffset.UtcNow), default);
        return new(f.Owner, f.Universe, audience, id, routes[0], routes[1], evidence, f);
    }
    internal static async Task<OptimizationView> Activate(IServiceProvider s, Fixture f)
    {
        var w = s.GetRequiredService<IOptimizationWorkbench>(); var view = await w.InspectAsync(f.Owner, f.Universe, f.Experiment, default);
        return await w.ControlAsync(f.Owner, f.Universe, f.Experiment, new(Guid.NewGuid(), "Activate", view.RowVersion, "fixture:authorized-private-cohort"), default);
    }
    internal static OptimizationGenerate Generate(Fixture f, string cohort = "PrivateFixture") { var id = Guid.NewGuid(); return new(id, cohort, f.Audience, new(id, "draft", "Write an original calm adult reflection.")); }
}

internal sealed class ControlledSlowFixtureB : ITextGenerationProvider
{
    private readonly FixtureTextB provider = new();
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool Delay { get; set; } = true;
    internal string? LastPrompt { get; private set; }
    public string Key => provider.Key;
    public void Validate(AiProviderSettings settings) => provider.Validate(settings);
    public async Task<AiProviderResult> GenerateAsync(AiProviderCall call, CancellationToken ct) { LastPrompt = call.Prompt; Started.TrySetResult(); if (Delay) await Task.Delay(5000, ct); return await provider.GenerateAsync(call, ct); }
    public IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall call, CancellationToken ct) => provider.StreamAsync(call, ct);
}
