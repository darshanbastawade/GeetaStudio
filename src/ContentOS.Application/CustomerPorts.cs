using System.Security.Claims;
using ContentOS.Contracts;
namespace ContentOS.Application;

public sealed record CustomerScope(Guid TenantId, ClaimsPrincipal Principal);
public sealed record CustomerVerifiedIdentity(string Provider, string Account, string Environment, string Subject, bool Fixture);
public interface IIdentityProvider
{
    string Key { get; }
    Task<CustomerVerifiedIdentity> VerifyAsync(ClaimsPrincipal host, string account, Guid challenge, string? assertion, CancellationToken ct);
}
public sealed record CustomerPaymentDispatch(Guid OrderId, string Provider, string Account, string Environment, string CustomerReference, string? ExternalReference, CustomerProductView Product);
public sealed record CustomerPaymentOutcome(string State, string? ExternalReference = null, CustomerPaymentEvidence? Evidence = null);
public interface IPaymentProvider
{
    string Key { get; }
    Task<CustomerPaymentOutcome> SubmitAsync(CustomerPaymentDispatch request, CancellationToken ct);
    Task<CustomerPaymentOutcome> ReconcileAsync(CustomerPaymentDispatch request, CancellationToken ct);
    Task<CustomerPaymentOutcome> ChangeAsync(CustomerPaymentDispatch request, CustomerPaymentAction action, CancellationToken ct);
    CustomerPaymentEvidence VerifyWebhook(CustomerSignedWebhook request);
}
public interface ICustomerPaymentEvidenceStore { Task<CustomerPaymentEvidence?> LatestAsync(CustomerPaymentDispatch request, CancellationToken ct); }
public interface INotificationProvider { string Key { get; } Task<string> DeliverAsync(Guid notificationId, Guid internalUserId, string kind, CancellationToken ct); }
public interface ICustomerWorkbench
{
    Task<Guid> RegisterAsync(CustomerScope s, CustomerRegistration r, CancellationToken ct);
    Task<CustomerUserView> MeAsync(CustomerScope s, CancellationToken ct);
    Task<CustomerChallengeView> ChallengeAsync(CustomerScope s, CustomerChallengeRequest r, CancellationToken ct);
    Task<Guid> LinkAsync(CustomerScope s, CustomerProof r, CancellationToken ct);
    Task<Guid> RecoverAsync(CustomerScope s, CustomerProof r, CancellationToken ct);
    Task<Guid> HouseholdAsync(CustomerScope s, CustomerHouseholdRequest r, CancellationToken ct);
    Task<CustomerHouseholdView[]> HouseholdsAsync(CustomerScope s, CancellationToken ct);
    Task<Guid> InviteAsync(CustomerScope s, Guid household, CustomerInviteRequest r, CancellationToken ct);
    Task<Guid> TransferAsync(CustomerScope s, Guid household, CustomerTransfer r, CancellationToken ct);
    Task<Guid> AcceptAsync(CustomerScope s, Guid invite, CustomerOperation r, CancellationToken ct);
    Task<Guid> ProfileAsync(CustomerScope s, CustomerProfileRequest r, CancellationToken ct);
    Task<CustomerProfileView[]> ProfilesAsync(CustomerScope s, CancellationToken ct);
    Task<Guid> EditProfileAsync(CustomerScope s, Guid profile, CustomerProfileEdit r, CancellationToken ct);
    Task<Guid> ConsentAsync(CustomerScope s, CustomerConsentRequest r, CancellationToken ct);
    Task<Guid> ProgressAsync(CustomerScope s, Guid profile, CustomerProgressRequest r, CancellationToken ct);
    Task<CustomerProgressView[]> ProgressAsync(CustomerScope s, Guid profile, CancellationToken ct);
    Task<Guid> DeleteProfileAsync(CustomerScope s, Guid profile, CustomerOperation r, CancellationToken ct);
    Task<Guid> DeleteAsync(CustomerScope s, CustomerOperation r, CancellationToken ct);
    Task<CustomerExport> ExportAsync(CustomerScope s, CancellationToken ct);
    Task<CustomerProductView[]> ProductsAsync(CustomerScope s, CancellationToken ct);
    Task<Guid> ProductAsync(CoreScope s, CustomerProductRequest r, CancellationToken ct);
    Task<Guid> RouteAsync(CoreScope s, CustomerRouteRequest r, CancellationToken ct);
    Task<Guid> ControlRouteAsync(CoreScope s, Guid id, CustomerRouteControl r, CancellationToken ct);
    Task<CustomerRouteView[]> RoutesAsync(CoreScope s, CancellationToken ct);
    Task<CustomerProviderInfo[]> ProvidersAsync(CoreScope s, CancellationToken ct);
    Task<Guid> CheckoutAsync(CustomerScope s, CustomerCheckout r, CancellationToken ct);
    Task<CustomerOrderView[]> OrdersAsync(CustomerScope s, CancellationToken ct);
    Task<CustomerOrderView> SubmitAsync(CustomerScope s, Guid order, CancellationToken ct);
    Task<CustomerOrderView> ReconcileAsync(CustomerScope s, Guid order, CancellationToken ct);
    Task<CustomerOrderView> ChangePaymentAsync(CustomerScope s, Guid order, CustomerPaymentAction r, CancellationToken ct);
    Task<Guid> WebhookAsync(Guid tenant, CustomerSignedWebhook r, CancellationToken ct);
    Task<CustomerEntitlements> EntitlementsAsync(CustomerScope s, CancellationToken ct);
    Task<Guid> GrantAsync(CoreScope s, CustomerGrant r, CancellationToken ct);
    Task<CustomerWallet> WalletAsync(CustomerScope s, CancellationToken ct);
    Task<Guid> ReserveAsync(CustomerScope s, CustomerReserve r, CancellationToken ct);
    Task<Guid> CancelReservationAsync(CustomerScope s, Guid id, CustomerOperation r, CancellationToken ct);
    Task<Guid> DispatchAsync(CoreScope s, Guid id, CustomerDispatch r, CancellationToken ct);
    Task<Guid> SettleAsync(CoreScope s, Guid id, CustomerSettlement r, CancellationToken ct);
    Task<CustomerReservationView[]> ReservationsAsync(CustomerScope s, CancellationToken ct);
    Task<CustomerNotificationView[]> NotificationsAsync(CustomerScope s, CancellationToken ct);
    Task<Guid> DeliverNoticeAsync(CustomerScope s, Guid id, CustomerOperation r, CancellationToken ct);
}
