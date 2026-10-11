namespace ContentOS.Domain;

public sealed class PromptRevision : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public string SettingsJson { get; set; } = "";
}
public sealed class WorkflowDefinition : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public Guid PromptId { get; set; }
    public string SettingsJson { get; set; } = "";
}
public sealed class DurableWorkflow : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid DefinitionId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public string InputsJson { get; set; } = "";
    public string State { get; set; } = "Queued";
    public string Outcome { get; set; } = "";
    public int NextStep { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
}
public sealed class WorkflowCheckpoint : CoreRecord
{
    public Guid RunId { get; set; }
    public int Number { get; set; }
    public string Kind { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid? AiRunId { get; set; }
    public string State { get; set; } = "Pending";
    public string Outcome { get; set; } = "";
    public int Attempts { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public string? ResultJson { get; set; }
}
public sealed class WorkflowDecision : CoreRecord
{
    public Guid RunId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Action { get; set; } = "";
    public string EvidenceReference { get; set; } = "";
}
