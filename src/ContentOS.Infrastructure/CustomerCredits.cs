using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using static ContentOS.Application.CustomerRules;
namespace ContentOS.Infrastructure;

public sealed partial class CustomerWorkbench
{
    private async Task<CustomerCredit[]> Ledger(Guid tenant, Guid user, CancellationToken ct)
    {
        var rows = await db.CustomerCredits.AsNoTracking().Where(x => x.TenantId == tenant && x.UserId == user).OrderBy(x => x.CreatedUtc).Take(10001).ToArrayAsync(ct);
        Require(rows.Length <= 10000, "Wallet exceeds the operational bound; reconcile/archive before continuing.");
        return rows.Concat(db.ChangeTracker.Entries<CustomerCredit>().Where(x => x.State == EntityState.Added && x.Entity.TenantId == tenant && x.Entity.UserId == user).Select(x => x.Entity)).ToArray();
    }
    private static long Available(IEnumerable<CustomerCredit> rows) => rows.Sum(x => x.Kind is "Grant" or "Purchase" or "Release" or "ExpiryAdjustment" ? x.Amount : x.Kind is "Reserve" or "Expiry" or "Reversal" ? -x.Amount : 0);
    private static long Held(IEnumerable<CustomerCredit> rows) => rows.Sum(x => x.Kind == "Reserve" ? x.Amount : x.Kind is "Consume" or "Release" ? -x.Amount : 0);
    private CustomerCredit Entry(CustomerReservation r, Guid lot, string kind, long amount, string evidence)
    { var e = new CustomerCredit { Id = Guid.NewGuid(), TenantId = r.TenantId, UserId = r.UserId, ReservationId = r.Id, LotId = lot, Kind = kind, Amount = amount, Evidence = evidence }; db.CustomerCredits.Add(e); return e; }
    private async Task Expire(Guid tenant, Guid user, CancellationToken ct)
    {
        foreach (var r in await db.CustomerReservations.Where(x => x.TenantId == tenant && x.UserId == user && x.State == "Reserved" && x.ExpiresUtc <= DateTimeOffset.UtcNow).ToArrayAsync(ct)) if (r.State == "Reserved") await Finish(r, 0, "Released", "reservation-expiry", ct);
        var rows = await Ledger(tenant, user, ct);
        foreach (var lot in rows.Where(x => x.Kind is "Grant" or "Purchase" && x.ExpiresUtc <= DateTimeOffset.UtcNow))
        {
            var amount = Available(rows.Where(x => x.Id == lot.Id || x.LotId == lot.Id));
            if (amount > 0) db.CustomerCredits.Add(new() { Id = Guid.NewGuid(), TenantId = tenant, UserId = user, LotId = lot.Id, Kind = "Expiry", Amount = amount, Evidence = "credit-lot-expiry" });
        }
    }
    public Task<Guid> GrantAsync(CoreScope s, CustomerGrant r, CancellationToken ct) => Staff(s, r.OperationId, "credit-grant", r, async () =>
    { await User(s.TenantId, r.UserId, ct); Credits(r.Amount); Future(r.ExpiresUtc); Key(r.Evidence); var e = new CustomerCredit { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = r.UserId, Kind = "Grant", Amount = r.Amount, ExpiresUtc = r.ExpiresUtc, Evidence = r.Evidence }; db.CustomerCredits.Add(e); return e.Id; }, ct);
    public async Task<CustomerWallet> WalletAsync(CustomerScope s, CancellationToken ct)
    {
        var u = await Self(s, ct);
        // Reads do not silently append ledger entries. Expired unreserved credits are projected out.
        var rows = await Ledger(s.TenantId, u.Id, ct); long expiryDue = 0;
        foreach (var lot in rows.Where(x => x.Kind is "Grant" or "Purchase" && x.ExpiresUtc <= DateTimeOffset.UtcNow)) expiryDue += Math.Max(0, Available(rows.Where(x => x.Id == lot.Id || x.LotId == lot.Id)));
        return new(Available(rows) - expiryDue, Held(rows), rows.Where(x => x.Kind == "Consume").Sum(x => x.Amount), rows.Where(x => x.Kind == "Reversal").Sum(x => x.Amount), rows.Where(x => x.Kind == "Expiry").Sum(x => x.Amount) - rows.Where(x => x.Kind == "ExpiryAdjustment").Sum(x => x.Amount) + expiryDue, true,
            rows.Select(x => new CustomerCreditView(x.Id, x.Kind, x.Amount, x.LotId, x.ReservationId, x.OrderId, x.ExpiresUtc)).ToArray());
    }
    public Task<Guid> ReserveAsync(CustomerScope s, CustomerReserve r, CancellationToken ct) => Own(s, r.OperationId, "reserve", r, async u =>
    {
        Credits(r.Amount); Key(r.UsageKey); await ProfileAccess(s, r.ProfileId, u, ct); await Expire(s.TenantId, u.Id, ct);
        var old = await db.CustomerReservations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.UsageKey == r.UsageKey, ct);
        if (old != null) { if (old.Amount != r.Amount || old.ProfileId != r.ProfileId) throw new RuntimeConflictException("Usage reservation differs."); return old.Id; }
        var rows = await Ledger(s.TenantId, u.Id, ct); Require(Available(rows) >= r.Amount, "Insufficient available credits; reservations and reversals are included.");
        var reserve = new CustomerReservation { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = u.Id, ProfileId = r.ProfileId, UsageKey = r.UsageKey, Amount = r.Amount, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) }; db.CustomerReservations.Add(reserve); var remaining = r.Amount;
        foreach (var lot in rows.Where(x => x.Kind is "Grant" or "Purchase" && x.ExpiresUtc > DateTimeOffset.UtcNow).OrderBy(x => x.ExpiresUtc).ThenBy(x => x.CreatedUtc))
        { var amount = Math.Min(remaining, Math.Max(0, Available(rows.Where(x => x.Id == lot.Id || x.LotId == lot.Id)))); if (amount > 0) Entry(reserve, lot.Id, "Reserve", amount, r.UsageKey); remaining -= amount; if (remaining == 0) break; }
        Require(remaining == 0, "Credits unavailable after lot expiry/reversal reconciliation."); return reserve.Id;
    }, ct);
    public Task<Guid> CancelReservationAsync(CustomerScope s, Guid id, CustomerOperation r, CancellationToken ct) => Own(s, r.OperationId, "cancel-reservation", new { id, r }, async u =>
    { var reservation = await db.CustomerReservations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.Id == id, ct) ?? throw new CoreAccessException(); Require(reservation.State == "Reserved", "Dispatched/unknown work must settle from its original trusted service receipt."); await Finish(reservation, 0, "Released", "customer-cancel", ct); await Expire(s.TenantId, u.Id, ct); return reservation.Id; }, ct);
    public Task<Guid> DispatchAsync(CoreScope s, Guid id, CustomerDispatch r, CancellationToken ct) => Staff(s, r.OperationId, "dispatch-reservation", new { id, r }, async () =>
    {
        Key(r.Provider); Key(r.Account); Key(r.Resource); var x = await db.CustomerReservations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == id, ct) ?? throw new CoreAccessException(); await User(s.TenantId, x.UserId, ct);
        Require(x.State == "Reserved" && x.ExpiresUtc > DateTimeOffset.UtcNow, "Reservation is expired/already dispatched."); var userScopeProfile = await db.CustomerProfiles.SingleAsync(p => p.TenantId == s.TenantId && p.Id == x.ProfileId, ct); Require(!userScopeProfile.Deleted, "Profile deleted before dispatch.");
        x.Provider = r.Provider; x.Account = r.Account; x.Resource = r.Resource; x.State = "Running"; return x.Id;
    }, ct);
    private async Task Finish(CustomerReservation r, long consumed, string finalState, string evidence, CancellationToken ct)
    {
        Require(consumed >= 0 && consumed <= r.Amount, "Consumption exceeds reservation."); var rows = await Ledger(r.TenantId, r.UserId, ct); var held = rows.Where(x => x.ReservationId == r.Id).ToArray(); Require(Held(held) == r.Amount, "Reservation has already been reconciled."); var remaining = consumed;
        foreach (var allocation in held.Where(x => x.Kind == "Reserve")) { var used = Math.Min(remaining, allocation.Amount); if (used > 0) Entry(r, allocation.LotId!.Value, "Consume", used, evidence); if (used < allocation.Amount) Entry(r, allocation.LotId!.Value, "Release", allocation.Amount - used, evidence); remaining -= used; }
        r.State = finalState; r.Consumed = consumed; r.UpdatedUtc = DateTimeOffset.UtcNow;
    }
    public Task<Guid> SettleAsync(CoreScope s, Guid id, CustomerSettlement r, CancellationToken ct) => Staff(s, r.OperationId, "settle-reservation", new { id, r }, async () =>
    {
        Key(r.Evidence); var x = await db.CustomerReservations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == id, ct) ?? throw new CoreAccessException();
        Require(x.Provider == r.Provider && x.Account == r.Account && x.Resource == r.Resource && r.Outcome is "Completed" or "Failed" or "Unknown", "Original work binding/outcome required.");
        var hash = Hash(Json(new { r.Provider, r.Account, r.Resource, r.Outcome, r.Consumed, r.Evidence }));
        if (x.State is "Consumed" or "Released") { if (x.OutcomeHash != hash) throw new RuntimeConflictException("Final usage settlement differs."); return x.Id; }
        Require(x.State is "Running" or "Unknown" && (r.Outcome == "Completed" || r.Consumed == 0), "Failure/unknown cannot consume client-reported credits.");
        if (r.Outcome == "Unknown") { x.State = "Unknown"; return x.Id; }
        await Finish(x, r.Consumed, r.Outcome == "Completed" ? "Consumed" : "Released", r.Evidence, ct); x.OutcomeHash = hash; await Expire(s.TenantId, x.UserId, ct); return x.Id;
    }, ct);
    public async Task<CustomerReservationView[]> ReservationsAsync(CustomerScope s, CancellationToken ct)
    { var u = await Self(s, ct); return await db.CustomerReservations.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UserId == u.Id).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => new CustomerReservationView(x.Id, x.UsageKey, x.Amount, x.State, x.Consumed)).ToArrayAsync(ct); }
}
