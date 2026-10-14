using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IOptimizationWorkbench
{
    Task<Guid> ProposeAsync(CoreScope s, Guid u, OptimizationProposal r, CancellationToken ct);
    Task<OptimizationView[]> ListAsync(CoreScope s, Guid u, CancellationToken ct);
    Task<OptimizationView> InspectAsync(CoreScope s, Guid u, Guid id, CancellationToken ct);
    Task<OptimizationView> EvaluateAsync(CoreScope s, Guid u, Guid id, OptimizationEvaluation r, CancellationToken ct);
    Task<OptimizationView> ControlAsync(CoreScope s, Guid u, Guid id, OptimizationControl r, CancellationToken ct);
    Task<OptimizationView> MeasureAsync(CoreScope s, Guid u, Guid id, OptimizationMeasurement r, CancellationToken ct);
    Task<OptimizationGeneration> GenerateAsync(CoreScope s, Guid u, Guid id, OptimizationGenerate r, CancellationToken ct);
    Task<OptimizationOperationalReport> ReportAsync(CoreScope s, Guid u, CancellationToken ct);
    Task<OptimizationOperationalReport> AutomateAsync(CoreScope s, Guid u, OptimizationAutomation r, CancellationToken ct);
    Task MonitorAsync(CancellationToken ct);
}
