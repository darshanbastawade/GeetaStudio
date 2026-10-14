using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

// Host owns authentication. No client JSON can construct the principal passed by the API.
public sealed class HostIdentityProvider : IIdentityProvider
{
    public string Key => "identity.windows.v1";
    public Task<CustomerVerifiedIdentity> VerifyAsync(ClaimsPrincipal host, string account, Guid challenge, string? assertion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); CustomerRules.Require(host.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(host.Identity.Name) && account == "local-windows" && assertion == null, "Verified host authentication required.");
        return Task.FromResult(new CustomerVerifiedIdentity(Key, account, "Local", host.Identity!.Name!.ToUpperInvariant(), false));
    }
}
public sealed record FixtureIdentityClaim(Guid Challenge, string Actor, string Subject, string Provider, string Account, long Expires);
public sealed class FixtureIdentityProvider(string key, string signingSecret) : IIdentityProvider
{
    public string Key => key;
    private readonly string fixtureKey = signingSecret;
    public static string Sign(FixtureIdentityClaim claim, string secret) { var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(CustomerRules.Json(claim))); return payload + "." + Mac(payload, secret); }
    private static string Mac(string payload, string secret) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
    public Task<CustomerVerifiedIdentity> VerifyAsync(ClaimsPrincipal host, string account, Guid challenge, string? assertion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); CustomerRules.Require(host.Identity?.IsAuthenticated == true && assertion?.Length <= 3000 && challenge != Guid.Empty, "Verified challenge proof required.");
        var parts = (assertion ?? "").Split('.'); CustomerRules.Require(parts.Length == 2 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(parts[1]), Encoding.UTF8.GetBytes(Mac(parts[0], fixtureKey))), "Invalid identity proof signature.");
        FixtureIdentityClaim claim;
        try { claim = CustomerRules.Read<FixtureIdentityClaim>(Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]))); } catch (Exception e) when (e is FormatException or System.Text.Json.JsonException) { throw new RuntimeValidationException("Malformed identity proof."); }
        CustomerRules.Require(claim.Challenge == challenge && claim.Provider == Key && claim.Account == account && claim.Actor.Equals(host.Identity!.Name, StringComparison.OrdinalIgnoreCase) && claim.Expires >= DateTimeOffset.UtcNow.ToUnixTimeSeconds() && claim.Expires <= DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(), "Identity proof scope/expiry mismatch."); CustomerRules.Key(claim.Subject); CustomerRules.Key(account);
        return Task.FromResult(new CustomerVerifiedIdentity(Key, account, "Test", claim.Subject, true));
    }
}
public sealed class FixturePaymentProvider(string key, ICustomerPaymentEvidenceStore store, string signingSecret) : IPaymentProvider
{
    public string Key => key;
    private readonly string fixtureKey = signingSecret;
    private static string Mac(string payload, long sent, string secret) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(sent + "." + payload)));
    public static CustomerSignedWebhook Sign(CustomerPaymentEvidence e, string secret) { var payload = CustomerRules.Json(e); var sent = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); return new(payload, sent, Mac(payload, sent, secret)); }
    public CustomerPaymentEvidence VerifyWebhook(CustomerSignedWebhook r)
    {
        CustomerRules.Require(r.Payload is { Length: <= 16000 } && r.Signature is { Length: 64 } && Math.Abs((decimal)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - r.SentAtUnix) <= 300 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(r.Signature), Encoding.UTF8.GetBytes(Mac(r.Payload, r.SentAtUnix, fixtureKey))), "Invalid/stale payment signature.");
        var e = CustomerRules.Read<CustomerPaymentEvidence>(r.Payload); CustomerRules.Require(e.Provider == Key && e.Environment == "Test", "Payment adapter/environment mismatch."); return e;
    }
    public Task<CustomerPaymentOutcome> SubmitAsync(CustomerPaymentDispatch r, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); CustomerRules.Require(r.Provider == Key && r.Environment == "Test", "Fixture payment only."); return Task.FromResult(new CustomerPaymentOutcome(Key == "payment.unknown.v1" ? "Unknown" : "Pending", "fixture-order:" + r.OrderId.ToString("N"))); }
    public async Task<CustomerPaymentOutcome> ReconcileAsync(CustomerPaymentDispatch r, CancellationToken ct)
    { CustomerRules.Require(r.Provider == Key && r.Environment == "Test", "Original fixture binding required."); var e = await store.LatestAsync(r, ct); return new(e?.State ?? "Pending", "fixture-order:" + r.OrderId.ToString("N"), e); }
    public Task<CustomerPaymentOutcome> ChangeAsync(CustomerPaymentDispatch r, CustomerPaymentAction a, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); CustomerRules.Require(r.Provider == Key && r.Environment == "Test" && a.Action is "Refund" or "Cancel", "Original fixture payment binding required."); return Task.FromResult(new CustomerPaymentOutcome("Pending", r.ExternalReference)); }
}
public sealed class InboxNotificationProvider : INotificationProvider
{
    public string Key => "notification.inbox.v1";
    public Task<string> DeliverAsync(Guid notificationId, Guid internalUserId, string kind, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); CustomerRules.Require(internalUserId != Guid.Empty, "Internal notification recipient required."); return Task.FromResult("inbox:" + notificationId.ToString("N")); }
}
