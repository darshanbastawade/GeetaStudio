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
            r.Mode == "Manual" && r.SourceAdapter != "trend.uploaded-rss.v1") throw new RuntimeValidationException("Approved deployed adapter/configuration and explicit fixture/upload coverage required.");
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
        var cluster = db.TrendClusters.Local.SingleOrDefault(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == key) ?? await db.TrendClusters.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == key, ct);
        if (cluster == null) { cluster = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = key }; db.TrendClusters.Add(cluster); }
        if (await db.TrendCandidates.CountAsync(x => x.ClusterId == cluster.Id, ct) + db.TrendCandidates.Local.Count(x => x.ClusterId == cluster.Id) >= 25) throw new RuntimeValidationException("Cluster observation capacity25; append a new event/time-window.");
        var row = new TrendCandidate { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ConnectorId = connector.Id, ClusterId = cluster.Id, SnapshotJson = Json(signal), ExtractionJson = Json(extraction), CoverageJson = Json(coverage), RetrievedUtc = DateTimeOffset.UtcNow };
        db.TrendCandidates.Add(row); return row.Id;
    }
    public Task<Guid[]> PollAsync(CoreScope s, Guid u, PollTrends r, CancellationToken ct) => Command(s, u, r.OperationId, "poll", r, "Admin", async () =>
    {
        var connector = await Connector(s, u, r.ConnectorId, ct); if (!connector.Enabled) throw new EvidenceBlockedException("SourceDisabled"); var config = Read<RegisterTrendConnector>(connector.SnapshotJson);
        var xml = config.FeedSnapshotId.HasValue ? await Text(s, u, config.FeedSnapshotId.Value, config.FeedLocale, config.FeedRegion, ct) : null;
        var discovery = sources.Single(x => x.Key == config.SourceAdapter).Discover(new(config, xml), ct);
        if (discovery.Contract != "trend.contract.v1" || discovery.Adapter != config.SourceAdapter || discovery.Signals.Length > 25) throw new RuntimeValidationException("Discovery contract/budget exceeded.");
        var result = new List<Guid>(); foreach (var signal in discovery.Signals) result.Add(await Ingest(s, u, connector, signal, discovery.Coverage, ct)); return result.ToArray();
    }, ct);
    public Task<Guid> SubmitAsync(CoreScope s, Guid u, SubmitTrendSignal r, CancellationToken ct) => Command(s, u, r.OperationId, "manual", r, "Write", async () =>
    { if (r.Signal.Kind != "Manual") throw new RuntimeValidationException("Manual URL/topic submissions use the same evidence pipeline."); return await Ingest(s, u, await Connector(s, u, r.ConnectorId, ct), r.Signal, ["ManualSubmission", "NoAutomaticVerification"], ct); }, ct);
    private async Task<TrendClusterView> ClusterView(CoreScope s, TrendCluster row, CancellationToken ct)
    {
        var proof = await TrendFence.Cluster(db, row, ct); var blocks = proof.Blocks.ToList();
        if (row.State != "Verified" || row.CheckedFingerprint != proof.Fingerprint) blocks.Add("UnreviewedOrChangedEvidence");
        var records = await db.TrendCandidates.AsNoTracking().Where(x => x.ClusterId == row.Id).OrderBy(x => x.RetrievedUtc).ToListAsync(ct);
        var candidates = new List<TrendCandidateView>();
        foreach (var record in records)
        {
            var signal = Read<TrendSignal>(record.SnapshotJson); var extraction = Read<TrendExtraction>(record.ExtractionJson); var withheld = false;
            foreach (var id in extraction.SnapshotIds) if (!(await new SourceRightsGuard(db).DecideAsync(s, id, new("Read", signal.Locale, signal.Region, "Text"), ct)).Decision.Allowed) withheld = true;
            var observations = records.Where(x => x.ConnectorId == record.ConnectorId).Select(x => Read<TrendSignal>(x.SnapshotJson)).Where(x => x.ExternalId == signal.ExternalId).SelectMany(x => x.Metrics);
            candidates.Add(new(record.Id, row.Id, record.ConnectorId, signal, withheld ? null : extraction, record.RetrievedUtc, Read<string[]>(record.CoverageJson), TrendRules.Momentum(observations), withheld ? ["RightsWithheld"] : []));
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
    private sealed record TeachingSnapshot(LinkTrendTeaching Request, KnowledgeResult Retrieval);
    private async Task<TrendTeachingView> TeachingView(CoreScope s, Guid u, TrendTeaching row, CancellationToken ct)
    {
        var snapshot = Read<TeachingSnapshot>(row.SnapshotJson); var blocks = new List<string>();
        try { await TrendFence.AssertCluster(db, await Cluster(s, u, row.ClusterId, ct), ct); } catch (EvidenceBlockedException e) { blocks.Add(e.Message); }
        var lesson = await lessons.InspectAsync(s, u, row.LessonVariantId, ct); if (lesson.OutputWithheld || lesson.ReviewStatus != "Approved") blocks.Add("TeachingRightsOrReviewChanged");
        var candidateSignals = await db.TrendCandidates.AsNoTracking().Where(x => x.ClusterId == row.ClusterId).Select(x => x.SnapshotJson).ToArrayAsync(ct);
        var sensitive = candidateSignals.Select(x => Read<TrendSignal>(x)).Any(x => x.Risks.Length > 0);
        return new(row.Id, row.ClusterId, row.LessonVariantId, row.Outcome, snapshot.Request.Rationale, lesson.OutputWithheld ? null : snapshot.Retrieval,
            snapshot.Request.Alternatives, row.ReviewStatus, Convert.ToBase64String(row.RowVersion), blocks.ToArray())
        { RiskLevel = sensitive ? "SensitiveHumanReviewRequired" : "StandardEditorialReviewRequired" };
    }
    public async Task<TrendTeachingView[]> TeachingsAsync(CoreScope s, Guid u, CancellationToken ct)
    { await Access(s, u, "Read", ct); var result = new List<TrendTeachingView>(); foreach (var row in await db.TrendTeachings.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).Take(100).ToListAsync(ct)) result.Add(await TeachingView(s, u, row, ct)); return result.ToArray(); }
    public async Task ReviewTeachingAsync(CoreScope s, Guid u, Guid id, TrendReview r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "teaching-review", new { id, r }, "Review", async () =>
    {
        var row = await Teaching(s, u, id, ct); Expected(row, r.ExpectedRowVersion); TrendRules.Attest(r.Mode, r.EvidenceReference); var view = await TeachingView(s, u, row, ct);
        if (r.Decision is not ("Approve" or "Reject")) throw new RuntimeValidationException("Approve or Reject.");
        if (r.Decision == "Approve" && (view.Outcome != "Matched" || view.Blocks.Length > 0)) throw new EvidenceBlockedException("NoSuitableGeetaConnection");
        row.ReviewStatus = r.Decision == "Approve" ? "Approved" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; return id;
    }, ct);
}
public static class TrendServices { public static IServiceCollection AddTrendWorkbench(this IServiceCollection services) { services.AddScoped<ITrendWorkbench, TrendWorkbench>(); return services; } }
