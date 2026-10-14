using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using static ContentOS.Application.CustomerRules;
namespace ContentOS.Infrastructure;

public sealed partial class CustomerWorkbench
{
    private async Task<CustomerMember> Member(Guid tenant, Guid household, Guid user, bool parent, CancellationToken ct)
    {
        var m = await db.CustomerMembers.SingleOrDefaultAsync(x => x.TenantId == tenant && x.HouseholdId == household && x.UserId == user && x.Enabled, ct) ?? throw new CoreAccessException();
        if (parent && m.Role is not ("Owner" or "Parent")) throw new CoreAccessException(); return m;
    }
    private async Task<CustomerProfile> ProfileAccess(CustomerScope s, Guid id, CustomerUser user, CancellationToken ct)
    {
        var p = await db.CustomerProfiles.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == id && !x.Deleted, ct) ?? throw new CoreAccessException();
        await Member(s.TenantId, p.HouseholdId, user.Id, p.UserId != user.Id || p.AgeBand != "Adult", ct);
        if (p.AgeBand == "Adult" && p.UserId != user.Id) throw new CoreAccessException(); return p;
    }
    private async Task<bool> Consented(Guid tenant, Guid user, Guid? profile, string purpose, CancellationToken ct)
    {
        var rows = db.CustomerConsents.AsNoTracking().Where(x => x.TenantId == tenant && x.ProfileId == profile && x.Purpose == purpose);
        if (profile == null) rows = rows.Where(x => x.UserId == user);
        var last = await rows.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (last is not { Granted: true } || last.ExpiresUtc <= DateTimeOffset.UtcNow || !await db.CustomerUsers.AnyAsync(x => x.TenantId == tenant && x.Id == last.UserId && x.State == "Active", ct)) return false;
        if (profile == null) return true;
        var p = await db.CustomerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == profile && !x.Deleted, ct);
        if (p == null) return false;
        if (p.AgeBand == "Adult") return last.UserId == p.UserId;
        return await db.CustomerMembers.AnyAsync(x => x.TenantId == tenant && x.HouseholdId == p.HouseholdId && x.UserId == last.UserId && x.Enabled && (x.Role == "Owner" || x.Role == "Parent"), ct);
    }
    private static CustomerProfileView View(CustomerProfile p) => new(p.Id, p.HouseholdId, p.Alias, p.AgeBand, p.Locale, p.Personalization, false, Convert.ToBase64String(p.RowVersion));
    private static CustomerProgressView View(CustomerProgress p) => new(p.Id, p.ProfileId, p.Key, p.Position, p.Bookmark, Convert.ToBase64String(p.RowVersion));
    private static void Row(CoreRecord r, string expected) { if (Convert.ToBase64String(r.RowVersion) != expected) throw new RuntimeConflictException("Stale row version; reload."); }
    public Task<Guid> HouseholdAsync(CustomerScope s, CustomerHouseholdRequest r, CancellationToken ct) => Own(s, r.OperationId, "household", r, u =>
    { Require(u.AdultAttested, "Adult responsibility attestation required; regional age assurance remains launch-gated."); Label(r.Label); var h = new CustomerHousehold { Id = Guid.NewGuid(), TenantId = s.TenantId, Label = r.Label }; db.CustomerHouseholds.Add(h); db.CustomerMembers.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, HouseholdId = h.Id, UserId = u.Id, Role = "Owner" }); return Task.FromResult(h.Id); }, ct);
    public async Task<CustomerHouseholdView[]> HouseholdsAsync(CustomerScope s, CancellationToken ct)
    { var u = await Self(s, ct); return await (from m in db.CustomerMembers.AsNoTracking() join h in db.CustomerHouseholds.AsNoTracking() on m.HouseholdId equals h.Id where m.TenantId == s.TenantId && h.TenantId == s.TenantId && m.UserId == u.Id && m.Enabled select new CustomerHouseholdView(h.Id, h.Label, m.Role)).Take(100).ToArrayAsync(ct); }
    public Task<Guid> InviteAsync(CustomerScope s, Guid household, CustomerInviteRequest r, CancellationToken ct) => Own(s, r.OperationId, "invite", new { household, r }, async u =>
    { var m = await Member(s.TenantId, household, u.Id, true, ct); if (m.Role != "Owner") throw new CoreAccessException(); Require(r.Role is "Parent" or "Adult" && r.UserId != u.Id, "Invite a verified adult account; children use parent-owned profiles."); Require((await User(s.TenantId, r.UserId, ct)).AdultAttested, "Invite an attested adult; child profiles require parent management."); var invite = new CustomerInvite { Id = Guid.NewGuid(), TenantId = s.TenantId, HouseholdId = household, UserId = r.UserId, Role = r.Role, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(2) }; db.CustomerInvites.Add(invite); return invite.Id; }, ct);
    public Task<Guid> AcceptAsync(CustomerScope s, Guid invite, CustomerOperation r, CancellationToken ct) => Own(s, r.OperationId, "accept-invite", new { invite, r }, async u =>
    {
        var i = await db.CustomerInvites.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == invite && x.UserId == u.Id, ct) ?? throw new CoreAccessException(); Require(!i.Used && i.ExpiresUtc > DateTimeOffset.UtcNow, "Expired/used household invitation."); i.Used = true;
        var m = await db.CustomerMembers.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.HouseholdId == i.HouseholdId && x.UserId == u.Id, ct); if (m != null) throw new RuntimeConflictException("Household membership already exists.");
        m = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, HouseholdId = i.HouseholdId, UserId = u.Id, Role = i.Role }; db.CustomerMembers.Add(m); return m.Id;
    }, ct);
    public Task<Guid> TransferAsync(CustomerScope s, Guid household, CustomerTransfer r, CancellationToken ct) => Own(s, r.OperationId, "transfer-owner", new { household, r }, async u =>
    { var old = await Member(s.TenantId, household, u.Id, true, ct); if (old.Role != "Owner") throw new CoreAccessException(); Require(r.UserId != u.Id, "New owner must be another accepted household adult."); await User(s.TenantId, r.UserId, ct); var next = await Member(s.TenantId, household, r.UserId, false, ct); next.Role = "Owner"; old.Role = "Parent"; return household; }, ct);
    public Task<Guid> ProfileAsync(CustomerScope s, CustomerProfileRequest r, CancellationToken ct) => Own(s, r.OperationId, "profile", r, async u =>
    {
        Label(r.Alias); Require(r.AgeBand is "Adult" or "Teen" or "Child", "Age band required; no birth date collected."); Key(r.Locale); await Member(s.TenantId, r.HouseholdId, u.Id, r.AgeBand != "Adult", ct);
        var p = new CustomerProfile { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = u.Id, HouseholdId = r.HouseholdId, Alias = r.Alias, AgeBand = r.AgeBand, Locale = r.Locale }; db.CustomerProfiles.Add(p); return p.Id;
    }, ct);
    public async Task<CustomerProfileView[]> ProfilesAsync(CustomerScope s, CancellationToken ct)
    {
        var u = await Self(s, ct); var rows = await (from p in db.CustomerProfiles.AsNoTracking() join m in db.CustomerMembers.AsNoTracking() on p.HouseholdId equals m.HouseholdId where p.TenantId == s.TenantId && m.TenantId == s.TenantId && m.UserId == u.Id && m.Enabled && !p.Deleted && (p.UserId == u.Id || p.AgeBand != "Adult" && (m.Role == "Owner" || m.Role == "Parent")) select p).Take(100).ToArrayAsync(ct); var result = new List<CustomerProfileView>(); foreach (var p in rows) result.Add(View(p) with { Personalization = p.Personalization && await Consented(s.TenantId, u.Id, p.Id, "Personalization", ct) }); return result.ToArray();
    }
    public Task<Guid> EditProfileAsync(CustomerScope s, Guid profile, CustomerProfileEdit r, CancellationToken ct) => Own(s, r.OperationId, "edit-profile", new { profile, r }, async u =>
    {
        var p = await ProfileAccess(s, profile, u, ct); Row(p, r.RowVersion); Label(r.Alias); Key(r.Locale);
        Require(!r.Personalization || p.AgeBand == "Adult" && await Consented(s.TenantId, u.Id, p.Id, "Personalization", ct), "Persistent child personalization is launch-gated; adults need explicit current consent.");
        p.Alias = r.Alias; p.Locale = r.Locale; p.Personalization = r.Personalization; p.UpdatedUtc = DateTimeOffset.UtcNow; return p.Id;
    }, ct);
    public Task<Guid> ConsentAsync(CustomerScope s, CustomerConsentRequest r, CancellationToken ct) => Own(s, r.OperationId, "consent", r, async u =>
    {
        Require(r.Purpose is "Progress" or "Personalization" or "Notifications", "Owned consent purpose required."); Key(r.Evidence); if (r.Granted) Future(r.ExpiresUtc);
        CustomerProfile? p = null; if (r.ProfileId != null) p = await ProfileAccess(s, r.ProfileId.Value, u, ct);
        Require(r.Purpose == "Notifications" || p != null, "Progress/personalization consent binds one profile.");
        var c = new CustomerConsent { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = u.Id, ProfileId = r.ProfileId, Purpose = r.Purpose, Granted = r.Granted, ExpiresUtc = r.Granted ? r.ExpiresUtc : DateTimeOffset.UtcNow, Evidence = r.Evidence }; db.CustomerConsents.Add(c);
        if (!r.Granted && p != null) { p.Personalization = false; if (r.Purpose == "Progress") db.CustomerProgress.RemoveRange(await db.CustomerProgress.Where(x => x.TenantId == s.TenantId && x.ProfileId == p.Id).ToArrayAsync(ct)); if (r.Purpose == "Personalization") await EraseTalk(s.TenantId, p.Id, false, ct); }
        return c.Id;
    }, ct);
    public Task<Guid> ProgressAsync(CustomerScope s, Guid profile, CustomerProgressRequest r, CancellationToken ct) => Own(s, r.OperationId, "sync-progress", new { profile, r }, async u =>
    {
        var p = await ProfileAccess(s, profile, u, ct); Require(await Consented(s.TenantId, u.Id, p.Id, "Progress", ct), "Current parent/profile consent required for progress sync."); Key(r.Key); Require(r.Position is >= 0 and <= 1000000, "Bounded progress position required.");
        var row = await db.CustomerProgress.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.ProfileId == p.Id && x.Key == r.Key, ct);
        if (row == null) { Require(r.RowVersion == "", "New progress has no previous row version."); row = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, ProfileId = p.Id, Key = r.Key }; db.CustomerProgress.Add(row); } else Row(row, r.RowVersion);
        row.Position = r.Position; row.Bookmark = r.Bookmark; row.UpdatedUtc = DateTimeOffset.UtcNow; return row.Id;
    }, ct);
    public async Task<CustomerProgressView[]> ProgressAsync(CustomerScope s, Guid profile, CancellationToken ct)
    { var u = await Self(s, ct); await ProfileAccess(s, profile, u, ct); if (!await Consented(s.TenantId, u.Id, profile, "Progress", ct)) return []; return (await db.CustomerProgress.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.ProfileId == profile).Take(1000).ToArrayAsync(ct)).Select(View).ToArray(); }
    private async Task EraseProfile(Guid tenant, CustomerProfile p, CancellationToken ct)
    {
        Require(!await db.CustomerReservations.AnyAsync(x => x.TenantId == tenant && x.ProfileId == p.Id && (x.State == "Running" || x.State == "Unknown"), ct), "Reconcile running/unknown generation before deleting its profile.");
        foreach (var r in await db.CustomerReservations.Where(x => x.TenantId == tenant && x.ProfileId == p.Id && x.State == "Reserved").ToArrayAsync(ct)) await Finish(r, 0, "Released", "privacy-delete", ct);
        db.CustomerProgress.RemoveRange(await db.CustomerProgress.Where(x => x.TenantId == tenant && x.ProfileId == p.Id).ToArrayAsync(ct)); await EraseTalk(tenant, p.Id, true, ct);
        foreach (var creation in await db.Creations.Where(x => x.TenantId == tenant && x.ProfileId == p.Id && x.State != "Deleted").ToArrayAsync(ct)) await CreationStudio.Erase(db, creation, ct);
        foreach (var upload in await db.CreationUploads.Where(x => x.TenantId == tenant && x.ProfileId == p.Id && !x.Deleted).ToArrayAsync(ct)) { upload.Content = []; upload.AltText = ""; upload.Deleted = true; }
        p.Alias = ""; p.Locale = ""; p.Personalization = false; p.Deleted = true;
    }
    public Task<Guid> DeleteProfileAsync(CustomerScope s, Guid profile, CustomerOperation r, CancellationToken ct) => Own(s, r.OperationId, "delete-profile", new { profile, r }, async u => { var p = await ProfileAccess(s, profile, u, ct); await EraseProfile(s.TenantId, p, ct); return p.Id; }, ct);
    public Task<Guid> DeleteAsync(CustomerScope s, CustomerOperation r, CancellationToken ct) => Own(s, r.OperationId, "delete-account", r, async u =>
    {
        var owned = await db.CustomerMembers.Where(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.Enabled && x.Role == "Owner").Select(x => x.HouseholdId).ToArrayAsync(ct);
        Require(!await db.CustomerMembers.AnyAsync(x => x.TenantId == s.TenantId && owned.Contains(x.HouseholdId) && x.UserId != u.Id && x.Enabled, ct), "Transfer shared-household ownership before account deletion.");
        Require(!await db.CustomerOrders.AnyAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && (x.State == "Submitting" || x.State == "Unknown" || x.RequestedAction != null), ct), "Reconcile pending payment actions before deletion.");
        Require(!await db.CustomerOrders.AnyAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && (x.State == "Pending" || x.State == "Paid" && x.EndsUtc > DateTimeOffset.UtcNow)), "Cancel/reconcile pending payments and active subscriptions before account deletion.");
        foreach (var h in await db.CustomerHouseholds.Where(x => x.TenantId == s.TenantId && owned.Contains(x.Id)).ToArrayAsync(ct)) h.Label = "";
        foreach (var o in await db.CustomerOrders.Where(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.State == "Created").ToArrayAsync(ct)) o.State = "Cancelled";
        foreach (var p in await db.CustomerProfiles.Where(x => x.TenantId == s.TenantId && x.UserId == u.Id && !x.Deleted).ToArrayAsync(ct)) await EraseProfile(s.TenantId, p, ct);
        foreach (var b in await db.CustomerIdentities.Where(x => x.TenantId == s.TenantId && x.UserId == u.Id).ToArrayAsync(ct)) b.Enabled = false;
        foreach (var m in await db.CustomerMembers.Where(x => x.TenantId == s.TenantId && x.UserId == u.Id).ToArrayAsync(ct)) m.Enabled = false;
        u.State = "Deleted"; u.DisplayName = ""; u.AdultAttested = false; return u.Id;
    }, ct);
    public async Task<CustomerExport> ExportAsync(CustomerScope s, CancellationToken ct)
    {
        var user = await MeAsync(s, ct); var profiles = (await ProfilesAsync(s, ct)).Where(x => db.CustomerProfiles.Any(p => p.TenantId == s.TenantId && p.Id == x.Id && p.UserId == user.Id)).ToArray(); var progress = new List<CustomerProgressView>(); foreach (var p in profiles) progress.AddRange(await ProgressAsync(s, p.Id, ct));
        return new(user, await HouseholdsAsync(s, ct), profiles, progress.ToArray(), await OrdersAsync(s, ct), await WalletAsync(s, ct), await NotificationsAsync(s, ct), "Profile labels and synchronized progress are erased on deletion. Minimal pseudonymous identity tombstones, consent/audit and financial/credit evidence remain for reconciliation; a reviewed jurisdiction-specific retention policy is required before launch. Other adults' profiles and financial data are excluded.", await db.CustomerIdentities.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UserId == user.Id).Select(x => new CustomerIdentityView(x.Id, x.Provider, x.Account, x.Environment, x.SubjectHash, x.Enabled)).ToArrayAsync(ct), await db.CustomerConsents.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UserId == user.Id).Select(x => new CustomerConsentView(x.Id, x.ProfileId, x.Purpose, x.Granted, x.ExpiresUtc, x.Evidence, x.CreatedUtc)).ToArrayAsync(ct));
    }
    public async Task<CustomerNotificationView[]> NotificationsAsync(CustomerScope s, CancellationToken ct)
    { var u = await Self(s, ct); return await db.CustomerNotifications.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UserId == u.Id).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => new CustomerNotificationView(x.Id, x.OrderId, x.Kind, x.State, x.Receipt)).ToArrayAsync(ct); }
    public Task<Guid> DeliverNoticeAsync(CustomerScope s, Guid id, CustomerOperation r, CancellationToken ct) => Own(s, r.OperationId, "notice", new { id, r }, async u =>
    { var n = await db.CustomerNotifications.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == u.Id && x.Id == id, ct) ?? throw new CoreAccessException(); if (n.State == "Delivered") return n.Id; var provider = notifications.SingleOrDefault(x => x.Key == n.Provider) ?? throw new RuntimeValidationException("Original notification provider unavailable."); n.Receipt = await provider.DeliverAsync(n.Id, u.Id, n.Kind, ct); n.State = "Delivered"; return n.Id; }, ct);
}
