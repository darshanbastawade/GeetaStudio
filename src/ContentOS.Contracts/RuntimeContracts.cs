namespace ContentOS.Contracts;

public sealed record RegisterProvider(string AdapterKey, string Endpoint = "local:test", string? SecretRef = null,
    int MaxConcurrency = 1, string Capability = "runtime.probe", string Locale = "en-IN");
public sealed record CreateRevision(Guid ProfileId, int MaxAttempts = 3, decimal Budget = 3,
    int DeadlineSeconds = 60, string Timezone = "Asia/Kolkata");
public sealed record CreateJob(string Scenario = "success", DateTimeOffset? ScheduledUtc = null, string Timezone = "Asia/Kolkata");
public sealed record ExecutionSnapshot(Guid RevisionId, Guid ProfileId, Guid ConnectionId, string AdapterKey,
    string Capability, string Locale, int ContractVersion, int MaxAttempts, decimal Budget, int DeadlineSeconds,
    string BudgetPolicyVersion = "runtime.probe.v1", string WorkflowVersion = "probe.v1");
public sealed record ProviderView(Guid Id, Guid ProfileId, string AdapterKey, bool Enabled, bool Killed,
    bool CredentialAvailable, int MaxConcurrency, DateTimeOffset? CircuitUntilUtc);
public sealed record JobView(Guid Id, string State, ExecutionSnapshot Snapshot, DateTimeOffset ScheduledUtc,
    string Timezone, int Attempts, bool CancelRequested, string? FailureReason, string? Result,
    string? LeaseOwner = null, DateTimeOffset? LeaseUntilUtc = null, DateTimeOffset? HeartbeatUtc = null);
public sealed record AttemptView(Guid Id, int Number, string State, string? Error, string TraceId,
    Guid AuditId, bool Reconciliation, DateTimeOffset StartedUtc, DateTimeOffset? EndedUtc, decimal Usage);
public sealed record OperationView(Guid Id, Guid ConnectionId, string AdapterKey, string State,
    string? ExternalResourceId, Guid? LastAttemptId);
public sealed record JobDetail(JobView Job, IReadOnlyList<AttemptView> Attempts, OperationView? Operation);
public sealed record RuntimeEvent(Guid JobId, Guid AttemptId, string State, string TraceId);
