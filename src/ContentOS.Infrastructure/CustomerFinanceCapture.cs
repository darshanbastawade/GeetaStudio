using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class AnalyticsWorkbench
{
    public Task<AnalyticsImportResult> CaptureCustomerPaymentsAsync(CoreScope s, Guid u, AnalyticsCapture r, CancellationToken ct) => Command(s, u, r.OperationId, "capture-customer-payments", r, async () =>
    {
        const string key = "owned-customer-payments";
        var source = await db.AnalyticsSources.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == key && x.Version == 1, ct);
        if (source == null) { source = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = key, Version = 1, SnapshotJson = Json(new AnalyticsSourceRequest(Guid.Empty, key, 1, "analytics.fixture.a.v1", "ContentOS.CustomerPayments", "local:customer-payments", "fixture:signed-local-payments")) }; db.AnalyticsSources.Add(source); }
        var binding = Read<AnalyticsSourceRequest>(source.SnapshotJson); AnalyticsRules.Require(source.Enabled && binding.SourceSystem == "ContentOS.CustomerPayments" && binding.AccountReference == "local:customer-payments" && binding.Adapter == "analytics.fixture.a.v1", "Original customer finance capture binding required.");
        var orders = await (from o in db.CustomerOrders.AsNoTracking() join p in db.CustomerProducts.AsNoTracking() on o.ProductId equals p.Id where o.TenantId == s.TenantId && p.TenantId == s.TenantId && p.UniverseId == u && o.PaidUtc != null select o).Take(10001).ToArrayAsync(ct); AnalyticsRules.Require(orders.Length <= 10000, "Customer finance capture exceeds operational limit.");
        var ids = new List<Guid>(); var inserted = 0; var duplicates = 0;
        foreach (var order in orders)
        {
            var product = Read<CustomerProductView>(order.SnapshotJson); var economic = "owned:customer-payment:" + order.Id; var stream = product.RevenueStream;
            var revenue = new AnalyticsEntryInput(economic, economic, "Revenue", order.PaidUtc!.Value, "fixture:verified-payment:" + order.Id, AcquisitionChannel: order.Acquisition, RevenueStream: stream, Currency: product.Currency, Gross: product.PriceMinor / 100m);
            var first = await Insert(s, u, source, [revenue], true, ct); ids.AddRange(first.EntryIds); inserted += first.Inserted; duplicates += first.Duplicates;
            var prior = await db.AnalyticsEntries.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.RelatedEntryId == first.EntryIds[0] && x.Kind == "Refund").ToArrayAsync(ct);
            var capturedRefund = prior.Sum(x => Read<AnalyticsEntryInput>(x.SnapshotJson).Amount);
            var delta = order.RefundedMinor / 100m - capturedRefund;
            AnalyticsRules.Require(delta >= 0, "Captured refund watermark cannot decrease.");
            if (delta > 0)
            {
                var latest = await db.CustomerPaymentEvents.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.OrderId == order.Id).OrderByDescending(x => x.Sequence).FirstAsync(ct);
                var observation = Read<CustomerPaymentEvidence>(latest.EventJson); var refundKey = "owned:customer-refund:" + latest.Id;
                var refund = new AnalyticsEntryInput(refundKey, refundKey, "Refund", observation.OccurredUtc, "fixture:verified-refund:" + latest.Id, AcquisitionChannel: order.Acquisition, RevenueStream: stream, Currency: product.Currency, Amount: delta, RelatedEntryId: first.EntryIds[0]);
                var next = await Insert(s, u, source, [refund], true, ct); ids.AddRange(next.EntryIds); inserted += next.Inserted; duplicates += next.Duplicates;
            }
        }
        return new AnalyticsImportResult(ids.ToArray(), inserted, duplicates, "owned.customer.fixture.v1");
    }, ct);
}
