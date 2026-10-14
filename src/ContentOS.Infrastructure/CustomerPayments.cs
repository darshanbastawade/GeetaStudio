using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using static ContentOS.Application.CustomerRules;
namespace ContentOS.Infrastructure;

public sealed partial class CustomerWorkbench
{
    private static CustomerProductView View(CustomerProduct p) => new(p.Id, p.Key, p.Version, p.Kind, p.Currency, p.PriceMinor, p.Credits, p.Days, Read<string[]>(p.FeaturesJson), UniverseId: p.UniverseId, RevenueStream: p.RevenueStream);
    public async Task<CustomerProductView[]> ProductsAsync(CustomerScope s, CancellationToken ct)
    { await Self(s, ct); return (await db.CustomerProducts.AsNoTracking().Where(x => x.TenantId == s.TenantId).OrderBy(x => x.Key).ThenByDescending(x => x.Version).Take(100).ToArrayAsync(ct)).Select(View).ToArray(); }
    public Task<Guid> ProductAsync(CoreScope s, CustomerProductRequest r, CancellationToken ct) => Staff(s, r.OperationId, "product", r, async () =>
    {
        if (r.UniverseId != null && !await db.Universes.AnyAsync(x => x.TenantId == s.TenantId && x.Id == r.UniverseId, ct)) throw new CoreAccessException();
        Key(r.Key); Require(r.Version > 0 && r.Kind is "Subscription" or "Credits" or "Digital" or "Institutional" && r.Currency is "INR" or "USD" or "EUR" && r.PriceMinor is > 0 and <= 1000000000 && r.Credits is >= 0 and <= 1000000000 && r.Days is > 0 and <= 730 && r.Features is { Length: <= 20 }, "Bounded product/price/period required.");
        Require(r.Kind != "Credits" || r.Credits > 0 && r.Features.Length == 0, "Credit pack needs credits, not subscription entitlements."); foreach (var f in r.Features) Key(f); Require(r.Features.Distinct().Count() == r.Features.Length, "Duplicate feature.");
        Require(r.Kind is not ("Digital" or "Institutional") || r.Credits == 0 && r.Features.Length > 0, "Licensed products grant access, never mint customer credits.");
        var stream = string.IsNullOrEmpty(r.RevenueStream) ? (r.Kind == "Subscription" ? "GeetaPlusSubscription" : "CreateCredits") : r.RevenueStream; Require(r.Kind switch { "Subscription" => stream == "GeetaPlusSubscription", "Digital" => stream == "BooksComicsPacks", "Institutional" => stream == "InstitutionalContract", _ => stream is "TalkCredits" or "CreateCredits" }, "Product revenue stream must match its owned kind.");
        var p = new CustomerProduct { Id = Guid.NewGuid(), TenantId = s.TenantId, Key = r.Key, Version = r.Version, UniverseId = r.UniverseId, RevenueStream = stream, Kind = r.Kind, Currency = r.Currency, PriceMinor = r.PriceMinor, Credits = r.Credits, Days = r.Days, FeaturesJson = Json(r.Features) }; db.CustomerProducts.Add(p); return p.Id;
    }, ct);
    public Task<Guid> RouteAsync(CoreScope s, CustomerRouteRequest r, CancellationToken ct) => Staff(s, r.OperationId, "payment-route", r, () =>
    { Key(r.Key); Key(r.Account); Require(r.Version > 0 && r.Environment == "Test" && payments.Any(x => x.Key == r.Provider), "Only deployed test payment adapters are qualified locally; live charges disabled."); var row = new CustomerPaymentRoute { Id = Guid.NewGuid(), TenantId = s.TenantId, Key = r.Key, Version = r.Version, Provider = r.Provider, Account = r.Account, Environment = r.Environment }; db.CustomerPaymentRoutes.Add(row); return Task.FromResult(row.Id); }, ct);
    public Task<Guid> ControlRouteAsync(CoreScope s, Guid id, CustomerRouteControl r, CancellationToken ct) => Staff(s, r.OperationId, "route-control", new { id, r }, async () =>
    {
        var row = await db.CustomerPaymentRoutes.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == id, ct) ?? throw new CoreAccessException(); Require(!r.Active || r.Enabled, "Active route must be enabled.");
        if (r.Active)
        {
            foreach (var old in await db.CustomerPaymentRoutes.Where(x => x.TenantId == s.TenantId && x.Active).ToArrayAsync(ct)) old.Active = false;
            // Preserve the filtered uniqueness fence: retire first, then activate, under the same tenant transaction.
            await db.SaveChangesAsync(ct);
        }
        row.Enabled = r.Enabled; row.Active = r.Active; return row.Id;
    }, ct);
    public async Task<CustomerRouteView[]> RoutesAsync(CoreScope s, CancellationToken ct)
    { await Admin(s, ct); return await db.CustomerPaymentRoutes.AsNoTracking().Where(x => x.TenantId == s.TenantId).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => new CustomerRouteView(x.Id, x.Key, x.Version, x.Provider, x.Account, x.Environment, x.Enabled, x.Active)).ToArrayAsync(ct); }
    public Task<Guid> CheckoutAsync(CustomerScope s, CustomerCheckout r, CancellationToken ct) => Own(s, r.OperationId, "checkout", r, async u =>
    {
        Key(r.Acquisition); var product = await db.CustomerProducts.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == r.ProductId, ct) ?? throw new CoreAccessException();
        var route = await db.CustomerPaymentRoutes.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Active && x.Enabled, ct) ?? throw new RuntimeValidationException("No qualified active test payment route.");
        Require(payments.Any(x => x.Key == route.Provider) && route.Environment == "Test", "Pinned payment adapter unavailable.");
        var binding = await db.CustomerPaymentBindings.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.Provider == route.Provider && x.Account == route.Account && x.Environment == route.Environment, ct);
        if (binding == null) { binding = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = u.Id, Provider = route.Provider, Account = route.Account, Environment = route.Environment, CustomerReference = "fixture-customer:" + Guid.NewGuid().ToString("N") }; db.CustomerPaymentBindings.Add(binding); }
        var order = new CustomerOrder { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = u.Id, ProductId = product.Id, BindingId = binding.Id, RouteId = route.Id, SnapshotJson = Json(View(product)), Acquisition = r.Acquisition }; db.CustomerOrders.Add(order); return order.Id;
    }, ct);
    private async Task<CustomerPaymentDispatch> Dispatch(CustomerOrder o, CancellationToken ct)
    {
        var b = await db.CustomerPaymentBindings.AsNoTracking().SingleAsync(x => x.TenantId == o.TenantId && x.Id == o.BindingId && x.UserId == o.UserId, ct);
        var route = await db.CustomerPaymentRoutes.AsNoTracking().SingleAsync(x => x.TenantId == o.TenantId && x.Id == o.RouteId, ct);
        Require(b.Provider == route.Provider && b.Account == route.Account && b.Environment == route.Environment, "Original payment binding mismatch.");
        return new(o.Id, b.Provider, b.Account, b.Environment, b.CustomerReference, o.ExternalReference, Read<CustomerProductView>(o.SnapshotJson));
    }
    private async Task<CustomerOrderView> OrderView(CustomerOrder o, CancellationToken ct)
    { var d = await Dispatch(o, ct); return new(o.Id, o.State, d.Provider, d.Account, d.Environment, d.CustomerReference, o.ExternalReference, o.SubscriptionReference, d.Product.PriceMinor, d.Product.Currency, o.RefundedMinor, o.EndsUtc, d.Product.Features); }
    private async Task<CustomerOrder> Order(CustomerScope s, Guid id, CancellationToken ct)
    { var u = await Self(s, ct); return await db.CustomerOrders.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.Id == id, ct) ?? throw new CoreAccessException(); }
    public async Task<CustomerOrderView[]> OrdersAsync(CustomerScope s, CancellationToken ct)
    { var u = await Self(s, ct); var rows = await db.CustomerOrders.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UserId == u.Id).OrderByDescending(x => x.CreatedUtc).Take(100).ToArrayAsync(ct); var result = new List<CustomerOrderView>(); foreach (var row in rows) result.Add(await OrderView(row, ct)); return result.ToArray(); }
    public async Task<CustomerOrderView> SubmitAsync(CustomerScope s, Guid id, CancellationToken ct)
    {
        Guid? claim = null; CustomerPaymentDispatch? request = null;
        await Own(s, Guid.NewGuid(), "submit-claim", new { id }, async u =>
        {
            var o = await db.CustomerOrders.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.Id == id, ct) ?? throw new CoreAccessException();
            if (o.State != "Created") return o.Id;
            Require(await db.CustomerPaymentRoutes.AnyAsync(x => x.TenantId == s.TenantId && x.Id == o.RouteId && x.Enabled, ct), "Pinned route disabled; do not fallback to another provider.");
            claim = Guid.NewGuid(); o.Lease = claim; o.LeaseEndsUtc = DateTimeOffset.UtcNow.AddMinutes(2); o.State = "Submitting"; request = await Dispatch(o, ct); Require(payments.Any(x => x.Key == request.Provider), "Original payment adapter unavailable; no dispatch or fallback."); return o.Id;
        }, ct);
        if (claim == null) return await OrderView(await Order(s, id, ct), ct);
        CustomerPaymentOutcome outcome;
        try { outcome = await payments.Single(x => x.Key == request!.Provider).SubmitAsync(request!, ct); }
        catch (Exception) { outcome = new("Unknown"); }
        await Command(s.TenantId, Actor(s), Guid.NewGuid(), "submit-receipt", new { id, claim, outcome.State, outcome.ExternalReference }, async () =>
        { var o = await db.CustomerOrders.SingleAsync(x => x.TenantId == s.TenantId && x.Id == id, CancellationToken.None); if (o.Lease != claim || o.State != "Submitting") return o.Id; Require(outcome.State is "Pending" or "Unknown", "Client/submit success cannot grant payment entitlement; reconcile signed evidence."); o.State = outcome.State; o.ExternalReference = outcome.ExternalReference; o.Lease = null; return o.Id; }, CancellationToken.None);
        return await OrderView(await Order(s, id, CancellationToken.None), CancellationToken.None);
    }
    private async Task Apply(CustomerOrder o, CustomerPaymentEvidence e, CancellationToken ct)
    {
        if (e.Sequence <= o.Sequence) return;
        var product = Read<CustomerProductView>(o.SnapshotJson);
        Require(e.RefundedMinor >= o.RefundedMinor && (o.RefundedMinor == 0 || e.State != "Paid") && (o.State != "Cancelled" || e.State != "Paid"), "Refund/cancellation cannot be silently reversed into new access/revenue.");
        if (e.State is "Paid" or "Refunded")
        {
            var paidUtc = e.PaidUtc ?? (e.State == "Paid" ? e.OccurredUtc : o.PaidUtc); Require(paidUtc != null && paidUtc <= e.OccurredUtc, "Refund requires original verified payment time."); o.PaidUtc ??= paidUtc;
            if (product.Credits > 0)
            {
                var lot = await db.CustomerCredits.SingleOrDefaultAsync(x => x.TenantId == o.TenantId && x.UserId == o.UserId && x.OrderId == o.Id && x.Kind == "Purchase", ct);
                if (lot == null) { lot = new() { Id = Guid.NewGuid(), TenantId = o.TenantId, UserId = o.UserId, OrderId = o.Id, Kind = "Purchase", Amount = product.Credits, ExpiresUtc = paidUtc!.Value.AddDays(product.Days), Evidence = "verified-payment:" + o.Id.ToString("N") }; db.CustomerCredits.Add(lot); }
                var previous = await db.CustomerCredits.Where(x => x.TenantId == o.TenantId && x.UserId == o.UserId && x.OrderId == o.Id && x.Kind == "Reversal").SumAsync(x => x.Amount, ct);
                var reversal = RefundCredits(product.Credits, e.RefundedMinor, product.PriceMinor) - previous;
                if (reversal > 0)
                {
                    var expired = await db.CustomerCredits.Where(x => x.TenantId == o.TenantId && x.UserId == o.UserId && x.LotId == lot.Id && x.Kind == "Expiry").SumAsync(x => x.Amount, ct) - await db.CustomerCredits.Where(x => x.TenantId == o.TenantId && x.UserId == o.UserId && x.LotId == lot.Id && x.Kind == "ExpiryAdjustment").SumAsync(x => x.Amount, ct);
                    if (expired > 0) db.CustomerCredits.Add(new() { Id = Guid.NewGuid(), TenantId = o.TenantId, UserId = o.UserId, OrderId = o.Id, LotId = lot.Id, Kind = "ExpiryAdjustment", Amount = Math.Min(expired, reversal), Evidence = "verified-expired-refund:" + e.EventId });
                    db.CustomerCredits.Add(new() { Id = Guid.NewGuid(), TenantId = o.TenantId, UserId = o.UserId, OrderId = o.Id, LotId = lot.Id, Kind = "Reversal", Amount = reversal, Evidence = "verified-refund:" + e.EventId });
                }
            }
        }
        o.State = e.State; o.Sequence = e.Sequence; o.RefundedMinor = e.RefundedMinor; o.ExternalReference ??= e.ExternalReference; o.SubscriptionReference ??= e.SubscriptionReference; o.EndsUtc = e.EndsUtc; o.RequestedAction = null; o.Lease = null; o.UpdatedUtc = DateTimeOffset.UtcNow;
        var key = o.Id.ToString("N") + ":" + e.Sequence; db.CustomerNotifications.Add(new() { Id = Guid.NewGuid(), TenantId = o.TenantId, UserId = o.UserId, OrderId = o.Id, Key = key, Kind = e.State == "Refunded" ? "RefundAndCreditReversal" : e.State == "Failed" ? "PaymentFailure" : e.State == "Cancelled" ? "SubscriptionCancelled" : "PaymentConfirmed" });
    }
    public async Task<Guid> WebhookAsync(Guid tenant, CustomerSignedWebhook r, CancellationToken ct)
    {
        Require(r.Payload is { Length: <= 16000 }, "Bounded payment webhook required."); var untrusted = Read<CustomerPaymentEvidence>(r.Payload); var provider = payments.SingleOrDefault(x => x.Key == untrusted.Provider) ?? throw new RuntimeValidationException("Payment adapter not deployed.");
        var e = provider.VerifyWebhook(r); Key(e.EventId); Key(e.Account); Key(e.CustomerReference); Key(e.ExternalReference); Require(e.SubscriptionReference == null || e.SubscriptionReference.Length <= 100, "Subscription reference too long.");
        Require(e.Sequence is > 0 and <= 1000000000 && e.State is "Paid" or "Failed" or "Cancelled" or "Refunded" && e.OccurredUtc <= DateTimeOffset.UtcNow.AddMinutes(5), "Invalid canonical payment event.");
        var fingerprint = Hash(r.Payload);
        return await Command(tenant, "payment:" + e.Provider + ":" + e.Account, Guid.NewGuid(), "payment-event", new { e.EventId, fingerprint }, async () =>
        {
            var old = await db.CustomerPaymentEvents.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Provider == e.Provider && x.Account == e.Account && x.Environment == e.Environment && x.EventId == e.EventId, ct);
            if (old != null) { if (old.Fingerprint != fingerprint) throw new RuntimeConflictException("Payment event replay changed."); return old.Id; }
            var o = await db.CustomerOrders.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == e.OrderId, ct) ?? throw new CoreAccessException(); var d = await Dispatch(o, ct);
            Require(o.State != "Created" && d.Provider == e.Provider && d.Account == e.Account && d.Environment == e.Environment && d.CustomerReference == e.CustomerReference && (o.ExternalReference == null || o.ExternalReference == e.ExternalReference) && e.PriceMinor == d.Product.PriceMinor && e.Currency == d.Product.Currency && e.RefundedMinor >= 0 && e.RefundedMinor <= d.Product.PriceMinor && e.OccurredUtc >= o.CreatedUtc.AddMinutes(-5), "Original order/customer/price/currency/environment binding mismatch.");
            Require(e.State == "Refunded" ? e.RefundedMinor > 0 : e.RefundedMinor == o.RefundedMinor || e.Sequence < o.Sequence, "Refund must use canonical refund state.");
            if (d.Product.Kind == "Subscription" && e.State == "Paid") Require(!string.IsNullOrEmpty(e.SubscriptionReference) && e.EndsUtc > e.OccurredUtc && e.EndsUtc <= e.OccurredUtc.AddDays(d.Product.Days + 1), "Verified subscription period/resource required.");
            if (d.Product.Kind is "Digital" or "Institutional" && e.State == "Paid") Require(e.EndsUtc > e.OccurredUtc && e.EndsUtc <= e.OccurredUtc.AddDays(d.Product.Days + 1), "Verified bounded licensed-access period required.");
            Require(o.SubscriptionReference == null || e.SubscriptionReference == o.SubscriptionReference, "Subscription resource changed; explicit migration required.");
            if (await db.CustomerPaymentEvents.AnyAsync(x => x.TenantId == tenant && x.OrderId == o.Id && x.Sequence == e.Sequence, ct)) throw new RuntimeConflictException("Provider sequence already has another event.");
            var previousRefund = await db.CustomerPaymentEvents.Where(x => x.TenantId == tenant && x.OrderId == o.Id && x.Sequence < e.Sequence).OrderByDescending(x => x.Sequence).Select(x => x.EventJson).FirstOrDefaultAsync(ct);
            var laterRefund = await db.CustomerPaymentEvents.Where(x => x.TenantId == tenant && x.OrderId == o.Id && x.Sequence > e.Sequence).OrderBy(x => x.Sequence).Select(x => x.EventJson).FirstOrDefaultAsync(ct);
            Require((previousRefund == null || Read<CustomerPaymentEvidence>(previousRefund).RefundedMinor <= e.RefundedMinor) && (laterRefund == null || Read<CustomerPaymentEvidence>(laterRefund).RefundedMinor >= e.RefundedMinor), "Refund observations contradict their original provider sequence.");
            var row = new CustomerPaymentEvent { Id = Guid.NewGuid(), TenantId = tenant, OrderId = o.Id, RouteId = o.RouteId, Provider = e.Provider, Account = e.Account, Environment = e.Environment, EventId = e.EventId, Fingerprint = fingerprint, Sequence = e.Sequence, EventJson = Json(e) }; db.CustomerPaymentEvents.Add(row); await Apply(o, e, ct); return row.Id;
        }, ct);
    }
    public async Task<CustomerOrderView> ReconcileAsync(CustomerScope s, Guid id, CancellationToken ct)
    {
        var order = await Order(s, id, ct); Require(order.State != "Created", "Submit once before reconciliation."); Require(order.State != "Submitting" || order.LeaseEndsUtc <= DateTimeOffset.UtcNow, "Payment dispatch is in flight.");
        var d = await Dispatch(order, ct); var outcome = await payments.Single(x => x.Key == d.Provider).ReconcileAsync(d, ct);
        await Own(s, Guid.NewGuid(), "payment-reconcile", new { id, outcome.State }, async _ => { var o = await db.CustomerOrders.SingleAsync(x => x.TenantId == s.TenantId && x.Id == id, ct); if (outcome.Evidence != null) await Apply(o, outcome.Evidence, ct); else if (o.State is "Unknown" or "Submitting") { o.State = "Pending"; o.ExternalReference ??= outcome.ExternalReference; o.Lease = null; } return o.Id; }, ct);
        return await OrderView(await Order(s, id, ct), ct);
    }
    public async Task<CustomerOrderView> ChangePaymentAsync(CustomerScope s, Guid id, CustomerPaymentAction r, CancellationToken ct)
    {
        CustomerPaymentDispatch? request = null; var submit = false;
        await Own(s, r.OperationId, "payment-change", new { id, r }, async u =>
        {
            var o = await db.CustomerOrders.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.Id == id, ct) ?? throw new CoreAccessException(); var d = await Dispatch(o, ct);
            Require(o.RequestedAction == null && o.State is "Paid" or "Pending" && r.Action is "Refund" or "Cancel", "Reconcile original pending action before another request.");
            if (r.Action == "Refund") Require(o.State == "Paid" && r.AmountMinor > 0 && r.AmountMinor <= d.Product.PriceMinor - o.RefundedMinor, "Refund exceeds remaining original payment."); else Require(r.AmountMinor == 0 && (d.Product.Kind == "Subscription" || o.State == "Pending"), "Cancellation has no client amount.");
            o.RequestedAction = Json(r); request = d; submit = true; return o.Id;
        }, ct);
        if (submit) { try { await payments.Single(x => x.Key == request!.Provider).ChangeAsync(request!, r, ct); } catch (Exception) { /* Persisted original action remains unknown; reconcile, never fallback/resubmit. */ } }
        return await OrderView(await Order(s, id, CancellationToken.None), CancellationToken.None);
    }
    public async Task<CustomerEntitlements> EntitlementsAsync(CustomerScope s, CancellationToken ct)
    { var u = await Self(s, ct); var rows = await db.CustomerOrders.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.State == "Paid" && x.RefundedMinor == 0 && x.EndsUtc > DateTimeOffset.UtcNow).ToArrayAsync(ct); return new(rows.SelectMany(x => Read<CustomerProductView>(x.SnapshotJson).Features).Distinct().Order().ToArray(), true, false); }
}
public sealed class CustomerPaymentEvidenceStore(CoreDbContext db) : ICustomerPaymentEvidenceStore
{
    public async Task<CustomerPaymentEvidence?> LatestAsync(CustomerPaymentDispatch r, CancellationToken ct)
    { var json = await db.CustomerPaymentEvents.AsNoTracking().Where(x => x.OrderId == r.OrderId && x.Provider == r.Provider && x.Account == r.Account && x.Environment == r.Environment).OrderByDescending(x => x.Sequence).Select(x => x.EventJson).FirstOrDefaultAsync(ct); return json == null ? null : Read<CustomerPaymentEvidence>(json); }
}
