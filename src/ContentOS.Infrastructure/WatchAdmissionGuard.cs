using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

internal static class WatchAdmissionGuard
{
    internal static async Task Assert(CoreDbContext db, CoreScope s, Guid u, WatchAdmission? admission, CancellationToken ct)
    {
        if (admission == null) return;
        var row = await db.WatchProductions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == admission.ProductionId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        if (row.State != "Running" || row.DeadlineUtc <= DateTimeOffset.UtcNow || !await db.WatchStages.AnyAsync(x => x.Id == admission.StageId && x.ProductionId == row.Id && (x.Kind == "Assets" || x.Kind == "Voice") && (x.State == "Working" || x.State == "Waiting"), ct)) throw new EvidenceBlockedException("WATCHOwnerCancelledOrMediaStageUnavailable");
        var snap = EditorialBinding.Read<WatchSnapshot>(row.SnapshotJson); await EditorialBinding.AssertExport(db, s, snap.Script.RevisionId, "Export", ct);
        if (!await db.WatchProfiles.AnyAsync(x => x.Id == row.ProfileId && x.Enabled, ct)) throw new EvidenceBlockedException("WATCHOwnerRendererDisabled");
    }
}
