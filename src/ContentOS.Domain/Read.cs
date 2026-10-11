namespace ContentOS.Domain;

public sealed class ReadCollection : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ManifestId { get; set; }
    public string Key { get; set; } = ""; public int Version { get; set; }
    public string Title { get; set; } = "";
    public string Mode { get; set; } = ""; public string Rationale { get; set; } = ""; public string Actor { get; set; } = "";
    public string State { get; set; } = "Draft";
}
public sealed class ReadPiece : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid CorpusVerseId { get; set; }
    public Guid EditorialItemId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid LessonVariantId { get; set; }
    public string Journey { get; set; } = ""; public int Position { get; set; }
}
public sealed class ReadEvidence : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid CollectionId { get; set; }
    public string Kind { get; set; } = ""; public string Mode { get; set; } = ""; public string Reference { get; set; } = ""; public string Notes { get; set; } = "";
    public string Fingerprint { get; set; } = ""; public string Actor { get; set; } = ""; public DateTimeOffset ExpiresUtc { get; set; }
}
public sealed class ReadOperation : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public Guid ResultId { get; set; }
    public string Kind { get; set; } = ""; public string Actor { get; set; } = ""; public string Fingerprint { get; set; } = "";
}
public sealed class ReadEventCount : CoreRecord
{
    public Guid CollectionId { get; set; }
    public Guid PieceId { get; set; }
    public DateTime Day { get; set; }
    public int Version { get; set; } = 1; public string Name { get; set; } = ""; public int Count { get; set; }
}
