using ContentOS.Contracts;
namespace ContentOS.Application;

// Deployed local evaluands only. External evaluands/judges must use M07 admission, snapshots, rights and cost policy.
public interface IEvaluationAdapter { string Key { get; } EvaluationOutput Evaluate(EvaluationInput input); }
public interface IQualityEvaluation
{
    Task<EvaluationDatasetView> RegisterDatasetAsync(CoreScope scope, Guid universe, RegisterEvaluationDataset request, CancellationToken ct);
    Task<IReadOnlyList<EvaluationDatasetView>> DatasetsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<EvaluationCandidateView> RegisterCandidateAsync(CoreScope scope, Guid universe, RegisterEvaluationCandidate request, CancellationToken ct);
    Task<IReadOnlyList<EvaluationCandidateView>> CandidatesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<EvaluationReport> RunAsync(CoreScope scope, Guid universe, RunEvaluation request, CancellationToken ct);
    Task<EvaluationReport> InspectAsync(CoreScope scope, Guid universe, Guid runId, CancellationToken ct);
    Task<IReadOnlyList<EvaluationReport>> ReportsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewAsync(CoreScope scope, Guid universe, EvaluationHumanReview request, CancellationToken ct);
    Task AdviseAsync(CoreScope scope, Guid universe, EvaluationAdvisory request, CancellationToken ct);
}
