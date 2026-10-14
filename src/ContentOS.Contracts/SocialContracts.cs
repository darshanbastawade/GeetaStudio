namespace ContentOS.Contracts;

public sealed record SocialCapability(string Key, int Version, string Platform, string[] Formats, int TitleLimit, int TextLimit, int MaxImages, string[] RequiredScopes, string DocumentationUrl, string DocumentationStatus);
public sealed record RegisterSocialAccount(Guid OperationId, string Key, int Version, string Platform, string AccountReference, string Adapter, string Mode, string CapabilityKey, string[] AuthorizedScopes, DateTimeOffset? TokenExpiresUtc, string EvidenceReference);
public sealed record SocialAccountView(Guid Id, RegisterSocialAccount Binding, bool Enabled, string TokenState, string ConnectionState, bool Qualified, string RowVersion);
public sealed record SocialAccountControl(Guid OperationId, string ExpectedRowVersion, string Action, string EvidenceReference);
public sealed record CreateSocialPackage(Guid OperationId, string Key, Guid EditorialItemId, Guid AccountId, string Format, string CampaignId, string Caption, string Link, Guid[] ImageVersionIds, Guid? WatchProductionId = null, string Aspect = "Vertical", bool MadeForKids = false, string DestinationTitle = "");
public sealed record SocialPayload(Guid EditorialItemId, Guid RevisionId, Guid ApprovalId, string EditorialFingerprint, string Platform, string AccountReference, string Format, string Title, string Text, string Article, string CampaignId, string Link, string[] Slides, SocialMedia[] Media, bool MadeForKids, string Locale, Guid AudienceId, string ContentLabel, CitationExport[] Citations, string[] ClaimLabels);
public sealed record SocialMedia(Guid Id, string Kind, string Sha256, long Length, string Mime, string Aspect, Guid? ProductionId = null, double? DurationSeconds = null, string Role = "Image");
public sealed record SocialPackageSnapshot(CreateSocialPackage Request, RegisterSocialAccount Account, SocialCapability Capability, SocialPayload Payload, SocialPublisherCapabilities? PublisherCapabilities = null);
public sealed record SocialReview(Guid OperationId, string ExpectedRowVersion, string PayloadHash, string Decision, string Mode, string EvidenceReference);
public sealed record SocialPackageView(Guid Id, string Key, string ReviewStatus, string PayloadHash, SocialPackageSnapshot? Snapshot, string RowVersion, string[] Blocks);
public sealed record ScheduleSocial(Guid OperationId, Guid PackageId, DateTimeOffset ScheduledUtc, string Timezone, int DeadlineSeconds = 300, int MaxAttempts = 3);
public sealed record SocialControl(Guid OperationId, string ExpectedRowVersion, string Action, string EvidenceReference);
public sealed record SocialReceipt(Guid OperationId, string EventId, string Platform, string AccountReference, string Adapter, string PayloadHash, string RemoteId, string RemoteUrl, string EvidenceReference);
public sealed record SocialPublicationView(Guid Id, Guid PackageId, string State, string Platform, string AccountReference, string Adapter, DateTimeOffset ScheduledUtc, string Timezone, int Attempts, string? RemoteId, string? RemoteUrl, string? Outcome, string RowVersion, SocialHistoryView[] History, string Mode);
public sealed record SocialHistoryView(string Kind, string Detail, DateTimeOffset CreatedUtc);
public sealed record SocialNotificationView(Guid Id, Guid PublicationId, string Kind, string Detail, bool Acknowledged, string RowVersion);
public sealed record SocialExport(SocialPackageView Package, SocialPublicationView? Publication, string Status, string[] Instructions);

public sealed record SocialPublisherCapabilities(bool IdempotentSubmit, bool Reconciliation, bool Callback, string Qualification, string Version);
