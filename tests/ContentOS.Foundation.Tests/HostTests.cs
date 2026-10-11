using System.Net;
using System.Net.Http.Json;
using ContentOS.Contracts;
using GeetaStudio.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ContentOS.Foundation.Tests;

public sealed class HostTests
{
    [Fact]
    public async Task Development_host_exposes_real_liveness_and_labeled_fixture()
    {
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b => b.UseEnvironment("Development").ConfigureServices(TestAuthentication.Configure));
        using var client = factory.CreateClient();
        var health = await client.GetFromJsonAsync<HealthStatus>("/health/live");
        Assert.Equal("Healthy", health!.Status);
        Assert.Equal("GeetaStudio.Api", health.Service);
        Assert.Equal(TimeSpan.Zero, health.CheckedAtUtc.Offset);
        var fixture = await client.GetFromJsonAsync<DevelopmentStatus>("/api/v1/development/status");
        Assert.True(fixture!.IsFixture);
        Assert.Equal("Development", fixture.Mode);
    }

    [Fact]
    public async Task Production_does_not_expose_development_fixture()
    {
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b => b.UseEnvironment("Production").ConfigureServices(TestAuthentication.Configure));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/development/status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Theory]
    [InlineData("healthy", true)]
    [InlineData("bad-json", false)]
    [InlineData("wrong-service", false)]
    [InlineData("http-error", false)]
    [InlineData("offline", false)]
    [InlineData("timeout", false)]
    public async Task Web_reports_actual_reachability_and_safe_errors(string response, bool expected)
    {
        using var http = new HttpClient(new HealthHandler(response)) { BaseAddress = new Uri("http://localhost/") };
        var result = await new ApiHealthClient(http).CheckAsync();
        Assert.Equal(expected, result.Reachable);
        if (!expected) Assert.Null(result.Health);
        Assert.DoesNotContain("sensitive-detail", result.Message);
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved()
    {
        using var http = new HttpClient(new HealthHandler("timeout")) { BaseAddress = new Uri("http://localhost/") };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ApiHealthClient(http).CheckAsync(cancellation.Token));
    }

    private sealed class HealthHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/health/live", request.RequestUri!.AbsolutePath);
            if (response == "offline") throw new HttpRequestException("sensitive-detail");
            if (response == "timeout") throw new TaskCanceledException("sensitive-detail");
            return Task.FromResult(response switch
            {
                "bad-json" => new(HttpStatusCode.OK) { Content = new StringContent("not JSON") },
                "http-error" => new(HttpStatusCode.ServiceUnavailable),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new HealthStatus(response == "wrong-service" ? "Other" : "GeetaStudio.Api", "Healthy", DateTimeOffset.UtcNow))
                }
            });
        }
    }
}
