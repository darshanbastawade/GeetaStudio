using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlTrendStoredEvidenceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static void NoText(object value, string text)
    {
        static bool Contains(JsonElement node, string needle) => node.ValueKind switch
        {
            JsonValueKind.String => node.GetString()!.Contains(needle, StringComparison.Ordinal),
            JsonValueKind.Array => node.EnumerateArray().Any(x => Contains(x, needle)),
            JsonValueKind.Object => node.EnumerateObject().Any(x => Contains(x.Value, needle)),
            _ => false
        };
        // Inspect decoded scalar fields too: Unicode escaping must not conceal a leak.
        Assert.False(Contains(JsonSerializer.SerializeToElement(value, Json), text), "Ineligible stored text leaked into a serialized response.");
    }

    [SqlFact]
    public async Task Independent_source_in_no_match_is_withheld_after_revocation_without_retrieval_or_snapshot_rewrite()
    {
        var embedding = new GuardedEmbedding(); using var root = TrendTestFixture.Services(embedding); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await TrendTestFixture.Setup(s, "stored-no-match-" + Guid.NewGuid()); var db = s.GetRequiredService<CoreDbContext>();
        var independent = await IndependentIndex(s, f); var trends = s.GetRequiredService<ITrendWorkbench>();
        var request = new LinkTrendTeaching(Guid.NewGuid(), f.Cluster, f.ChildLesson, independent.Query, "Controlled no-match lookup: " + independent.Text, ["No connection established: " + independent.Text]);
        var lookup = await trends.LinkAsync(f.Owner, f.Universe, request, default);
        Assert.Equal("NoSuitableGeetaConnection", lookup.Outcome); Assert.Contains(lookup.Retrieval!.Evidence, x => x.SnapshotId == independent.Snapshot);
        var frozen = await db.TrendTeachings.AsNoTracking().SingleAsync(x => x.Id == lookup.Id); var hash = EvidenceTestFixture.Hash(frozen.SnapshotJson);
        var sources = s.GetRequiredService<ISourceRegistry>(); var work = await sources.InspectAsync(f.Owner, f.Universe, independent.Work, default);
        await sources.RevokeAsync(f.Owner, f.Universe, independent.Work, new(Guid.NewGuid(), null, "fixture:independent-stored-source-revoked", work.Work.RowVersion), default);
        Assert.False((await s.GetRequiredService<ILessonEngine>().InspectAsync(f.Owner, f.Universe, f.ChildLesson, default)).OutputWithheld);
        var admin = s.GetRequiredService<IKnowledgeAdministration>();
        var alias = (await admin.AliasesAsync(f.Owner, f.Universe, default)).Single(x => x.Key == independent.Query.Alias);
        var original = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.EmbeddingSpaceId == f.Query.EmbeddingSpaceId);
        await admin.ActivateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), alias.Key, original.Id, alias.RowVersion, original.EmbeddingSpaceId), default);
        embedding.Forbid = true; var calls = embedding.Calls;
        var read = (await trends.TeachingsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == lookup.Id);
        NoText(read, independent.Text); Assert.Null(read.Retrieval); Assert.NotEmpty(read.Blocks); Assert.Equal(lookup.Outcome, read.Outcome);
        NoText(await trends.LinkAsync(f.Owner, f.Universe, request, default), independent.Text);
        Assert.Equal(calls, embedding.Calls);
        Assert.Equal(hash, EvidenceTestFixture.Hash((await db.TrendTeachings.AsNoTracking().SingleAsync(x => x.Id == lookup.Id)).SnapshotJson));
        await Assert.ThrowsAsync<CoreAccessException>(() => trends.TeachingsAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, default));
    }

    [SqlFact]
    public async Task Corrected_additional_hit_withholds_matched_story_and_blocks_renewed_review_and_reuse()
    {
        var embedding = new GuardedEmbedding(); using var root = TrendTestFixture.Services(embedding); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await TrendTestFixture.Setup(s, "stored-story-" + Guid.NewGuid()); var db = s.GetRequiredService<CoreDbContext>(); var trends = s.GetRequiredService<ITrendWorkbench>();
        var match = await TrendTestFixture.Match(s, f, "gathered Duryodhana"); Assert.Equal("Matched", match.Outcome);
        var extra = match.Retrieval!.Evidence.Single(x => x.Verse == 2);
        var story = await TrendTestFixture.Package(s, f, match); Assert.NotNull(story.Snapshot); Assert.Contains(story.Snapshot.Retrieval.Evidence, x => x.SnapshotId == extra.SnapshotId);
        var frozen = await db.TrendStoryBriefs.AsNoTracking().SingleAsync(x => x.Id == story.Id); var hash = EvidenceTestFixture.Hash(frozen.SnapshotJson);
        await s.GetRequiredService<IEditorialWorkbench>().CorrectAsync(f.Owner, f.Universe, new(Guid.NewGuid(), null, extra.SnapshotId, "Controlled correction of additional retrieval source, not the selected teaching.", "fixture:stored-extra-source-correction"), default);
        Assert.False((await s.GetRequiredService<ILessonEngine>().InspectAsync(f.Owner, f.Universe, f.ChildLesson, default)).OutputWithheld);
        embedding.Forbid = true; var calls = embedding.Calls;
        var read = (await trends.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == story.Id);
        NoText(read, extra.ExactText); Assert.Null(read.Snapshot); Assert.NotEmpty(read.Blocks);
        var teaching = (await trends.TeachingsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == match.Id);
        NoText(teaching, extra.ExactText); Assert.Null(teaching.Retrieval);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => trends.ReviewTeachingAsync(f.Owner, f.Universe, match.Id, new(Guid.NewGuid(), teaching.RowVersion, "Approve", "Fixture", "fixture:must-not-restore-corrected-text"), default));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => trends.ReviewStoryAsync(f.Owner, f.Universe, story.Id, new(Guid.NewGuid(), read.RowVersion, "Approve", "Fixture", "fixture:must-not-restore-corrected-story"), default));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => TrendTestFixture.Package(s, f, match));
        await trends.RecheckAsync(f.Owner, f.Universe, default);
        Assert.Equal("Held", (await trends.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == story.Id).State);
        var remediation = Assert.Single(await trends.RemediationsAsync(f.Owner, f.Universe, default));
        Assert.Equal("EditorialReview", remediation.State); Assert.Null(remediation.EditorialCorrectionId);
        Assert.Equal(0, await db.EditorialCorrections.CountAsync(x => x.ClaimRevisionId == f.NewsClaim));
        Assert.Equal(calls, embedding.Calls);
        Assert.Equal(hash, EvidenceTestFixture.Hash((await db.TrendStoryBriefs.AsNoTracking().SingleAsync(x => x.Id == story.Id)).SnapshotJson));
    }

    [SqlFact]
    public async Task Eligible_stored_results_and_empty_no_match_remain_honest_without_embedding_again()
    {
        var embedding = new GuardedEmbedding(); using var root = TrendTestFixture.Services(embedding); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await TrendTestFixture.Setup(s, "stored-valid-" + Guid.NewGuid()); var trends = s.GetRequiredService<ITrendWorkbench>();
        var match = await TrendTestFixture.Match(s, f); var empty = await TrendTestFixture.Match(s, f, "How do I bake a pizza?"); var story = await TrendTestFixture.Package(s, f, match);
        var independent = await IndependentIndex(s, f); var admin = s.GetRequiredService<IKnowledgeAdministration>();
        var aliases = await admin.AliasesAsync(f.Owner, f.Universe, default); var head = aliases.Single(x => x.Key == f.Query.Alias); var next = aliases.Single(x => x.Key == independent.Query.Alias);
        await admin.ActivateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), head.Key, next.ActiveIndexId!.Value, head.RowVersion, independent.Query.EmbeddingSpaceId), default);
        embedding.Forbid = true; var calls = embedding.Calls;
        var rows = await trends.TeachingsAsync(f.Owner, f.Universe, default);
        Assert.NotEmpty(rows.Single(x => x.Id == match.Id).Retrieval!.Evidence);
        Assert.Equal(match.Retrieval!.IndexId, rows.Single(x => x.Id == match.Id).Retrieval!.IndexId);
        var noMatch = rows.Single(x => x.Id == empty.Id); Assert.Equal("NoSuitableGeetaConnection", noMatch.Outcome); Assert.Empty(noMatch.Retrieval!.Evidence);
        Assert.NotNull((await trends.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == story.Id).Snapshot); Assert.Equal(calls, embedding.Calls);
    }

    private sealed record Independent(Guid Work, Guid Snapshot, string Text, KnowledgeQuery Query);
    private static async Task<Independent> IndependentIndex(IServiceProvider s, TrendTestFixture.Fixture f)
    {
        var db = s.GetRequiredService<CoreDbContext>(); var sources = s.GetRequiredService<ISourceRegistry>(); var corpus = s.GetRequiredService<ICanonicalCorpus>();
        var lesson = await s.GetRequiredService<ILessonEngine>().InspectAsync(f.Owner, f.Universe, f.ChildLesson, default);
        var anchorText = await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId && x.CorpusTextId != null).Select(x => x.CorpusTextId!.Value).FirstAsync();
        var manifestA = await (from t in db.CorpusTexts join v in db.CorpusVerses on t.VerseId equals v.Id where t.Id == anchorText select v.ManifestId).SingleAsync();
        var work = await sources.RegisterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "independent-stored-translation", "Controlled separate binding of exact Besant sample; no live qualification", "Translation"), default);
        var edition = await sources.EditionAsync(f.Owner, f.Universe, work, new(Guid.NewGuid(), "besant-sample-fixture", "M04 exact sample", "Annie Besant", "Controlled fixture", "en-IN", "Discourse1 verses1-2"), default);
        var permission = await sources.PermissionAsync(f.Owner, f.Universe, edition, new(Guid.NewGuid(), "fixture:independent-private-test-only", ["en-IN"], ["IN"], ["Text", "Json"], false, true, true, false, true, "Fixture only; no human or public rights approval."), default);
        var detail = await sources.InspectAsync(f.Owner, f.Universe, work, default); await sources.ReviewPermissionAsync(f.Owner, f.Universe, permission, new(detail.Permissions.Single().RowVersion, "Approved"), default);
        var bindings = new List<CorpusSourceBindingInput>(); var inputs = new List<CorpusVerseInput>(); Guid targetSnapshot = default; string targetText = "";
        for (var number = 1; number <= 2; number++)
        {
            var texts = await (from t in db.CorpusTexts.AsNoTracking() join v in db.CorpusVerses on t.VerseId equals v.Id where v.ManifestId == manifestA && v.Verse == number select t).ToArrayAsync();
            var en = texts.Single(x => x.Locale == "en-IN"); var sa = texts.Single(x => x.Locale == "sa-IN");
            var snapshot = await sources.ImportAsync(f.Owner, f.Universe, edition, new(Guid.NewGuid(), "exact-sample.txt", Encoding.UTF8.GetBytes(en.ExactText), en.Sha256, "fixture:separate-exact-sample-not-a-new-translation", true), default);
            detail = await sources.InspectAsync(f.Owner, f.Universe, work, default); await sources.ReviewSnapshotAsync(f.Owner, f.Universe, snapshot, new(detail.Snapshots.Single(x => x.Id == snapshot).RowVersion, "Approved"), default);
            bindings.Add(new(1, number, "", "Translation", "en-IN", snapshot, 1, number, en.Sha256, en.SourceLocator)); bindings.Add(new(1, number, "", sa.Kind, sa.Locale, sa.SnapshotId, 1, number, sa.Sha256, sa.SourceLocator));
            inputs.Add(new(1, number, "", [new("Translation", "en-IN", en.ExactText, en.Sha256, snapshot, en.SourceLocator), new(sa.Kind, sa.Locale, sa.ExactText, sa.Sha256, sa.SnapshotId, sa.SourceLocator)]));
            if (number == 2) { targetSnapshot = snapshot; targetText = en.ExactText; }
        }
        var manifest = await corpus.RegisterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "independent-stored-sample", 1, "Private exact two-verse fixture", 2, [new(1, 1, "", 1, "Exact sample"), new(1, 2, "", 2, "Exact sample")], bindings), default);
        await corpus.ImportAsync(f.Owner, f.Universe, manifest, new(Guid.NewGuid(), inputs), default);
        foreach (var verse in await corpus.ChapterAsync(f.Owner, f.Universe, manifest, 1, "IN", default)) await corpus.ReviewAsync(f.Owner, f.Universe, manifest, verse.Id, new(verse.RowVersion, "Approved", "fixture:stored-eligibility-regression"), default);
        var target = await db.CorpusTexts.AsNoTracking().Where(x => x.SnapshotId == targetSnapshot).Select(x => x.Id).SingleAsync();
        var admin = s.GetRequiredService<IKnowledgeAdministration>(); var indexId = await admin.QueueAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "independent-stored-index", manifest, "en-IN", "IN", "sql-hybrid-v1", "concept-projection-v1"), default);
        await s.GetRequiredService<IKnowledgeIndexWorker>().ProcessAsync(f.Owner, f.Universe, indexId, default);
        var index = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == indexId);
        await admin.ValidateAsync(f.Owner, f.Universe, indexId, new(index.RowVersion, "fixture:controlled-two-label-regression", [new("Duryodhana", target), new("How do I bake a pizza?", null)]), default);
        index = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == indexId);
        await admin.ActivateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "independent-stored", indexId, "", index.EmbeddingSpaceId), default);
        return new(work, targetSnapshot, targetText, new("independent-stored", "Duryodhana", "en-IN", "IN", index.EmbeddingSpaceId));
    }

    private sealed class GuardedEmbedding : ILegacyEmbeddingProvider
    {
        public int Calls { get; private set; }
        public bool Forbid { get; set; }
        public bool Supports(string model, int dimensions, string normalization) => new ReviewedConceptEmbedding().Supports(model, dimensions, normalization);
        public ValueTask<OwnedEmbedding> EmbedAsync(EmbeddingRequest request, CancellationToken ct)
        {
            if (Forbid) throw new InvalidOperationException("Stored evidence must be checked without another embedding operation.");
            Calls++; return new ReviewedConceptEmbedding().EmbedAsync(request, ct);
        }
    }
}
