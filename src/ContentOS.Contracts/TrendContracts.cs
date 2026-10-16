namespace ContentOS.Contracts;

public sealed record TrendMetric(string Name, decimal Value, DateTimeOffset ObservedUtc);
public sealed record TrendSignal(string ExternalId, string EventKey, string ReportOrigin, bool Primary, string Kind, string Headline,
    string Url, string Category, string Region, string Locale, DateTimeOffset? EventUtc, DateTimeOffset? PublishedUtc,
    DateTimeOffset? UpdatedUtc, DateTimeOffset ObservedUtc, Guid[] SubstanceSnapshotIds, Guid? TranscriptSnapshotId,
    bool RequiresTranscript, string[] Risks, TrendMetric[] Metrics, string ReportStatus = "Current")
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? DiscoveryContentHash { get; init; }
}
public sealed record RegisterTrendConnector(Guid OperationId, string Key, int Version, string SourceAdapter, string ExtractorAdapter,
    string Mode, string EvidenceReference, TrendSignal[] FixtureSignals, Guid? FeedSnapshotId = null, string FeedLocale = "en-IN", string FeedRegion = "IN")
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TrendLiveFeed? LiveFeed { get; init; }
}
public sealed record TrendLiveFeed(string Key, string Url, Guid PermissionSnapshotId, string PermissionReference);
public sealed record TrendConnectorView(Guid Id, RegisterTrendConnector Configuration, bool Enabled, string RowVersion);
public sealed record TrendDiscoveryRequest(RegisterTrendConnector Configuration, string? FeedXml);
public sealed record TrendDiscovery(string Contract, string Adapter, string[] Coverage, TrendSignal[] Signals);
public sealed record TrendExtractionRequest(TrendSignal Signal, IReadOnlyDictionary<Guid, string> PermittedTexts);
public sealed record TrendExtraction(string Contract, string Adapter, string EvidenceAccess, string TranscriptAccess, Guid[] SnapshotIds, string? Text);
public sealed record PollTrends(Guid OperationId, Guid ConnectorId);
public sealed record SubmitTrendSignal(Guid OperationId, Guid ConnectorId, TrendSignal Signal);
public sealed record TrendCandidateView(Guid Id, Guid ClusterId, Guid ConnectorId, TrendSignal Signal, TrendExtraction? Extraction,
    DateTimeOffset RetrievedUtc, string[] Coverage, decimal? Momentum, string[] Blocks);
public sealed record ReviewTrendCluster(Guid OperationId, string ExpectedRowVersion, string Decision, Guid? ClaimRevisionId,
    string Dilemma, DateTimeOffset ExpiresUtc, string Mode, string EvidenceReference);
public sealed record TrendClusterView(Guid Id, string Key, string State, string Dilemma, Guid? ClaimRevisionId, DateTimeOffset? CheckedUtc,
    DateTimeOffset? ExpiresUtc, string RowVersion, string Fingerprint, int IndependentReports, TrendCandidateView[] Candidates, string[] Blocks);
public sealed record LinkTrendTeaching(Guid OperationId, Guid ClusterId, Guid LessonVariantId, KnowledgeQuery Query, string Rationale, string[] Alternatives);
public sealed record TrendTeachingView(Guid Id, Guid ClusterId, Guid LessonVariantId, string Outcome, string Rationale,
    KnowledgeResult? Retrieval, string[] Alternatives, string ReviewStatus, string RowVersion, string[] Blocks)
{ public string RiskLevel { get; init; } = "HumanEditorialReviewRequired"; }
public sealed record TrendReview(Guid OperationId, string ExpectedRowVersion, string Decision, string Mode, string EvidenceReference);
public sealed record PrepareTrendStory(Guid OperationId, Guid TeachingId, string Key, int Version, string Format,
    string Analogy, string FictionalHumor, string[] Channels, RegisterFactoryPlan Plan);
public sealed record TrendPackage(string Key, Guid AudienceId, string Locale, string LearningGoal, RegistryEpisodeSnapshot Cast,
    string Hook, string Script, decimal EstimatedProductionCost);
public sealed record TrendStorySnapshot(Guid ClusterId, Guid TeachingId, string ClusterFingerprint, string Format, string FactualSummary,
    string Uncertainty, string Analogy, string ScriptureInterpretation, string InterpretationAuthor, string Tradition, string FictionalHumor,
    Guid[] EventSources, Guid[] ScriptureSources, KnowledgeResult Retrieval, DateTimeOffset CheckedUtc, DateTimeOffset ExpiresUtc,
    string[] Channels, TrendPackage[] Packages, string[] RequiredGuestRoles);
public sealed record TrendStoryView(Guid Id, Guid? FactoryPlanId, int Version, string State, TrendStorySnapshot? Snapshot,
    string RowVersion, string[] Blocks, string[] GuestProposals);
public sealed record BindTrendStory(Guid OperationId, Guid BriefId, Guid EditorialItemId);
public sealed record TrendChange(Guid OperationId, string ExpectedRowVersion, string Action, string Reason, string EvidenceReference);
public sealed record TrendRemediationView(Guid Id, Guid ClusterId, Guid BriefId, string Reason, string State, Guid? EditorialCorrectionId);
public sealed record TrendStats(int Candidates, int Verified, int Held, int Rejected, int AcceptedBriefs, int RejectedBriefs,
    decimal EstimatedProductionCost, int OpenRemediations, decimal ActualTextGenerationCost, decimal? ActualMediaCost)
{
    public decimal OutstandingTextGenerationCost { get; init; }
    public string? TextGenerationCurrency { get; init; }
}

public sealed record TrendRankWeights(double Freshness = .2, double Momentum = .05, double Evidence = .3, double Novelty = .15, double Cost = .1, double TeachingRelevance = .2);
public sealed record RankTrends(Guid AudienceId, string Locale, TrendRankWeights Weights);
public sealed record TrendRankView(Guid ClusterId, bool Eligible, double? Score, bool MomentumMissing, string[] Blocks);
