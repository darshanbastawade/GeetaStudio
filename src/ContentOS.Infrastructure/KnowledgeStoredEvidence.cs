using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class KnowledgeService
{
    // Revalidate the immutable result; never embed, rank, switch aliases or refresh its evidence.
    internal static async Task<bool> StoredAllowed(CoreDbContext db, CoreScope s, Guid u, KnowledgeQuery query, KnowledgeResult? result, CancellationToken ct)
    {
        if (query.Limit is < 1 or > 20 || result?.Evidence == null || result.Evidence.Count > query.Limit || result.Evidence.Select(x => x.ChunkId).Distinct().Count() != result.Evidence.Count) return false;
        var index = await db.KnowledgeIndices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == result.IndexId && x.TenantId == s.TenantId && x.UniverseId == u, ct);
        if (index == null || index.State != "Ready" || index.ValidationEvidence == null || index.Locale != query.Locale || index.Territory != query.Territory ||
            index.EmbeddingSpaceId != query.EmbeddingSpaceId || index.EmbeddingSpaceId != result.EmbeddingSpaceId || index.Adapter != result.Adapter || index.Model != result.Model ||
            index.Dimensions != result.Dimensions || index.Normalization != result.Normalization || index.Chunking != result.Chunking ||
            result.Outcome != (result.Evidence.Count == 0 ? "NoSupportedMatch" : "SupportedEvidence")) return false;
        if (!await db.CorpusManifests.AnyAsync(x => x.Id == index.ManifestId && x.TenantId == s.TenantId && x.UniverseId == u, ct)) return false;
        try
        {
            await CheckGraph(db, s, index, ct, query.Use);
            foreach (var hit in result.Evidence)
            {
                if (!double.IsFinite(hit.Score) || hit.Score < 0 || !await db.KnowledgeChunks.AnyAsync(x => x.IndexId == index.Id && x.StableChunkId == hit.ChunkId && x.CorpusTextId == hit.CorpusTextId, ct)) return false;
                var permitted = await Allowed(db, s, index, hit.CorpusTextId, ct, query.Use);
                if (permitted == null) return false;
                var p = permitted.Value;
                var exact = new KnowledgeHit(hit.ChunkId, p.Text.Id, p.Text.SnapshotId, p.Manifest.Id, p.Manifest.Version, p.Manifest.Edition,
                    p.Verse.Chapter, p.Verse.Verse, p.Verse.VariantKey, p.Text.Kind == "HistoricalSource" ? "HistoricalSource" : p.Text.Kind == "Translation" ? "Translation" : "Quotation",
                    p.Text.ExactText, p.SourceStart, p.Text.ExactText.Length, p.Text.Sha256, p.Text.SourceLocator, p.Citation, hit.Score);
                if (hit != exact) return false;
            }
        }
        catch (KnowledgeBlockedException) { return false; }
        catch (CoreAccessException) { return false; }
        return true;
    }
}
