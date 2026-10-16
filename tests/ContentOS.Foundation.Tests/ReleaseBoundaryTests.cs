using System.Net;
using System.Net.Http.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

public sealed class ReleaseBoundaryTests
{
    private sealed class Gate(ReleaseDecision decision) : IReleaseGate
    { public Task<ReleaseDecision> EvaluateAsync(string actor, CancellationToken ct) => Task.FromResult(decision); }
    private sealed class Reader(Guid selected) : IPublicRead
    {
        public int Gets { get; private set; }
        public Task<ReadCollectionSummary[]> DirectoryAsync(CancellationToken ct) => Task.FromResult<ReadCollectionSummary[]>([new(selected, "selected", 1, "Selected", 1, "Active"), new(Guid.NewGuid(), "other", 1, "Not selected", 1, "Active")]);
        public Task<ReadBook?> GetAsync(string key, CancellationToken ct) { Gets++; return Task.FromResult<ReadBook?>(null); }
        public Task EventAsync(string key, ReadEngagement request, CancellationToken ct) => Task.CompletedTask;
    }
    [Fact]
    public async Task Production_enforces_real_routes_even_when_configuration_requests_a_bypass_and_filters_directory()
    {
        var target = new ReleaseTarget("selected", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); var reader = new Reader(target.ResourceId);
        var decision = new ReleaseDecision("SYNTHETIC HOST TEST", DateTimeOffset.UtcNow, "code", "policy", "config", "manifest", true, [], [new("READ", true, true, [], [new(target, "binding", [], [], [target.ResourceId])])]);
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Runtime:Enabled", "true");
            b.UseSetting("ConnectionStrings:ContentOS", "Server=does-not-exist;Database=NoConnectionAttempt;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Runtime:Enabled"] = "true",
                ["Release:Enforce"] = "false",
                ["ConnectionStrings:ContentOS"] = "Server=does-not-exist;Database=NoConnectionAttempt;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;"
            }));
            b.ConfigureTestServices(s => { TestAuthentication.Configure(s); s.AddScoped<IReleaseGate>(_ => new Gate(decision)); s.AddSingleton<IPublicRead>(reader); });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.IsType<Gate>(factory.Services.GetRequiredService<IReleaseGate>());
        Assert.Contains(factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>(), endpoint => endpoint.RoutePattern.RawText!.TrimEnd('/') == "/api/v1/read");
        foreach (var path in new[] { "/api/v1/talk/tenants/" + target.TenantId, "/api/v1/customer/tenants/" + target.TenantId + "/checkout", "/api/v1/commerce/tenants/" + target.TenantId, "/api/v1/read/unlisted", "/staff/talk" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(0, reader.Gets);
        var rows = await client.GetFromJsonAsync<ReadCollectionSummary[]>("/api/v1/read");
        Assert.Single(rows!); Assert.Equal(target.ResourceId, rows![0].Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
}
