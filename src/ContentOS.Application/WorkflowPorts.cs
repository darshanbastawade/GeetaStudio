using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IWorkflowRuntime
{
    Task<Guid> RegisterPromptAsync(CoreScope scope, Guid universe, RegisterPrompt request, CancellationToken ct);
    Task<IReadOnlyList<PromptView>> PromptsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> RegisterDefinitionAsync(CoreScope scope, Guid universe, RegisterWorkflow request, CancellationToken ct);
    Task<IReadOnlyList<WorkflowDefinitionView>> DefinitionsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<DurableWorkflowView> StartAsync(CoreScope scope, Guid universe, StartWorkflow request, CancellationToken ct);
    Task<DurableWorkflowView> InspectAsync(CoreScope scope, Guid run, CancellationToken ct);
    Task<IReadOnlyList<DurableWorkflowView>> RunsAsync(CoreScope scope, CancellationToken ct);
    Task<DurableWorkflowView> ControlAsync(CoreScope scope, Guid run, string action, ControlWorkflow request, CancellationToken ct);
    Task<DurableWorkflowView> ExportCheckpointAsync(CoreScope scope, Guid run, CancellationToken ct);
}
public interface IWorkflowWorker
{
    Task<bool> ProcessNextAsync(CancellationToken ct);
}
public interface IToolInvoker
{
    IReadOnlyList<WorkflowTool> Tools { get; }
    Task<WorkflowEvidence> RetrieveAsync(CoreScope scope, Guid universe, StartWorkflow inputs, string locale, CancellationToken ct);
    Task<AiRunView> GenerateAsync(CoreScope scope, Guid universe, GenerateAi request, CancellationToken ct);
}
