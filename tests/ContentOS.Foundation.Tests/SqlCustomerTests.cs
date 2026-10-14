using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlCustomerTests
{
    [SqlFact]
    public async Task Concurrent_spending_unknown_work_and_exactly_once_failure_or_partial_settlement_preserve_balance()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CustomerTestFixture.Setup(s); var w = s.GetRequiredService<ICustomerWorkbench>(); await w.GrantAsync(f.Admin, new(Guid.NewGuid(), f.User, 100, DateTimeOffset.UtcNow.AddDays(30), "fixture:grant"), default);
        async Task<Guid?> Reserve(string key) { using var child = root.CreateScope(); try { return await child.ServiceProvider.GetRequiredService<ICustomerWorkbench>().ReserveAsync(f.Alice, new(Guid.NewGuid(), f.Profile, key, 70), default); } catch (RuntimeValidationException) { return null; } }
        var racers = await Task.WhenAll(Reserve("race-a"), Reserve("race-b")); Assert.Single(racers, x => x != null); var id = racers.Single(x => x != null)!.Value; Assert.Equal(30, (await w.WalletAsync(f.Alice, default)).Available);
        await w.DispatchAsync(f.Admin, id, new(Guid.NewGuid(), "fixture.generator", "original-account", "run-one"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.CancelReservationAsync(f.Alice, id, new(Guid.NewGuid()), default)); await Assert.ThrowsAsync<RuntimeValidationException>(() => w.SettleAsync(f.Admin, id, new(Guid.NewGuid(), "fixture.generator", "different-account", "run-one", "Completed", 20, "fixture:receipt"), default));
        await w.SettleAsync(f.Admin, id, new(Guid.NewGuid(), "fixture.generator", "original-account", "run-one", "Unknown", 0, "fixture:unknown"), default); Assert.Equal(70, (await w.WalletAsync(f.Alice, default)).Reserved);
        var settlement = new CustomerSettlement(Guid.NewGuid(), "fixture.generator", "original-account", "run-one", "Completed", 20, "fixture:receipt"); await w.SettleAsync(f.Admin, id, settlement, default); await w.SettleAsync(f.Admin, id, settlement with { OperationId = Guid.NewGuid() }, default); Assert.Equal(80, (await w.WalletAsync(f.Alice, default)).Available); Assert.Equal(20, (await w.WalletAsync(f.Alice, default)).Consumed);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => w.SettleAsync(f.Admin, id, settlement with { OperationId = Guid.NewGuid(), Consumed = 21 }, default));
        var failed = await w.ReserveAsync(f.Alice, new(Guid.NewGuid(), f.Profile, "failed-generation", 50), default); await w.DispatchAsync(f.Admin, failed, new(Guid.NewGuid(), "fixture.generator", "original-account", "failed-run"), default); var failure = new CustomerSettlement(Guid.NewGuid(), "fixture.generator", "original-account", "failed-run", "Failed", 0, "fixture:failure"); await w.SettleAsync(f.Admin, failed, failure, default); await w.SettleAsync(f.Admin, failed, failure with { OperationId = Guid.NewGuid() }, default); Assert.Equal(80, (await w.WalletAsync(f.Alice, default)).Available); Assert.Equal(0, (await w.WalletAsync(f.Alice, default)).Reserved);
        await Assert.ThrowsAsync<CoreAccessException>(() => w.GrantAsync(new(f.Tenant, "not-admin"), new(Guid.NewGuid(), f.User, 100, DateTimeOffset.UtcNow.AddDays(1), "fixture:unauthorized"), default));
    }
    [SqlFact]
    public async Task Duplicate_out_of_order_refunds_reconcile_debt_without_new_revenue_and_pin_original_provider()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CustomerTestFixture.Setup(s); var w = s.GetRequiredService<ICustomerWorkbench>(); var (product, route) = await CustomerTestFixture.Catalog(w, f); var purchase = new CustomerCheckout(Guid.NewGuid(), product, "Instagram"); var id = await w.CheckoutAsync(f.Alice, purchase, default); Assert.Equal(id, await w.CheckoutAsync(f.Alice, purchase, default)); var o = await w.SubmitAsync(f.Alice, id, default); Assert.Equal("Pending", o.State); Assert.Equal(0, (await w.WalletAsync(f.Alice, default)).Available);
        var paid = CustomerTestFixture.Evidence(o); var eventId = await CustomerTestFixture.Send(w, f, paid); Assert.Equal(eventId, await CustomerTestFixture.Send(w, f, paid)); Assert.Equal(100, (await w.WalletAsync(f.Alice, default)).Available);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.WebhookAsync(f.Tenant, FixturePaymentProvider.Sign(paid with { EventId = "wrong-signature" }, "wrong-signing-key-long-enough"), default)); await Assert.ThrowsAsync<RuntimeValidationException>(() => CustomerTestFixture.Send(w, f, paid with { EventId = "wrong-customer", Sequence = 2, CustomerReference = "fixture:other" }));
        var r = await w.ReserveAsync(f.Alice, new(Guid.NewGuid(), f.Profile, "consumed-before-refund", 60), default); await w.DispatchAsync(f.Admin, r, new(Guid.NewGuid(), "fixture.generator", "original", "resource"), default); await w.SettleAsync(f.Admin, r, new(Guid.NewGuid(), "fixture.generator", "original", "resource", "Completed", 60, "fixture:accepted-work"), default);
        var b = await w.RouteAsync(f.Admin, new(Guid.NewGuid(), "payments", 2, "payment.fixture.b.v1", "replacement-account"), default); await w.ControlRouteAsync(f.Admin, b, new(Guid.NewGuid(), true, true), default);
        await w.ControlRouteAsync(f.Admin, route, new(Guid.NewGuid(), true, true), default);
        await w.ControlRouteAsync(f.Admin, b, new(Guid.NewGuid(), true, true), default);
        Assert.Single(await w.RoutesAsync(f.Admin, default), x => x.Active);
        var old = await w.ChangePaymentAsync(f.Alice, id, new(Guid.NewGuid(), "Refund", 1000), default); Assert.Equal("payment.fixture.a.v1", old.Provider); Assert.Equal(o.CustomerReference, old.CustomerReference);
        var refund = CustomerTestFixture.Evidence(o, 3, "Refunded", 1000, paid.OccurredUtc); await CustomerTestFixture.Send(w, f, refund); await CustomerTestFixture.Send(w, f, refund); await CustomerTestFixture.Send(w, f, paid with { EventId = "delayed-paid", Sequence = 2 }); Assert.Equal(-60, (await w.WalletAsync(f.Alice, default)).Available); Assert.Equal(100, (await w.WalletAsync(f.Alice, default)).Reversed);
        var reconciled = await w.ReconcileAsync(f.Alice, id, default); Assert.Equal("Refunded", reconciled.State); Assert.Equal(o.CustomerReference, reconciled.CustomerReference); Assert.Equal("payment.fixture.a.v1", reconciled.Provider);
        var newer = await w.CheckoutAsync(f.Alice, new(Guid.NewGuid(), product), default); Assert.Equal("payment.fixture.b.v1", (await w.SubmitAsync(f.Alice, newer, default)).Provider); Assert.Equal(f.User, (await w.MeAsync(f.Alice, default)).Id);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ReserveAsync(f.Alice, new(Guid.NewGuid(), f.Profile, "debt-blocks", 1), default));
    }
    [SqlFact]
    public async Task Refund_before_paid_and_unknown_submission_recover_after_restart_without_duplicate_purchase()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CustomerTestFixture.Setup(s); var w = s.GetRequiredService<ICustomerWorkbench>(); var (product, route) = await CustomerTestFixture.Catalog(w, f, provider: "payment.unknown.v1"); var id = await w.CheckoutAsync(f.Alice, new(Guid.NewGuid(), product), default); var o = await w.SubmitAsync(f.Alice, id, default); Assert.Equal("Unknown", o.State); Assert.Equal("Unknown", (await w.SubmitAsync(f.Alice, id, default)).State);
        var paidTime = DateTimeOffset.UtcNow; var refund = CustomerTestFixture.Evidence(o, 3, "Refunded", 1000, paidTime); await CustomerTestFixture.Send(w, f, refund); await CustomerTestFixture.Send(w, f, CustomerTestFixture.Evidence(o, 1, "Paid", paidUtc: paidTime));
        using var restarted = CustomerTestFixture.Services(); using var next = restarted.CreateScope(); var rw = next.ServiceProvider.GetRequiredService<ICustomerWorkbench>(); Assert.Equal("Refunded", (await rw.ReconcileAsync(f.Alice, id, default)).State); var balance = await rw.WalletAsync(f.Alice, default); Assert.Equal(0, balance.Available); Assert.Single(balance.Entries, x => x.Kind == "Purchase"); Assert.Single(balance.Entries, x => x.Kind == "Reversal");
    }
    [SqlFact]
    public async Task Subscription_access_comes_from_signed_current_period_and_cancel_does_not_resurrect_from_old_events()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var f = await CustomerTestFixture.Setup(scope.ServiceProvider); var w = scope.ServiceProvider.GetRequiredService<ICustomerWorkbench>(); var (product, route) = await CustomerTestFixture.Catalog(w, f, "Subscription"); var id = await w.CheckoutAsync(f.Alice, new(Guid.NewGuid(), product), default); var o = await w.SubmitAsync(f.Alice, id, default); Assert.Empty((await w.EntitlementsAsync(f.Alice, default)).Features);
        var paid = CustomerTestFixture.Evidence(o, 2); await CustomerTestFixture.Send(w, f, paid); var features = await w.EntitlementsAsync(f.Alice, default); Assert.Contains("GeetaPlus", features.Features); Assert.True(features.FixtureOnly); Assert.False(features.LiveQualified);
        await CustomerTestFixture.Send(w, f, CustomerTestFixture.Evidence(o, 1, "Failed")); Assert.Contains("GeetaPlus", (await w.EntitlementsAsync(f.Alice, default)).Features);
        await w.ChangePaymentAsync(f.Alice, id, new(Guid.NewGuid(), "Cancel"), default); await CustomerTestFixture.Send(w, f, CustomerTestFixture.Evidence(o, 3, "Cancelled")); Assert.Empty((await w.EntitlementsAsync(f.Alice, default)).Features);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => CustomerTestFixture.Send(w, f, CustomerTestFixture.Evidence(o, 4)));
        var notices = await w.NotificationsAsync(f.Alice, default); Assert.Equal(2, notices.Length); await w.DeliverNoticeAsync(f.Alice, notices[0].Id, new(Guid.NewGuid()), default); Assert.StartsWith("inbox:", (await w.NotificationsAsync(f.Alice, default)).Single(x => x.Id == notices[0].Id).Receipt);
    }
}
