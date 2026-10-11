using ContentOS.Application;

namespace ContentOS.Integrations;

// Deterministic local adapters only. The ledger simulates a remote committed effect.
public sealed class TestOperationProvider(string key, ITestEffectLedger ledger) : ITestOperationProvider
{
    public string Key => key;
    public async Task<ProviderOutcome> ExecuteAsync(ClaimedWork work, CancellationToken ct)
    {
        if (work.Scenario == "transient") return new(ProviderError.Unavailable, Usage: 1);
        if (work.Scenario == "terminal") return new(ProviderError.InvalidInput, Usage: 1);
        var resource = await ledger.SubmitAsync(work.OperationId, work.Snapshot.ConnectionId, key, ct);
        if (work.Scenario == "slow") await Task.Delay(TimeSpan.FromSeconds(20), ct);
        if (work.Scenario == "unknown") return new(ProviderError.Unknown, Usage: 1);
        return new(ProviderError.None, resource, $"{key}: deterministic probe completed", 1);
    }
    public async Task<ProviderOutcome> ReconcileAsync(ClaimedWork work, CancellationToken ct)
    {
        var resource = await ledger.FindAsync(work.OperationId, work.Snapshot.ConnectionId, key, ct);
        return resource is null ? new(ProviderError.None, AuthoritativeNotFound: true)
            : new(ProviderError.None, resource, $"{key}: reconciled original operation", 1);
    }
}
