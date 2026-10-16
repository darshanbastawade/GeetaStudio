namespace ContentOS.Contracts;

public sealed record FactoryFormat(string Kind, string Instruction, int MaxCharacters = 8000, decimal EstimatedMediaCost = 0, int ReviewMinutes = 5, int MediaMinutes = 0);
public sealed record FactoryBrief(string Key, Guid LessonVariantId, CastingBrief Cast, Guid[] RelationshipRevisionIds, string[] Concepts, string LearningGoal,
    int Chapter, int Verse, string Journey, int Position, int Priority, FactoryFormat[] Formats);
public sealed record RegisterFactoryPlan(Guid OperationId, string Key, int Version, string Title, string JourneyKind, string Adapter, FactoryBrief[] Briefs, double DuplicateThreshold = .92)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? WorkflowDefinitionId { get; init; }
}
public sealed record FactoryBriefSnapshot(FactoryBrief Brief, LessonVariantView Lesson, RegistryEpisodeSnapshot Cast);
public sealed record FactoryPlanSnapshot(RegisterFactoryPlan Rules, FactoryBriefSnapshot[] Briefs)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowSnapshot? GenerationWorkflow { get; init; }
}
public sealed record FactoryPlanView(Guid Id, FactoryPlanSnapshot? Snapshot, string ReviewStatus, string? ReviewEvidence, string RowVersion, bool OutputWithheld);
public sealed record FactoryReview(Guid OperationId, string ExpectedRowVersion, bool Approve, string EvidenceReference);
public sealed record FactoryBudget(decimal MaxGenerationCost, string Currency, int MaxGeneratedCharacters, int DeadlineSeconds = 300, int MaxItems = 60);
public sealed record StartFactoryBatch(Guid OperationId, string BusinessKey, Guid PlanId, FactoryBudget Budget);
public sealed record FactoryControl(Guid OperationId, string ExpectedRowVersion, string Action);
public sealed record FactoryWorkView(Guid Id, string BriefKey, string Format, string State, int Attempts, Guid? ArtifactId, string Outcome)
{
    public Guid? WorkflowId { get; init; }
    public string? WorkflowState { get; init; }
}
public sealed record FactoryBatchView(Guid Id, string BusinessKey, Guid PlanId, string State, string Outcome, int GeneratedCharacters, decimal GenerationCost,
    FactoryBudget Budget, DateTimeOffset DeadlineUtc, string RowVersion, FactoryWorkView[] Work)
{
    public decimal GenerationReserved { get; init; }
    public decimal EstimatedGenerationCost { get; init; }
}
public sealed record FactoryRenderRequest(FactoryBriefSnapshot Snapshot, FactoryFormat Format);
public sealed record FactoryRenderResult(string Body, string OriginalTreatment, string TemplateVersion);
public sealed record FactoryArtifactSnapshot(Guid PlanId, string BriefKey, string Format, Guid LessonVariantId, FactoryBriefSnapshot Input, string Adapter, FactoryRenderResult Output)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FactoryGenerationProvenance? Generation { get; init; }
}
public sealed record FactoryGenerationProvenance(Guid WorkflowId, Guid AiRunId, string Schema, FactoryGeneratedScript Script)
{
    public Guid FictionClaimRevisionId { get; init; }
}
public sealed record FactoryDialogue(Guid CharacterId, string Text);
public sealed record FactoryGeneratedScript(string Schema, string Hook, string Scene, FactoryDialogue[] Dialogue, string Decision, string Reflection);
public sealed record FactoryArtifactView(Guid Id, Guid CoreContentId, Guid CoreRevisionId, Guid? ParentArtifactId, string ReviewStatus, string? ReviewEvidence,
    string RowVersion, FactoryArtifactSnapshot? Snapshot, bool OutputWithheld);
public sealed record FactoryCoverage(int Chapter, int Verse, string Concept, Guid AudienceId, string Locale, string Format, int Planned, int Generated,
    int Reviewed, int Published, int PubliclyUsable, int Reused, int RejectedDuplicates, int Withheld, int TopicFatigue, int SuggestedPriority);
public sealed record FactoryEstimate(Guid PlanId, int Items, decimal GenerationCost, decimal EstimatedMediaCost, int ReviewMinutes, int MediaMinutes, int MaximumCharacters)
{
    public string? GenerationCurrency { get; init; }
    public string? EstimatedMediaCurrency { get; init; }
}
public sealed record FactoryPilotRequest(Guid OperationId, string Key, Guid[] LessonVariantIds, CastingBrief Cast, Guid[] Relationships);
