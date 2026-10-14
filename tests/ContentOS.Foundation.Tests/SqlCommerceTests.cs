using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlCommerceTests
{
    [SqlFact]
    public async Task Digital_order_pins_reviewed_version_replays_refunds_and_finance_without_minting_credits()
    {
        using var root = CommerceTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CommerceTestFixture.Setup(s); var shop = s.GetRequiredService<ICommerceWorkbench>(); var owner = f.Creation.Owner; var request = new CommercePurchase(Guid.NewGuid(), f.Digital, "Instagram"); var id = await shop.PurchaseAsync(owner, request, default); Assert.Equal(id, await shop.PurchaseAsync(owner, request, default)); await Assert.ThrowsAsync<RuntimeValidationException>(() => shop.DownloadAsync(owner, id, default)); await CommerceTestFixture.Paid(s, f, id); await CommerceTestFixture.Drain(s, f, id);
        var before = await shop.DownloadAsync(owner, id, default); Assert.Contains("CC BY-SA", before.Citations.Single().RequiredAttribution); Assert.Contains("size:A4", before.Html); var studio = s.GetRequiredService<ICreationStudio>(); var creation = await studio.InspectAsync(owner, f.CreationId, default); await studio.EditAsync(owner, f.CreationId, new(Guid.NewGuid(), creation.RowVersion, new("Learner", "kindness", "A later private edit")), default); Assert.Equal(before.Sha256, (await shop.DownloadAsync(owner, id, default)).Sha256);
        var analytics = s.GetRequiredService<IAnalyticsWorkbench>(); var first = await analytics.CaptureCustomerPaymentsAsync(f.Creation.Editorial.Owner, f.Creation.Editorial.Universe, new(Guid.NewGuid()), default); Assert.Equal(1, first.Inserted); Assert.Equal(0, (await analytics.CaptureCustomerPaymentsAsync(f.Creation.Editorial.Owner, f.Creation.Editorial.Universe, new(Guid.NewGuid()), default)).Inserted);
        var payment = await CommerceTestFixture.Payment(s, f, id); var paid = CustomerTestFixture.Evidence(payment); var refund = CustomerTestFixture.Evidence(payment, 3, "Refunded", payment.PriceMinor, DateTimeOffset.UtcNow.AddMinutes(-1)); await CommerceTestFixture.Send(s, f, refund); await shop.ReconcileAsync(owner, id, default); await Assert.ThrowsAsync<RuntimeValidationException>(() => shop.DownloadAsync(owner, id, default)); await analytics.CaptureCustomerPaymentsAsync(f.Creation.Editorial.Owner, f.Creation.Editorial.Universe, new(Guid.NewGuid()), default);
        Assert.Equal(0, (await analytics.ReportAsync(f.Creation.Editorial.Owner, f.Creation.Editorial.Universe, AnalyticsTestFixture.Query, default)).RecognizedNet); Assert.Equal(8, (await s.GetRequiredService<ICustomerWorkbench>().WalletAsync(owner, default)).Available); Assert.Equal(1, await s.GetRequiredService<CoreDbContext>().CommerceOrders.CountAsync(x => x.Id == id)); _ = paid;
    }
    [SqlFact]
    public async Task Unknown_fulfillment_reconciles_original_route_after_switch_without_duplicate_effect_or_revenue()
    {
        using var root = CommerceTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CommerceTestFixture.Setup(s, "delivery.unknown.v1"); var shop = s.GetRequiredService<ICommerceWorkbench>(); var id = await shop.PurchaseAsync(f.Creation.Owner, new(Guid.NewGuid(), f.Digital), default); await CommerceTestFixture.Paid(s, f, id); await CommerceTestFixture.Drain(s, f, id); Assert.Equal("Unknown", (await shop.OrdersAsync(f.Creation.Owner, default)).Single().DeliveryState);
        await shop.RouteAsync(f.Creation.Editorial.Owner, new(Guid.NewGuid(), "delivery", 2, "delivery.fixture.b.v1", "replacement-inbox"), default); var reconciled = await shop.ReconcileAsync(f.Creation.Owner, id, default); Assert.Equal("Delivered", reconciled.DeliveryState); Assert.Equal("delivery.unknown.v1", reconciled.DeliveryAdapter); Assert.Equal("fixture-local-inbox", reconciled.Account); await shop.ReconcileAsync(f.Creation.Owner, id, default);
        var db = s.GetRequiredService<CoreDbContext>(); Assert.Equal(1, await db.CommerceEffects.CountAsync(x => db.CommerceDeliveries.Any(d => d.Id == x.DeliveryId && d.OrderId == id))); var finance = s.GetRequiredService<IAnalyticsWorkbench>(); Assert.Equal(1, (await finance.CaptureCommerceAsync(f.Creation.Editorial.Owner, f.Creation.Editorial.Universe, new(Guid.NewGuid()), default)).Inserted); Assert.Equal(0, (await finance.CaptureCommerceAsync(f.Creation.Editorial.Owner, f.Creation.Editorial.Universe, new(Guid.NewGuid()), default)).Inserted);
    }
    [SqlFact]
    public async Task Institutional_seats_scope_cohorts_current_consent_and_refunded_license_are_enforced()
    {
        using var root = CommerceTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CommerceTestFixture.Setup(s); var shop = s.GetRequiredService<ICommerceWorkbench>(); var customers = s.GetRequiredService<ICustomerWorkbench>(); var id = await shop.PurchaseAsync(f.Creation.Owner, new(Guid.NewGuid(), f.Institutional, InstitutionId: f.Institution), default); await CommerceTestFixture.Paid(s, f, id);
        var people = new List<(CustomerScope Scope, Guid User, Guid Profile)>();
        for (var i = 0; i < 5; i++) { var userScope = CustomerTestFixture.Scope(f.Creation.Owner.TenantId, "cohort-" + Guid.NewGuid()); var user = await customers.RegisterAsync(userScope, new(Guid.NewGuid(), "Controlled adult", true), default); var house = await customers.HouseholdAsync(userScope, new(Guid.NewGuid(), "Controlled household"), default); var profile = await customers.ProfileAsync(userScope, new(Guid.NewGuid(), house, "Controlled learner", "Adult", "en-IN"), default); await shop.SeatAsync(f.Creation.Owner, f.Institution, new(Guid.NewGuid(), user, "Pilot"), default); await customers.ConsentAsync(userScope, new(Guid.NewGuid(), profile, "Progress", true, DateTimeOffset.UtcNow.AddDays(1), "fixture:cohort-consent"), default); await shop.ActivityAsync(userScope, f.Institution, new(Guid.NewGuid(), profile, true), default); people.Add((userScope, user, profile)); }
        Assert.Equal(1, (await shop.CohortsAsync(f.Creation.Owner, f.Institution, default)).Single().CompletionRate); Assert.Equal(5, (await shop.InstitutionsAsync(f.Creation.Owner, default)).Single().ActiveSeats); await shop.InstitutionDownloadAsync(people[0].Scope, f.Institution, f.Institutional, default);
        var other = await shop.InstitutionAsync(people[0].Scope, new(Guid.NewGuid(), "Other isolated pilot", "fixture:other-contract"), default); await Assert.ThrowsAsync<CoreAccessException>(() => shop.CohortsAsync(people[0].Scope, f.Institution, default)); await Assert.ThrowsAsync<CoreAccessException>(() => shop.InstitutionDownloadAsync(people[1].Scope, other, f.Institutional, default));
        await customers.ConsentAsync(people[0].Scope, new(Guid.NewGuid(), people[0].Profile, "Progress", false, DateTimeOffset.UtcNow, "fixture:withdrawn"), default); Assert.Null((await shop.CohortsAsync(f.Creation.Owner, f.Institution, default)).Single().CompletionRate);
        var payment = await CommerceTestFixture.Payment(s, f, id); await CommerceTestFixture.Send(s, f, CustomerTestFixture.Evidence(payment, 3, "Refunded", payment.PriceMinor, DateTimeOffset.UtcNow.AddMinutes(-1))); await shop.ReconcileAsync(f.Creation.Owner, id, default); await Assert.ThrowsAsync<CoreAccessException>(() => shop.InstitutionDownloadAsync(people[0].Scope, f.Institution, f.Institutional, default)); Assert.Equal(0, (await shop.InstitutionsAsync(f.Creation.Owner, default)).Single(x => x.Id == f.Institution).SeatCapacity);
    }
    [SqlFact]
    public async Task Creator_erasure_withholds_licensed_copies_and_other_accounts_cannot_read_individual_orders()
    {
        using var root = CommerceTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CommerceTestFixture.Setup(s); var shop = s.GetRequiredService<ICommerceWorkbench>(); var id = await shop.PurchaseAsync(f.Creation.Owner, new(Guid.NewGuid(), f.Digital), default); await CommerceTestFixture.Paid(s, f, id);
        var other = CustomerTestFixture.Scope(f.Creation.Owner.TenantId, "unlicensed-" + Guid.NewGuid()); await s.GetRequiredService<ICustomerWorkbench>().RegisterAsync(other, new(Guid.NewGuid(), "Another account", true), default); await Assert.ThrowsAsync<CoreAccessException>(() => shop.DownloadAsync(other, id, default));
        await s.GetRequiredService<ICreationStudio>().DeleteAsync(f.Creation.Owner, f.CreationId, default); await Assert.ThrowsAsync<RuntimeValidationException>(() => shop.DownloadAsync(f.Creation.Owner, id, default)); var db = s.GetRequiredService<CoreDbContext>(); Assert.All(await db.CommerceProducts.AsNoTracking().Where(x => x.CreationId == f.CreationId).ToArrayAsync(), x => { Assert.Null(x.PackageJson); Assert.False(x.Enabled); });
    }
}
