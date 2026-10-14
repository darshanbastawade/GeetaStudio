using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IVideoRenderer
{
    string Key { get; }
    WatchRendererCapabilities Capabilities { get; }
    Task<WatchRenderResult> SubmitAsync(WatchRenderRequest request, CancellationToken ct);
    Task<WatchRenderResult> ReconcileAsync(WatchRenderRequest request, string? resource, CancellationToken ct);
    Task<WatchFileInfo> InspectAsync(byte[] master, CancellationToken ct);
}
public interface ISubtitleProvider
{
    string Key { get; }
    WatchCue[] Compile(WatchScene[] scenes); string Vtt(WatchCue[] cues); string Srt(WatchCue[] cues);
}
public interface IWatchMasterStore
{
    Task<WatchStoredMaster> PutAsync(Guid id, byte[] bytes, string owner, CancellationToken ct);
}
public interface IWatchFactory
{
    Task<Guid> TemplateAsync(CoreScope s, Guid universe, RegisterWatchTemplate request, CancellationToken ct);
    Task<WatchTemplateView[]> TemplatesAsync(CoreScope s, Guid universe, CancellationToken ct);
    Task ReviewTemplateAsync(CoreScope s, Guid universe, Guid id, WatchReview request, CancellationToken ct);
    Task<Guid> ProfileAsync(CoreScope s, Guid universe, RegisterWatchProfile request, CancellationToken ct);
    Task<WatchProfileView[]> ProfilesAsync(CoreScope s, Guid universe, CancellationToken ct);
    Task DisableProfileAsync(CoreScope s, Guid universe, Guid id, WatchControl request, CancellationToken ct);
    Task<WatchProductionView> CreateAsync(CoreScope s, Guid universe, CreateWatch request, CancellationToken ct);
    Task<WatchProductionView[]> ListAsync(CoreScope s, Guid universe, CancellationToken ct);
    Task<WatchProductionView> InspectAsync(CoreScope s, Guid universe, Guid id, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
    Task ControlAsync(CoreScope s, Guid universe, Guid id, WatchControl request, CancellationToken ct);
    Task ReviewAsync(CoreScope s, Guid universe, Guid id, WatchReview request, CancellationToken ct);
    Task<(byte[] Bytes, string Mime)> PreviewAsync(CoreScope s, Guid universe, Guid artifact, CancellationToken ct);
    Task<WatchExport> ExportAsync(CoreScope s, Guid universe, Guid id, string purpose, CancellationToken ct);
}
