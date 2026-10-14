using System.Net;
using System.Net.Http.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlTrendTests
{
    internal static async Task<TrendStoryView> Generate(IServiceProvider s, TrendTestFixture.Fixture f)
    {
        var trends = s.GetRequiredService<ITrendWorkbench>(); var factory = s.GetRequiredService<IDraftFactory>();
        var match = await TrendTestFixture.Match(s, f); Assert.Equal("Matched", match.Outcome);
        var brief = await TrendTestFixture.Package(s, f, match); Assert.Equal(2, brief.Snapshot!.Packages.Length);
        Assert.NotEqual(brief.Snapshot.Packages[0].AudienceId, brief.Snapshot.Packages[1].AudienceId);
        Assert.NotEqual(brief.Snapshot.Packages[0].Cast.Cast[0].Character.CharacterId, brief.Snapshot.Packages[1].Cast.Cast[0].Character.CharacterId);
        var plan = (await factory.PlansAsync(f.Owner, f.Universe, default)).Single(x => x.Id == brief.FactoryPlanId);
        await factory.ReviewPlanAsync(f.Owner, f.Universe, plan.Id, new(Guid.NewGuid(), plan.RowVersion, true, "fixture:review-shared-plan"), default);
        var start = new StartFactoryBatch(Guid.NewGuid(), "m15-brief-before-review", plan.Id, new(0, "INR", 24000, 300, 2));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => factory.StartAsync(f.Owner, f.Universe, start, default));
        await trends.ReviewStoryAsync(f.Owner, f.Universe, brief.Id, new(Guid.NewGuid(), brief.RowVersion, "Approve", "Fixture", "fixture:review-original-scripts-not-human-release"), default);
        await factory.StartAsync(f.Owner, f.Universe, start, default); await FactoryTestFixture.Drain(s);
        var artifacts = (await factory.ArtifactsAsync(f.Owner, f.Universe, default)).Where(x => x.Snapshot?.PlanId == plan.Id).ToArray(); Assert.Equal(2, artifacts.Length);
        var db = s.GetRequiredService<CoreDbContext>(); foreach (var artifact in artifacts)
        {
            Assert.Contains(TrendTestFixture.Fact, artifact.Snapshot!.Output.Body); Assert.Contains("Original fictional analogy", artifact.Snapshot.Output.Body);
            Assert.Equal(2, await db.Provenance.CountAsync(x => x.ContentRevisionId == artifact.CoreRevisionId && f.NewsSources.Contains(x.SnapshotId!.Value)));
            Assert.True(await db.ClaimLinks.AnyAsync(x => x.ContentRevisionId == artifact.CoreRevisionId && x.ClaimRevisionId == f.NewsClaim));
        }
        using var restart = TrendTestFixture.Services(); using var scope = restart.CreateScope(); Assert.Equal(brief.Id, (await scope.ServiceProvider.GetRequiredService<ITrendWorkbench>().StoriesAsync(f.Owner, f.Universe, default)).Single().Id);
        return (await trends.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == brief.Id);
    }
    [SqlFact]
    public async Task Two_original_audience_packages_use_new_guests_shared_factory_and_both_evidence_chains()
    {
        using var root = TrendTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await TrendTestFixture.Setup(s); var brief = await Generate(s, f);
        var ranked = await s.GetRequiredService<ITrendWorkbench>().RankAsync(f.Owner, f.Universe, new(f.ChildAudience, "en-IN", new()), default); Assert.True(ranked.Single(x => x.ClusterId == f.Cluster).Eligible); Assert.True(ranked.Single().MomentumMissing);
        Assert.Equal(0, (await s.GetRequiredService<ITrendWorkbench>().StatsAsync(f.Owner, f.Universe, default)).ActualTextGenerationCost); Assert.Null((await s.GetRequiredService<ITrendWorkbench>().StatsAsync(f.Owner, f.Universe, default)).ActualMediaCost);
    }
    [SqlFact]
    public async Task Forced_stale_copied_contradicted_and_missing_transcript_cases_cannot_be_promoted_by_popularity()
    {
        using var root = TrendTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await TrendTestFixture.Setup(s); var w = s.GetRequiredService<ITrendWorkbench>();
        var noMatch = await TrendTestFixture.Match(s, f, "How do I bake a pizza?"); Assert.Equal("NoSuitableGeetaConnection", noMatch.Outcome);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => w.ReviewTeachingAsync(f.Owner, f.Universe, noMatch.Id, new(Guid.NewGuid(), noMatch.RowVersion, "Approve", "Fixture", "fixture:forced-mapping-denied"), default));
        var huge = new[] { new TrendMetric("views", 100000000, DateTimeOffset.UtcNow) };
        var stale = TrendTestFixture.Signal(f.NewsSources, "stale") with { PublishedUtc = DateTimeOffset.UtcNow.AddDays(-8), EventUtc = DateTimeOffset.UtcNow.AddDays(-8), Metrics = huge };
        var video = TrendTestFixture.Signal(f.NewsSources, "video") with { Kind = "Manual", RequiresTranscript = true, TranscriptSnapshotId = null };
        await w.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector, stale with { Kind = "Manual" }), default);
        await w.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector, video), default);
        var copies = new RegisterTrendConnector(Guid.NewGuid(), "copied", 1, "trend.fixture.a.v1", "trend.owned-text.a.v1", "Fixture", "fixture:copied-reports",
            [TrendTestFixture.Signal([f.NewsSources[0]], "copies", "same-original", false), TrendTestFixture.Signal([f.NewsSources[1]], "copies", "same-original", false)]);
        var copied = await w.ConnectorAsync(f.Owner, f.Universe, copies, default); await w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), copied), default);
        var rows = await w.ClustersAsync(f.Owner, f.Universe, default);
        Assert.Contains("StaleStory", rows.Single(x => x.Candidates.Any(c => c.Signal.EventKey == "stale")).Blocks);
        Assert.Contains("TranscriptUnavailable", rows.Single(x => x.Candidates.Any(c => c.Signal.EventKey == "video")).Blocks);
        var copy = rows.Single(x => x.Candidates.Any(c => c.Signal.EventKey == "copies")); Assert.Equal(1, copy.IndependentReports); Assert.Contains("IndependentCorroborationRequired", copy.Blocks);
        foreach (var row in rows.Where(x => x.Id != f.Cluster)) await Assert.ThrowsAsync<EvidenceBlockedException>(() => w.ReviewClusterAsync(f.Owner, f.Universe, row.Id,
            new(Guid.NewGuid(), row.RowVersion, "Approve", f.NewsClaim, "No virality override", DateTimeOffset.UtcNow.AddHours(1), "Fixture", "fixture:negative-case"), default));
        await s.GetRequiredService<IEvidenceEngine>().CorrectAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.NewsClaim, "Contradicted controlled report", "fixture:contradiction"), default);
        await w.RecheckAsync(f.Owner, f.Universe, default); Assert.NotEqual("Verified", (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Cluster).State);
        Assert.All(await w.RankAsync(f.Owner, f.Universe, new(f.ChildAudience, "en-IN", new(.01, .95, .01, .01, .01, .01)), default), x => Assert.False(x.Eligible));
    }
    [SqlFact]
    public async Task Correction_and_expiry_block_scheduled_content_and_create_visible_editorial_remediation()
    {
        using var root = TrendTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await TrendTestFixture.Setup(s); var brief = await Generate(s, f);
        var factory = s.GetRequiredService<IDraftFactory>(); var art = (await factory.ArtifactsAsync(f.Owner, f.Universe, default)).First(x => x.Snapshot?.PlanId == brief.FactoryPlanId);
        var editorial = s.GetRequiredService<IEditorialWorkbench>(); var packet = await TrendTestFixture.Schedule(s, f, art); Assert.Equal("Scheduled", packet.State);
        var db = s.GetRequiredService<CoreDbContext>(); var cluster = await db.TrendClusters.SingleAsync(x => x.Id == f.Cluster); cluster.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        var blocked = await Assert.ThrowsAsync<EvidenceBlockedException>(() => s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, art.CoreContentId, default)); Assert.Contains("TrendRecheckRequired", blocked.Message);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => editorial.ActAsync(f.Owner, f.Universe, packet.Id, EditorialTestFixture.Action(packet, "PublishDryRun"), default));
        await s.GetRequiredService<ITrendWorkbench>().RecheckAsync(f.Owner, f.Universe, default);
        Assert.NotEmpty(await s.GetRequiredService<ITrendWorkbench>().RemediationsAsync(f.Owner, f.Universe, default));
        Assert.NotEmpty(await editorial.CorrectionsAsync(f.Owner, f.Universe, default)); packet = await editorial.InspectAsync(f.Owner, f.Universe, packet.Id, default); Assert.Equal("Corrected", packet.State);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => factory.StartAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "expired-second-batch", brief.FactoryPlanId!.Value, new(0, "INR", 24000, 300, 2)), default));
    }
    [SqlFact]
    public async Task Provider_switch_replay_scope_and_actual_http_roles_preserve_contract_and_different_coverage()
    {
        using var root = TrendTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await TrendTestFixture.Setup(s, "integration-test-Administrator"); var w = s.GetRequiredService<ITrendWorkbench>();
        var c = new RegisterTrendConnector(Guid.NewGuid(), "switch", 1, "trend.fixture.b.v1", "trend.owned-text.b.v1", "Fixture", "fixture:alternate-source-extractor",
            [TrendTestFixture.Signal(f.NewsSources, "switch-news"), TrendTestFixture.Signal([], "switch-video") with { Kind = "VideoMetadata", RequiresTranscript = true }]);
        var id = await w.ConnectorAsync(f.Owner, f.Universe, c, default); var poll = new PollTrends(Guid.NewGuid(), id); var first = await w.PollAsync(f.Owner, f.Universe, poll, default); Assert.Single(first); Assert.Equal(first, await w.PollAsync(f.Owner, f.Universe, poll, default));
        var candidate = (await w.ClustersAsync(f.Owner, f.Universe, default)).SelectMany(x => x.Candidates).Single(x => x.Id == first[0]); Assert.Contains("VideoChartsUnsupported", candidate.Coverage); Assert.Equal("UnsupportedByAdapter", candidate.Extraction!.TranscriptAccess);
        await Assert.ThrowsAsync<CoreAccessException>(() => w.ClustersAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, default));
        var db = s.GetRequiredService<CoreDbContext>(); if (!await db.Members.AnyAsync(x => x.TenantId == f.Owner.TenantId && x.Actor == "integration-test-Reader")) { db.Members.Add(new() { TenantId = f.Owner.TenantId, Actor = "integration-test-Reader", Permission = "Reader" }); await db.SaveChangesAsync(); }
        using var app = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Runtime:Enabled"] = "true",
                ["ConnectionStrings:ContentOS"] = LessonTestFixture.Connection,
                ["Staff:Administrator:0"] = f.Owner.Actor,
                ["Staff:Reader:0"] = "integration-test-Reader",
                ["Workflows:WorkerEnabled"] = "false",
                ["Knowledge:IndexWorkerEnabled"] = "false",
                ["Factory:WorkerEnabled"] = "false",
                ["Read:RetentionWorkerEnabled"] = "false",
                ["Trends:RecheckWorkerEnabled"] = "false",
                ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets")
            }));
            b.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = app.CreateClient(); var path = $"/api/v1/trends/tenants/{f.Owner.TenantId}/universes/{f.Universe}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/staff/trends")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Reader"); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "/clusters")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/poll", new PollTrends(Guid.NewGuid(), id))).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/poll", new PollTrends(Guid.NewGuid(), id))).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1"); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/poll", new PollTrends(Guid.NewGuid(), id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/staff/trends")).StatusCode);
        var observed = await db.TrendCandidates.SingleAsync(x => x.Id == first[0]); observed.SnapshotJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
