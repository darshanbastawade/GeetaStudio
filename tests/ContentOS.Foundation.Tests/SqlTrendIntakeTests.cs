using System.Net;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlTrendIntakeTests
{
    private sealed class Fresh : ITrendSource
    {
        public string Key => "trend.fixture.a.v1";
        public TrendDiscovery Discover(TrendDiscoveryRequest r, CancellationToken ct) => new("trend.contract.v1", Key, ["ControlledFixtureNews", "NoLiveNetwork"], r.Configuration.FixtureSignals.Select(x => x with { ObservedUtc = DateTimeOffset.UtcNow }).ToArray());
    }
    [SqlFact]
    public async Task Fresh_poll_ids_and_retrieval_times_reuse_candidates_and_do_not_expire_approved_briefs()
    {
        using var root = TrendTestFixture.Services(configure: services => { services.RemoveAll<ITrendSource>(); services.AddSingleton<ITrendSource, Fresh>(); });
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await TrendTestFixture.Setup(s, "rss-repeat-" + Guid.NewGuid()); var w = s.GetRequiredService<ITrendWorkbench>();
        // Reproduce pre-fix duplicate history without rewriting/deleting any accepted record.
        var db = s.GetRequiredService<CoreDbContext>(); var original = await db.TrendCandidates.AsNoTracking().FirstAsync(x => x.ClusterId == f.Cluster);
        var legacy = new ContentOS.Domain.TrendCandidate { Id = Guid.NewGuid(), TenantId = original.TenantId, UniverseId = original.UniverseId, ConnectorId = original.ConnectorId, ClusterId = original.ClusterId, SnapshotJson = original.SnapshotJson, ExtractionJson = original.ExtractionJson, CoverageJson = original.CoverageJson, RetrievedUtc = original.RetrievedUtc.AddMinutes(1) }; db.TrendCandidates.Add(legacy); await db.SaveChangesAsync();
        var review = (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Cluster);
        await w.ReviewClusterAsync(f.Owner, f.Universe, f.Cluster, new(Guid.NewGuid(), review.RowVersion, "Approve", f.NewsClaim, "Fixture recheck of preserved legacy history", DateTimeOffset.UtcNow.AddHours(1), "Fixture", "fixture:legacy-history-review"), default);
        var brief = await SqlTrendTests.Generate(s, f); var before = (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Cluster);
        var first = await w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector), default);
        using var restart = root.CreateScope(); var again = await restart.ServiceProvider.GetRequiredService<ITrendWorkbench>().PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector), default);
        Assert.Equal(first, again); Assert.Contains(original.Id, first); Assert.DoesNotContain(legacy.Id, first); var after = (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Cluster);
        Assert.Equal(before.Candidates.Select(x => x.Id).Order(), after.Candidates.Select(x => x.Id).Order()); Assert.Equal(before.Fingerprint, after.Fingerprint); Assert.Equal("Verified", after.State);
        Assert.Equal(before.Candidates.Select(x => x.RetrievedUtc), after.Candidates.Select(x => x.RetrievedUtc)); Assert.Equal("Approved", (await w.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == brief.Id).State); Assert.Empty(await w.RemediationsAsync(f.Owner, f.Universe, default));
    }
    [SqlFact]
    public async Task Changed_metric_versions_are_retained_and_actual_retraction_propagates_correction_once()
    {
        using var root = TrendTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await TrendTestFixture.Setup(s, "rss-change-" + Guid.NewGuid()); var w = s.GetRequiredService<ITrendWorkbench>();
        var brief = await SqlTrendTests.Generate(s, f); var signal = TrendTestFixture.Signal(f.NewsSources) with { Kind = "Manual", Metrics = [new("views", 100, DateTimeOffset.UtcNow.AddMinutes(-1))] };
        var first = await w.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector, signal), default);
        Assert.Equal(first, await w.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector, signal with { ObservedUtc = DateTimeOffset.UtcNow }), default));
        var metric = signal with { Metrics = [new("views", 110, DateTimeOffset.UtcNow)] }; Assert.NotEqual(first, await w.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector, metric), default));
        Assert.Equal("Held", (await w.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == brief.Id).State);
        await w.RecheckAsync(f.Owner, f.Universe, default); var reviewTasks = await w.RemediationsAsync(f.Owner, f.Universe, default); Assert.NotEmpty(reviewTasks); Assert.All(reviewTasks, x => Assert.Null(x.EditorialCorrectionId));
        for (var i = 0; i < 21; i++) await w.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector, metric with { Metrics = [new("views", 200 + i, DateTimeOffset.UtcNow.AddSeconds(-30 + i))] }), default);
        Assert.Equal(25, await s.GetRequiredService<CoreDbContext>().TrendCandidates.CountAsync(x => x.ClusterId == f.Cluster));
        var atLimit = (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Cluster);
        await w.ReviewClusterAsync(f.Owner, f.Universe, f.Cluster, new(Guid.NewGuid(), atLimit.RowVersion, "Approve", f.NewsClaim, "Fixture recheck of the complete retained evidence", DateTimeOffset.UtcNow.AddHours(1), "Fixture", "fixture:recheck-at-capacity"), default);
        Assert.Equal("Verified", (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Cluster).State);
        var retracted = metric with { ReportStatus = "Retracted", UpdatedUtc = DateTimeOffset.UtcNow }; var request = new SubmitTrendSignal(Guid.NewGuid(), f.Connector, retracted);
        var full = await Assert.ThrowsAsync<RuntimeValidationException>(() => w.SubmitAsync(f.Owner, f.Universe, request, default)); Assert.Equal("ObservationCapacityReached", full.Outcome);
        var held = (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Cluster);
        Assert.Contains("SourceObservationCapacityReached", held.Blocks);
        var renewed = await Assert.ThrowsAsync<EvidenceBlockedException>(() => w.ReviewClusterAsync(f.Owner, f.Universe, f.Cluster,
            new(Guid.NewGuid(), held.RowVersion, "Approve", f.NewsClaim, "Fixture cannot reapprove evidence after an unrecordable observation", DateTimeOffset.UtcNow.AddHours(1), "Fixture", "fixture:reject-incomplete-history"), default));
        Assert.Contains("SourceObservationCapacityReached", renewed.Message);
        await w.RecheckAsync(f.Owner, f.Universe, default); Assert.Contains(await w.RemediationsAsync(f.Owner, f.Universe, default), x => x.EditorialCorrectionId.HasValue);
        var db = s.GetRequiredService<CoreDbContext>(); var corrections = await db.EditorialCorrections.CountAsync(x => x.TenantId == f.Owner.TenantId);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.SubmitAsync(f.Owner, f.Universe, request, default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.SubmitAsync(f.Owner, f.Universe, new(Guid.NewGuid(), f.Connector, retracted with { ObservedUtc = DateTimeOffset.UtcNow }), default)); await w.RecheckAsync(f.Owner, f.Universe, default);
        Assert.Equal(corrections, await db.EditorialCorrections.CountAsync(x => x.TenantId == f.Owner.TenantId)); Assert.Equal("Held", (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Cluster).State);
    }
    [SqlFact]
    public async Task Configured_http_metadata_versions_are_stable_and_current_feed_rights_withhold_output()
    {
        var policies = new List<RssFeedPolicy>(); var xml = RssHttpContractTests.Feed(); var calls = 0; CoreDbContext? observed = null;
        var handler = new RssHttpContractTests.Handler((_, _) => { calls++; Assert.Null(observed!.Database.CurrentTransaction); return Task.FromResult(RssHttpContractTests.Response(xml)); });
        using var root = TrendTestFixture.Services(configure: services => services.AddSingleton<ITrendSource>(new ConfiguredRssTrendSource(policies, () => handler)));
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await TrendTestFixture.Setup(s, "rss-http-" + Guid.NewGuid()); observed = s.GetRequiredService<CoreDbContext>();
        var binding = RssHttpContractTests.Binding with { PermissionSnapshotId = f.NewsSources[0] }; policies.Add(new(binding, true)); var w = s.GetRequiredService<ITrendWorkbench>();
        var id = await w.ConnectorAsync(f.Owner, f.Universe, RssHttpContractTests.Config(binding), default); var request = new PollTrends(Guid.NewGuid(), id);
        var first = await w.PollAsync(f.Owner, f.Universe, request, default); Assert.Equal(first, await w.PollAsync(f.Owner, f.Universe, request, default)); Assert.Equal(1, calls);
        Assert.Equal(first, await w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), id), default)); xml = xml.Replace("Unverified source summary", "Updated metadata only"); var changed = await w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), id), default); Assert.NotEqual(first[0], changed[0]);
        var cluster = (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Candidates.Any(c => c.ConnectorId == id)); Assert.Equal(2, cluster.Candidates.Length); Assert.Contains("SubstanceUnavailable", cluster.Blocks); Assert.All(cluster.Candidates, c => { Assert.Empty(c.Signal.SubstanceSnapshotIds); Assert.Equal("MetadataOnly", c.Extraction!.EvidenceAccess); });
        var versionBase = xml;
        for (var version = 2; version < 25; version++) { xml = versionBase.Replace("Updated metadata only", "Observation version " + version); await w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), id), default); }
        var atCapacity = await w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), id), default); Assert.Single(atCapacity); Assert.Equal(25, await observed.TrendCandidates.CountAsync(x => x.ConnectorId == id));
        xml = versionBase.Replace("Updated metadata only", "One observation beyond capacity"); await Assert.ThrowsAsync<RuntimeValidationException>(() => w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), id), default)); Assert.Equal(25, await observed.TrendCandidates.CountAsync(x => x.ConnectorId == id)); var beforeRevocation = calls;
        var source = await (from snap in observed.SourceSnapshots join edition in observed.SourceEditions on snap.EditionId equals edition.Id join work in observed.SourceWorks on edition.WorkId equals work.Id where snap.Id == binding.PermissionSnapshotId select work).SingleAsync();
        await s.GetRequiredService<ISourceRegistry>().RevokeAsync(f.Owner, f.Universe, source.Id, new(Guid.NewGuid(), null, "fixture:withdraw-feed-permission", Convert.ToBase64String(source.RowVersion)), default);
        cluster = (await w.ClustersAsync(f.Owner, f.Universe, default)).Single(x => x.Id == cluster.Id); Assert.Contains("FeedRightsWithheld", cluster.Blocks); Assert.All(cluster.Candidates, c => { Assert.Equal("", c.Signal.Url); Assert.Equal("[Feed metadata withheld]", c.Signal.Headline); });
        await Assert.ThrowsAsync<RightsBlockedException>(() => w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), id), default)); Assert.Equal(beforeRevocation, calls);
        await Assert.ThrowsAsync<CoreAccessException>(() => w.PollAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, new(Guid.NewGuid(), id), default));
    }
    [SqlFact]
    public async Task Disable_committed_during_http_prevents_acceptance_without_holding_a_sql_transaction()
    {
        var policies = new List<RssFeedPolicy>(); Func<Task>? during = null;
        var handler = new RssHttpContractTests.Handler(async (_, _) => { await during!(); return RssHttpContractTests.Response(RssHttpContractTests.Feed()); });
        using var root = TrendTestFixture.Services(configure: services => services.AddSingleton<ITrendSource>(new ConfiguredRssTrendSource(policies, () => handler)));
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await TrendTestFixture.Setup(s, "rss-disable-" + Guid.NewGuid()); var w = s.GetRequiredService<ITrendWorkbench>(); var binding = RssHttpContractTests.Binding with { PermissionSnapshotId = f.NewsSources[0] }; policies.Add(new(binding, true)); var id = await w.ConnectorAsync(f.Owner, f.Universe, RssHttpContractTests.Config(binding), default);
        during = async () => { Assert.Null(s.GetRequiredService<CoreDbContext>().Database.CurrentTransaction); using var other = root.CreateScope(); var workbench = other.ServiceProvider.GetRequiredService<ITrendWorkbench>(); var row = (await workbench.ConnectorsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == id); await workbench.DisableAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), row.RowVersion, "Disable", "fixture:disabled-during-fetch", "fixture:operator-disabled"), default); };
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => w.PollAsync(f.Owner, f.Universe, new(Guid.NewGuid(), id), default)); Assert.False(await s.GetRequiredService<CoreDbContext>().TrendCandidates.AnyAsync(x => x.ConnectorId == id));
    }
}
