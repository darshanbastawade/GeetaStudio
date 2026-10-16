using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class KnowledgeService(CoreDbContext db, IEmbeddingProvider embedding, IEnumerable<IKnowledgeRankingAdapter> adapters) : IKnowledgeAdministration, IKnowledgeRetriever, IKnowledgeIndexWorker
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static T Parse<T>(string s) => JsonSerializer.Deserialize<T>(s, Json)!;
    private static string Serialize<T>(T x) => JsonSerializer.Serialize(x, Json);
    private static string Hash<T>(T x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(x))));
    private static string Row(CoreRecord r) => Convert.ToBase64String(r.RowVersion);
    private static void Expected(CoreRecord r, string value) { byte[] b; try { b = Convert.FromBase64String(value); } catch (Exception e) when (e is FormatException or ArgumentNullException) { throw new RuntimeValidationException("Invalid rowversion."); } if (!r.RowVersion.SequenceEqual(b)) throw new CoreConcurrencyException(); }
    private static string Evidence(string x) { CoreRules.Text(x, 1000, "Review evidence"); if (!x.StartsWith("manual:") && !x.StartsWith("fixture:") && !x.StartsWith("evidence:")) throw new RuntimeValidationException("Explicit review evidence required."); return x; }
    private async Task Access(CoreScope s, Guid u, bool admin, CancellationToken ct)
    {
        var m = await db.Members.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor, ct);
        if (m == null || admin && m.Permission != "Administrator" || !await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
    }
    private IKnowledgeRankingAdapter Adapter(string key) => adapters.SingleOrDefault(x => x.Key == key) ?? throw new RuntimeValidationException("Retrieval adapter is not deployed/allowlisted.");
    private void Audit(CoreScope s, Guid id, string action) => db.SourceAudits.Add(new SourceAudit { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = action });
    private async Task Save(CancellationToken ct) { try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); } }
    private async Task<Guid> Command<T>(CoreScope s, Guid u, Guid op, string kind, T request, Func<Task<Guid>> execute, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Access(s, u, true, ct);
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable operation ID required.");
        var fingerprint = Hash(new { u, request }); var old = await db.KnowledgeCommands.FromSqlInterpolated($"SELECT * FROM [knowledge].[KnowledgeCommand] WITH (UPDLOCK,HOLDLOCK) WHERE [TenantId]={s.TenantId} AND [OperationId]={op}").SingleOrDefaultAsync(ct);
        if (old != null) { if (old.Actor != s.Actor || old.Kind != kind || old.Fingerprint != fingerprint) throw new RuntimeConflictException("Knowledge command replay differs."); return old.ResultId; }
        var id = await execute(); db.KnowledgeCommands.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, OperationId = op, Actor = s.Actor, Kind = kind, Fingerprint = fingerprint, ResultId = id }); Audit(s, id, kind); await Save(ct); await tx.CommitAsync(ct); return id;
    }
    private async Task Provenance(CoreScope s, Guid u, Guid snapshot, Guid? claim, CancellationToken ct)
    {
        if (!await (from snap in db.SourceSnapshots join e in db.SourceEditions on snap.EditionId equals e.Id join w in db.SourceWorks on e.WorkId equals w.Id where snap.Id == snapshot && w.UniverseId == u && w.TenantId == s.TenantId select snap.Id).AnyAsync(ct)) throw new CoreAccessException();
        if (claim is Guid id && !await db.ClaimRevisions.AnyAsync(r => r.Id == id && db.Claims.Any(c => c.Id == r.ClaimId && c.UniverseId == u), ct)) throw new CoreAccessException();
    }
    private static async Task<bool> CurrentClaim(CoreDbContext db, Guid? id, CancellationToken ct)
    {
        if (id == null) return true; var r = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return r.ReviewStatus == "Approved" && await db.Claims.AnyAsync(c => c.Id == r.ClaimId && c.CurrentVersion == r.Version, ct) && !await db.ClaimCorrections.AnyAsync(c => c.ClaimRevisionId == id && c.Status == "Open", ct) && !await db.EditorialCorrections.AnyAsync(c => c.ClaimRevisionId == id, ct);
    }
    public Task<Guid> ConceptAsync(CoreScope s, Guid u, RegisterConcept r, CancellationToken ct) => Command(s, u, r.OperationId, "knowledge.concept", r, async () =>
    {
        CoreRules.Text(r.Key, 200, "Concept key"); await Provenance(s, u, r.SnapshotId, r.ClaimRevisionId, ct);
        if (r.Terms == null || r.Terms.Count is < 1 or > 20) throw new RuntimeValidationException("1–20 explicit terms required.");
        foreach (var t in r.Terms) { CoreRules.Text(t.Text, 200, "Concept term"); if (t.Kind is not ("Name" or "Alias" or "Translation") || !await db.Locales.AnyAsync(l => l.Id == t.Locale, ct)) throw new RuntimeValidationException("Invalid term language/kind."); }
        if (r.Terms.Distinct().Count() != r.Terms.Count) throw new RuntimeValidationException("Duplicate terms.");
        var concept = new KnowledgeConcept { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, SnapshotId = r.SnapshotId, ClaimRevisionId = r.ClaimRevisionId, TermsJson = Serialize(r.Terms) };
        db.KnowledgeConcepts.Add(concept); return concept.Id;
    }, ct);
    public Task<Guid> RelationAsync(CoreScope s, Guid u, RegisterConceptRelation r, CancellationToken ct) => Command(s, u, r.OperationId, "knowledge.relation", r, async () =>
    {
        if (r.FromId == r.ToId || r.Kind is not ("Related" or "Broader" or "Contrasts")) throw new RuntimeValidationException("Explicit non-self relation required.");
        if (await db.KnowledgeConcepts.CountAsync(c => new[] { r.FromId, r.ToId }.Contains(c.Id) && c.UniverseId == u, ct) != 2) throw new CoreAccessException();
        await Provenance(s, u, r.SnapshotId, r.ClaimRevisionId, ct);
        var edge = new KnowledgeRelation { Id = Guid.NewGuid(), FromId = r.FromId, ToId = r.ToId, Kind = r.Kind, SnapshotId = r.SnapshotId, ClaimRevisionId = r.ClaimRevisionId }; db.KnowledgeRelations.Add(edge); return edge.Id;
    }, ct);
    public async Task ReviewAsync(CoreScope s, Guid u, Guid id, ReviewKnowledge r, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Access(s, u, true, ct);
        var concept = await db.KnowledgeConcepts.SingleOrDefaultAsync(c => c.Id == id && c.UniverseId == u, ct);
        var edge = concept == null ? await db.KnowledgeRelations.SingleOrDefaultAsync(e => e.Id == id && db.KnowledgeConcepts.Any(c => c.Id == e.FromId && c.UniverseId == u), ct) : null;
        CoreRecord row = concept as CoreRecord ?? edge ?? throw new CoreAccessException(); Expected(row, r.ExpectedRowVersion);
        if (r.Status is not ("Approved" or "Rejected") || (concept?.ReviewStatus ?? edge!.ReviewStatus) != "Pending") throw new RuntimeConflictException("Knowledge review is frozen.");
        Evidence(r.EvidenceReference); var snap = concept?.SnapshotId ?? edge!.SnapshotId; var claim = concept?.ClaimRevisionId ?? edge?.ClaimRevisionId;
        if (r.Status == "Approved")
        {
            var locale = await (from snapshot in db.SourceSnapshots join edition in db.SourceEditions on snapshot.EditionId equals edition.Id where snapshot.Id == snap select edition.Language).SingleAsync(ct);
            var rights = await new SourceRightsGuard(db).DecideAsync(s, snap, new("Read", locale, "IN", "Text"), ct);
            if (!rights.Decision.Allowed || !await CurrentClaim(db, claim, ct)) throw new KnowledgeBlockedException("UnreviewedOrRestrictedProvenance");
        }
        if (concept != null) { concept.ReviewStatus = r.Status; concept.Reviewer = s.Actor; concept.ReviewEvidence = r.EvidenceReference; }
        else { edge!.ReviewStatus = r.Status; edge.Reviewer = s.Actor; edge.ReviewEvidence = r.EvidenceReference; }
        Audit(s, id, "knowledge.review"); await Save(ct); await tx.CommitAsync(ct);
    }
    public async Task<KnowledgeGraph> GraphAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var concepts = await db.KnowledgeConcepts.AsNoTracking().Where(c => c.UniverseId == u).OrderBy(c => c.Key).Take(101).ToListAsync(ct);
        if (concepts.Count > 100) throw new RuntimeValidationException("Graph exceeds 100-concept bound.");
        var ids = concepts.Select(c => c.Id).ToArray(); var edges = await db.KnowledgeRelations.AsNoTracking().Where(e => ids.Contains(e.FromId)).OrderBy(e => e.Id).Take(301).ToListAsync(ct);
        if (edges.Count > 300) throw new RuntimeValidationException("Graph exceeds 300-edge bound.");
        return new(concepts.Select(c => new ConceptView(c.Id, c.Key, c.SnapshotId, c.ClaimRevisionId, Parse<ConceptTermInput[]>(c.TermsJson), c.ReviewStatus, c.Reviewer, c.ReviewEvidence, Row(c))).ToArray(), edges.Select(e => new RelationView(e.Id, e.FromId, e.ToId, e.Kind, e.SnapshotId, e.ReviewStatus, Row(e))).ToArray());
    }
    private static async Task CheckGraph(CoreDbContext db, CoreScope s, KnowledgeIndex index, CancellationToken ct, IntendedUse? use = null)
    {
        foreach (var c in Parse<ConceptFeature[]>(index.GraphJson))
        {
            var rights = await new SourceRightsGuard(db).DecideAsync(s, c.SnapshotId, use ?? new("Read", index.Locale, index.Territory, "Text"), ct);
            if (!rights.Decision.Allowed || !await CurrentClaim(db, c.ClaimRevisionId, ct)) throw new KnowledgeBlockedException("StaleGraphProvenance");
            if (c.ClaimRevisionId is Guid claimId)
            {
                foreach (var snapshot in await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == claimId).Select(x => x.SnapshotId).Distinct().ToListAsync(ct))
                {
                    var dependency = await new SourceRightsGuard(db).DecideAsync(s, snapshot, use ?? new("Read", index.Locale, index.Territory, "Text"), ct);
                    if (!dependency.Decision.Allowed) throw new KnowledgeBlockedException("StaleClaimEvidence");
                }
            }
        }
    }
    public Task<Guid> QueueAsync(CoreScope s, Guid u, QueueKnowledgeIndex r, CancellationToken ct) => Command(s, u, r.OperationId, "knowledge.index-queued", r, async () =>
    {
        if (embedding is IRoutedEmbeddingProvider routed) r = r with { Model = await routed.PinAsync(s, u, r.Model, ct) };
        CoreRules.Text(r.Key, 200, "Index key"); _ = Adapter(r.Adapter); if (!embedding.Supports(r.Model, 128, "l2")) throw new RuntimeValidationException("Unsupported embedding profile.");
        var manifest = await db.CorpusManifests.SingleOrDefaultAsync(m => m.Id == r.ManifestId && m.UniverseId == u, ct) ?? throw new CoreAccessException();
        SourceRules.Use("Read", r.Locale, r.Territory, "Text");
        var graph = await GraphAsync(s, u, ct); var approved = graph.Concepts.Where(c => c.ReviewStatus == "Approved").ToArray();
        var features = new List<ConceptFeature>();
        foreach (var c in approved)
        {
            var rights = await new SourceRightsGuard(db).DecideAsync(s, c.SnapshotId, new("Read", r.Locale, r.Territory, "Text"), ct);
            if (rights.Decision.Allowed && await CurrentClaim(db, c.ClaimRevisionId, ct)) features.Add(new(c.Id, c.SnapshotId, c.ClaimRevisionId, c.Terms, []));
        }
        // Approved relation provenance is carried as a separate graph feature, never inferred from generated text.
        foreach (var e in graph.Relations.Where(e => e.ReviewStatus == "Approved" && e.Kind is "Related" or "Broader"))
        {
            var entity = await db.KnowledgeRelations.AsNoTracking().SingleAsync(x => x.Id == e.Id, ct);
            var rights = await new SourceRightsGuard(db).DecideAsync(s, e.SnapshotId, new("Read", r.Locale, r.Territory, "Text"), ct);
            if (!rights.Decision.Allowed || !await CurrentClaim(db, entity.ClaimRevisionId, ct) || !features.Any(f => f.Id == e.FromId) || !features.Any(f => f.Id == e.ToId)) continue;
            var from = features.FindIndex(f => f.Id == e.FromId); features[from] = features[from] with { Related = features[from].Related.Append(e.ToId).ToArray() };
            features.Add(new(e.Id, e.SnapshotId, entity.ClaimRevisionId, [], []));
        }
        var material = await db.CorpusTexts.AsNoTracking().Where(t => t.Locale == r.Locale && db.CorpusVerses.Any(v => v.Id == t.VerseId && v.ManifestId == manifest.Id && v.ReviewStatus == "Approved")).OrderBy(t => t.Id).Select(t => t.Id).Take(1001).ToListAsync(ct);
        if (material.Count is < 1 or > 1000) throw new RuntimeValidationException("Index requires 1–1000 reviewed corpus components.");
        var graphJson = Serialize(features); var materialJson = Serialize(material);
        var index = new KnowledgeIndex
        {
            Id = Guid.NewGuid(),
            TenantId = s.TenantId,
            UniverseId = u,
            ManifestId = manifest.Id,
            Actor = s.Actor,
            Key = r.Key,
            Locale = r.Locale,
            Territory = r.Territory,
            Adapter = r.Adapter,
            Model = r.Model,
            GraphJson = graphJson,
            MaterialJson = materialJson,
            EmbeddingSpaceId = Hash(new { manifest.Id, manifest.Version, r.Model, Dimensions = 128, Normalization = "l2", Chunking = "verse-component-v1", r.Locale, Graph = graphJson, Material = materialJson })
        };
        db.KnowledgeIndices.Add(index); return index.Id;
    }, ct);
    private async Task<KnowledgeIndex> Index(Guid u, Guid id, CancellationToken ct) => await db.KnowledgeIndices.SingleOrDefaultAsync(x => x.Id == id && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private static async Task<(CorpusText Text, CorpusVerse Verse, CorpusManifest Manifest, CitationExport Citation, int SourceStart)?> Allowed(CoreDbContext db, CoreScope s, KnowledgeIndex index, Guid textId, CancellationToken ct, IntendedUse? use = null)
    {
        var text = await db.CorpusTexts.AsNoTracking().SingleOrDefaultAsync(t => t.Id == textId && t.Locale == index.Locale, ct); if (text == null) return null;
        var verse = await db.CorpusVerses.AsNoTracking().SingleAsync(v => v.Id == text.VerseId, ct);
        if (verse.ManifestId != index.ManifestId || verse.ReviewStatus != "Approved") return null;
        if (!await db.CorpusBindings.AnyAsync(b => b.ManifestId == verse.ManifestId && b.Chapter == verse.Chapter && b.Verse == verse.Verse && b.VariantKey == verse.VariantKey && b.Kind == text.Kind && b.Locale == text.Locale && b.SnapshotId == text.SnapshotId && b.Sha256 == text.Sha256 && b.SourceLocator == text.SourceLocator && b.SourceChapter == verse.Chapter && b.SourceVerse == verse.Verse, ct)) return null;
        var rights = await new SourceRightsGuard(db).DecideAsync(s, text.SnapshotId, use ?? new("Read", index.Locale, index.Territory, "Text"), ct);
        if (!rights.Decision.Allowed) return null;
        var manifest = await db.CorpusManifests.AsNoTracking().SingleAsync(m => m.Id == verse.ManifestId, ct);
        var sourceText = await db.SourceSnapshots.AsNoTracking().Where(x => x.Id == text.SnapshotId).Select(x => x.Text).SingleAsync(ct);
        var sourceStart = sourceText?.IndexOf(text.ExactText, StringComparison.Ordinal) ?? -1;
        if (sourceStart < 0) return null;
        return (text, verse, manifest, rights.Citation!, sourceStart);
    }
    private static void ValidateEmbedding(OwnedEmbedding e, int dimensions)
    {
        if (e.Vector.Length != dimensions || e.Vector.Any(x => !float.IsFinite(x)) || e.Features.Length > 4000) throw new KnowledgeBlockedException("InvalidEmbeddingContract");
        var norm = Math.Sqrt(e.Vector.Sum(x => (double)x * x)); if (norm != 0 && Math.Abs(norm - 1) > .001) throw new KnowledgeBlockedException("InvalidEmbeddingNormalization");
    }
    public Task<bool> ProcessNextAsync(CancellationToken ct) => Process(null, ct);
    public async Task<bool> ProcessAsync(CoreScope scope, Guid universe, Guid index, CancellationToken ct)
    {
        await Access(scope, universe, true, ct);
        if (!await db.KnowledgeIndices.AnyAsync(x => x.Id == index && x.UniverseId == universe && x.TenantId == scope.TenantId, ct)) throw new CoreAccessException();
        return await Process(index, ct);
    }
    private async Task<bool> Process(Guid? target, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var query = target == null ? db.KnowledgeIndices.FromSqlRaw("SELECT TOP (1) * FROM [knowledge].[KnowledgeIndex] WITH (UPDLOCK,HOLDLOCK) WHERE [State]='Queued' ORDER BY [CreatedUtc],[Id]") : db.KnowledgeIndices.FromSqlInterpolated($"SELECT * FROM [knowledge].[KnowledgeIndex] WITH (UPDLOCK,HOLDLOCK) WHERE [State]='Queued' AND [Id]={target.Value}");
        var index = await query.SingleOrDefaultAsync(ct);
        if (index == null) return false; var s = new CoreScope(index.TenantId, index.Actor);
        try
        {
            await Access(s, index.UniverseId, true, ct); await CheckGraph(db, s, index, ct);
            foreach (var id in Parse<Guid[]>(index.MaterialJson))
            {
                var allowed = await Allowed(db, s, index, id, ct); if (allowed == null) continue;
                var t = allowed.Value.Text; var vector = await embedding.EmbedAsync(new(index.Model, index.Dimensions, index.Normalization, t.ExactText, index.Locale, Parse<ConceptFeature[]>(index.GraphJson), s.TenantId, s.Actor, index.UniverseId), ct); ValidateEmbedding(vector, index.Dimensions);
                db.KnowledgeChunks.Add(new() { Id = Guid.NewGuid(), IndexId = index.Id, StableChunkId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(t.Id + ":" + index.Chunking))[..16]), CorpusTextId = t.Id, VectorJson = Serialize(vector.Vector), FeaturesJson = Serialize(vector.Features) });
            }
            if (!db.KnowledgeChunks.Local.Any(c => c.IndexId == index.Id)) throw new KnowledgeBlockedException("NoPermittedCorpus");
            index.State = "Ready"; Audit(s, index.Id, "knowledge.index-built"); await Save(ct); await tx.CommitAsync(ct); return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await tx.RollbackAsync(ct); await tx.DisposeAsync(); db.ChangeTracker.Clear(); var failed = await db.KnowledgeIndices.SingleAsync(i => i.Id == index.Id, ct); failed.State = "Failed"; failed.Error = e is KnowledgeBlockedException k ? k.Reason : e.GetType().Name; await Save(ct); return true;
        }
    }
    public async Task<IReadOnlyList<KnowledgeIndexView>> IndicesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var rows = await db.KnowledgeIndices.AsNoTracking().Where(i => i.UniverseId == u).OrderByDescending(i => i.CreatedUtc).Take(100).ToListAsync(ct);
        var result = new List<KnowledgeIndexView>(); foreach (var i in rows) result.Add(new(i.Id, i.Key, i.ManifestId, i.Locale, i.Adapter, i.Model, i.EmbeddingSpaceId, i.Dimensions, i.Normalization, i.Chunking, i.State, await db.KnowledgeChunks.CountAsync(c => c.IndexId == i.Id, ct), i.Error, i.ValidationEvidence, i.ValidationJson, Row(i))); return result;
    }
    private async Task<KnowledgeResult> Search(CoreScope s, KnowledgeIndex index, string query, string territory, int limit, CancellationToken ct, IntendedUse? use = null)
    {
        CoreRules.Text(query, 1000, "Query"); if (limit is < 1 or > 20 || index.State != "Ready") throw new RuntimeValidationException("Index must be ready; result limit 1–20.");
        if (territory != index.Territory) throw new KnowledgeBlockedException("TerritoryProfileMismatch");
        if (use != null)
        {
            SourceRules.Use(use.Purpose, use.Language, use.Territory, use.Format);
            if (use.Language != index.Locale || use.Territory != territory) throw new RuntimeValidationException("Intended-use language/territory must match the query.");
        }
        await CheckGraph(db, s, index, ct, use); var input = await embedding.EmbedAsync(new(index.Model, index.Dimensions, index.Normalization, query, index.Locale, Parse<ConceptFeature[]>(index.GraphJson), s.TenantId, s.Actor, index.UniverseId), ct); ValidateEmbedding(input, index.Dimensions);
        var chunks = await db.KnowledgeChunks.AsNoTracking().Where(c => c.IndexId == index.Id).Take(1001).ToListAsync(ct); if (chunks.Count > 1000) throw new RuntimeValidationException("Index exceeds local retrieval bound.");
        var permitted = new Dictionary<Guid, (KnowledgeChunk Chunk, CorpusText Text, CorpusVerse Verse, CorpusManifest Manifest, CitationExport Citation, int SourceStart)>();
        foreach (var c in chunks) { var allowed = await Allowed(db, s, index, c.CorpusTextId, ct, use); if (allowed != null) permitted[c.StableChunkId] = (c, allowed.Value.Text, allowed.Value.Verse, allowed.Value.Manifest, allowed.Value.Citation, allowed.Value.SourceStart); }
        var candidates = permitted.Select(p => new RankedCandidate(p.Key, Parse<float[]>(p.Value.Chunk.VectorJson), Parse<string[]>(p.Value.Chunk.FeaturesJson))).ToArray();
        foreach (var candidate in candidates) ValidateEmbedding(new(candidate.Vector, candidate.Features), index.Dimensions);
        var scores = await Adapter(index.Adapter).RankAsync(new(input, candidates, limit), ct);
        if (scores.Count > limit || scores.Select(x => x.ChunkId).Distinct().Count() != scores.Count || scores.Any(x => !permitted.ContainsKey(x.ChunkId) || !double.IsFinite(x.Score) || x.Score < 0)) throw new KnowledgeBlockedException("InvalidRetrievalContract");
        var hits = scores.Select(score => { var p = permitted[score.ChunkId]; return new KnowledgeHit(score.ChunkId, p.Text.Id, p.Text.SnapshotId, p.Manifest.Id, p.Manifest.Version, p.Manifest.Edition, p.Verse.Chapter, p.Verse.Verse, p.Verse.VariantKey, p.Text.Kind == "HistoricalSource" ? "HistoricalSource" : p.Text.Kind == "Translation" ? "Translation" : "Quotation", p.Text.ExactText, p.SourceStart, p.Text.ExactText.Length, p.Text.Sha256, p.Text.SourceLocator, p.Citation, score.Score); }).ToArray();
        return new(hits.Length == 0 ? "NoSupportedMatch" : "SupportedEvidence", index.Id, index.EmbeddingSpaceId, index.Adapter, hits, index.Model, index.Dimensions, index.Normalization, index.Chunking);
    }
    public async Task<KnowledgeResult> RetrieveAsync(CoreScope s, Guid u, KnowledgeQuery r, CancellationToken ct)
    {
        var ownsTransaction = db.Database.CurrentTransaction == null; if (ownsTransaction) db.ChangeTracker.Clear();
        await using var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null; await Access(s, u, false, ct);
        var alias = await db.KnowledgeAliases.AsNoTracking().SingleOrDefaultAsync(a => a.UniverseId == u && a.Locale == r.Locale && a.Key == r.Alias, ct);
        if (alias?.ActiveIndexId == null) throw new KnowledgeBlockedException("NoActiveIndex");
        var index = await Index(u, alias.ActiveIndexId.Value, ct);
        if (index.ValidationEvidence == null) throw new KnowledgeBlockedException("UnvalidatedIndex");
        if (index.EmbeddingSpaceId != r.EmbeddingSpaceId) throw new KnowledgeBlockedException("EmbeddingSpaceMismatch");
        var result = await Search(s, index, r.Query, r.Territory, r.Limit, ct, r.Use); Audit(s, index.Id, "knowledge.retrieved"); await Save(ct); if (tx != null) await tx.CommitAsync(ct); return result;
    }
    public async Task ValidateAsync(CoreScope s, Guid u, Guid id, ValidateKnowledgeIndex r, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Access(s, u, true, ct);
        var index = await Index(u, id, ct); Expected(index, r.ExpectedRowVersion); Evidence(r.EvidenceReference);
        if (r.Questions == null || r.Questions.Count is < 2 or > 20 || !r.Questions.Any(q => q.ExpectedCorpusTextId == null) || !r.Questions.Any(q => q.ExpectedCorpusTextId != null)) throw new RuntimeValidationException("Include positive and no-answer labeled questions.");
        var results = new List<object>(); var passed = true;
        foreach (var q in r.Questions) { var result = await Search(s, index, q.Query, index.Territory, 5, ct); var ok = q.ExpectedCorpusTextId is Guid target ? result.Evidence.Any(h => h.CorpusTextId == target) : result.Evidence.Count == 0; passed &= ok; results.Add(new { q.Query, q.ExpectedCorpusTextId, Passed = ok, Retrieved = result.Evidence.Select(h => h.CorpusTextId) }); }
        index.ValidationJson = Serialize(new { Passed = passed, Questions = results }); index.ValidationEvidence = passed ? r.EvidenceReference : null; Audit(s, id, passed ? "knowledge.shadow-passed" : "knowledge.shadow-failed"); await Save(ct); await tx.CommitAsync(ct);
    }
    public Task<Guid> ActivateAsync(CoreScope s, Guid u, ActivateKnowledgeIndex r, CancellationToken ct) => Command(s, u, r.OperationId, "knowledge.alias-activated", r, async () =>
    {
        CoreRules.Text(r.Alias, 200, "Alias"); var index = await Index(u, r.IndexId, ct);
        if (index.State != "Ready" || index.ValidationEvidence == null) throw new KnowledgeBlockedException("UnvalidatedIndex");
        if (index.EmbeddingSpaceId != r.QueryEmbeddingSpaceId) throw new KnowledgeBlockedException("EmbeddingSpaceMismatch");
        await CheckGraph(db, s, index, ct);
        // Recheck golden cases against current rights/reviews before activation, including rollback.
        var saved = Parse<JsonElement>(index.ValidationJson!);
        foreach (var q in saved.GetProperty("questions").EnumerateArray())
        {
            var result = await Search(s, index, q.GetProperty("query").GetString()!, index.Territory, 5, ct);
            var target = q.GetProperty("expectedCorpusTextId");
            if (target.ValueKind == JsonValueKind.Null ? result.Evidence.Count != 0 : !result.Evidence.Any(h => h.CorpusTextId == target.GetGuid())) throw new KnowledgeBlockedException("StaleValidation");
        }
        var alias = await db.KnowledgeAliases.FromSqlInterpolated($"SELECT * FROM [knowledge].[KnowledgeAlias] WITH (UPDLOCK,HOLDLOCK) WHERE [UniverseId]={u} AND [Locale]={index.Locale} AND [Key]={r.Alias}").SingleOrDefaultAsync(ct);
        if (alias == null) { if (r.ExpectedRowVersion != "") throw new CoreConcurrencyException(); alias = new() { Id = Guid.NewGuid(), UniverseId = u, Locale = index.Locale, Key = r.Alias }; db.KnowledgeAliases.Add(alias); }
        else Expected(alias, r.ExpectedRowVersion);
        alias.PreviousIndexId = alias.ActiveIndexId; alias.ActiveIndexId = index.Id; return alias.Id;
    }, ct);
    public async Task<IReadOnlyList<KnowledgeAliasView>> AliasesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); return await db.KnowledgeAliases.AsNoTracking().Where(a => a.UniverseId == u).Take(100).Select(a => new KnowledgeAliasView(a.Id, a.Key, a.Locale, a.ActiveIndexId, a.PreviousIndexId, Convert.ToBase64String(a.RowVersion))).ToListAsync(ct);
    }
}
