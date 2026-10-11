namespace ContentOS.Domain;

public sealed class FactoryPlan : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = ""; public int Version { get; set; }
    public string SnapshotJson { get; set; } = ""; public string ReviewStatus { get; set; } = "Draft"; public string? ReviewEvidence { get; set; }
}
public sealed class FactoryBatch : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid PlanId { get; set; }
    public string BusinessKey { get; set; } = "";
    public string Actor { get; set; } = ""; public string Fingerprint { get; set; } = ""; public string BudgetJson { get; set; } = "";
    public string State { get; set; } = "Queued"; public string Outcome { get; set; } = ""; public int GeneratedCharacters { get; set; }
    public decimal GenerationCost { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
}
public sealed class FactoryWork : CoreRecord
{
    public Guid BatchId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string BriefKey { get; set; } = ""; public string Format { get; set; } = "";
    public string State { get; set; } = "Pending"; public string Outcome { get; set; } = ""; public int Attempts { get; set; }
    public Guid? ArtifactId { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
}
public sealed class FactoryArtifact : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid PlanId { get; set; }
    public string BriefKey { get; set; } = ""; public string Format { get; set; } = "";
    public Guid LessonVariantId { get; set; }
    public Guid CoreContentId { get; set; }
    public Guid CoreRevisionId { get; set; }
    public Guid? ParentArtifactId { get; set; }
    public Guid AudienceId { get; set; }
    public string Locale { get; set; } = ""; public string Fingerprint { get; set; } = ""; public string BodyHash { get; set; } = "";
    public string SnapshotJson { get; set; } = ""; public string Treatment { get; set; } = ""; public string ReviewStatus { get; set; } = "Draft"; public string? ReviewEvidence { get; set; }
}
public sealed class FactoryOperation : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Kind { get; set; } = ""; public string Fingerprint { get; set; } = ""; public Guid ResultId { get; set; }
}
