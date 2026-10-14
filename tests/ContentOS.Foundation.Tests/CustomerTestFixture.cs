using System.Security.Claims;
using System.Security.Cryptography;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class CustomerTestFixture
{
    internal static readonly string Secret = Environment.GetEnvironmentVariable("Customers__FixtureSigningKey") ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    internal const string Connection = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
    internal static ServiceProvider Services()
    {
        var services = new ServiceCollection().AddLogging(); services.AddContentCore(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ContentOS"] = Connection }).Build()); services.AddCustomerWorkbench(); services.AddAnalyticsWorkbench();
        services.AddSingleton<IIdentityProvider, HostIdentityProvider>(); foreach (var k in new[] { "identity.fixture.a.v1", "identity.fixture.b.v1" }) services.AddSingleton<IIdentityProvider>(new FixtureIdentityProvider(k, Secret));
        foreach (var k in new[] { "payment.fixture.a.v1", "payment.fixture.b.v1", "payment.unknown.v1" }) services.AddScoped<IPaymentProvider>(s => new FixturePaymentProvider(k, s.GetRequiredService<ICustomerPaymentEvidenceStore>(), Secret));
        services.AddSingleton<INotificationProvider, InboxNotificationProvider>(); return services.BuildServiceProvider();
    }
    internal static CustomerScope Scope(Guid tenant, string actor) => new(tenant, new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Name, actor)], "VerifiedTestHost")));
    internal sealed record Fixture(Guid Tenant, Guid Universe, CoreScope Admin, CustomerScope Alice, Guid User, Guid Household, Guid Profile);
    internal static async Task<Fixture> Setup(IServiceProvider s, string? actor = null)
    {
        var db = s.GetRequiredService<CoreDbContext>(); var id = Guid.NewGuid(); var org = new Organization { Id = Guid.NewGuid(), Name = "M20 controlled fixture" }; db.Organizations.Add(org); db.Tenants.Add(new() { Id = id, OrganizationId = org.Id, Key = "m20-" + id.ToString("N"), Name = "M20 fixture" });
        var universe = Guid.NewGuid(); db.Universes.Add(new() { Id = universe, TenantId = id, Key = "m20-fixture", Name = "Controlled customer analytics" }); var admin = new CoreScope(id, "integration-test-Administrator"); db.Members.Add(new() { TenantId = id, Actor = admin.Actor, Permission = "Administrator" }); await db.SaveChangesAsync();
        var a = Scope(id, actor ?? "fixture-alice:" + id.ToString("N")); var w = s.GetRequiredService<ICustomerWorkbench>(); var u = await w.RegisterAsync(a, new(Guid.NewGuid(), "Fixture adult", true), default); var h = await w.HouseholdAsync(a, new(Guid.NewGuid(), "Fixture household"), default); var p = await w.ProfileAsync(a, new(Guid.NewGuid(), h, "Fixture reader", "Adult", "en-IN"), default); return new(id, universe, admin, a, u, h, p);
    }
    internal static async Task<(Guid Product, Guid Route)> Catalog(ICustomerWorkbench w, Fixture f, string kind = "Credits", string provider = "payment.fixture.a.v1")
    {
        var p = await w.ProductAsync(f.Admin, new(Guid.NewGuid(), kind == "Credits" ? "create-credits" : "geeta-plus", 1, kind, "INR", 1000, kind == "Credits" ? 100 : 0, 30, kind == "Credits" ? [] : ["GeetaPlus", "ReadPlus"], UniverseId: f.Universe), default);
        var route = await w.RouteAsync(f.Admin, new(Guid.NewGuid(), "payments", 1, provider, "fixture-account"), default); await w.ControlRouteAsync(f.Admin, route, new(Guid.NewGuid(), true, true), default); return (p, route);
    }
    internal static CustomerPaymentEvidence Evidence(CustomerOrderView o, long seq = 1, string state = "Paid", long refund = 0, DateTimeOffset? paidUtc = null) => new("evt-" + Guid.NewGuid().ToString("N"), o.Id, o.Provider, o.Account, o.Environment, o.CustomerReference, o.ExternalReference ?? "fixture-order:" + o.Id.ToString("N"), o.Features.Length > 0 ? "fixture-sub:" + o.Id.ToString("N") : null, seq, state, o.PriceMinor, o.Currency, refund, DateTimeOffset.UtcNow, o.Features.Length > 0 ? DateTimeOffset.UtcNow.AddDays(30) : null, paidUtc);
    internal static Task<Guid> Send(ICustomerWorkbench w, Fixture f, CustomerPaymentEvidence e) => w.WebhookAsync(f.Tenant, FixturePaymentProvider.Sign(e, Secret), default);
    internal static CustomerProof Proof(CustomerScope s, CustomerChallengeView c, string subject) => new(Guid.NewGuid(), c.Id, c.Provider, c.Account, FixtureIdentityProvider.Sign(new(c.Id, s.Principal.Identity!.Name!, subject, c.Provider, c.Account, DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds()), Secret));
}
