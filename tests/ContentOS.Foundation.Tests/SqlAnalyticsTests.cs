using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlAnalyticsTests
{
    [SqlFact]
    public async Task Financial_import_duplicates_delayed_fx_settlements_and_provider_change_reconcile_exactly()
    {
        using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await EditorialTestFixture.Setup(s, "m19-finance"); var w = s.GetRequiredService<IAnalyticsWorkbench>(); var source = await AnalyticsTestFixture.Source(s, f);
        var fx = await w.FxAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "usd-inr", 1, "USD", "INR", 80, DateTimeOffset.UtcNow.AddDays(-2), "fixture:fixed-fx"), default);
        var rows = new[] { AnalyticsTestFixture.Revenue("sub"), AnalyticsTestFixture.Revenue("sponsor", 10, "Sponsorship", "Unknown") with { Currency = "USD", Tax = 0, Refunds = 0, Fees = 1, FxId = fx }, AnalyticsTestFixture.Cost("production"), AnalyticsTestFixture.Cost("overhead", 20) with { CostCategory = "OperatingOverhead" }, AnalyticsTestFixture.Revenue("estimate", 200, "PlatformPayout", "YouTube") with { FinancialState = "Estimated", Tax = 0, Refunds = 0, Fees = 0 } };
        var request = new AnalyticsImport(Guid.NewGuid(), source, rows); var first = await w.ImportAsync(f.Owner, f.Universe, request, default); Assert.Equal(5, first.Inserted);
        Assert.Equal(first.EntryIds, (await w.ImportAsync(f.Owner, f.Universe, request, default)).EntryIds);
        Assert.Equal(5, (await w.ImportAsync(f.Owner, f.Universe, request with { OperationId = Guid.NewGuid() }, default)).Duplicates);
        var oldAsOf = DateTimeOffset.UtcNow;
        var settled = new AnalyticsEntryInput("settled", "settled", "Settlement", DateTimeOffset.UtcNow.AddSeconds(-1), "fixture:bank", AcquisitionChannel: "Instagram", Amount: 820, RelatedEntryId: first.EntryIds[0], SettlementReference: "bank:one");
        await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [settled]), default);
        var report = await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query, default);
        Assert.Equal(1540, report.RecognizedNet); Assert.Equal(200, report.EstimatedNet); Assert.Equal(120, report.Costs); Assert.Equal(820, report.Settled); Assert.Equal(720, report.UnsettledDifference);
        Assert.Equal(820, report.Groups.Single(x => x.Dimension == "RevenueStream" && x.Key == "GeetaPlusSubscription").RecognizedNet);
        Assert.Equal(820, report.Groups.Single(x => x.Dimension == "AcquisitionChannel" && x.Key == "Instagram").RecognizedNet);
        Assert.Equal(720, report.Groups.Single(x => x.Dimension == "AcquisitionChannel" && x.Key == "Unknown").RecognizedNet);
        var replacement = await AnalyticsTestFixture.Source(s, f, 2, "analytics.fixture.b.v1");
        Assert.Equal(5, (await w.ImportAsync(f.Owner, f.Universe, request with { OperationId = Guid.NewGuid(), SourceId = replacement }, default)).Duplicates);
        await w.FxAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "usd-inr", 2, "USD", "INR", 90, DateTimeOffset.UtcNow.AddDays(-2), "fixture:new-rate"), default);
        Assert.Equal(1540, (await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query, default)).RecognizedNet);
        Assert.Equal(0, (await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query with { AsOfUtc = oldAsOf }, default)).Settled);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [rows[0] with { Gross = 2000 }]), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [settled with { EventId = "other", EconomicKey = "other", Amount = 1, SettlementReference = "bank:two" }]), default));
        using var fresh = root.CreateScope(); Assert.Equal(1540, (await fresh.ServiceProvider.GetRequiredService<IAnalyticsWorkbench>().ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query, default)).RecognizedNet);
        var refund = new AnalyticsEntryInput("late-refund", "late-refund", "Refund", DateTimeOffset.UtcNow.AddSeconds(-1), "fixture:refund", AcquisitionChannel: "Instagram", RevenueStream: "GeetaPlusSubscription", Amount: 20, RelatedEntryId: first.EntryIds[0]);
        await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), replacement, [refund]), default);
        Assert.Equal(1, (await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), replacement, [refund]), default)).Duplicates);
        var refunded = await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query, default);
        Assert.Equal(1520, refunded.RecognizedNet); Assert.Equal(820, refunded.Settled); Assert.Equal(700, refunded.UnsettledDifference);
        var ledgerAsOf = DateTimeOffset.UtcNow; var page1 = await w.EntriesAsync(f.Owner, f.Universe, default, 0, 2, ledgerAsOf); var page2 = await w.EntriesAsync(f.Owner, f.Universe, default, 2, 2, ledgerAsOf); Assert.Equal(2, page1.Length); Assert.Equal(2, page2.Length); Assert.Empty(page1.Select(x => x.Id).Intersect(page2.Select(x => x.Id)));
    }
    [SqlFact]
    public async Task Metric_snapshots_definitions_missingness_and_received_asof_keep_history()
    {
        using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s, "m19-metrics"); var w = s.GetRequiredService<IAnalyticsWorkbench>(); var source = await AnalyticsTestFixture.Source(s, f);
        var d = await w.DefinitionAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "views", 1, "count", "Snapshot", "Account reported views for the exact window; not unique viewers", "fixture:definition"), default);
        var now = DateTimeOffset.UtcNow.AddMinutes(-2);
        var input = new AnalyticsEntryInput("metric1", "metric1", "Metric", now, "fixture:metric", DefinitionId: d, MetricValue: 100, WindowStartUtc: now.AddDays(-1), WindowEndUtc: now, ObservedUtc: now);
        await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [input]), default); var oldAsOf = DateTimeOffset.UtcNow;
        await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [input with { EventId = "metric2", EconomicKey = "metric2", MetricValue = 120, ObservedUtc = now.AddMinutes(1) }]), default);
        var d2 = await w.DefinitionAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "views", 2, "count", "Delta", "New counting definition, disjoint windows", "fixture:changed-definition"), default);
        await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [input with { EventId = "missing", EconomicKey = "missing", DefinitionId = d2, MetricValue = null, Coverage = "Unavailable" }]), default);
        var report = await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query, default);
        Assert.Equal(120, report.Metrics.Single(x => x.Version == 1).Value); Assert.Null(report.Metrics.Single(x => x.Version == 2).Value);
        Assert.Equal(100, (await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query with { AsOfUtc = oldAsOf }, default)).Metrics.Single().Value);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [input with { EventId = "overlap", EconomicKey = "overlap", WindowStartUtc = now.AddHours(-12), ObservedUtc = now.AddMinutes(1) }]), default));
    }
    [SqlFact]
    public async Task Missing_fx_estimates_and_forecast_versions_are_visible_not_blended()
    {
        using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s, "m19-scenario"); var w = s.GetRequiredService<IAnalyticsWorkbench>(); var source = await AnalyticsTestFixture.Source(s, f);
        await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [AnalyticsTestFixture.Revenue("usd") with { Currency = "USD" }]), default);
        var report = await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query, default); Assert.Equal(1, report.ExcludedMissingFx); Assert.Equal(0, report.RecognizedNet); Assert.Contains(report.Gaps, x => x.Contains("incomplete"));
        var first = AnalyticsTestFixture.Forecast(); await w.ForecastAsync(f.Owner, f.Universe, first, default); await w.ForecastAsync(f.Owner, f.Universe, first with { OperationId = Guid.NewGuid(), Version = 2, Conversion = .2m }, default);
        var forecasts = await w.ForecastsAsync(f.Owner, f.Universe, default); Assert.Equal(17500, forecasts.Single(x => x.Assumptions.Version == 1).ProjectedGross); Assert.Equal(35000, forecasts.Single(x => x.Assumptions.Version == 2).ProjectedGross);
        Assert.All(forecasts, x => Assert.Contains("never guaranteed", x.Warning));
    }
    [SqlFact]
    public async Task Cross_scope_untrusted_roles_disable_and_immutable_evidence_are_enforced()
    {
        using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s, "m19-access"); var other = await EditorialTestFixture.Setup(s, "m19-other"); var w = s.GetRequiredService<IAnalyticsWorkbench>(); var source = await AnalyticsTestFixture.Source(s, f);
        var accessDb = s.GetRequiredService<CoreDbContext>(); accessDb.Members.Add(new() { TenantId = f.Owner.TenantId, Actor = "analytics-reader", Permission = "Reader" }); await accessDb.SaveChangesAsync();
        var reader = new CoreScope(f.Owner.TenantId, "analytics-reader"); Assert.Equal(0, (await w.ReportAsync(reader, f.Universe, AnalyticsTestFixture.Query, default)).RecognizedNet);
        var request = new AnalyticsImport(Guid.NewGuid(), source, [AnalyticsTestFixture.Cost("cost")]);
        await Assert.ThrowsAsync<CoreAccessException>(() => w.ImportAsync(other.Owner, other.Universe, request, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => w.ReportAsync(new(f.Owner.TenantId, "stranger"), f.Universe, AnalyticsTestFixture.Query, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => w.ImportAsync(reader, f.Universe, request, default));
        var first = await w.ImportAsync(f.Owner, f.Universe, request, default);
        var db = s.GetRequiredService<CoreDbContext>(); var row = await db.AnalyticsEntries.SingleAsync(x => x.Id == first.EntryIds[0]); row.SnapshotJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var v = (await w.SourcesAsync(f.Owner, f.Universe, default)).Single(); await w.ControlAsync(f.Owner, f.Universe, "source", source, new(Guid.NewGuid(), v.RowVersion, "Disable", "fixture:kill"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ImportAsync(f.Owner, f.Universe, request with { OperationId = Guid.NewGuid() }, default));
        Assert.Equal(first.EntryIds, (await w.ImportAsync(f.Owner, f.Universe, request, default)).EntryIds);
    }
    [SqlFact]
    public async Task Concurrent_reimports_commit_one_financial_event_and_changed_operation_conflicts()
    {
        using var root = AnalyticsTestFixture.Services(); using var setup = root.CreateScope(); var f = await EditorialTestFixture.Setup(setup.ServiceProvider, "m19-concurrent"); var source = await AnalyticsTestFixture.Source(setup.ServiceProvider, f); var input = AnalyticsTestFixture.Cost("same");
        using var a = root.CreateScope(); using var b = root.CreateScope();
        var result = await Task.WhenAll(a.ServiceProvider.GetRequiredService<IAnalyticsWorkbench>().ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [input]), default), b.ServiceProvider.GetRequiredService<IAnalyticsWorkbench>().ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [input]), default));
        Assert.Equal(1, result.Sum(x => x.Inserted)); Assert.Equal(1, result.Sum(x => x.Duplicates));
        var w = setup.ServiceProvider.GetRequiredService<IAnalyticsWorkbench>(); var op = new AnalyticsImport(Guid.NewGuid(), source, [input]); await w.ImportAsync(f.Owner, f.Universe, op, default);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => w.ImportAsync(f.Owner, f.Universe, op with { Entries = [input with { Amount = 101 }] }, default));
    }
}
