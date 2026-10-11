using ContentOS.Contracts;
namespace ContentOS.Application;

public interface ITextGenerationProvider
{
    string Key { get; }
    void Validate(AiProviderSettings settings);
    Task<AiProviderResult> GenerateAsync(AiProviderCall call, CancellationToken ct);
    IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall call, CancellationToken ct);
}
public interface IAiEmbeddingAdapter
{
    string Key { get; }
    void Validate(AiProviderSettings settings);
    ValueTask<AiEmbeddingResult> EmbedAsync(AiEmbeddingCall call, CancellationToken ct);
}
public interface IAiAdministration
{
    Task<Guid> RegisterAsync(CoreScope scope, RegisterAiProfile request, CancellationToken ct);
    Task<IReadOnlyList<AiProfileView>> ProfilesAsync(CoreScope scope, CancellationToken ct);
    Task<Guid> RouteAsync(CoreScope scope, AiRouteSettings request, CancellationToken ct);
    Task ActivateAsync(CoreScope scope, Guid route, CancellationToken ct);
    Task<IReadOnlyList<AiRouteView>> RoutesAsync(CoreScope scope, CancellationToken ct);
    Task<Guid> BudgetAsync(CoreScope scope, ConfigureAiBudget request, CancellationToken ct);
    Task<IReadOnlyList<AiBudgetView>> BudgetsAsync(CoreScope scope, CancellationToken ct);
    Task SwitchAsync(CoreScope scope, Guid profile, AiConnectionSwitch request, CancellationToken ct);
    Task ReconcileAsync(CoreScope scope, Guid run, Guid attempt, AiReconcile request, CancellationToken ct);
    Task RecoverAsync(CoreScope scope, Guid run, CancellationToken ct);
}
public interface IAiTaskRouter
{
    Task<AiRunView> GenerateAsync(CoreScope scope, Guid universe, GenerateAi request, CancellationToken ct);
    IAsyncEnumerable<AiStreamEvent> StreamAsync(CoreScope scope, Guid universe, GenerateAi request, CancellationToken ct);
    Task<AiRunView> InspectAsync(CoreScope scope, Guid run, CancellationToken ct);
    Task<IReadOnlyList<AiRunView>> RunsAsync(CoreScope scope, CancellationToken ct);
}
public interface IRoutedEmbeddingProvider : IEmbeddingProvider
{
    Task<string> PinAsync(CoreScope scope, Guid universe, string model, CancellationToken ct);
}
public interface ILegacyEmbeddingProvider : IEmbeddingProvider;
