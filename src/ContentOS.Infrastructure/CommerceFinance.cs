using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class AnalyticsWorkbench
{
    public Task<AnalyticsImportResult> CaptureCommerceAsync(CoreScope s, Guid u, AnalyticsCapture r, CancellationToken ct) => Command(s, u, r.OperationId, "capture-commerce-delivery", r, async () =>
    {
        const string key = "owned-commerce-fulfillment"; var source = await db.AnalyticsSources.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == key && x.Version == 1, ct);
        if (source == null) { source = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = key, Version = 1, SnapshotJson = Json(new AnalyticsSourceRequest(Guid.Empty, key, 1, "owned.capture.v1", "ContentOS.Commerce", "local:fulfillment", "fixture:owned-delivery-ledger")) }; db.AnalyticsSources.Add(source); }
        var binding = Read<AnalyticsSourceRequest>(source.SnapshotJson); AnalyticsRules.Require(source.Enabled && binding.Adapter == "owned.capture.v1" && binding.SourceSystem == "ContentOS.Commerce" && binding.AccountReference == "local:fulfillment", "Original owned fulfillment capture required."); var inputs = await (from d in db.CommerceDeliveries.AsNoTracking() join o in db.CommerceOrders.AsNoTracking() on d.OrderId equals o.Id join p in db.CommerceProducts.AsNoTracking() on o.ProductId equals p.Id where d.TenantId == s.TenantId && p.UniverseId == u && d.State == "Delivered" select d).Take(10001).ToArrayAsync(ct); AnalyticsRules.Require(inputs.Length <= 10000, "Narrow fulfillment capture scope.");
        var rows = inputs.Select(x => new AnalyticsEntryInput("owned:commerce-delivery:" + x.Id, "owned:commerce-delivery:" + x.Id, "Cost", x.UpdatedUtc, "fixture:delivery-receipt:" + x.Id, CostCategory: "OperatingOverhead", Currency: x.Currency, Amount: x.Cost)).ToArray();
        foreach (var old in await db.AnalyticsEntries.Where(x => x.SourceId == source.Id).ToArrayAsync(ct)) { var index = Array.FindIndex(rows, x => x.EconomicKey == old.EconomicKey); if (index >= 0) { var prior = Read<AnalyticsEntryInput>(old.SnapshotJson); AnalyticsRules.Require(rows[index].Amount == prior.Amount && rows[index].Currency == prior.Currency, "Original fulfillment cost changed."); rows[index] = prior; } }
        return rows.Length == 0 ? new AnalyticsImportResult([], 0, 0, "owned.commerce.fixture.v1") : await Insert(s, u, source, rows, true, ct);
    }, ct);
}
