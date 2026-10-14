using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class AnalyticsTestFixture
{
    internal static ServiceProvider Services() => SocialTestFixture.Services(s =>
    {
        s.AddAnalyticsWorkbench(); foreach (var key in new[] { "analytics.import.v1", "analytics.fixture.a.v1", "analytics.fixture.b.v1" }) s.AddSingleton<IChannelAnalyticsProvider>(new AnalyticsImportProvider(key));
    });
    internal static Task<Guid> Source(IServiceProvider s, EditorialTestFixture.Fixture f, int version = 1, string adapter = "analytics.fixture.a.v1") =>
        s.GetRequiredService<IAnalyticsWorkbench>().SourceAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "finance", version, adapter, "fixture-finance", "fixture:account", "fixture:finance-evidence"), default);
    internal static AnalyticsEntryInput Revenue(string key, decimal gross = 1000, string stream = "GeetaPlusSubscription", string channel = "Instagram") =>
        new(key, key, "Revenue", DateTimeOffset.UtcNow.AddMinutes(-1), "fixture:finance-export", AcquisitionChannel: channel, RevenueStream: stream, Gross: gross, Tax: 100, Refunds: 50, Fees: 30);
    internal static AnalyticsEntryInput Cost(string key, decimal amount = 100) => new(key, key, "Cost", DateTimeOffset.UtcNow.AddMinutes(-1), "fixture:cost-export", CostCategory: "AiTool", Amount: amount);
    internal static AnalyticsReportQuery Query => new("INR", DateTimeOffset.UtcNow.AddDays(-90), DateTimeOffset.UtcNow.AddDays(1));
    internal static AnalyticsForecastRequest Forecast(int version = 1) => new(Guid.NewGuid(), "scenario", version, "INR", "GeetaPlusSubscription", "Instagram",
        1000, .1m, 100, .5m, 3, false, 0, 100, 20, 30, 10, .5m, 1.5m, "fixture:assumptions", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
}
