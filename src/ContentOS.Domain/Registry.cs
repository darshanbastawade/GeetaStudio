namespace ContentOS.Domain;
// Closed M10 editorial artifact kinds, not an arbitrary plugin/configuration engine.
public sealed class RegistryRevision : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Kind { get; set; } = "";
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public Guid? CharacterId { get; set; }
    public Guid? CoreRevisionId { get; set; }
    public Guid? OtherCharacterId { get; set; }
    public Guid? EpisodeId { get; set; }
    public string RulesJson { get; set; } = "";
    public string ReviewStatus { get; set; } = "Draft";
    public string? ReviewEvidence { get; set; }
    public string? EscalationEvidence { get; set; }
}
public sealed class RegistryEpisode : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public int CurrentVersion { get; set; }
}
public sealed class RegistryOperation : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid ResultId { get; set; }
}
