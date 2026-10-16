using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

public sealed partial class SqlFactoryAiTests
{
    [SqlFact]
    public async Task Permitted_feed_metadata_needs_independent_facts_and_uses_the_original_shared_ai_factory()
    {
        var calls = new Calls(); var policies = new List<RssFeedPolicy>(); var xml = RssHttpContractTests.Feed();
        var handler = new RssHttpContractTests.Handler((_, _) => Task.FromResult(RssHttpContractTests.Response(xml)));
        using var root = Services(calls, services => services.AddSingleton<ITrendSource>(new ConfiguredRssTrendSource(policies, () => handler)));
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s, calls); var owner = f.Factory.Owner; var universe = f.Factory.Universe; var db = s.GetRequiredService<CoreDbContext>();
        var sources = new[] { await TrendTestFixture.Source(s, owner, universe, "event-primary", TrendTestFixture.Fact, video: true), await TrendTestFixture.Source(s, owner, universe, "event-independent", TrendTestFixture.Fact, video: true) };
        var binding = RssHttpContractTests.Binding with { PermissionSnapshotId = sources[0] }; policies.Add(new(binding, true)); var trends = s.GetRequiredService<ITrendWorkbench>(); var connector = await trends.ConnectorAsync(owner, universe, RssHttpContractTests.Config(binding), default);
        await trends.PollAsync(owner, universe, new(Guid.NewGuid(), connector), default); var cluster = (await trends.ClustersAsync(owner, universe, default)).Single(); Assert.Contains("SubstanceUnavailable", cluster.Blocks); Assert.Equal(0, calls.Count);
        var signal = cluster.Candidates.Single().Signal with { Kind = "Manual", Primary = true, SubstanceSnapshotIds = sources };
        await trends.SubmitAsync(owner, universe, new(Guid.NewGuid(), connector, signal), default);
        var evidence = s.GetRequiredService<IEvidenceEngine>(); var rubric = await evidence.RegisterRubricAsync(owner, universe, new(Guid.NewGuid(), "event", 1, "Controlled two-source fixture, not live review", 2), default);
        var fact = await evidence.SubmitAsync(owner, universe, new(Guid.NewGuid(), "event", "", "NewsClaim", "NewsClaim", "en-IN", TrendTestFixture.Fact, "Controlled fixture", "", "Limited", rubric, sources.Select(id => new EvidenceSpanInput(id, 0, TrendTestFixture.Fact.Length, TrendTestFixture.Fact, EvidenceTestFixture.Hash(TrendTestFixture.Fact), "fixture:independent-event", "Supporting")).ToArray()), default);
        var claim = (await evidence.ListAsync(owner, universe, default)).SelectMany(x => x.Revisions).Single(x => x.Id == fact); await evidence.ReviewAsync(owner, universe, fact, new(claim.RowVersion, "Approved", "Corroborated", "Supported", "fixture:synthetic-factual-review"), default);
        cluster = (await trends.ClustersAsync(owner, universe, default)).Single(); await trends.ReviewClusterAsync(owner, universe, cluster.Id, new(Guid.NewGuid(), cluster.RowVersion, "Approve", fact, "Check the setting before deciding", DateTimeOffset.UtcNow.AddHours(1), "Manual", "manual:controlled-fixture-not-human-release"), default);
        var lesson = f.Plan.Snapshot!.Briefs.Single().Lesson; var text = await db.ClaimSpans.Where(x => x.ClaimRevisionId == lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId && x.CorpusTextId != null).Select(x => x.CorpusTextId!.Value).FirstAsync();
        var corpus = await db.CorpusTexts.AsNoTracking().SingleAsync(x => x.Id == text); var verse = await db.CorpusVerses.AsNoTracking().SingleAsync(x => x.Id == corpus.VerseId); var admin = s.GetRequiredService<IKnowledgeAdministration>();
        var concept = await admin.ConceptAsync(owner, universe, new(Guid.NewGuid(), "gathering", corpus.SnapshotId, [new("en-IN", "gather", "Name"), new("en-IN", "armies", "Alias")], lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId), default);
        var graph = await admin.GraphAsync(owner, universe, default); await admin.ReviewAsync(owner, universe, concept, new(graph.Concepts.Single(x => x.Id == concept).RowVersion, "Approved", "fixture:context"), default);
        var indexId = await admin.QueueAsync(owner, universe, new(Guid.NewGuid(), "trend-ai", verse.ManifestId, "en-IN", "IN", "sql-hybrid-v1", "concept-projection-v1"), default);
        for (var n = 0; n < 100; n++) { if ((await admin.IndicesAsync(owner, universe, default)).Single(x => x.Id == indexId).State == "Ready") break; await s.GetRequiredService<IKnowledgeIndexWorker>().ProcessNextAsync(default); }
        var index = (await admin.IndicesAsync(owner, universe, default)).Single(x => x.Id == indexId); await admin.ValidateAsync(owner, universe, indexId, new(index.RowVersion, "manual:M06-owner-approved-two-labels-reused-on-private-fixture", [new("Where did the opposing armies gather?", text), new("How do I bake a pizza?", null)]), default);
        index = (await admin.IndicesAsync(owner, universe, default)).Single(x => x.Id == indexId); await admin.ActivateAsync(owner, universe, new(Guid.NewGuid(), "trend-ai", indexId, "", index.EmbeddingSpaceId), default);
        var teaching = await trends.LinkAsync(owner, universe, new(Guid.NewGuid(), cluster.Id, f.Factory.Lesson, new("trend-ai", "Where did the opposing armies gather?", "en-IN", "IN", index.EmbeddingSpaceId), "An original setting analogy, not endorsement of events.", []), default);
        Assert.Equal("Matched", teaching.Outcome); await trends.ReviewTeachingAsync(owner, universe, teaching.Id, new(Guid.NewGuid(), teaching.RowVersion, "Approve", "Manual", "manual:controlled-fixture-not-human-release"), default);
        var brief = await trends.PrepareAsync(owner, universe, new(Guid.NewGuid(), teaching.Id, "shared-ai", 1, "OriginalFiction", "An original fictional choice about context.", "Gentle self-correction.", ["LocalPreview"], f.Plan.Snapshot.Rules), default);
        Assert.Contains("pending shared AI factory", brief.Snapshot!.Packages.Single().Script); Assert.Equal(0, calls.Count);
        await trends.ReviewStoryAsync(owner, universe, brief.Id, new(Guid.NewGuid(), brief.RowVersion, "Approve", "Manual", "manual:controlled-fixture-not-human-release"), default);
        var factory = s.GetRequiredService<IDraftFactory>(); var plan = (await factory.PlansAsync(owner, universe, default)).Single(x => x.Id == brief.FactoryPlanId); await factory.ReviewPlanAsync(owner, universe, plan.Id, new(Guid.NewGuid(), plan.RowVersion, true, "fixture:review-plan"), default);
        f = f with { Plan = (await factory.PlansAsync(owner, universe, default)).Single(x => x.Id == plan.Id) }; var batch = await Queue(root, f, Request(f)); var run = batch.Work.Single().WorkflowId!.Value;
        Assert.Equal("AwaitingReview", (await Advance(root, f, run)).State); await DrainFactory(root, f, batch.Id); batch = await Batch(s, f, batch.Id); Assert.Equal("Completed", batch.State); Assert.Equal(1, calls.Count); Assert.False(calls.SawTransaction); Assert.True(batch.GenerationCost > 0);
        var artifact = (await factory.ArtifactsAsync(owner, universe, default)).Single(x => x.Id == batch.Work.Single().ArtifactId); Assert.Contains(TrendTestFixture.Fact, artifact.Snapshot!.Output.Body); Assert.Equal(run, artifact.Snapshot.Generation!.WorkflowId);
        Assert.True(await db.TrendBindings.AnyAsync(x => x.BriefId == brief.Id && x.ContentRevisionId == artifact.CoreRevisionId));
        var links = await db.ClaimLinks.AsNoTracking().Where(x => x.ContentRevisionId == artifact.CoreRevisionId).OrderBy(x => x.Start).ToArrayAsync(); var end = 0;
        foreach (var link in links) { Assert.True(artifact.Snapshot.Output.Body[end..link.Start].All(char.IsWhiteSpace)); end = link.Start + link.Length; }
        Assert.Equal(artifact.Snapshot.Output.Body.Length, end); Assert.Equal("Pending", (await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == artifact.Snapshot.Generation.FictionClaimRevisionId)).ReviewStatus);
        Assert.True(await db.EditorialItems.AnyAsync(x => x.ContentId == artifact.CoreContentId));
    }
}
