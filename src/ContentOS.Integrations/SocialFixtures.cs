using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;
// Controlled local simulation only; never contacts a social platform or reads a token.
public sealed class SocialFixturePublisher(string key, ISocialEffectLedger ledger) : ISocialPublisher
{
    public string Key => key;
    public SocialPublisherCapabilities Capabilities => new(true, true, true, "OfflineFixture", "fixture.v1");
    public async Task<SocialOutcome> SubmitAsync(SocialDispatch r, CancellationToken ct)
    {
        if (key == "social.rate-limit.v1") return new("RateLimited", ConfirmedNotSubmitted: true, RetryAfterSeconds: 2);
        var committed = await ledger.CommitAsync(r, ct);
        return key == "social.unknown.v1" ? new("Unknown") : committed;
    }
    public Task<SocialOutcome> ReconcileAsync(SocialDispatch r, CancellationToken ct) => ledger.FindAsync(r, ct);
}
