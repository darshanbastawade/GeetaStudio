using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContentOS.Foundation.Tests;

public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute() { if (Environment.GetEnvironmentVariable("GEETA_SQL_TESTS") != "1") Skip = "Enable the explicit SQL integration profile."; }
}

[CollectionDefinition("SQL runtime", DisableParallelization = true)]
public sealed class SqlRuntimeCollection;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed partial class SqlRuntimeTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var store = s.GetRequiredService<IRuntimeStore>();
        foreach (var previous in await store.JobsAsync(Ct))
        {
            if (previous.State is "Completed" or "Cancelled" or "DeadLetter") continue;
            await store.CancelAsync(previous.Id, Actor, Trace, Ct);
            if ((await store.InspectAsync(previous.Id, Ct))!.Job.State == "Unknown") await store.ReconcileAsync(previous.Id, Actor, Trace, Ct);
        }
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        for (var i = 0; i < 100 && await s.GetRequiredService<RuntimeRunner>().RunOneAsync("test-recovery-cleanup", TimeSpan.FromSeconds(5), Ct); i++) { }
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private const string Connection = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
    private const string Actor = "integration-test";
    private const string Trace = "0123456789abcdef0123456789abcdef";
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static ServiceProvider Services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:ContentOS"] = Connection, ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets") }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(config);
        services.AddContentRuntime(config, true);
        foreach (var key in RuntimeRules.Adapters)
            services.AddScoped<ITestOperationProvider>(s => new TestOperationProvider(key, s.GetRequiredService<ITestEffectLedger>()));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private static async Task<Guid> ConfigureAsync(IServiceProvider services, string adapter = "test.a", int attempts = 3, int concurrency = 1)
    {
        var store = services.GetRequiredService<IProviderConfigurationStore>();
        var provider = await store.RegisterAsync(new RegisterProvider(adapter, MaxConcurrency: concurrency), Actor, Trace, Ct);
        var revision = await store.RevisionAsync(new CreateRevision(provider.ProfileId, attempts, attempts), Actor, Trace, Ct);
        await store.ActivateAsync(revision, Actor, Trace, Ct); return provider.Id;
    }
    [SqlFact]
    public async Task Route_switch_preserves_snapshots_and_kill_blocks_pinned_work()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var a = await ConfigureAsync(s); var store = s.GetRequiredService<IRuntimeStore>();
        var old = await store.CreateAsync(new(), Actor, Trace, Ct);
        var b = await ConfigureAsync(s, "test.b"); var next = await store.CreateAsync(new(), Actor, Trace, Ct);
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        Assert.Equal(a, (await store.InspectAsync(old, Ct))!.Job.Snapshot.ConnectionId);
        Assert.Equal(b, (await store.InspectAsync(next, Ct))!.Job.Snapshot.ConnectionId);
        await s.GetRequiredService<IProviderConfigurationStore>().SetEnabledAsync(a, false, true, Actor, Trace, Ct);
        var runner = s.GetRequiredService<RuntimeRunner>(); await runner.RunOneAsync("switch-test", TimeSpan.FromSeconds(5), Ct);
        await runner.RunOneAsync("switch-test", TimeSpan.FromSeconds(5), Ct);
        Assert.Equal("DeadLetter", (await store.InspectAsync(old, Ct))!.Job.State);
        Assert.Equal("Completed", (await store.InspectAsync(next, Ct))!.Job.State);
        Assert.StartsWith("test.b", (await store.InspectAsync(next, Ct))!.Job.Result);
        Assert.False(await s.GetRequiredService<RuntimeDbContext>().Effects.AnyAsync(e => e.ConnectionId == a));
    }
    [SqlFact]
    public async Task Two_workers_claim_once_and_crash_recovers_the_original_effect_with_fencing()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        await ConfigureAsync(s); var store = s.GetRequiredService<IRuntimeStore>(); var id = await store.CreateAsync(new(), Actor, Trace, Ct);
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        using var workerA = root.CreateScope(); using var workerB = root.CreateScope();
        var claims = await Task.WhenAll(workerA.ServiceProvider.GetRequiredService<IRuntimeStore>().ClaimAsync("a", TimeSpan.FromMilliseconds(600), Ct),
            workerB.ServiceProvider.GetRequiredService<IRuntimeStore>().ClaimAsync("b", TimeSpan.FromMilliseconds(600), Ct));
        var first = Assert.Single(claims, c => c is not null)!;
        var winningScope = claims[0] is not null ? workerA : workerB;
        Assert.Equal(ProviderError.None, await winningScope.ServiceProvider.GetRequiredService<IRuntimeStore>().AdmitAsync(first, Ct));
        await winningScope.ServiceProvider.GetRequiredService<ITestEffectLedger>().SubmitAsync(first.OperationId, first.Snapshot.ConnectionId, first.Snapshot.AdapterKey, Ct);
        // Simulate loss of the worker after a committed provider effect, with no completion/heartbeat.
        await Task.Delay(800);
        using var recovery = root.CreateScope(); var recoveredStore = recovery.ServiceProvider.GetRequiredService<IRuntimeStore>();
        var second = await recoveredStore.ClaimAsync("restarted", TimeSpan.FromSeconds(5), Ct); Assert.NotNull(second); Assert.True(second.Reconciliation);
        Assert.Equal(ProviderError.None, await recoveredStore.AdmitAsync(second, Ct));
        var adapter = recovery.ServiceProvider.GetServices<ITestOperationProvider>().Single(p => p.Key == second.Snapshot.AdapterKey);
        await recoveredStore.CompleteAsync(second, await adapter.ReconcileAsync(second, Ct), Ct);
        await winningScope.ServiceProvider.GetRequiredService<IRuntimeStore>().CompleteAsync(first, new(ProviderError.Terminal), Ct);
        var detail = await recoveredStore.InspectAsync(id, Ct); Assert.Equal("Completed", detail!.Job.State);
        Assert.Equal(first.OperationId, detail.Operation!.Id); Assert.Equal(2, detail.Attempts.Count);
        Assert.Equal("LeaseExpired", detail.Attempts[0].State);
        Assert.Single(await recovery.ServiceProvider.GetRequiredService<RuntimeDbContext>().Effects.Where(e => e.Id == first.OperationId).ToListAsync());
        Assert.All(detail.Attempts, a => { Assert.Equal(32, a.TraceId.Length); Assert.NotEqual(Guid.Empty, a.AuditId); });
    }
    [SqlFact]
    public async Task Unknown_outcome_requires_explicit_reconciliation_and_retains_budget_and_binding()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; await ConfigureAsync(s);
        var store = s.GetRequiredService<IRuntimeStore>(); var id = await store.CreateAsync(new("unknown"), Actor, Trace, Ct);
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        var runner = s.GetRequiredService<RuntimeRunner>(); await runner.RunOneAsync("unknown", TimeSpan.FromSeconds(5), Ct);
        var detail = (await store.InspectAsync(id, Ct))!; Assert.Equal("Unknown", detail.Job.State); Assert.Single(detail.Attempts);
        Assert.False(await runner.RunOneAsync("no-blind-resubmit", TimeSpan.FromSeconds(5), Ct));
        var db = s.GetRequiredService<RuntimeDbContext>(); Assert.False((await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id)).Settled);
        await store.ReconcileAsync(id, Actor, Trace, Ct); await runner.RunOneAsync("reconcile", TimeSpan.FromSeconds(5), Ct);
        var final = (await store.InspectAsync(id, Ct))!; Assert.Equal("Completed", final.Job.State);
        Assert.Equal(detail.Operation!.Id, final.Operation!.Id); Assert.True(final.Attempts[1].Reconciliation);
        Assert.Single(await db.Effects.AsNoTracking().Where(e => e.Id == final.Operation.Id).ToListAsync());
        Assert.True((await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id)).Settled);
    }
    [SqlFact]
    public async Task Retries_are_bounded_and_terminal_failures_are_not_retried()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; await ConfigureAsync(s, attempts: 2);
        var store = s.GetRequiredService<IRuntimeStore>(); var id = await store.CreateAsync(new("transient"), Actor, Trace, Ct);
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct); var runner = s.GetRequiredService<RuntimeRunner>();
        await runner.RunOneAsync("retry", TimeSpan.FromSeconds(5), Ct); await Task.Delay(1400); await runner.RunOneAsync("retry", TimeSpan.FromSeconds(5), Ct);
        var result = (await store.InspectAsync(id, Ct))!; Assert.Equal("DeadLetter", result.Job.State); Assert.Equal(2, result.Job.Attempts);
        Assert.Equal(2, (await s.GetRequiredService<RuntimeDbContext>().Reservations.AsNoTracking().SingleAsync(r => r.Id == id)).Used);
        var terminal = await store.CreateAsync(new("terminal"), Actor, Trace, Ct); await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        await runner.RunOneAsync("terminal", TimeSpan.FromSeconds(5), Ct); Assert.Single((await store.InspectAsync(terminal, Ct))!.Attempts);
        Assert.Equal("DeadLetter", (await store.InspectAsync(terminal, Ct))!.Job.State);
    }
    [SqlFact]
    public async Task Outbox_replay_and_cancellation_do_not_duplicate_or_run_cancelled_work()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; await ConfigureAsync(s);
        var store = s.GetRequiredService<IRuntimeStore>(); var id = await store.CreateAsync(new(), Actor, Trace, Ct);
        await store.CancelAsync(id, Actor, Trace, Ct); var transport = s.GetRequiredService<IJobTransport>(); await transport.DispatchAsync(Ct);
        var db = s.GetRequiredService<RuntimeDbContext>(); var message = await db.Outbox.SingleAsync(o => o.JobId == id);
        await db.Outbox.Where(o => o.Id == message.Id).ExecuteUpdateAsync(p => p.SetProperty(o => o.DispatchedUtc, (DateTimeOffset?)null));
        db.ChangeTracker.Clear(); await transport.DispatchAsync(Ct);
        Assert.Single(await db.Inbox.Where(i => i.Id == message.Id).ToListAsync());
        Assert.Null(await store.ClaimAsync("cancelled", TimeSpan.FromSeconds(5), Ct));
        Assert.Equal("Cancelled", (await store.InspectAsync(id, Ct))!.Job.State);
        Assert.True((await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id)).Settled);
    }
    [SqlFact]
    public async Task Asset_ids_are_stable_and_owner_isolation_and_integrity_survive_new_scopes()
    {
        using var root = Services(); Guid id;
        using (var scope = root.CreateScope()) id = await scope.ServiceProvider.GetRequiredService<IAssetStore>().PutAsync(new byte[] { 1, 2, 3 }, Actor, Ct);
        using var next = root.CreateScope(); var assets = next.ServiceProvider.GetRequiredService<IAssetStore>();
        Assert.Equal(new byte[] { 1, 2, 3 }, await assets.GetAsync(id, Actor, Ct)); Assert.Null(await assets.GetAsync(id, "another-owner", Ct));
        var path = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets", id.ToString("N") + ".bin");
        await File.WriteAllBytesAsync(path, [4]); await Assert.ThrowsAsync<RuntimeConflictException>(() => assets.GetAsync(id, Actor, Ct));
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
    }
    [SqlFact]
    public async Task Invalid_configuration_and_missing_secrets_fail_without_creating_records()
    {
        using var root = Services(); using var scope = root.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<IProviderConfigurationStore>();
        await Assert.ThrowsAsync<RuntimeValidationException>(() => store.RegisterAsync(new("test.a", Endpoint: "https://untrusted.invalid"), Actor, Trace, Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => store.RegisterAsync(new("test.a", Capability: "text.generate"), Actor, Trace, Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => store.RegisterAsync(new("test.a", SecretRef: "env:GEETA_PROVIDER_MISSING_M1_TEST"), Actor, Trace, Ct));
    }
    [SqlFact]
    public async Task Admin_requires_identity_and_correct_role_and_denials_are_audited()
    {
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Staff:Administrator:0"] = "integration-test-Administrator", ["Staff:Reader:0"] = "integration-test-Reader", ["Runtime:Enabled"] = "true", ["ConnectionStrings:ContentOS"] = Connection, ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets") }));
            builder.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/runtime/providers")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/runtime/providers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/runtime/providers/" + Guid.NewGuid() + "/kill", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        var rejected = await client.PostAsync("/api/v1/runtime/providers/" + Guid.NewGuid() + "/kill", null);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("Staff action header", await rejected.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        using var malformed = new StringContent("{\"adapterKey\":123}", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/runtime/providers", malformed)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Audit.AnyAsync(a => a.Action == "admin.access-denied"));
    }
}

// Test-only scheme, registered solely by the WebApplicationFactory above.
public sealed class TestStaffAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new(ClaimTypes.Name, "integration-test-" + role), new(ClaimTypes.Role, role)], "Test");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
    }
}

public static class TestAuthentication
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<IAuthenticationSchemeProvider, TestSchemeProvider>();
        services.AddAuthentication(o => { o.DefaultScheme = "Test"; o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; o.DefaultForbidScheme = "Test"; })
            .AddScheme<AuthenticationSchemeOptions, TestStaffAuthentication>("Test", _ => { });
    }
}
public sealed class TestSchemeProvider(IOptions<AuthenticationOptions> options) : AuthenticationSchemeProvider(options)
{
    public override Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync() => Task.FromResult(Enumerable.Empty<AuthenticationScheme>());
}
