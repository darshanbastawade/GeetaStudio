using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class SocialPublishing
{
    private async Task Admission(CoreScope s, Guid u, SocialPublication row, CancellationToken ct)
    {
        var account = await Account(s, u, row.AccountId, ct); var binding = Read<RegisterSocialAccount>(account.SnapshotJson);
        if (!account.Enabled || account.TokenState == "Revoked" || binding.TokenExpiresUtc <= DateTimeOffset.UtcNow) throw new EvidenceBlockedException("DestinationAccountDisabledExpiredOrRevoked");
        if (binding.Mode != "Fixture" || Publisher(binding.Adapter).Capabilities.Qualification != "OfflineFixture") throw new EvidenceBlockedException("LivePublishingUnqualifiedUseManualExport");
        if (Read<SocialPackageSnapshot>(row.SnapshotJson).PublisherCapabilities?.Version != Publisher(binding.Adapter).Capabilities.Version) throw new EvidenceBlockedException("PinnedPublisherVersionChanged"); if (row.DeadlineUtc <= DateTimeOffset.UtcNow || row.Attempts >= row.MaxAttempts) throw new EvidenceBlockedException("PublishDeadlineOrAttemptsExceeded");
        if (catalog.Resolve(binding.CapabilityKey).RequiredScopes.Except(binding.AuthorizedScopes).Any()) throw new EvidenceBlockedException("DestinationScopesRequired");
        await Guard(s, u, await Package(s, u, row.PackageId, ct), "Export", true, ct);
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = await db.SocialPublications.AsNoTracking().Where(x => ((x.State == "Scheduled" && x.NextUtc <= now) || (x.State == "Submitting" && x.LeaseUntilUtc < now)) && db.SocialAccounts.Any(a => a.Id == x.AccountId && a.Mode == "Fixture") && db.Members.Any(m => m.TenantId == x.TenantId && m.Actor == x.Actor && (m.Permission == "Administrator" || m.Permission == "Publisher" || m.Permission == "Editor"))).OrderBy(x => x.NextUtc).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (candidate == null) return false; var s = new CoreScope(candidate.TenantId, candidate.Actor); var u = candidate.UniverseId; var token = Guid.NewGuid();
        var claim = await Command(s, u, Guid.NewGuid(), "claim", new { candidate.Id, token }, "Publish", async () =>
        {
            var row = await Publication(s, u, candidate.Id, ct);
            if (row.State == "Submitting") { if (row.LeaseUntilUtc > DateTimeOffset.UtcNow) return false; row.State = "Unknown"; row.LeaseToken = null; row.LeaseUntilUtc = null; row.Outcome = "ExpiredSubmittingLeaseRequiresOriginalReconciliation"; History(row, "Unknown", row.Outcome); return false; }
            if (row.State != "Scheduled" || row.NextUtc > DateTimeOffset.UtcNow) return false;
            try { await Admission(s, u, row, ct); } catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { row.State = "Failed"; row.Outcome = e.Message; History(row, "Failed", e.Message); return false; }
            row.State = "Submitting"; row.Attempts++; row.LeaseToken = token; row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddSeconds(60); History(row, "Submitting", "Original destination/account/adapter/payload pinned; no fallback"); return true;
        }, ct);
        if (!claim) return true;
        var current = await Publication(s, u, candidate.Id, ct); var request = await Dispatch(current, ct); SocialOutcome outcome;
        try
        {
            // Recheck the current lease, package and account immediately before dispatch.
            await Access(s, u, "Publish", ct); await AdmissionForClaim(s, u, current, token, ct);
            outcome = await Publisher(request.Adapter).SubmitAsync(request, ct);
        }
        catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { outcome = new("Blocked", ConfirmedNotSubmitted: true); }
        catch (OperationCanceledException) { outcome = new("Unknown"); }
        catch (Exception) { outcome = new("Unknown"); }
        await Command(s, u, Guid.NewGuid(), "completion", new { candidate.Id, token, outcome }, "Publish", async () =>
        {
            var row = await Publication(s, u, candidate.Id, ct);
            if (row.LeaseToken != token && row.State != "Withdrawn") return row.Id;
            await Apply(row, outcome, ct); row.LeaseToken = null; row.LeaseUntilUtc = null; return row.Id;
        }, CancellationToken.None); return true;
    }
    private async Task AdmissionForClaim(CoreScope s, Guid u, SocialPublication row, Guid token, CancellationToken ct)
    {
        await db.Entry(row).ReloadAsync(ct);
        if (row.State != "Submitting" || row.LeaseToken != token || row.LeaseUntilUtc <= DateTimeOffset.UtcNow) throw new EvidenceBlockedException("CurrentSubmittingLeaseRequired");
        // One admitted attempt is already counted.
        if (row.Attempts > row.MaxAttempts || row.DeadlineUtc <= DateTimeOffset.UtcNow) throw new EvidenceBlockedException("BoundedSubmissionRequired");
        var account = await Account(s, u, row.AccountId, ct); var binding = Read<RegisterSocialAccount>(account.SnapshotJson);
        if (!account.Enabled || account.TokenState == "Revoked" || binding.TokenExpiresUtc <= DateTimeOffset.UtcNow) throw new EvidenceBlockedException("AccountUnavailable");
        await Guard(s, u, await Package(s, u, row.PackageId, ct), "Export", true, ct);
    }
    private async Task Apply(SocialPublication row, SocialOutcome outcome, CancellationToken ct)
    {
        if (outcome.State == "Published")
        {
            if (string.IsNullOrWhiteSpace(outcome.RemoteId) || string.IsNullOrWhiteSpace(outcome.RemoteUrl)) throw new RuntimeValidationException("Original authoritative publication receipt required.");
            if (row.RemoteId != null && (row.RemoteId != outcome.RemoteId || row.RemoteUrl != outcome.RemoteUrl)) throw new RuntimeConflictException("Original remote publication binding cannot change.");
            row.RemoteId = outcome.RemoteId; row.RemoteUrl = outcome.RemoteUrl; row.Outcome = row.State == "Withdrawn" ? "PublishedAfterWithdrawalRequiresManualTakedown" : "Published";
            var snap = Read<SocialPackageSnapshot>(row.SnapshotJson); var s = new CoreScope(row.TenantId, row.Actor);
            if (!row.EditorialCopyId.HasValue)
            {
                var copy = new EditorialCopy { Id = Guid.NewGuid(), TenantId = row.TenantId, UniverseId = row.UniverseId, ItemId = snap.Payload.EditorialItemId, RevisionId = snap.Payload.RevisionId, Channel = snap.Account.Platform, Mode = snap.Account.Mode == "Fixture" ? "Fixture" : "Manual", Reference = outcome.RemoteUrl, State = "Recorded" }; db.EditorialCopies.Add(copy); row.EditorialCopyId = copy.Id;
                // A correction can precede a late provider receipt. Add the new copy to existing remediation.
                var contentId = await db.Revisions.Where(x => x.Id == snap.Payload.RevisionId).Select(x => x.ContentId).SingleAsync(ct);
                foreach (var task in await db.EditorialCorrectionTasks.AsNoTracking().Where(x => x.TenantId == row.TenantId && x.ContentId == contentId && x.Kind == "Internal" && x.State != "Resolved").ToArrayAsync(ct)) db.EditorialCorrectionTasks.Add(new() { Id = Guid.NewGuid(), TenantId = row.TenantId, UniverseId = row.UniverseId, CorrectionId = task.CorrectionId, ContentId = contentId, RevisionId = snap.Payload.RevisionId, CopyId = copy.Id, Kind = "External", Reason = "Late publication receipt requires manual correction/takedown." });
            }
            try { await Guard(s, row.UniverseId, await Package(s, row.UniverseId, row.PackageId, ct), "Export", true, ct); } catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { row.Outcome = "PublishedButCurrentApprovalOrRightsBlocked;ManualRemediationRequired"; }
            if (row.State != "Withdrawn") row.State = "Published"; History(row, "Receipt", row.Outcome + "; " + outcome.RemoteId); return;
        }
        if (row.State == "Withdrawn") { row.Outcome = "Withdrawn; " + outcome.State; History(row, "WithdrawnOutcome", row.Outcome); return; }
        if (outcome.State == "RateLimited" && outcome.ConfirmedNotSubmitted && row.Attempts < row.MaxAttempts && outcome.RetryAfterSeconds is > 0 and <= 3600 && DateTimeOffset.UtcNow.AddSeconds(outcome.RetryAfterSeconds) < row.DeadlineUtc) { row.State = "Scheduled"; row.NextUtc = DateTimeOffset.UtcNow.AddSeconds(outcome.RetryAfterSeconds); row.Outcome = "ConfirmedNotSubmittedRateLimit"; History(row, "Deferred", row.Outcome); return; }
        row.State = outcome.ConfirmedNotSubmitted ? "Failed" : "Unknown"; row.Outcome = outcome.State; History(row, row.State, outcome.State);
    }
    public async Task ControlAsync(CoreScope s, Guid u, Guid id, SocialControl r, CancellationToken ct)
    {
        if (r.Action == "Reconcile")
        {
            await Command(s, u, r.OperationId, "reconcile", new { id, r }, "Publish", async () =>
            {
                var row = await Publication(s, u, id, ct); Expected(row, r.ExpectedRowVersion);
                if (row.State != "Unknown" && !(row.State == "Withdrawn" && row.RemoteId == null && row.Attempts > 0)) throw new RuntimeConflictException("Only unknown submitted work needs original reconciliation.");
                var request = await Dispatch(row, ct); var snap = Read<SocialPackageSnapshot>(row.SnapshotJson); var publisher = Publisher(request.Adapter);
                if (!publisher.Capabilities.Reconciliation || snap.PublisherCapabilities?.Version != publisher.Capabilities.Version) throw new EvidenceBlockedException("OriginalProviderReconciliationUnavailable;ManualInvestigationRequired");
                var outcome = await publisher.ReconcileAsync(request, ct); await Apply(row, outcome, ct); row.LeaseToken = null; row.LeaseUntilUtc = null; History(row, "Reconciled", "Original " + request.Adapter + "/" + request.Platform + "/" + request.AccountReference); return id;
            }, ct); return;
        }
        await Command(s, u, r.OperationId, "control", new { id, r }, "Publish", async () => { var row = await Publication(s, u, id, ct); Expected(row, r.ExpectedRowVersion); CoreRules.Text(r.EvidenceReference, 1000, "Publication control evidence"); if (r.Action != "Withdraw") throw new RuntimeValidationException("Withdraw or Reconcile required; unknown work cannot be resumed/resubmitted."); if (row.State == "Withdrawn") return id; row.State = "Withdrawn"; row.Outcome = row.RemoteId == null ? (row.Attempts > 0 ? "SubmittedOutcomeStillRequiresReconciliation" : "CancelledBeforeSubmission") : "RemotePostRetained;ManualTakedownRequired"; History(row, "Withdrawn", row.Outcome); return id; }, ct);
    }
    public async Task ReceiptAsync(CoreScope s, Guid u, Guid id, SocialReceipt r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "receipt", new { id, r }, "Publish", async () =>
    {
        var row = await Publication(s, u, id, ct); var request = await Dispatch(row, ct); var snap = Read<SocialPackageSnapshot>(row.SnapshotJson);
        if (r.Platform != request.Platform || r.AccountReference != request.AccountReference || r.Adapter != request.Adapter || r.PayloadHash != request.PayloadHash) throw new RuntimeConflictException("Receipt destination/account/provider/payload differs.");
        CoreRules.Text(r.EventId, 100, "Receipt event"); CoreRules.Text(r.RemoteId, 100, "External resource"); CoreRules.Text(r.RemoteUrl, 200, "External URL");
        var fingerprint = Hash(new { id, r.Platform, r.AccountReference, r.Adapter, r.PayloadHash, r.RemoteId, r.RemoteUrl, r.EvidenceReference });
        var old = await db.SocialCallbacks.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.EventId == r.EventId, ct);
        if (old != null) { if (old.PublicationId != id || old.Fingerprint != fingerprint) throw new RuntimeConflictException("Duplicate event differs."); return id; }
        if (snap.Account.Mode == "Fixture")
        {
            if (row.Attempts == 0) throw new RuntimeConflictException("Unsubmitted fixture cannot receive a callback.");
            var proof = await ledger.FindAsync(request, ct); if (proof.State != "Published" || proof.RemoteId != r.RemoteId || proof.RemoteUrl != r.RemoteUrl) throw new EvidenceBlockedException("AuthenticatedOriginalFixtureReceiptRequired");
        }
        else
        {
            if (row.State != "Scheduled" || row.ScheduledUtc > DateTimeOffset.UtcNow) throw new RuntimeConflictException("Due reviewed manual package required.");
            MediaRules.Attest("Manual", r.EvidenceReference); await Guard(s, u, await Package(s, u, row.PackageId, ct), "Publish", true, ct);
            if (!Uri.TryCreate(r.RemoteUrl, UriKind.Absolute, out var url) || url.Scheme != "https" || url.UserInfo.Length > 0 || !(request.Platform switch { "YouTube" => url.Host is "youtube.com" or "www.youtube.com" or "youtu.be", "Instagram" => url.Host is "instagram.com" or "www.instagram.com", "Facebook" => url.Host is "facebook.com" or "www.facebook.com", "X" => url.Host is "x.com" or "www.x.com", _ => false })) throw new RuntimeValidationException("Actual destination URL required.");
        }
        db.SocialCallbacks.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, PublicationId = id, EventId = r.EventId, Fingerprint = fingerprint }); await Apply(row, new("Published", r.RemoteId, r.RemoteUrl), ct); return id;
    }, ct);
}
public sealed class SqlSocialEffectLedger(IDbContextFactory<CoreDbContext> factory) : ISocialEffectLedger
{
    public async Task<SocialOutcome> CommitAsync(SocialDispatch r, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"SocialFixture:" + r.PublicationId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Fixture ledger unavailable',1;", ct);
        var old = await db.SocialEffects.SingleOrDefaultAsync(x => x.PublicationId == r.PublicationId, ct); var fingerprint = EditorialBinding.Hash(r);
        if (old != null) { if (old.Fingerprint != fingerprint) throw new RuntimeConflictException("Fixture original binding differs."); return EditorialBinding.Read<SocialOutcome>(old.OutcomeJson); }
        var outcome = new SocialOutcome("Published", "fixture-" + r.PublicationId.ToString("N"), "fixture://" + r.Platform + "/" + r.PublicationId.ToString("N")); db.SocialEffects.Add(new() { Id = Guid.NewGuid(), TenantId = r.TenantId, UniverseId = r.UniverseId, PublicationId = r.PublicationId, Fingerprint = fingerprint, OutcomeJson = EditorialBinding.Json(outcome) }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return outcome;
    }
    public async Task<SocialOutcome> FindAsync(SocialDispatch r, CancellationToken ct)
    { await using var db = await factory.CreateDbContextAsync(ct); var row = await db.SocialEffects.AsNoTracking().SingleOrDefaultAsync(x => x.PublicationId == r.PublicationId && x.TenantId == r.TenantId && x.UniverseId == r.UniverseId, ct); if (row == null) return new("AuthoritativeNotFound", ConfirmedNotSubmitted: true); if (row.Fingerprint != EditorialBinding.Hash(r)) throw new RuntimeConflictException("Original destination differs."); return EditorialBinding.Read<SocialOutcome>(row.OutcomeJson); }
}
