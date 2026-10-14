using System.Security.Cryptography;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContentOS.Foundation.Tests;

public sealed class HardeningRuleTests
{
    [Fact]
    public void Both_owned_models_match_the_existing_migration_snapshots()
    {
        using var core = new CoreDesignFactory().CreateDbContext([]);
        using var runtime = new RuntimeDesignFactory().CreateDbContext([]);
        Assert.False(core.Database.HasPendingModelChanges());
        Assert.False(runtime.Database.HasPendingModelChanges());
    }
    [Fact]
    public void Rotated_webhook_key_rejects_retired_signatures_and_accepts_new_original_binding()
    {
        var retired = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var current = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var e = new CustomerPaymentEvidence("rotation-probe", Guid.NewGuid(), "payment.fixture.a.v1", "original-account", "Test", "customer", "original-resource", null, 1, "Paid", 100, "INR", 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var provider = new FixturePaymentProvider(e.Provider, new EmptyEvidence(), current);
        Assert.Throws<RuntimeValidationException>(() => provider.VerifyWebhook(FixturePaymentProvider.Sign(e, retired)));
        Assert.Equal(e, provider.VerifyWebhook(FixturePaymentProvider.Sign(e, current)));
        Assert.Throws<RuntimeValidationException>(() => provider.VerifyWebhook(FixturePaymentProvider.Sign(e with { Environment = "Production" }, current)));
    }
    private sealed class EmptyEvidence : ICustomerPaymentEvidenceStore
    {
        public Task<CustomerPaymentEvidence?> LatestAsync(CustomerPaymentDispatch r, CancellationToken ct) => Task.FromResult<CustomerPaymentEvidence?>(null);
    }
}
