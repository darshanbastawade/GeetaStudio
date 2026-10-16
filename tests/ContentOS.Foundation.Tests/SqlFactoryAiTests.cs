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

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed partial class SqlFactoryAiTests
{
    private sealed class Calls
    {
        public string Scenario = "success";
        public Guid Character;
        public int Count;
        public bool SawTransaction;
        public CancellationTokenSource? StopAfterAi;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class Provider(Calls state, CoreDbContext db) : ITextGenerationProvider
    {
        public string Key => "fixture.text.a";
        public void Validate(AiProviderSettings settings) => new FixtureTextA().Validate(settings);
        public async Task<AiProviderResult> GenerateAsync(AiProviderCall call, CancellationToken ct)
        {
            state.Count++; state.SawTransaction |= db.Database.CurrentTransaction != null; state.Entered.TrySetResult();
            if (state.Scenario == "block")
            {
                try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { state.Cancelled.TrySetResult(); throw; }
            }
            var script = new FactoryGeneratedScript("factory-script.v1", "A shared school project", state.Scenario == "long" ? string.Join(" ", Enumerable.Repeat("The original learner records an observation before making a calm choice.", 24)) : "An original learner notices a missing observation.", [new(state.Character, "Let us compare what each person actually saw.")], "Ask a calm question before deciding.", "Which observation could change the decision?");
            var text = state.Scenario == "inner-schema" ? "{}" : JsonSerializer.Serialize(script, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var body = JsonSerializer.Serialize(new GeneratedText(text, state.Scenario == "citation" ? [Guid.NewGuid().ToString()] : call.SourceIds), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new("Success", body, state.Scenario == "unknown" ? null : new(200, 0, 100), "fixture:factory-" + call.AttemptId);
        }
        public IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall call, CancellationToken ct) => throw new NotSupportedException();
    }
    private static ServiceProvider Services(Calls state, Action<IServiceCollection>? configure = null) => FactoryTestFixture.Services(s =>
    {
        var config = (IConfiguration)s.Last(x => x.ServiceType == typeof(IConfiguration)).ImplementationInstance!;
        s.AddAiControlPlane(config); s.AddOwnedWorkflows(); s.AddQualityEvaluation(); s.AddEditorialWorkbench();
        s.AddSingleton<IEmbeddingProvider, ReviewedConceptEmbedding>(); s.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>();
        s.AddSingleton<IEvaluationAdapter>(new EvaluationFixtureAdapter("evaluation.fixture.a.v1"));
        s.AddScoped<ITextGenerationProvider>(sp => new Provider(state, sp.GetRequiredService<CoreDbContext>()));
        s.AddSingleton<ITextGenerationProvider, FixtureTextB>();
        s.AddScoped<IToolInvoker>(sp => new InterruptTools(new WorkflowTools(sp.GetRequiredService<CoreDbContext>(), sp.GetRequiredService<IKnowledgeRetriever>(), sp.GetRequiredService<IAiTaskRouter>()), state));
        s.AddTrendWorkbench(); s.AddSingleton<ITrendSource, FixtureTrendSourceA>(); s.AddSingleton<ITrendExtractor, OwnedTrendExtractorA>(); configure?.Invoke(s);
    });
    private sealed class InterruptTools(IToolInvoker inner, Calls state) : IToolInvoker
    {
        public IReadOnlyList<WorkflowTool> Tools => inner.Tools;
        public Task<WorkflowEvidence> RetrieveAsync(CoreScope s, Guid u, StartWorkflow r, string locale, CancellationToken ct) => inner.RetrieveAsync(s, u, r, locale, ct);
        public async Task<AiRunView> GenerateAsync(CoreScope s, Guid u, GenerateAi r, CancellationToken ct)
        {
            var result = await inner.GenerateAsync(s, u, r, ct);
            if (state.StopAfterAi != null) { state.StopAfterAi.Cancel(); throw new OperationCanceledException(ct); }
            return result;
        }
    }
    private sealed record Fixture(FactoryTestFixture.Fixture Factory, FactoryPlanView Plan, Guid Definition, Guid Route);
    private static async Task<Fixture> Setup(IServiceProvider s, Calls calls, bool aiRights = true, decimal tenantBudget = 10)
    {
        var f = await FactoryTestFixture.Setup(s, "factory-ai-" + Guid.NewGuid(), aiRights); calls.Character = f.Character;
        var workflow = s.GetRequiredService<IWorkflowRuntime>(); var ai = s.GetRequiredService<IAiAdministration>();
        var prompt = await workflow.RegisterPromptAsync(f.Owner, f.Universe, new("factory-script", 1, "Write an original fictional script. Brief {{brief}} Audience {{audience}} Cast {{cast}} Lesson {{lesson}} Format {{format}}", new[] { "brief", "audience", "cast", "lesson", "format" }.Select(x => new PromptInput(x, 2000)).ToArray(), "en-IN", f.Audience, RequireEvidence: true), default);
        var profile = await ai.RegisterAsync(f.Owner, new(SqlAiTests.Settings(streaming: false)), default);
        var route = await ai.RouteAsync(f.Owner, new("draft", "factory.v1", [profile], .1m, "USD", DeadlineSeconds: 20, PromptVersion: "factory-script.v1"), default);
        await ai.ActivateAsync(f.Owner, route, default); await ai.BudgetAsync(f.Owner, new("ai.text", "USD", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), tenantBudget), default);
        var definition = await workflow.RegisterDefinitionAsync(f.Owner, f.Universe, new("factory-script", 1, prompt, route, WorkflowRules.Sequence.Select(x => new WorkflowStepDefinition(x, x == "draft" ? "ai.generate.v1" : x == "retrieve" ? "evidence.retrieve.v1" : null)).ToArray(), .1m), default);
        var r = FactoryTestFixture.Request(f, second: false); r = r with { Adapter = "ai-workflow.v1", WorkflowDefinitionId = definition, Briefs = r.Briefs.Select(b => b with { Formats = [new("Story", "A respectful original scene with a practical choice.", 16000)] }).ToArray() };
        return new(f, await FactoryTestFixture.Approved(s, f, r), definition, route);
    }
    private static StartFactoryBatch Request(Fixture f, decimal limit = .1m) => new(Guid.NewGuid(), "ai-" + Guid.NewGuid(), f.Plan.Id, new(limit, "USD", 16000, 600, 1));
    private static async Task<FactoryBatchView> Batch(IServiceProvider s, Fixture f, Guid id) => (await s.GetRequiredService<IDraftFactory>().BatchesAsync(f.Factory.Owner, f.Factory.Universe, default)).Single(x => x.Id == id);
    private static async Task<FactoryBatchView> Queue(ServiceProvider root, Fixture f, StartFactoryBatch request)
    {
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var factory = s.GetRequiredService<IDraftFactory>(); var batch = await factory.StartAsync(f.Factory.Owner, f.Factory.Universe, request, default);
        for (var i = 0; i < 100; i++) { batch = await Batch(s, f, batch.Id); if (batch.Work.Single().WorkflowId != null || batch.State == "CompletedWithIssues") return batch; await factory.ProcessNextAsync(default); }
        throw new Exception("Factory queue did not reach its own checkpoint.");
    }
    private static async Task<DurableWorkflowView> Advance(ServiceProvider root, Fixture f, Guid id, int step = 5)
    {
        for (var i = 0; i < 100; i++)
        {
            using var scope = root.CreateScope(); var s = scope.ServiceProvider; var view = await s.GetRequiredService<IWorkflowRuntime>().InspectAsync(f.Factory.Owner, id, default);
            if (view.NextStep >= step || view.State is "Blocked" or "Failed" or "Cancelled" or "AwaitingReview") return view;
            await s.GetRequiredService<IWorkflowWorker>().ProcessNextAsync(default);
        }
        throw new Exception("Workflow did not reach its own checkpoint.");
    }
    private static async Task DrainFactory(ServiceProvider root, Fixture f, Guid id)
    {
        for (var i = 0; i < 100; i++)
        {
            using var scope = root.CreateScope(); var s = scope.ServiceProvider; await s.GetRequiredService<IDraftFactory>().ProcessNextAsync(default);
            var b = await Batch(s, f, id); var db = s.GetRequiredService<CoreDbContext>();
            if (b.State == "CompletedWithIssues" || b.State == "Completed" && await db.EditorialItems.AnyAsync(e => db.FactoryArtifacts.Any(a => a.CoreContentId == e.ContentId && b.Work.Select(w => w.ArtifactId).Contains(a.Id)))) return;
        }
        throw new Exception("Factory did not complete/import its own draft.");
    }
    [SqlFact]
    public async Task Original_workflow_recovers_once_and_enters_editorial_with_canonical_cast_and_paid_usage()
    {
        var calls = new Calls { Scenario = "long" }; using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var request = Request(f);
        var queued = await Queue(root, f, request); var work = Assert.Single(queued.Work); Assert.NotNull(work.WorkflowId);
        Assert.Equal(queued.Id, (await s.GetRequiredService<IDraftFactory>().StartAsync(f.Factory.Owner, f.Factory.Universe, request, default)).Id);
        var workflow = await Advance(root, f, work.WorkflowId!.Value); Assert.Equal("AwaitingReview", workflow.State);
        await DrainFactory(root, f, queued.Id); // Fresh worker scopes after the paid checkpoint simulate restart.
        var done = await Batch(s, f, queued.Id); Assert.Equal("Completed", done.State); Assert.Equal(1, calls.Count); Assert.False(calls.SawTransaction); Assert.True(done.GenerationCost > 0); Assert.Equal(0, done.GenerationReserved);
        var artifact = Assert.Single(await s.GetRequiredService<IDraftFactory>().ArtifactsAsync(f.Factory.Owner, f.Factory.Universe, default), x => x.Id == done.Work.Single().ArtifactId);
        Assert.Equal(work.WorkflowId, artifact.Snapshot!.Generation!.WorkflowId); Assert.Equal("factory-script.v1", artifact.Snapshot.Generation.Schema);
        Assert.True(artifact.Snapshot.Output.OriginalTreatment.Length > 2000);
        Assert.Contains("Generated original fiction", artifact.Snapshot.Output.Body);
        var db = s.GetRequiredService<CoreDbContext>(); var item = await db.EditorialItems.AsNoTracking().SingleAsync(x => x.ContentId == artifact.CoreContentId);
        var packet = await s.GetRequiredService<IEditorialWorkbench>().InspectAsync(f.Factory.Owner, f.Factory.Universe, item.Id, default);
        Assert.Equal("Draft", packet.State); Assert.Empty(packet.Approvals); Assert.False(packet.PublicIntended); Assert.Single(packet.ProductionRuns); Assert.Equal(artifact.Snapshot.Generation.AiRunId, packet.ProductionRuns[0].Id); Assert.NotEmpty(packet.Claims);
        Assert.Equal(f.Factory.Character, packet.FactoryHistory.Single().Input.Cast.Beats.Single().SpeakerCharacterId);
        Assert.Equal(2, packet.Claims.Length); Assert.NotEmpty(packet.Citations);
        var links = await db.ClaimLinks.AsNoTracking().Where(x => x.ContentRevisionId == artifact.CoreRevisionId).OrderBy(x => x.Start).ToArrayAsync();
        Assert.Equal(3, links.Length); var end = 0;
        foreach (var link in links)
        {
            Assert.True(artifact.Snapshot.Output.Body[end..link.Start].All(char.IsWhiteSpace));
            var claim = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == link.ClaimRevisionId);
            Assert.Equal(claim.Statement, artifact.Snapshot.Output.Body.Substring(link.Start, link.Length)); end = link.Start + link.Length;
        }
        Assert.Equal(artifact.Snapshot.Output.Body.Length, end);
        var fiction = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == artifact.Snapshot.Generation.FictionClaimRevisionId);
        Assert.Equal("Pending", fiction.ReviewStatus); Assert.Null(fiction.Reviewer);
        var gate = await s.GetRequiredService<IEvidenceEngine>().CheckAsync(f.Factory.Owner, f.Factory.Universe, artifact.CoreRevisionId, new("Read", "en-IN", "IN", "Text"), default);
        Assert.False(gate.Eligible); Assert.Equal("InsufficientEvidence", gate.Reason);
        await s.GetRequiredService<IEvidenceEngine>().ReviewAsync(f.Factory.Owner, f.Factory.Universe, fiction.Id, new(Convert.ToBase64String(fiction.RowVersion), "Approved", "Attributed", "Supported", "fixture:controlled-fiction-review"), default);
        gate = await s.GetRequiredService<IEvidenceEngine>().CheckAsync(f.Factory.Owner, f.Factory.Universe, artifact.CoreRevisionId, new("Read", "en-IN", "IN", "Text"), default);
        Assert.True(gate.Eligible, gate.Reason); Assert.Equal(3, gate.Labels.Count);
        await Assert.ThrowsAsync<CoreAccessException>(() => s.GetRequiredService<IDraftFactory>().BatchesAsync(new(Guid.NewGuid(), f.Factory.Owner.Actor), f.Factory.Universe, default));
        await s.GetRequiredService<IDraftFactory>().ProcessNextAsync(default); Assert.Equal(1, calls.Count);
    }
    [SqlFact]
    public async Task Aggregate_allocations_reject_insufficient_batch_before_any_child_or_paid_operation()
    {
        var calls = new Calls(); using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => s.GetRequiredService<IDraftFactory>().StartAsync(f.Factory.Owner, f.Factory.Universe, Request(f, .01m), default));
        Assert.Equal(0, calls.Count); Assert.False(await s.GetRequiredService<CoreDbContext>().DurableWorkflows.AnyAsync(x => x.TenantId == f.Factory.Owner.TenantId));
    }
    [SqlFact]
    public async Task Stale_factory_lease_cannot_bind_a_child_but_recovery_uses_one_original_work_identity()
    {
        var calls = new Calls(); using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var db = s.GetRequiredService<CoreDbContext>();
        var b = await s.GetRequiredService<IDraftFactory>().StartAsync(f.Factory.Owner, f.Factory.Universe, Request(f), default); var work = await db.FactoryWorks.SingleAsync(x => x.BatchId == b.Id);
        var stale = Guid.NewGuid(); work.State = "Running"; work.LeaseToken = stale; work.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        var request = new StartWorkflow(work.Id, f.Definition, new[] { "brief", "audience", "cast", "lesson", "format" }.ToDictionary(x => x, x => "controlled stale proof"), f.Plan.Snapshot!.Briefs[0].Lesson.Snapshot.Bundle.SourceIds) { FactoryAdmission = new(work.Id, stale) };
        await Assert.ThrowsAsync<RuntimeValidationException>(() => s.GetRequiredService<IWorkflowRuntime>().StartAsync(f.Factory.Owner, f.Factory.Universe, request, default));
        Assert.False(await db.DurableWorkflows.AnyAsync(x => x.OperationId == work.Id));
        using var resumed = root.CreateScope(); var factory = resumed.ServiceProvider.GetRequiredService<IDraftFactory>();
        for (var n = 0; n < 100 && !(await Batch(resumed.ServiceProvider, f, b.Id)).Work.Single().WorkflowId.HasValue; n++) await factory.ProcessNextAsync(default);
        b = await Batch(resumed.ServiceProvider, f, b.Id); Assert.NotNull(b.Work.Single().WorkflowId); Assert.Equal(1, await db.DurableWorkflows.CountAsync(x => x.OperationId == work.Id)); Assert.Equal(0, calls.Count);
        await factory.ControlAsync(f.Factory.Owner, f.Factory.Universe, b.Id, new(Guid.NewGuid(), b.RowVersion, "Cancel"), default);
    }
    [SqlFact]
    public async Task A_crash_after_paid_completion_before_checkpoint_acceptance_recovers_the_original_ai_run()
    {
        var calls = new Calls(); using var stop = new CancellationTokenSource(); using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var b = await Queue(root, f, Request(f)); var id = b.Work.Single().WorkflowId!.Value;
        await Advance(root, f, id, 2); calls.StopAfterAi = stop;
        using (var worker = root.CreateScope())
            for (var n = 0; n < 100 && !stop.IsCancellationRequested; n++) await worker.ServiceProvider.GetRequiredService<IWorkflowWorker>().ProcessNextAsync(stop.Token);
        Assert.True(stop.IsCancellationRequested); Assert.Equal(1, calls.Count);
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var draft = await db.WorkflowCheckpoints.SingleAsync(x => x.RunId == id && x.Kind == "draft");
        Assert.Equal("Running", draft.State); var paid = await db.AiRuns.AsNoTracking().SingleAsync(x => x.OperationId == draft.OperationId); Assert.Equal("Completed", paid.State);
        draft.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); calls.StopAfterAi = null;
        Assert.Equal("AwaitingReview", (await Advance(root, f, id)).State); await DrainFactory(root, f, b.Id);
        Assert.Equal(1, calls.Count); Assert.Equal(paid.Id, (await db.WorkflowCheckpoints.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).AiRunId);
    }
    [SqlFact]
    public async Task Exhausted_shared_M7_budget_blocks_even_a_sufficient_factory_allocation()
    {
        var calls = new Calls(); using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls, tenantBudget: .00001m); var b = await Queue(root, f, Request(f));
        var result = await Advance(root, f, b.Work.Single().WorkflowId!.Value); Assert.Equal("Failed", result.State); Assert.Equal("QuotaExhausted", result.Outcome); Assert.Equal(0, calls.Count);
    }
    [SqlFact]
    public async Task Cancellation_before_dispatch_commits_child_cancellation_without_generation()
    {
        var calls = new Calls(); using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var b = await Queue(root, f, Request(f));
        await s.GetRequiredService<IDraftFactory>().ControlAsync(f.Factory.Owner, f.Factory.Universe, b.Id, new(Guid.NewGuid(), b.RowVersion, "Cancel"), default);
        var w = await s.GetRequiredService<IWorkflowRuntime>().InspectAsync(f.Factory.Owner, b.Work.Single().WorkflowId!.Value, default);
        Assert.Equal("Cancelled", w.State); Assert.Equal(0, calls.Count); Assert.Empty(await s.GetRequiredService<IAiTaskRouter>().RunsAsync(f.Factory.Owner, default));
    }
    [SqlFact]
    public async Task Unknown_usage_stays_bound_reserved_and_never_gets_a_second_generation()
    {
        var calls = new Calls { Scenario = "unknown" }; using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var request = Request(f); var b = await Queue(root, f, request);
        var w = await Advance(root, f, b.Work.Single().WorkflowId!.Value); Assert.Equal("Blocked", w.State); Assert.Equal("NeedsReconciliation", w.Outcome);
        Assert.True((await Batch(s, f, b.Id)).GenerationReserved > 0); Assert.Equal(1, calls.Count);
        Assert.Equal(b.Id, (await s.GetRequiredService<IDraftFactory>().StartAsync(f.Factory.Owner, f.Factory.Universe, request, default)).Id);
        await s.GetRequiredService<IDraftFactory>().ProcessNextAsync(default); Assert.Equal(1, calls.Count); Assert.Single(await s.GetRequiredService<IAiTaskRouter>().RunsAsync(f.Factory.Owner, default));
    }
    [SqlFact]
    public async Task Invalid_inner_schema_and_fabricated_citations_create_no_factory_artifact()
    {
        foreach (var scenario in new[] { "inner-schema", "citation" })
        {
            var calls = new Calls { Scenario = scenario }; using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var b = await Queue(root, f, Request(f));
            await Advance(root, f, b.Work.Single().WorkflowId!.Value); await DrainFactory(root, f, b.Id);
            Assert.Equal("CompletedWithIssues", (await Batch(s, f, b.Id)).State); Assert.Empty(await s.GetRequiredService<IDraftFactory>().ArtifactsAsync(f.Factory.Owner, f.Factory.Universe, default)); Assert.Equal(1, calls.Count);
        }
    }
    [SqlFact]
    public async Task Current_source_and_cast_withdrawal_fence_the_child_before_paid_admission()
    {
        foreach (var source in new[] { true, false })
        {
            var calls = new Calls(); using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var b = await Queue(root, f, Request(f)); var id = b.Work.Single().WorkflowId!.Value;
            await Advance(root, f, id, 2);
            if (source)
            {
                var registry = s.GetRequiredService<ISourceRegistry>(); var detail = await registry.InspectAsync(f.Factory.Owner, f.Factory.Universe, f.Factory.SourceWork, default);
                await registry.RevokeAsync(f.Factory.Owner, f.Factory.Universe, detail.Work.Id, new(Guid.NewGuid(), null, "Fixture source withdrawal", detail.Work.RowVersion), default);
            }
            else
            {
                var registry = s.GetRequiredService<IUniverseRegistry>(); var c = (await registry.CharactersAsync(f.Factory.Owner, f.Factory.Universe, default)).Single(x => x.CharacterId == f.Factory.Character);
                await registry.ControlCharacterAsync(f.Factory.Owner, f.Factory.Universe, c.CharacterId, new(Guid.NewGuid(), c.RowVersion, "Retire", "fixture:withdraw-before-paid-call"), default);
            }
            Assert.Equal("Failed", (await Advance(root, f, id)).State); Assert.Equal(0, calls.Count); Assert.Empty(await s.GetRequiredService<IAiTaskRouter>().RunsAsync(f.Factory.Owner, default));
        }
    }
    [SqlFact]
    public async Task Denied_ai_permission_is_not_inferred_from_read_or_adaptation_permission()
    {
        var calls = new Calls(); using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls, false);
        var b = await Queue(root, f, Request(f)); Assert.Equal("CompletedWithIssues", b.State); Assert.Null(b.Work.Single().WorkflowId); Assert.Equal(0, calls.Count);
    }
    [SqlFact]
    public async Task Cancellation_reaches_an_admitted_provider_and_preserves_unknown_charges()
    {
        var calls = new Calls { Scenario = "block" }; using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var b = await Queue(root, f, Request(f)); var id = b.Work.Single().WorkflowId!.Value; await Advance(root, f, id, 2);
        var running = Advance(root, f, id); await calls.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        b = await Batch(s, f, b.Id); await s.GetRequiredService<IDraftFactory>().ControlAsync(f.Factory.Owner, f.Factory.Universe, b.Id, new(Guid.NewGuid(), b.RowVersion, "Cancel"), default);
        await calls.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30)); await running.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal("Cancelled", (await s.GetRequiredService<IWorkflowRuntime>().InspectAsync(f.Factory.Owner, id, default)).State); Assert.True((await Batch(s, f, b.Id)).GenerationReserved > 0); Assert.Equal(1, calls.Count);
    }
    [SqlFact]
    public async Task Route_changes_require_a_new_reviewed_plan_and_old_work_keeps_its_snapshot()
    {
        var calls = new Calls(); using var root = Services(calls); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var old = await Queue(root, f, Request(f));
        var ai = s.GetRequiredService<IAiAdministration>(); var profile = await ai.RegisterAsync(f.Factory.Owner, new(SqlAiTests.Settings("fixture.text.b", streaming: false)), default);
        var route = await ai.RouteAsync(f.Factory.Owner, new("draft", "factory.v2", [profile], .1m, "USD", DeadlineSeconds: 20, PromptVersion: "factory-script.v1"), default); await ai.ActivateAsync(f.Factory.Owner, route, default);
        var runtime = s.GetRequiredService<IWorkflowRuntime>(); var definition = (await runtime.DefinitionsAsync(f.Factory.Owner, f.Factory.Universe, default)).Single(x => x.Id == f.Definition);
        var next = await runtime.RegisterDefinitionAsync(f.Factory.Owner, f.Factory.Universe, definition.Settings with { Version = 2, RouteId = route }, default);
        var request = f.Plan.Snapshot!.Rules with { OperationId = Guid.NewGuid(), Version = 2, WorkflowDefinitionId = next };
        var plan = await FactoryTestFixture.Approved(s, f.Factory, request); var updated = f with { Plan = plan }; var fresh = await Queue(root, updated, Request(updated));
        Assert.Equal(f.Route, (await runtime.InspectAsync(f.Factory.Owner, old.Work.Single().WorkflowId!.Value, default)).Snapshot.Route.Id);
        Assert.Equal(route, (await runtime.InspectAsync(f.Factory.Owner, fresh.Work.Single().WorkflowId!.Value, default)).Snapshot.Route.Id);
        foreach (var batch in new[] { old, fresh }) await s.GetRequiredService<IDraftFactory>().ControlAsync(f.Factory.Owner, f.Factory.Universe, batch.Id, new(Guid.NewGuid(), (await Batch(s, f, batch.Id)).RowVersion, "Cancel"), default);
    }
}
