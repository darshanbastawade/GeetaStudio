using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace ContentOS.Infrastructure;

public sealed partial class EditorialWorkbench
{
    public async Task<EditorialProviderStatus> ProvidersAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, "Admin", ct); var profiles = (await aiAdmin.ProfilesAsync(s, ct)).ToArray(); var connections = new List<EditorialProviderConnection>();
        foreach (var profile in profiles)
        {
            var opened = db.Database.GetDbConnection().State != System.Data.ConnectionState.Open; if (opened) await db.Database.OpenConnectionAsync(ct);
            try
            {
                await using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                cmd.CommandText = "SELECT [Enabled],[Killed],[SecretRef] FROM [runtime].[Connections] WHERE [Id]=@id"; var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = profile.ConnectionId; cmd.Parameters.Add(p);
                bool enabled, killed; string? reference;
                await using (var r = await cmd.ExecuteReaderAsync(ct)) { if (!await r.ReadAsync(ct)) throw new CoreAccessException(); enabled = r.GetBoolean(0); killed = r.GetBoolean(1); reference = r.IsDBNull(2) ? null : r.GetString(2); }
                connections.Add(new(profile.Id, enabled, killed, await secrets.IsAvailableAsync(reference, ct)));
            }
            finally { if (opened) await db.Database.CloseConnectionAsync(); }
        }
        var heads = await db.AiRouteHeads.AsNoTracking().Where(x => x.TenantId == s.TenantId).ToListAsync(ct);
        return new(profiles, (await aiAdmin.RoutesAsync(s, ct)).ToArray(), heads.Select(x => new EditorialRouteHead(x.Id, x.Task, x.RouteId, Convert.ToBase64String(x.RowVersion))).ToArray(), connections.ToArray());
    }
    public Task<Guid> RouteAsync(CoreScope s, Guid u, EditorialRouteCommand r, CancellationToken ct) => Command(s, u, r.OperationId, "route", r, "Admin", async () =>
    {
        if (r.Action == "ValidateAndSave")
        {
            if (r.Settings == null) throw new RuntimeValidationException("Typed route settings required.");
            return await aiAdmin.RouteAsync(s, r.Settings, ct);
        }
        if (r.Action == "Disable")
        {
            if (!r.ProfileId.HasValue) throw new RuntimeValidationException("Owned profile required."); await aiAdmin.SwitchAsync(s, r.ProfileId.Value, new(false, true), ct); return r.ProfileId.Value;
        }
        if (r.Action is not ("Activate" or "Rollback")) throw new RuntimeValidationException("Allowed route actions:ValidateAndSave,Activate,Rollback,Disable.");
        var route = await db.AiRoutes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.RouteId && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        var head = await db.AiRouteHeads.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Task == route.Task, ct);
        if (head == null) { if (r.ExpectedHeadRowVersion != "") throw new CoreConcurrencyException(); } else Expected(head, r.ExpectedHeadRowVersion);
        await aiAdmin.ActivateAsync(s, r.RouteId, ct); return r.RouteId;
    }, ct);
    public async Task<EditorialTrendView> SubmitTrendAsync(CoreScope s, Guid u, EditorialTrendInput r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "trend", r, "Write", async () =>
        {
            CoreRules.Text(r.Key, 100, "Trend key"); CoreRules.Text(r.Headline, 1000, "Headline"); CoreRules.Text(r.ScriptPreview, 8000, "Script preview");
            if (r.SourceSnapshotIds.Length is < 1 or > 10 || r.ClaimRevisionIds.Length > 10 || r.ExpiresUtc <= r.ObservedUtc || r.ExpiresUtc - r.ObservedUtc > TimeSpan.FromDays(7) || r.ObservedUtc > DateTimeOffset.UtcNow.AddMinutes(5) || r.EstimatedCost is < 0 or > 100000 || r.Currency is not ("INR" or "USD")) throw new RuntimeValidationException("Bounded timestamped manual trend with source/cost scope required.");
            foreach (var snapshot in r.SourceSnapshotIds) if (!await (from snap in db.SourceSnapshots join ed in db.SourceEditions on snap.EditionId equals ed.Id join work in db.SourceWorks on ed.WorkId equals work.Id where snap.Id == snapshot && work.TenantId == s.TenantId && work.UniverseId == u select snap.Id).AnyAsync(ct)) throw new CoreAccessException();
            foreach (var claim in r.ClaimRevisionIds) if (!await db.ClaimRevisions.AnyAsync(x => x.Id == claim && db.Claims.Any(c => c.Id == x.ClaimId && c.TenantId == s.TenantId && c.UniverseId == u), ct)) throw new CoreAccessException();
            var row = new EditorialTrend { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, SnapshotJson = Json(r) }; db.EditorialTrends.Add(row); return row.Id;
        }, ct); return await TrendView(s, u, await db.EditorialTrends.AsNoTracking().SingleAsync(x => x.Id == id, ct), ct);
    }
    private async Task<EditorialTrendView> TrendView(CoreScope s, Guid u, EditorialTrend row, CancellationToken ct)
    {
        var r = Read<EditorialTrendInput>(row.SnapshotJson); var blocks = new List<string>();
        if (r.ExpiresUtc <= DateTimeOffset.UtcNow) blocks.Add("StaleCandidate");
        foreach (var snapshot in r.SourceSnapshotIds) { var d = await new SourceRightsGuard(db).DecideAsync(s, snapshot, new("Read", r.Cast.Locale, "IN", "Text"), ct); if (!d.Decision.Allowed) blocks.Add("Rights:" + d.Decision.Reason); }
        if (r.ClaimRevisionIds.Length == 0) blocks.Add("UnverifiedEvent");
        foreach (var id in r.ClaimRevisionIds)
        {
            var claim = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == id, ct); var head = await db.Claims.AsNoTracking().SingleAsync(x => x.Id == claim.ClaimId, ct);
            if (claim.ReviewStatus != "Approved" || claim.Type is not ("NewsClaim" or "HistoricalClaim") || head.CurrentVersion != claim.Version || await db.ClaimCorrections.AnyAsync(x => x.ClaimRevisionId == id && x.Status == "Open", ct) || await db.EditorialCorrections.AnyAsync(x => x.ClaimRevisionId == id, ct)) blocks.Add("UnverifiedOrStaleEvent");
            if (!await db.ClaimSpans.AnyAsync(x => x.ClaimRevisionId == id && r.SourceSnapshotIds.Contains(x.SnapshotId), ct)) blocks.Add("EventSourcesDoNotMatch");
        }
        if (!r.LessonVariantId.HasValue) blocks.Add("NoSuitableTeachingMatch");
        else { var lesson = await lessons.InspectAsync(s, u, r.LessonVariantId.Value, ct); if (lesson.OutputWithheld || lesson.ReviewStatus != "Approved") blocks.Add("UnreviewedTeachingMatch"); if (lesson.Snapshot.Audience.Rules.Audience!.AudienceId != r.Cast.AudienceId || lesson.Snapshot.Locale.Rules.Locale!.Locale != r.Cast.Locale) blocks.Add("TeachingAudienceMismatch"); }
        if (r.RequiresTranscript)
        {
            if (!r.TranscriptSnapshotId.HasValue || !r.SourceSnapshotIds.Contains(r.TranscriptSnapshotId.Value) || !await db.SourceSnapshots.AnyAsync(x => x.Id == r.TranscriptSnapshotId && x.Text != null && x.ReviewStatus == "Approved", ct)) blocks.Add("TranscriptUnavailable");
        }
        RegistryCastPlan? preview = null; try { preview = await registry.CastAsync(s, u, r.Cast, ct); if (preview.MissingRoles.Length > 0 || preview.Selected.Any(x => x.Character.Profile.ReviewStatus != "Approved" || x.Character.Status != "Active")) blocks.Add("InappropriateCast"); } catch (RuntimeValidationException) { blocks.Add("InappropriateCast"); }
        if (blocks.Any(x => x.StartsWith("Rights:"))) r = r with { Headline = "Withheld pending current source rights", ScriptPreview = "Withheld" };
        return new(row.Id, r, row.State, Convert.ToBase64String(row.RowVersion), blocks.Distinct().ToArray(), preview, row.ReviewEvidence);
    }
    public async Task<IReadOnlyList<EditorialTrendView>> TrendsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, "Read", ct); var rows = await db.EditorialTrends.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(101).ToListAsync(ct);
        if (rows.Count > 100) throw new RuntimeValidationException("Manual trend queue capacity100 exceeded."); var result = new List<EditorialTrendView>(); foreach (var row in rows) result.Add(await TrendView(s, u, row, ct)); return result;
    }
    public async Task ReviewTrendAsync(CoreScope s, Guid u, Guid id, ReviewEditorialTrend r, CancellationToken ct)
    {
        await Command(s, u, r.OperationId, "trend-review", new { id, r }, "Review", async () =>
        {
            var row = await db.EditorialTrends.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException(); Expected(row, r.ExpectedRowVersion);
            EditorialRules.Evidence(r.EvidenceReference, r.EvidenceReference.Trim().StartsWith("fixture:") ? "Fixture" : "Manual");
            if (r.Decision is not ("Approve" or "Hold" or "Reject")) throw new RuntimeValidationException("Trend decision:Approve,Hold,Reject.");
            var view = await TrendView(s, u, row, ct); if (r.Decision == "Approve" && view.Blocks.Length > 0) throw new EvidenceBlockedException(string.Join(", ", view.Blocks));
            row.State = r.Decision == "Approve" ? "Approved" : r.Decision == "Hold" ? "Held" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; return id;
        }, ct);
    }
}
