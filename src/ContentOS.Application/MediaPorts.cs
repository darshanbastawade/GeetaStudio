using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IMediaFileInspector { MediaInfo Inspect(byte[] bytes); }
public interface IImageGenerationProvider
{
    string Key { get; }
    MediaProviderCapabilities Capabilities { get; }
    Task<MediaProviderResult> SubmitAsync(MediaProviderRequest request, CancellationToken ct);
    Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest request, string? resource, CancellationToken ct);
}
public interface ISpeechSynthesisProvider
{
    string Key { get; }
    MediaProviderCapabilities Capabilities { get; }
    Task<MediaProviderResult> SubmitAsync(MediaProviderRequest request, CancellationToken ct);
    Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest request, string? resource, CancellationToken ct);
}
public interface IMediaStorageMover
{
    Task<MediaStoreMoveResult> MoveAsync(Guid asset, string actor, string storeKey, CancellationToken ct);
    Task<string> LocationAsync(Guid asset, string actor, CancellationToken ct);
}
public interface IMediaLibrary
{
    Task<MediaVersionView> ImportAsync(CoreScope scope, Guid universe, ImportMedia request, CancellationToken ct);
    Task<MediaAssetView[]> AssetsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewAsync(CoreScope scope, Guid universe, Guid version, ReviewMedia request, CancellationToken ct);
    Task<MediaExport> ExportAsync(CoreScope scope, Guid universe, Guid version, string purpose, CancellationToken ct);
    Task<MediaStoreMoveResult> MoveAsync(CoreScope scope, Guid universe, MoveMedia request, CancellationToken ct);
    Task<Guid> ProfileAsync(CoreScope scope, Guid universe, RegisterMediaProfile request, CancellationToken ct);
    Task<MediaProfileView[]> ProfilesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task DisableAsync(CoreScope scope, Guid universe, Guid profile, MediaProfileControl request, CancellationToken ct);
    Task<MediaGenerationView> GenerateAsync(CoreScope scope, Guid universe, GenerateMedia request, CancellationToken ct);
    Task<MediaGenerationView[]> GenerationsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<bool> ProcessNextAsync(string worker, CancellationToken ct);
    Task ReconcileAsync(CoreScope scope, Guid universe, ReconcileMedia request, CancellationToken ct);
    Task<MediaVersionView> AcceptAsync(CoreScope scope, Guid universe, AcceptMediaResult request, CancellationToken ct);
    Task<Guid> PackageAsync(CoreScope scope, Guid universe, RegisterCharacterMedia request, CancellationToken ct);
    Task<CharacterMediaView[]> PackagesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewPackageAsync(CoreScope scope, Guid universe, Guid package, ReviewMedia request, CancellationToken ct);
    Task<Guid> BindPackageAsync(CoreScope scope, Guid universe, Guid package, Guid operation, CancellationToken ct);
}
