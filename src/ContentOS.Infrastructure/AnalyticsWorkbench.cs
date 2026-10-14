using System.Diagnostics;
using System.Diagnostics.Metrics;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed partial class AnalyticsWorkbench(CoreDbContext db, IEnumerable<IChannelAnalyticsProvider> providers) : IAnalyticsWorkbench
{
    private static readonly Meter Meter = new("ContentOS.Analytics", "1.0");
    private static readonly Counter<long> Imports = Meter.CreateCounter<long>("analytics.imported");
    private static readonly Counter<long> Duplicates = Meter.CreateCounter<long>("analytics.duplicates");
    private static readonly ActivitySource Activities = new("ContentOS.Analytics");
    private static string Json<T>(T x) => EditorialBinding.Json(x);
    private static T Read<T>(string x) => EditorialBinding.Read<T>(x);
    private static string Hash(object x) => EditorialBinding.Hash(x);
    private static string Version(CoreRecord x) => Convert.ToBase64String(x.RowVersion);
    private async Task Access(CoreScope s, Guid u, bool write, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.TenantId == s.TenantId && x.Id == u, ct)) throw new CoreAccessException();
        var role = await db.Members.Where(x => x.TenantId == s.TenantId && x.Actor == s.Actor).Select(x => x.Permission).SingleOrDefaultAsync(ct);
        if (role == null || write && role != "Administrator") throw new CoreAccessException();
    }
    private async Task<T> Command<T>(CoreScope s, Guid u, Guid op, string kind, object request, Func<Task<T>> act, CancellationToken ct)
    {
        AnalyticsRules.Require(op != Guid.Empty, "Stable analytics operation required.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Analytics:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Analytics lock unavailable',1;", ct);
        await Access(s, u, true, ct); var fingerprint = Hash(new { u, request });
        var old = await db.AnalyticsCommands.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (old != null) { if (old.Actor != s.Actor || old.Kind != kind || old.Fingerprint != fingerprint) throw new RuntimeConflictException("Analytics operation replay differs."); return Read<T>(old.ResultJson); }
        var result = await act();
        db.AnalyticsCommands.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Actor = s.Actor, OperationId = op, Kind = kind, Fingerprint = fingerprint, RequestJson = Json(request), ResultJson = Json(result) });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = u, Action = "analytics." + kind });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
        await tx.CommitAsync(ct); return result;
    }
    public Task<Guid> SourceAsync(CoreScope s, Guid u, AnalyticsSourceRequest r, CancellationToken ct) => Command(s, u, r.OperationId, "source", r, async () =>
    {
        AnalyticsRules.Version(r.Key, r.Version); AnalyticsRules.Require(!string.Equals(r.Key, "owned-commerce-fulfillment", StringComparison.OrdinalIgnoreCase) && !string.Equals(r.SourceSystem, "ContentOS.Commerce", StringComparison.OrdinalIgnoreCase) && !string.Equals(r.Key, "owned-production", StringComparison.OrdinalIgnoreCase) && !string.Equals(r.Key, "owned-customer-payments", StringComparison.OrdinalIgnoreCase) && !string.Equals(r.SourceSystem, "ContentOS", StringComparison.OrdinalIgnoreCase) && !string.Equals(r.SourceSystem, "ContentOS.CustomerPayments", StringComparison.OrdinalIgnoreCase), "Owned production source is reserved for authoritative capture."); AnalyticsRules.Reference(r.SourceSystem); AnalyticsRules.Reference(r.AccountReference); AnalyticsRules.Reference(r.EvidenceReference);
        AnalyticsRules.Require(providers.Any(x => x.Key == r.Adapter), "Deployed analytics import adapter required.");
        var previous = await db.AnalyticsSources.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key).ToArrayAsync(ct);
        if (previous.Any(x => x.Version >= r.Version)) throw new RuntimeConflictException("Append a higher source profile version.");
        if (previous.Any(x => { var p = Read<AnalyticsSourceRequest>(x.SnapshotJson); return p.SourceSystem != r.SourceSystem || p.AccountReference != r.AccountReference; })) throw new RuntimeConflictException("Provider replacement cannot change the originating system/account.");
        var row = new AnalyticsSource { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r) }; db.AnalyticsSources.Add(row); return row.Id;
    }, ct);
    public async Task<AnalyticsSourceView[]> SourcesAsync(CoreScope s, Guid u, CancellationToken ct) { await Access(s, u, false, ct); return (await db.AnalyticsSources.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderBy(x => x.Key).ThenBy(x => x.Version).ToArrayAsync(ct)).Select(x => new AnalyticsSourceView(x.Id, Read<AnalyticsSourceRequest>(x.SnapshotJson), x.Enabled, Version(x))).ToArray(); }
    public Task<Guid> DefinitionAsync(CoreScope s, Guid u, AnalyticsDefinitionRequest r, CancellationToken ct) => Command(s, u, r.OperationId, "definition", r, async () =>
    {
        AnalyticsRules.Version(r.Key, r.Version); AnalyticsRules.Reference(r.Unit); AnalyticsRules.Reference(r.EvidenceReference);
        AnalyticsRules.Require(r.Aggregation is "Delta" or "Snapshot" or "Gauge" && !string.IsNullOrWhiteSpace(r.Description) && r.Description.Length <= 2000, "Explicit metric definition, aggregation and bounded description required.");
        if (await db.AnalyticsDefinitions.AnyAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key && x.Version >= r.Version, ct)) throw new RuntimeConflictException("Append a higher metric definition version.");
        var row = new AnalyticsDefinition { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r) }; db.AnalyticsDefinitions.Add(row); return row.Id;
    }, ct);
    public async Task<AnalyticsDefinitionView[]> DefinitionsAsync(CoreScope s, Guid u, CancellationToken ct) { await Access(s, u, false, ct); return (await db.AnalyticsDefinitions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToArrayAsync(ct)).Select(x => new AnalyticsDefinitionView(x.Id, Read<AnalyticsDefinitionRequest>(x.SnapshotJson))).ToArray(); }
    public Task<Guid> FxAsync(CoreScope s, Guid u, AnalyticsFxRequest r, CancellationToken ct) => Command(s, u, r.OperationId, "fx", r, async () =>
    {
        AnalyticsRules.Version(r.Key, r.Version); AnalyticsRules.Currency(r.FromCurrency); AnalyticsRules.Currency(r.ToCurrency); AnalyticsRules.Reference(r.EvidenceReference);
        AnalyticsRules.Require(r.Rate is > 0 and <= 1000000 && decimal.Round(r.Rate, 8) == r.Rate && r.FromCurrency != r.ToCurrency && r.EffectiveUtc <= DateTimeOffset.UtcNow, "Explicit effective FX rate and provenance required.");
        if (await db.AnalyticsRates.AnyAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key && x.Version >= r.Version, ct)) throw new RuntimeConflictException("Append a higher FX version.");
        var row = new AnalyticsFx { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r) }; db.AnalyticsRates.Add(row); return row.Id;
    }, ct);
    public async Task<AnalyticsFxView[]> RatesAsync(CoreScope s, Guid u, CancellationToken ct) { await Access(s, u, false, ct); return (await db.AnalyticsRates.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToArrayAsync(ct)).Select(x => new AnalyticsFxView(x.Id, Read<AnalyticsFxRequest>(x.SnapshotJson))).ToArray(); }
    public Task<Guid> ConsentAsync(CoreScope s, Guid u, AnalyticsConsentRequest r, CancellationToken ct) => Command(s, u, r.OperationId, "consent", r, async () =>
    {
        AnalyticsRules.Reference(r.SubjectReference); AnalyticsRules.Reference(r.EvidenceReference);
        AnalyticsRules.Require(r.Purpose == "AnalyticsCohorts" && r.ExpiresUtc > DateTimeOffset.UtcNow && r.ExpiresUtc <= DateTimeOffset.UtcNow.AddYears(2), "Explicit bounded analytics consent required.");
        AnalyticsRules.Require(r.Fixture ? r.SubjectReference.StartsWith("fixture:") && r.EvidenceReference.StartsWith("fixture:") : r.SubjectReference.StartsWith("subject:") && Guid.TryParseExact(r.SubjectReference[8..], "D", out _) && !r.EvidenceReference.StartsWith("fixture:"), "Pseudonymous subject and genuine versus fixture consent evidence required.");
        if (await db.AnalyticsConsents.AnyAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.SubjectReference == r.SubjectReference && x.Purpose == r.Purpose, ct)) throw new RuntimeConflictException("Subject/purpose consent already exists; revoked consent is not renewed implicitly.");
        var row = new AnalyticsConsent { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, SubjectReference = r.SubjectReference, Purpose = r.Purpose, ExpiresUtc = r.ExpiresUtc, Fixture = r.Fixture, EvidenceReference = r.EvidenceReference }; db.AnalyticsConsents.Add(row); return row.Id;
    }, ct);
    public async Task<AnalyticsConsentView[]> ConsentsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, true, ct); return await db.AnalyticsConsents.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).Select(x => new AnalyticsConsentView(x.Id, x.Purpose, x.Enabled, x.ExpiresUtc, x.Fixture, Convert.ToBase64String(x.RowVersion))).ToArrayAsync(ct);
    }
    public async Task ControlAsync(CoreScope s, Guid u, string kind, Guid id, AnalyticsControl r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "control", new { kind, id, r }, async () =>
    {
        AnalyticsRules.Reference(r.EvidenceReference); CoreRecord row;
        if (kind == "source") row = await db.AnalyticsSources.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        else if (kind == "consent") row = await db.AnalyticsConsents.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        else throw new RuntimeValidationException("Source or consent required.");
        if (Version(row) != r.ExpectedRowVersion) throw new CoreConcurrencyException();
        AnalyticsRules.Require(r.Action == "Disable", "Disable required; append a source version to re-enable.");
        if (row is AnalyticsSource source) source.Enabled = false; else ((AnalyticsConsent)row).Enabled = false; return id;
    }, ct);
    public Task<AnalyticsProviderInfo[]> ProvidersAsync(CoreScope s, Guid u, CancellationToken ct) => ProviderViews(s, u, ct);
    private async Task<AnalyticsProviderInfo[]> ProviderViews(CoreScope s, Guid u, CancellationToken ct) { await Access(s, u, false, ct); return providers.Select(x => x.Info).ToArray(); }

    public Task<AnalyticsImportResult> ImportAsync(CoreScope s, Guid u, AnalyticsImport r, CancellationToken ct) => Command(s, u, r.OperationId, "import", r, async () =>
    {
        using var activity = Activities.StartActivity("analytics.import");
        var source = await db.AnalyticsSources.SingleOrDefaultAsync(x => x.Id == r.SourceId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        AnalyticsRules.Require(source.Enabled && r.Entries != null && r.Entries.Length is > 0 and <= 100, "Enabled source and at most 100 entries required.");
        var snapshot = Read<AnalyticsSourceRequest>(source.SnapshotJson); var adapter = providers.SingleOrDefault(x => x.Key == snapshot.Adapter) ?? throw new RuntimeValidationException("Original analytics adapter unavailable.");
        var normalized = await adapter.NormalizeAsync(snapshot, r.Entries!, ct);
        AnalyticsRules.Require(normalized.Length == r.Entries!.Length, "Adapter must preserve event cardinality.");
        var result = await Insert(s, u, source, normalized, false, ct); Imports.Add(result.Inserted); Duplicates.Add(result.Duplicates); return result;
    }, ct);
    private async Task<AnalyticsImportResult> Insert(CoreScope s, Guid u, AnalyticsSource source, AnalyticsEntryInput[] inputs, bool owned, CancellationToken ct)
    {
        AnalyticsRules.Require(inputs.All(x => x != null), "Null analytics events are invalid.");
        var sourceConfig = Read<AnalyticsSourceRequest>(source.SnapshotJson); var ids = new List<Guid>(); var duplicates = 0;
        foreach (var input in inputs)
        {
            AnalyticsRules.Entry(input);
            AnalyticsRules.Require(owned || !input.EconomicKey.StartsWith("owned:", StringComparison.OrdinalIgnoreCase), "Owned cost keys are reserved for authoritative capture.");
            var fingerprint = Hash(input);
            var old = db.AnalyticsEntries.Local.FirstOrDefault(x => x.TenantId == s.TenantId && x.UniverseId == u && (x.Kind == input.Kind && x.EconomicKey == input.EconomicKey || x.SourceSystem == sourceConfig.SourceSystem && x.AccountReference == sourceConfig.AccountReference && x.EventId == input.EventId))
                ?? await db.AnalyticsEntries.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && (x.Kind == input.Kind && x.EconomicKey == input.EconomicKey || x.SourceSystem == sourceConfig.SourceSystem && x.AccountReference == sourceConfig.AccountReference && x.EventId == input.EventId), ct);
            if (old != null)
            {
                if (old.Fingerprint != fingerprint || old.SourceSystem != sourceConfig.SourceSystem || old.AccountReference != sourceConfig.AccountReference) throw new RuntimeConflictException("Duplicate economic/event identity differs; no silent historical rewrite.");
                ids.Add(old.Id); duplicates++; continue;
            }
            if (input.ProductionId != null)
            {
                var production = await db.WatchProductions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ProductionId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                AnalyticsRules.Require(input.RevisionId == production.CoreRevisionId, "Production cost must match the exact script revision.");
            }
            if (input.Kind == "Metric" && input.PublicationId != null)
            {
                var publication = await db.SocialPublications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.PublicationId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                var binding = Read<SocialPackageSnapshot>(publication.SnapshotJson);
                AnalyticsRules.Require(sourceConfig.SourceSystem == binding.Account.Platform && sourceConfig.AccountReference == binding.Account.AccountReference, "Platform metrics must match the original publication platform/account.");
            }
            var content = await Content(s, u, input.RevisionId, input.PublicationId, input.Campaign, ct); if (content != null) content = content with { DiscoverySources = [input.DiscoverySource] };
            if (input.ConsentId != null)
            {
                var consent = await db.AnalyticsConsents.SingleOrDefaultAsync(x => x.Id == input.ConsentId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                AnalyticsRules.Require(consent.Enabled && consent.ExpiresUtc > DateTimeOffset.UtcNow && input.OccurredUtc >= consent.CreatedUtc, "Current consent must precede the activity.");
                AnalyticsRules.Require(consent.Fixture == sourceConfig.Adapter.StartsWith("analytics.fixture."), "Fixture versus genuine consent and source mode must agree.");
            }
            if (input.DefinitionId != null)
            {
                var definition = await db.AnalyticsDefinitions.SingleOrDefaultAsync(x => x.Id == input.DefinitionId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                var aggregation = Read<AnalyticsDefinitionRequest>(definition.SnapshotJson).Aggregation;
                var metrics = await db.AnalyticsEntries.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.DefinitionId == input.DefinitionId && x.SourceSystem == sourceConfig.SourceSystem && x.AccountReference == sourceConfig.AccountReference && x.RevisionId == input.RevisionId && x.PublicationId == input.PublicationId).ToArrayAsync(ct);
                foreach (var existing in metrics.Concat(db.AnalyticsEntries.Local.Where(x => x.StateIsMetric(input, sourceConfig, s, u))).DistinctBy(x => x.Id))
                {
                    var previous = Read<AnalyticsEntryInput>(existing.SnapshotJson);
                    if (previous.Campaign != input.Campaign) continue;
                    var overlap = previous.WindowStartUtc < input.WindowEndUtc && input.WindowStartUtc < previous.WindowEndUtc;
                    AnalyticsRules.Require(!overlap || aggregation != "Delta" && previous.WindowStartUtc == input.WindowStartUtc && previous.WindowEndUtc == input.WindowEndUtc, "Overlapping metric windows would double count; append observations of an identical snapshot window or disjoint delta windows.");
                    if (previous.WindowStartUtc == input.WindowStartUtc && previous.WindowEndUtc == input.WindowEndUtc && previous.ObservedUtc == input.ObservedUtc) throw new RuntimeConflictException("One immutable observation per series/window/time.");
                }
            }
            decimal? reporting = null; string? reportingCurrency = null;
            if (input.Kind is "Revenue" or "Refund" or "Cost" or "Settlement")
            {
                if (input.FxId != null)
                {
                    var fx = await db.AnalyticsRates.SingleOrDefaultAsync(x => x.Id == input.FxId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                    var rate = Read<AnalyticsFxRequest>(fx.SnapshotJson);
                    AnalyticsRules.Require(rate.FromCurrency == input.Currency && rate.EffectiveUtc <= input.OccurredUtc, "FX source currency/effective evidence must match actual event.");
                    reporting = decimal.Round(AnalyticsRules.FinancialAmount(input) * rate.Rate, 8); reportingCurrency = rate.ToCurrency;
                }
                else { reporting = AnalyticsRules.FinancialAmount(input); reportingCurrency = input.Currency; }
            }
            if (input.Kind is "Settlement" or "Refund")
            {
                var original = db.AnalyticsEntries.Local.FirstOrDefault(x => x.Id == input.RelatedEntryId && x.TenantId == s.TenantId && x.UniverseId == u)
                    ?? await db.AnalyticsEntries.SingleOrDefaultAsync(x => x.Id == input.RelatedEntryId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                var revenue = Read<AnalyticsEntryInput>(original.SnapshotJson);
                AnalyticsRules.Require(revenue.Kind == "Revenue" && revenue.FinancialState == "Earned" && revenue.Currency == input.Currency && input.OccurredUtc >= revenue.OccurredUtc && input.FxId == revenue.FxId, "Settlement must reconcile original earned currency and FX binding.");
                var all = await db.AnalyticsEntries.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.RelatedEntryId == original.Id).ToArrayAsync(ct);
                var payments = all.Concat(db.AnalyticsEntries.Local.Where(x => x.RelatedEntryId == original.Id)).DistinctBy(x => x.Id).Select(x => Read<AnalyticsEntryInput>(x.SnapshotJson)).ToArray();
                if (input.Kind == "Settlement") AnalyticsRules.Require(payments.Where(x => x.Kind == "Settlement").All(x => x.SettlementReference != input.SettlementReference), "Settlement reference already reconciled.");
                if (input.Kind == "Settlement") AnalyticsRules.Require(payments.Where(x => x.Kind == "Settlement").Sum(x => x.Amount) + input.Amount <= AnalyticsRules.Net(revenue) - payments.Where(x => x.Kind == "Refund").Sum(x => x.Amount), "Settlement exceeds recognized net receipts.");
                else AnalyticsRules.Require(input.RevenueStream == revenue.RevenueStream && payments.Where(x => x.Kind == "Refund").Sum(x => x.Amount) + input.Amount <= revenue.Gross - revenue.Tax - revenue.PassThrough - revenue.Refunds, "Refund exceeds original refundable amount or changes its stream.");
                AnalyticsRules.Require(input.RevisionId == revenue.RevisionId && input.PublicationId == revenue.PublicationId && input.Campaign == revenue.Campaign && input.AcquisitionChannel == revenue.AcquisitionChannel, "Settlement keeps revenue dimensions.");
            }
            var row = new AnalyticsEntry { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, SourceId = source.Id, SourceSystem = sourceConfig.SourceSystem, AccountReference = sourceConfig.AccountReference, EventId = input.EventId, EconomicKey = input.EconomicKey, Kind = input.Kind, OccurredUtc = input.OccurredUtc, Fingerprint = fingerprint, SnapshotJson = Json(input), ContentJson = content == null ? null : Json(content), ReportingAmount = reporting, ReportingCurrency = reportingCurrency, RevisionId = input.RevisionId, PublicationId = input.PublicationId, DefinitionId = input.DefinitionId, FxId = input.FxId, ConsentId = input.ConsentId, RelatedEntryId = input.RelatedEntryId };
            db.AnalyticsEntries.Add(row); ids.Add(row.Id);
        }
        return new(ids.ToArray(), ids.Count - duplicates, duplicates, sourceConfig.Adapter);
    }
    private async Task<AnalyticsContentSnapshot?> Content(CoreScope s, Guid u, Guid? revision, Guid? publication, string campaign, CancellationToken ct)
    {
        if (publication != null)
        {
            var p = await db.SocialPublications.SingleOrDefaultAsync(x => x.Id == publication && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
            var snap = Read<SocialPackageSnapshot>(p.SnapshotJson);
            AnalyticsRules.Require(revision == snap.Payload.RevisionId && campaign == snap.Payload.CampaignId, "Publication must match exact revision and campaign.");
        }
        if (revision == null) return null;
        var pair = await (from r in db.Revisions.AsNoTracking() join c in db.Contents.AsNoTracking() on r.ContentId equals c.Id where r.Id == revision && c.TenantId == s.TenantId && c.UniverseId == u select new { r, c }).SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
        var briefs = await (from binding in db.TrendBindings join b in db.TrendStoryBriefs on binding.BriefId equals b.Id where binding.TenantId == s.TenantId && binding.UniverseId == u && binding.ContentRevisionId == revision select b).ToArrayAsync(ct);
        var chars = await db.Cast.Where(x => x.ContentRevisionId == revision).Select(x => x.CharacterRevisionId).ToArrayAsync(ct);
        var profileIds = new List<Guid>();
        var storyRows = await db.RegistryRevisions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == "Story").ToArrayAsync(ct);
        foreach (var story in storyRows.Select(x => Read<RegistryEpisodeSnapshot>(x.RulesJson)).Where(x => x.ContentRevisionId == revision))
        {
            chars = chars.Concat(story.Cast.Select(x => x.Character.CoreRevisionId)).ToArray();
            profileIds.AddRange(story.Cast.Select(x => x.Character.Profile.Id));
        }
        var factory = await db.FactoryArtifacts.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.CoreRevisionId == revision).ToArrayAsync(ct);
        foreach (var artifact in factory)
        {
            var snapshot = Read<FactoryArtifactSnapshot>(artifact.SnapshotJson);
            chars = chars.Concat(snapshot.Input.Cast.Cast.Select(x => x.Character.CoreRevisionId)).ToArray();
            profileIds.AddRange(snapshot.Input.Cast.Cast.Select(x => x.Character.Profile.Id));
        }
        var sourceRevisions = new List<Guid>(); var briefIds = briefs.Select(x => x.Id).ToList(); var clusterIds = briefs.Select(x => x.ClusterId).ToList();
        var productions = await db.WatchProductions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.CoreRevisionId == revision).ToArrayAsync(ct);
        foreach (var production in productions)
        {
            var snapshot = Read<WatchSnapshot>(production.SnapshotJson);
            sourceRevisions.Add(snapshot.Script.RevisionId);
            chars = chars.Concat(snapshot.Cast.Select(x => x.Character.CoreRevisionId)).ToArray();
            profileIds.AddRange(snapshot.Cast.Select(x => x.Character.Profile.Id));
            if (snapshot.Request.TrendBriefId != null) briefIds.Add(snapshot.Request.TrendBriefId.Value);
            if (snapshot.Trend != null) clusterIds.Add(snapshot.Trend.ClusterId);
        }
        var format = await db.EditorialItems.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.ContentId == pair.c.Id).Select(x => x.Format).FirstOrDefaultAsync(ct) ?? pair.r.Kind;
        return new(pair.r.Id, pair.c.Id, pair.c.AudienceId, pair.c.LocaleId, pair.r.Kind, productions.Length == 0 ? format : "WatchEpisode", briefIds.Distinct().ToArray(), clusterIds.Distinct().ToArray(), chars.Distinct().ToArray(), [], profileIds.Distinct().ToArray(), sourceRevisions.Distinct().ToArray());
    }
    public async Task<AnalyticsEntryView[]> EntriesAsync(CoreScope s, Guid u, CancellationToken ct, int skip = 0, int take = 100, DateTimeOffset? asOfUtc = null)
    {
        await Access(s, u, true, ct); AnalyticsRules.Require(skip is >= 0 and <= 1000000 && take is >= 1 and <= 1000, "Bounded ledger pagination required."); var asOf = asOfUtc ?? DateTimeOffset.UtcNow; AnalyticsRules.Require(asOf <= DateTimeOffset.UtcNow.AddMinutes(1), "Actual ledger as-of required."); return (await db.AnalyticsEntries.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.CreatedUtc <= asOf).OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).Skip(skip).Take(take).ToArrayAsync(ct)).Select(View).ToArray();
    }
    private static AnalyticsEntryView View(AnalyticsEntry x) => new(x.Id, x.SourceId, Read<AnalyticsEntryInput>(x.SnapshotJson), x.ContentJson == null ? null : Read<AnalyticsContentSnapshot>(x.ContentJson), x.ReportingAmount, x.ReportingCurrency, x.Fingerprint, x.CreatedUtc);
    public Task<Guid> ForecastAsync(CoreScope s, Guid u, AnalyticsForecastRequest r, CancellationToken ct) => Command(s, u, r.OperationId, "forecast", r, async () =>
    {
        AnalyticsRules.Forecast(r);
        if (await db.AnalyticsForecasts.AnyAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key && x.Version >= r.Version, ct)) throw new RuntimeConflictException("Append a higher forecast scenario version.");
        var row = new AnalyticsForecast { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r) }; db.AnalyticsForecasts.Add(row); return row.Id;
    }, ct);
}
internal static class AnalyticsEntryMatching
{
    internal static bool StateIsMetric(this AnalyticsEntry x, AnalyticsEntryInput i, AnalyticsSourceRequest source, CoreScope s, Guid u) => x.TenantId == s.TenantId && x.UniverseId == u && x.Kind == "Metric" && x.DefinitionId == i.DefinitionId && x.SourceSystem == source.SourceSystem && x.AccountReference == source.AccountReference && x.RevisionId == i.RevisionId && x.PublicationId == i.PublicationId;
}
public static class AnalyticsRegistration { public static IServiceCollection AddAnalyticsWorkbench(this IServiceCollection s) => s.AddScoped<IAnalyticsWorkbench, AnalyticsWorkbench>(); }
