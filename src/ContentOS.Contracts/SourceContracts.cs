namespace ContentOS.Contracts;

public sealed record RegisterSource(Guid OperationId, string Key, string Title, string Kind);
public sealed record RegisterEdition(Guid OperationId, string Key, string Author, string? Translator, string Publisher,
    string Language, string Locator, string CitationFormat = "{author}. {title}. {edition}. {publisher}. {locator}");
public sealed record GrantSourcePermission(Guid OperationId, string EvidenceReference, string[] Languages, string[] Territories,
    string[] Formats, bool Commercial, bool Adaptation, bool Translation, bool AiProcessing, bool SnapshotRetention,
    string RequiredAttribution, DateTimeOffset? ExpiresUtc = null);
public sealed record ReviewSource(string ExpectedRowVersion, string Status);
public sealed record ImportSourceFile(Guid OperationId, string FileName, byte[] Content, string ExpectedSha256,
    string EvidenceReference, bool RetainFullSnapshot, DateTimeOffset? RetrievedUtc = null);
public sealed record SourceWorkView(Guid Id, Guid TenantId, Guid UniverseId, string Key, string Title, string Kind, bool Revoked, string RowVersion);
public sealed record EditionView(Guid Id, Guid WorkId, string Key, int Version, string Language, string Locator,
    Guid? PermissionId, bool Revoked, string RowVersion);
public sealed record SnapshotView(Guid Id, Guid EditionId, string FileName, string Sha256, int Length, bool SnapshotRetained,
    string EvidenceReference, string ReviewStatus, DateTimeOffset RetrievedUtc, DateTimeOffset ImportedUtc, string RowVersion);
public sealed record PermissionView(Guid Id, Guid EditionId, int Version, string Status, string EvidenceReference,
    IReadOnlyList<string> Languages, IReadOnlyList<string> Territories, IReadOnlyList<string> Formats,
    bool Commercial, bool Adaptation, bool Translation, bool AiProcessing, bool SnapshotRetention,
    string RequiredAttribution, DateTimeOffset? ExpiresUtc, string RowVersion);
public sealed record SourceDetail(SourceWorkView Work, IReadOnlyList<EditionView> Editions,
    IReadOnlyList<SnapshotView> Snapshots, IReadOnlyList<PermissionView> Permissions);
public sealed record IntendedUse(string Purpose, string Language, string Territory, string Format,
    bool Commercial = false, bool Adaptation = false, bool Translation = false, bool AiProcessing = false);
public sealed record RightsDecisionView(Guid Id, Guid SnapshotId, Guid? PermissionId, bool Allowed, string Reason, IntendedUse Use, DateTimeOffset CheckedUtc);
public sealed record LinkEvidence(Guid OperationId, Guid? SnapshotId, Guid? ParentRevisionId, Guid? ContentRevisionId,
    Guid? AssetReferenceId, string Relation);
public sealed record CitationExport(Guid SnapshotId, Guid EditionId, Guid PermissionId, string Citation, string RequiredAttribution,
    string Sha256, string EvidenceReference, bool SnapshotRetained);
public sealed record ProvenanceExport(Guid Id, Guid? SnapshotId, Guid? ParentRevisionId, Guid? ContentRevisionId, Guid? AssetReferenceId, string Relation);
public sealed record RightsBundle(IReadOnlyList<CitationExport> Citations, IReadOnlyList<ProvenanceExport> Provenance);
public sealed record SourceAwareExport(int SchemaVersion, ContentExport Content, IntendedUse Use);
public sealed record RecordSourceUse(Guid OperationId, Guid ContentRevisionId, IntendedUse Use, bool Published,
    string ExternalReference, string Mode = "Fixture");
public sealed record RevokeSource(Guid OperationId, Guid? EditionId, string Reason, string ExpectedRowVersion);
public sealed record ImpactedDerivative(Guid? ContentRevisionId, Guid? AssetReferenceId);
public sealed record RemediationView(Guid Id, Guid RevocationId, Guid SourceUseId, Guid ContentRevisionId,
    string ExternalReference, string Mode, string Status, string? ResolutionEvidence, string RowVersion);
public sealed record CorpusDerivative(Guid ManifestId, Guid VerseId, string Kind);
public sealed record RevocationImpact(Guid Id, IReadOnlyList<ImpactedDerivative> Derivatives, IReadOnlyList<RemediationView> Remediation, IReadOnlyList<CorpusDerivative>? Corpus = null);
public sealed record ResolveRemediation(string ExpectedRowVersion, string EvidenceReference);
public sealed record SourceReviewQueue(IReadOnlyList<SnapshotView> Snapshots, IReadOnlyList<PermissionView> Permissions);

