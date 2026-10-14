using System.Security.Claims;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Integrations;
using Xunit;
namespace ContentOS.Foundation.Tests;

public sealed class CustomerRuleTests
{
    [Fact]
    public async Task Verified_identity_proof_pins_challenge_actor_account_and_expiry()
    {
        var host = CustomerTestFixture.Scope(Guid.NewGuid(), "fixture-owner").Principal; var challenge = Guid.NewGuid(); var p = new FixtureIdentityProvider("identity.fixture.a.v1", CustomerTestFixture.Secret);
        var claim = new FixtureIdentityClaim(challenge, "fixture-owner", "subject-one", p.Key, "fixture-account", DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds()); var token = FixtureIdentityProvider.Sign(claim, CustomerTestFixture.Secret);
        Assert.Equal("subject-one", (await p.VerifyAsync(host, "fixture-account", challenge, token, default)).Subject);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => p.VerifyAsync(host, "other-account", challenge, token, default)); await Assert.ThrowsAsync<RuntimeValidationException>(() => p.VerifyAsync(host, "fixture-account", Guid.NewGuid(), token, default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => p.VerifyAsync(new ClaimsPrincipal(new ClaimsIdentity()), "fixture-account", challenge, token, default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => p.VerifyAsync(host, "fixture-account", challenge, FixtureIdentityProvider.Sign(claim with { Expires = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds() }, CustomerTestFixture.Secret), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => p.VerifyAsync(host, "fixture-account", challenge, FixtureIdentityProvider.Sign(claim, "wrong-signing-key-with-enough-length"), default));
    }
    [Fact]
    public void Refund_units_are_cumulative_bounded_and_never_new_credits()
    { Assert.Equal(0, CustomerRules.RefundCredits(100, 0, 1000)); Assert.Equal(25, CustomerRules.RefundCredits(100, 250, 1000)); Assert.Equal(100, CustomerRules.RefundCredits(100, 1000, 1000)); Assert.Throws<RuntimeValidationException>(() => CustomerRules.RefundCredits(100, 1001, 1000)); Assert.Throws<RuntimeValidationException>(() => CustomerRules.Credits(-1)); Assert.Throws<RuntimeValidationException>(() => CustomerRules.Read<CustomerCheckout>("not json")); }
}
