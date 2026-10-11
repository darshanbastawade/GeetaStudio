using ContentOS.Contracts;

namespace ContentOS.Application;

public interface ISecretResolver
{
    ValueTask<ResolvedSecret?> ResolveAsync(string reference, CancellationToken cancellationToken = default);
    ValueTask<bool> IsAvailableAsync(string? reference, CancellationToken cancellationToken = default);
}
public sealed class ResolvedSecret(string value) : IDisposable
{
    private readonly char[] _value = value.ToCharArray();
    public ReadOnlyMemory<char> Value => _value;
    public override string ToString() => "[redacted]";
    public void Dispose() => Array.Clear(_value);
}
public interface IProviderConfigurationStore
{
    Task<ProviderView> RegisterAsync(RegisterProvider request, string actor, string trace, CancellationToken ct);
    Task<IReadOnlyList<ProviderView>> ProvidersAsync(CancellationToken ct);
    Task<Guid> RevisionAsync(CreateRevision request, string actor, string trace, CancellationToken ct);
    Task ActivateAsync(Guid revision, string actor, string trace, CancellationToken ct);
    Task SetEnabledAsync(Guid connection, bool enabled, bool killed, string actor, string trace, CancellationToken ct);
    Task TestAsync(Guid connection, CancellationToken ct);
}
public interface IRuntimeStore
{
    Task<Guid> CreateAsync(CreateJob request, string actor, string trace, CancellationToken ct);
    Task<JobDetail?> InspectAsync(Guid job, CancellationToken ct);
    Task<IReadOnlyList<JobView>> JobsAsync(CancellationToken ct);
    Task CancelAsync(Guid job, string actor, string trace, CancellationToken ct);
    Task ReconcileAsync(Guid job, string actor, string trace, CancellationToken ct);
    Task RetryDeadLetterAsync(Guid job, string actor, string trace, CancellationToken ct);
    Task<ClaimedWork?> ClaimAsync(string worker, TimeSpan lease, CancellationToken ct);
    Task<bool> HeartbeatAsync(Guid job, Guid token, TimeSpan lease, CancellationToken ct);
    Task<ProviderError> AdmitAsync(ClaimedWork work, CancellationToken ct);
    Task CompleteAsync(ClaimedWork work, ProviderOutcome result, CancellationToken ct);
    Task AuditAsync(string actor, string action, Guid? subject, string trace, CancellationToken ct);
}
public sealed record ClaimedWork(Guid JobId, Guid AttemptId, Guid LeaseToken, Guid OperationId,
    ExecutionSnapshot Snapshot, string Scenario, bool Reconciliation, DateTimeOffset DeadlineUtc, string TraceId);
public enum ProviderError { None, CircuitOpen, Unavailable, RateLimited, InvalidInput, Configuration, UnsupportedCapability, PolicyRejected, QuotaExhausted, Unknown, Terminal }
public sealed record ProviderOutcome(ProviderError Error, string? ResourceId = null, string? Result = null,
    decimal Usage = 0, bool AuthoritativeNotFound = false);
public interface ITestOperationProvider
{
    string Key { get; }
    Task<ProviderOutcome> ExecuteAsync(ClaimedWork work, CancellationToken ct);
    Task<ProviderOutcome> ReconcileAsync(ClaimedWork work, CancellationToken ct);
}
public interface ITestEffectLedger
{
    Task<string> SubmitAsync(Guid operation, Guid connection, string adapter, CancellationToken ct);
    Task<string?> FindAsync(Guid operation, Guid connection, string adapter, CancellationToken ct);
}
public interface IJobTransport { Task<int> DispatchAsync(CancellationToken ct); }
public interface IAssetStore
{
    Task<Guid> PutAsync(ReadOnlyMemory<byte> content, string actor, CancellationToken ct);
    Task<byte[]?> GetAsync(Guid id, string actor, CancellationToken ct);
}
public interface ITelemetryExporter { void Export(RuntimeEvent runtimeEvent); }
