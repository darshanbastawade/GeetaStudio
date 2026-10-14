namespace ContentOS.Contracts;

public sealed record AnalyticsSourceRequest(Guid OperationId, string Key, int Version, string Adapter, string SourceSystem, string AccountReference, string EvidenceReference);
public sealed record AnalyticsSourceView(Guid Id, AnalyticsSourceRequest Snapshot, bool Enabled, string RowVersion);
public sealed record AnalyticsDefinitionRequest(Guid OperationId, string Key, int Version, string Unit, string Aggregation, string Description, string EvidenceReference);
public sealed record AnalyticsDefinitionView(Guid Id, AnalyticsDefinitionRequest Snapshot);
public sealed record AnalyticsFxRequest(Guid OperationId, string Key, int Version, string FromCurrency, string ToCurrency, decimal Rate, DateTimeOffset EffectiveUtc, string EvidenceReference);
public sealed record AnalyticsFxView(Guid Id, AnalyticsFxRequest Snapshot);
public sealed record AnalyticsConsentRequest(Guid OperationId, string SubjectReference, string Purpose, DateTimeOffset ExpiresUtc, string EvidenceReference, bool Fixture = true);
public sealed record AnalyticsConsentView(Guid Id, string Purpose, bool Enabled, DateTimeOffset ExpiresUtc, bool Fixture, string RowVersion);
public sealed record AnalyticsControl(Guid OperationId, string ExpectedRowVersion, string Action, string EvidenceReference);
public sealed record AnalyticsEntryInput(string EventId, string EconomicKey, string Kind, DateTimeOffset OccurredUtc, string EvidenceReference,
    Guid? RevisionId = null, Guid? PublicationId = null, string Campaign = "", string AcquisitionChannel = "Unknown", string DiscoverySource = "Unknown",
    string RevenueStream = "", string CostCategory = "", string FinancialState = "Earned", string Currency = "INR",
    decimal Gross = 0, decimal Tax = 0, decimal Refunds = 0, decimal Fees = 0, decimal PassThrough = 0, decimal Amount = 0,
    Guid? FxId = null, Guid? RelatedEntryId = null, string SettlementReference = "", Guid? DefinitionId = null, decimal? MetricValue = null,
    DateTimeOffset? WindowStartUtc = null, DateTimeOffset? WindowEndUtc = null, DateTimeOffset? ObservedUtc = null, string Coverage = "Available",
    Guid? ConsentId = null, string Activity = "", decimal? LearningScore = null, string SessionReference = "",
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid? ProductionId = null);
public sealed record AnalyticsImport(Guid OperationId, Guid SourceId, AnalyticsEntryInput[] Entries);
public sealed record AnalyticsImportResult(Guid[] EntryIds, int Inserted, int Duplicates, string Adapter);
public sealed record AnalyticsContentSnapshot(Guid RevisionId, Guid ContentId, Guid AudienceId, string Locale, string ContentMode, string Format,
    Guid[] TrendBriefIds, Guid[] TrendClusterIds, Guid[] CharacterRevisionIds, string[] DiscoverySources, Guid[] CharacterProfileRevisionIds, Guid[]? SourceRevisionIds = null);
public sealed record AnalyticsEntryView(Guid Id, Guid SourceId, AnalyticsEntryInput Event, AnalyticsContentSnapshot? Content,
    decimal? ReportingAmount, string? ReportingCurrency, string Fingerprint, DateTimeOffset ReceivedUtc);
public sealed record AnalyticsGroup(string Dimension, string Key, decimal RecognizedNet, decimal EstimatedNet, decimal Costs, decimal Settled, int Entries);
public sealed record AnalyticsMetric(string Key, int Version, string Unit, string Aggregation, string SourceSystem, string AccountReference,
    Guid? RevisionId, Guid? PublicationId, string Campaign, decimal? Value, int AvailableWindows, int MissingWindows, string Coverage);
public sealed record AnalyticsVariant(Guid RevisionId, AnalyticsContentSnapshot Content, int ApprovedOutputs, decimal? CostPerApprovedOutput,
    decimal ProductionCost, decimal ReviewCost, decimal ReworkCost, decimal Overhead, decimal? ContributionMargin,
    double? TimeToPublishSeconds, bool Corrected, bool CurrentlyApproved, decimal? LearningFeedbackMean, string LearningCoverage);
public sealed record AnalyticsCohort(string Cohort, int? ConsentedSubjects, decimal? Conversion, decimal? Retention, decimal? LearningMean, string Coverage);
public sealed record AnalyticsReport(string Currency, DateTimeOffset AsOfUtc, DateTimeOffset StartsUtc, DateTimeOffset EndsUtc,
    decimal RecognizedNet, decimal EstimatedNet, decimal Costs, decimal Settled, decimal UnsettledDifference,
    AnalyticsGroup[] Groups, AnalyticsMetric[] Metrics, AnalyticsVariant[] Variants, AnalyticsCohort[] Cohorts, string[] Gaps,
    int ExcludedMissingFx, int SuppressedOrRevokedActivities, decimal? CorrectionRate, string[] UnresolvedOwnedCosts, AnalyticsSessionCost[] SessionCosts, AnalyticsEpisodeCost[] EpisodeCosts);
public sealed record AnalyticsReportQuery(string Currency, DateTimeOffset StartsUtc, DateTimeOffset EndsUtc, DateTimeOffset? AsOfUtc = null);
public sealed record AnalyticsCapture(Guid OperationId);
public sealed record AnalyticsForecastRequest(Guid OperationId, string Key, int Version, string Currency, string RevenueStream, string AcquisitionChannel,
    decimal Audience, decimal Conversion, decimal UnitPrice, decimal Retention, int Periods, bool PlatformPayoutEligible,
    decimal PayoutPerThousand, decimal MediaSpend, decimal AiToolCost, decimal OperatingOverhead, decimal ReviewCost,
    decimal LowMultiplier, decimal HighMultiplier, string AssumptionEvidence, DateTimeOffset StartsUtc, DateTimeOffset EndsUtc);
public sealed record AnalyticsForecastView(Guid Id, AnalyticsForecastRequest Assumptions, decimal ProjectedGross, decimal ProjectedCost,
    decimal ProjectedContribution, decimal LowGross, decimal HighGross, decimal ActualRecognizedNet, decimal ActualCost,
    string ActualCoverage, string Warning);
public sealed record AnalyticsProviderInfo(string Key, string ContractVersion, string Status, string[] Capabilities, int MaxEvents,
    string Permission, string DataPolicy, string Documentation, string VerificationDate);

public sealed record AnalyticsSessionCost(string Cohort, int? Sessions, decimal? CostPerSession, string Coverage);

public sealed record AnalyticsEpisodeCost(Guid ProductionId, Guid RevisionId, bool CurrentlyApproved, decimal? CostPerApprovedEpisode, string Coverage);
