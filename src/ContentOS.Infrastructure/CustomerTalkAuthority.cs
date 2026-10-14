using ContentOS.Application;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CustomerWorkbench
{
    internal async Task<bool> CommerceProgressConsent(CustomerScope s, Guid profile, CancellationToken ct) { var u = await Self(s, ct); await ProfileAccess(s, profile, u, ct); return await Consented(s.TenantId, u.Id, profile, "Progress", ct); }
    internal Task<bool> CommerceCurrentConsent(Guid tenant, Guid user, Guid profile, CancellationToken ct) => Consented(tenant, user, profile, "Progress", ct);
    internal async Task<ContentOS.Contracts.CreationWorkspace[]> CreationWorkspaces(System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct)
    {
        var host = await identities.Single(x => x.Key == "identity.windows.v1").VerifyAsync(principal, "local-windows", Guid.Empty, null, ct);
        var candidates = await (from binding in db.CustomerIdentities.AsNoTracking() join user in db.CustomerUsers.AsNoTracking() on binding.UserId equals user.Id join tenant in db.Tenants.AsNoTracking() on binding.TenantId equals tenant.Id where binding.Enabled && user.State == "Active" && binding.Provider == host.Provider && binding.Account == host.Account && binding.Environment == host.Environment select new { tenant.Id, tenant.Name, binding.SubjectHash }).Take(1000).ToArrayAsync(ct);
        return candidates.Where(x => x.SubjectHash == Subject(x.Id, host)).Select(x => new ContentOS.Contracts.CreationWorkspace(x.Id, x.Name)).DistinctBy(x => x.Id).ToArray();
    }
    internal Task<Guid> ReleaseCreationCredit(CoreScope s, Guid id, Guid op, CancellationToken ct) => Staff(s, op, "release-creation-credit", new { id }, async () =>
    {
        var r = await db.CustomerReservations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == id, ct) ?? throw new CoreAccessException();
        if (r.State == "Released") return id;
        CustomerRules.Require(r.State == "Reserved" && r.UsageKey.StartsWith("create:", StringComparison.Ordinal), "Only undispatched owned creation credits can be released.");
        await Finish(r, 0, "Released", "creation-not-dispatched", ct); await Expire(s.TenantId, r.UserId, ct); return id;
    }, ct);
    internal async Task<(CustomerUser User, CustomerProfile Profile, bool Memory)> TalkAuthority(CustomerScope s, Guid profile, CancellationToken ct)
    {
        var u = await Self(s, ct); var p = await ProfileAccess(s, profile, u, ct);
        return (u, p, p.AgeBand == "Adult" && p.Personalization && await Consented(s.TenantId, u.Id, p.Id, "Personalization", ct));
    }
    private async Task EraseTalk(Guid tenant, Guid profile, bool close, CancellationToken ct)
    {
        foreach (var session in await db.TalkSessions.Where(x => x.TenantId == tenant && x.ProfileId == profile).ToArrayAsync(ct))
        {
            session.Remember = false; if (close) session.State = "Deleted"; talkMemory?.Forget(session.Id);
            foreach (var turn in await db.TalkTurns.Where(x => x.SessionId == session.Id && (x.Question != null || x.Answer != null)).ToArrayAsync(ct)) { turn.Question = null; turn.Answer = null; }
        }
    }
}
