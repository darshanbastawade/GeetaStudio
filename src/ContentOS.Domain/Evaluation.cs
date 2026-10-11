namespace ContentOS.Domain;

public sealed class EvaluationDataset : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = ""; public int Version { get; set; }
    public string SnapshotJson { get; set; } = "";
}
public sealed class EvaluationCandidate : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = ""; public int Version { get; set; }
    public string SnapshotJson { get; set; } = "";
}
public sealed class EvaluationRun : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid DatasetId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid? BaselineRunId { get; set; }
    public string Split { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
}
public sealed class EvaluationReview : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid DatasetId { get; set; }
    public Guid? RunId { get; set; }
    public string CaseKey { get; set; } = ""; public string Mode { get; set; } = ""; public string Actor { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
}
public sealed class EvaluationAdvice : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid RunId { get; set; }
    public string CaseKey { get; set; } = ""; public string Actor { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
}
public sealed class EvaluationOperation : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public string Kind { get; set; } = ""; public string Actor { get; set; } = ""; public string Fingerprint { get; set; } = "";
    public Guid ResultId { get; set; }
}
