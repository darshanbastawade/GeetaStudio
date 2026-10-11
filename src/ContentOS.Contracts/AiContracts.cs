namespace ContentOS.Contracts;

// Model names/prices are versioned provider configuration, never task enums.
public sealed record AiPricing(string Version, string Currency, decimal InputPerMillion, decimal CachedInputPerMillion, decimal OutputPerMillion);
public sealed record AiPolicy(string[] Classifications, string Region, int RetentionDays, bool Training, string PolicyVersion);
public sealed record AiProviderSettings(string Adapter, string Model, string Capability, string Locale,
    bool Structured, bool Streaming, bool Tools, int MaxInputBytes, int MaxOutputTokens,
    AiPricing Pricing, AiPolicy Policy, string Qualification, string EvidenceReference, string Scenario = "success");
public sealed record RegisterAiProfile(AiProviderSettings Settings, string? SecretRef = null, int MaxConcurrency = 2);
public sealed record AiProfileView(Guid Id, Guid ConnectionId, AiProviderSettings Settings);
public sealed record AiRouteSettings(string Task, string Version, Guid[] Profiles, decimal RunLimit, string Currency,
    int DeadlineSeconds = 60, bool Structured = true, bool Streaming = false, bool Tools = false,
    string Classification = "Public", string Region = "local", int MaxRetentionDays = 0, bool AllowTraining = false,
    string PolicyVersion = "owned-policy.v1", string PromptVersion = "generated-text.v1");
public sealed record AiRouteView(Guid Id, AiRouteSettings Settings, bool Active);
public sealed record ConfigureAiBudget(string Capability, string Currency, DateTimeOffset StartsUtc, DateTimeOffset EndsUtc, decimal Limit);
public sealed record AiBudgetView(Guid Id, string Capability, string Currency, DateTimeOffset StartsUtc, DateTimeOffset EndsUtc, decimal Limit, decimal Reserved, decimal Spent, string RowVersion);
public sealed record GenerateAi(Guid OperationId, string Task, string Prompt, string Locale = "en-IN",
    string Classification = "Public", string Region = "local", int MaxRetentionDays = 0, bool AllowTraining = false,
    bool Structured = true, bool Streaming = false, bool Tools = false, Guid[]? SourceSnapshotIds = null,
    KnowledgeQuery? EvidenceQuery = null, Guid? PinnedRouteId = null)
{
    // Host-owned lease proof: never accepted from HTTP JSON or included in replay fingerprints.
    [System.Text.Json.Serialization.JsonIgnore]
    public WorkflowAiAdmission? WorkflowAdmission { get; init; }
}
public sealed record WorkflowAiAdmission(Guid RunId, Guid StepId, Guid LeaseToken);
public sealed record AiUsage(long InputTokens, long CachedInputTokens, long OutputTokens);
public sealed record GeneratedText(string Text, string[] Citations);
public sealed record AiExecutionSnapshot(Guid RouteId, AiRouteSettings Route, AiProfileView[] Profiles,
    string Locale, string RequestHash, string[] SourceIds, string OutputLabel = "GeneratedDraft");
public sealed record AiAttemptView(Guid Id, int Number, Guid ProfileId, Guid ConnectionId, string State, string Outcome,
    AiUsage? Usage, decimal Ceiling, decimal Cost, string? ProviderRequestId, string? ReconciliationEvidence);
public sealed record AiRunView(Guid Id, Guid OperationId, string State, string Outcome, AiExecutionSnapshot Snapshot,
    decimal Reserved, decimal Spent, GeneratedText? Output, IReadOnlyList<AiAttemptView> Attempts, IReadOnlyList<CitationExport>? SourceCitations = null);
public sealed record AiReconcile(AiUsage Usage, string EvidenceReference);
public sealed record AiConnectionSwitch(bool Enabled, bool Killed);
public sealed record AiProviderCall(Guid RunId, Guid AttemptId, AiProviderSettings Settings, string Prompt,
    int OutputLimit, string[] SourceIds, bool Streaming, string? SecretRef = null);
public sealed record AiProviderResult(string Outcome, string? Content, AiUsage? Usage, string? RequestId = null);
public sealed record AiStreamEvent(string Kind, string? Delta = null, AiProviderResult? Result = null, AiRunView? Run = null);
public sealed record AiEmbeddingCall(AiProviderSettings Settings, EmbeddingRequest Request);
public sealed record AiEmbeddingResult(OwnedEmbedding Embedding, AiUsage Usage);
public sealed record PinAiEmbedding(Guid ProfileId, string ModelReference);
