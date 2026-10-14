using ContentOS.Contracts;
namespace ContentOS.Application;

public interface ICommerceDelivery
{
    string Key { get; }
    string Qualification { get; }
    Task<CommerceReceipt> SubmitAsync(CommerceDispatch request, CancellationToken ct);
    Task<CommerceReceipt> ReconcileAsync(CommerceDispatch request, CancellationToken ct);
}
public interface ICommerceDeliveryLedger
{
    Task<CommerceReceipt> CommitAsync(CommerceDispatch request, CancellationToken ct);
    Task<CommerceReceipt> FindAsync(CommerceDispatch request, CancellationToken ct);
}
public interface ICommerceWorkbench
{
    Task<Guid> ProductAsync(CustomerScope scope, CommerceProductRequest request, CancellationToken ct);
    Task<CommerceProductView[]> ProductsAsync(CustomerScope scope, CancellationToken ct);
    Task<Guid> RouteAsync(CoreScope scope, CommerceRouteRequest request, CancellationToken ct);
    Task<Guid> PurchaseAsync(CustomerScope scope, CommercePurchase request, CancellationToken ct);
    Task<CommerceOrderView[]> OrdersAsync(CustomerScope scope, CancellationToken ct);
    Task<CommerceOrderView> ReconcileAsync(CustomerScope scope, Guid order, CancellationToken ct);
    Task RefundAsync(CustomerScope scope, Guid order, CustomerPaymentAction request, CancellationToken ct);
    Task<CommerceDownload> DownloadAsync(CustomerScope scope, Guid order, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
    Task<Guid> InstitutionAsync(CustomerScope scope, CommerceInstitutionRequest request, CancellationToken ct);
    Task<CommerceInstitutionView[]> InstitutionsAsync(CustomerScope scope, CancellationToken ct);
    Task SeatAsync(CustomerScope scope, Guid institution, CommerceSeatRequest request, CancellationToken ct);
    Task ActivityAsync(CustomerScope scope, Guid institution, CommerceActivityRequest request, CancellationToken ct);
    Task<CommerceCohortReport[]> CohortsAsync(CustomerScope scope, Guid institution, CancellationToken ct);
    Task<CommerceDownload> InstitutionDownloadAsync(CustomerScope scope, Guid institution, Guid product, CancellationToken ct);
    Task<Guid> OfferAsync(CoreScope scope, CommerceOfferRequest request, CancellationToken ct);
    Task<CommerceOfferView[]> OffersAsync(CustomerScope scope, CancellationToken ct);
}
