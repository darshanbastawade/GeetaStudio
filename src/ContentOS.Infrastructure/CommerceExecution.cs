using System.Data;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed partial class CommerceWorkbench
{
    private async Task Apply(Guid id, Guid? token, CommerceReceipt receipt, CancellationToken ct)
    {
        var first = await db.CommerceDeliveries.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        await Change(first.TenantId, async () =>
        {
            var row = await db.CommerceDeliveries.SingleAsync(x => x.Id == id, ct);
            if (token.HasValue && row.Lease != token || row.State == "Delivered") return false;
            CreationRules.Require(receipt.State is "Delivered" or "Unknown" or "AuthoritativeNotFound" or "Withheld" && receipt.Cost == 0 && receipt.Currency == "INR", "Only original zero-cost fixture delivery receipts are qualified.");
            if (receipt.State == "Delivered")
            {
                // An authoritative existing effect remains truthful after a later refund.
                CreationRules.Require(!string.IsNullOrWhiteSpace(receipt.Resource), "Authoritative original delivery resource required.");
                row.State = "Delivered"; row.Receipt = receipt.Resource; row.Cost = receipt.Cost; row.Currency = receipt.Currency;
            }
            else
            {
                row.State = receipt.State == "Withheld" ? "Withheld" : "Unknown";
                try { await ValidateDelivery(row.TenantId, row.OrderId, ct); }
                catch (Exception e) when (e is RuntimeValidationException or RightsBlockedException or EvidenceBlockedException or CoreAccessException) { row.State = "Withheld"; }
            }
            row.Lease = null; row.LeaseUntilUtc = null; return true;
        }, ct);
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow; var first = await db.CommerceDeliveries.AsNoTracking().Where(x => x.State == "Queued" || x.State == "Submitting" && x.LeaseUntilUtc < now).OrderBy(x => x.CreatedUtc).FirstOrDefaultAsync(ct); if (first == null) return false;
        var token = Guid.NewGuid(); var claimed = await Change(first.TenantId, async () => { var row = await db.CommerceDeliveries.SingleAsync(x => x.Id == first.Id, ct); if (row.State == "Submitting") { if (row.LeaseUntilUtc > DateTimeOffset.UtcNow) return false; row.State = "Unknown"; row.Lease = null; row.LeaseUntilUtc = null; return false; } if (row.State != "Queued") return false; var order = await db.CommerceOrders.SingleAsync(x => x.Id == row.OrderId, ct); try { await CurrentPaid(order, ct); await Guard(await db.CommerceProducts.SingleAsync(x => x.Id == order.ProductId, ct), ct); } catch (Exception e) when (e is RuntimeValidationException or RightsBlockedException or EvidenceBlockedException or CoreAccessException) { row.State = "Withheld"; order.State = "Withheld"; return false; } row.State = "Submitting"; row.Lease = token; row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(2); return true; }, ct); if (!claimed) return true;
        CommerceReceipt receipt; var dispatch = Read<CommerceDispatch>(first.RequestJson) with { LeaseToken = token };
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(TimeSpan.FromSeconds(30));
            using var stop = new CancellationTokenSource(); var watch = WatchDelivery(dispatch, limit, stop.Token);
            try { receipt = await Adapter(dispatch.Adapter).SubmitAsync(dispatch, limit.Token); }
            finally { await stop.CancelAsync(); await watch; }
        }
        catch (Exception) { receipt = new("Unknown"); }
        await Apply(first.Id, token, receipt, CancellationToken.None); return true;
    }
    private async Task WatchDelivery(CommerceDispatch dispatch, CancellationTokenSource execution, CancellationToken stop)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await timer.WaitForNextTickAsync(stop))
            {
                await using var check = new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                var now = DateTimeOffset.UtcNow;
                var allowed = await (from d in check.CommerceDeliveries.AsNoTracking()
                                     join o in check.CommerceOrders on d.OrderId equals o.Id
                                     join pay in check.CustomerOrders on o.PaymentOrderId equals pay.Id
                                     join p in check.CommerceProducts on o.ProductId equals p.Id
                                     where d.Id == dispatch.DeliveryId && d.TenantId == dispatch.TenantId && d.State == "Submitting" && d.Lease == dispatch.LeaseToken && d.LeaseUntilUtc > now
                                         && o.State == "Active" && pay.State == "Paid" && pay.RefundedMinor == 0 && pay.EndsUtc > now && p.Enabled
                                     select d.Id).AnyAsync(stop);
                if (!allowed) { execution.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception) { execution.Cancel(); }
    }
}
public sealed class SqlCommerceDeliveryLedger(IDbContextFactory<CoreDbContext> factory, IServiceScopeFactory scopes) : ICommerceDeliveryLedger
{
    public async Task<CommerceReceipt> CommitAsync(CommerceDispatch r, CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Same lock order as commerce operations. Payment callbacks share Customers;
        // the paid-state read and local effect commit cannot straddle a committed refund.
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Commerce:" + r.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 51000,'Commerce lock unavailable',1;", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Customers:" + r.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 51000,'Customer lock unavailable',1;", ct);
        var delivery = await db.CommerceDeliveries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.DeliveryId && x.TenantId == r.TenantId && x.OrderId == r.OrderId, ct) ?? throw new CoreAccessException(); var pinned = EditorialBinding.Read<CommerceDispatch>(delivery.RequestJson); var hash = EditorialBinding.Hash(r); CreationRules.Require(hash == EditorialBinding.Hash(pinned), "Pinned original delivery binding differs.");
        var old = await db.CommerceEffects.SingleOrDefaultAsync(x => x.DeliveryId == r.DeliveryId, ct); if (old != null) { CreationRules.Require(old.Fingerprint == hash, "Delivery effect replay differs."); return EditorialBinding.Read<CommerceReceipt>(old.OutcomeJson); }
        if (r.LeaseToken == null || delivery.State != "Submitting" || delivery.Lease != r.LeaseToken || delivery.LeaseUntilUtc == null || delivery.LeaseUntilUtc <= DateTimeOffset.UtcNow) return new("Withheld");
        try { await ((CommerceWorkbench)scope.ServiceProvider.GetRequiredService<ICommerceWorkbench>()).ValidateDelivery(r.TenantId, r.OrderId, ct); }
        catch (Exception e) when (e is RuntimeValidationException or RightsBlockedException or EvidenceBlockedException or CoreAccessException) { return new("Withheld"); }
        if (delivery.LeaseUntilUtc <= DateTimeOffset.UtcNow) return new("Withheld");
        ct.ThrowIfCancellationRequested();
        var receipt = new CommerceReceipt("Delivered", "fixture-delivery:" + r.DeliveryId.ToString("N")); db.CommerceEffects.Add(new() { Id = Guid.NewGuid(), TenantId = r.TenantId, DeliveryId = r.DeliveryId, Fingerprint = hash, OutcomeJson = EditorialBinding.Json(receipt) }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return receipt;
    }
    public async Task<CommerceReceipt> FindAsync(CommerceDispatch r, CancellationToken ct) { await using var db = await factory.CreateDbContextAsync(ct); var row = await db.CommerceEffects.AsNoTracking().SingleOrDefaultAsync(x => x.DeliveryId == r.DeliveryId && x.TenantId == r.TenantId, ct); if (row == null) return new("AuthoritativeNotFound"); CreationRules.Require(row.Fingerprint == EditorialBinding.Hash(r), "Original fulfillment binding differs."); return EditorialBinding.Read<CommerceReceipt>(row.OutcomeJson); }
}
