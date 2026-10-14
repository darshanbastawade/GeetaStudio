namespace ContentOS.Domain;

public sealed class OptimizationExperiment : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public string Owner { get; set; } = "";
    public string ProposalJson { get; set; } = "";
    public string State { get; set; } = "Proposed";
    public int Dispatches { get; set; }
    public decimal ReservedCost { get; set; }
}
public sealed class OptimizationEvent : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ExperimentId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string EvidenceJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
public sealed class OptimizationAutomationRun : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
