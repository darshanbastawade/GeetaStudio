using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class AnalyticsWorkbench
{
    public async Task<AnalyticsReport> ReportAsync(CoreScope s, Guid u, AnalyticsReportQuery r, CancellationToken ct)
    {
        await Access(s, u, false, ct); AnalyticsRules.Currency(r.Currency); var asOf = r.AsOfUtc ?? DateTimeOffset.UtcNow;
        AnalyticsRules.Require(r.EndsUtc > r.StartsUtc && r.EndsUtc - r.StartsUtc <= TimeSpan.FromDays(3660) && asOf <= DateTimeOffset.UtcNow.AddMinutes(1), "Bounded range and actual as-of required.");
        var query = db.AnalyticsEntries.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.CreatedUtc <= asOf && x.OccurredUtc >= r.StartsUtc && x.OccurredUtc < r.EndsUtc && x.OccurredUtc <= asOf);
        AnalyticsRules.Require(await query.CountAsync(ct) <= 10000, "Narrow report period: maximum 10000 operational entries.");
        var entries = (await query.ToArrayAsync(ct)).Select(View).ToArray();
        decimal? Money(AnalyticsEntryView e) => e.Event.Currency == r.Currency ? AnalyticsRules.FinancialAmount(e.Event) : e.ReportingCurrency == r.Currency ? e.ReportingAmount : null;
        var financial = entries.Where(x => x.Event.Kind is "Revenue" or "Refund" or "Cost" or "Settlement").ToArray();
        var usable = financial.Where(x => Money(x) != null).ToArray(); var missing = financial.Length - usable.Length;
        decimal Revenue(IEnumerable<AnalyticsEntryView> xs, string state) => xs.Where(x => (x.Event.Kind == "Revenue" || x.Event.Kind == "Refund") && x.Event.FinancialState == state).Sum(x => Money(x) ?? 0);
        decimal Costs(IEnumerable<AnalyticsEntryView> xs) => xs.Where(x => x.Event.Kind == "Cost" && x.Event.FinancialState == "Earned").Sum(x => Money(x) ?? 0);
        decimal Settled(IEnumerable<AnalyticsEntryView> xs) => xs.Where(x => x.Event.Kind == "Settlement").Sum(x => Money(x) ?? 0);
        var groups = new List<AnalyticsGroup>();
        void Group(string dimension, Func<AnalyticsEntryView, string> key) { foreach (var g in usable.GroupBy(key)) groups.Add(new(dimension, g.Key, Revenue(g, "Earned"), Revenue(g, "Estimated"), Costs(g), Settled(g), g.Count())); }
        Group("RevenueStream", e => e.Event.Kind is "Revenue" or "Refund" ? e.Event.RevenueStream : e.Event.Kind == "Settlement" ? "SettlementReconciliation" : "Costs");
        Group("AcquisitionChannel", e => e.Event.AcquisitionChannel);
        Group("Campaign", e => e.Event.Campaign == "" ? "Unknown" : e.Event.Campaign);
        Group("CostCategory", e => e.Event.Kind == "Cost" ? e.Event.CostCategory : "NonCost");
        var definitions = await db.AnalyticsDefinitions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToDictionaryAsync(x => x.Id, x => x.SnapshotJson, ct);
        var sources = await db.AnalyticsSources.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToDictionaryAsync(x => x.Id, x => x.SnapshotJson, ct);
        var metrics = new List<AnalyticsMetric>();
        foreach (var g in entries.Where(x => x.Event.Kind == "Metric").GroupBy(x => new { x.Event.DefinitionId, Source = Read<AnalyticsSourceRequest>(sources[x.SourceId]).SourceSystem, Account = Read<AnalyticsSourceRequest>(sources[x.SourceId]).AccountReference, x.Event.RevisionId, x.Event.PublicationId, x.Event.Campaign }))
        {
            var d = Read<AnalyticsDefinitionRequest>(definitions[g.Key.DefinitionId!.Value]);
            var windows = g.Where(x => x.Event.ObservedUtc <= asOf).GroupBy(x => new { x.Event.WindowStartUtc, x.Event.WindowEndUtc }).Select(x => x.OrderByDescending(z => z.Event.ObservedUtc).First()).ToArray();
            var available = windows.Where(x => x.Event.Coverage != "Unavailable").ToArray();
            decimal? value = available.Length == 0 ? null : d.Aggregation == "Gauge" ? available.OrderByDescending(x => x.Event.ObservedUtc).First().Event.MetricValue : available.Sum(x => x.Event.MetricValue);
            var coverage = windows.Length == 0 ? "UnavailableAsOf" : windows.Any(x => x.Event.Coverage != "Available" || x.Event.WindowStartUtc < r.StartsUtc || x.Event.WindowEndUtc > r.EndsUtc) ? "PartialOrUnavailable" : "ReportedWindowsOnly";
            metrics.Add(new(d.Key, d.Version, d.Unit, d.Aggregation, g.Key.Source, g.Key.Account, g.Key.RevisionId, g.Key.PublicationId, g.Key.Campaign, value, available.Length, windows.Count(x => x.Event.Coverage == "Unavailable"), coverage));
        }
        var variants = new List<AnalyticsVariant>();
        var published = await (from p in db.SocialPublications.AsNoTracking() join h in db.SocialHistory.AsNoTracking() on p.Id equals h.PublicationId where p.TenantId == s.TenantId && p.UniverseId == u && h.Kind == "Receipt" && h.CreatedUtc <= asOf select new { p.SnapshotJson, h.CreatedUtc }).ToArrayAsync(ct);
        foreach (var g in entries.Where(x => x.Content != null).GroupBy(x => x.Content!.RevisionId))
        {
            var content = g.First().Content!;
            var approved = await db.EditorialApprovals.AnyAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.RevisionId == g.Key && x.CreatedUtc <= asOf, ct);
            var revisionVersion = await db.Revisions.Where(x => x.Id == g.Key).Select(x => x.Version).SingleAsync(ct);
            var current = await db.Contents.AnyAsync(x => x.Id == content.ContentId && x.TenantId == s.TenantId && x.UniverseId == u && x.EditorialState == "Approved" && x.CurrentVersion == revisionVersion, ct);
            var corrected = await db.EditorialCorrectionTasks.AnyAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.RevisionId == g.Key && x.CreatedUtc <= asOf, ct);
            var first = published.Where(x => Read<SocialPackageSnapshot>(x.SnapshotJson).Payload.RevisionId == g.Key).OrderBy(x => x.CreatedUtc).FirstOrDefault();
            var created = await db.Revisions.Where(x => x.Id == g.Key).Select(x => x.CreatedUtc).SingleAsync(ct);
            decimal Category(string category) => g.Where(x => x.Event.Kind == "Cost" && x.Event.CostCategory == category && x.Event.FinancialState == "Earned").Sum(x => Money(x) ?? 0);
            var production = Category("Production") + Category("AiTool") + Category("MediaSpend"); var review = Category("Review"); var rework = Category("Rework"); var overhead = Category("OperatingOverhead");
            var cost = production + review + rework; var missingCost = g.Any(x => x.Event.Kind == "Cost" && Money(x) == null);
            variants.Add(new(g.Key, content with { DiscoverySources = g.Select(x => x.Event.DiscoverySource).Distinct().ToArray() }, approved ? 1 : 0,
                !approved || missingCost || !g.Any(x => x.Event.Kind == "Cost" && x.Event.FinancialState == "Earned") ? null : cost, production, review, rework, overhead,
                missingCost || g.Any(x => (x.Event.Kind == "Revenue" || x.Event.Kind == "Refund") && Money(x) == null) || !g.Any(x => x.Event.Kind == "Revenue" && x.Event.FinancialState == "Earned") ? null : Revenue(g, "Earned") - cost,
                first == null ? null : (first.CreatedUtc - created).TotalSeconds, corrected, current, null, "Unavailable until five currently consented subject feedback records; see consented cohort report"));
        }
        var (cohorts, suppressed) = await Cohorts(s, u, entries, asOf, ct);
        var (sessions, feedback) = await SessionFeedback(s, u, entries, r.Currency, ct);
        variants = variants.Select(v => feedback.TryGetValue(v.RevisionId, out var mean) ? v with { LearningFeedbackMean = mean, LearningCoverage = "Observed consented feedback, five-subject minimum; no causal learning claim" } : v).ToList(); var unresolved = await Unresolved(s, u, ct);
        var gaps = new List<string> { "Operational ledger, not audited accounting or guaranteed earnings.", "Estimated revenue and settlements are never added to recognized revenue.", "Reconciliation difference compares recognition and settlement events in the selected period, not an invoice outstanding balance.", "Unknown acquisition is not inferred.", "Metrics cover reported windows only; definition versions/accounts are separate.", "Distinct historically approved revision denominators; current approval is separately labelled. Overhead shown separately.", "Cohorts use current consent, even for historical queries, and disclose no subject references." };
        var fixtureEvents = entries.Count(x => Read<AnalyticsSourceRequest>(sources[x.SourceId]).Adapter.StartsWith("analytics.fixture."));
        if (fixtureEvents > 0) gaps.Add($"This report contains {fixtureEvents} synthetic fixture-source events; its totals are demonstration evidence, not verified earnings.");
        if (entries.Any(x => x.Event.EconomicKey.StartsWith("owned:", StringComparison.OrdinalIgnoreCase))) gaps.Add("Captured charges retain original local/test/provider qualification; an operational charge record is not a qualified vendor invoice.");
        if (missing > 0) gaps.Add($"{missing} entries lack pinned FX to {r.Currency}; totals are incomplete.");
        if (entries.All(x => x.Event.Kind != "Metric")) gaps.Add("Engagement unavailable: no metrics imported.");
        if (cohorts.Length == 0) gaps.Add("Funnel/retention/learning unavailable: no eligible consented activity.");
        if (usable.Any(x => x.Event.Kind == "Cost" && x.Event.FinancialState == "Estimated")) gaps.Add("Estimated costs excluded from actual costs; inspect ledger.");
        if (unresolved.Length > 0) gaps.Add("Unresolved/uncaptured owned costs: contribution is partial.");
        var episodeCosts = new List<AnalyticsEpisodeCost>();
        foreach (var g in entries.Where(x => x.Event.Kind == "Cost" && x.Event.FinancialState == "Earned" && x.Event.ProductionId != null).GroupBy(x => x.Event.ProductionId!.Value))
        {
            var production = await db.WatchProductions.AsNoTracking().SingleAsync(x => x.Id == g.Key && x.TenantId == s.TenantId && x.UniverseId == u, ct);
            var approved = production.State == "Approved" && production.ReviewJson != null;
            var reviewMode = production.ReviewJson == null ? "Pending" : Read<WatchReview>(production.ReviewJson).Mode;
            var incomplete = g.Any(x => Money(x) == null);
            episodeCosts.Add(new(g.Key, production.CoreRevisionId, approved, !approved || incomplete ? null : g.Sum(x => Money(x)),
                "Current " + reviewMode + " master review; selected-period directly allocated costs only; script approval alone cannot approve an episode. Missing review/rework/overhead allocation remains explicit."));
        }
        var recognized = Revenue(usable, "Earned"); var settled = Settled(usable);
        return new(r.Currency, asOf, r.StartsUtc, r.EndsUtc, recognized, Revenue(usable, "Estimated"), Costs(usable), settled, recognized - settled, groups.ToArray(), metrics.ToArray(), variants.ToArray(), cohorts, gaps.ToArray(), missing, suppressed, variants.Count == 0 ? null : (decimal)variants.Count(x => x.Corrected) / variants.Count, unresolved, sessions, episodeCosts.ToArray());
    }
    private async Task<(AnalyticsCohort[], int)> Cohorts(CoreScope s, Guid u, AnalyticsEntryView[] entries, DateTimeOffset asOf, CancellationToken ct)
    {
        var consents = await db.AnalyticsConsents.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToDictionaryAsync(x => x.Id, ct);
        var activities = entries.Where(x => x.Event.Kind == "Activity").ToArray();
        var permitted = activities.Where(x => consents.TryGetValue(x.Event.ConsentId!.Value, out var c) && c.Enabled && c.ExpiresUtc > DateTimeOffset.UtcNow).ToArray();
        var result = new List<AnalyticsCohort>();
        // Cohort membership follows first acquired event, not the month of each later return.
        var subjects = permitted.GroupBy(x => x.Event.ConsentId).Where(x => x.Any(y => y.Event.Activity == "Acquired")).ToArray();
        foreach (var g in subjects.GroupBy(x => (consents[x.Key!.Value].Fixture ? "Fixture" : "Consented") + ":" + x.Where(y => y.Event.Activity == "Acquired").Min(y => y.Event.OccurredUtc).ToUniversalTime().ToString("yyyy-MM") + ":" + x.Where(y => y.Event.Activity == "Acquired").OrderBy(y => y.Event.OccurredUtc).First().Event.AcquisitionChannel))
        {
            var members = g.ToArray(); if (members.Length < 5) { result.Add(new(g.Key, null, null, null, null, "Suppressed: fewer than five consented acquired subjects")); continue; }
            DateTimeOffset Acquired(IEnumerable<AnalyticsEntryView> x) => x.Where(y => y.Event.Activity == "Acquired").Min(y => y.Event.OccurredUtc);
            var converted = members.Count(x => x.Any(y => y.Event.Activity == "Converted" && y.Event.OccurredUtc >= Acquired(x)));
            var eligible = members.Where(x => Acquired(x).AddDays(30) <= asOf).ToArray();
            var retained = eligible.Count(x => x.Any(y => y.Event.Activity == "Returned" && y.Event.OccurredUtc >= Acquired(x).AddDays(30)));
            var learning = members.SelectMany(x => x.Where(y => y.Event.Activity == "Learning" && y.Event.OccurredUtc >= Acquired(x))).GroupBy(x => x.Event.ConsentId).Select(x => x.Average(y => y.Event.LearningScore!.Value)).ToArray();
            result.Add(new(g.Key, members.Length, (decimal)converted / members.Length, eligible.Length < 5 ? null : (decimal)retained / eligible.Length,
                learning.Length < 5 ? null : learning.Average(), "Consented observed activities only; 30-day return. No causal learning/personality inference."));
        }
        return (result.ToArray(), activities.Length - permitted.Length);
    }
    private async Task<(AnalyticsSessionCost[], Dictionary<Guid, decimal>)> SessionFeedback(CoreScope s, Guid u, AnalyticsEntryView[] entries, string currency, CancellationToken ct)
    {
        var ids = await db.AnalyticsConsents.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Enabled && x.ExpiresUtc > DateTimeOffset.UtcNow).ToDictionaryAsync(x => x.Id, ct);
        var valid = entries.Where(x => x.Event.ConsentId != null && ids.ContainsKey(x.Event.ConsentId.Value)).ToArray();
        var feedback = new Dictionary<Guid, decimal>();
        foreach (var g in valid.Where(x => x.Event.Activity == "Learning" && x.Event.RevisionId != null).GroupBy(x => x.Event.RevisionId!.Value))
        {
            var subjects = g.GroupBy(x => x.Event.ConsentId).Select(x => x.Average(y => y.Event.LearningScore!.Value)).ToArray();
            if (subjects.Length >= 5) feedback[g.Key] = subjects.Average();
        }
        var sessions = new List<AnalyticsSessionCost>();
        foreach (var g in valid.Where(x => x.Event.SessionReference != "").GroupBy(x => ids[x.Event.ConsentId!.Value].Fixture ? "Fixture" : "Consented"))
        {
            var observed = g.Where(x => x.Event.Kind == "Activity").GroupBy(x => new { x.Event.ConsentId, x.Event.SessionReference }).ToArray();
            var subjects = observed.Select(x => x.Key.ConsentId).Distinct().Count();
            if (subjects < 5) { sessions.Add(new(g.Key, null, null, "Suppressed: fewer than five consented session subjects")); continue; }
            decimal total = 0; var hasMissing = false; var costSessions = new HashSet<string>();
            foreach (var x in g.Where(x => x.Event.Kind == "Cost" && x.Event.FinancialState == "Earned"))
            {
                if (!observed.Any(y => y.Key.ConsentId == x.Event.ConsentId && y.Key.SessionReference == x.Event.SessionReference)) continue;
                var cost = x.Event.Currency == currency ? x.Event.Amount : x.ReportingCurrency == currency ? x.ReportingAmount : null;
                if (cost == null) hasMissing = true; else { total += cost.Value; costSessions.Add(x.Event.ConsentId + ":" + x.Event.SessionReference); }
            }
            var complete = costSessions.Count == observed.Length && !hasMissing;
            sessions.Add(new(g.Key, observed.Length, complete ? total / observed.Length : null, complete ? "Explicit session costs only; overhead not allocated" : "Unavailable/partial: missing cost or FX for observed sessions"));
        }
        return (sessions.ToArray(), feedback);
    }
    public async Task<AnalyticsForecastView[]> ForecastsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var rows = await db.AnalyticsForecasts.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderBy(x => x.Key).ThenBy(x => x.Version).ToArrayAsync(ct);
        var result = new List<AnalyticsForecastView>();
        foreach (var row in rows)
        {
            var a = Read<AnalyticsForecastRequest>(row.SnapshotJson);
            var query = db.AnalyticsEntries.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.OccurredUtc >= a.StartsUtc && x.OccurredUtc < a.EndsUtc);
            AnalyticsRules.Require(await query.CountAsync(ct) <= 10000, "Forecast actual comparison exceeds operational limit.");
            var raw = (await query.ToArrayAsync(ct)).Select(View); decimal actualRevenue = 0, actualCost = 0; var known = 0; var missing = 0;
            foreach (var e in raw.Where(x => x.Event.FinancialState == "Earned" && x.Event.AcquisitionChannel == a.AcquisitionChannel && ((x.Event.Kind == "Revenue" || x.Event.Kind == "Refund") && x.Event.RevenueStream == a.RevenueStream || x.Event.Kind == "Cost")))
            {
                var amount = e.Event.Currency == a.Currency ? AnalyticsRules.FinancialAmount(e.Event) : e.ReportingCurrency == a.Currency ? e.ReportingAmount : null;
                if (amount == null) { missing++; continue; }
                known++; if (e.Event.Kind is "Revenue" or "Refund") actualRevenue += amount.Value; else actualCost += amount.Value;
            }
            var gross = AnalyticsRules.ForecastGross(a); var cost = (a.MediaSpend + a.AiToolCost + a.OperatingOverhead + a.ReviewCost) * a.Periods;
            result.Add(new(row.Id, a, gross, cost, gross - cost, decimal.Round(gross * a.LowMultiplier, 8), decimal.Round(gross * a.HighMultiplier, 8), actualRevenue, actualCost,
                known == 0 ? "Unavailable: no matching actual entries" : missing > 0 ? "Partial: missing FX" : "Recorded only; cost shared by acquisition channel, not allocated across revenue streams",
                "Scenario projection, never guaranteed earnings. Gross forecast versus net actual retains deduction differences. Assumptions/eligibility/version are explicit."));
        }
        return result.ToArray();
    }
}
