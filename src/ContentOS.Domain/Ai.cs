namespace ContentOS.Domain;

public static class AiRules
{
    public static readonly string[] Tasks = ["draft", "summarize", "translate", "evaluate", "answer-with-evidence", "embedding"];
    public static readonly string[] Adapters = ["openai.text.v1", "fixture.text.a", "fixture.text.b", "local.embedding.v1"];
}
public sealed class AiProfile : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid ConnectionId { get; set; }
    public string SettingsJson { get; set; } = "";
}
public sealed class AiRoute : CoreRecord
{
    public Guid TenantId { get; set; }
    public string Task { get; set; } = "";
    public string SettingsJson { get; set; } = "";
}
public sealed class AiRouteHead : CoreRecord
{
    public Guid TenantId { get; set; }
    public string Task { get; set; } = "";
    public Guid RouteId { get; set; }
}
public sealed class AiBudget : CoreRecord
{
    public Guid TenantId { get; set; }
    public string Capability { get; set; } = "";
    public string Currency { get; set; } = "";
    public DateTimeOffset StartsUtc { get; set; }
    public DateTimeOffset EndsUtc { get; set; }
    public decimal Limit { get; set; }
    public decimal Reserved { get; set; }
    public decimal Spent { get; set; }
}
public sealed class AiRun : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public Guid BudgetId { get; set; }
    public Guid RouteId { get; set; }
    public string Actor { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public string State { get; set; } = "Reserved";
    public string Outcome { get; set; } = "";
    public decimal Reserved { get; set; }
    public decimal Spent { get; set; }
    public string? OutputJson { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
}
public sealed class AiAttempt : CoreRecord
{
    public Guid RunId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid ConnectionId { get; set; }
    public int Number { get; set; }
    public string State { get; set; } = "Started";
    public string Outcome { get; set; } = "";
    public decimal Ceiling { get; set; }
    public decimal Cost { get; set; }
    public long? InputTokens { get; set; }
    public long? CachedInputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public string? ProviderRequestId { get; set; }
    public string? ReconciliationEvidence { get; set; }
    public DateTimeOffset? EndedUtc { get; set; }
}
