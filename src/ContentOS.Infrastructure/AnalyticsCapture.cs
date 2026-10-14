using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class AnalyticsWorkbench
{
    public Task<AnalyticsImportResult> CaptureAsync(CoreScope s, Guid u, AnalyticsCapture r, CancellationToken ct) => Command(s, u, r.OperationId, "capture-owned", r, async () =>
    {
        const string key = "owned-production";
        var source = await db.AnalyticsSources.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == key && x.Version == 1, ct);
        if (source == null)
        {
            source = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = key, Version = 1, SnapshotJson = Json(new AnalyticsSourceRequest(Guid.Empty, key, 1, "owned.capture.v1", "ContentOS", "local:production", "owned:sql-ledgers")) }; db.AnalyticsSources.Add(source);
        }
        var sourceBinding = Read<AnalyticsSourceRequest>(source.SnapshotJson);
        AnalyticsRules.Require(source.Enabled && sourceBinding.Adapter == "owned.capture.v1" && sourceBinding.SourceSystem == "ContentOS" && sourceBinding.AccountReference == "local:production", "Original enabled owned capture source required."); var inputs = new List<AnalyticsEntryInput>();
        foreach (var ai in await db.AiRuns.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.State == "Completed").ToArrayAsync(ct))
        {
            var snap = Read<AiExecutionSnapshot>(ai.SnapshotJson);
            inputs.Add(new("owned:ai:" + ai.Id, "owned:ai:" + ai.Id, "Cost", ai.UpdatedUtc, "owned:ai-ledger:" + ai.Id, CostCategory: "AiTool", Currency: snap.Route.Currency, Amount: ai.Spent));
        }
        foreach (var media in await db.MediaGenerations.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.ActualCost != null && (x.State == "Completed" || x.State == "Failed")).ToArrayAsync(ct))
        {
            var snap = Read<MediaGenerationSnapshot>(media.SnapshotJson); Guid? revision = null;
            if (snap.Request.Admission != null) revision = await db.WatchProductions.Where(x => x.Id == snap.Request.Admission.ProductionId && x.TenantId == s.TenantId && x.UniverseId == u).Select(x => (Guid?)x.CoreRevisionId).SingleOrDefaultAsync(ct);
            inputs.Add(new("owned:media:" + media.Id, "owned:media:" + media.Id, "Cost", media.UpdatedUtc, "owned:media-ledger:" + media.Id, revision, CostCategory: "Production", Currency: media.Currency, Amount: media.ActualCost!.Value, ProductionId: snap.Request.Admission?.ProductionId));
        }
        // Audio stage amounts are M16 costs, not a second charge. WATCH owns only its renderer costs.
        foreach (var stage in await db.WatchStages.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind.StartsWith("Render") && x.State == "Accepted").ToArrayAsync(ct))
        {
            var p = await db.WatchProductions.AsNoTracking().SingleAsync(x => x.Id == stage.ProductionId && x.TenantId == s.TenantId && x.UniverseId == u, ct);
            var snap = Read<WatchSnapshot>(p.SnapshotJson);
            inputs.Add(new("owned:watch:" + stage.Id, "owned:watch:" + stage.Id, "Cost", stage.UpdatedUtc, "owned:watch-render:" + stage.Id, p.CoreRevisionId, CostCategory: "Production", Currency: snap.Profile.Configuration.Currency, Amount: stage.ActualCost, ProductionId: p.Id));
        }
        foreach (var old in await db.AnalyticsEntries.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.SourceId == source.Id).ToArrayAsync(ct))
        {
            var index = inputs.FindIndex(x => x.EconomicKey == old.EconomicKey); if (index < 0) continue;
            var current = inputs[index]; var previous = Read<AnalyticsEntryInput>(old.SnapshotJson);
            if (current.Amount != previous.Amount || current.Currency != previous.Currency || current.RevisionId != previous.RevisionId || current.ProductionId != previous.ProductionId) throw new RuntimeConflictException("Captured owned cost changed; reconcile explicitly.");
            inputs[index] = previous;
        }
        AnalyticsRules.Require(inputs.Count <= 10000, "Owned capture exceeds operational limit.");
        return inputs.Count == 0 ? new AnalyticsImportResult([], 0, 0, "owned.capture.v1") : await Insert(s, u, source, inputs.ToArray(), true, ct);
    }, ct);
    private async Task<string[]> Unresolved(CoreScope s, Guid u, CancellationToken ct)
    {
        var result = new List<string>();
        var captured = await db.AnalyticsEntries.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == "Cost").Select(x => x.EconomicKey).ToArrayAsync(ct); var set = captured.ToHashSet();
        foreach (var a in await db.AiRuns.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToArrayAsync(ct))
            if (a.State == "UsageUnresolved" || a.State == "Completed" && a.Spent > 0 && !set.Contains("owned:ai:" + a.Id)) result.Add("AiRun:" + a.Id + ":" + a.State);
        foreach (var m in await db.MediaGenerations.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToArrayAsync(ct))
            if (m.State is "Unknown" or "Submitted" or "Accepting" || m.ActualCost > 0 && !set.Contains("owned:media:" + m.Id)) result.Add("MediaGeneration:" + m.Id + ":" + m.State);
        foreach (var w in await db.WatchStages.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind.StartsWith("Render")).ToArrayAsync(ct))
            if (w.State == "Unknown" || w.ActualCost > 0 && !set.Contains("owned:watch:" + w.Id)) result.Add("WatchStage:" + w.Id + ":" + w.State);
        return result.ToArray();
    }
}
