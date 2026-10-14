namespace ContentOS.Domain;

public sealed class AnalyticsSource : CoreRecord { public Guid TenantId { get; set; } public Guid UniverseId { get; set; } public string Key { get; set; } = ""; public int Version { get; set; } public string SnapshotJson { get; set; } = ""; public bool Enabled { get; set; } = true; }
public sealed class AnalyticsDefinition : CoreRecord { public Guid TenantId { get; set; } public Guid UniverseId { get; set; } public string Key { get; set; } = ""; public int Version { get; set; } public string SnapshotJson { get; set; } = ""; }
public sealed class AnalyticsFx : CoreRecord { public Guid TenantId { get; set; } public Guid UniverseId { get; set; } public string Key { get; set; } = ""; public int Version { get; set; } public string SnapshotJson { get; set; } = ""; }
public sealed class AnalyticsConsent : CoreRecord { public Guid TenantId { get; set; } public Guid UniverseId { get; set; } public string SubjectReference { get; set; } = ""; public string Purpose { get; set; } = ""; public DateTimeOffset ExpiresUtc { get; set; } public bool Enabled { get; set; } = true; public bool Fixture { get; set; } public string EvidenceReference { get; set; } = ""; }
public sealed class AnalyticsEntry : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid SourceId { get; set; }
    public string SourceSystem { get; set; } = ""; public string AccountReference { get; set; } = ""; public string EventId { get; set; } = "";
    public string EconomicKey { get; set; } = ""; public string Kind { get; set; } = ""; public DateTimeOffset OccurredUtc { get; set; }
    public string Fingerprint { get; set; } = ""; public string SnapshotJson { get; set; } = ""; public string? ContentJson { get; set; }
    public decimal? ReportingAmount { get; set; }
    public string? ReportingCurrency { get; set; }
    public Guid? RevisionId { get; set; }
    public Guid? PublicationId { get; set; }
    public Guid? DefinitionId { get; set; }
    public Guid? FxId { get; set; }
    public Guid? ConsentId { get; set; }
    public Guid? RelatedEntryId { get; set; }
}
public sealed class AnalyticsForecast : CoreRecord { public Guid TenantId { get; set; } public Guid UniverseId { get; set; } public string Key { get; set; } = ""; public int Version { get; set; } public string SnapshotJson { get; set; } = ""; }
public sealed class AnalyticsCommand : CoreRecord { public Guid TenantId { get; set; } public Guid UniverseId { get; set; } public Guid OperationId { get; set; } public string Actor { get; set; } = ""; public string Kind { get; set; } = ""; public string Fingerprint { get; set; } = ""; public string RequestJson { get; set; } = ""; public string ResultJson { get; set; } = ""; }
