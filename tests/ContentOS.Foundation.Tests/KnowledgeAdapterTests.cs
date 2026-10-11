using ContentOS.Contracts;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class KnowledgeAdapterTests
{
    [Fact]
    public async Task Reviewed_alias_matches_without_rewriting_source_and_unknown_query_has_no_match()
    {
        var id = Guid.NewGuid(); var provider = new ReviewedConceptEmbedding();
        var graph = new[] { new ConceptFeature(id, Guid.NewGuid(), null, [new("en-IN", "battlefield", "Alias"), new("en-IN", "field", "Name")], []) };
        var document = await provider.EmbedAsync(new("concept-projection-v1", 128, "l2", "On the field of Kuru", "en-IN", graph), default);
        var query = await provider.EmbedAsync(new("concept-projection-v1", 128, "l2", "battlefield", "en-IN", graph), default);
        var absent = await provider.EmbedAsync(new("concept-projection-v1", 128, "l2", "How do I bake a pizza?", "en-IN", graph), default);
        foreach (var adapter in new ContentOS.Application.IKnowledgeRankingAdapter[] { new SqlHybridRanking(), new IndependentFixtureRanking() })
        {
            var candidate = new RankedCandidate(Guid.NewGuid(), document.Vector, document.Features);
            Assert.Single(await adapter.RankAsync(new(query, [candidate], 5), default)); Assert.Empty(await adapter.RankAsync(new(absent, [candidate], 5), default));
        }
        Assert.Contains("concept:" + id, document.Features);
    }
    [Fact]
    public async Task Equal_dimensions_do_not_make_different_models_compatible()
    {
        var provider = new ReviewedConceptEmbedding();
        var first = await provider.EmbedAsync(new("concept-projection-v1", 128, "l2", "field", "en-IN", []), default);
        var next = await provider.EmbedAsync(new("concept-projection-v2", 128, "l2", "field", "en-IN", []), default);
        Assert.Equal(first.Vector.Length, next.Vector.Length); Assert.False(first.Vector.SequenceEqual(next.Vector));
        await Assert.ThrowsAsync<ArgumentException>(async () => await provider.EmbedAsync(new("arbitrary-remote", 128, "l2", "field", "en-IN", []), default));
    }
}
