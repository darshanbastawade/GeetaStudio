namespace ContentOS.Contracts;

public sealed record EvaluationCase(string Key, string Split, string Category, string Input, string Locale, Guid AudienceId,
    string Capability, string Severity, Guid[] ExpectedTextIds, string[] RequiredProperties, string[] ForbiddenProperties,
    bool ExpectNoAnswer, bool RequiresMedia, string[] VerifiedClaims, string[] AllowedLifeStages, string[] Continuity,
    string Rubric, CastingBrief? Cast = null, string? ExpectedAlias = null, Guid? ExpectedCharacterId = null, Guid? MediaSnapshotId = null, string? RetrievalQuery = null);
public sealed record RegisterEvaluationDataset(Guid OperationId, string Key, int Version, string Rationale, EvaluationCase[] Cases);
public sealed record EvaluationDatasetView(Guid Id, RegisterEvaluationDataset Dataset, int HumanReviewedCases, int FixtureReviewedCases);
public sealed record EvaluationConfiguration(string Adapter, string PromptVersion, string RetrievalVersion, string ConfigurationVersion, string ReleaseKey, string EvaluandAdapter = "unqualified-recorded", Guid? AiRouteId = null, KnowledgeQuery? Retrieval = null, Guid? RetrievalIndexId = null);
public sealed record RegisterEvaluationCandidate(Guid OperationId, string Key, int Version, EvaluationConfiguration Configuration);
public sealed record EvaluationCandidateView(Guid Id, RegisterEvaluationCandidate Candidate);
public sealed record EvaluationCitation(Guid TextId, string? ExactQuote = null);
public sealed record EvaluationCastMember(Guid CharacterId, Guid ProfileId, string Role, string VoiceAnchor, string LifeStage);
public sealed record EvaluationOutput(string Body, string Locale, EvaluationCitation[] Citations, bool NoAnswer,
    EvaluationCastMember[] Cast, string[] Claims, string? MediaEvidenceReference, string[] Continuity, decimal Cost, string Currency);
public sealed record EvaluationInput(EvaluationCase Case, EvaluationCitation[] Evidence, EvaluationCastMember[] Cast, string Version);
public sealed record RunEvaluation(Guid OperationId, Guid DatasetId, Guid CandidateId, string Split, Guid? BaselineRunId = null,
    EvaluationRecordedOutput[]? RecordedOutputs = null, int DeadlineSeconds = 120, decimal MaxCost = 0);
public sealed record EvaluationRecordedOutput(string CaseKey, EvaluationOutput Output, Guid? FactoryArtifactId = null, Guid? AiRunId = null);
public sealed record EvaluationFinding(string Code, string Severity, string Detail);
public sealed record EvaluationCaseResult(string CaseKey, string Locale, string Capability, EvaluationOutput? Output,
    double LatencyMs, EvaluationFinding[] Deterministic, bool Passed)
{
    public EvaluationCase? Case { get; init; }
    public bool RetrievalSample { get; init; }
    public string LatencyKind { get; init; } = "LocalProcessing";
    public Guid? AiRunId { get; init; }
    public Guid? FactoryArtifactId { get; init; }
    public EvaluationRetrievalSnapshot? Retrieval { get; init; }
}
public sealed record EvaluationHumanReview(Guid OperationId, Guid DatasetId, string CaseKey, Guid? RunId, string Mode,
    int FaithfulMeaning, int Usefulness, int Readability, int AgeSuitability, int? CharacterAppeal, int? Understanding,
    bool Accept, string EvidenceReference, string RubricVersion, string Notes);
public sealed record EvaluationAdvisory(Guid OperationId, Guid RunId, string CaseKey, string JudgeAdapter, string JudgeVersion,
    string PromptVersion, double Score, string Rationale, string EvidenceReference);
public sealed record EvaluationQualification(string Status, string[] Blocks, int HumanAccepted, int FixtureAccepted, int AdvisoryCount,
    double RetrievalAccuracy, double? RetrievalTarget, double P95LatencyMs, double? LatencyTargetMs, decimal Cost,
    string[] TestedCapabilitiesAndLocales, string[] PassedCapabilitiesAndLocales)
{ public string Currency { get; init; } = "INR"; }
public sealed record EvaluationReport(Guid Id, Guid DatasetId, Guid CandidateId, string Split, Guid? BaselineRunId,
    EvaluationConfiguration Configuration, EvaluationCaseResult[] Results, EvaluationQualification Qualification, DateTimeOffset CreatedUtc)
{
    public string DatasetKey { get; init; } = "";
    public int DatasetVersion { get; init; }
    public string CandidateKey { get; init; } = "";
    public int CandidateVersion { get; init; }
    public double FailureRate { get; init; }
    public EvaluationHumanReview[] DatasetLabels { get; init; } = [];
    public EvaluationHumanReview[] HumanResults { get; init; } = [];
    public EvaluationHumanReview[] FixtureResults { get; init; } = [];
    public EvaluationAdvisory[] AdvisoryResults { get; init; } = [];
}





public sealed record EvaluationRetrievalSnapshot(Guid IndexId, string EmbeddingSpaceId, string Adapter, string Model, string Outcome, Guid[] CorpusTextIds);
