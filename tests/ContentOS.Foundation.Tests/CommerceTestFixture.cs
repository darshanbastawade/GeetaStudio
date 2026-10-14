using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class CommerceTestFixture
{
    internal static ServiceProvider Services() => CreationTestFixture.Services(configure: s =>
    {
        s.AddCommerceWorkbench(); s.AddAnalyticsWorkbench(); foreach (var key in new[] { "analytics.fixture.a.v1", "analytics.fixture.b.v1", "analytics.import.v1" }) s.AddSingleton<IChannelAnalyticsProvider>(new AnalyticsImportProvider(key));
        foreach (var key in new[] { "payment.fixture.a.v1", "payment.fixture.b.v1", "payment.unknown.v1" }) s.AddScoped<IPaymentProvider>(p => new FixturePaymentProvider(key, p.GetRequiredService<ICustomerPaymentEvidenceStore>(), CustomerTestFixture.Secret));
        foreach (var key in new[] { "delivery.fixture.a.v1", "delivery.fixture.b.v1", "delivery.unknown.v1", "email.fixture.inbox.v1", "print.fixture.v1" }) s.AddSingleton<ICommerceDelivery>(p => new CommerceDeliveryAdapter(key, p.GetRequiredService<ICommerceDeliveryLedger>()));
    });
    internal sealed record Fixture(CreationTestFixture.Fixture Creation, Guid CreationId, Guid Digital, Guid Institutional, Guid Institution);
    internal static async Task<Fixture> Setup(IServiceProvider s, string adapter = "delivery.fixture.a.v1", string? actor = null)
    {
        var f = await CreationTestFixture.Setup(s, format: "Comic", actor: actor); var studio = s.GetRequiredService<ICreationStudio>(); var id = await studio.SubmitAsync(f.Owner, new(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience", "A private test learning reflection."), []), default); var ready = await CreationTestFixture.Drain(s, f, id); await studio.ReviewAsync(f.Owner, id, new(Guid.NewGuid(), ready.RowVersion, "Approve", "fixture:private-test-pack"), default);
        var customers = s.GetRequiredService<ICustomerWorkbench>(); var route = await customers.RouteAsync(f.Editorial.Owner, new(Guid.NewGuid(), "pilot-payments", 1, "payment.fixture.a.v1", "fixture-market"), default); await customers.ControlRouteAsync(f.Editorial.Owner, route, new(Guid.NewGuid(), true, true), default);
        var shop = s.GetRequiredService<ICommerceWorkbench>(); await shop.RouteAsync(f.Editorial.Owner, new(Guid.NewGuid(), "delivery", 1, adapter, "fixture-local-inbox"), default);
        var license = new CommerceLicense("PrivateTest", 30, 1, "PinnedVersionNotifyCorrections", "fixture:private-terms", "Synthetic private pilot; no real commercial authorization", true);
        var digital = await shop.ProductAsync(f.Owner, new(Guid.NewGuid(), "private-comic", 1, id, "Comic", "INR", 1000, license), default); var institution = await shop.InstitutionAsync(f.Owner, new(Guid.NewGuid(), "Controlled institutional pilot", "fixture:institution-contract"), default); var institutional = await shop.ProductAsync(f.Owner, new(Guid.NewGuid(), "institution-learning", 1, id, "Institutional", "INR", 5000, license with { Seats = 5 }), default);
        return new(f, id, digital, institutional, institution);
    }
    internal static async Task<CustomerOrderView> Payment(IServiceProvider s, Fixture f, Guid order)
    { var row = (await s.GetRequiredService<ICommerceWorkbench>().OrdersAsync(f.Creation.Owner, default)).Single(x => x.Id == order); return (await s.GetRequiredService<ICustomerWorkbench>().OrdersAsync(f.Creation.Owner, default)).Single(x => x.Id == row.PaymentOrderId); }
    internal static Task<Guid> Send(IServiceProvider s, Fixture f, CustomerPaymentEvidence e) => s.GetRequiredService<ICustomerWorkbench>().WebhookAsync(f.Creation.Owner.TenantId, FixturePaymentProvider.Sign(e, CustomerTestFixture.Secret), default);
    internal static async Task Paid(IServiceProvider s, Fixture f, Guid order) { var payment = await Payment(s, f, order); await Send(s, f, CustomerTestFixture.Evidence(payment)); await s.GetRequiredService<ICommerceWorkbench>().ReconcileAsync(f.Creation.Owner, order, default); }
    internal static async Task Drain(IServiceProvider s, Fixture f, Guid order)
    {
        var shop = s.GetRequiredService<ICommerceWorkbench>(); for (var i = 0; i < 100; i++) { var row = (await shop.OrdersAsync(f.Creation.Owner, default)).Single(x => x.Id == order); if (row.DeliveryState is "Delivered" or "Unknown" or "Withheld") return; await shop.ProcessNextAsync(default); }
        throw new InvalidOperationException("This original delivery did not settle.");
    }
}
