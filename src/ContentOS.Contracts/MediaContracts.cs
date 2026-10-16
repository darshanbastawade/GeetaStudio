namespace ContentOS.Contracts;

public sealed record MediaLicense(string Kind, string EvidenceReference, Guid[] SourceSnapshotIds, bool Commercial, bool Adaptation,
    string Attribution, DateTimeOffset? ExpiresUtc = null, string? LikenessConsentReference = null, string? VoiceConsentReference = null);
public sealed record MediaInfo(string Mime, int? Width, int? Height, double? DurationSeconds, string Sha256, long Length);
public sealed record ImportMedia(Guid OperationId, string Key, int Version, string FileName, string AccessibilityDescription,
    string Kind, byte[] Bytes, string ExpectedSha256, MediaLicense License, Guid AudienceId, string Locale,
    Guid? CharacterProfileRevisionId = null, Guid[]? ParentVersionIds = null, Guid? GenerationId = null);
public sealed record MediaVersionView(Guid Id, Guid AssetId, int Version, Guid AssetReferenceId, Guid StorageAssetId,
    string Kind, string AccessibilityDescription, MediaInfo Info, MediaLicense License, Guid? CharacterProfileRevisionId,
    Guid[] ParentVersionIds, Guid? GenerationId, string ReviewStatus, string? ReviewEvidence, string RowVersion, string[] Blocks);
public sealed record MediaAssetView(Guid Id, string Key, int CurrentVersion, string RowVersion, MediaVersionView[] Versions);
public sealed record ReviewMedia(Guid OperationId, string ExpectedRowVersion, string Decision, string Mode, string EvidenceReference, string[] Deviations);
public sealed record MoveMedia(Guid OperationId, Guid VersionId, string StoreKey);
public sealed record MediaExport(int SchemaVersion, MediaVersionView Version, byte[] Bytes, string StoreKey);
public sealed record PronunciationMapping(string Word, string SpeakAs);
public sealed record SpeechVoiceResource(string Key, string[] Locales);
public sealed record SpeechAdapterView(string Key, MediaProviderCapabilities Capabilities, SpeechVoiceResource[] Voices);
public sealed record CharacterMediaRules(Guid ProfileRevisionId, Guid AudienceId, string Locale, string PresentationStyle,
    string VisualAnchor, string VoiceAnchor, string[] Palette, string[] StyleRules, string[] Poses, string[] SceneReferences,
    PronunciationMapping[] Pronunciation, Guid VisualVersionId, Guid VoiceVersionId, Guid[] AdditionalVersionIds,
    string VoiceProfileKey, string VoiceResourceKey);
public sealed record RegisterCharacterMedia(Guid OperationId, string Key, int Version, CharacterMediaRules Rules);
public sealed record CharacterMediaView(Guid Id, string Key, int Version, CharacterMediaRules Rules, string ReviewStatus,
    string? ReviewEvidence, Guid? PresentationRevisionId, string RowVersion, string[] Blocks);
public sealed record MediaProviderCapabilities(string Kind, string[] Locales, bool ReferenceImages, bool PronunciationSubstitution,
    bool Cloning, bool IdempotentSubmission, bool Reconciliation, string Qualification, string[] Coverage);
public sealed record RegisterMediaProfile(Guid OperationId, string Key, int Version, string AdapterKey, string AccountReference,
    string Kind, string Locale, string ResourceKey, decimal CallCeiling, string Currency, decimal DailyBudget, string Mode, string EvidenceReference);
public sealed record MediaProfileView(Guid Id, RegisterMediaProfile Configuration, MediaProviderCapabilities Capabilities, bool Enabled, string RowVersion);
public sealed record MediaProfileControl(Guid OperationId, string ExpectedRowVersion, string EvidenceReference);
public sealed record GenerateMedia(Guid OperationId, Guid ProfileId, ImportMedia Output, string PromptOrText,
    Guid[] ReferenceVersionIds, PronunciationMapping[] Pronunciation, decimal MaxCost, int DeadlineSeconds = 120,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WatchAdmission? Admission = null);
public sealed record WatchAdmission(Guid ProductionId, Guid StageId);
public sealed record MediaGenerationSnapshot(RegisterMediaProfile Profile, MediaProviderCapabilities Capabilities, GenerateMedia Request);
public sealed record MediaGenerationView(Guid Id, string State, MediaGenerationSnapshot Snapshot, string? ExternalResourceId,
    Guid? AcceptedVersionId, decimal ReservedCost, decimal? ActualCost, string? Outcome, DateTimeOffset DeadlineUtc, string RowVersion);
public sealed record MediaProviderRequest(Guid OperationId, string AccountReference, string ResourceKey, string Locale, string PromptOrText,
    byte[][] ReferenceImages, PronunciationMapping[] Pronunciation, string[] Palette, int MaxBytes, decimal Ceiling);
public sealed record MediaProviderResult(string State, string? ResourceId, byte[]? Bytes, decimal? Cost, string? Outcome = null);
public sealed record ReconcileMedia(Guid OperationId, Guid GenerationId);
public sealed record AcceptMediaResult(Guid OperationId, Guid GenerationId, string ResourceId, byte[] Bytes, string ExpectedSha256, decimal Cost);
public sealed record MediaStoreMoveResult(Guid AssetId, string StoreKey, string Sha256, long Length);
