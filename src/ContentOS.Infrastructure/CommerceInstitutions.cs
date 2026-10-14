using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CommerceWorkbench
{
    private async Task<CommerceInstitution> InstitutionAccess(CustomerScope s, Guid id, bool admin, CancellationToken ct)
    {
        var user = await customers.MeAsync(s, ct); var institution = await db.CommerceInstitutions.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == id, ct) ?? throw new CoreAccessException();
        var seat = await db.CommerceSeats.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.InstitutionId == id && x.UserId == user.Id && x.Enabled, ct);
        if (seat == null || admin && seat.Role is not ("Owner" or "Administrator")) throw new CoreAccessException(); return institution;
    }
    private async Task<int> Capacity(Guid tenant, Guid institution, CancellationToken ct)
    {
        var rows = await (from o in db.CommerceOrders.AsNoTracking() join pay in db.CustomerOrders.AsNoTracking() on o.PaymentOrderId equals pay.Id join product in db.CommerceProducts.AsNoTracking() on o.ProductId equals product.Id where o.TenantId == tenant && o.InstitutionId == institution && o.State == "Active" && pay.State == "Paid" && pay.RefundedMinor == 0 && pay.EndsUtc > DateTimeOffset.UtcNow && product.Enabled select product.SettingsJson).ToArrayAsync(ct);
        // Overlapping renewal periods do not multiply the organization's seat allowance.
        return rows.Select(x => Read<CommerceProductRequest>(x).License.Seats).DefaultIfEmpty(0).Max();
    }
    private async Task<bool> Audit(Guid tenant, Guid user, Guid subject, Guid op, string kind, object request, CancellationToken ct)
    {
        CreationRules.Require(op != Guid.Empty, "Stable institutional operation required."); var old = await db.CommerceAudits.SingleOrDefaultAsync(x => x.TenantId == tenant && x.OperationId == op, ct); if (old != null) { CreationRules.Require(old.UserId == user && old.SubjectId == subject && old.Kind == kind && old.Fingerprint == Hash(request), "Institutional replay differs."); return true; }
        db.CommerceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = tenant, UserId = user, SubjectId = subject, OperationId = op, Kind = kind, Fingerprint = Hash(request) }); return false;
    }
    public Task<Guid> InstitutionAsync(CustomerScope s, CommerceInstitutionRequest r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        var user = await customers.MeAsync(s, ct); CreationRules.Require(user.AdultAttested && r.OperationId != Guid.Empty, "Adult institutional responsibility attestation required."); CustomerRules.Label(r.Name); CustomerRules.Key(r.ContractReference); var old = await db.CommerceInstitutions.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct); if (old != null) { CreationRules.Require(old.UserId == user.Id && old.Name == r.Name && old.ContractReference == r.ContractReference, "Institution replay differs."); return old.Id; }
        var institution = new CommerceInstitution { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = user.Id, OperationId = r.OperationId, Name = r.Name, ContractReference = r.ContractReference }; db.CommerceInstitutions.Add(institution); db.CommerceSeats.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, InstitutionId = institution.Id, UserId = user.Id, Cohort = "Administrators", Role = "Owner" }); return institution.Id;
    }, ct);
    public async Task<CommerceInstitutionView[]> InstitutionsAsync(CustomerScope s, CancellationToken ct) { var user = await customers.MeAsync(s, ct); var rows = await db.CommerceInstitutions.AsNoTracking().Where(x => x.TenantId == s.TenantId && db.CommerceSeats.Any(m => m.InstitutionId == x.Id && m.UserId == user.Id && m.Enabled)).Take(100).ToArrayAsync(ct); var result = new List<CommerceInstitutionView>(); foreach (var row in rows) result.Add(new(row.Id, row.Name, await Capacity(s.TenantId, row.Id, ct), await db.CommerceSeats.CountAsync(x => x.InstitutionId == row.Id && x.Enabled && x.Role != "Owner", ct), row.ContractReference)); return result.ToArray(); }
    public Task SeatAsync(CustomerScope s, Guid institution, CommerceSeatRequest r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        var user = await customers.MeAsync(s, ct); var org = await InstitutionAccess(s, institution, true, ct); if (await Audit(s.TenantId, user.Id, institution, r.OperationId, "seat-change", r, ct)) return true; CustomerRules.Label(r.Cohort); CreationRules.Require(r.Role is "Learner" or "Administrator" && r.UserId != org.UserId, "Only the institution owner can retain its immutable ownership seat.");
        CreationRules.Require(await db.CustomerUsers.AnyAsync(x => x.Id == r.UserId && x.TenantId == s.TenantId && x.State == "Active" && x.AdultAttested, ct), "Assign seats to verified adult accounts; children remain guardian-managed profiles.");
        var seat = await db.CommerceSeats.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.InstitutionId == institution && x.UserId == r.UserId, ct);
        if (r.Enabled) CreationRules.Require(await db.CommerceSeats.CountAsync(x => x.InstitutionId == institution && x.Enabled && x.Role != "Owner" && x.UserId != r.UserId, ct) < await Capacity(s.TenantId, institution, ct), "Current licensed seat allowance exceeded.");
        if (seat == null) { seat = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, InstitutionId = institution, UserId = r.UserId }; db.CommerceSeats.Add(seat); }
        seat.Cohort = r.Cohort; seat.Role = r.Role; seat.Enabled = r.Enabled; return true;
    }, ct);
    public Task ActivityAsync(CustomerScope s, Guid institution, CommerceActivityRequest r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        var user = await customers.MeAsync(s, ct); await InstitutionAccess(s, institution, false, ct); CreationRules.Require(await Capacity(s.TenantId, institution, ct) > 0 && await customers.CommerceProgressConsent(s, r.ProfileId, ct), "Current institutional license and own/guardian progress consent required.");
        var old = await db.CommerceActivities.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct); if (old != null) { CreationRules.Require(old.UserId == user.Id && old.InstitutionId == institution && old.ProfileId == r.ProfileId && old.Completed == r.Completed, "Progress replay differs."); return true; }
        CreationRules.Require(r.OperationId != Guid.Empty, "Stable activity operation required."); db.CommerceActivities.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, InstitutionId = institution, UserId = user.Id, ProfileId = r.ProfileId, OperationId = r.OperationId, Completed = r.Completed }); return true;
    }, ct);
    public async Task<CommerceCohortReport[]> CohortsAsync(CustomerScope s, Guid institution, CancellationToken ct)
    {
        await InstitutionAccess(s, institution, true, ct); var seats = await db.CommerceSeats.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.InstitutionId == institution && x.Enabled && x.Role == "Learner").ToArrayAsync(ct); var licensed = await Capacity(s.TenantId, institution, ct) > 0; var result = new List<CommerceCohortReport>();
        foreach (var cohort in seats.GroupBy(x => x.Cohort)) { var samples = new List<bool>(); foreach (var seat in cohort) { var activities = await db.CommerceActivities.AsNoTracking().Where(x => x.InstitutionId == institution && x.UserId == seat.UserId).OrderByDescending(x => x.CreatedUtc).Take(100).ToArrayAsync(ct); foreach (var activity in activities) { if (await db.CustomerUsers.AnyAsync(x => x.Id == seat.UserId && x.State == "Active", ct) && await db.CustomerProfiles.AnyAsync(x => x.Id == activity.ProfileId && !x.Deleted, ct) && await customers.CommerceCurrentConsent(s.TenantId, seat.UserId, activity.ProfileId, ct)) { samples.Add(activity.Completed); break; } } } var allowed = licensed && samples.Count >= 5; result.Add(new(cohort.Key, cohort.Count(), allowed ? samples.Count : null, allowed ? samples.Count(x => x) / (decimal)samples.Count : null, allowed ? "Current consented observed completion; no causal learning claim" : "Suppressed: no current license or fewer than five consented subjects")); }
        return result.ToArray();
    }
    public Task<CommerceDownload> InstitutionDownloadAsync(CustomerScope s, Guid institution, Guid product, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        await InstitutionAccess(s, institution, false, ct);
        var order = await db.CommerceOrders.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.InstitutionId == institution && x.ProductId == product && x.State == "Active").OrderByDescending(x => x.CreatedUtc).FirstOrDefaultAsync(ct) ?? throw new CoreAccessException();
        await CurrentPaid(order, ct);
        var licensed = await db.CommerceProducts.AsNoTracking().SingleAsync(x => x.Id == product && x.TenantId == s.TenantId, ct);
        var allowance = Read<CommerceProductRequest>(licensed.SettingsJson).License.Seats;
        // The pilot has one shared roster. A larger SKU cannot extend another SKU's license.
        var assigned = await db.CommerceSeats.CountAsync(x => x.TenantId == s.TenantId && x.InstitutionId == institution && x.Enabled && x.Role != "Owner", ct);
        CreationRules.Require(assigned <= allowance, "This product's licensed seat allowance is exceeded; reduce the active institutional roster before downloading.");
        return await Package(licensed, ct);
    }, ct);
    public Task<Guid> OfferAsync(CoreScope s, CommerceOfferRequest r, CancellationToken ct) => Change(s.TenantId, async () => { await Admin(s, ct); CustomerRules.Key(r.Key); CustomerRules.Key(r.ContractReference); CreationRules.Require(r.OperationId != Guid.Empty && r.Kind is "Merchandise" or "ContentLicensing" or "ContentOSLicensing" or "Sponsorship" or "AffiliateCommerce" && r.Prerequisites.Length is > 0 and <= 10 && !string.IsNullOrWhiteSpace(r.Disclosure) && r.Disclosure.Length <= 1000, "Register an explicitly disclosed prerequisite-gated offer; no automatic revenue."); foreach (var p in r.Prerequisites) CustomerRules.Key(p); var old = await db.CommerceOffers.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct); if (old != null) { CreationRules.Require(old.SettingsJson == Json(r), "Offer replay differs."); return old.Id; } var row = new CommerceOffer { Id = Guid.NewGuid(), TenantId = s.TenantId, OperationId = r.OperationId, SettingsJson = Json(r) }; db.CommerceOffers.Add(row); return row.Id; }, ct);
    public async Task<CommerceOfferView[]> OffersAsync(CustomerScope s, CancellationToken ct) { await customers.MeAsync(s, ct); return (await db.CommerceOffers.AsNoTracking().Where(x => x.TenantId == s.TenantId).Take(100).ToArrayAsync(ct)).Select(x => { var r = Read<CommerceOfferRequest>(x.SettingsJson); return new CommerceOfferView(x.Id, r.Key, r.Kind, r.Prerequisites, r.ContractReference, r.Disclosure, "PrerequisitesPending;NotPurchasable;NoRevenueRecognized"); }).ToArray(); }
}
