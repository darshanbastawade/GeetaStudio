using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;
// Local reviewed-concept projection; no neural model, network or content transmission.
public sealed class ReviewedConceptEmbedding : ILegacyEmbeddingProvider
{
    public bool Supports(string model, int dimensions, string normalization) => model is "concept-projection-v1" or "concept-projection-v2" && dimensions == 128 && normalization == "l2";
    private static readonly HashSet<string> Stop = new("a an the and or of to in on is are was were did do does how where what who should i we it for with".Split(' '));
    public static string[] Tokens(string text) => Regex.Matches(text.Normalize(NormalizationForm.FormC).ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(m => m.Value).Where(t => !Stop.Contains(t)).Select(t => t.Length > 5 && t.EndsWith("ies") ? t[..^3] + "y" : t.Length > 5 && t.EndsWith("ed") ? t[..^2] : t.Length > 5 && t.EndsWith("ing") ? t[..^3] : t).Distinct().ToArray();
    public ValueTask<OwnedEmbedding> EmbedAsync(EmbeddingRequest r, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if (!Supports(r.Model, r.Dimensions, r.Normalization)) throw new ArgumentException("Unsupported embedding profile.");
        var features = Tokens(r.Text).Select(t => "word:" + t).ToHashSet();
        foreach (var c in r.Graph) if (c.Terms.Where(t => t.Locale == r.Locale).Any(t => Tokens(t.Text).All(x => features.Contains("word:" + x)) && Tokens(t.Text).Length > 0))
        {
            features.Add("concept:" + c.Id); foreach (var related in c.Related) features.Add("concept:" + related);
        }
        var vector = new float[r.Dimensions];
        foreach (var f in features) { var hash = SHA256.HashData(Encoding.UTF8.GetBytes(r.Model + ":" + f)); vector[BitConverter.ToUInt32(hash, 0) % r.Dimensions] += f.StartsWith("concept:") ? 3 : 1; }
        var norm = Math.Sqrt(vector.Sum(x => (double)x * x)); if (norm > 0) for (var i = 0; i < vector.Length; i++) vector[i] /= (float)norm;
        return ValueTask.FromResult(new OwnedEmbedding(vector, features.Order().ToArray()));
    }
}
public sealed class SqlHybridRanking : IKnowledgeRankingAdapter
{
    public string Key => "sql-hybrid-v1";
    public ValueTask<IReadOnlyList<KnowledgeScore>> RankAsync(KnowledgeRanking r, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var words = r.Query.Features.Where(f => f.StartsWith("word:", StringComparison.Ordinal)).ToArray();
        var concepts = r.Query.Features.Where(f => f.StartsWith("concept:", StringComparison.Ordinal)).ToArray();
        var weights = words.ToDictionary(f => f, f => Math.Log(1 + (r.Candidates.Count - r.Candidates.Count(c => c.Features.Contains(f)) + .5) / (r.Candidates.Count(c => c.Features.Contains(f)) + .5)));
        var denominator = Math.Max(.00001, weights.Values.Sum());
        var scores = r.Candidates.Select(c =>
        {
            var lexical = words.Where(c.Features.Contains).Sum(f => weights[f]) / denominator;
            var conceptHits = concepts.Count(c.Features.Contains);
            var semantic = conceptHits == 0 ? 0 : .75 * conceptHits / Math.Max(1, concepts.Length) + .25 * Math.Max(0, r.Query.Vector.Zip(c.Vector, (a, b) => (double)a * b).Sum());
            return new KnowledgeScore(c.ChunkId, .7 * lexical + .3 * semantic);
        }).Where(x => x.Score >= .12).OrderByDescending(x => x.Score).ThenBy(x => x.ChunkId).Take(r.Limit).ToArray();
        return ValueTask.FromResult<IReadOnlyList<KnowledgeScore>>(scores);
    }
}
public sealed class IndependentFixtureRanking : IKnowledgeRankingAdapter
{
    public string Key => "fixture-ranking-v1";
    public ValueTask<IReadOnlyList<KnowledgeScore>> RankAsync(KnowledgeRanking r, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var weights = new Dictionary<string, double>();
        foreach (var term in r.Query.Features)
        {
            var documents = 0; foreach (var candidate in r.Candidates) if (Array.IndexOf(candidate.Features, term) >= 0) documents++;
            weights[term] = 1d / (documents + 1);
        }
        var total = Math.Max(.00001, weights.Values.Sum()); var result = new List<KnowledgeScore>();
        foreach (var candidate in r.Candidates)
        {
            var score = 0d; foreach (var term in weights) if (Array.IndexOf(candidate.Features, term.Key) >= 0) score += term.Value;
            if (score > 0) result.Add(new(candidate.ChunkId, score / total));
        }
        return ValueTask.FromResult<IReadOnlyList<KnowledgeScore>>(result.OrderByDescending(x => x.Score).ThenBy(x => x.ChunkId).Take(r.Limit).ToArray());
    }
}
