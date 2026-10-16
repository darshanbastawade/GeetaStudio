using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class MediaLibrary
{
    private (string Key, MediaProviderCapabilities Cap) Adapter(string key)
    { var image = images.SingleOrDefault(x => x.Key == key); if (image != null) return (key, image.Capabilities); var voice = voices.SingleOrDefault(x => x.Key == key); return voice != null ? (key, voice.Capabilities) : throw new RuntimeValidationException("Deployed qualified media adapter required."); }
    public Task<Guid> ProfileAsync(CoreScope s, Guid u, RegisterMediaProfile r, CancellationToken ct) => Command(s, u, r.OperationId, "profile", r, "Admin", async () =>
    {
        CoreRules.Text(r.Key, 100, "Media profile key"); CoreRules.Text(r.AccountReference, 200, "Original account binding"); if (!System.Text.RegularExpressions.Regex.IsMatch(r.AccountReference, "^local:[A-Za-z0-9_-]{1,100}$")) throw new RuntimeValidationException("Only explicit local account references are supported by shipped M16 adapters."); CoreRules.Text(r.ResourceKey, 1000, "Provider resource/voice binding"); MediaRules.Attest(r.Mode, r.EvidenceReference); var a = Adapter(r.AdapterKey);
        if (a.Cap.Kind != r.Kind || !a.Cap.Locales.Contains(r.Locale) || r.CallCeiling < 0 || r.DailyBudget < r.CallCeiling || r.DailyBudget > 10000 || r.Currency is not ("INR" or "USD") || a.Cap.Qualification != "LiveSandboxVerified" && r.CallCeiling != 0) throw new EvidenceBlockedException("MediaProfileCapabilityOrLivePaidQualificationRequired");
        if (a.Cap.Qualification == "OfflineFixture" && r.Mode != "Fixture") throw new EvidenceBlockedException("FixtureAdapterCannotAcquireLiveQualificationByAttestation");
        if (r.Kind == "Speech") voices.Single(x => x.Key == r.AdapterKey).ValidateProfile(r);
        var old = await db.MediaProfiles.Where(x => x.TenantId == s.TenantId && x.UniverseId == u && x.Key == r.Key).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct); if (r.Version != (old?.Version ?? 0) + 1) throw new RuntimeConflictException("Append media profile version."); var row = new MediaProfile { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, SnapshotJson = Json(r) }; db.MediaProfiles.Add(row); return row.Id;
    }, ct);
    public async Task<MediaProfileView[]> ProfilesAsync(CoreScope s, Guid u, CancellationToken ct)
    { await Access(s, u, "Read", ct); return (await db.MediaProfiles.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).Take(100).ToListAsync(ct)).Select(x => { var p = Read<RegisterMediaProfile>(x.SnapshotJson); return new MediaProfileView(x.Id, p, Adapter(p.AdapterKey).Cap, x.Enabled, Convert.ToBase64String(x.RowVersion)); }).ToArray(); }
    public async Task<SpeechAdapterView[]> SpeechAdaptersAsync(CoreScope s, Guid u, CancellationToken ct)
    { await Access(s, u, "Admin", ct); return voices.Select(x => new SpeechAdapterView(x.Key, x.Capabilities, x.VoiceResources)).ToArray(); }
    public async Task DisableAsync(CoreScope s, Guid u, Guid id, MediaProfileControl r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "disable", new { id, r }, "Admin", async () => { var row = await db.MediaProfiles.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException(); Expected(row, r.ExpectedRowVersion); MediaRules.Attest(r.EvidenceReference.StartsWith("fixture:") ? "Fixture" : "Manual", r.EvidenceReference); row.Enabled = false; return id; }, ct);
    private static MediaGenerationView GenerationView(MediaGeneration x) => new(x.Id, x.State, Read<MediaGenerationSnapshot>(x.SnapshotJson), x.ExternalResourceId, x.AcceptedVersionId, x.ReservedCost, x.ActualCost, x.Outcome, x.DeadlineUtc, Convert.ToBase64String(x.RowVersion));
    private async Task<MediaGeneration> Generation(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.MediaGenerations.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    public async Task<MediaGenerationView> GenerateAsync(CoreScope s, Guid u, GenerateMedia r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "generate", r, "Admin", async () =>
        {
            await WatchAdmissionGuard.Assert(db, s, u, r.Admission, ct);
            var profile = await db.MediaProfiles.SingleOrDefaultAsync(x => x.Id == r.ProfileId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException(); if (!profile.Enabled) throw new EvidenceBlockedException("MediaProviderDisabled"); var p = Read<RegisterMediaProfile>(profile.SnapshotJson); var cap = Adapter(p.AdapterKey).Cap; MediaRules.Capability(cap, r); MediaRules.License(r.Output.License);
            if (p.Locale != r.Output.Locale || r.Output.Bytes.Length != 0 || r.Output.GenerationId != null || r.Output.ExpectedSha256.Length != 0 || p.CallCeiling > r.MaxCost) throw new RuntimeValidationException("Generation output metadata must not contain pre-generated bytes or a fabricated provider result.");
            if (p.Kind == "Speech" && r.Output.Kind != "Voice" || p.Kind == "Image" && r.Output.Kind is "Voice" or "Music") throw new EvidenceBlockedException("UnsupportedGeneratedMediaKind");
            if (cap.Qualification == "OfflineFixture" && r.Output.License.Kind != "Fixture") throw new EvidenceBlockedException("FixtureOutputRightsRemainFixture");
            if (r.Output.CharacterProfileRevisionId.HasValue) { var characterProfile = await db.RegistryRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.Output.CharacterProfileRevisionId && x.UniverseId == u && x.TenantId == s.TenantId && x.Kind == "Character", ct) ?? throw new CoreAccessException(); var identity = Read<RegistryCharacterRules>(characterProfile.RulesJson); if (characterProfile.ReviewStatus != "Approved" || identity.FictionalStatus != "Fictional") throw new EvidenceBlockedException("ThisMediaProviderSupportsReviewedFictionalCharactersOnly"); }
            await MediaRightsGuard.Generation(db, s, u, r, p.Kind, ct);
            var now = DateTimeOffset.UtcNow; var periodStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
            var used = await db.MediaGenerations.Where(x => x.TenantId == s.TenantId && x.Currency == p.Currency && x.CreatedUtc >= periodStart).SumAsync(x => x.ActualCost ?? x.ReservedCost, ct); if (used + p.CallCeiling > p.DailyBudget) throw new EvidenceBlockedException("MediaPeriodBudgetExceeded");
            var row = new MediaGeneration { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ProfileId = profile.Id, Actor = s.Actor, SnapshotJson = Json(new MediaGenerationSnapshot(p, cap, r)), ReservedCost = p.CallCeiling, Currency = p.Currency, DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(r.DeadlineSeconds) }; db.MediaGenerations.Add(row); return row.Id;
        }, ct); return GenerationView(await Generation(s, u, id, ct));
    }
    public async Task<MediaGenerationView[]> GenerationsAsync(CoreScope s, Guid u, CancellationToken ct) { await Access(s, u, "Read", ct); return (await db.MediaGenerations.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)).Select(GenerationView).ToArray(); }
    private async Task<MediaProviderRequest> ProviderRequest(CoreScope s, Guid u, MediaGeneration row, CancellationToken ct, bool reconcile = false)
    {
        var snap = Read<MediaGenerationSnapshot>(row.SnapshotJson); if (!reconcile) { await WatchAdmissionGuard.Assert(db, s, u, snap.Request.Admission, ct); await MediaRightsGuard.Generation(db, s, u, snap.Request, snap.Profile.Kind, ct); }
        var references = new List<byte[]>(); foreach (var id in snap.Request.ReferenceVersionIds) { var version = await MediaRightsGuard.Assert(db, s, u, id, "Reuse", true, ct); var bytes = await store.GetAsync(version.StorageAssetId, version.StorageOwner, ct) ?? throw new RuntimeConflictException("Reference image unavailable."); if (inspector.Inspect(bytes).Mime != "image/png") throw new EvidenceBlockedException("ActualImageReferenceRequired"); references.Add(bytes); }
        return new(row.Id, snap.Profile.AccountReference, snap.Profile.ResourceKey, snap.Profile.Locale, snap.Request.PromptOrText, references.ToArray(), snap.Request.Pronunciation, [], 1048576, snap.Profile.CallCeiling);
    }
    public async Task<bool> ProcessNextAsync(string worker, CancellationToken ct)
    {
        var candidate = await db.MediaGenerations.AsNoTracking().Where(x => (x.State == "Prepared" || x.State == "Submitting" && x.LeaseUntilUtc < DateTimeOffset.UtcNow) && db.Members.Any(m => m.TenantId == x.TenantId && m.Actor == x.Actor && m.Permission == "Administrator")).OrderBy(x => x.CreatedUtc).FirstOrDefaultAsync(ct); if (candidate == null) return false; var s = new CoreScope(candidate.TenantId, candidate.Actor); var u = candidate.UniverseId; var token = Guid.NewGuid();
        var submit = await Command(s, u, Guid.NewGuid(), "claim", new { candidate.Id, worker }, "Admin", async () =>
        {
            var row = await Generation(s, u, candidate.Id, ct); if (row.State == "Submitting" && row.LeaseUntilUtc < DateTimeOffset.UtcNow) { row.State = "Unknown"; row.Outcome = "LeaseExpiredAfterPossibleSubmission"; return false; }
            if (row.State != "Prepared") return false;
            if (row.DeadlineUtc <= DateTimeOffset.UtcNow || !await db.MediaProfiles.AnyAsync(x => x.Id == row.ProfileId && x.Enabled, ct)) { row.State = "Failed"; row.ActualCost = 0; row.Outcome = "DeadlineOrProviderDisabledBeforeSubmission"; return false; }
            try { await ProviderRequest(s, u, row, ct); } catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { row.State = "Failed"; row.ActualCost = 0; row.Outcome = "ReferenceRightsOrReviewChangedBeforeSubmission"; return false; }
            row.State = "Submitting"; row.LeaseToken = token; row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddSeconds(30); return true;
        }, ct);
        if (!submit) return true;
        var current = await Generation(s, u, candidate.Id, ct); var snapshot = Read<MediaGenerationSnapshot>(current.SnapshotJson); MediaProviderResult result;
        try { var request = await ProviderRequest(s, u, current, ct); using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(current.DeadlineUtc - DateTimeOffset.UtcNow); result = snapshot.Profile.Kind == "Image" ? await images.Single(x => x.Key == snapshot.Profile.AdapterKey).SubmitAsync(request, deadline.Token) : await voices.Single(x => x.Key == snapshot.Profile.AdapterKey).SubmitAsync(request, deadline.Token); }
        catch (Exception e) when (e is not CoreAccessException) { result = new("Unknown", null, null, null, e.GetType().Name); }
        await Record(s, u, current.Id, result, token, CancellationToken.None); return true;
    }
    private async Task Record(CoreScope s, Guid u, Guid id, MediaProviderResult result, Guid? token, CancellationToken ct)
    {
        await Command(s, u, Guid.NewGuid(), "provider-result", new { id, result.State, result.ResourceId, result.Cost, Digest = result.Bytes == null ? null : MediaRules.Hash(result.Bytes) }, "Admin", async () =>
        {
            var row = await Generation(s, u, id, ct); if (row.State == "Completed") return id; if (token.HasValue && row.LeaseToken != token) throw new RuntimeConflictException("Media result lease superseded.");
            if (row.ExternalResourceId != null && result.ResourceId != null && row.ExternalResourceId != result.ResourceId) throw new RuntimeConflictException("Original external resource binding cannot change.");
            row.ExternalResourceId ??= result.ResourceId; row.Outcome = result.Outcome;
            if (result.Cost < 0 || result.Cost > row.ReservedCost) throw new EvidenceBlockedException("MediaCostExceedsPinnedCeiling");
            row.ActualCost = result.Cost;
            if (result.State == "Completed" && result.Bytes != null && result.ResourceId != null) { inspector.Inspect(result.Bytes); row.State = "Accepting"; row.ResultSha256 = MediaRules.Hash(result.Bytes); }
            else if (result.State is "Failed" or "Unsupported") { row.State = "Failed"; row.ActualCost ??= 0; }
            else row.State = result.State == "Submitted" ? "Submitted" : "Unknown";
            return id;
        }, ct);
        if (result.State == "Completed" && result.Bytes != null && result.ResourceId != null) await AcceptAsync(s, u, new(Derived(id, "accept"), id, result.ResourceId, result.Bytes, MediaRules.Hash(result.Bytes), result.Cost ?? 0), ct);
    }
    public async Task ReconcileAsync(CoreScope s, Guid u, ReconcileMedia r, CancellationToken ct)
    {
        await Command(s, u, r.OperationId, "reconcile-request", r, "Admin", async () => { return (await Generation(s, u, r.GenerationId, ct)).Id; }, ct); var row = await Generation(s, u, r.GenerationId, ct); if (row.State == "Completed") return; if (row.State is not ("Unknown" or "Submitted" or "Accepting")) throw new RuntimeConflictException("Only original submitted/unknown media can reconcile.");
        var snap = Read<MediaGenerationSnapshot>(row.SnapshotJson); if (!snap.Capabilities.Reconciliation) throw new EvidenceBlockedException("OriginalMediaProviderReconciliationUnsupported"); var request = await ProviderRequest(s, u, row, ct, true);
        var result = snap.Profile.Kind == "Image" ? await images.Single(x => x.Key == snap.Profile.AdapterKey).ReconcileAsync(request, row.ExternalResourceId, ct) : await voices.Single(x => x.Key == snap.Profile.AdapterKey).ReconcileAsync(request, row.ExternalResourceId, ct); await Record(s, u, row.Id, result, null, ct);
    }
    public async Task<MediaVersionView> AcceptAsync(CoreScope s, Guid u, AcceptMediaResult r, CancellationToken ct)
    {
        await Access(s, u, "Admin", ct); var row = await Generation(s, u, r.GenerationId, ct);
        if (row.ExternalResourceId != r.ResourceId || row.ResultSha256 != r.ExpectedSha256 || MediaRules.Hash(r.Bytes) != r.ExpectedSha256 || row.ActualCost != r.Cost) throw new RuntimeConflictException("Callback must match authenticated original provider output/checksum/cost.");
        await Command(s, u, r.OperationId, "callback", new { r.GenerationId, r.ResourceId, r.ExpectedSha256, r.Cost }, "Admin", () => Task.FromResult(row.Id), ct);
        if (row.AcceptedVersionId.HasValue) return await View(s, u, await Version(s, u, row.AcceptedVersionId.Value, ct), ct);
        if (row.State != "Accepting") throw new RuntimeConflictException("Reconcile provider output before acceptance."); var snap = Read<MediaGenerationSnapshot>(row.SnapshotJson);
        var asset = await ImportAsync(s, u, snap.Request.Output with { OperationId = Derived(row.Id, "output"), Bytes = r.Bytes, ExpectedSha256 = r.ExpectedSha256, GenerationId = row.Id, ParentVersionIds = (snap.Request.Output.ParentVersionIds ?? []).Concat(snap.Request.ReferenceVersionIds).Distinct().ToArray() }, ct);
        await Command(s, u, Derived(row.Id, "complete"), "completed", new { GenerationId = row.Id, VersionId = asset.Id }, "Admin", async () => { var current = await Generation(s, u, row.Id, ct); current.State = "Completed"; current.AcceptedVersionId = asset.Id; current.LeaseToken = null; current.LeaseUntilUtc = null; return asset.Id; }, ct); return asset;
    }
}

