using System.Net;
using System.Net.Http.Json;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlTalkApiTests
{
    [SqlFact]
    public async Task Customer_http_authority_csrf_source_links_and_staff_boundaries_are_enforced()
    {
        using var root = TalkTestFixture.Services(); using var scope = root.CreateScope(); var f = await TalkTestFixture.Setup(scope.ServiceProvider, actor: "integration-test-Reader"); var c = f.Customer;
        using var app = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Runtime:Enabled"] = "true", ["ConnectionStrings:ContentOS"] = CustomerTestFixture.Connection, ["ConnectionStrings:Runtime"] = CustomerTestFixture.Connection, ["Talk:RetentionWorkerEnabled"] = "false", ["Read:RetentionWorkerEnabled"] = "false", ["Factory:WorkerEnabled"] = "false", ["Workflows:WorkerEnabled"] = "false", ["Knowledge:IndexWorkerEnabled"] = "false", ["Trends:RecheckWorkerEnabled"] = "false", ["Media:WorkerEnabled"] = "false", ["Watch:WorkerEnabled"] = "false", ["Social:WorkerEnabled"] = "false" })); b.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = app.CreateClient(); var path = "/api/v1/customer/tenants/" + c.Tenant + "/talk";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/talk")).StatusCode); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader"); var page = await client.GetAsync("/talk"); Assert.Equal(HttpStatusCode.OK, page.StatusCode); Assert.Equal("no-store", page.Headers.CacheControl!.ToString());
        var open = new TalkOpen(Guid.NewGuid(), f.Policy, c.Profile); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/sessions", open)).StatusCode); client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1"); var created = await client.PostAsJsonAsync(path + "/sessions", open); created.EnsureSuccessStatusCode(); var id = await created.Content.ReadFromJsonAsync<Guid>();
        var answer = await client.PostAsJsonAsync(path + "/sessions/" + id + "/turns", new TalkAsk(Guid.NewGuid(), "Where did the opposing armies gather?")); answer.EnsureSuccessStatusCode(); var turn = (await answer.Content.ReadFromJsonAsync<TalkTurnView>())!; Assert.Equal("Supported", turn.Outcome);
        var source = await client.GetAsync(path + "/sessions/" + id + "/turns/" + turn.Id + "/sources/" + turn.Sources.Single().CorpusTextId); Assert.Equal(HttpStatusCode.OK, source.StatusCode); Assert.Contains("CC BY-SA", await source.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "/sessions/" + Guid.NewGuid())).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/customer/tenants/" + Guid.NewGuid() + "/talk/sessions/" + id)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/staff/talk")).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/customer-admin/tenants/" + c.Tenant + "/talk/problems")).StatusCode);
    }
}

