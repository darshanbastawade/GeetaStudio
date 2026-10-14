using ContentOS.Application;
using ContentOS.Domain;
using Xunit;
namespace ContentOS.Foundation.Tests;

public sealed class AnalyticsRuleTests
{
    [Fact]
    public void Subscription_forecast_keeps_retention_and_uncertainty_and_payout_requires_eligibility()
    {
        var r = AnalyticsTestFixture.Forecast(); AnalyticsRules.Forecast(r); Assert.Equal(17500, AnalyticsRules.ForecastGross(r));
        var payout = r with { RevenueStream = "PlatformPayout", PlatformPayoutEligible = false, PayoutPerThousand = 200 };
        AnalyticsRules.Forecast(payout); Assert.Equal(0, AnalyticsRules.ForecastGross(payout)); Assert.Equal(600, AnalyticsRules.ForecastGross(payout with { PlatformPayoutEligible = true }));
        Assert.Throws<RuntimeValidationException>(() => AnalyticsRules.Forecast(r with { Conversion = 2 }));
    }
    [Fact]
    public void Financial_net_keeps_taxes_refunds_fees_independent_and_preserves_negative_net()
    {
        AnalyticsRules.Entry(AnalyticsTestFixture.Cost("documented-zero", 0));
        var r = AnalyticsTestFixture.Revenue("sale"); AnalyticsRules.Entry(r); Assert.Equal(820, AnalyticsRules.Net(r));
        var refund = r with { Tax = 0, Refunds = 1000 }; AnalyticsRules.Entry(refund); Assert.Equal(-30, AnalyticsRules.Net(refund));
        Assert.Throws<RuntimeValidationException>(() => AnalyticsRules.Entry(r with { Gross = -1 }));
        Assert.Throws<RuntimeValidationException>(() => AnalyticsRules.Entry(r with { RevenueStream = "Instagram" }));
    }
    [Fact]
    public void Missing_metrics_are_null_and_unconsented_identity_is_rejected()
    {
        var now = DateTimeOffset.UtcNow.AddSeconds(-1);
        var r = AnalyticsTestFixture.Cost("metric") with { Kind = "Metric", CostCategory = "", Amount = 0, DefinitionId = Guid.NewGuid(), WindowStartUtc = now.AddDays(-1), WindowEndUtc = now, OccurredUtc = now, ObservedUtc = now, Coverage = "Unavailable" };
        AnalyticsRules.Entry(r); Assert.Throws<RuntimeValidationException>(() => AnalyticsRules.Entry(r with { MetricValue = 0 }));
        Assert.Throws<RuntimeValidationException>(() => AnalyticsRules.Entry(r with { Kind = "Activity", Activity = "Converted", DefinitionId = null, WindowStartUtc = null, WindowEndUtc = null, ObservedUtc = null, Coverage = "Available" }));
        Assert.Throws<RuntimeValidationException>(() => AnalyticsRules.Reference("person@example.com"));
    }
}
