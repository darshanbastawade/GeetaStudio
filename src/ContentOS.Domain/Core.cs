namespace ContentOS.Domain;

public abstract class CoreRecord
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class Organization : CoreRecord { public string Name { get; set; } = ""; }
public sealed class Tenant : CoreRecord { public Guid OrganizationId { get; set; } public string Key { get; set; } = ""; public string Name { get; set; } = ""; }
public sealed class TenantMember
{
    public Guid TenantId { get; set; }
    public string Actor { get; set; } = "";
    public string Permission { get; set; } = "Reader";
}
public sealed class Universe : CoreRecord
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public bool PublicKnowledgeRead { get; set; }
}
public sealed class UniverseGrant
{
    public Guid UniverseId { get; set; }
    public Guid GranteeTenantId { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
    public bool Export { get; set; }
}
public sealed class ContentLocale { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
public sealed class AudienceProfile : CoreRecord
{
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
}
public sealed class Subject : CoreRecord
{
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
}
public sealed class UniversePack : CoreRecord
{
    public Guid UniverseId { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public int Version { get; set; } = 1;
}
public sealed class PackTerm { public Guid PackId { get; set; } public string Key { get; set; } = ""; public string Value { get; set; } = ""; }
public sealed class PackSubjectType { public Guid PackId { get; set; } public string Key { get; set; } = ""; }
public sealed class SourceRule { public Guid PackId { get; set; } public string Kind { get; set; } = ""; public string Rule { get; set; } = ""; }
public sealed class EditorialPolicy { public Guid PackId { get; set; } public string Key { get; set; } = ""; public string Rule { get; set; } = ""; }
public sealed class PackAudience { public Guid PackId { get; set; } public Guid AudienceId { get; set; } }
public sealed class PackCharacter { public Guid PackId { get; set; } public Guid CharacterId { get; set; } }
public sealed class Character : CoreRecord
{
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public string PersistenceScope { get; set; } = "Recurring";
    public string Status { get; set; } = "Draft";
}
public sealed class CharacterRevision
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public int Version { get; set; }
    public string DisplayName { get; set; } = "";
    public string LifeStage { get; set; } = "";
    public string Biography { get; set; } = "";
    public string FictionalStatus { get; set; } = "Fictional";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CharacterAlias { public Guid UniverseId { get; set; } public Guid CharacterId { get; set; } public string Name { get; set; } = ""; }
public sealed class CharacterRelationship { public Guid FromCharacterId { get; set; } public Guid ToCharacterId { get; set; } public string Kind { get; set; } = ""; }
public sealed class PresentationProfile : CoreRecord
{
    public Guid CharacterRevisionId { get; set; }
    public string LocaleId { get; set; } = "";
    public Guid AudienceId { get; set; }
    public int Version { get; set; } = 1;
    public Guid? VisualAssetReferenceId { get; set; }
    public Guid? VoiceAssetReferenceId { get; set; }
    public string Approval { get; set; } = "Draft";
}
public sealed class ContentItem : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid AudienceId { get; set; }
    public string LocaleId { get; set; } = "";
    public int CurrentVersion { get; set; }
    public string EditorialState { get; set; } = "Draft";
    public bool PublicKnowledge { get; set; }
}
public sealed class ContentRevision
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public int Version { get; set; }
    public Guid OperationId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Kind { get; set; } = "";
    public string RightsStatus { get; set; } = "Unverified";
    public string Provenance { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CoreAssetReference : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid ContentRevisionId { get; set; }
    public Guid StorageAssetId { get; set; }
    public string RightsStatus { get; set; } = "Unverified";
}
public sealed class WorkflowRun : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid ContentRevisionId { get; set; }
    public Guid OperationId { get; set; }
    public string State { get; set; } = "Planned";
}
public sealed class EpisodeCastEntry
{
    public Guid ContentRevisionId { get; set; }
    public Guid CharacterRevisionId { get; set; }
    public Guid PresentationProfileId { get; set; }
    public string Role { get; set; } = "";
}
public sealed class CoreOperation
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string Actor { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid ContentId { get; set; }
}
public sealed class CoreEvent
{
    public Guid Id { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public Guid ContentId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid OperationId { get; set; }
    public string Type { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CoreAccessException : Exception { public CoreAccessException() : base("Tenant or universe access denied.") { } }
public sealed class CoreConcurrencyException : Exception { public CoreConcurrencyException() : base("Content changed. Reload the current version before updating.") { } }
public static class CoreRules
{
    public static void Text(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new RuntimeValidationException(field + " is missing or exceeds its limit.");
    }
    public static void Revision(string title, string body, string kind, string rights, string provenance, bool publicKnowledge)
    {
        Text(title, 200, "Title"); Text(body, 16000, "Body"); Text(provenance, 1000, "Provenance");
        if (kind is not ("Canonical" or "Translation" or "Interpretation" or "CurrentEvent" or "Fiction" or "Synthetic"))
            throw new RuntimeValidationException("Unsupported content kind.");
        if (rights is not ("Unverified" or "Owned" or "Licensed" or "PublicDomain" or "Synthetic"))
            throw new RuntimeValidationException("Unsupported rights status.");
        if (publicKnowledge && (kind is not ("Canonical" or "Translation") || rights is not ("Licensed" or "PublicDomain")))
            throw new RuntimeValidationException("Public knowledge requires canonical/translation content with explicitly recorded licensed/public-domain rights.");
    }
    public static void Transition(string current, string next, string rights)
    {
        var allowed = (current, next) is ("Draft", "Review") or ("Review", "Draft") or ("Review", "Approved") or ("Approved", "Archived");
        if (!allowed || (next == "Approved" && rights == "Unverified")) throw new RuntimeValidationException("Editorial transition is invalid or rights are unverified.");
    }
}
