using System.Net;
using System.Net.Http.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlCustomerApiTests
{
    [SqlFact]
    public async Task Payment_finance_capture_replays_and_late_observations_do_not_double_count_refunds()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var f = await CustomerTestFixture.Setup(scope.ServiceProvider); var w = scope.ServiceProvider.GetRequiredService<ICustomerWorkbench>(); var a = scope.ServiceProvider.GetRequiredService<IAnalyticsWorkbench>(); var (product, route) = await CustomerTestFixture.Catalog(w, f); var id = await w.CheckoutAsync(f.Alice, new(Guid.NewGuid(), product, "Instagram"), default); var o = await w.SubmitAsync(f.Alice, id, default); var paid = CustomerTestFixture.Evidence(o); await CustomerTestFixture.Send(w, f, paid);
        var first = await a.CaptureCustomerPaymentsAsync(f.Admin, f.Universe, new(Guid.NewGuid()), default); Assert.Equal(1, first.Inserted); Assert.Equal(10, (await a.ReportAsync(f.Admin, f.Universe, AnalyticsTestFixture.Query, default)).RecognizedNet);
        var refund = CustomerTestFixture.Evidence(o, 3, "Refunded", 500, paid.OccurredUtc); await CustomerTestFixture.Send(w, f, refund); var second = await a.CaptureCustomerPaymentsAsync(f.Admin, f.Universe, new(Guid.NewGuid()), default); Assert.Equal(1, second.Inserted); Assert.Equal(5, (await a.ReportAsync(f.Admin, f.Universe, AnalyticsTestFixture.Query, default)).RecognizedNet);
        await CustomerTestFixture.Send(w, f, CustomerTestFixture.Evidence(o, 2, "Refunded", 200, paid.OccurredUtc)); Assert.Equal(0, (await a.CaptureCustomerPaymentsAsync(f.Admin, f.Universe, new(Guid.NewGuid()), default)).Inserted); Assert.Equal(5, (await a.ReportAsync(f.Admin, f.Universe, AnalyticsTestFixture.Query, default)).RecognizedNet);
        await CustomerTestFixture.Send(w, f, CustomerTestFixture.Evidence(o, 4, "Refunded", 1000, paid.OccurredUtc)); await a.CaptureCustomerPaymentsAsync(f.Admin, f.Universe, new(Guid.NewGuid()), default); var report = await a.ReportAsync(f.Admin, f.Universe, AnalyticsTestFixture.Query, default); Assert.Equal(0, report.RecognizedNet); Assert.Contains(report.Groups, x => x.Dimension == "AcquisitionChannel" && x.Key == "Instagram"); Assert.Equal(0, (await a.CaptureCustomerPaymentsAsync(f.Admin, f.Universe, new(Guid.NewGuid()), default)).Inserted);
    }
    [SqlFact]
    public async Task Private_customer_api_requires_verified_host_csrf_and_household_access_and_never_allows_customer_grants()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var f = await CustomerTestFixture.Setup(scope.ServiceProvider, "integration-test-Reader");
        using var app = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b => { b.UseEnvironment("Development"); b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Runtime:Enabled"] = "true", ["ConnectionStrings:ContentOS"] = CustomerTestFixture.Connection, ["ConnectionStrings:Runtime"] = CustomerTestFixture.Connection, ["Customers:FixtureSigningKey"] = CustomerTestFixture.Secret, ["Read:RetentionWorkerEnabled"] = "false", ["Factory:WorkerEnabled"] = "false", ["Workflows:WorkerEnabled"] = "false", ["Knowledge:IndexWorkerEnabled"] = "false", ["Trends:RecheckWorkerEnabled"] = "false", ["Media:WorkerEnabled"] = "false", ["Watch:WorkerEnabled"] = "false", ["Social:WorkerEnabled"] = "false" })); b.ConfigureServices(TestAuthentication.Configure); });
        using var client = app.CreateClient(); var path = "/api/v1/customer/tenants/" + f.Tenant; Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/me")).StatusCode); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        var me = await client.GetAsync(path + "/me"); Assert.Equal(HttpStatusCode.OK, me.StatusCode); Assert.Equal("no-store", me.Headers.CacheControl!.ToString()); Assert.Equal(f.User, (await me.Content.ReadFromJsonAsync<CustomerUserView>())!.Id);
        var noCsrf = await client.PostAsJsonAsync(path + "/households", new CustomerHouseholdRequest(Guid.NewGuid(), "Fixture")); Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode); client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/customer-admin/tenants/" + f.Tenant + "/grants", new CustomerGrant(Guid.NewGuid(), f.User, 999, DateTimeOffset.UtcNow.AddDays(1), "fixture:client-forgery"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path + "/payment-success", new { orderId = Guid.NewGuid(), paid = true })).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "/profiles/" + Guid.NewGuid() + "/progress")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/account")).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/staff/customers")).StatusCode);
    }
}
