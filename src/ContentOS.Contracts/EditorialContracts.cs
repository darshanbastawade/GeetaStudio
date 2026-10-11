namespace ContentOS.Contracts;

public sealed record StartEditorial(Guid OperationId, string Key, string Format, string Brief, CreateContent? Draft = null, Guid? ContentId = null, bool PublicIntended = true);
public sealed record EditEditorial(Guid OperationId, string ExpectedRowVersion, string Title, string Body, string Kind, string RightsStatus, string Provenance);
public sealed record EditorialEvaluationBinding(Guid ReportId, string CaseKey);
public sealed record EditorialAction(Guid OperationId, string ExpectedRowVersion, string Action, string EvidenceReference,
    string Mode = "Fixture", EditorialEvaluationBinding? Evaluation = null, DateTimeOffset? ScheduledUtc = null, string Channel = "LocalPreview");
public sealed record AttachEditorialAsset(Guid OperationId, string ExpectedRowVersion, string Slot, string MediaType, byte[] Content, string EvidenceReference);
public sealed record EditorialAssetView(Guid Id, string Slot, string MediaType, Guid AssetReferenceId, Guid StorageAssetId, string Sha256, long Length, string RightsStatus, string EvidenceReference);
public sealed record EditorialApprovalView(Guid Id, Guid RevisionId, string Fingerprint, string Mode, string Reviewer, string EvidenceReference, DateTimeOffset CreatedUtc, bool Current);
public sealed record EditorialLineageNode(Guid ContentId, Guid RevisionId, int Version, string Format, string State, string Locale, Guid AudienceId);
public sealed record EditorialCopyView(Guid Id, Guid RevisionId, string Channel, string Mode, string Reference, string State, string RowVersion);
public sealed record EditorialEventView(Guid Id, string Action, string Actor, string EvidenceReference, DateTimeOffset CreatedUtc);
public sealed record EditorialPacket(Guid Id, string Key, string Format, string Brief, string Author, Guid ContentId, Guid RevisionId,
    string State, string RowVersion, bool PublicIntended, bool HighRisk, RevisionContract? Current, RevisionContract? Previous,
    EditorialAssetView[] Assets, EditorialApprovalView[] Approvals, string Fingerprint, string[] Blocks,
    CitationExport[] Citations, ClaimLabelView[] Claims, DisagreementView[] Disagreements, Guid[] SourceSnapshotIds,
    FactoryArtifactSnapshot[] FactoryHistory, LessonVariantView[] Variants, RegistryEpisodeSnapshot[] Episodes,
    AiRunView[] ProductionRuns, EvaluationReport[] Evaluations, EditorialLineageNode[] Lineage,
    EditorialCopyView[] Copies, EditorialEventView[] History, bool SourceOutputWithheld);
public sealed record EditorialSummary(Guid Id, string Key, string Format, string State, Guid ContentId, string Author, string RowVersion);
public sealed record EditorialCorrection(Guid OperationId, Guid? ClaimRevisionId, Guid? SnapshotId, string Reason, string EvidenceReference);
public sealed record EditorialCorrectionTaskView(Guid Id, Guid CorrectionId, Guid ContentId, Guid RevisionId, Guid? CopyId, string Kind, string State, string Reason, string? EvidenceReference, string RowVersion, EditorialEventView[] Attempts);
public sealed record ResolveEditorialCorrection(Guid OperationId, string ExpectedRowVersion, string Action, string EvidenceReference, Guid? ReplacementContentId = null);
public sealed record EditorialProviderStatus(AiProfileView[] Profiles, AiRouteView[] Routes, EditorialRouteHead[] Heads, EditorialProviderConnection[] Connections);
public sealed record EditorialRouteHead(Guid Id, string Task, Guid RouteId, string RowVersion);
public sealed record EditorialProviderConnection(Guid ProfileId, bool Enabled, bool Killed, bool CredentialAvailable);
public sealed record EditorialRouteCommand(Guid OperationId, string Action, Guid RouteId, string ExpectedHeadRowVersion, Guid? ProfileId = null, AiRouteSettings? Settings = null);
public sealed record EditorialTrendInput(Guid OperationId, string Key, string Headline, Guid[] ClaimRevisionIds, Guid[] SourceSnapshotIds,
    DateTimeOffset ObservedUtc, DateTimeOffset ExpiresUtc, decimal EstimatedCost, string Currency, CastingBrief Cast,
    string ScriptPreview, Guid? LessonVariantId, bool RequiresTranscript, Guid? TranscriptSnapshotId = null);
public sealed record EditorialTrendView(Guid Id, EditorialTrendInput Input, string State, string RowVersion, string[] Blocks, RegistryCastPlan? CastPreview, string? ReviewEvidence);
public sealed record ReviewEditorialTrend(Guid OperationId, string ExpectedRowVersion, string Decision, string EvidenceReference);
