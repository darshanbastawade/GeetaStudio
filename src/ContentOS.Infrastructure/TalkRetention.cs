using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;
namespace ContentOS.Infrastructure;

public sealed partial class TalkWorkbench
{
    public async Task PurgeAsync(CancellationToken ct)
    {
        // Bounded sweeps; owners are rechecked before every history/context read as well.
        var ids = await db.TalkSessions.AsNoTracking().Where(x => x.Remember || x.State == "Open").OrderBy(x => x.RetainUntilUtc).Take(200).Select(x => new { x.Id, x.TenantId }).ToArrayAsync(ct);
        foreach (var id in ids) await Change(id.TenantId, async () =>
        {
            var session = await db.TalkSessions.SingleAsync(x => x.Id == id.Id, ct); var now = DateTimeOffset.UtcNow;
            var profile = await db.CustomerProfiles.AsNoTracking().SingleAsync(x => x.Id == session.ProfileId, ct);
            var active = await db.CustomerUsers.AnyAsync(x => x.Id == session.UserId && x.State == "Active", ct);
            var consent = await db.CustomerConsents.AsNoTracking().Where(x => x.TenantId == id.TenantId && x.ProfileId == session.ProfileId && x.Purpose == "Personalization").OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            var member = await db.CustomerMembers.AnyAsync(x => x.TenantId == id.TenantId && x.UserId == session.UserId && x.HouseholdId == profile.HouseholdId && x.Enabled, ct);
            var retain = active && member && !profile.Deleted && profile.AgeBand == "Adult" && profile.Personalization && consent is { Granted: true } && consent.UserId == session.UserId && consent.ExpiresUtc > now && session.RetainUntilUtc > now && session.State is "Open" or "Cancelled";
            if (!retain) { session.Remember = false; foreach (var t in await db.TalkTurns.Where(t => t.SessionId == session.Id && (t.Question != null || t.Answer != null)).ToArrayAsync(ct)) { t.Question = null; t.Answer = null; } }
            if (!active || !member || profile.Deleted || session.ExpiresUtc <= now) { session.State = session.State == "Deleted" ? "Deleted" : "Expired"; transient.Forget(session.Id); }
            return true;
        }, ct);
    }
}

