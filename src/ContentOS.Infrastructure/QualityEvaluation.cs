using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed class QualityEvaluation(CoreDbContext db, IUniverseRegistry registry, IDraftFactory factory, IKnowledgeRetriever retriever, IEnumerable<IEvaluationAdapter> adapters) : IQualityEvaluation
{
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, ReadOptions)!;
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(value))));
    private async Task Access(CoreScope s, Guid u, bool write, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct) || !await db.Members.AnyAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor && (!write || x.Permission == "Administrator"), ct)) throw new CoreAccessException();
    }
    private async Task<Guid> Command<T>(CoreScope s, Guid u, Guid op, string kind, T request, Func<Task<Guid>> action, CancellationToken ct)
    {
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable operation required.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Evaluation:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Evaluation lock unavailable',1;", ct);
        await Access(s, u, true, ct); var fingerprint = Hash(new { u, request });
        var prior = await db.EvaluationOperations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (prior != null) { if (prior.Actor != s.Actor || prior.Kind != kind || prior.Fingerprint != fingerprint) throw new RuntimeConflictException("Evaluation replay differs."); return prior.ResultId; }
        var id = await action();
        db.EvaluationOperations.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = op, Kind = kind, Actor = s.Actor, Fingerprint = fingerprint, ResultId = id });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = "evaluation." + kind });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return id;
    }
    private async Task<EvaluationDataset> Dataset(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.EvaluationDatasets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<EvaluationRun> Run(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.EvaluationRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private async Task<EvaluationDatasetView> DatasetView(EvaluationDataset d, bool hideHeldOut, CancellationToken ct)
    {
        var reviews = await db.EvaluationReviews.AsNoTracking().Where(x => x.DatasetId == d.Id && x.RunId == null).ToListAsync(ct);
        var r = Read<RegisterEvaluationDataset>(d.SnapshotJson);
        return new(d.Id, hideHeldOut ? r with { Cases = r.Cases.Where(c => c.Split != "HeldOut").ToArray() } : r,
            reviews.Count(x => x.Mode == "Human" && Read<EvaluationHumanReview>(x.SnapshotJson).Accept),
            reviews.Count(x => x.Mode == "Fixture" && Read<EvaluationHumanReview>(x.SnapshotJson).Accept));
    }
    public async Task<EvaluationDatasetView> RegisterDatasetAsync(CoreScope s, Guid u, RegisterEvaluationDataset r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "dataset", r, async () =>
        {
            EvaluationRules.Dataset(r);
            var prior = await db.EvaluationDatasets.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (r.Version != (prior?.Version ?? 0) + 1) throw new RuntimeConflictException("Append the next dataset version.");
            foreach (var c in r.Cases)
            {
                if (!await db.Audiences.AnyAsync(x => x.Id == c.AudienceId && x.UniverseId == u, ct) || !await db.Locales.AnyAsync(x => x.Id == c.Locale, ct)) throw new CoreAccessException();
                foreach (var text in c.ExpectedTextIds) if (await Text(s, u, text, ct) == null) throw new RuntimeValidationException("Expected citations must resolve in the owned corpus.");
                if (c.Cast != null && (c.Cast.AudienceId != c.AudienceId || c.Cast.Locale != c.Locale)) throw new RuntimeValidationException("Cast must match case audience/locale.");
            }
            var row = new EvaluationDataset { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r) }; db.EvaluationDatasets.Add(row); return row.Id;
        }, ct); return await DatasetView(await Dataset(s, u, id, ct), false, ct);
    }
    public async Task<IReadOnlyList<EvaluationDatasetView>> DatasetsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var rows = await db.EvaluationDatasets.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(101).ToListAsync(ct);
        if (rows.Count > 100) throw new RuntimeValidationException("Dataset list exceeds initial capacity100.");
        var result = new List<EvaluationDatasetView>(); foreach (var row in rows) result.Add(await DatasetView(row, true, ct)); return result;
    }
    public async Task<EvaluationCandidateView> RegisterCandidateAsync(CoreScope s, Guid u, RegisterEvaluationCandidate r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "candidate", r, async () =>
        {
            CoreRules.Text(r.Key, 100, "Candidate key");
            foreach (var v in new[] { r.Configuration.PromptVersion, r.Configuration.RetrievalVersion, r.Configuration.ConfigurationVersion, r.Configuration.ReleaseKey }) CoreRules.Text(v, 100, "Configuration binding");
            if (r.Configuration.Adapter != "recorded.v1" && !adapters.Any(x => x.Key == r.Configuration.Adapter)) throw new RuntimeValidationException("Only deployed local evaluands or recorded outputs are supported; live calls require M07.");
            if (r.Configuration.Retrieval != null)
            {
                var index = await db.KnowledgeIndices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.Configuration.RetrievalIndexId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                if (index.Model is not ("concept-projection-v1" or "concept-projection-v2") || index.State != "Ready" || r.Configuration.Retrieval.EmbeddingSpaceId != index.EmbeddingSpaceId || r.Configuration.RetrievalVersion != index.EmbeddingSpaceId
                    || r.Configuration.Retrieval.Locale != index.Locale || r.Configuration.Retrieval.Use?.Purpose is not (null or "Read")) throw new RuntimeValidationException("Pin the owned ready retrieval index/space/locale and Read use.");
            }
            var prior = await db.EvaluationCandidates.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (r.Version != (prior?.Version ?? 0) + 1) throw new RuntimeConflictException("Append candidate version.");
            var row = new EvaluationCandidate { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r) }; db.EvaluationCandidates.Add(row); return row.Id;
        }, ct); return new(id, Read<RegisterEvaluationCandidate>((await db.EvaluationCandidates.AsNoTracking().SingleAsync(x => x.Id == id, ct)).SnapshotJson));
    }
    public async Task<IReadOnlyList<EvaluationCandidateView>> CandidatesAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct);
        var rows = await db.EvaluationCandidates.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).Take(201).ToListAsync(ct);
        if (rows.Count > 200) throw new RuntimeValidationException("Candidate capacity200 exceeded.");
        return rows.Select(x => new EvaluationCandidateView(x.Id, Read<RegisterEvaluationCandidate>(x.SnapshotJson))).ToArray();
    }
    private async Task<CorpusText?> Text(CoreScope s, Guid u, Guid id, CancellationToken ct) =>
        await (from t in db.CorpusTexts.AsNoTracking()
               join v in db.CorpusVerses on t.VerseId equals v.Id
               join m in db.CorpusManifests on v.ManifestId equals m.Id
               where t.Id == id && m.UniverseId == u && m.TenantId == s.TenantId && v.ReviewStatus == "Approved"
               select t).SingleOrDefaultAsync(ct);
    private async Task<(EvaluationInput Input, EvaluationFinding[] Findings)> Input(CoreScope s, Guid u, EvaluationCase c, CancellationToken ct)
    {
        var errors = new List<EvaluationFinding>(); var evidence = new List<EvaluationCitation>(); var cast = Array.Empty<EvaluationCastMember>();
        foreach (var id in c.ExpectedTextIds)
        {
            var t = await Text(s, u, id, ct);
            if (t == null) { errors.Add(new("FabricatedCitation", "Critical", "Expected corpus evidence is missing/unreviewed.")); continue; }
            var decision = await new SourceRightsGuard(db).DecideAsync(s, t.SnapshotId, new("Read", c.Locale, "IN", "Text"), ct);
            if (!decision.Decision.Allowed) errors.Add(new("Rights", "Critical", decision.Decision.Reason));
            evidence.Add(new(id));
        }
        if (c.Cast != null)
        {
            try
            {
                var plan = await registry.CastAsync(s, u, c.Cast, ct);
                if (plan.MissingRoles.Length > 0 && !c.ExpectNoAnswer) errors.Add(new("InappropriateCast", "Critical", "No eligible cast fills required roles."));
                if (plan.MissingRoles.Length == 0)
                {
                    cast = plan.Selected.Select(x => new EvaluationCastMember(x.Character.CharacterId, x.Character.Profile.Id, x.Role,
                        x.Presentation?.Rules.VoiceAnchor ?? x.Character.Profile.Rules.VoiceAnchor, x.Character.Profile.Rules.LifeStage)).ToArray();
                    if (plan.Selected.Any(x => x.Character.Status != "Active" || x.Character.Profile.ReviewStatus != "Approved")) errors.Add(new("InappropriateCast", "Critical", "Cast profile is not approved/active."));
                }
            }
            catch (RuntimeValidationException) { if (!c.ExpectNoAnswer) errors.Add(new("InappropriateCast", "Critical", "Cast constraints could not be satisfied.")); }
        }
        if (c.ExpectedAlias != null && !await db.Aliases.AnyAsync(x => x.UniverseId == u && x.Name == c.ExpectedAlias && x.CharacterId == c.ExpectedCharacterId, ct)) errors.Add(new("Alias", "Critical", "Alias does not resolve to the expected stable identity."));
        return (new(c, evidence.ToArray(), cast, "evaluation-input.v1"), errors.ToArray());
    }
    private async Task<EvaluationFinding[]> Evidence(CoreScope s, Guid u, EvaluationCase c, EvaluationOutput o, CancellationToken ct)
    {
        var errors = new List<EvaluationFinding>();
        if (c.RequiresMedia && !o.NoAnswer)
        {
            if (!c.MediaSnapshotId.HasValue || o.MediaEvidenceReference != "snapshot:" + c.MediaSnapshotId)
                errors.Add(new("MissingMediaEvidence", "Critical", "Media claims require the exact owned expected transcript snapshot, not an arbitrary reference."));
            else
            {
                var snapshot = await (from snap in db.SourceSnapshots
                                      join edition in db.SourceEditions on snap.EditionId equals edition.Id
                                      join work in db.SourceWorks on edition.WorkId equals work.Id
                                      where snap.Id == c.MediaSnapshotId && work.TenantId == s.TenantId && work.UniverseId == u
                                      select snap).SingleOrDefaultAsync(ct);
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Text)) errors.Add(new("MissingMediaEvidence", "Critical", "Expected transcript is unavailable in the owned source registry."));
                else
                {
                    var mediaRights = await new SourceRightsGuard(db).DecideAsync(s, snapshot.Id, new("Read", c.Locale, "IN", "Text"), ct);
                    if (!mediaRights.Decision.Allowed) errors.Add(new("Rights", "Critical", mediaRights.Decision.Reason));
                }
            }
        }
        if (o.Citations == null) return errors.Append(new EvaluationFinding("Schema", "Critical", "Citation array required.")).ToArray();
        foreach (var cite in o.Citations)
        {
            var t = await Text(s, u, cite.TextId, ct);
            if (t == null) { errors.Add(new("FabricatedCitation", "Critical", "Citation does not resolve in the owned reviewed corpus.")); continue; }
            if (!c.ExpectedTextIds.Contains(cite.TextId)) errors.Add(new("UnexpectedCitation", "Critical", "A resolvable verse is not necessarily relevant to this case."));
            if (cite.ExactQuote != null && cite.ExactQuote != t.ExactText) errors.Add(new("Quotation", "Critical", "Declared exact source quotation differs from the stored edition."));
            var decision = await new SourceRightsGuard(db).DecideAsync(s, t.SnapshotId, new("Read", c.Locale, "IN", "Text"), ct);
            if (!decision.Decision.Allowed) errors.Add(new("Rights", "Critical", decision.Decision.Reason));
        }
        return errors.ToArray();
    }
    private static string HeldOutIdentity(EvaluationCase c) => Hash(new { c.Input, c.Locale, c.AudienceId, c.Category });
    public async Task<EvaluationReport> RunAsync(CoreScope s, Guid u, RunEvaluation r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "run", r, async () =>
        {
            if (r.Split is not ("Development" or "HeldOut") || r.DeadlineSeconds is < 5 or > 300 || r.MaxCost != 0) throw new RuntimeValidationException("Bounded local evaluation only, cost0; use admitted recorded M07 outputs for remote results.");
            var dataset = Read<RegisterEvaluationDataset>((await Dataset(s, u, r.DatasetId, ct)).SnapshotJson);
            var candidate = Read<RegisterEvaluationCandidate>((await db.EvaluationCandidates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.CandidateId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException()).SnapshotJson);
            var cases = dataset.Cases.Where(c => c.Split == r.Split).ToArray(); if (cases.Length == 0) throw new RuntimeValidationException("Selected split has no cases.");
            var recorded = r.RecordedOutputs ?? [];
            if (candidate.Configuration.Adapter == "recorded.v1" && (recorded.Length != cases.Length || recorded.Select(x => x.CaseKey).Distinct().Count() != cases.Length || recorded.Any(x => !cases.Any(c => c.Key == x.CaseKey)))
                || candidate.Configuration.Adapter != "recorded.v1" && recorded.Length != 0) throw new RuntimeValidationException("Recorded outputs must exactly match the selected cases; fixture runs cannot substitute them.");
            EvaluationReport? baseline = null;
            if (r.BaselineRunId.HasValue)
            {
                baseline = Read<EvaluationReport>((await Run(s, u, r.BaselineRunId.Value, ct)).SnapshotJson);
                if (baseline.DatasetId != r.DatasetId || baseline.Split != "Development" || baseline.Results.Length < 1) throw new RuntimeValidationException("Baseline must be a Development run of this dataset version.");
            }
            if (r.Split == "HeldOut")
            {
                if (baseline == null) throw new RuntimeValidationException("Held-out scoring requires a measured Development baseline.");
                var usedDatasets = await (from run in db.EvaluationRuns join d in db.EvaluationDatasets on run.DatasetId equals d.Id where run.TenantId == s.TenantId && run.UniverseId == u && run.Split == "HeldOut" select d.SnapshotJson).ToListAsync(ct);
                var used = usedDatasets.SelectMany(x => Read<RegisterEvaluationDataset>(x).Cases.Where(c => c.Split == "HeldOut")).Select(HeldOutIdentity).ToHashSet();
                if (cases.Any(c => used.Contains(HeldOutIdentity(c)))) throw new RuntimeConflictException("Held-out cases have already been scored. Refresh genuinely new cases; operation replay alone is allowed.");
            }
            var results = new List<EvaluationCaseResult>(); var watch = Stopwatch.StartNew();
            foreach (var c in cases)
            {
                ct.ThrowIfCancellationRequested();
                if (watch.Elapsed.TotalSeconds >= r.DeadlineSeconds)
                {
                    results.Add(new(c.Key, c.Locale, c.Capability, null, 0, [new("Deadline", "Critical", "No work admitted after the original evaluation deadline.")], false) { Case = c, RetrievalSample = c.Category is "real-citation" or "missing-citation" or "no-answer" or "forced-connection" });
                    continue;
                }
                var latency = Stopwatch.StartNew(); var (input, inputErrors) = await Input(s, u, c, ct);
                EvaluationRetrievalSnapshot? retrievalSnapshot = null;
                EvaluationOutput? output = null; double? providerLatency = null; Guid? aiRunId = null; Guid? factoryArtifactId = null; var errors = inputErrors.ToList();
                if (watch.Elapsed.TotalSeconds >= r.DeadlineSeconds) errors.Add(new("Deadline", "Critical", "Original evaluation deadline exceeded."));
                else
                {
                    if (candidate.Configuration.Retrieval != null)
                    {
                        var q = candidate.Configuration.Retrieval with { Query = c.RetrievalQuery ?? c.Input, Locale = c.Locale };
                        var alias = await db.KnowledgeAliases.AsNoTracking().SingleOrDefaultAsync(x => x.UniverseId == u && x.Key == q.Alias && x.Locale == q.Locale, ct);
                        if (alias?.ActiveIndexId != candidate.Configuration.RetrievalIndexId) errors.Add(new("RetrievalBinding", "Critical", "Alias changed from the pinned candidate index; register a new candidate."));
                        else try
                            {
                                var found = await retriever.RetrieveAsync(s, u, q, ct);
                                retrievalSnapshot = new(found.IndexId, found.EmbeddingSpaceId, found.Adapter, found.Model, found.Outcome, found.Evidence.Select(x => x.CorpusTextId).ToArray());
                                if (found.IndexId != candidate.Configuration.RetrievalIndexId || found.EmbeddingSpaceId != candidate.Configuration.RetrievalVersion
                                    || c.ExpectNoAnswer && found.Evidence.Count > 0 || !c.ExpectNoAnswer && c.ExpectedTextIds.Any(id => !retrievalSnapshot.CorpusTextIds.Contains(id)))
                                    errors.Add(new("Retrieval", "Critical", "Measured retrieval does not satisfy expected evidence/no-answer properties."));
                                input = input with { Evidence = found.Evidence.Select(x => new EvaluationCitation(x.CorpusTextId)).ToArray() };
                            }
                            catch (KnowledgeBlockedException) { errors.Add(new("Retrieval", "Critical", "Owned knowledge policy/evidence blocks this query.")); }
                    }
                    if (candidate.Configuration.Adapter == "recorded.v1")
                    {
                        var item = recorded.Single(x => x.CaseKey == c.Key); output = item.Output;
                        if (item.AiRunId.HasValue)
                        {
                            var ai = await db.AiRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == item.AiRunId.Value && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                            var pinned = Read<AiExecutionSnapshot>(ai.SnapshotJson);
                            var actual = ai.OutputJson == null ? null : Read<GeneratedText>(ai.OutputJson);
                            var attempts = await db.AiAttempts.AsNoTracking().Where(x => x.RunId == ai.Id && x.State == "Completed").ToListAsync(ct);
                            aiRunId = ai.Id;
                            if (attempts.Count > 0 && attempts.All(a => a.EndedUtc.HasValue))
                                providerLatency = attempts.Sum(a => (a.EndedUtc!.Value - a.CreatedUtc).TotalMilliseconds);
                            var binding = pinned.Profiles.Where(p => attempts.Any(a => a.ProfileId == p.Id && a.Outcome == "Success")).ToArray();
                            if (ai.State != "Completed" || actual?.Text != output.Body || output.Cost != ai.Spent || output.Currency != pinned.Route.Currency || pinned.Locale != c.Locale
                                || pinned.RouteId != candidate.Configuration.AiRouteId || pinned.Route.PromptVersion != candidate.Configuration.PromptVersion
                                || binding.Length == 0 || binding.Any(p => p.Settings.Adapter != candidate.Configuration.EvaluandAdapter || p.Settings.Qualification != "LiveSandboxVerified" || p.Settings.Adapter.StartsWith("fixture.", StringComparison.Ordinal)))
                                errors.Add(new("ExecutionProvenance", "Critical", "Recorded output must bind a completed non-fixture M07 run, exact body/cost/locale/route/prompt/provider."));
                        }
                        else errors.Add(new("ExecutionProvenance", "Major", "Recorded execution lacks a qualified M07 binding; imported samples alone do not qualify a provider."));
                        if (item.FactoryArtifactId.HasValue)
                        {
                            factoryArtifactId = item.FactoryArtifactId;
                            var artifact = (await factory.ArtifactsAsync(s, u, ct)).SingleOrDefault(x => x.Id == item.FactoryArtifactId.Value) ?? throw new CoreAccessException();
                            if (artifact.OutputWithheld || artifact.Snapshot == null) errors.Add(new("Rights", "Critical", "Recorded factory output is currently withheld."));
                            else if (output.Body != artifact.Snapshot.Output.Body || output.Locale != artifact.Snapshot.Input.Brief.Cast.Locale) errors.Add(new("ArtifactMismatch", "Critical", "Recorded body/locale must match immutable factory output."));
                        }
                    }
                    else output = adapters.Single(x => x.Key == candidate.Configuration.Adapter).Evaluate(input);
                    if (output != null)
                    {
                        errors.AddRange(EvaluationRules.Check(c, output, input.Cast)); errors.AddRange(await Evidence(s, u, c, output, ct));
                        if (candidate.Configuration.Adapter != "recorded.v1" && output.Cost != 0) errors.Add(new("Budget", "Critical", "Local adapter cost must be zero."));
                    }
                    else errors.Add(new("Schema", "Critical", "Adapter returned no output."));
                }
                if (watch.Elapsed.TotalSeconds > r.DeadlineSeconds) errors.Add(new("Deadline", "Critical", "Evaluation exceeded its original deadline."));
                latency.Stop(); results.Add(new(c.Key, c.Locale, c.Capability, output, providerLatency ?? latency.Elapsed.TotalMilliseconds, errors.ToArray(), errors.Count == 0)
                {
                    Case = c,
                    RetrievalSample = c.Category is "real-citation" or "missing-citation" or "no-answer" or "forced-connection",
                    LatencyKind = providerLatency.HasValue ? "M07CompletedAttempts" : candidate.Configuration.Adapter == "recorded.v1" ? "ImportValidationOnly" : "LocalProcessing",
                    AiRunId = aiRunId,
                    FactoryArtifactId = factoryArtifactId,
                    Retrieval = retrievalSnapshot
                });
            }
            if (results.Where(x => x.Output != null).Select(x => x.Output!.Currency).Distinct().Count() > 1) throw new RuntimeValidationException("One report cannot add costs across different currencies.");
            if (baseline != null && results.Any(x => x.Output != null && x.Output.Currency != baseline.Qualification.Currency)) throw new RuntimeValidationException("Baseline/candidate costs require a common currency.");
            var report = new EvaluationReport(Guid.NewGuid(), r.DatasetId, r.CandidateId, r.Split, r.BaselineRunId, candidate.Configuration, results.ToArray(),
                new("Pending", [], 0, 0, 0, 0, baseline == null ? null : Retrieval(baseline.Results), EvaluationRules.P95(results.Select(x => x.LatencyMs)),
                    baseline == null ? null : Math.Max(50, baseline.Qualification.P95LatencyMs * 2), results.Sum(x => x.Output?.Cost ?? 0), [], [])
                { Currency = results.FirstOrDefault(x => x.Output != null)?.Output?.Currency ?? "INR" }, DateTimeOffset.UtcNow)
            { DatasetKey = dataset.Key, DatasetVersion = dataset.Version, CandidateKey = candidate.Key, CandidateVersion = candidate.Version };
            db.EvaluationRuns.Add(new() { Id = report.Id, TenantId = s.TenantId, UniverseId = u, DatasetId = r.DatasetId, CandidateId = r.CandidateId, BaselineRunId = r.BaselineRunId, Split = r.Split, SnapshotJson = Json(report) }); return report.Id;
        }, ct); return await Report(s, u, await Run(s, u, id, ct), ct);
    }
    private static double Retrieval(IEnumerable<EvaluationCaseResult> results)
    {
        var samples = results.Where(x => x.RetrievalSample).ToArray(); return samples.Length == 0 ? 0 : samples.Count(x => !x.Deterministic.Any(f => f.Code is "FabricatedCitation" or "MissingCitation" or "UnexpectedCitation" or "ForcedConnection" or "UnboundVerseLocator" or "Retrieval" or "RetrievalBinding")) / (double)samples.Length;
    }
    private async Task<EvaluationReport> Report(CoreScope s, Guid u, EvaluationRun row, CancellationToken ct)
    {
        var stored = Read<EvaluationReport>(row.SnapshotJson); var dataset = Read<RegisterEvaluationDataset>((await Dataset(s, u, row.DatasetId, ct)).SnapshotJson);
        var results = new List<EvaluationCaseResult>();
        foreach (var result in stored.Results)
        {
            var c = dataset.Cases.Single(c => c.Key == result.CaseKey); var (input, current) = await Input(s, u, c, ct);
            var findings = result.Deterministic.Concat(current).ToList();
            if (result.FactoryArtifactId.HasValue)
            {
                var artifact = (await factory.ArtifactsAsync(s, u, ct)).SingleOrDefault(x => x.Id == result.FactoryArtifactId);
                if (artifact == null || artifact.OutputWithheld || artifact.Snapshot == null)
                    findings.Add(new("Rights", "Critical", "Recorded factory evidence is currently unavailable/withheld."));
            }
            if (result.AiRunId.HasValue)
            {
                var ai = await db.AiRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == result.AiRunId && x.TenantId == s.TenantId && x.UniverseId == u, ct);
                if (ai == null || ai.State != "Completed") findings.Add(new("ExecutionProvenance", "Critical", "Recorded execution is not currently complete."));
                else foreach (var source in Read<AiExecutionSnapshot>(ai.SnapshotJson).SourceIds)
                {
                    if (!Guid.TryParse(source, out var sourceId)) { findings.Add(new("FabricatedCitation", "Critical", "Invalid recorded source binding.")); continue; }
                    var rights = await new SourceRightsGuard(db).DecideAsync(s, sourceId, new("Generate", result.Locale, "IN", "Text"), ct);
                    if (!rights.Decision.Allowed) findings.Add(new("Rights", "Critical", rights.Decision.Reason));
                }
            }
            if (result.Output != null) { findings.AddRange(EvaluationRules.Check(c, result.Output, input.Cast)); findings.AddRange(await Evidence(s, u, c, result.Output, ct)); }
            findings = findings.Distinct().ToList(); results.Add(result with { Deterministic = findings.ToArray(), Passed = findings.Count == 0, Output = findings.Any(x => x.Code == "Rights") ? null : result.Output, Case = findings.Any(x => x.Code == "Rights") ? null : c });
        }
        var reviews = await db.EvaluationReviews.AsNoTracking().Where(x => x.DatasetId == row.DatasetId && (x.RunId == null || x.RunId == row.Id)).ToListAsync(ct);
        var labels = reviews.Where(x => x.RunId == null && x.Mode == "Human").Select(x => Read<EvaluationHumanReview>(x.SnapshotJson)).ToArray();
        var human = reviews.Where(x => x.RunId == row.Id && x.Mode == "Human").Select(x => Read<EvaluationHumanReview>(x.SnapshotJson)).ToArray();
        var fixture = reviews.Count(x => x.RunId == row.Id && x.Mode == "Fixture" && Read<EvaluationHumanReview>(x.SnapshotJson).Accept);
        var blocks = new List<string>();
        if (results.Any(x => !x.Passed)) blocks.Add("DeterministicFailures");
        if (dataset.Cases.Count(c => c.Split == "Development" && labels.Any(l => l.CaseKey == c.Key && l.Accept)) < 100) blocks.Add("100HumanReviewedDevelopmentCasesRequired");
        if (dataset.Cases.Count(c => c.Split == "HeldOut" && labels.Any(l => l.CaseKey == c.Key && l.Accept)) < 20) blocks.Add("20HumanReviewedHeldOutCasesRequired");
        if (!EvaluationRules.Categories.All(category => dataset.Cases.Any(c => c.Category == category))) blocks.Add("RequiredCoverageMissing");
        if (row.Split != "HeldOut") blocks.Add("HeldOutReleaseSampleRequired");
        if (stored.Configuration.Retrieval != null && !await db.KnowledgeAliases.AnyAsync(x => x.UniverseId == u && x.Key == stored.Configuration.Retrieval.Alias && x.Locale == stored.Configuration.Retrieval.Locale && x.ActiveIndexId == stored.Configuration.RetrievalIndexId, ct)) blocks.Add("RetrievalConfigurationChanged");
        if (human.Count(x => x.Accept) != results.Count || human.Any(x => !x.Accept)) blocks.Add("HumanReleaseAcceptanceRequired");
        if (stored.Qualification.RetrievalTarget == null || Retrieval(results) < stored.Qualification.RetrievalTarget) blocks.Add("MeasuredRetrievalTargetNotMet");
        if (stored.Qualification.LatencyTargetMs == null || stored.Qualification.P95LatencyMs > stored.Qualification.LatencyTargetMs) blocks.Add("MeasuredLatencyTargetNotMet");
        if (stored.Configuration.Adapter != "recorded.v1") blocks.Add("FixtureAdapterNotProductionQualified");
        var tested = results.Select(x => x.Capability + "/" + x.Locale).Distinct().ToArray();
        var passed = blocks.Count != 0 ? Array.Empty<string>() : tested.Where(pair => results.Where(x => x.Capability + "/" + x.Locale == pair).All(x => x.Passed && human.Any(h => h.CaseKey == x.CaseKey && h.Accept))).ToArray();
        var advice = await db.EvaluationAdvices.AsNoTracking().Where(x => x.RunId == row.Id).Select(x => x.SnapshotJson).ToListAsync(ct);
        var q = stored.Qualification with
        {
            Status = blocks.Count == 0 ? "TestedConditionsAccepted" : "Blocked",
            Blocks = blocks.ToArray(),
            HumanAccepted = human.Count(x => x.Accept),
            FixtureAccepted = fixture,
            AdvisoryCount = advice.Count,
            RetrievalAccuracy = Retrieval(results),
            TestedCapabilitiesAndLocales = tested,
            PassedCapabilitiesAndLocales = passed
        };
        return stored with
        {
            Results = results.ToArray(),
            Qualification = q,
            FailureRate = results.Count(x => !x.Passed) / (double)results.Count,
            DatasetLabels = labels.Where(l => row.Split == "HeldOut" || dataset.Cases.Single(c => c.Key == l.CaseKey).Split == "Development").ToArray(),
            HumanResults = human,
            FixtureResults = reviews.Where(x => x.RunId == row.Id && x.Mode == "Fixture").Select(x => Read<EvaluationHumanReview>(x.SnapshotJson)).ToArray(),
            AdvisoryResults = results.Any(x => x.Deterministic.Any(f => f.Code == "Rights")) ? [] : advice.Select(Read<EvaluationAdvisory>).ToArray()
        };
    }
    public async Task<EvaluationReport> InspectAsync(CoreScope s, Guid u, Guid runId, CancellationToken ct)
    {
        await Access(s, u, false, ct); return await Report(s, u, await Run(s, u, runId, ct), ct);
    }
    public async Task<IReadOnlyList<EvaluationReport>> ReportsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var rows = await db.EvaluationRuns.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(101).ToListAsync(ct);
        if (rows.Count > 100) throw new RuntimeValidationException("Report capacity100 exceeded; archive/pagination required.");
        var result = new List<EvaluationReport>(); foreach (var row in rows) result.Add(await Report(s, u, row, ct)); return result;
    }
    public async Task ReviewAsync(CoreScope s, Guid u, EvaluationHumanReview r, CancellationToken ct)
    {
        await Command(s, u, r.OperationId, "review", r, async () =>
        {
            var d = Read<RegisterEvaluationDataset>((await Dataset(s, u, r.DatasetId, ct)).SnapshotJson);
            var c = d.Cases.SingleOrDefault(c => c.Key == r.CaseKey) ?? throw new RuntimeValidationException("Unknown review case.");
            if (r.RunId.HasValue) { var run = await Run(s, u, r.RunId.Value, ct); if (run.DatasetId != r.DatasetId || !Read<EvaluationReport>(run.SnapshotJson).Results.Any(x => x.CaseKey == r.CaseKey)) throw new RuntimeValidationException("Review must bind an actual run case."); }
            foreach (var v in new[] { r.FaithfulMeaning, r.Usefulness, r.Readability, r.AgeSuitability }) if (v is < 1 or > 5) throw new RuntimeValidationException("Rubric scores1–5 required.");
            if (r.Mode is not ("Fixture" or "Human") || r.Accept && new[] { r.FaithfulMeaning, r.Usefulness, r.Readability, r.AgeSuitability }.Min() < 4) throw new RuntimeValidationException("Acceptance requires all four rubric scores at least4.");
            if (r.Mode == "Human" && r.EvidenceReference.Trim().StartsWith("fixture:", StringComparison.OrdinalIgnoreCase) || r.Mode == "Fixture" && !r.EvidenceReference.Trim().StartsWith("fixture:", StringComparison.OrdinalIgnoreCase)) throw new RuntimeValidationException("Fixture references cannot count as human review.");
            if (r.CharacterAppeal is < 1 or > 5 || r.Understanding is < 1 or > 5 || c.Cast != null && r.Mode == "Human" && r.RunId.HasValue && (!r.CharacterAppeal.HasValue || !r.Understanding.HasValue)) throw new RuntimeValidationException("Cast human release review requires appeal and understanding feedback.");
            if (r.Mode == "Human" && !r.EvidenceReference.Trim().StartsWith("manual:", StringComparison.OrdinalIgnoreCase)) throw new RuntimeValidationException("Human review requires an explicit manual: evidence attestation.");
            if (r.Mode == "Human" && r.Accept && c.Cast != null && r.RunId.HasValue && (r.CharacterAppeal < 4 || r.Understanding < 4)) throw new RuntimeValidationException("Accepted cast feedback requires appeal and understanding scores at least4.");
            CoreRules.Text(r.EvidenceReference, 1000, "Review evidence"); CoreRules.Text(r.RubricVersion, 100, "Rubric version"); CoreRules.Text(r.Notes, 2000, "Review notes");
            if (await db.EvaluationReviews.AnyAsync(x => x.DatasetId == r.DatasetId && x.CaseKey == r.CaseKey && x.RunId == r.RunId && x.Mode == r.Mode, ct)) throw new RuntimeConflictException("Review is immutable; refresh the dataset/run to reassess.");
            var row = new EvaluationReview { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, DatasetId = r.DatasetId, RunId = r.RunId, CaseKey = r.CaseKey, Mode = r.Mode, Actor = s.Actor, SnapshotJson = Json(r) }; db.EvaluationReviews.Add(row); return row.Id;
        }, ct);
    }
    public async Task AdviseAsync(CoreScope s, Guid u, EvaluationAdvisory r, CancellationToken ct)
    {
        await Command(s, u, r.OperationId, "advisory", r, async () =>
        {
            var run = Read<EvaluationReport>((await Run(s, u, r.RunId, ct)).SnapshotJson);
            if (!run.Results.Any(x => x.CaseKey == r.CaseKey) || !double.IsFinite(r.Score) || r.Score is < 0 or > 1) throw new RuntimeValidationException("Advisory must bind an actual case with score0–1.");
            foreach (var v in new[] { r.JudgeAdapter, r.JudgeVersion, r.PromptVersion }) CoreRules.Text(v, 100, "Judge binding");
            CoreRules.Text(r.Rationale, 2000, "Advisory rationale"); CoreRules.Text(r.EvidenceReference, 1000, "Advisory evidence");
            var row = new EvaluationAdvice { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, RunId = r.RunId, CaseKey = r.CaseKey, Actor = s.Actor, SnapshotJson = Json(r) }; db.EvaluationAdvices.Add(row); return row.Id;
        }, ct);
    }
}
public static class EvaluationServices { public static IServiceCollection AddQualityEvaluation(this IServiceCollection s) => s.AddScoped<IQualityEvaluation, QualityEvaluation>(); }
