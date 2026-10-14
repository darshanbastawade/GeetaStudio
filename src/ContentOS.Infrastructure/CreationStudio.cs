using System.Data;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed class CreationStudio(CoreDbContext db, CustomerWorkbench customers, IEditorialWorkbench editorial, IMediaLibrary mediaLibrary, IWatchFactory watch, IEnumerable<ICreationRenderer> renderers) : ICreationStudio
{
    public Task<CreationWorkspace[]> WorkspacesAsync(System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct) => customers.CreationWorkspaces(principal, ct);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static readonly System.Diagnostics.Metrics.Meter Meter = new("ContentOS.Create", "1.0");
    private static readonly System.Diagnostics.Metrics.Counter<long> Events = Meter.CreateCounter<long>("creation.transitions");
    private static string Json<T>(T x) => JsonSerializer.Serialize(x, Options);
    private static T Read<T>(string x) => JsonSerializer.Deserialize<T>(x, Options)!;
    private static string Hash(object x) => EditorialBinding.Hash(x);
    private static Guid Op(Guid id, string action) => new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id + ":create.v1:" + action))[..16]);
    private static string Version(CoreRecord x) => Convert.ToBase64String(x.RowVersion);
    private ICreationRenderer Renderer(string key) => renderers.SingleOrDefault(x => x.Key == key && x.Qualification == "LocalZeroCost") ?? throw new RuntimeValidationException("Qualified local creation adapter required; paid/live creation is not enabled.");
    private async Task<T> Change<T>(Guid tenant, Func<Task<T>> action, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Create:" + tenant},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 51000,'Creation lock unavailable',1;", ct);
        try { var result = await action(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result; } catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    private async Task Admin(CoreScope s, CancellationToken ct) { if (!await db.Members.AnyAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor && x.Permission == "Administrator", ct)) throw new CoreAccessException(); }
    private static CoreScope Staff(CreationTemplate t) => new(t.TenantId, t.Actor);
    private async Task<EditorialPacket> Guard(CreationTemplate t, string purpose, CancellationToken ct)
    {
        CreationRules.Require(t.Enabled, "Template disabled."); await Admin(Staff(t), ct); var r = Read<CreationTemplateRequest>(t.SettingsJson); Renderer(r.Adapter);
        var packet = await editorial.InspectAsync(Staff(t), r.UniverseId, r.EditorialItemId, ct);
        CreationRules.Require(packet.RevisionId == r.RevisionId && packet.Current != null && !packet.SourceOutputWithheld && packet.Approvals.Any(x => x.Current), "Template source approval changed; renew template review.");
        var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == packet.ContentId, ct); var audience = await db.Audiences.AsNoTracking().SingleAsync(x => x.Id == head.AudienceId, ct);
        CreationRules.Require(head.LocaleId == "en-IN" && r.AgeBands.All(x => audience.MinAge <= (x == "Child" ? 5 : x == "Teen" ? 13 : 18) && audience.MaxAge >= (x == "Child" ? 12 : x == "Teen" ? 17 : 120)), "Reviewed source audience and locale must cover each declared age band.");
        var profile = await db.RegistryRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.CharacterProfileRevisionId && x.TenantId == t.TenantId && x.UniverseId == t.UniverseId && x.Kind == "Character" && x.ReviewStatus == "Approved", ct) ?? throw new EvidenceBlockedException("ReviewedCanonicalCharacterRequired");
        var rules = Read<RegistryCharacterRules>(profile.RulesJson); var core = await db.CharacterRevisions.AsNoTracking().SingleAsync(x => x.Id == profile.CoreRevisionId, ct);
        CreationRules.Require(rules.FictionalStatus == "Fictional" && rules.AudienceIds.Contains(head.AudienceId) && rules.Locales.Contains("en-IN") && await db.Characters.AnyAsync(x => x.Id == core.CharacterId && x.Status == "Active", ct) && !await db.CharacterRevisions.AnyAsync(x => x.CharacterId == core.CharacterId && x.Version > core.Version, ct), "Canonical character changed/retired or is ineligible; renew template review.");
        await new SourceRightsGuard(db).CheckAsync(Staff(t), r.RevisionId, new(purpose, "en-IN", "IN", "Text", Adaptation: purpose == "Generate"), ct);
        foreach (var media in r.MediaVersionIds) await MediaRightsGuard.Assert(db, Staff(t), r.UniverseId, media, purpose == "Publish" ? "Publish" : "Reuse", true, ct);
        if (r.WatchProductionId != null) { var video = await watch.ExportAsync(Staff(t), r.UniverseId, r.WatchProductionId.Value, purpose == "Publish" ? "Publish" : "Export", ct); CreationRules.Require(video.Production.State == "Approved" && video.Production.Snapshot.Script.RevisionId == r.RevisionId, "Exact reviewed media of the template source required."); }
        return packet;
    }
    public Task<Guid> TemplateAsync(CoreScope s, CreationTemplateRequest r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        await Admin(s, ct); CustomerRules.Key(r.Key); CreationRules.Require(r.OperationId != Guid.Empty && r.Version > 0 && r.Format is "Story" or "Comic" or "LearningPack" && r.Credits is >= 1 and <= 100 && r.AgeBands.Length is > 0 and <= 3 && r.AgeBands.All(x => x is "Adult" or "Teen" or "Child") && r.Themes.Length is > 0 and <= 10 && r.MediaVersionIds.Length <= 4, "Bounded reviewed template required.");
        foreach (var theme in r.Themes) CustomerRules.Key(theme); MediaRules.Attest(r.Evidence.StartsWith("fixture:") ? "Fixture" : "Manual", r.Evidence);
        var old = await db.CreationTemplates.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct); if (old != null) { CreationRules.Require(old.Actor == s.Actor && old.SettingsJson == Json(r), "Template replay differs."); return old.Id; }
        var row = new CreationTemplate { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = r.UniverseId, OperationId = r.OperationId, Actor = s.Actor, Key = r.Key, Version = r.Version, SettingsJson = Json(r) }; await Guard(row, "Generate", ct); db.CreationTemplates.Add(row); return row.Id;
    }, ct);
    public async Task<CreationTemplateView[]> TemplatesAsync(CustomerScope s, CancellationToken ct) { await customers.MeAsync(s, ct); return (await db.CreationTemplates.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.Enabled).Take(100).ToArrayAsync(ct)).Select(x => new CreationTemplateView(x.Id, Read<CreationTemplateRequest>(x.SettingsJson), x.Enabled)).ToArray(); }
    public Task ControlTemplateAsync(CoreScope s, Guid id, bool enabled, CancellationToken ct) => Change(s.TenantId, async () => { await Admin(s, ct); var t = await db.CreationTemplates.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == id, ct) ?? throw new CoreAccessException(); t.Enabled = enabled; if (enabled) await Guard(t, "Generate", ct); db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = enabled ? "create.template-enabled" : "create.template-disabled" }); return true; }, ct);
    private async Task<(PrivateCreation Row, CreationTemplate Template)> Own(CustomerScope s, Guid id, CancellationToken ct)
    {
        var user = await customers.MeAsync(s, ct); var row = await db.Creations.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UserId == user.Id, ct) ?? throw new CoreAccessException(); await customers.TalkAuthority(s, row.ProfileId, ct); var t = await db.CreationTemplates.SingleAsync(x => x.Id == row.TemplateId && x.TenantId == s.TenantId, ct); return (row, t);
    }
    public Task<Guid> UploadAsync(CustomerScope s, CreationUploadRequest r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        var authority = await customers.TalkAuthority(s, r.ProfileId, ct); CreationRules.Require(r.OperationId != Guid.Empty && r.Consent && !r.RealPerson && r.Rights == "Owned", "Explicit owned-material/guardian consent required; real-person likeness processing is not qualified.");
        Renderer("create.local.a.v1").Validate(new(r.MediaType, r.Content, r.AltText, r.Rights)); var fingerprint = Hash(r); var old = await db.CreationUploads.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct); if (old != null) { CreationRules.Require(old.UserId == authority.User.Id && old.Fingerprint == fingerprint && !old.Deleted, "Upload replay differs or was deleted."); return old.Id; }
        CreationRules.Require(await db.CreationUploads.CountAsync(x => x.UserId == authority.User.Id && !x.Deleted, ct) < 20, "Delete unused private uploads before adding more.");
        var row = new CreationUpload { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = authority.User.Id, ProfileId = r.ProfileId, OperationId = r.OperationId, Fingerprint = fingerprint, MediaType = r.MediaType, Content = r.Content, AltText = r.AltText, Rights = r.Rights }; db.CreationUploads.Add(row); return row.Id;
    }, ct);
    private async Task Materials(PrivateCreation row, CancellationToken ct)
    {
        foreach (var id in Read<Guid[]>(row.UploadsJson))
        {
            if (!await db.CreationUploads.AnyAsync(x => x.Id == id && x.TenantId == row.TenantId && x.UserId == row.UserId && x.ProfileId == row.ProfileId && !x.Deleted, ct)) throw new CoreAccessException();
            CreationRules.Require(!await db.Creations.AnyAsync(x => x.Id != row.Id && x.TenantId == row.TenantId && x.State != "Deleted" && x.UploadsJson.Contains(id.ToString()), ct), "Private uploads bind one creation; upload separately for another pack.");
        }
    }
    public Task DeleteUploadAsync(CustomerScope s, Guid id, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        var user = await customers.MeAsync(s, ct); var upload = await db.CreationUploads.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == user.Id && x.Id == id, ct) ?? throw new CoreAccessException(); await customers.TalkAuthority(s, upload.ProfileId, ct);
        CreationRules.Require(!await db.Creations.AnyAsync(x => x.TenantId == s.TenantId && x.State != "Deleted" && x.UploadsJson.Contains(id.ToString()), ct), "Delete the owning creation before removing its material."); upload.Content = []; upload.AltText = ""; upload.Deleted = true; return true;
    }, ct);
    public async Task<Guid> SubmitAsync(CustomerScope s, CreationSubmit r, CancellationToken ct)
    {
        var id = await Change(s.TenantId, async () =>
        {
            var a = await customers.TalkAuthority(s, r.ProfileId, ct); var t = await db.CreationTemplates.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == r.TemplateId, ct) ?? throw new CoreAccessException(); var cfg = Read<CreationTemplateRequest>(t.SettingsJson); CreationRules.Input(r.Input, cfg.Themes); CreationRules.Require(r.OperationId != Guid.Empty && r.UploadIds.Length <= 4 && r.UploadIds.Distinct().Count() == r.UploadIds.Length && cfg.AgeBands.Contains(a.Profile.AgeBand) && a.Profile.Locale == "en-IN", "Stable scoped template/profile/upload request required."); await Guard(t, "Generate", ct);
            var old = await db.Creations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct); if (old != null) { CreationRules.Require(old.UserId == a.User.Id && old.Fingerprint == Hash(r), "Creation replay differs."); return old.Id; }
            CreationRules.Require(await db.Creations.CountAsync(x => x.UserId == a.User.Id && (x.State == "Queued" || x.State == "Running" || x.State == "Preparing"), ct) < 3, "Three active creations maximum.");
            var row = new PrivateCreation { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = a.User.Id, ProfileId = r.ProfileId, TemplateId = t.Id, OperationId = r.OperationId, Fingerprint = Hash(r), InputJson = Json(r.Input), UploadsJson = Json(r.UploadIds) }; await Materials(row, ct); db.Creations.Add(row); return row.Id;
        }, ct);
        var current = await Own(s, id, ct); if (current.Row.State != "Preparing") return id;
        Guid? reservation = null;
        try
        {
            reservation = await customers.ReserveAsync(s, new(Op(id, "reserve"), r.ProfileId, "create:" + id, Read<CreationTemplateRequest>(current.Template.SettingsJson).Credits), ct);
            var admitted = await Change(s.TenantId, async () => { var x = await Own(s, id, ct); if (x.Row.State != "Preparing") return false; x.Row.ReservationId = reservation; x.Row.State = "Queued"; Notice(x.Row, "Queued"); return true; }, ct);
            if (!admitted) await customers.CancelReservationAsync(s, reservation.Value, new(Op(id, "release")), CancellationToken.None);
        }
        catch { if (reservation != null) await customers.CancelReservationAsync(s, reservation.Value, new(Op(id, "release")), CancellationToken.None); await Change(s.TenantId, async () => { var x = await Own(s, id, CancellationToken.None); if (x.Row.State == "Preparing") { x.Row.State = "Failed"; x.Row.Outcome = "CreditAdmissionFailed"; } return true; }, CancellationToken.None); throw; }
        return id;
    }
    private void Notice(PrivateCreation r, string kind) { db.CreationEvents.Add(new() { Id = Guid.NewGuid(), TenantId = r.TenantId, CreationId = r.Id, Kind = kind }); Events.Add(1, new KeyValuePair<string, object?>("state", kind)); }
    private async Task<CreationView> View(PrivateCreation row, CreationTemplate t, CancellationToken ct)
    {
        var cfg = Read<CreationTemplateRequest>(t.SettingsJson); EditorialPacket? packet = null; var withheld = false;
        if (row.State != "Deleted") { try { packet = await Guard(t, "Export", ct); } catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException or RuntimeValidationException or CoreAccessException) { withheld = true; } }
        var html = !withheld && row.State is "Ready" or "Approved" ? await db.CreationRevisions.Where(x => x.CreationId == row.Id && x.Version == row.Version).Select(x => x.Html).SingleOrDefaultAsync(ct) : null;
        return new(row.Id, t.Id, row.State, withheld ? "SourceOrTemplateWithheld" : row.Outcome, row.Version, Version(row), cfg.Credits, row.ChargedCredits, row.State == "Queued" ? await db.Creations.CountAsync(x => x.TenantId == row.TenantId && x.UserId == row.UserId && x.State == "Queued" && x.CreatedUtc <= row.CreatedUtc, ct) : 0, row.Review, !withheld && row.PublicEligible, html, packet?.Citations ?? [], row.ReservationId, cfg.Adapter, row.Cleanup, row.State == "Deleted" ? null : Read<CreationInput>(row.InputJson));
    }
    public async Task<CreationView> InspectAsync(CustomerScope s, Guid id, CancellationToken ct) { var (row, t) = await Own(s, id, ct); return await View(row, t, ct); }
    public async Task<CreationView[]> ListAsync(CustomerScope s, CancellationToken ct) { var u = await customers.MeAsync(s, ct); var ids = await db.Creations.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UserId == u.Id).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => x.Id).ToArrayAsync(ct); var result = new List<CreationView>(); foreach (var id in ids) result.Add(await InspectAsync(s, id, ct)); return result.ToArray(); }
    private async Task<CreationRender> RenderRequest(PrivateCreation row, CreationTemplate t, CancellationToken ct)
    {
        var cfg = Read<CreationTemplateRequest>(t.SettingsJson); var packet = await Guard(t, "Generate", ct); await Materials(row, ct); var materials = new List<CreationMaterial>();
        foreach (var id in Read<Guid[]>(row.UploadsJson)) { var x = await db.CreationUploads.SingleAsync(x => x.Id == id, ct); materials.Add(new(x.MediaType, x.Content, x.AltText, x.Rights)); }
        foreach (var id in cfg.MediaVersionIds) { var export = await mediaLibrary.ExportAsync(Staff(t), cfg.UniverseId, id, "Reuse", ct); var material = Read<MediaLibrary.Material>((await db.MediaVersions.AsNoTracking().SingleAsync(x => x.Id == id, ct)).SnapshotJson); var audience = await db.Contents.Where(x => x.Id == packet.ContentId).Select(x => x.AudienceId).SingleAsync(ct); CreationRules.Require(material.Info.Mime == "image/png" && material.Request.AudienceId == audience && material.Request.Locale == "en-IN", "Reviewed illustration must match the source audience/locale."); materials.Add(new("image/png", export.Bytes, material.Request.AccessibilityDescription, "Licensed", Json(material.Request.License))); }
        var character = Read<RegistryCharacterRules>((await db.RegistryRevisions.AsNoTracking().SingleAsync(x => x.Id == cfg.CharacterProfileRevisionId, ct)).RulesJson);
        return new(row.Id, cfg.Format, Read<CreationInput>(row.InputJson), packet.Current!.Title, packet.Current.Body, packet.Citations, materials.ToArray(), character.Name);
    }
    private async Task Append(PrivateCreation row, CreationRenderResult result, CancellationToken ct)
    {
        CreationRules.Require(result.Cost == 0 && result.Currency == "INR" && result.Html.Length <= 2000000, "Only bounded local zero-provider-cost output is qualified."); row.Version++; db.CreationRevisions.Add(new() { Id = Guid.NewGuid(), TenantId = row.TenantId, CreationId = row.Id, Version = row.Version, Html = result.Html, Sha256 = Hash(result.Html) }); row.State = "Ready"; row.Review = "Pending"; row.PublicEligible = false; Notice(row, "Ready"); await Task.CompletedTask;
    }
    public Task EditAsync(CustomerScope s, Guid id, CreationEdit r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        var (row, t) = await Own(s, id, ct); if (await Replay(row, r.OperationId, "edit", r, ct)) return true; CreationRules.Require(Version(row) == r.RowVersion && row.State is "Ready" or "Approved" && row.Version < 20, "Reload an editable bounded creation."); CreationRules.Input(r.Input, Read<CreationTemplateRequest>(t.SettingsJson).Themes); row.InputJson = Json(r.Input); var output = await Renderer(Read<CreationTemplateRequest>(t.SettingsJson).Adapter).RenderAsync(await RenderRequest(row, t, ct), ct); await Append(row, output, ct); return true;
    }, ct);
    private async Task<bool> Replay(PrivateCreation row, Guid op, string kind, object request, CancellationToken ct)
    {
        CreationRules.Require(op != Guid.Empty, "Stable creation operation required."); var old = await db.CreationCommands.SingleOrDefaultAsync(x => x.TenantId == row.TenantId && x.OperationId == op, ct); if (old != null) { CreationRules.Require(old.CreationId == row.Id && old.Kind == kind && old.Fingerprint == Hash(request), "Creation command replay differs."); return true; }
        db.CreationCommands.Add(new() { Id = Guid.NewGuid(), TenantId = row.TenantId, CreationId = row.Id, OperationId = op, Kind = kind, Fingerprint = Hash(request) }); return false;
    }
    public Task ReviewAsync(CustomerScope s, Guid id, CreationReview r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        var (row, t) = await Own(s, id, ct); if (await Replay(row, r.OperationId, "review", r, ct)) return true; CreationRules.Require(Version(row) == r.RowVersion && row.State == "Ready" && r.Decision is "Approve" or "Reject", "Current creation review required."); MediaRules.Attest(r.Mode, r.Evidence); var packet = await Guard(t, "Export", ct);
        if (r.SharingConsent) { await Admin(new(s.TenantId, s.Principal.Identity!.Name!), ct); await Guard(t, "Publish", ct); CreationRules.Require(r.Mode == "Manual" && packet.Approvals.Any(x => x.Current && x.Mode == "Manual"), "Genuine publication review required; fixture cannot authorize sharing."); }
        row.Review = r.Decision; row.PublicEligible = r.Decision == "Approve" && r.SharingConsent; row.State = r.Decision == "Approve" ? "Approved" : "Ready"; Notice(row, "Review" + r.Decision); return true;
    }, ct);
    public async Task<CreationPackage> ExportAsync(CustomerScope s, Guid id, CancellationToken ct)
    {
        var (row, t) = await Own(s, id, ct); CreationRules.Require(row.State == "Approved" && row.Review == "Approve", "Review the current private creation before export."); await Guard(t, "Export", ct); var v = await View(row, t, ct); var rev = await db.CreationRevisions.AsNoTracking().SingleAsync(x => x.CreationId == id && x.Version == row.Version, ct); var cfg = Read<CreationTemplateRequest>(t.SettingsJson); var videos = new List<CreationVideo>();
        if (cfg.WatchProductionId.HasValue) { var master = await watch.ExportAsync(Staff(t), cfg.UniverseId, cfg.WatchProductionId.Value, "Export", ct); var artifact = master.Production.Artifacts.First(); var file = await watch.PreviewAsync(Staff(t), cfg.UniverseId, artifact.Id, ct); videos.Add(new(artifact.Id, artifact.Sha256, file.Bytes, master.Vtt, master.Srt)); }
        return new(id, row.Version, rev.Sha256, v.Html!, v.Citations, "Private personalized fiction; source and material licenses remain applicable. Optional video is the exact separately approved master, without personalized video regeneration. No public URL is created.", row.PublicEligible, cfg.RevisionId, videos.ToArray());
    }
    internal async Task AssertLicensedRevision(Guid tenant, Guid issuer, Guid id, int version, string sha, CancellationToken ct)
    {
        var row = await db.Creations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id && x.UserId == issuer && x.State != "Deleted", ct) ?? throw new CoreAccessException();
        var profile = await db.CustomerProfiles.AsNoTracking().SingleAsync(x => x.Id == row.ProfileId, ct);
        CreationRules.Require(!profile.Deleted && await db.CustomerUsers.AnyAsync(x => x.Id == issuer && x.State == "Active", ct) && await db.CustomerMembers.AnyAsync(x => x.UserId == issuer && x.HouseholdId == profile.HouseholdId && x.Enabled && (profile.AgeBand == "Adult" || (x.Role == "Owner" || x.Role == "Parent")), ct), "Original content owner/guardian authority was withdrawn.");
        var t = await db.CreationTemplates.AsNoTracking().SingleAsync(x => x.Id == row.TemplateId, ct); await Guard(t, "Export", ct);
        CreationRules.Require(await db.CreationRevisions.AnyAsync(x => x.CreationId == id && x.Version == version && x.Sha256 == sha && x.Html != null, ct), "Exact licensed content revision is no longer retained.");
    }
    public async Task CancelAsync(CustomerScope s, Guid id, CancellationToken ct)
    {
        var data = await Change(s.TenantId, async () => { var (row, _) = await Own(s, id, ct); CreationRules.Require(row.State is "Preparing" or "Queued" or "Cancelled", "Only undispatched work can be cancelled/refunded; delete accepted work without promising a credit refund."); row.ReservationId ??= await db.CustomerReservations.Where(x => x.TenantId == row.TenantId && x.UserId == row.UserId && x.UsageKey == "create:" + row.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct); row.State = "Cancelled"; row.PublicEligible = false; Notice(row, "Cancelled"); return row.ReservationId; }, ct);
        if (data != null && (await db.CustomerReservations.AsNoTracking().SingleAsync(x => x.Id == data, ct)).State == "Reserved") await customers.CancelReservationAsync(s, data.Value, new(Op(id, "release")), ct);
    }
    public async Task DeleteAsync(CustomerScope s, Guid id, CancellationToken ct)
    {
        var existing = await Own(s, id, ct); if (existing.Row.State is "Preparing" or "Queued") await CancelAsync(s, id, ct);
        await Change(s.TenantId, async () => { var (row, _) = await Own(s, id, ct); CreationRules.Require(row.State is not ("Running" or "Settling" or "SettlingFailed"), "Wait for the original admitted local operation to settle before deleting."); await Erase(db, row, ct); return true; }, ct);
    }
    internal static async Task Erase(CoreDbContext db, PrivateCreation row, CancellationToken ct)
    {
        row.State = "Deleted"; row.InputJson = "{}"; row.PublicEligible = false; row.Review = "Deleted"; row.Cleanup = "Pending";
        foreach (var rev in await db.CreationRevisions.Where(x => x.CreationId == row.Id).ToArrayAsync(ct)) rev.Html = null;
        foreach (var id in Read<Guid[]>(row.UploadsJson)) { var upload = await db.CreationUploads.SingleAsync(x => x.Id == id, ct); upload.Content = []; upload.AltText = ""; upload.Deleted = true; }
        row.UploadsJson = "[]";
        foreach (var bundle in await db.CommerceProducts.Where(x => x.TenantId == row.TenantId && x.CreationId == row.Id).ToArrayAsync(ct)) { bundle.PackageJson = null; bundle.Enabled = false; foreach (var order in await db.CommerceOrders.Where(x => x.ProductId == bundle.Id && x.State == "Active").ToArrayAsync(ct)) order.State = "Withheld"; }
    }
    public async Task<CreationNotice[]> NoticesAsync(CustomerScope s, CancellationToken ct) { var u = await customers.MeAsync(s, ct); return await db.CreationEvents.AsNoTracking().Where(x => x.TenantId == s.TenantId && db.Creations.Any(c => c.Id == x.CreationId && c.UserId == u.Id)).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => new CreationNotice(x.Id, x.CreationId, x.Kind, x.CreatedUtc)).ToArrayAsync(ct); }
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = await db.Creations.AsNoTracking().Where(x => x.State == "Queued" || x.State == "Preparing" && x.DeadlineUtc <= now || (x.State == "Running" || x.State == "Settling" || x.State == "SettlingFailed") && x.LeaseUntilUtc < now || x.State == "Deleted" && x.Cleanup == "Pending").OrderBy(x => x.CreatedUtc).FirstOrDefaultAsync(ct);
        if (candidate == null) return false;
        var t = await db.CreationTemplates.AsNoTracking().SingleAsync(x => x.Id == candidate.TemplateId, ct); var cfg = Read<CreationTemplateRequest>(t.SettingsJson);
        if (candidate.State == "Deleted") { await Renderer(cfg.Adapter).CleanupAsync(candidate.Id, ct); await Change(candidate.TenantId, async () => { var row = await db.Creations.SingleAsync(x => x.Id == candidate.Id, ct); row.Cleanup = "CompletedLocalNoExternalArtifacts"; Notice(row, "Deleted"); return true; }, ct); return true; }
        var lease = Guid.NewGuid();
        var claimed = await Change(candidate.TenantId, async () =>
        {
            var row = await db.Creations.SingleAsync(x => x.Id == candidate.Id, ct);
            if ((row.State is "Running" or "Settling" or "SettlingFailed") && row.LeaseUntilUtc >= DateTimeOffset.UtcNow || row.State is not ("Queued" or "Preparing" or "Running" or "Settling" or "SettlingFailed")) return false;
            row.ReservationId ??= await db.CustomerReservations.Where(x => x.TenantId == row.TenantId && x.UserId == row.UserId && x.UsageKey == "create:" + row.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (row.State is not ("Settling" or "SettlingFailed"))
            {
                if (row.Attempts >= 3 || row.DeadlineUtc <= DateTimeOffset.UtcNow || row.ReservationId == null) { row.State = "SettlingFailed"; row.Outcome = "CreationDeadlineAttemptsOrUnadmittedInterruption"; }
                else { row.State = "Running"; row.Attempts++; }
            }
            row.Lease = lease; row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(2); return true;
        }, ct);
        if (!claimed) return true;
        var rowNow = await db.Creations.SingleAsync(x => x.Id == candidate.Id, ct);
        if (rowNow.State == "Running")
        {
            CreationRenderResult? result = null; var outcome = "Completed";
            try
            {
                var profile = await db.CustomerProfiles.AsNoTracking().SingleAsync(x => x.Id == rowNow.ProfileId, ct);
                CreationRules.Require(!profile.Deleted && await db.CustomerUsers.AnyAsync(x => x.Id == rowNow.UserId && x.State == "Active", ct) && await db.CustomerMembers.AnyAsync(x => x.UserId == rowNow.UserId && x.HouseholdId == profile.HouseholdId && x.Enabled && (profile.AgeBand == "Adult" || x.Role == "Owner" || x.Role == "Parent"), ct), "Current owner/guardian authority required.");
                await customers.DispatchAsync(Staff(t), rowNow.ReservationId!.Value, new(Op(rowNow.Id, "dispatch"), cfg.Adapter, t.Id.ToString(), rowNow.Id.ToString()), ct);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromMinutes(2));
                result = await Renderer(cfg.Adapter).RenderAsync(await RenderRequest(rowNow, t, deadline.Token), deadline.Token); await Guard(t, "Generate", deadline.Token);
                CreationRules.Require(result.Cost == 0 && result.Currency == "INR" && result.Html.Length <= 2000000, "Qualified bounded local output required.");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { outcome = "LocalCompositionDeadline"; }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) when (e is RuntimeValidationException or CoreAccessException or RightsBlockedException or EvidenceBlockedException) { outcome = "Blocked"; }
            catch (Exception) { outcome = "FailedLocalComposition"; }
            // Only the current worker can persist the result that authorizes credit settlement.
            var accepted = await Change(candidate.TenantId, async () =>
            {
                var row = await db.Creations.SingleAsync(x => x.Id == candidate.Id, ct);
                if (row.Lease != lease || row.State != "Running" || row.LeaseUntilUtc <= DateTimeOffset.UtcNow) return false;
                if (result != null && outcome == "Completed") { await Append(row, result, ct); row.State = "Settling"; }
                else row.State = "SettlingFailed";
                row.Outcome = outcome; return true;
            }, ct);
            if (!accepted) return true;
        }
        var persisted = await db.Creations.AsNoTracking().SingleAsync(x => x.Id == candidate.Id, ct);
        if (persisted.State is not ("Settling" or "SettlingFailed")) return true;
        var successful = persisted.State == "Settling";
        // Every recovery worker derives the same receipt from durable acceptance, never from an expired worker's transient result.
        var ledgerScope = Staff(t);
        if (!await db.Members.AnyAsync(x => x.TenantId == t.TenantId && x.Actor == t.Actor && x.Permission == "Administrator", ct)) ledgerScope = new(t.TenantId, await db.Members.Where(x => x.TenantId == t.TenantId && x.Permission == "Administrator").OrderBy(x => x.Actor).Select(x => x.Actor).FirstOrDefaultAsync(ct) ?? throw new CoreAccessException());
        if (persisted.ReservationId.HasValue)
        {
            var reservation = await db.CustomerReservations.AsNoTracking().SingleAsync(x => x.Id == persisted.ReservationId, ct);
            if (reservation.State == "Reserved") await customers.ReleaseCreationCredit(ledgerScope, reservation.Id, Op(persisted.Id, "release-staff"), ct);
            if (reservation.State is "Running" or "Unknown") await customers.SettleAsync(ledgerScope, reservation.Id, new(Op(persisted.Id, "settle"), cfg.Adapter, t.Id.ToString(), persisted.Id.ToString(), successful ? "Completed" : "Failed", successful ? cfg.Credits : 0, "evidence:local-create:" + persisted.Id), ct);
        }
        await Change(candidate.TenantId, async () =>
        {
            var row = await db.Creations.SingleAsync(x => x.Id == candidate.Id, ct); if (row.Lease != lease || row.State is not ("Settling" or "SettlingFailed")) return false;
            row.State = successful ? "Ready" : "Failed"; row.ChargedCredits = successful ? cfg.Credits : 0; row.Lease = null; row.LeaseUntilUtc = null; Notice(row, row.State); return true;
        }, ct); return true;
    }
}
public static class CreationServices { public static IServiceCollection AddCreationStudio(this IServiceCollection s) { s.AddScoped<ICreationStudio, CreationStudio>(); return s; } }
