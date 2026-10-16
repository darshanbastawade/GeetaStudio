using System.Data;
using System.Security.Cryptography;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed partial class TrendWorkbench(CoreDbContext db, IEnumerable<ITrendSource> sources, IEnumerable<ITrendExtractor> extractors,
    IKnowledgeRetriever knowledge, ILessonEngine lessons, IUniverseRegistry registry, IDraftFactory factory, IEnumerable<IFactoryTemplate> templates, IEditorialWorkbench editorial) : ITrendWorkbench
{
    private static string Json<T>(T v) => EditorialBinding.Json(v);
    private static T Read<T>(string v) => EditorialBinding.Read<T>(v);
    private static string Hash<T>(T v) => EditorialBinding.Hash(v);
    private static Guid Derived(Guid id, string purpose) => new(SHA256.HashData(Encoding.UTF8.GetBytes(id + ":" + purpose)).AsSpan(0, 16));
    private async Task Access(CoreScope s, Guid u, string role, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
        var member = await db.Members.Where(x => x.TenantId == s.TenantId && x.Actor == s.Actor).Select(x => x.Permission).SingleOrDefaultAsync(ct);
        if (member == null || role == "Admin" && member != "Administrator" || role == "Review" && member is not ("Administrator" or "Editor") ||
            role == "Write" && member is not ("Administrator" or "Editor" or "Author")) throw new CoreAccessException();
    }
    private async Task<T> Command<T, R>(CoreScope s, Guid u, Guid op, string kind, R request, string role, Func<Task<T>> action, CancellationToken ct)
    {
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable operation ID required.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Trends:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Trend lock unavailable',1;", ct);
        await Access(s, u, role, ct); var fingerprint = Hash(new { u, request });
        var prior = await db.TrendOperations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (prior != null) { if (prior.Kind != kind || prior.Actor != s.Actor || prior.Fingerprint != fingerprint) throw new RuntimeConflictException("Trend replay differs."); return Read<T>(prior.ResultJson); }
        var result = await action(); db.TrendOperations.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = op, Kind = kind, Actor = s.Actor, Fingerprint = fingerprint, RequestJson = Json(request), ResultJson = Json(result) });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = op, Action = "trend." + kind });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
        await tx.CommitAsync(ct); return result;
    }
    private static void Expected(CoreRecord row, string expected) { if (Convert.ToBase64String(row.RowVersion) != expected) throw new CoreConcurrencyException(); }
    private async Task<TrendCluster> Cluster(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.TrendClusters.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<TrendConnector> Connector(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.TrendConnectors.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<TrendTeaching> Teaching(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.TrendTeachings.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<TrendStoryBrief> Story(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.TrendStoryBriefs.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<string> Text(CoreScope s, Guid u, Guid id, string locale, string region, CancellationToken ct)
    {
        var snapshot = await (from snap in db.SourceSnapshots.AsNoTracking()
                              join edition in db.SourceEditions on snap.EditionId equals edition.Id
                              join work in db.SourceWorks on edition.WorkId equals work.Id
                              where snap.Id == id && work.TenantId == s.TenantId && work.UniverseId == u
                              select snap).SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
        if (snapshot.ReviewStatus != "Approved" || snapshot.Text == null || snapshot.Text.Length > 65536) throw new EvidenceBlockedException("AuthorizedReviewedSubstanceRequired");
        var decision = await new SourceRightsGuard(db).DecideAsync(s, id, new("Read", locale, region, "Text"), ct);
        if (!decision.Decision.Allowed) throw new RightsBlockedException(decision.Decision.Reason); return snapshot.Text;
    }
    public Task<Guid> ConnectorAsync(CoreScope s, Guid u, RegisterTrendConnector r, CancellationToken ct) => Command(s, u, r.OperationId, "connector", r, "Admin", async () =>
    {
        CoreRules.Text(r.Key, 100, "Connector key"); TrendRules.Attest(r.Mode, r.EvidenceReference);
        if (r.Version < 1 || r.FixtureSignals.Length > 25 || !sources.Any(x => x.Key == r.SourceAdapter) || !extractors.Any(x => x.Key == r.ExtractorAdapter) ||
            r.Mode == "Manual" && r.SourceAdapter is not ("trend.uploaded-rss.v1" or "trend.configured-rss.v1")) throw new RuntimeValidationException("Approved deployed adapter/configuration and explicit fixture/upload coverage required.");
        sources.Single(x => x.Key == r.SourceAdapter).Validate(r);
        if (r.LiveFeed != null && r.SourceAdapter != "trend.configured-rss.v1") throw new RuntimeValidationException("Live binding requires the configured RSS adapter.");
        foreach (var signal in r.FixtureSignals) TrendRules.Signal(signal);
        var previous = await db.TrendConnectors.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (r.Version != (previous?.Version ?? 0) + 1) throw new RuntimeConflictException("Append the next connector version.");
        if (r.FeedSnapshotId.HasValue) await Text(s, u, r.FeedSnapshotId.Value, r.FeedLocale, r.FeedRegion, ct);
        var row = new TrendConnector { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r) }; db.TrendConnectors.Add(row); return row.Id;
    }, ct);
    public async Task<TrendConnectorView[]> ConnectorsAsync(CoreScope s, Guid u, CancellationToken ct)
    { await Access(s, u, "Read", ct); return (await db.TrendConnectors.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderBy(x => x.Key).ThenBy(x => x.Version).Take(100).ToListAsync(ct)).Select(x => new TrendConnectorView(x.Id, Read<RegisterTrendConnector>(x.SnapshotJson), x.Enabled, Convert.ToBase64String(x.RowVersion))).ToArray(); }
    public async Task DisableAsync(CoreScope s, Guid u, Guid id, TrendChange r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "disable", new { id, r }, "Admin", async () =>
    { var row = await Connector(s, u, id, ct); Expected(row, r.ExpectedRowVersion); if (r.Action != "Disable") throw new RuntimeValidationException("Disable only; register a reviewed version to switch."); TrendRules.Attest(r.EvidenceReference.StartsWith("fixture:") ? "Fixture" : "Manual", r.EvidenceReference); row.Enabled = false; return id; }, ct);
    private async Task<Guid> Ingest(CoreScope s, Guid u, TrendConnector connector, TrendSignal signal, string[] coverage, CancellationToken ct)
    {
        TrendRules.Signal(signal);
        if (!connector.Enabled) throw new EvidenceBlockedException("SourceDisabled");
        var texts = new Dictionary<Guid, string>();
        foreach (var id in signal.SubstanceSnapshotIds.Append(signal.TranscriptSnapshotId ?? Guid.Empty).Where(x => x != Guid.Empty).Distinct())
            texts[id] = await Text(s, u, id, signal.Locale, signal.Region, ct);
        if (texts.Values.Sum(x => x.Length) > 65536) throw new RuntimeValidationException("Extraction input limit64KiB.");
        var config = Read<RegisterTrendConnector>(connector.SnapshotJson);
        var extraction = extractors.Single(x => x.Key == config.ExtractorAdapter).Extract(new(signal, texts), ct);
        if (extraction.Contract != "trend.contract.v1" || extraction.Adapter != config.ExtractorAdapter || extraction.SnapshotIds.Any(x => !texts.ContainsKey(x)) || extraction.Text?.Length > 65536) throw new RuntimeValidationException("Extractor violated canonical permitted-evidence contract.");
        var key = TrendRules.ClusterKey(signal);
        var live = config.LiveFeed != null;
        // Immutable connector revision + opaque source ID + semantic observation, not poll ID/time.
        // Existing JSON history is queried in place: no migration or historical rewrites.
        var history = await db.TrendCandidates.FromSqlInterpolated($"SELECT c.* FROM [trends].[TrendCandidate] c JOIN [trends].[TrendCluster] g ON g.Id=c.ClusterId WHERE c.ConnectorId={connector.Id} AND c.TenantId={s.TenantId} AND c.UniverseId={u} AND JSON_VALUE(c.SnapshotJson,'$.ExternalId') COLLATE Latin1_General_100_BIN2={signal.ExternalId} AND ({live}=CAST(1 AS bit) OR g.[Key]={key})").AsNoTracking().Take(26).ToListAsync(ct);
        history.AddRange(db.TrendCandidates.Local.Where(x => db.Entry(x).State == EntityState.Added && x.ConnectorId == connector.Id && Read<TrendSignal>(x.SnapshotJson).ExternalId == signal.ExternalId && (live || TrendRules.ClusterKey(Read<TrendSignal>(x.SnapshotJson)) == key)));
        var identity = Observation(signal);
        var prior = history.OrderBy(x => x.RetrievedUtc).ThenBy(x => x.Id).FirstOrDefault(x => Observation(Read<TrendSignal>(x.SnapshotJson)) == identity);
        if (prior != null) return prior.Id;
        if (live && history.Count > 0) key = (await db.TrendClusters.AsNoTracking().SingleAsync(x => x.Id == history[0].ClusterId, ct)).Key;
        var cluster = db.TrendClusters.Local.SingleOrDefault(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == key) ?? await db.TrendClusters.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == key, ct);
        if (cluster == null) { cluster = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = key }; db.TrendClusters.Add(cluster); }
        if (await db.TrendCandidates.CountAsync(x => x.ClusterId == cluster.Id, ct) + db.TrendCandidates.Local.Count(x => x.ClusterId == cluster.Id && db.Entry(x).State == EntityState.Added) >= 25)
        {
            // Commit the safety hold even when history cannot accept another version. Do not
            // leave a previously approved brief usable after observing an unrecordable change.
            var action = "trend.observation-capacity-held." + identity;
            if (!db.SourceAudits.Local.Any(x => x.TenantId == s.TenantId && x.SubjectId == cluster.Id && x.Action == action) && !await db.SourceAudits.AnyAsync(x => x.TenantId == s.TenantId && x.SubjectId == cluster.Id && x.Action == action, ct))
            {
                if (cluster.State is not ("Corrected" or "Withdrawn")) cluster.State = "Held";
                await Invalidate(cluster, signal.ReportStatus == "Current" ? "SourceObservationCapacityReached" : "SourceCorrectedOrRetracted", ct);
                db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = cluster.Id, Action = action });
            }
            return Guid.Empty; // Durable failed-result marker, rejected only after transaction commit.
        }
        var row = new TrendCandidate { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ConnectorId = connector.Id, ClusterId = cluster.Id, SnapshotJson = Json(signal), ExtractionJson = Json(extraction), CoverageJson = Json(coverage), RetrievedUtc = DateTimeOffset.UtcNow };
        db.TrendCandidates.Add(row);
        if (cluster.State == "Verified" || signal.ReportStatus != "Current" && await db.TrendStoryBriefs.AnyAsync(x => x.ClusterId == cluster.Id, ct))
        {
            if (cluster.State is not ("Corrected" or "Withdrawn")) cluster.State = "Held";
            await Invalidate(cluster, signal.ReportStatus == "Current" ? "SourceObservationChanged" : "SourceCorrectedOrRetracted", ct);
        }
        return row.Id;
    }
    private static string Observation(TrendSignal signal) => Hash(signal with
    {
        ObservedUtc = default,
        SubstanceSnapshotIds = signal.SubstanceSnapshotIds.Distinct().Order().ToArray(),
        Risks = signal.Risks.Order().ToArray(),
        Metrics = signal.Metrics.OrderBy(x => x.Name).ThenBy(x => x.ObservedUtc).ThenBy(x => x.Value).ToArray()
    });
    public async Task<Guid[]> PollAsync(CoreScope s, Guid u, PollTrends r, CancellationToken ct)
    {
        await Access(s, u, "Admin", ct); if (r.OperationId == Guid.Empty) throw new RuntimeValidationException("Stable operation ID required.");
        var old = await db.TrendOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct);
        if (old != null) { if (old.Kind != "poll" || old.Actor != s.Actor || old.Fingerprint != Hash(new { u, request = r })) throw new RuntimeConflictException("Trend replay differs."); return PollResult(Read<Guid[]>(old.ResultJson)); }
        var connector = await Connector(s, u, r.ConnectorId, ct); if (!connector.Enabled) throw new EvidenceBlockedException("SourceDisabled");
        var config = Read<RegisterTrendConnector>(connector.SnapshotJson); var source = sources.Single(x => x.Key == config.SourceAdapter); source.Validate(config);
        var xml = config.FeedSnapshotId.HasValue ? await Text(s, u, config.FeedSnapshotId.Value, config.FeedLocale, config.FeedRegion, ct) : null;
        // No network inside the command transaction. Recheck current source/permission before acceptance.
        var discovery = await source.DiscoverAsync(new(config, config.LiveFeed == null ? xml : null), ct);
        if (discovery.Contract != "trend.contract.v1" || discovery.Adapter != config.SourceAdapter || discovery.Signals.Length > 25) throw new RuntimeValidationException("Discovery contract/budget exceeded.");
        return PollResult(await Command(s, u, r.OperationId, "poll", r, "Admin", async () =>
        {
            var current = await Connector(s, u, r.ConnectorId, ct); if (!current.Enabled || current.SnapshotJson != Json(config)) throw new EvidenceBlockedException("SourceDisabledOrChanged"); source.Validate(config);
            if (config.FeedSnapshotId.HasValue) await Text(s, u, config.FeedSnapshotId.Value, config.FeedLocale, config.FeedRegion, ct);
            var result = new List<Guid>(); foreach (var signal in discovery.Signals) result.Add(await Ingest(s, u, current, signal, discovery.Coverage, ct)); return result.ToArray();
        }, ct));
    }
    private static Guid[] PollResult(Guid[] ids)
    {
        if (ids.Contains(Guid.Empty)) throw new RuntimeValidationException("Observation capacity25 reached; affected work is held. Append a reviewed source/event version.", "ObservationCapacityReached");
        return ids;
    }
    public async Task<Guid> SubmitAsync(CoreScope s, Guid u, SubmitTrendSignal r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "manual", r, "Write", async () =>
        { if (r.Signal.Kind != "Manual") throw new RuntimeValidationException("Manual URL/topic submissions use the same evidence pipeline."); return await Ingest(s, u, await Connector(s, u, r.ConnectorId, ct), r.Signal, ["ManualSubmission", "NoAutomaticVerification"], ct); }, ct);
        return PollResult([id])[0];
    }
    private async Task<TrendClusterView> ClusterView(CoreScope s, TrendCluster row, CancellationToken ct)
    {
        var proof = await TrendFence.Cluster(db, row, ct); var blocks = proof.Blocks.ToList();
        if (row.State != "Verified" || row.CheckedFingerprint != proof.Fingerprint) blocks.Add("UnreviewedOrChangedEvidence");
        var records = await db.TrendCandidates.AsNoTracking().Where(x => x.ClusterId == row.Id).OrderBy(x => x.RetrievedUtc).ToListAsync(ct);
        var candidates = new List<TrendCandidateView>();
        foreach (var record in records)
        {
            var signal = Read<TrendSignal>(record.SnapshotJson); var extraction = Read<TrendExtraction>(record.ExtractionJson); var withheld = false;
            var connector = await db.TrendConnectors.AsNoTracking().SingleAsync(x => x.Id == record.ConnectorId, ct); var feed = Read<RegisterTrendConnector>(connector.SnapshotJson).LiveFeed;
            var feedWithheld = feed != null && !(await new SourceRightsGuard(db).DecideAsync(s, feed.PermissionSnapshotId, new("Read", signal.Locale, signal.Region, "Text"), ct)).Decision.Allowed;
            if (feedWithheld) { withheld = true; signal = signal with { Headline = "[Feed metadata withheld]", Url = "", EventKey = "", ReportOrigin = "Withheld", Metrics = [], SubstanceSnapshotIds = [], TranscriptSnapshotId = null }; }
            foreach (var id in extraction.SnapshotIds) if (!(await new SourceRightsGuard(db).DecideAsync(s, id, new("Read", signal.Locale, signal.Region, "Text"), ct)).Decision.Allowed) withheld = true;
            var observations = records.Where(x => x.ConnectorId == record.ConnectorId).Select(x => Read<TrendSignal>(x.SnapshotJson)).Where(x => x.ExternalId == signal.ExternalId).SelectMany(x => x.Metrics);
            candidates.Add(new(record.Id, row.Id, record.ConnectorId, signal, withheld ? null : extraction, record.RetrievedUtc, Read<string[]>(record.CoverageJson), TrendRules.Momentum(observations), withheld ? [feedWithheld ? "FeedRightsWithheld" : "RightsWithheld"] : []));
        }
        return new(row.Id, row.Key, row.State, row.Dilemma, row.ClaimRevisionId, row.CheckedUtc, row.ExpiresUtc, Convert.ToBase64String(row.RowVersion), proof.Fingerprint, proof.Independent, candidates.ToArray(), blocks.Distinct().ToArray());
    }
    public async Task<TrendClusterView[]> ClustersAsync(CoreScope s, Guid u, CancellationToken ct)
    { await Access(s, u, "Read", ct); var result = new List<TrendClusterView>(); foreach (var row in await db.TrendClusters.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)) result.Add(await ClusterView(s, row, ct)); return result.ToArray(); }
    public async Task ReviewClusterAsync(CoreScope s, Guid u, Guid id, ReviewTrendCluster r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "verify", new { id, r }, "Review", async () =>
    {
        var row = await Cluster(s, u, id, ct); Expected(row, r.ExpectedRowVersion); TrendRules.Attest(r.Mode, r.EvidenceReference);
        if (r.Decision is not ("Approve" or "Hold" or "Reject") || r.ExpiresUtc <= DateTimeOffset.UtcNow || r.ExpiresUtc > DateTimeOffset.UtcNow.AddDays(7)) throw new RuntimeValidationException("Explicit bounded verification/recheck deadline required.");
        CoreRules.Text(r.Dilemma, 2000, "Human dilemma");
        if (row.State is "Corrected" or "Withdrawn") throw new EvidenceBlockedException("Append fresh evidence/event version after correction.");
        row.ClaimRevisionId = r.ClaimRevisionId; row.Dilemma = r.Dilemma; row.CheckedUtc = DateTimeOffset.UtcNow; row.ExpiresUtc = r.ExpiresUtc;
        var proof = await TrendFence.Cluster(db, row, ct);
        if (r.Decision == "Approve" && proof.Blocks.Length > 0) throw new EvidenceBlockedException(string.Join(", ", proof.Blocks));
        row.CheckedFingerprint = proof.Fingerprint; row.State = r.Decision == "Approve" ? "Verified" : r.Decision == "Hold" ? "Held" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; return id;
    }, ct);
    public async Task<TrendTeachingView> LinkAsync(CoreScope s, Guid u, LinkTrendTeaching r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "teaching", r, "Write", async () =>
        {
            var cluster = await Cluster(s, u, r.ClusterId, ct); await TrendFence.AssertCluster(db, cluster, ct); CoreRules.Text(r.Rationale, 2000, "Adaptation rationale");
            if (r.Alternatives.Length > 10) throw new RuntimeValidationException("Bounded alternatives required.");
            var lesson = await lessons.InspectAsync(s, u, r.LessonVariantId, ct); if (lesson.OutputWithheld || lesson.ReviewStatus != "Approved") throw new EvidenceBlockedException("ReviewedInterpretationRequired");
            var result = await knowledge.RetrieveAsync(s, u, r.Query, ct);
            var anchorIds = await db.ClaimSpans.Where(x => x.ClaimRevisionId == lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId && x.CorpusTextId != null).Select(x => x.CorpusTextId!.Value).ToArrayAsync(ct);
            var interpretation = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == lesson.Snapshot.Bundle.Request.InterpretationClaimRevisionId, ct);
            var matched = result.Evidence.Any(x => anchorIds.Contains(x.CorpusTextId)) && interpretation.Type == "Interpretation" && interpretation.ReviewStatus == "Approved";
            var row = new TrendTeaching
            {
                Id = Guid.NewGuid(),
                TenantId = s.TenantId,
                UniverseId = u,
                ClusterId = cluster.Id,
                LessonVariantId = r.LessonVariantId,
                SnapshotJson = Json(new { Request = r, Retrieval = result }),
                Outcome = matched ? "Matched" : "NoSuitableGeetaConnection"
            }; db.TrendTeachings.Add(row); return row.Id;
        }, ct); return await TeachingView(s, u, await Teaching(s, u, id, ct), ct);
    }
    internal sealed record TeachingSnapshot(LinkTrendTeaching Request, KnowledgeResult Retrieval);
    private Task<TrendTeachingView> TeachingView(CoreScope s, Guid u, TrendTeaching row, CancellationToken ct) => Project(async () =>
    {
        row = await Teaching(s, u, row.Id, ct);
        var snapshot = Read<TeachingSnapshot>(row.SnapshotJson); var blocks = new List<string>();
        try { await TrendFence.AssertCluster(db, await Cluster(s, u, row.ClusterId, ct), ct); } catch (EvidenceBlockedException e) { blocks.Add(e.Message); }
        var lesson = await lessons.InspectAsync(s, u, row.LessonVariantId, ct); if (lesson.OutputWithheld || lesson.ReviewStatus != "Approved") blocks.Add("TeachingRightsOrReviewChanged");
        var candidateSignals = await db.TrendCandidates.AsNoTracking().Where(x => x.ClusterId == row.ClusterId).Select(x => x.SnapshotJson).ToArrayAsync(ct);
        var sensitive = candidateSignals.Select(x => Read<TrendSignal>(x)).Any(x => x.Risks.Length > 0);
        if (!await KnowledgeService.StoredAllowed(db, s, u, snapshot.Request.Query, snapshot.Retrieval, ct)) blocks.Add("StoredRetrievalOutputWithheld");
        var withheld = blocks.Count > 0;
        return new TrendTeachingView(row.Id, row.ClusterId, row.LessonVariantId, row.Outcome, withheld ? "" : snapshot.Request.Rationale, withheld ? null : snapshot.Retrieval,
            withheld ? [] : snapshot.Request.Alternatives, row.ReviewStatus, Convert.ToBase64String(row.RowVersion), blocks.ToArray())
        { RiskLevel = sensitive ? "SensitiveHumanReviewRequired" : "StandardEditorialReviewRequired" };
    }, ct);
    public Task<TrendTeachingView[]> TeachingsAsync(CoreScope s, Guid u, CancellationToken ct)
        => Project(async () => { await Access(s, u, "Read", ct); var result = new List<TrendTeachingView>(); foreach (var row in await db.TrendTeachings.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).Take(100).ToListAsync(ct)) result.Add(await TeachingView(s, u, row, ct)); return result.ToArray(); }, ct);
    public async Task ReviewTeachingAsync(CoreScope s, Guid u, Guid id, TrendReview r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "teaching-review", new { id, r }, "Review", async () =>
    {
        var row = await Teaching(s, u, id, ct); Expected(row, r.ExpectedRowVersion); TrendRules.Attest(r.Mode, r.EvidenceReference); var view = await TeachingView(s, u, row, ct);
        if (r.Decision is not ("Approve" or "Reject")) throw new RuntimeValidationException("Approve or Reject.");
        if (r.Decision == "Approve" && (view.Outcome != "Matched" || view.Blocks.Length > 0)) throw new EvidenceBlockedException("NoSuitableGeetaConnection");
        row.ReviewStatus = r.Decision == "Approve" ? "Approved" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; return id;
    }, ct);
}
public static class TrendServices { public static IServiceCollection AddTrendWorkbench(this IServiceCollection services) { services.AddScoped<ITrendWorkbench, TrendWorkbench>(); return services; } }
