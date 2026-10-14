using ContentOS.Contracts;
namespace ContentOS.Application;

public interface ISocialPublishing
{
    Task<SocialCapability[]> CapabilitiesAsync(CoreScope s, Guid u, CancellationToken ct);
    Task<Guid> RegisterAccountAsync(CoreScope s, Guid u, RegisterSocialAccount request, CancellationToken ct);
    Task<SocialAccountView[]> AccountsAsync(CoreScope s, Guid u, CancellationToken ct);
    Task ControlAccountAsync(CoreScope s, Guid u, Guid id, SocialAccountControl request, CancellationToken ct);
    Task<SocialPackageView> PrepareAsync(CoreScope s, Guid u, CreateSocialPackage request, CancellationToken ct);
    Task<SocialPackageView[]> PackagesAsync(CoreScope s, Guid u, CancellationToken ct);
    Task ReviewAsync(CoreScope s, Guid u, Guid id, SocialReview request, CancellationToken ct);
    Task<SocialExport> ExportAsync(CoreScope s, Guid u, Guid id, CancellationToken ct);
    Task<SocialPublicationView> ScheduleAsync(CoreScope s, Guid u, ScheduleSocial request, CancellationToken ct);
    Task<SocialPublicationView[]> PublicationsAsync(CoreScope s, Guid u, CancellationToken ct);
    Task ControlAsync(CoreScope s, Guid u, Guid id, SocialControl request, CancellationToken ct);
    Task ReceiptAsync(CoreScope s, Guid u, Guid id, SocialReceipt request, CancellationToken ct);
    Task<SocialNotificationView[]> NotificationsAsync(CoreScope s, Guid u, CancellationToken ct);
    Task AcknowledgeAsync(CoreScope s, Guid u, Guid id, SocialControl request, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
}
public sealed record SocialDispatch(Guid PublicationId, Guid TenantId, Guid UniverseId, string Platform, string AccountReference, string Adapter, string PayloadHash, SocialPayload Payload);
public sealed record SocialOutcome(string State, string? RemoteId = null, string? RemoteUrl = null, bool ConfirmedNotSubmitted = false, int RetryAfterSeconds = 0);

public interface ISocialPublisher
{
    string Key { get; }
    SocialPublisherCapabilities Capabilities { get; }
    Task<SocialOutcome> SubmitAsync(SocialDispatch request, CancellationToken ct);
    Task<SocialOutcome> ReconcileAsync(SocialDispatch request, CancellationToken ct);
}
public interface ISocialEffectLedger
{
    Task<SocialOutcome> CommitAsync(SocialDispatch request, CancellationToken ct);
    Task<SocialOutcome> FindAsync(SocialDispatch request, CancellationToken ct);
}

public interface ISocialCapabilityCatalog { SocialCapability[] All { get; } SocialCapability Resolve(string key); }
