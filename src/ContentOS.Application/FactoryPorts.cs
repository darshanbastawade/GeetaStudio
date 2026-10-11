using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IFactoryTemplate { string Key { get; } FactoryRenderResult Render(FactoryRenderRequest request, CancellationToken ct); }
public interface IDraftFactory
{
    Task<FactoryPlanView> RegisterPlanAsync(CoreScope scope, Guid universe, RegisterFactoryPlan request, CancellationToken ct);
    Task<IReadOnlyList<FactoryPlanView>> PlansAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewPlanAsync(CoreScope scope, Guid universe, Guid id, FactoryReview request, CancellationToken ct);
    Task<FactoryEstimate> EstimateAsync(CoreScope scope, Guid universe, Guid id, CancellationToken ct);
    Task<FactoryBatchView> StartAsync(CoreScope scope, Guid universe, StartFactoryBatch request, CancellationToken ct);
    Task<IReadOnlyList<FactoryBatchView>> BatchesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ControlAsync(CoreScope scope, Guid universe, Guid id, FactoryControl request, CancellationToken ct);
    Task<IReadOnlyList<FactoryArtifactView>> ArtifactsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewArtifactAsync(CoreScope scope, Guid universe, Guid id, FactoryReview request, CancellationToken ct);
    Task<IReadOnlyList<FactoryCoverage>> CoverageAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<IReadOnlyList<FactoryPlanView>> ProposePilotsAsync(CoreScope scope, Guid universe, FactoryPilotRequest request, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
}
