using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlHistoricalTests
{
    [SqlFact]
    public async Task Historical_retrieval_switch_workflow_factory_and_export_use_existing_owned_contracts()
    {
        using var root = HistoricalTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await HistoricalTestFixture.Setup(s, all: false); var proof = await HistoricalTestFixture.Pipeline(s, f);
        Assert.Equal("SupportedEvidence", proof.Match.Outcome); Assert.Equal("NoSupportedMatch", proof.NoMatch.Outcome);
        Assert.All(proof.Match.Evidence, x => Assert.Equal("HistoricalSource", x.Label));
        Assert.Equal("AwaitingReview", proof.WorkflowState); Assert.NotEqual(Guid.Empty, proof.FactoryArtifact);
        Assert.Equal("ExportOnly;NotPublished", proof.PublishingStatus);
        await Assert.ThrowsAsync<CoreAccessException>(() => s.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, proof.Query, default));
        var registry = s.GetRequiredService<ISourceRegistry>(); var work = await registry.InspectAsync(f.Owner, f.Universe, f.Works["sarkar"], default);
        await registry.RevokeAsync(f.Owner, f.Universe, work.Work.Id, new(Guid.NewGuid(), null, "History retrieval revocation", work.Work.RowVersion), default);
        var result = await s.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(f.Owner, f.Universe, proof.Query, default);
        Assert.DoesNotContain(result.Evidence, x => x.SnapshotId == f.Snapshots["sarkar"]);
        var held = await s.GetRequiredService<IWorkflowRuntime>().InspectAsync(f.Owner, proof.Workflow, default);
        Assert.True(held.OutputWithheld);
    }

    [SqlFact]
    public async Task Classification_and_disagreement_are_fenced_and_correction_and_revocation_withhold_history()
    {
        using var root = HistoricalTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await HistoricalTestFixture.Setup(s, all: false); var read = s.GetRequiredService<IReadWorkbench>();
        var collection = await read.RegisterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "invalid-history-" + Guid.NewGuid(), 1, "Invalid history probe", null, "Fixture", "Failure checks", 4), default);
        var request = new AddReadPiece(Guid.NewGuid(), collection, null, f.Items["legend"].Id, f.Lessons["legend"].Id, "history", 1, new("DocumentedFact", 0, 0, "UndatedLegend", [], []));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => read.AddAsync(f.Owner, f.Universe, request, default));
        request = request with { History = new("PopularLegend", 0, 0, "UndatedLearningScene", [], []) };
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => read.AddAsync(f.Owner, f.Universe, request, default));
        request = request with { EditorialItemId = f.Items["disagreement"].Id, LessonVariantId = f.Lessons["disagreement"].Id, History = new("DebatedClaim", 1659, 1659, "DisputedSourceYearCE", [], [], Guid.NewGuid()) };
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => read.AddAsync(f.Owner, f.Universe, request, default));
        var db = s.GetRequiredService<CoreDbContext>();
        var original = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == f.Anchors["coronation"]);
        var span = await db.ClaimSpans.AsNoTracking().FirstAsync(x => x.ClaimRevisionId == original.Id);
        var invented = span.ExactText + " fabricated addition";
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => s.GetRequiredService<IEvidenceEngine>().SubmitAsync(f.Owner, f.Universe,
            new(Guid.NewGuid(), "invented-history-" + Guid.NewGuid(), "", "SourceQuotation", "SourceQuotation", "en-IN", invented,
                "Fixture", "Historical account", "SourceExact", original.RubricId,
                [new(span.SnapshotId, span.Start, span.Length, invented, HistoricalTestFixture.Hash(invented), span.Locator, "Supporting")]), default));
        var disagreement = await db.ClaimDisagreements.SingleAsync(x => x.LeftRevisionId == f.Anchors["disagreement"]);
        await s.GetRequiredService<IEvidenceEngine>().CorrectAsync(f.Owner, f.Universe, new(Guid.NewGuid(), disagreement.RightRevisionId, "Peer account needs correction", "fixture:M26-peer-correction"), default);
        Assert.DoesNotContain((await read.PreviewAsync(f.Owner.Actor, f.Collection, default))!.Pieces, x => x.History!.Classification == "DebatedClaim");
        var sources = s.GetRequiredService<ISourceRegistry>(); var work = await sources.InspectAsync(f.Owner, f.Universe, f.Works["sarkar"], default);
        await sources.RevokeAsync(f.Owner, f.Universe, work.Work.Id, new(Guid.NewGuid(), null, "Fixture source permission withdrawn", work.Work.RowVersion), default);
        var book = (await read.PreviewAsync(f.Owner.Actor, f.Collection, default))!;
        Assert.Empty(book.Pieces); // The learning interpretation for the invented dialogue also cites the revoked methodology excerpt.
    }

    [SqlFact]
    public async Task Two_historical_watch_samples_pin_spoken_voice_evidence_captions_and_keep_public_release_blocked()
    {
        using var root = HistoricalTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await HistoricalTestFixture.Setup(s, all: false); var watch = s.GetRequiredService<IWatchFactory>();
        foreach (var fact in new[] { true, false })
        {
            var p = await HistoricalTestFixture.Watch(s, f, fact);
            Assert.Equal("Approved", p.State); Assert.Equal(2, p.Artifacts.Length); Assert.StartsWith(fact ? "Historical learning" : "Original fiction", p.Timeline!.FictionLabel);
            Assert.Contains(p.Timeline.Claims, x => x.Label == (fact ? "HistoricalClaim" : "FictionalScene"));
            if (fact) Assert.Equal(2, p.Timeline.SourceSnapshotIds.Length);
            var export = await watch.ExportAsync(f.Owner, f.Universe, p.Id, "Export", default);
            Assert.Contains(p.Timeline.Script, export.Vtt); Assert.NotEmpty(p.Timeline.Credits!);
            await Assert.ThrowsAsync<EvidenceBlockedException>(() => watch.ExportAsync(f.Owner, f.Universe, p.Id, "Publish", default));
            using var restart = root.CreateScope();
            foreach (var a in p.Artifacts) Assert.Equal(a.Sha256, MediaRules.Hash((await restart.ServiceProvider.GetRequiredService<IWatchFactory>().PreviewAsync(f.Owner, f.Universe, a.Id, default)).Bytes));
        }
    }

    [SqlFact]
    public async Task Ten_historical_entries_reuse_reviewed_pipeline_preserve_labels_and_remain_private_after_restart()
    {
        using var root = HistoricalTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var db = s.GetRequiredService<CoreDbContext>(); var geeta = CoreSeed.StableId("universe:geeta");
        var before = await db.CorpusTexts.AsNoTracking().Where(x => db.CorpusVerses.Any(v => v.Id == x.VerseId && db.CorpusManifests.Any(m => m.Id == v.ManifestId && m.UniverseId == geeta))).Select(x => new { x.Id, x.Sha256 }).ToArrayAsync();
        var f = await HistoricalTestFixture.Setup(s); var read = s.GetRequiredService<IReadWorkbench>();
        var book = (await read.PreviewAsync(f.Owner.Actor, f.Collection, default))!;
        Assert.Equal("History", book.MaterialKind); Assert.Equal(10, book.Pieces.Length); Assert.Equal(10, book.ExpectedVerses);
        Assert.Contains(book.Pieces, x => x.History!.Classification == "DebatedClaim" && x.Sources.Any(q => q.Kind == "DisputedHistoricalSource"));
        Assert.Contains(book.Pieces, x => x.History!.Classification == "PopularLegend" && x.History.StartYear == 0);
        Assert.Contains(book.Pieces, x => x.History!.Classification == "DramatizedDialogue");
        Assert.All(book.Pieces, x => { Assert.NotNull(x.History); Assert.DoesNotContain(x.Sources, y => y.Locale == "sa-IN"); Assert.All(x.Sources, y => Assert.NotEmpty(y.Attribution)); });
        Assert.Contains(await read.PrivateDirectoryAsync(f.Owner.Actor, default), x => x.Id == f.Collection && x.Universe.Length > 0);
        using var fresh = root.CreateScope(); Assert.Equal(10, (await fresh.ServiceProvider.GetRequiredService<IReadWorkbench>().PreviewAsync(f.Owner.Actor, f.Collection, default))!.Pieces.Length);
        await Assert.ThrowsAsync<CoreAccessException>(() => read.PreviewAsync("unrelated-historical-reader", f.Collection, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => read.InspectAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, f.Collection, default));
        Assert.Null(await s.GetRequiredService<IPublicRead>().GetAsync(f.Key, default));
        var status = await read.InspectAsync(f.Owner, f.Universe, f.Collection, default);
        Assert.Contains("FixtureCollectionCannotRollOut", status.ReleaseBlocks);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => read.ControlAsync(f.Owner, f.Universe, f.Collection, new(Guid.NewGuid(), status.RowVersion, "Activate"), default));
        Assert.Equal(before.OrderBy(x => x.Id), (await db.CorpusTexts.AsNoTracking().Where(x => db.CorpusVerses.Any(v => v.Id == x.VerseId && db.CorpusManifests.Any(m => m.Id == v.ManifestId && m.UniverseId == geeta))).Select(x => new { x.Id, x.Sha256 }).ToArrayAsync()).OrderBy(x => x.Id));
    }
}
