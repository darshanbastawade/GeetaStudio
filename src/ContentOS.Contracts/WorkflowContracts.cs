namespace ContentOS.Contracts;

public sealed record PromptInput(string Name, int MaxLength, bool Required = true);
public sealed record RegisterPrompt(string Key, int Version, string Template, PromptInput[] Inputs,
    string Locale, Guid AudienceId, string OutputSchema = "generated-text.v1", bool RequireEvidence = false);
public sealed record PromptView(Guid Id, RegisterPrompt Settings);
public sealed record WorkflowStepDefinition(string Kind, string? Tool, int MaxAttempts = 1);
public sealed record RegisterWorkflow(string Key, int Version, Guid PromptId, Guid RouteId,
    WorkflowStepDefinition[] Steps, decimal MaxCost, string Currency = "USD", int MaxTokens = 40000,
    int DeadlineSeconds = 600, int LeaseSeconds = 90, bool HumanReview = true);
public sealed record WorkflowDefinitionView(Guid Id, RegisterWorkflow Settings);
public sealed record StartWorkflow(Guid OperationId, Guid DefinitionId, Dictionary<string, string> Inputs,
    Guid[]? SourceSnapshotIds = null, KnowledgeQuery? Query = null, string? PlanJson = null, Guid? EvidenceContentRevisionId = null);
public sealed record WorkflowSnapshot(PromptView Prompt, WorkflowDefinitionView Definition, AiRouteView Route,
    AiProfileView[] Profiles, string Engine = "owned-sequence.v1", string Schema = "workflow-checkpoint.v1");
public sealed record WorkflowEvidence(Guid[] SourceIds, string Data, KnowledgeResult? Retrieval = null);
public sealed record WorkflowPlan(string[] Steps);
public sealed record WorkflowStepView(Guid Id, int Number, string Kind, Guid OperationId, Guid? AiRunId,
    string State, string Outcome, int Attempts, DateTimeOffset? LeaseUntilUtc, string? ResultJson);
public sealed record WorkflowDecisionView(Guid Id, string Action, string Actor, string EvidenceReference);
public sealed record DurableWorkflowView(Guid Id, Guid OperationId, string State, string Outcome, int NextStep,
    DateTimeOffset DeadlineUtc, WorkflowSnapshot Snapshot, StartWorkflow Inputs,
    IReadOnlyList<WorkflowStepView> Steps, IReadOnlyList<WorkflowDecisionView> Decisions, string RowVersion, bool OutputWithheld = false);
public sealed record ControlWorkflow(Guid OperationId, string ExpectedRowVersion, string EvidenceReference, bool Approve = true);
public sealed record WorkflowTool(string Key, string InputSchema, string OutputSchema, string RequiredPermission, string SideEffect);
