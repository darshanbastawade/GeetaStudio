using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IEditorialWorkbench
{
    Task<EditorialPacket> StartAsync(CoreScope scope, Guid universe, StartEditorial request, CancellationToken ct);
    Task<IReadOnlyList<EditorialSummary>> ListAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<EditorialPacket> InspectAsync(CoreScope scope, Guid universe, Guid id, CancellationToken ct);
    Task<EditorialPacket> EditAsync(CoreScope scope, Guid universe, Guid id, EditEditorial request, CancellationToken ct);
    Task<EditorialPacket> ActAsync(CoreScope scope, Guid universe, Guid id, EditorialAction request, CancellationToken ct);
    Task<EditorialPacket> AttachAssetAsync(CoreScope scope, Guid universe, Guid id, AttachEditorialAsset request, CancellationToken ct);
    Task<(byte[] Bytes, string MediaType)> PreviewAssetAsync(CoreScope scope, Guid universe, Guid asset, CancellationToken ct);
    Task<Guid> CorrectAsync(CoreScope scope, Guid universe, EditorialCorrection request, CancellationToken ct);
    Task<IReadOnlyList<EditorialCorrectionTaskView>> CorrectionsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ResolveCorrectionAsync(CoreScope scope, Guid universe, Guid task, ResolveEditorialCorrection request, CancellationToken ct);
    Task<EditorialProviderStatus> ProvidersAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> RouteAsync(CoreScope scope, Guid universe, EditorialRouteCommand request, CancellationToken ct);
    Task<EditorialTrendView> SubmitTrendAsync(CoreScope scope, Guid universe, EditorialTrendInput request, CancellationToken ct);
    Task<IReadOnlyList<EditorialTrendView>> TrendsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewTrendAsync(CoreScope scope, Guid universe, Guid id, ReviewEditorialTrend request, CancellationToken ct);
}
