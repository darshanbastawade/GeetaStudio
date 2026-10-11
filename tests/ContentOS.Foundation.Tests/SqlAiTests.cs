using System.Net;
using System.Net.Http.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlAiTests
{
    private const string Connection = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static ServiceProvider Services(ITextGenerationProvider? replacement = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ContentOS"] = Connection }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddSingleton<IConfiguration>(config); services.AddContentRuntime(config, true); services.AddContentCore(config);
        services.AddSingleton<ITextGenerationProvider>(replacement ?? new FixtureTextA()); services.AddSingleton<ITextGenerationProvider, FixtureTextB>();
        services.AddSingleton<IAiEmbeddingAdapter, LocalAiEmbedding>(); services.AddSingleton<ILegacyEmbeddingProvider, ReviewedConceptEmbedding>();
        services.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>(); services.AddAiControlPlane(config); return services.BuildServiceProvider();
    }
    private sealed record Fixture(CoreScope Owner, Guid Universe);
    private static async Task<Fixture> Setup(IServiceProvider s)
    {
        var db = s.GetRequiredService<CoreDbContext>(); var org = Guid.NewGuid(); var tenant = Guid.NewGuid(); var universe = Guid.NewGuid();
        db.Organizations.Add(new() { Id = org, Name = "M07 isolated fixture" }); db.Tenants.Add(new() { Id = tenant, OrganizationId = org, Key = "m07-" + tenant, Name = "M07 test" });
        db.Universes.Add(new() { Id = universe, TenantId = tenant, Key = "original", Name = "Original test fiction" });
        db.Members.Add(new() { TenantId = tenant, Actor = "m07-owner", Permission = "Administrator" });
        db.Members.Add(new() { TenantId = tenant, Actor = "m07-reader", Permission = "Reader" });
        foreach (var role in new[] { "Administrator", "Reader" }) db.Members.Add(new() { TenantId = tenant, Actor = "integration-test-" + role, Permission = role });
        if (!await db.Locales.AnyAsync(x => x.Id == "en-IN")) db.Locales.Add(new() { Id = "en-IN", Name = "English" });
        await db.SaveChangesAsync(); return new(new(tenant, "m07-owner"), universe);
    }
    internal static AiProviderSettings Settings(string adapter = "fixture.text.a", string scenario = "success", bool streaming = true) =>
        new(adapter, adapter == "fixture.text.b" ? "fixture-b.v1" : "fixture-a.v1", "ai.text", "en-IN", true, streaming, false, 32768, 128,
            new("fixture-rates.v1", "USD", 1m, .25m, 8m), new(["Public", "Internal"], "local", 0, false, "owned-policy.v1"), "OfflineVerified", "fixture:M07-original-fiction", scenario);
    private static GenerateAi Request(string task = "draft") => new(Guid.NewGuid(), task, "Write an original calm conversation.");
    private static async Task<Guid> Budget(IServiceProvider s, Fixture f, decimal limit = 1, string capability = "ai.text") =>
        await s.GetRequiredService<IAiAdministration>().BudgetAsync(f.Owner, new(capability, "USD", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), limit), Ct);
    private static async Task<Guid> Route(IServiceProvider s, Fixture f, Guid[] profiles, string task = "draft", bool streaming = true)
    {
        var admin = s.GetRequiredService<IAiAdministration>(); var id = await admin.RouteAsync(f.Owner, new(task, "task.v1", profiles, .1m, "USD", Streaming: streaming), Ct);
        await admin.ActivateAsync(f.Owner, id, Ct); return id;
    }
    [SqlFact]
    public async Task Route_switch_replay_snapshots_usage_cache_and_rollback_are_durable()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var a = s.GetRequiredService<IAiAdministration>(); var router = s.GetRequiredService<IAiTaskRouter>();
        await Budget(s, f); var first = await a.RegisterAsync(f.Owner, new(Settings(scenario: "cache")), Ct); var second = await a.RegisterAsync(f.Owner, new(Settings("fixture.text.b")), Ct);
        var routeA = await Route(s, f, [first]); var request = Request(); var old = await router.GenerateAsync(f.Owner, f.Universe, request, Ct);
        Assert.Equal("Success", old.Outcome); Assert.StartsWith("Fixture A", old.Output!.Text); Assert.Equal(30, old.Attempts.Single().Usage!.CachedInputTokens);
        var legacyJson = System.Text.Json.JsonSerializer.Serialize(new { universe = f.Universe, r = new { request.OperationId, request.Task, request.Prompt, request.Locale, request.Classification, request.Region, request.MaxRetentionDays, request.AllowTraining, request.Structured, request.Streaming, request.Tools, request.SourceSnapshotIds, request.EvidenceQuery } }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var legacyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacyJson)));
        Assert.Equal(legacyHash, old.Snapshot.RequestHash);
        Assert.Equal(.0001775m, old.Spent); Assert.Equal(0, old.Reserved);
        var routeB = await Route(s, f, [second]); var newer = await router.GenerateAsync(f.Owner, f.Universe, Request(), Ct); Assert.StartsWith("Fixture B", newer.Output!.Text);
        Assert.Equal(routeB, newer.Snapshot.RouteId); Assert.Equal(routeA, (await router.InspectAsync(f.Owner, old.Id, Ct)).Snapshot.RouteId);
        Assert.Equal(old.Id, (await router.GenerateAsync(f.Owner, f.Universe, request, Ct)).Id);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => router.GenerateAsync(f.Owner, f.Universe, request with { Prompt = "changed" }, Ct));
        await a.ActivateAsync(f.Owner, routeA, Ct); Assert.Equal(routeA, (await router.GenerateAsync(f.Owner, f.Universe, Request(), Ct)).Snapshot.RouteId);
        using var fresh = root.CreateScope(); var persisted = await fresh.ServiceProvider.GetRequiredService<IAiTaskRouter>().InspectAsync(f.Owner, old.Id, Ct); Assert.Equal(old.Spent, persisted.Spent);
        var db = fresh.ServiceProvider.GetRequiredService<CoreDbContext>(); var row = await db.AiProfiles.SingleAsync(x => x.Id == first); row.SettingsJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task Charged_schema_failure_falls_back_but_refusal_and_quota_do_not()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var a = s.GetRequiredService<IAiAdministration>(); var router = s.GetRequiredService<IAiTaskRouter>(); await Budget(s, f);
        var b = await a.RegisterAsync(f.Owner, new(Settings("fixture.text.b")), Ct);
        foreach (var scenario in new[] { "schema", "refusal", "quota", "transient" })
        {
            var id = await a.RegisterAsync(f.Owner, new(Settings(scenario: scenario)), Ct); await Route(s, f, [id, b]);
            var run = await router.GenerateAsync(f.Owner, f.Universe, Request(), Ct);
            var fallsBack = scenario is "schema" or "transient"; Assert.Equal(fallsBack ? 2 : 1, run.Attempts.Count);
            Assert.Equal(fallsBack ? "Success" : scenario == "refusal" ? "PolicyRejected" : "QuotaExhausted", run.Outcome);
            Assert.Equal(run.Attempts.Sum(x => x.Cost), run.Spent); Assert.Equal(0, run.Reserved);
            if (fallsBack) Assert.True(run.Spent > run.Attempts[1].Cost); else Assert.Null(run.Output);
        }
    }
    [SqlFact]
    public async Task Unknown_usage_holds_full_attempt_ceiling_and_reconciliation_is_idempotent()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var a = s.GetRequiredService<IAiAdministration>(); var router = s.GetRequiredService<IAiTaskRouter>(); await Budget(s, f);
        var id = await a.RegisterAsync(f.Owner, new(Settings(scenario: "unknown")), Ct); var b = await a.RegisterAsync(f.Owner, new(Settings("fixture.text.b")), Ct); await Route(s, f, [id, b]);
        var run = await router.GenerateAsync(f.Owner, f.Universe, Request(), Ct); var attempt = Assert.Single(run.Attempts);
        Assert.Equal("UsageUnresolved", run.State); Assert.Null(attempt.Usage); Assert.Equal(attempt.Ceiling, run.Reserved); Assert.True(run.Reserved > 0); Assert.Equal(0, run.Spent);
        var evidence = new AiReconcile(new(20, 10, 5), "fixture:authoritative-usage-ledger"); await a.ReconcileAsync(f.Owner, run.Id, attempt.Id, evidence, Ct); await a.ReconcileAsync(f.Owner, run.Id, attempt.Id, evidence, Ct);
        var reconciled = await router.InspectAsync(f.Owner, run.Id, Ct); Assert.Equal("UsageReconciled", reconciled.Outcome); Assert.Equal(0, reconciled.Reserved); Assert.Equal(.0000525m, reconciled.Spent);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => a.ReconcileAsync(f.Owner, run.Id, attempt.Id, evidence with { Usage = new(1, 0, 1) }, Ct));
    }
    private sealed class GateText : ITextGenerationProvider
    {
        public string Key => "fixture.text.a"; public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Validate(AiProviderSettings s) => new FixtureTextA().Validate(s);
        public async Task<AiProviderResult> GenerateAsync(AiProviderCall c, CancellationToken ct) { Started.TrySetResult(); await Release.Task.WaitAsync(ct); return await new FixtureTextA().GenerateAsync(c, ct); }
        public IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall c, CancellationToken ct) => new FixtureTextA().StreamAsync(c, ct);
    }
    [SqlFact]
    public async Task Concurrent_runs_cannot_oversubscribe_shared_reservations()
    {
        var gate = new GateText(); using var root = Services(gate); Fixture f;
        using (var scope = root.CreateScope()) { var s = scope.ServiceProvider; f = await Setup(s); await Budget(s, f, .003m); var id = await s.GetRequiredService<IAiAdministration>().RegisterAsync(f.Owner, new(Settings()), Ct); await Route(s, f, [id]); }
        using var first = root.CreateScope(); using var second = root.CreateScope();
        var pending = first.ServiceProvider.GetRequiredService<IAiTaskRouter>().GenerateAsync(f.Owner, f.Universe, Request(), Ct);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try { var rejected = await Assert.ThrowsAsync<RuntimeValidationException>(() => second.ServiceProvider.GetRequiredService<IAiTaskRouter>().GenerateAsync(f.Owner, f.Universe, Request(), Ct)); Assert.Equal("QuotaExhausted", rejected.Outcome); }
        finally { gate.Release.TrySetResult(); }
        var run = await pending; Assert.Equal("Success", run.Outcome);
        var budget = Assert.Single(await second.ServiceProvider.GetRequiredService<IAiAdministration>().BudgetsAsync(f.Owner, Ct)); Assert.True(budget.Reserved + budget.Spent <= budget.Limit);
    }
    [SqlFact]
    public async Task Policy_capability_kill_rights_and_isolation_gates_precede_execution()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var a = s.GetRequiredService<IAiAdministration>(); var router = s.GetRequiredService<IAiTaskRouter>(); await Budget(s, f);
        var id = await a.RegisterAsync(f.Owner, new(Settings(streaming: false)), Ct);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => Route(s, f, [id]));
        await Route(s, f, [id], streaming: false);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => router.GenerateAsync(f.Owner, f.Universe, Request() with { Streaming = true }, Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => router.GenerateAsync(f.Owner, f.Universe, Request() with { Classification = "Internal" }, Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => router.GenerateAsync(f.Owner, f.Universe, Request() with { Tools = true }, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => router.GenerateAsync(new(f.Owner.TenantId, "m07-reader"), f.Universe, Request(), Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => a.ProfilesAsync(new(Guid.NewGuid(), f.Owner.Actor), Ct));
        var p = Settings("fixture.text.b") with { Policy = new(["Public"], "EU", 0, false, "owned-policy.v1") };
        await Assert.ThrowsAsync<RuntimeValidationException>(() => a.RegisterAsync(f.Owner, new(p), Ct));
        var source = await SourceTestFixture.CreateAsync(s.GetRequiredService<ISourceRegistry>(), f.Owner, f.Universe);
        var permitted = await router.GenerateAsync(f.Owner, f.Universe, Request() with { SourceSnapshotIds = [source.Snapshot] }, Ct); Assert.Contains(source.Snapshot.ToString(), permitted.Output!.Citations); Assert.Contains(permitted.SourceCitations!, x => x.SnapshotId == source.Snapshot && x.RequiredAttribution.Contains("Credit:"));
        var sources = s.GetRequiredService<ISourceRegistry>(); var detail = await sources.InspectAsync(f.Owner, f.Universe, source.Work, Ct);
        await sources.RevokeAsync(f.Owner, f.Universe, source.Work, new(Guid.NewGuid(), null, "fixture rights revocation", detail.Work.RowVersion), Ct);
        await Assert.ThrowsAsync<RightsBlockedException>(() => router.GenerateAsync(f.Owner, f.Universe, Request() with { SourceSnapshotIds = [source.Snapshot] }, Ct)); Assert.Null((await router.InspectAsync(f.Owner, permitted.Id, Ct)).Output);
        await a.SwitchAsync(f.Owner, id, new(true, true), Ct); var killed = await router.GenerateAsync(f.Owner, f.Universe, Request(), Ct); Assert.Equal("Configuration", killed.Outcome); Assert.Equal(0, killed.Spent);
    }
    [SqlFact]
    public async Task Streaming_matches_validated_final_output_and_cancellation_keeps_unknown_usage()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var a = s.GetRequiredService<IAiAdministration>(); var router = s.GetRequiredService<IAiTaskRouter>(); await Budget(s, f);
        var id = await a.RegisterAsync(f.Owner, new(Settings()), Ct); await Route(s, f, [id]);
        var events = new List<AiStreamEvent>(); await foreach (var e in router.StreamAsync(f.Owner, f.Universe, Request(), Ct)) events.Add(e);
        Assert.Contains(events, x => x.Kind == "Delta"); var final = events.Last().Run!; Assert.Equal("Success", final.Outcome); Assert.NotNull(final.Attempts.Single().Usage);
        var slow = await a.RegisterAsync(f.Owner, new(Settings(scenario: "slow")), Ct); await Route(s, f, [slow]); using var cancel = new CancellationTokenSource();
        var pending = router.GenerateAsync(f.Owner, f.Universe, Request(), cancel.Token);
        await Task.Delay(300); await cancel.CancelAsync(); var interrupted = await pending;
        Assert.Equal("UsageUnresolved", interrupted.State); Assert.True(interrupted.Reserved > 0); Assert.Null(interrupted.Attempts.Single().Usage);
    }
    [SqlFact]
    public async Task Pinned_embedding_routes_account_usage_and_do_not_follow_new_active_models()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var a = s.GetRequiredService<IAiAdministration>(); await Budget(s, f, capability: "ai.embedding");
        async Task<Guid> Add(string model)
        {
            var p = Settings() with { Adapter = "local.embedding.v1", Capability = "ai.embedding", Model = model, Structured = false, Streaming = false, Pricing = new("local-free.v1", "USD", 0, 0, 0) };
            var id = await a.RegisterAsync(f.Owner, new(p), Ct); var route = await a.RouteAsync(f.Owner, new("embedding", "embedding.v1", [id], .1m, "USD", Structured: false), Ct); await a.ActivateAsync(f.Owner, route, Ct); return id;
        }
        await Add("concept-projection-v1"); var provider = (IRoutedEmbeddingProvider)s.GetRequiredService<IEmbeddingProvider>(); var pinned = await provider.PinAsync(f.Owner, f.Universe, "route:embedding", Ct);
        var input = new EmbeddingRequest(pinned, 128, "l2", "original field", "en-IN", [], f.Owner.TenantId, f.Owner.Actor, f.Universe); var first = await provider.EmbedAsync(input, Ct);
        await Add("concept-projection-v2"); var newer = await provider.PinAsync(f.Owner, f.Universe, "route:embedding", Ct); Assert.NotEqual(pinned, newer);
        Assert.Equal(first.Vector, (await provider.EmbedAsync(input, Ct)).Vector); Assert.NotEqual(first.Vector, (await provider.EmbedAsync(input with { Model = newer }, Ct)).Vector);
        var runs = await s.GetRequiredService<IAiTaskRouter>().RunsAsync(f.Owner, Ct); Assert.Equal(3, runs.Count); Assert.All(runs, x => { Assert.Equal("Success", x.Outcome); Assert.NotNull(x.Attempts.Single().Usage); });
    }
    [SqlFact]
    public async Task Ai_api_enforces_authentication_action_header_role_and_tenant_membership()
    {
        using var root = Services(); using var scope = root.CreateScope(); var f = await Setup(scope.ServiceProvider);
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Staff:Administrator:0"] = "integration-test-Administrator",
                ["Staff:Reader:0"] = "integration-test-Reader",
                ["Runtime:Enabled"] = "true",
                ["Knowledge:IndexWorkerEnabled"] = "false",
                ["ConnectionStrings:ContentOS"] = Connection,
                ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets")
            }));
            b.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = factory.CreateClient(); var url = $"/api/v1/ai/tenants/{f.Owner.TenantId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/staff/ai")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator"); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/staff/ai")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url + "/profiles", new RegisterAiProfile(Settings()))).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1"); client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url + "/profiles", new RegisterAiProfile(Settings()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url + "/profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/ai/tenants/{Guid.NewGuid()}/profiles")).StatusCode);
    }

    [SqlFact]
    public async Task Expired_interruption_releases_unsubmitted_allowance_and_retains_submitted_unknown_usage()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s);
        var admin = s.GetRequiredService<IAiAdministration>(); var router = s.GetRequiredService<IAiTaskRouter>();
        var budgetId = await Budget(s, f); var profileId = await admin.RegisterAsync(f.Owner, new(Settings()), Ct); var routeId = await Route(s, f, [profileId]);
        var profile = (await admin.ProfilesAsync(f.Owner, Ct)).Single(x => x.Id == profileId);
        var settings = (await admin.RoutesAsync(f.Owner, Ct)).Single(x => x.Id == routeId).Settings;
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear();
        var snapshot = System.Text.Json.JsonSerializer.Serialize(new AiExecutionSnapshot(routeId, settings, [profile], "en-IN", "fixture-interruption", []), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        async Task<Guid> PersistInterrupted(bool submitted)
        {
            db.ChangeTracker.Clear(); var budget = await db.AiBudgets.SingleAsync(x => x.Id == budgetId); budget.Reserved += .005m;
            var run = new AiRun { Id = Guid.NewGuid(), TenantId = f.Owner.TenantId, UniverseId = f.Universe, Actor = f.Owner.Actor, OperationId = Guid.NewGuid(), BudgetId = budgetId, RouteId = routeId, Fingerprint = "fixture-interruption", SnapshotJson = snapshot, Reserved = .005m, State = submitted ? "Running" : "Reserved", DeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(-1) };
            db.AiRuns.Add(run);
            if (submitted) db.AiAttempts.Add(new() { Id = Guid.NewGuid(), RunId = run.Id, Number = 1, ProfileId = profileId, ConnectionId = profile.ConnectionId, Ceiling = .0027m });
            await db.SaveChangesAsync(); return run.Id;
        }
        var unsubmitted = await PersistInterrupted(false); await admin.RecoverAsync(f.Owner, unsubmitted, Ct);
        Assert.Equal(0, (await router.InspectAsync(f.Owner, unsubmitted, Ct)).Reserved);
        var submitted = await PersistInterrupted(true); await admin.RecoverAsync(f.Owner, submitted, Ct);
        var pending = await router.InspectAsync(f.Owner, submitted, Ct); Assert.Equal("UsageUnresolved", pending.State); Assert.Equal(.0027m, pending.Reserved);
        var attempt = Assert.Single(pending.Attempts); Assert.Equal("UsageUnknown", attempt.State); Assert.Null(attempt.Usage);
        await admin.ReconcileAsync(f.Owner, submitted, attempt.Id, new(new(2000000, 0, 0), "fixture:authoritative-overage"), Ct);
        var budgetAfter = Assert.Single(await admin.BudgetsAsync(f.Owner, Ct)); Assert.Equal(2m, budgetAfter.Spent); Assert.Equal(0, budgetAfter.Reserved);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => router.GenerateAsync(f.Owner, f.Universe, Request(), Ct));
    }
    [SqlFact]
    public async Task Fallback_policy_currency_and_period_overlap_cannot_weaken_constraints()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var admin = s.GetRequiredService<IAiAdministration>();
        await Budget(s, f);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => Budget(s, f));
        var a = await admin.RegisterAsync(f.Owner, new(Settings()), Ct);
        var b = await admin.RegisterAsync(f.Owner, new(Settings("fixture.text.b") with { Policy = new(["Public"], "local", 0, false, "owned-policy.v1") }), Ct);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => admin.RouteAsync(f.Owner, new("draft", "internal.v1", [a, b], .1m, "USD", Classification: "Internal"), Ct));
        var different = await admin.RegisterAsync(f.Owner, new(Settings("fixture.text.b") with { Pricing = new("currency.v1", "INR", 1, .25m, 8) }), Ct);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => Route(s, f, [a, different]));
    }
}
