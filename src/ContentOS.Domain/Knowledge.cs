namespace ContentOS.Domain;

public sealed class KnowledgeConcept : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public Guid SnapshotId { get; set; }
    public Guid? ClaimRevisionId { get; set; }
    public string TermsJson { get; set; } = "[]"; public string ReviewStatus { get; set; } = "Pending";
    public string? Reviewer { get; set; }
    public string? ReviewEvidence { get; set; }
}
public sealed class KnowledgeRelation : CoreRecord
{
    public Guid FromId { get; set; }
    public Guid ToId { get; set; }
    public string Kind { get; set; } = "";
    public Guid SnapshotId { get; set; }
    public Guid? ClaimRevisionId { get; set; }
    public string ReviewStatus { get; set; } = "Pending"; public string? Reviewer { get; set; }
    public string? ReviewEvidence { get; set; }
}
public sealed class KnowledgeIndex : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ManifestId { get; set; }
    public string Actor { get; set; } = ""; public string Key { get; set; } = ""; public string Locale { get; set; } = "";
    public string Territory { get; set; } = "IN"; public string Adapter { get; set; } = ""; public string Model { get; set; } = "";
    public int Dimensions { get; set; } = 128; public string Normalization { get; set; } = "l2"; public string Chunking { get; set; } = "verse-component-v1";
    public string MaterialJson { get; set; } = "[]"; public string EmbeddingSpaceId { get; set; } = ""; public string GraphJson { get; set; } = "[]";
    public string State { get; set; } = "Queued"; public string? Error { get; set; }
    public string? ValidationEvidence { get; set; }
    public string? ValidationJson { get; set; }
}
public sealed class KnowledgeChunk : CoreRecord
{
    public Guid IndexId { get; set; }
    public Guid StableChunkId { get; set; }
    public Guid CorpusTextId { get; set; }
    public string VectorJson { get; set; } = "[]"; public string FeaturesJson { get; set; } = "[]";
}
public sealed class KnowledgeAlias : CoreRecord
{
    public Guid UniverseId { get; set; }
    public string Locale { get; set; } = ""; public string Key { get; set; } = "";
    public Guid? ActiveIndexId { get; set; }
    public Guid? PreviousIndexId { get; set; }
}
public sealed class KnowledgeCommand : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Kind { get; set; } = ""; public string Fingerprint { get; set; } = ""; public Guid ResultId { get; set; }
}
public sealed class KnowledgeBlockedException(string reason) : Exception("Knowledge retrieval blocked: " + reason) { public string Reason { get; } = reason; }
