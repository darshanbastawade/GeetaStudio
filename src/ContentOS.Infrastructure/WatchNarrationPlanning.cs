using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class WatchFactory
{
    public async Task<CreateWatch> PlanNarrationAsync(CoreScope s, Guid universe, CreateWatch request, CancellationToken ct)
    {
        await Access(s, universe, "Admin", ct);
        var packet = await editorial.InspectAsync(s, universe, request.EditorialItemId, ct);
        if (packet.Current == null || packet.SourceOutputWithheld || packet.State is not ("Approved" or "Scheduled" or "Published") || !packet.Approvals.Any(x => x.Current)) throw new EvidenceBlockedException("ApproveCurrentScriptBeforeNarrationPlanning");
        // Planning creates no production, media job, review or provider side effect.
        // The ordinary CreateAsync boundary rechecks the returned request and budget.
        var profile = await db.WatchProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProfileId && x.TenantId == s.TenantId && x.UniverseId == universe && x.Enabled, ct) ?? throw new CoreAccessException();
        var binding = Read<RegisterWatchProfile>(profile.SnapshotJson);
        var template = await db.WatchTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == binding.TemplateId && x.TenantId == s.TenantId && x.UniverseId == universe && x.ReviewStatus == "Approved", ct) ?? throw new EvidenceBlockedException("ReviewedScopedNarrationLayoutRequired");
        return WatchNarration.Plan(request, packet.Current.Body, Read<RegisterWatchTemplate>(template.SnapshotJson).Rules);
    }
}
