namespace ContentOS.Domain;

public sealed class TalkPolicy : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public string SettingsJson { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
public sealed class TalkSession : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid OperationId { get; set; }
    public bool Remember { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset RetainUntilUtc { get; set; }
    public string State { get; set; } = "Open";
}
public sealed class TalkTurn : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public Guid OperationId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string State { get; set; } = "Preparing";
    public string Outcome { get; set; } = "";
    public Guid LeaseToken { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? AiRunId { get; set; }
    public Guid? RouteId { get; set; }
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public string SourcesJson { get; set; } = "[]";
    public long Credits { get; set; }
    public decimal ProviderCost { get; set; }
    public string? Currency { get; set; }
}
public sealed class TalkProblem : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid TurnId { get; set; }
    public Guid OperationId { get; set; }
    public string Category { get; set; } = "";
    public string EvidenceJson { get; set; } = "";
}
