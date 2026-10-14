using ContentOS.Contracts;
namespace ContentOS.Application;

public interface ITrendSource { string Key { get; } TrendDiscovery Discover(TrendDiscoveryRequest request, CancellationToken ct); }
public interface ITrendExtractor { string Key { get; } TrendExtraction Extract(TrendExtractionRequest request, CancellationToken ct); }
public interface ITrendWorkbench
{
    Task<Guid> ConnectorAsync(CoreScope scope, Guid universe, RegisterTrendConnector request, CancellationToken ct);
    Task<TrendConnectorView[]> ConnectorsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task DisableAsync(CoreScope scope, Guid universe, Guid id, TrendChange request, CancellationToken ct);
    Task<Guid[]> PollAsync(CoreScope scope, Guid universe, PollTrends request, CancellationToken ct);
    Task<Guid> SubmitAsync(CoreScope scope, Guid universe, SubmitTrendSignal request, CancellationToken ct);
    Task<TrendClusterView[]> ClustersAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewClusterAsync(CoreScope scope, Guid universe, Guid id, ReviewTrendCluster request, CancellationToken ct);
    Task<TrendTeachingView> LinkAsync(CoreScope scope, Guid universe, LinkTrendTeaching request, CancellationToken ct);
    Task ReviewTeachingAsync(CoreScope scope, Guid universe, Guid id, TrendReview request, CancellationToken ct);
    Task<TrendTeachingView[]> TeachingsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<TrendStoryView> PrepareAsync(CoreScope scope, Guid universe, PrepareTrendStory request, CancellationToken ct);
    Task ReviewStoryAsync(CoreScope scope, Guid universe, Guid id, TrendReview request, CancellationToken ct);
    Task<TrendStoryView[]> StoriesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task BindAsync(CoreScope scope, Guid universe, BindTrendStory request, CancellationToken ct);
    Task ChangeAsync(CoreScope scope, Guid universe, Guid id, TrendChange request, CancellationToken ct);
    Task RecheckAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<TrendRemediationView[]> RemediationsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<TrendRankView[]> RankAsync(CoreScope scope, Guid universe, RankTrends request, CancellationToken ct);
    Task<TrendStats> StatsAsync(CoreScope scope, Guid universe, CancellationToken ct);
}
