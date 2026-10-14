namespace ContentOS.Contracts;

public sealed record CommerceLicense(string Mode, int Days, int Seats, string UpdatePolicy, string ContractReference, string Disclosure, bool PrivateSharingConsent);
public sealed record CommerceProductRequest(Guid OperationId, string Key, int Version, Guid CreationId, string Kind, string Currency, long PriceMinor, CommerceLicense License);
public sealed record CommerceProductView(Guid Id, string Key, int Version, string Kind, string Currency, long PriceMinor, int ContentVersion, string ContentSha256, CommerceLicense License, bool Enabled, bool FixtureOnly);
public sealed record CommerceRouteRequest(Guid OperationId, string Key, int Version, string Adapter, string Account, bool Active = true);
public sealed record CommercePurchase(Guid OperationId, Guid ProductId, string Acquisition = "Unknown", Guid? InstitutionId = null);
public sealed record CommerceOrderView(Guid Id, Guid ProductId, Guid? PaymentOrderId, string State, string PaymentState, string DeliveryState, string DeliveryAdapter, string Account, string? Receipt, int ContentVersion, string ContentSha256, CommerceLicense License, DateTimeOffset? EndsUtc);
public sealed record CommerceDispatch(Guid DeliveryId, Guid OrderId, Guid TenantId, string Adapter, string Account, string ContentSha256, string Mode)
{
    // Transient admission proof; never changes the persisted original resource binding.
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? LeaseToken { get; init; }
}
public sealed record CommerceReceipt(string State, string? Resource = null, decimal Cost = 0, string Currency = "INR");
public sealed record CommerceInstitutionRequest(Guid OperationId, string Name, string ContractReference);
public sealed record CommerceSeatRequest(Guid OperationId, Guid UserId, string Cohort, string Role = "Learner", bool Enabled = true);
public sealed record CommerceInstitutionView(Guid Id, string Name, int SeatCapacity, int ActiveSeats, string ContractReference);
public sealed record CommerceActivityRequest(Guid OperationId, Guid ProfileId, bool Completed);
public sealed record CommerceCohortReport(string Cohort, int ActiveSeats, int? ConsentedSubjects, decimal? CompletionRate, string Coverage);
public sealed record CommerceOfferRequest(Guid OperationId, string Key, string Kind, string[] Prerequisites, string ContractReference, string Disclosure);
public sealed record CommerceOfferView(Guid Id, string Key, string Kind, string[] Prerequisites, string ContractReference, string Disclosure, string State);
public sealed record CommerceDownload(Guid ProductId, int Version, string Html, string Sha256, string License, CitationExport[] Citations);
