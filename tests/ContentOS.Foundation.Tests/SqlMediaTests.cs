using System.Net.Http.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlMediaTests
{
    [SqlFact]
    public async Task New_guest_media_is_reviewed_bound_reused_promoted_and_storage_moved_without_identity_or_rights_loss()
    {
        using var root = MediaTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var c = await RegistryTestFixture.Character(s, f, "m16-coach", "Coach", "EpisodeOnly");
        var library = s.GetRequiredService<IMediaLibrary>(); var bytes = MediaFiles.ReferenceCard(c.Profile.Rules.VisualAnchor, ["#245441"]); var upload = MediaTestFixture.Upload(f, c, "sheet", bytes); var image = await library.ImportAsync(f.Owner, f.Universe, upload, default); Assert.Equal(image.Id, (await library.ImportAsync(f.Owner, f.Universe, upload, default)).Id);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => library.ExportAsync(f.Owner, f.Universe, image.Id, "Reuse", default)); await MediaTestFixture.Review(s, f, image);
        var voice = await library.ImportAsync(f.Owner, f.Universe, MediaTestFixture.Upload(f, c, "voice", ToneSpeechFixtureProvider.Tone(), "Voice"), default); await MediaTestFixture.Review(s, f, voice);
        var copy = await library.ImportAsync(f.Owner, f.Universe, MediaTestFixture.Upload(f, c, "separate-reference", bytes), default); Assert.NotEqual(image.AssetId, copy.AssetId); Assert.Equal(image.StorageAssetId, copy.StorageAssetId);
        var package = await MediaTestFixture.Package(s, f, c, image, voice); var binding = await library.BindPackageAsync(f.Owner, f.Universe, package.Id, Guid.NewGuid(), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, binding);
        var registry = s.GetRequiredService<IUniverseRegistry>(); var plan = await registry.CastAsync(f.Owner, f.Universe, RegistryTestFixture.Brief(f, ["Coach"]) with { Mode = "Production", Budget = new(2, 0, "INR", true), PinnedChoices = [new(c.CharacterId, "Coach", binding)] }, default); Assert.Single(plan.Selected); Assert.Equal(c.CharacterId, plan.Selected[0].Character.CharacterId);
        c = (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single(x => x.CharacterId == c.CharacterId); await registry.ControlCharacterAsync(f.Owner, f.Universe, c.CharacterId, new(Guid.NewGuid(), c.RowVersion, "PromoteGuest", "fixture:M16-recurring-same-identity"), default);
        var move = new MoveMedia(Guid.NewGuid(), image.Id, "archive"); var moved = await library.MoveAsync(f.Owner, f.Universe, move, default); Assert.Equal(image.StorageAssetId, moved.AssetId); Assert.Equal(image.Info.Sha256, moved.Sha256); Assert.Equal(moved, await library.MoveAsync(f.Owner, f.Universe, move, default));
        var exported = await library.ExportAsync(f.Owner, f.Universe, image.Id, "Export", default); Assert.Equal(bytes, exported.Bytes); Assert.Equal("archive", exported.StoreKey); Assert.Equal(image.Id, exported.Version.Id); Assert.Equal(image.AssetReferenceId, exported.Version.AssetReferenceId); Assert.Equal(System.Text.Json.JsonSerializer.Serialize(MediaTestFixture.License), System.Text.Json.JsonSerializer.Serialize(exported.Version.License));
        using var again = MediaTestFixture.Services(); using var fresh = again.CreateScope(); Assert.Equal(bytes, (await fresh.ServiceProvider.GetRequiredService<IMediaLibrary>().ExportAsync(f.Owner, f.Universe, image.Id, "Reuse", default)).Bytes);
        await Assert.ThrowsAsync<RightsBlockedException>(() => library.ExportAsync(f.Owner, f.Universe, image.Id, "Publish", default));
    }
    [SqlFact]
    public async Task Generation_switches_preserve_references_unknown_outcomes_and_duplicate_callback_bindings()
    {
        using var root = MediaTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var c = await RegistryTestFixture.Character(s, f, "m16-reference", "Coach"); var library = s.GetRequiredService<IMediaLibrary>(); var bytes = MediaFiles.ReferenceCard("fixed", ["#245441"]); var image = await library.ImportAsync(f.Owner, f.Universe, MediaTestFixture.Upload(f, c, "input", bytes), default); await MediaTestFixture.Review(s, f, image);
        var a = await MediaTestFixture.Profile(s, f); var b = await MediaTestFixture.Profile(s, f, "media.image.basic.v1"); var unknown = await MediaTestFixture.Profile(s, f, "media.image.unknown-fixture.v1");
        var output = MediaTestFixture.Upload(f, c, "generated", []) with { ExpectedSha256 = "" }; var request = new GenerateMedia(Guid.NewGuid(), a, output, "Original preview", [image.Id], [], 0);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => library.GenerateAsync(f.Owner, f.Universe, request with { OperationId = Guid.NewGuid(), ProfileId = b }, default));
        var run = await library.GenerateAsync(f.Owner, f.Universe, request, default); Assert.Equal(run.Id, (await library.GenerateAsync(f.Owner, f.Universe, request, default)).Id); await MediaTestFixture.Drain(s); run = (await library.GenerationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == run.Id); Assert.Equal("Completed", run.State); var accepted = await library.ExportAsync(f.Owner, f.Universe, run.AcceptedVersionId!.Value, "Read", default); Assert.Equal(bytes, accepted.Bytes); Assert.Contains(image.Id, accepted.Version.ParentVersionIds);
        var callback = new AcceptMediaResult(Guid.NewGuid(), run.Id, run.ExternalResourceId!, bytes, MediaRules.Hash(bytes), 0); Assert.Equal(accepted.Version.Id, (await library.AcceptAsync(f.Owner, f.Universe, callback, default)).Id); Assert.Equal(accepted.Version.Id, (await library.AcceptAsync(f.Owner, f.Universe, callback, default)).Id);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => library.AcceptAsync(f.Owner, f.Universe, callback with { ResourceId = "foreign-resource" }, default));
        var pending = await library.GenerateAsync(f.Owner, f.Universe, request with { OperationId = Guid.NewGuid(), ProfileId = unknown, Output = output with { Key = "unknown" } }, default); await MediaTestFixture.Drain(s); Assert.Equal("Unknown", (await library.GenerationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == pending.Id).State);
        await library.ReconcileAsync(f.Owner, f.Universe, new(Guid.NewGuid(), pending.Id), default); Assert.Equal("Completed", (await library.GenerationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == pending.Id).State);
        var profile = (await library.ProfilesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == a); await library.DisableAsync(f.Owner, f.Universe, a, new(Guid.NewGuid(), profile.RowVersion, "fixture:kill"), default); await Assert.ThrowsAsync<EvidenceBlockedException>(() => library.GenerateAsync(f.Owner, f.Universe, request with { OperationId = Guid.NewGuid() }, default));
    }
    [SqlFact]
    public async Task Scope_consent_quality_and_withdrawal_are_hard_gates_and_snapshots_are_immutable()
    {
        using var root = MediaTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var c = await RegistryTestFixture.Character(s, f, "m16-quality", "Coach"); var library = s.GetRequiredService<IMediaLibrary>(); var image = await library.ImportAsync(f.Owner, f.Universe, MediaTestFixture.Upload(f, c, "sheet", MediaFiles.ReferenceCard("fixture", [])), default);
        await Assert.ThrowsAsync<CoreAccessException>(() => library.ExportAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, image.Id, "Read", default)); await Assert.ThrowsAsync<CoreAccessException>(() => library.ImportAsync(new(f.Owner.TenantId, "m10-reader"), f.Universe, MediaTestFixture.Upload(f, c, "reader", MediaFiles.ReferenceCard("fixture", [])), default));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => library.ReviewAsync(f.Owner, f.Universe, image.Id, new(Guid.NewGuid(), image.RowVersion, "Approve", "Fixture", "fixture:unresolved", ["Wrong character silhouette"]), default));
        var a = await MediaTestFixture.Profile(s, f); var generation = new GenerateMedia(Guid.NewGuid(), a, MediaTestFixture.Upload(f, c, "clone", []) with { ExpectedSha256 = "", License = MediaTestFixture.License with { LikenessConsentReference = "manual:permission-not-a-cloning-capability" } }, "preview", [], [], 0); await Assert.ThrowsAsync<EvidenceBlockedException>(() => library.GenerateAsync(f.Owner, f.Universe, generation, default));
        await MediaTestFixture.Review(s, f, image); image = (await library.AssetsAsync(f.Owner, f.Universe, default)).SelectMany(x => x.Versions).Single(x => x.Id == image.Id); await library.ReviewAsync(f.Owner, f.Universe, image.Id, new(Guid.NewGuid(), image.RowVersion, "Withdraw", "Fixture", "fixture:withdrawal", []), default); await Assert.ThrowsAsync<EvidenceBlockedException>(() => library.ExportAsync(f.Owner, f.Universe, image.Id, "Reuse", default));
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var row = await db.MediaVersions.SingleAsync(x => x.Id == image.Id); row.SnapshotJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [SqlFact]
    public async Task Actual_http_roles_headers_binary_preview_and_new_presentation_quality_are_enforced()
    {
        using var root = MediaTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s, "integration-test-Administrator"); var c = await RegistryTestFixture.Character(s, f, "m16-http-coach", "Coach"); var library = s.GetRequiredService<IMediaLibrary>();
        var image = await library.ImportAsync(f.Owner, f.Universe, MediaTestFixture.Upload(f, c, "http-sheet", MediaFiles.ReferenceCard("http", [])), default); await MediaTestFixture.Review(s, f, image);
        var voice = await library.ImportAsync(f.Owner, f.Universe, MediaTestFixture.Upload(f, c, "http-voice", ToneSpeechFixtureProvider.Tone(), "Voice"), default); await MediaTestFixture.Review(s, f, voice); var package = await MediaTestFixture.Package(s, f, c, image, voice);
        using var app = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Runtime:Enabled"] = "true",
                ["ConnectionStrings:ContentOS"] = LessonTestFixture.Connection,
                ["Staff:Administrator:0"] = f.Owner.Actor,
                ["Staff:Reader:0"] = "integration-test-Reader",
                ["Media:WorkerEnabled"] = "false",
                ["Factory:WorkerEnabled"] = "false",
                ["Trends:RecheckWorkerEnabled"] = "false",
                ["Read:RetentionWorkerEnabled"] = "false",
                ["Knowledge:IndexWorkerEnabled"] = "false",
                ["Workflows:WorkerEnabled"] = "false",
                ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets")
            })); b.ConfigureServices(TestAuthentication.Configure);
        });
        using (var apiScope = app.Services.CreateScope()) { var apiDb = apiScope.ServiceProvider.GetRequiredService<RuntimeDbContext>(); var persisted = await apiDb.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == image.StorageAssetId); Assert.True(persisted != null, "Runtime asset missing in " + apiDb.Database.GetDbConnection().Database); Assert.Equal(f.Owner.Actor, persisted!.Owner); var core = apiScope.ServiceProvider.GetRequiredService<CoreDbContext>(); var bound = await core.MediaVersions.AsNoTracking().SingleAsync(x => x.Id == image.Id); Assert.Equal(persisted.Owner, bound.StorageOwner); }
        using var client = app.CreateClient(); var path = $"/api/v1/media/tenants/{f.Owner.TenantId}/universes/{f.Universe}";
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/staff/media")).StatusCode); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        var preview = await client.GetAsync(path + "/versions/" + image.Id + "/preview"); Assert.True(preview.StatusCode == System.Net.HttpStatusCode.OK, await preview.Content.ReadAsStringAsync()); Assert.True(preview.Headers.CacheControl!.NoStore); Assert.Equal("image/png", preview.Content.Headers.ContentType!.MediaType);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/move", new MoveMedia(Guid.NewGuid(), image.Id, "archive"))).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        var upload = MediaTestFixture.Upload(f, c, "http-upload", MediaFiles.ReferenceCard("second", [])); Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/assets", upload)).StatusCode); client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1"); Assert.Equal(System.Net.HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/assets", upload)).StatusCode);
        var page = await client.GetAsync("/staff/media"); Assert.Equal(System.Net.HttpStatusCode.OK, page.StatusCode); Assert.Contains("noindex", page.Headers.GetValues("X-Robots-Tag"));
        var alternate = await library.PackageAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "different-presentation", 1, package.Rules with { PresentationStyle = "Fictional adviser presentation" }), default);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => library.BindPackageAsync(f.Owner, f.Universe, alternate, Guid.NewGuid(), default));
        var current = (await library.PackagesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == alternate); await library.ReviewPackageAsync(f.Owner, f.Universe, alternate, new(Guid.NewGuid(), current.RowVersion, "Approve", "Fixture", "fixture:new-style-quality-reviewed", []), default);
        var binding = await library.BindPackageAsync(f.Owner, f.Universe, alternate, Guid.NewGuid(), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, binding);
        var registry = s.GetRequiredService<IUniverseRegistry>(); var presentationView = (await registry.PresentationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == binding);
        var orphan = await registry.RegisterPresentationAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "orphan-media-binding", 1, presentationView.Rules), default); await Assert.ThrowsAsync<EvidenceBlockedException>(() => RegistryTestFixture.Review(s, f.Owner, f.Universe, orphan));
        var db = s.GetRequiredService<CoreDbContext>(); var revision = await db.Assets.AsNoTracking().Where(x => x.Id == image.AssetReferenceId).Select(x => x.ContentRevisionId).SingleAsync();
        var brief = RegistryTestFixture.Brief(f, ["Coach"]) with { PinnedChoices = [new(c.CharacterId, "Coach", binding)] }; var episode = (await registry.EpisodesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Episode);
        await registry.ComposeAsync(f.Owner, f.Universe, new(Guid.NewGuid(), episode.RowVersion, brief, "None", [new(c.CharacterId, "FictionalDialogue", "I pause before acting.", ["curious"], [], true)], [], ContentRevisionId: revision), default);
        await new SourceRightsGuard(db).CheckAsync(f.Owner, revision, new("Export", "en-IN", "IN", "Text"), default);
        current = (await library.PackagesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == alternate); await library.ReviewPackageAsync(f.Owner, f.Universe, alternate, new(Guid.NewGuid(), current.RowVersion, "Withdraw", "Fixture", "fixture:package-withdrawal", []), default);
        Assert.Equal("Approved", (await s.GetRequiredService<IUniverseRegistry>().PresentationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == binding).ReviewStatus);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => new SourceRightsGuard(db).CheckAsync(f.Owner, revision, new("Export", "en-IN", "IN", "Text"), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => s.GetRequiredService<IUniverseRegistry>().CastAsync(f.Owner, f.Universe, RegistryTestFixture.Brief(f, ["Coach"]) with { Mode = "Production", Budget = new(2, 0, "INR", true), PinnedChoices = [new(c.CharacterId, "Coach", binding)] }, default));
    }
    [SqlFact]
    public async Task Queued_reference_revocation_and_disabled_profiles_prevent_submission_and_expired_leases_reconcile()
    {
        using var root = MediaTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var c = await RegistryTestFixture.Character(s, f, "m16-admission", "Coach"); var library = s.GetRequiredService<IMediaLibrary>();
        var reference = await library.ImportAsync(f.Owner, f.Universe, MediaTestFixture.Upload(f, c, "admission-ref", MediaFiles.ReferenceCard("admission", [])), default); await MediaTestFixture.Review(s, f, reference);
        var profile = await MediaTestFixture.Profile(s, f); var output = MediaTestFixture.Upload(f, c, "queued-reference", []) with { ExpectedSha256 = "" };
        var queued = await library.GenerateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), profile, output, "Preserve the reference", [reference.Id], [], 0), default);
        reference = (await library.AssetsAsync(f.Owner, f.Universe, default)).SelectMany(x => x.Versions).Single(x => x.Id == reference.Id); await library.ReviewAsync(f.Owner, f.Universe, reference.Id, new(Guid.NewGuid(), reference.RowVersion, "Withdraw", "Fixture", "fixture:before-dispatch", []), default); await MediaTestFixture.Drain(s);
        var run = (await library.GenerationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == queued.Id); Assert.Equal("Failed", run.State); Assert.Null(run.AcceptedVersionId); Assert.Null(run.ExternalResourceId); Assert.Equal(0, run.ActualCost);
        var disabled = await library.GenerateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), profile, output with { Key = "queued-disabled" }, "Controlled sample", [], [], 0), default); var p = (await library.ProfilesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == profile); await library.DisableAsync(f.Owner, f.Universe, profile, new(Guid.NewGuid(), p.RowVersion, "fixture:disable-before-dispatch"), default); await MediaTestFixture.Drain(s); Assert.Equal("Failed", (await library.GenerationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == disabled.Id).State);
        var unknown = await MediaTestFixture.Profile(s, f, "media.image.unknown-fixture.v1"); var lost = await library.GenerateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), unknown, output with { Key = "expired-lease" }, "Controlled original resource", [], [], 0), default);
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var row = await db.MediaGenerations.SingleAsync(x => x.Id == lost.Id); row.State = "Submitting"; row.LeaseToken = Guid.NewGuid(); row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); await MediaTestFixture.Drain(s);
        Assert.Equal("Unknown", (await library.GenerationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == lost.Id).State); await library.ReconcileAsync(f.Owner, f.Universe, new(Guid.NewGuid(), lost.Id), default); Assert.Equal("Completed", (await library.GenerationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == lost.Id).State);
    }
}
