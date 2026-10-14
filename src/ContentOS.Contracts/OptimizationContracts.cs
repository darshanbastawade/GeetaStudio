namespace ContentOS.Contracts;

public sealed record OptimizationGuardrail(string Metric, decimal Minimum, decimal Maximum, int MinimumSamples);
public sealed record OptimizationProposal(Guid OperationId, string Key, int Version, string Dimension, string Hypothesis,
    Guid AudienceId, string Locale, string Cohort, string Mode, Guid BaselineCandidateId, Guid CandidateId,
    Guid? BaselineRouteId, Guid? CandidateRouteId, OptimizationGuardrail[] Guardrails, int MinimumDevelopmentCases,
    int MinimumHeldOutCases, int MaxDispatches, decimal MaxCost, string Currency, DateTimeOffset EndsUtc,
    string EvidenceReference, Guid? BaselinePromptId = null, Guid? CandidatePromptId = null);
public sealed record OptimizationEvaluation(Guid OperationId, Guid BaselineRunId, Guid DevelopmentRunId, Guid HeldOutRunId);
public sealed record OptimizationControl(Guid OperationId, string Action, string ExpectedRowVersion, string EvidenceReference);
public sealed record OptimizationMeasurement(Guid OperationId, string Metric, decimal Value, int Samples, string Origin,
    string EvidenceReference, DateTimeOffset ObservedUtc);
public sealed record OptimizationEventView(Guid Id, string Kind, string Actor, string EvidenceJson, DateTimeOffset CreatedUtc);
public sealed record OptimizationView(Guid Id, OptimizationProposal Proposal, string State, string RowVersion,
    OptimizationEventView[] History);
public sealed record OptimizationGenerate(Guid OperationId, string Cohort, Guid AudienceId, GenerateAi Request, Dictionary<string, string>? Inputs = null);
public sealed record OptimizationGeneration(Guid DispatchId, Guid SelectedRouteId, AiRunView Result, AiRunView? Shadow,
    bool CandidateAccepted);
public sealed record OptimizationAutomation(Guid OperationId, string Kind, Guid? EditorialItemId = null,
    Guid? RevisionId = null, DateTimeOffset? ScheduledUtc = null, string EvidenceReference = "fixture:operator-check", string? ExpectedRowVersion = null,
    OptimizationDependencyCheck[]? DependencyChecks = null);
public sealed record OptimizationDependencyCheck(string Key, string Kind, string PinnedVersion, string ObservedVersion,
    string OfficialDocumentation, DateTimeOffset ObservedUtc, DateTimeOffset ReviewDueUtc);
public sealed record OptimizationOperationalReport(DateTimeOffset AsOfUtc, string[] Measured, string[] Hypotheses,
    string[] Recommendations, string[] HumanGates, string[] Unavailable)
{
    public OptimizationDependencyCheck[] DependencyChecks { get; init; } = [];
}
