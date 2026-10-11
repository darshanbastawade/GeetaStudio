namespace ContentOS.Contracts;

public sealed record TenantView(Guid Id, string Key, string Name, string Permission);
public sealed record UniverseView(Guid Id, Guid TenantId, string Key, string Name, bool PublicKnowledgeRead, string RowVersion);
public sealed record AudienceView(Guid Id, string Key, string Name, int MinAge, int MaxAge);
public sealed record SubjectView(Guid Id, string Key, string Type, string Name);
public sealed record CharacterProfile(Guid CharacterId, Guid RevisionId, int Version, string Key, string DisplayName,
    string LifeStage, string Biography, string FictionalStatus, string PersistenceScope, string Status, IReadOnlyList<string> Aliases);
public sealed record CharacterRelationshipContract(Guid FromCharacterId, Guid ToCharacterId, string Kind);
public sealed record PresentationProfileContract(Guid Id, Guid CharacterRevisionId, string LocaleId, Guid AudienceId,
    int Version, Guid? VisualAssetReferenceId, Guid? VoiceAssetReferenceId, string Approval);
public sealed record EpisodeCastContract(Guid ContentRevisionId, Guid CharacterRevisionId, Guid PresentationProfileId, string Role);
public sealed record AssetReferenceContract(Guid Id, Guid TenantId, Guid ContentRevisionId, Guid StorageAssetId, string RightsStatus);
public sealed record WorkflowRunContract(Guid Id, Guid TenantId, Guid ContentRevisionId, Guid OperationId, string State);
public sealed record UniversePackManifest(int SchemaVersion, int Version, Guid Id, Guid UniverseId,
    IReadOnlyDictionary<string, string> Terminology, IReadOnlyList<string> SubjectTypes,
    IReadOnlyDictionary<string, string> SourceRules, IReadOnlyList<AudienceView> Audiences,
    IReadOnlyList<Guid> CharacterIds, IReadOnlyDictionary<string, string> EditorialPolicies);
public sealed record UniverseCatalog(UniverseView Universe, UniversePackManifest Manifest, IReadOnlyList<SubjectView> Subjects,
    IReadOnlyList<CharacterProfile> Characters, IReadOnlyList<string> Locales);
public sealed record CreateContent(Guid OperationId, Guid SubjectId, Guid AudienceId, string LocaleId,
    string Title, string Body, string Kind, string RightsStatus, string Provenance, bool PublicKnowledge = false);
public sealed record ReviseContent(Guid OperationId, string ExpectedRowVersion, string Title, string Body,
    string Kind, string RightsStatus, string Provenance);
public sealed record ChangeEditorialState(Guid OperationId, string ExpectedRowVersion, string State);
public sealed record RevisionContract(Guid Id, int Version, Guid OperationId, string Title, string Body,
    string Kind, string RightsStatus, string Provenance, DateTimeOffset CreatedUtc);
public sealed record ContentView(Guid Id, Guid TenantId, Guid UniverseId, Guid SubjectId, Guid AudienceId, string LocaleId,
    int CurrentVersion, string EditorialState, bool PublicKnowledge, string RowVersion, DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc, IReadOnlyList<RevisionContract> Revisions, IReadOnlyList<ClaimLabelView>? EvidenceLabels = null);
public sealed record ContentExport(int SchemaVersion, Guid TenantId, Guid UniverseId, ContentView Content, IReadOnlyList<CitationExport>? Citations = null, IReadOnlyList<ProvenanceExport>? Provenance = null, IReadOnlyList<ClaimLabelView>? EvidenceLabels = null);
public sealed record ImportContent(Guid OperationId, ContentExport Document);
public sealed record ShareUniverse(Guid GranteeTenantId, bool Read, bool Write, bool Export, string ExpectedRowVersion);
public sealed record DomainEventV1(int SchemaVersion, Guid Id, Guid TenantId, Guid UniverseId, Guid ContentId, Guid RevisionId,
    Guid OperationId, string Type, DateTimeOffset CreatedUtc);
