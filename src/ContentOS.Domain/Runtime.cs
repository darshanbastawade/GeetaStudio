namespace ContentOS.Domain;

public static class RuntimeRules
{
    public const string Capability = "runtime.probe";
    public static readonly string[] Adapters = ["test.a", "test.b"];
    public static void Validate(string adapter, string endpoint, string capability, string locale,
        int concurrency, int maxAttempts, decimal budget, int deadlineSeconds, string timezone)
    {
        if (!Adapters.Contains(adapter) || endpoint != "local:test") throw new RuntimeValidationException("Only deployed local test adapters are permitted in M1.");
        if (capability != Capability || locale != "en-IN") throw new RuntimeValidationException("Unsupported capability or locale.");
        if (concurrency is < 1 or > 4 || maxAttempts is < 1 or > 5 || budget < maxAttempts || budget > 100 || deadlineSeconds is < 5 or > 300)
            throw new RuntimeValidationException("Invalid concurrency, attempts, deadline or budget limits.");
        if (string.IsNullOrWhiteSpace(timezone)) throw new RuntimeValidationException("Schedule timezone is required.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timezone); }
        catch (TimeZoneNotFoundException) { throw new RuntimeValidationException("Unknown schedule timezone."); }
        catch (InvalidTimeZoneException) { throw new RuntimeValidationException("Invalid schedule timezone."); }
    }
    public static void ValidateScenario(string scenario)
    {
        if (scenario is not ("success" or "unknown" or "transient" or "terminal" or "slow"))
            throw new RuntimeValidationException("Unsupported deterministic test scenario.");
    }
    public static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();
}

public sealed class RuntimeValidationException(string message, string outcome = "InvalidInput") : Exception(message) { public string Outcome { get; } = outcome; }
public sealed class RuntimeConflictException(string message) : Exception(message);

public sealed class ProviderDefinition
{
    public string Id { get; set; } = "";
    public string Capability { get; set; } = RuntimeRules.Capability;
    public int ContractVersion { get; set; } = 1;
    public string ApprovedEndpoint { get; set; } = "local:test";
    public bool SupportsIdempotency { get; set; } = true;
    public bool SupportsReconciliation { get; set; } = true;
    public string Qualification { get; set; } = "DeterministicTestOnly";
}
public sealed class ProviderConnection
{
    public Guid Id { get; set; }
    public string AdapterKey { get; set; } = "";
    public string Endpoint { get; set; } = "local:test";
    public string? SecretRef { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Killed { get; set; }
    public int MaxConcurrency { get; set; } = 1;
    public int Failures { get; set; }
    public DateTimeOffset? CircuitUntilUtc { get; set; }
}

public sealed class CapabilityProfile
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public string Capability { get; set; } = RuntimeRules.Capability;
    public string Locale { get; set; } = "en-IN";
    public int ContractVersion { get; set; } = 1;
}

public sealed class RouteRevision
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public decimal Budget { get; set; } = 3;
    public int DeadlineSeconds { get; set; } = 60;
    public string Timezone { get; set; } = "Asia/Kolkata";
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class RouteHead
{
    public string Id { get; set; } = "runtime.probe";
    public Guid RevisionId { get; set; }
}

public sealed class Job
{
    public Guid Id { get; set; }
    public string Snapshot { get; set; } = "";
    public string Scenario { get; set; } = "success";
    public string State { get; set; } = "PendingDispatch";
    public DateTimeOffset ScheduledUtc { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
    public string Timezone { get; set; } = "Asia/Kolkata";
    public Guid ConnectionId { get; set; }
    public int AttemptCount { get; set; }
    public Guid? LeaseToken { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset? HeartbeatUtc { get; set; }
    public bool CancelRequested { get; set; }
    public string? FailureReason { get; set; }
    public string? Result { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class JobAttempt
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int Number { get; set; }
    public Guid LeaseToken { get; set; }
    public string TraceId { get; set; } = "";
    public Guid AuditId { get; set; }
    public bool Reconciliation { get; set; }
    public string State { get; set; } = "Started";
    public string? Error { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset? EndedUtc { get; set; }
    public decimal Usage { get; set; }
}

public sealed class ExternalOperation
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ConnectionId { get; set; }
    public string AdapterKey { get; set; } = "";
    public string State { get; set; } = "Prepared";
    public string? ExternalResourceId { get; set; }
    public Guid? LastAttemptId { get; set; }
}

public sealed class ProbeEffect
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public string AdapterKey { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class BudgetPolicy
{
    public string Id { get; set; } = "runtime.probe.v1";
    public decimal Limit { get; set; } = 10000;
    public decimal Reserved { get; set; }
    public decimal Spent { get; set; }
}

public sealed class BudgetReservation
{
    public Guid Id { get; set; }
    public decimal Reserved { get; set; }
    public decimal Used { get; set; }
    public bool Settled { get; set; }
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? DispatchedUtc { get; set; }
}
public sealed class InboxMessage
{
    public Guid Id { get; set; }
    public DateTimeOffset ReceivedUtc { get; set; }
}
public sealed class AuditEntry
{
    public Guid Id { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid? SubjectId { get; set; }
    public string TraceId { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}
public sealed class Asset
{
    public Guid Id { get; set; }
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public string Owner { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}
