namespace ContentOS.Domain;

public sealed class SourceWork : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool Revoked { get; set; }
}
public sealed class SourceParty : CoreRecord { public Guid TenantId { get; set; } public string Name { get; set; } = ""; }
public sealed class SourceEdition : CoreRecord
{
    public Guid WorkId { get; set; }
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? TranslatorId { get; set; }
    public Guid PublisherId { get; set; }
    public string Language { get; set; } = "";
    public string Locator { get; set; } = "";
    public string CitationFormat { get; set; } = "{author}. {title}. {edition}. {publisher}. {locator}";
    public Guid? PermissionId { get; set; }
    public bool Revoked { get; set; }
}
public sealed class SourcePermission : CoreRecord
{
    public Guid EditionId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "Pending";
    public string EvidenceReference { get; set; } = "";
    public bool Commercial { get; set; }
    public bool Adaptation { get; set; }
    public bool Translation { get; set; }
    public bool AiProcessing { get; set; }
    public bool SnapshotRetention { get; set; }
    public string RequiredAttribution { get; set; } = "";
    public DateTimeOffset? ExpiresUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedUtc { get; set; }
}
public sealed class PermissionLanguage { public Guid PermissionId { get; set; } public string Value { get; set; } = ""; }
public sealed class PermissionTerritory { public Guid PermissionId { get; set; } public string Value { get; set; } = ""; }
public sealed class PermissionFormat { public Guid PermissionId { get; set; } public string Value { get; set; } = ""; }
public sealed class SourceSnapshot : CoreRecord
{
    public Guid EditionId { get; set; }
    public Guid? ImportPermissionId { get; set; }
    public string FileName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int Length { get; set; }
    public string? Text { get; set; }
    public string EvidenceReference { get; set; } = "";
    public DateTimeOffset RetrievedUtc { get; set; }
    public string ReviewStatus { get; set; } = "Pending";
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedUtc { get; set; }
}
public sealed class ProvenanceEdge : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid? SnapshotId { get; set; }
    public Guid? ParentRevisionId { get; set; }
    public Guid? ContentRevisionId { get; set; }
    public Guid? AssetReferenceId { get; set; }
    public string Relation { get; set; } = "";
}
public sealed class RightsDecision : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid? PermissionId { get; set; }
    public string Actor { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Language { get; set; } = "";
    public string Territory { get; set; } = "";
    public string Format { get; set; } = "";
    public bool Commercial { get; set; }
    public bool Adaptation { get; set; }
    public bool Translation { get; set; }
    public bool AiProcessing { get; set; }
    public bool Allowed { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class SourceUse : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid OperationId { get; set; }
    public Guid ContentRevisionId { get; set; }
    public string Purpose { get; set; } = "";
    public bool Published { get; set; }
    public string ExternalReference { get; set; } = "";
    public string Mode { get; set; } = "Recorded";
}
public sealed class SourceRevocation : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid WorkId { get; set; }
    public Guid? EditionId { get; set; }
    public Guid OperationId { get; set; }
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
}
public sealed class RemediationTask : CoreRecord
{
    public Guid RevocationId { get; set; }
    public Guid SourceUseId { get; set; }
    public string Status { get; set; } = "Open";
    public string? ResolutionEvidence { get; set; }
}
public sealed class SourceCommand : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Kind { get; set; } = "";
    public Guid ResultId { get; set; }
}
public sealed class SourceAudit : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid SubjectId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
}
public sealed class RightsBlockedException(string reason) : Exception("Source rights block this use: " + reason)
{
    public string Reason { get; } = reason;
}
public static class SourceRules
{
    public static void Use(string purpose, string language, string territory, string format)
    {
        if (purpose is not ("Read" or "Generate" or "Export" or "Publish" or "Translate" or "Adapt"))
            throw new RuntimeValidationException("Unsupported intended use.");
        CoreRules.Text(language, 40, "Language"); CoreRules.Text(territory, 40, "Territory");
        if (format is not ("Text" or "Json" or "Audio" or "Video" or "Image")) throw new RuntimeValidationException("Unsupported format.");
    }
    public static string Decide(string status, bool revoked, bool reviewed, DateTimeOffset? expires, DateTimeOffset now,
        bool language, bool territory, bool format, bool allowCommercial, bool allowAdaptation, bool allowTranslation, bool allowAi,
        string purpose, bool commercial, bool adaptation, bool translation, bool ai)
    {
        if (revoked) return "Revoked";
        if (status != "Approved") return "UnknownRights";
        if (!reviewed) return "UnreviewedEvidence";
        if (expires <= now) return "Expired";
        if (!language) return "LanguageNotPermitted";
        if (!territory) return "TerritoryNotPermitted";
        if (!format) return "FormatNotPermitted";
        if (commercial && !allowCommercial) return "CommercialUseNotPermitted";
        if ((adaptation || purpose is "Generate" or "Adapt") && !allowAdaptation) return "AdaptationNotPermitted";
        if ((translation || purpose == "Translate") && !allowTranslation) return "TranslationNotPermitted";
        if ((ai || purpose == "Generate") && !allowAi) return "AiProcessingNotPermitted";
        return "Allowed";
    }
}
