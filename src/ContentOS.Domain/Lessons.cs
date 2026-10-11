namespace ContentOS.Domain;

public sealed class LessonProfile : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public string Kind { get; set; } = "";
    public string RulesJson { get; set; } = "";
    public string ReviewStatus { get; set; } = "Draft";
    public string? ReviewEvidence { get; set; }
}
public sealed class LessonBundle : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid AnchorClaimRevisionId { get; set; }
    public Guid InterpretationClaimRevisionId { get; set; }
    public string SnapshotJson { get; set; } = "";
}
public sealed class LessonMemory : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid LocaleProfileId { get; set; }
    public Guid BundleId { get; set; }
    public string EntryJson { get; set; } = "";
    public string ReviewStatus { get; set; } = "Draft";
    public string? ReviewEvidence { get; set; }
}
public sealed class LessonVariant : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid BundleId { get; set; }
    public Guid? ParentVariantId { get; set; }
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string SectionsJson { get; set; } = "";
    public string ReviewStatus { get; set; } = "Draft";
    public string? ReviewEvidence { get; set; }
}
public sealed class LessonCommand : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Kind { get; set; } = "";
    public Guid ResultId { get; set; }
}
