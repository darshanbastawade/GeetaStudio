using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class OptimizationWorkbench
{
    public async Task<OptimizationOperationalReport> ReportAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var now = DateTimeOffset.UtcNow;
        var items = await db.EditorialItems.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToArrayAsync(ct);
        var correctionCount = await db.EditorialCorrectionTasks.CountAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.State != "Resolved", ct);
        var rework = await db.EditorialHistories.CountAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Action == "Edit", ct);
        var runs = await db.AiRuns.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToArrayAsync(ct);
        var measured = new List<string> { $"Editorial items: {items.Length}; open correction tasks: {correctionCount}; edits/rework events: {rework}. Counts are not interchangeable with rates.", $"AI runs: {runs.Length}; failed/unknown outcomes: {runs.Count(x => x.Outcome != "" && x.Outcome != "Success")}; pending usage: {runs.Count(x => x.Outcome == "UnknownUsage")}." };
        var ledger = await analytics.ReportAsync(s, u, new("INR", now.AddDays(-30), now, now), ct);
        measured.Add($"M19 30-day correction rate: {ledger.CorrectionRate?.ToString() ?? "Unavailable"}; rework cost INR: {ledger.Variants.Sum(x => x.ReworkCost)}. Current consent/source coverage remains authoritative.");
        foreach (var cohort in ledger.Cohorts) measured.Add($"M19 cohort {cohort.Cohort}: conversion {cohort.Conversion?.ToString() ?? "Unavailable"}; retention {cohort.Retention?.ToString() ?? "Unavailable"}; learning feedback {cohort.LearningMean?.ToString() ?? "Unavailable"}; coverage {cohort.Coverage}. Feedback/quiz scores are not proven learning effectiveness.");
        foreach (var metric in ledger.Metrics) measured.Add($"M19 metric {metric.Key} v{metric.Version}: {metric.Value?.ToString() ?? "Unavailable"} {metric.Unit}; coverage {metric.Coverage}.");
        measured.Add($"Scoped failed workflows: {await db.DurableWorkflows.CountAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.State == "Failed", ct)}. Triage reports do not retry ambiguous side effects.");
        foreach (var group in runs.GroupBy(x => Read<AiExecutionSnapshot>(x.SnapshotJson).Route.Currency)) measured.Add($"AI recorded cost {group.Key}: {group.Sum(x => x.Spent)}; reservations: {group.Sum(x => x.Reserved)}. No cross-currency total.");
        var experiments = await db.OptimizationExperiments.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).ToArrayAsync(ct);
        var ids = experiments.Select(x => x.Id).ToArray(); var events = await db.OptimizationEvents.AsNoTracking().Where(x => ids.Contains(x.ExperimentId) && x.Kind == "measure").ToArrayAsync(ct);
        var observations = events.Select(x => Read<OptimizationMeasurement>(x.EvidenceJson)).ToArray();
        foreach (var m in observations) measured.Add($"Fixture {m.Metric}: {m.Value}; samples {m.Samples}; observed {m.ObservedUtc:O}; evidence {m.EvidenceReference}. Synthetic, not real learning effectiveness.");
        var permissions = await (from p in db.SourcePermissions.AsNoTracking() join e in db.SourceEditions on p.EditionId equals e.Id join w in db.SourceWorks on e.WorkId equals w.Id where w.TenantId == s.TenantId && w.UniverseId == u && e.PermissionId == p.Id select p).ToArrayAsync(ct);
        var currentPermissions = permissions.GroupBy(x => x.EditionId).Select(x => x.OrderByDescending(p => p.Version).First()).ToArray();
        var expired = currentPermissions.Count(x => x.ExpiresUtc <= now); var due = currentPermissions.Count(x => x.ExpiresUtc > now && x.ExpiresUtc <= now.AddDays(30));
        var revoked = await db.SourceWorks.CountAsync(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Revoked, ct);
        measured.Add($"Current permission versions checked: {currentPermissions.Length}; expired {expired}; due within 30 days {due}; revoked works {revoked}.");
        var recommendations = new List<string> { "Provider health/cost review uses recorded outcomes and same-currency spend; recommendations require adapter qualification and an explicit resource migration plan.", "Dependency/API monitoring: compare the pinned lockfiles and adapter documentation against official releases before human adoption; no unattended code or provider changes." };
        if (expired + due + revoked > 0) recommendations.Add("Source/rights alert: review affected lineage before generation/export; current boundary guards remain authoritative.");
        var reviews = await db.OptimizationAutomationRuns.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToArrayAsync(ct);
        var dependencies = reviews.SelectMany(x => Read<OptimizationOperationalReport>(x.ResultJson).DependencyChecks).OrderByDescending(x => x.ObservedUtc).DistinctBy(x => x.Key).ToArray();
        if (dependencies.Length == 0) recommendations.Add("Dependency/API review unavailable: human official-documentation review required.");
        foreach (var dependency in dependencies)
            recommendations.Add($"Fixture dependency/API {dependency.Key}: pinned {dependency.PinnedVersion}, observed {dependency.ObservedVersion}; {(dependency.ReviewDueUtc <= now ? "OVERDUE" : "review scheduled")}; {(dependency.PinnedVersion != dependency.ObservedVersion ? "version change requires human compatibility/qualification review" : "same recorded version; no live freshness claim")}; {dependency.OfficialDocumentation}.");
        var unavailable = OptimizationRules.Metrics.Where(m => !observations.Any(x => x.Metric == m)).Select(m => m + ": no experiment samples recorded").Concat(ledger.Gaps).ToArray();
        return new(now, measured.ToArray(), experiments.Select(x => Read<OptimizationProposal>(x.ProposalJson).Hypothesis).ToArray(), recommendations.ToArray(),
            ["High-risk trends, sensitive history/religion, child policy and materially changed content require renewed human editorial approval.", "M12 genuine human/held-out release acceptance remains pending; quiz scores, popularity and agent self-assessment do not establish learning effectiveness.", "Private fixture automation grants no publish, payment, credit transfer, subscription, or stateful migration authority."], unavailable)
        { DependencyChecks = dependencies };
    }
    public async Task<OptimizationOperationalReport> AutomateAsync(CoreScope s, Guid u, OptimizationAutomation r, CancellationToken ct)
    {
        await Access(s, u, true, ct); OptimizationRules.Evidence(r.EvidenceReference); OptimizationRules.Require(r.OperationId != Guid.Empty, "Stable automation operation required.");
        var old = await db.OptimizationAutomationRuns.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct);
        if (old != null) { if (old.UniverseId != u || old.Actor != s.Actor || old.Fingerprint != Hash(r)) throw new RuntimeConflictException("Automation replay differs."); return Read<OptimizationOperationalReport>(old.ResultJson); }
        OptimizationRules.Require(new[] { "CostReport", "RightsAlert", "FailedJobTriage", "ProviderReview", "DependencyReview", "ScheduleApproved" }.Contains(r.Kind), "Low-risk automation only; metrics cannot approve or publish content.");
        if (r.Kind == "DependencyReview")
        {
            OptimizationRules.Require(r.DependencyChecks is { Length: > 0 and <= 100 } && r.DependencyChecks.Select(x => x.Key).Distinct().Count() == r.DependencyChecks.Length, "Bounded unique dependency/API snapshots required.");
            foreach (var d in r.DependencyChecks!) OptimizationRules.Require(d.Key.Length is > 0 and <= 100 && d.Kind is "Package" or "Api" && d.PinnedVersion.Length is > 0 and <= 100 && d.ObservedVersion.Length is > 0 and <= 100 && d.OfficialDocumentation.Length <= 500 && Uri.TryCreate(d.OfficialDocumentation, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) && d.ObservedUtc <= DateTimeOffset.UtcNow && d.ReviewDueUtc > d.ObservedUtc && d.ReviewDueUtc <= d.ObservedUtc.AddDays(30), "Versioned official-documentation snapshot and a bounded review deadline required; URLs are data and are never executed.");
        }
        else OptimizationRules.Require(r.DependencyChecks == null, "Dependency checks belong only to dependency review.");
        if (r.Kind == "ScheduleApproved")
        {
            OptimizationRules.Require(r.EditorialItemId.HasValue && r.RevisionId.HasValue && r.ScheduledUtc.HasValue && r.ExpectedRowVersion != null, "Exact approved revision, row version and UTC schedule required.");
            var packet = await editorial.InspectAsync(s, u, r.EditorialItemId!.Value, ct);
            var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == packet.ContentId, ct); var audience = await db.Audiences.AsNoTracking().SingleAsync(x => x.Id == head.AudienceId, ct);
            OptimizationRules.Require(!packet.HighRisk && !packet.PublicIntended && audience.MinAge >= 18 && packet.Current?.Kind == "Fiction" && packet.SourceSnapshotIds.Length == 0 && packet.Claims.All(x => x.Label is "FictionalScene" or "Suggestion") && packet.RevisionId == r.RevisionId && packet.Approvals.Any(x => x.Current) && packet.Blocks.All(x => x is "PublicReleaseNotApproved" or "ExternalPublishingAdapterNotImplemented"), "Already approved private adult original fiction only; sensitive, child and materially changed content requires human approval.");
            await editorial.ActAsync(s, u, packet.Id, new(r.OperationId, r.ExpectedRowVersion!, "Schedule", r.EvidenceReference, ScheduledUtc: r.ScheduledUtc), ct);
        }
        var report = await ReportAsync(s, u, ct);
        if (r.Kind == "DependencyReview") report = report with { Measured = report.Measured.Append("DependencyReviewRecorded: operator supplied private fixture review; genuine official version/license review remains pending.").ToArray(), DependencyChecks = r.DependencyChecks! };
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct); await Lock(s, ct); await Access(s, u, true, ct);
        old = await db.OptimizationAutomationRuns.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct);
        if (old != null) { if (old.UniverseId != u || old.Actor != s.Actor || old.Fingerprint != Hash(r)) throw new RuntimeConflictException("Automation replay differs."); return Read<OptimizationOperationalReport>(old.ResultJson); }
        db.OptimizationAutomationRuns.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = r.OperationId, Actor = s.Actor, Fingerprint = Hash(r), ResultJson = Json(report) });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = u, Action = "optimization.automation." + r.Kind });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return report;
    }
    public async Task MonitorAsync(CancellationToken ct)
    {
        var rows = await db.OptimizationExperiments.AsNoTracking().Where(x => x.State == "Active").OrderBy(x => x.CreatedUtc).Take(10001).ToArrayAsync(ct);
        OptimizationRules.Require(rows.Length <= 10000, "Monitor capacity exceeded; operator partitioning is required.");
        foreach (var row in rows)
        {
            var s = new CoreScope(row.TenantId, row.Owner); var p = Read<OptimizationProposal>(row.ProposalJson);
            try
            {
                await Access(s, row.UniverseId, true, ct);
                var invalid = false;
                if (p.EndsUtc > DateTimeOffset.UtcNow)
                {
                    var evidence = Read<OptimizationEvaluation>((await db.OptimizationEvents.AsNoTracking().SingleAsync(x => x.ExperimentId == row.Id && x.Kind == "evaluate", ct)).EvidenceJson);
                    try { await CheckEvaluation(s, row.UniverseId, p, evidence, ct); } catch (RuntimeValidationException) { invalid = true; }
                }
                if (p.EndsUtc <= DateTimeOffset.UtcNow || invalid)
                    await ControlAsync(s, row.UniverseId, row.Id, new(Guid.NewGuid(), "Rollback", Convert.ToBase64String(row.RowVersion), "fixture:deadline-stop"), ct);
                // Once per UTC date and scope. Recorded reports are immutable and never send external messages.
                var opBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"M27-monitor:{row.TenantId}:{row.UniverseId}:{DateTimeOffset.UtcNow:yyyy-MM-dd}"));
                var dailyOperation = new Guid(opBytes.AsSpan(0, 16));
                if (!await db.OptimizationAutomationRuns.AsNoTracking().AnyAsync(x => x.TenantId == s.TenantId && x.OperationId == dailyOperation, ct))
                    await AutomateAsync(s, row.UniverseId, new(dailyOperation, "RightsAlert"), ct);
            }
            catch (CoreAccessException) { /* Removed administrator cannot execute automation. */ }
            catch (CoreConcurrencyException) { /* Newer control decision wins; next sweep rechecks. */ }
        }
    }
}
