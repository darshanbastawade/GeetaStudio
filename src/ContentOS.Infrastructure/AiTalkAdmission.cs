using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class AiService
{
    private async Task TalkAdmission(CoreScope s, Guid universe, Guid operation, TalkAiAdmission? claim, CancellationToken ct)
    {
        if (claim == null) return;
        await TalkWorkbench.Lock(db, s.TenantId, ct); var now = DateTimeOffset.UtcNow;
        var valid = await (from turn in db.TalkTurns
                           join session in db.TalkSessions on turn.SessionId equals session.Id
                           join policy in db.TalkPolicies on session.PolicyId equals policy.Id
                           join profile in db.CustomerProfiles on session.ProfileId equals profile.Id
                           join user in db.CustomerUsers on session.UserId equals user.Id
                           join reservation in db.CustomerReservations on turn.ReservationId equals reservation.Id
                           where turn.Id == claim.TurnId && session.Id == claim.SessionId && turn.OperationId == operation && turn.LeaseToken == claim.LeaseToken && turn.State == "Preparing" && turn.TenantId == s.TenantId && session.State == "Open" && session.ExpiresUtc > now && policy.UniverseId == universe && policy.Actor == s.Actor && policy.Enabled && !profile.Deleted && user.State == "Active" && reservation.State == "Running" && reservation.UserId == session.UserId && reservation.ProfileId == session.ProfileId && reservation.Provider == "ContentOS.AI" && reservation.Account == policy.Id.ToString() && reservation.Resource == turn.Id.ToString()
                           select turn.Id).AnyAsync(ct);
        if (!valid) throw new WorkflowAdmissionRejectedException();
        var sessionRow = await db.TalkSessions.AsNoTracking().SingleAsync(x => x.Id == claim.SessionId, ct);
        var profileRow = await db.CustomerProfiles.AsNoTracking().SingleAsync(x => x.Id == sessionRow.ProfileId, ct);
        if (!await db.CustomerMembers.AnyAsync(x => x.TenantId == s.TenantId && x.UserId == sessionRow.UserId && x.HouseholdId == profileRow.HouseholdId && x.Enabled && (profileRow.AgeBand == "Adult" || x.Role == "Owner" || x.Role == "Parent"), ct)) throw new WorkflowAdmissionRejectedException();
        var policyRow = await db.TalkPolicies.AsNoTracking().SingleAsync(x => x.Id == sessionRow.PolicyId, ct); var settings = Decode<TalkPolicyRequest>(policyRow.SettingsJson);
        if (!await db.RegistryRevisions.AnyAsync(x => x.Id == settings.PersonaRevisionId && x.TenantId == s.TenantId && x.UniverseId == universe && x.Kind == "Character" && x.ReviewStatus == "Approved" && db.Characters.Any(c => c.Id == x.CharacterId && c.Status == "Active") && !db.CharacterRevisions.Any(c => c.CharacterId == x.CharacterId && c.Version > db.CharacterRevisions.Where(v => v.Id == x.CoreRevisionId).Select(v => v.Version).First()), ct) || !await db.LessonProfiles.AnyAsync(x => x.Id == settings.AudienceProfileId && x.ReviewStatus == "Approved", ct) || !await db.LessonProfiles.AnyAsync(x => x.Id == settings.LocaleProfileId && x.ReviewStatus == "Approved", ct)) throw new WorkflowAdmissionRejectedException();
    }
    private async Task TalkAdmissionForRun(CoreScope s, Guid runId, TalkAiAdmission? claim, CancellationToken ct)
    {
        if (claim == null) return;
        var run = await db.AiRuns.AsNoTracking().SingleAsync(x => x.Id == runId && x.TenantId == s.TenantId, ct);
        await TalkAdmission(s, run.UniverseId, run.OperationId, claim, ct);
    }
}
