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
public sealed class SqlAnalyticsContentTests
{
    [SqlFact]
    public async Task Bound_content_publication_cast_variant_cost_and_correction_metadata_remain_scoped()
    {
        using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await WatchTestFixture.Setup(s, actor: "m19-content");
        var w = s.GetRequiredService<IAnalyticsWorkbench>(); var source = await AnalyticsTestFixture.Source(s, f.Context);
        var account = await SocialTestFixture.Account(s, f.Context); var package = await SocialTestFixture.Package(s, f.Context, f.Script, account); var publication = (await s.GetRequiredService<ISocialPublishing>().PublicationsAsync(f.Context.Owner, f.Context.Universe, default)).Single(x => x.PackageId == package.Id);
        var cost = AnalyticsTestFixture.Cost("article") with { RevisionId = f.Script.RevisionId, PublicationId = publication.Id, Campaign = "fixture-campaign", DiscoverySource = "Manual", Amount = 40 };
        await w.ImportAsync(f.Context.Owner, f.Context.Universe, new(Guid.NewGuid(), source, [cost, cost with { EventId = "review", EconomicKey = "review", CostCategory = "Review", Amount = 10 }]), default);
        var report = await w.ReportAsync(f.Context.Owner, f.Context.Universe, AnalyticsTestFixture.Query, default); var v = Assert.Single(report.Variants);
        Assert.Equal(f.Context.Audience, v.Content.AudienceId); Assert.Equal("en-IN", v.Content.Locale); Assert.Equal("Fiction", v.Content.ContentMode);
        Assert.Contains(f.Character.CoreRevisionId, v.Content.CharacterRevisionIds); Assert.Equal(50, v.CostPerApprovedOutput); Assert.False(v.Corrected);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ImportAsync(f.Context.Owner, f.Context.Universe, new(Guid.NewGuid(), source, [cost with { EventId = "wrong", EconomicKey = "wrong", Campaign = "elsewhere" }]), default));
        Assert.Contains(f.Character.Profile.Id, v.Content.CharacterProfileRevisionIds);
        var definition = await w.DefinitionAsync(f.Context.Owner, f.Context.Universe, new(Guid.NewGuid(), "views", 1, "count", "Delta", "Controlled publication views", "fixture:definition"), default);
        var end = DateTimeOffset.UtcNow.AddSeconds(-1);
        var metric = new AnalyticsEntryInput("view", "view", "Metric", end, "fixture:views", RevisionId: f.Script.RevisionId, PublicationId: publication.Id, Campaign: "fixture-campaign", DefinitionId: definition, MetricValue: 1, WindowStartUtc: end.AddHours(-1), WindowEndUtc: end, ObservedUtc: end);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ImportAsync(f.Context.Owner, f.Context.Universe, new(Guid.NewGuid(), source, [metric]), default));
        var channelSource = await w.SourceAsync(f.Context.Owner, f.Context.Universe, new(Guid.NewGuid(), "channel", 1, "analytics.fixture.a.v1", "X", "fixture:x", "fixture:account-metrics"), default);
        await w.ImportAsync(f.Context.Owner, f.Context.Universe, new(Guid.NewGuid(), channelSource, [metric]), default);
        var db = s.GetRequiredService<CoreDbContext>(); var correction = new EditorialCorrectionRecord { Id = Guid.NewGuid(), TenantId = f.Context.Owner.TenantId, UniverseId = f.Context.Universe, Reason = "fixture:correction", EvidenceReference = "fixture:correction" }; db.EditorialCorrections.Add(correction); db.EditorialCorrectionTasks.Add(new() { Id = Guid.NewGuid(), TenantId = f.Context.Owner.TenantId, UniverseId = f.Context.Universe, CorrectionId = correction.Id, ContentId = f.Script.ContentId, RevisionId = f.Script.RevisionId, Kind = "SourceCorrection", Reason = "fixture:correction" }); await db.SaveChangesAsync();
        Assert.True((await w.ReportAsync(f.Context.Owner, f.Context.Universe, AnalyticsTestFixture.Query, default)).Variants.Single().Corrected);
    }
    [SqlFact]
    public async Task Consent_revocation_suppresses_cohorts_learning_and_session_costs_even_for_historical_reports()
    {
        using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s, "m19-cohort"); var w = s.GetRequiredService<IAnalyticsWorkbench>(); var source = await AnalyticsTestFixture.Source(s, f);
        var editor = s.GetRequiredService<IEditorialWorkbench>(); var packet = await editor.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default);
        var db = s.GetRequiredService<CoreDbContext>(); var consentIds = new List<Guid>(); var entries = new List<AnalyticsEntryInput>(); var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            var consent = new AnalyticsConsent { Id = Guid.NewGuid(), TenantId = f.Owner.TenantId, UniverseId = f.Universe, SubjectReference = "fixture:subject-" + i, Purpose = "AnalyticsCohorts", ExpiresUtc = now.AddDays(30), Fixture = true, EvidenceReference = "fixture:consent-only", CreatedUtc = now.AddDays(-45) };
            db.AnalyticsConsents.Add(consent); consentIds.Add(consent.Id);
            var a = new AnalyticsEntryInput("acquired-" + i, "acquired-" + i, "Activity", now.AddDays(-40), "fixture:activity", RevisionId: packet.RevisionId, AcquisitionChannel: "Instagram", ConsentId: consent.Id, Activity: "Acquired", SessionReference: "session:" + i);
            entries.Add(a); entries.Add(a with { EventId = "converted-" + i, EconomicKey = "converted-" + i, Activity = "Converted", OccurredUtc = now.AddDays(-35) });
            entries.Add(a with { EventId = "returned-" + i, EconomicKey = "returned-" + i, Activity = "Returned", OccurredUtc = now.AddDays(-2) });
            entries.Add(a with { EventId = "learned-" + i, EconomicKey = "learned-" + i, Activity = "Learning", LearningScore = .8m, OccurredUtc = now.AddDays(-1) });
            entries.Add(AnalyticsTestFixture.Cost("session-cost-" + i, 10) with { RevisionId = packet.RevisionId, ConsentId = consent.Id, SessionReference = "session:" + i });
        }
        await db.SaveChangesAsync(); await w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, entries.ToArray()), default);
        var report = await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query, default);
        Assert.Equal(1, report.Cohorts.Single().Conversion); Assert.Equal(1, report.Cohorts.Single().Retention); Assert.Equal(.8m, report.Variants.Single().LearningFeedbackMean); Assert.Equal(10, report.SessionCosts.Single().CostPerSession);
        var asOf = DateTimeOffset.UtcNow; var consentView = (await w.ConsentsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == consentIds[0]);
        await w.ControlAsync(f.Owner, f.Universe, "consent", consentView.Id, new(Guid.NewGuid(), consentView.RowVersion, "Disable", "fixture:revoked"), default);
        report = await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query with { AsOfUtc = asOf }, default);
        Assert.Null(report.Cohorts.Single().Conversion); Assert.Null(report.SessionCosts.Single().CostPerSession); Assert.Null(report.Variants.Single().LearningFeedbackMean); Assert.Equal(4, report.SuppressedOrRevokedActivities);
        Assert.Equal(50, report.Costs); // Historical finances persist; viewer-linked reporting is suppressed.
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [entries[0] with { EventId = "late", EconomicKey = "late" }]), default));
    }
    [SqlFact]
    public async Task Owned_ai_cost_capture_is_idempotent_and_keeps_original_currency_and_missing_fx()
    {
        using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s, "m19-cost"); var w = s.GetRequiredService<IAnalyticsWorkbench>(); var admin = s.GetRequiredService<IAiAdministration>();
        await admin.BudgetAsync(f.Owner, new("ai.text", "USD", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), 1), default);
        var profile = await admin.RegisterAsync(f.Owner, new(SqlAiTests.Settings()), default);
        var route = await admin.RouteAsync(f.Owner, new("draft", "m19-cost.v1", [profile], .1m, "USD", Streaming: false), default); await admin.ActivateAsync(f.Owner, route, default);
        var run = await s.GetRequiredService<IAiTaskRouter>().GenerateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "draft", "An original fictional cost fixture."), default); Assert.True(run.Spent > 0);
        var first = await w.CaptureAsync(f.Owner, f.Universe, new(Guid.NewGuid()), default); var again = await w.CaptureAsync(f.Owner, f.Universe, new(Guid.NewGuid()), default);
        Assert.Equal(1, first.Inserted); Assert.Equal(0, again.Inserted); Assert.Equal(1, again.Duplicates);
        var report = await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query with { Currency = "USD" }, default); Assert.Equal(run.Spent, report.Costs);
        Assert.Equal(1, (await w.ReportAsync(f.Owner, f.Universe, AnalyticsTestFixture.Query, default)).ExcludedMissingFx);
        var source = await AnalyticsTestFixture.Source(s, f);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.SourceAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "OWNED-PRODUCTION", 2, "analytics.fixture.a.v1", "finance", "fixture:elsewhere", "fixture:reserved"), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ImportAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source, [AnalyticsTestFixture.Cost("OWNED:ai:" + run.Id)]), default));
    }

    [SqlFact]
    public async Task Completed_zero_price_watch_work_is_known_zero_cost_not_missing_or_double_counted()
    {
        using var root = AnalyticsTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await WatchTestFixture.Setup(s, actor: "m19-zero-cost"); var watch = s.GetRequiredService<IWatchFactory>();
        var production = await watch.CreateAsync(f.Context.Owner, f.Context.Universe, WatchTestFixture.Request(f), default);
        production = await WatchTestFixture.Drain(s, f, production.Id); Assert.Equal("Review", production.State);
        var w = s.GetRequiredService<IAnalyticsWorkbench>(); var captured = await w.CaptureAsync(f.Context.Owner, f.Context.Universe, new(Guid.NewGuid()), default);
        Assert.True(captured.Inserted > 0); Assert.Equal(0, (await w.CaptureAsync(f.Context.Owner, f.Context.Universe, new(Guid.NewGuid()), default)).Inserted);
        var report = await w.ReportAsync(f.Context.Owner, f.Context.Universe, AnalyticsTestFixture.Query, default);
        Assert.Equal(0, report.Costs); Assert.Null(report.Variants.Single().CostPerApprovedOutput);
        Assert.Equal("WatchEpisode", report.Variants.Single().Content.Format); Assert.Contains(f.Script.RevisionId, report.Variants.Single().Content.SourceRevisionIds!); Assert.Contains(f.Character.CoreRevisionId, report.Variants.Single().Content.CharacterRevisionIds);
        Assert.All(report.EpisodeCosts, e => Assert.Null(e.CostPerApprovedEpisode));
        await watch.ReviewAsync(f.Context.Owner, f.Context.Universe, production.Id, new(Guid.NewGuid(), production.RowVersion, "Approve", "Fixture", "fixture:technical-only-episode-review", [], 1), default);
        report = await w.ReportAsync(f.Context.Owner, f.Context.Universe, AnalyticsTestFixture.Query, default);
        Assert.Equal(0, report.EpisodeCosts.Single().CostPerApprovedEpisode); Assert.True(report.EpisodeCosts.Single().CurrentlyApproved);
        Assert.All(await w.EntriesAsync(f.Context.Owner, f.Context.Universe, default), e => Assert.Equal(0, e.Event.Amount));
    }
}
