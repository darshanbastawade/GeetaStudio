using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

public sealed class CommerceDeliveryAdapter(string key, ICommerceDeliveryLedger ledger) : ICommerceDelivery
{
    public string Key => key; public string Qualification => "OfflineFixture";
    public async Task<CommerceReceipt> SubmitAsync(CommerceDispatch r, CancellationToken ct) { CreationRules.Require(r.Adapter == Key && r.Mode == "PrivateTest", "Explicit private fixture delivery required."); var receipt = await ledger.CommitAsync(r, ct); return key == "delivery.unknown.v1" ? new("Unknown") : receipt; }
    public Task<CommerceReceipt> ReconcileAsync(CommerceDispatch r, CancellationToken ct) => ledger.FindAsync(r, ct);
}
