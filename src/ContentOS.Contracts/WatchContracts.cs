namespace ContentOS.Contracts;

public sealed record WatchTemplateRules(int FrameRate = 24, int CaptionSize = 28, int SafeMarginPercent = 10, string Font = "Nirmala UI", string Background = "#243E35", int CreditSeconds = 5);
public sealed record RegisterWatchTemplate(Guid OperationId, string Key, int Version, WatchTemplateRules Rules);
public sealed record WatchTemplateView(Guid Id, RegisterWatchTemplate Configuration, string ReviewStatus, string RowVersion);
public sealed record WatchReview(Guid OperationId, string ExpectedRowVersion, string Decision, string Mode, string EvidenceReference, string[] Deviations, int ReviewMinutes = 0);
public sealed record WatchRendererCapabilities(bool Configured, bool Playable, string[] Aspects, string[] Locales, bool IndicShaping, bool Reconciliation, string Qualification, string Version);
public sealed record RegisterWatchProfile(Guid OperationId, string Key, int Version, string Renderer, string AccountReference, Guid TemplateId, decimal CallCeiling, string Currency, string Mode, string EvidenceReference);
public sealed record WatchProfileView(Guid Id, RegisterWatchProfile Configuration, WatchRendererCapabilities Capabilities, bool Enabled, string RowVersion);
public sealed record WatchSceneInput(string Key, int ScriptStart, int ScriptLength, Guid CharacterId, Guid PresentationId, Guid? ImageVersionId, Guid? VoiceVersionId, string TranscriptEvidenceReference, Guid? MusicVersionId = null, GenerateMedia? ImageGeneration = null, GenerateMedia? VoiceGeneration = null);
public sealed record CreateWatch(Guid OperationId, string Key, int Version, Guid EditorialItemId, Guid ProfileId, WatchSceneInput[] Scenes, string[] Aspects, decimal MaxMediaCost = 0, int DeadlineSeconds = 300, Guid? TrendBriefId = null, Guid? ParentProductionId = null, string CorrectionNote = "");
public sealed record WatchMediaBinding(string SceneKey, Guid? ImageId, Guid? VoiceId, Guid? ImageJobId = null, Guid? VoiceJobId = null);
public sealed record WatchCast(RegistryCharacterView Character, RegistryRevisionView<RegistryPresentationRules> Presentation, string[] ContinuityTags, string[] WardrobeRules, string[] Roles);
public sealed record WatchScene(string Key, int ScriptStart, int ScriptLength, string Text, Guid CharacterId, Guid ProfileRevisionId, Guid PresentationId, MediaVersionView Image, MediaVersionView Voice, MediaVersionView? Music, double StartSeconds, double DurationSeconds, string TranscriptEvidenceReference);
public sealed record WatchCue(double StartSeconds, double EndSeconds, string Text, string SceneKey);
public sealed record WatchTimeline(int SchemaVersion, Guid ProductionId, Guid ScriptRevisionId, string ScriptSha256, string Title, string Script, string Locale, Guid AudienceId, string FictionLabel, string EventLabel, WatchCast[] Cast, WatchScene[] Scenes, WatchCue[] Captions, CitationExport[] Citations, ClaimLabelView[] Claims, Guid[] SourceSnapshotIds, TrendStorySnapshot? Trend, WatchTemplateView Template, WatchCreditPage[]? Credits = null);
public sealed record WatchSnapshot(CreateWatch Request, EditorialPacket Script, WatchProfileView Profile, WatchTemplateView Template, WatchCast[] Cast, TrendStorySnapshot? Trend);
public sealed record WatchStageView(Guid Id, string Kind, int Sequence, string State, int Attempts, string? Outcome, decimal ActualCost, string RowVersion);
public sealed record WatchArtifactView(Guid Id, string Aspect, Guid AssetReferenceId, Guid StorageAssetId, string Mime, string Sha256, long Length, double DurationSeconds, string RendererVersion, string[] Warnings);
public sealed record WatchProductionView(Guid Id, string Key, int Version, string State, Guid CoreRevisionId, WatchSnapshot Snapshot, WatchTimeline? Timeline, WatchStageView[] Stages, WatchArtifactView[] Artifacts, decimal CreationCost, string Currency, int ReviewMinutes, string RowVersion, string[] Blocks);
public sealed record WatchControl(Guid OperationId, string ExpectedRowVersion, string Action, string EvidenceReference);
public sealed record WatchRenderMaterial(string SceneKey, byte[] Image, byte[] Voice, byte[]? Music);
public sealed record WatchRenderRequest(Guid OperationId, string AccountReference, string ResourceKey, WatchTimeline Timeline, string Aspect, WatchRenderMaterial[] Materials);
public sealed record WatchRenderResult(string State, string? ResourceId, byte[]? Master, string Mime, decimal Cost, string RendererVersion, string[] Warnings, string? Outcome = null);
public sealed record WatchFileInfo(double DurationSeconds, int Width, int Height, double? PeakDb, bool Playable, string[] Warnings);
public sealed record WatchStoredMaster(Guid Id, string Sha256, long Length);
public sealed record WatchExport(WatchProductionView Production, string Vtt, string Srt);

public sealed record WatchCreditPage(string Text, double DurationSeconds);
