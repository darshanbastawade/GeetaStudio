using System.Security.Principal;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Foundation.Tests;
using Microsoft.Extensions.DependencyInjection;
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows-authenticated analytics demonstration required.");
var repo = Environment.GetEnvironmentVariable("GEETA_REPOSITORY") ?? throw new InvalidOperationException("Repository required.");
using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
var f = await EditorialTestFixture.Setup(s, WindowsIdentity.GetCurrent().Name); var w = s.GetRequiredService<IAnalyticsWorkbench>();
var editor = s.GetRequiredService<IEditorialWorkbench>(); var content = await EditorialTestFixture.Approved(s, f, await editor.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default));
var a = await AnalyticsTestFixture.Source(s, f);
var fx = await w.FxAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "usd-inr", 1, "USD", "INR", 80, DateTimeOffset.UtcNow.AddDays(-2), "fixture:fixed-fx-not-market-quote"), default);
var rows = new[] {
    AnalyticsTestFixture.Revenue("subscription") with { RevisionId = content.RevisionId },
    AnalyticsTestFixture.Revenue("sponsorship", 10, "Sponsorship", "Unknown") with { RevisionId = content.RevisionId, Currency = "USD", Tax = 0, Refunds = 0, Fees = 1, FxId = fx },
    AnalyticsTestFixture.Cost("production") with { RevisionId = content.RevisionId },
    AnalyticsTestFixture.Cost("review", 20) with { RevisionId = content.RevisionId, CostCategory = "Review" },
    AnalyticsTestFixture.Revenue("estimate", 200, "PlatformPayout", "YouTube") with { RevisionId = content.RevisionId, FinancialState = "Estimated", Tax = 0, Refunds = 0, Fees = 0 }
};
var first = await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), a, rows), default);
await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), a, [new("settlement", "settlement", "Settlement", DateTimeOffset.UtcNow.AddSeconds(-1), "fixture:bank-reconciliation", RevisionId: content.RevisionId, AcquisitionChannel: "Instagram", Amount: 820, RelatedEntryId: first.EntryIds[0], SettlementReference: "fixture:bank-one")]), default);
var b = await AnalyticsTestFixture.Source(s, f, 2, "analytics.fixture.b.v1");
var definition = await w.DefinitionAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "engagement-views", 1, "count", "Snapshot", "Fixture reported count for the exact coverage window; not unique viewers", "fixture:definition"), default);
var end = DateTimeOffset.UtcNow.AddMinutes(-2);
var metric = new AnalyticsEntryInput("views-one", "views-one", "Metric", end, "fixture:views", RevisionId: content.RevisionId, DefinitionId: definition, MetricValue: 100, WindowStartUtc: end.AddDays(-1), WindowEndUtc: end, ObservedUtc: end);
await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), a, [metric]), default);
await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), b, [metric with { EventId = "views-two", EconomicKey = "views-two", MetricValue = 120, ObservedUtc = end.AddMinutes(1) }]), default);
await w.ForecastAsync(f.Owner, f.Universe, AnalyticsTestFixture.Forecast(), default);
await w.ForecastAsync(f.Owner, f.Universe, AnalyticsTestFixture.Forecast(2) with { Conversion = .2m }, default);
var query = AnalyticsTestFixture.Query; var report = await w.ReportAsync(f.Owner, f.Universe, query, default);
if (report.RecognizedNet != 1540 || report.Costs != 120 || report.Settled != 820 || report.UnsettledDifference != 720) throw new InvalidOperationException("Known fixture total did not reconcile.");
var dir = Path.Combine(repo, ".local", "m19-demo"); Directory.CreateDirectory(dir); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var import = new AnalyticsImport(Guid.NewGuid(), b, rows);
File.WriteAllText(Path.Combine(dir, "reimport.json"), JsonSerializer.Serialize(import, json));
File.WriteAllText(Path.Combine(dir, "report.json"), JsonSerializer.Serialize(report, json));
File.WriteAllText(Path.Combine(dir, "fixture.json"), JsonSerializer.Serialize(new { tenant = f.Owner.TenantId, universe = f.Universe, revision = content.RevisionId, originalSource = a, replacementSource = b, originalEntry = first.EntryIds[0], startsUtc = query.StartsUtc, endsUtc = query.EndsUtc, expectedRecognized = 1540, expectedCosts = 120, expectedSettled = 820, expectedEstimated = 200 }, json));
Console.WriteLine("Private M19 finance fixture: INR1540 recognized, INR120 actual cost, INR820 settled, INR200 estimated. These are synthetic records, not earnings.");
namespace ContentOS.Foundation.Tests { internal static class ArchitectureTests { public static string RepositoryRoot => Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!; } }
