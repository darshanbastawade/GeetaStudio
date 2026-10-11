namespace ContentOS.Domain;

public sealed class EditorialItem : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ContentId { get; set; }
    public string Key { get; set; } = ""; public string Format { get; set; } = ""; public string Brief { get; set; } = ""; public string Author { get; set; } = "";
    public string State { get; set; } = "Brief"; public bool PublicIntended { get; set; }
    public Guid? ApprovalId { get; set; }
    public Guid? EvaluationRunId { get; set; }
    public string? EvaluationCaseKey { get; set; }
    public DateTimeOffset? ScheduledUtc { get; set; }
    public string Channel { get; set; } = "LocalPreview";
}
public sealed class EditorialApproval : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ItemId { get; set; }
    public Guid RevisionId { get; set; }
    public string Fingerprint { get; set; } = ""; public string Mode { get; set; } = ""; public string Reviewer { get; set; } = "";
    public string EvidenceReference { get; set; } = ""; public string SnapshotJson { get; set; } = "";
}
public sealed class EditorialAsset : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ItemId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid AssetReferenceId { get; set; }
    public string Slot { get; set; } = ""; public string MediaType { get; set; } = ""; public string EvidenceReference { get; set; } = "";
}
public sealed class EditorialCopy : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ItemId { get; set; }
    public Guid RevisionId { get; set; }
    public string Channel { get; set; } = ""; public string Mode { get; set; } = ""; public string Reference { get; set; } = ""; public string State { get; set; } = "Recorded";
}
public sealed class EditorialCorrectionRecord : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid? ClaimRevisionId { get; set; }
    public Guid? SnapshotId { get; set; }
    public string Reason { get; set; } = ""; public string EvidenceReference { get; set; } = "";
}
public sealed class EditorialCorrectionTask : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid CorrectionId { get; set; }
    public Guid ContentId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid? CopyId { get; set; }
    public string Kind { get; set; } = ""; public string State { get; set; } = "Open";
    public string Reason { get; set; } = ""; public string? EvidenceReference { get; set; }
}
public sealed class EditorialOperation : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public Guid ResultId { get; set; }
    public string Actor { get; set; } = ""; public string Kind { get; set; } = ""; public string Fingerprint { get; set; } = "";
}
public sealed class EditorialHistory : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ItemId { get; set; }
    public string Actor { get; set; } = ""; public string Action { get; set; } = ""; public string EvidenceReference { get; set; } = "";
}
public sealed class EditorialTrend : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public string SnapshotJson { get; set; } = ""; public string State { get; set; } = "Submitted"; public string? ReviewEvidence { get; set; }
}
public sealed class EditorialAttempt : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid TaskId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string EvidenceReference { get; set; } = "";
    public Guid? ReplacementContentId { get; set; }
}
