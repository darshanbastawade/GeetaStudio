using System.Net;
using System.Net.Http.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlEditorialTests
{
    [SqlFact]
    public async Task Exact_approval_scheduling_dryrun_replay_and_restart_preserve_history_without_publication()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>();
        var request = EditorialTestFixture.Request(f); var p = await e.StartAsync(f.Owner, f.Universe, request, default); Assert.Equal("Brief", p.State); Assert.Equal(p.Id, (await e.StartAsync(f.Owner, f.Universe, request, default)).Id);
        p = await EditorialTestFixture.Approved(s, f, p); Assert.Equal("Approved", p.State); Assert.True(Assert.Single(p.Approvals).Current); Assert.True(p.HighRisk); Assert.Contains("PublicReleaseNotApproved", p.Blocks);
        p = await e.ActAsync(f.Owner, f.Universe, p.Id, EditorialTestFixture.Action(p, "Schedule"), default); var publish = EditorialTestFixture.Action(p, "PublishDryRun");
        p = await e.ActAsync(f.Owner, f.Universe, p.Id, publish, default); Assert.Equal("Published", p.State); Assert.Equal("DryRun", Assert.Single(p.Copies).Mode); Assert.Equal(p.Id, (await e.ActAsync(f.Owner, f.Universe, p.Id, publish, default)).Id);
        using var restart = root.CreateScope(); var read = await restart.ServiceProvider.GetRequiredService<IEditorialWorkbench>().InspectAsync(f.Owner, f.Universe, p.Id, default); Assert.Equal(p.Fingerprint, read.Fingerprint); Assert.Equal(p.History.Length, read.History.Length);
        var db = s.GetRequiredService<CoreDbContext>(); Assert.False((await db.Contents.AsNoTracking().SingleAsync(x => x.Id == p.ContentId)).PublicKnowledge); Assert.False((await db.SourceUses.SingleAsync(x => x.OperationId == publish.OperationId)).Published);
        db.ChangeTracker.Clear(); var approval = await db.EditorialApprovals.SingleAsync(x => x.Id == p.Approvals[0].Id); approval.Fingerprint = "changed"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
    }
    [SqlFact]
    public async Task Text_edit_expires_approval_and_old_core_approval_export_cannot_bypass_the_workbench()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>(); var core = s.GetRequiredService<IContentCore>();
        var p = await EditorialTestFixture.Approved(s, f, await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default)); var old = p;
        var child = await core.CreateAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f, "legacy-child").Draft!, default);
        await s.GetRequiredService<ISourceRegistry>().LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), null, p.RevisionId, child.Revisions[0].Id, null, "derived-from"), default);
        await core.ExportAsync(f.Owner, f.Universe, child.Id, default);
        p = await e.EditAsync(f.Owner, f.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, p.Current!.Title, "A revised original learner chooses a different action.", "Fiction", "Owned", "fixture:new-treatment"), default);
        Assert.Equal("Draft", p.State); Assert.All(p.Approvals, x => Assert.False(x.Current)); Assert.NotEqual(old.Fingerprint, p.Fingerprint); Assert.NotNull(p.Previous);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => core.ExportAsync(f.Owner, f.Universe, child.Id, default));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => core.ExportAsync(f.Owner, f.Universe, p.ContentId, default));
        var view = await core.ReadAsync(f.Owner, f.Universe, p.ContentId, default); view = await core.TransitionAsync(f.Owner, f.Universe, view.Id, new(Guid.NewGuid(), view.RowVersion, "Review"), default);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => core.TransitionAsync(f.Owner, f.Universe, view.Id, new(Guid.NewGuid(), view.RowVersion, "Approved"), default));
        p = await EditorialTestFixture.Approved(s, f, await e.InspectAsync(f.Owner, f.Universe, p.Id, default)); var exported = await core.ExportAsync(f.Owner, f.Universe, p.ContentId, default); Assert.Single(exported.Content.Revisions); Assert.Equal(p.RevisionId, exported.Content.Revisions[0].Id);
        view = await core.ReadAsync(f.Owner, f.Universe, p.ContentId, default); await core.ReviseAsync(f.Owner, f.Universe, view.Id, new(Guid.NewGuid(), view.RowVersion, "Changed by old core API", "A different original fictional scene.", "Fiction", "Owned", "fixture:legacy-edit"), default);
        var expired = await e.InspectAsync(f.Owner, f.Universe, p.Id, default); Assert.Contains("ApprovalExpired", expired.Blocks); await Assert.ThrowsAsync<EvidenceBlockedException>(() => core.ExportAsync(f.Owner, f.Universe, p.ContentId, default));
    }
    [SqlFact]
    public async Task Audio_swap_and_direct_asset_mutation_invalidate_exact_manifest_and_previews_remain_scoped()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>();
        var p = await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default); p = await e.AttachAssetAsync(f.Owner, f.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, "voice", "audio/wav", EditorialTestFixture.Wav(), "fixture:owned-wave"), default);
        p = await EditorialTestFixture.Approved(s, f, p); var old = p.Approvals[0].Fingerprint; var first = Assert.Single(p.Assets);
        var preview = await e.PreviewAssetAsync(new(f.Owner.TenantId, "m10-reader"), f.Universe, first.Id, default); Assert.Equal("audio/wav", preview.MediaType); Assert.Equal(EditorialTestFixture.Wav(), preview.Bytes);
        await Assert.ThrowsAsync<CoreAccessException>(() => e.PreviewAssetAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, first.Id, default));
        p = await e.AttachAssetAsync(f.Owner, f.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, "voice", "audio/wav", EditorialTestFixture.Wav(200), "fixture:changed-wave"), default);
        Assert.Equal("Draft", p.State); Assert.NotEqual(first.Sha256, Assert.Single(p.Assets).Sha256); Assert.All(p.Approvals, a => Assert.False(a.Current)); Assert.NotEqual(old, p.Fingerprint);
        p = await EditorialTestFixture.Approved(s, f, p); var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var asset = await db.Assets.SingleAsync(x => x.Id == p.Assets[0].AssetReferenceId); asset.RightsStatus = "Unverified"; await db.SaveChangesAsync();
        var expired = await e.InspectAsync(f.Owner, f.Universe, p.Id, default); Assert.Contains("ApprovalExpired", expired.Blocks); await Assert.ThrowsAsync<EvidenceBlockedException>(() => s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, p.ContentId, default));
    }
    [SqlFact]
    public async Task Genuine_approval_is_not_fabricated_from_a_fixture_report_and_policy_changes_expire_existing_approval()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>();
        var p = await EditorialTestFixture.Reviewed(s, f, await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => e.ActAsync(f.Owner, f.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, "Approve", "manual:nonexistent-human-release", "Manual"), default));
        p = await e.ActAsync(f.Owner, f.Universe, p.Id, EditorialTestFixture.Action(p, "Approve"), default);
        var db = s.GetRequiredService<CoreDbContext>(); var pack = new UniversePack { Id = Guid.NewGuid(), UniverseId = f.Universe, Version = 2 }; db.Packs.Add(pack); db.Policies.Add(new() { PackId = pack.Id, Key = "human-review", Rule = "Updated review policy." }); await db.SaveChangesAsync();
        p = await e.InspectAsync(f.Owner, f.Universe, p.Id, default); Assert.Contains("ApprovalExpired", p.Blocks); Assert.All(p.Approvals, a => Assert.False(a.Current));
    }
    [SqlFact]
    public async Task Source_correction_reaches_translations_episodes_exports_and_post_copies_and_keeps_external_outcomes_honest()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>(); var sources = s.GetRequiredService<ISourceRegistry>(); var source = await SourceTestFixture.CreateAsync(sources, f.Owner, f.Universe);
        var original = await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default);
        await sources.LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source.Snapshot, null, original.RevisionId, null, "adapted-from"), default); original = await EditorialTestFixture.Approved(s, f, await e.InspectAsync(f.Owner, f.Universe, original.Id, default));
        var all = new List<EditorialPacket> { original };
        foreach (var format in new[] { "Translation", "Episode", "Export", "Post" })
        {
            var p = await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f, format) with { Format = format }, default); await sources.LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), null, original.RevisionId, p.RevisionId, null, "derived-from"), default);
            p = await EditorialTestFixture.Approved(s, f, await e.InspectAsync(f.Owner, f.Universe, p.Id, default)); p = await e.ActAsync(f.Owner, f.Universe, p.Id, EditorialTestFixture.Action(p, "Schedule"), default); p = await e.ActAsync(f.Owner, f.Universe, p.Id, EditorialTestFixture.Action(p, "PublishDryRun"), default); all.Add(p);
        }
        var correction = await e.CorrectAsync(f.Owner, f.Universe, new(Guid.NewGuid(), null, source.Snapshot, "Fixture source correction notice.", "fixture:source-correction"), default);
        var tasks = await e.CorrectionsAsync(f.Owner, f.Universe, default); Assert.Equal(5, tasks.Count(x => x.Kind == "Internal")); Assert.Equal(4, tasks.Count(x => x.Kind == "External"));
        foreach (var p in all) { var current = await e.InspectAsync(f.Owner, f.Universe, p.Id, default); Assert.Equal("Corrected", current.State); Assert.True(current.SourceOutputWithheld); await Assert.ThrowsAsync<EvidenceBlockedException>(() => s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, p.ContentId, default)); }
        var external = tasks.First(x => x.Kind == "External"); await e.ResolveCorrectionAsync(new(f.Owner.TenantId, "m13-publisher"), f.Universe, external.Id, new(Guid.NewGuid(), external.RowVersion, "Unsupported", "fixture:platform-not-implemented"), default);
        var internalTask = tasks.First(x => x.Kind == "Internal"); await e.ResolveCorrectionAsync(new(f.Owner.TenantId, "m13-editor"), f.Universe, internalTask.Id, new(Guid.NewGuid(), internalTask.RowVersion, "Withdraw", "fixture:withdraw"), default);
        tasks = await e.CorrectionsAsync(f.Owner, f.Universe, default); Assert.Contains(tasks, x => x.State == "Unsupported");
        Assert.Contains(tasks.Single(x => x.Id == external.Id).Attempts, x => x.Action == "Unsupported" && x.EvidenceReference == "fixture:platform-not-implemented"); Assert.Contains(tasks, x => x.State == "Withdrawn");
        var remediatedItem = all.Single(x => x.ContentId == external.ContentId);
        Assert.Contains((await e.InspectAsync(f.Owner, f.Universe, remediatedItem.Id, default)).History, x => x.Action.Contains(external.Id.ToString()) && x.EvidenceReference == "fixture:platform-not-implemented");
        Assert.All(tasks, x => Assert.Equal(correction, x.CorrectionId));
        var longEvidence = "fixture:" + new string('x', 800);
        await e.ResolveCorrectionAsync(new(f.Owner.TenantId, "m13-publisher"), f.Universe, external.Id, new(Guid.NewGuid(), tasks.Single(x => x.Id == external.Id).RowVersion, "Attempted", longEvidence), default);
        tasks = await e.CorrectionsAsync(f.Owner, f.Universe, default);
        var retained = tasks.Single(x => x.Id == external.Id).Attempts;
        Assert.Equal(2, retained.Length); Assert.Contains(retained, x => x.EvidenceReference == longEvidence); Assert.Contains(retained, x => x.Action == "Unsupported");
        var replacement = await EditorialTestFixture.Approved(s, f, await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f, "independent-correction"), default));
        var unresolved = tasks.First(x => x.Kind == "Internal" && x.State == "Open");
        await e.ResolveCorrectionAsync(f.Owner, f.Universe, unresolved.Id, new(Guid.NewGuid(), unresolved.RowVersion, "Resolve", "fixture:verified-independent-replacement", replacement.ContentId), default);
        Assert.Contains(await e.CorrectionsAsync(f.Owner, f.Universe, default), x => x.Id == unresolved.Id && x.State == "Resolved");
        Assert.Single((await s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, replacement.ContentId, default)).Content.Revisions);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, unresolved.ContentId, default)); await Assert.ThrowsAsync<RuntimeValidationException>(() => e.ResolveCorrectionAsync(f.Owner, f.Universe, external.Id, new(Guid.NewGuid(), tasks.Single(t => t.Id == external.Id).RowVersion, "Confirmed", "fixture:not-real-confirmation"), default));
    }
    [SqlFact]
    public async Task Scoped_provider_validation_activation_rollback_and_disable_are_concurrent_and_secret_free()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>(); var admin = s.GetRequiredService<IAiAdministration>();
        var a = await admin.RegisterAsync(f.Owner, new(SqlAiTests.Settings(streaming: false)), default); var b = await admin.RegisterAsync(f.Owner, new(SqlAiTests.Settings("fixture.text.b", streaming: false)), default);
        async Task<Guid> Route(Guid profile, string version) => await e.RouteAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "ValidateAndSave", Guid.Empty, "", Settings: new("draft", version, [profile], .1m, "USD")), default);
        var ra = await Route(a, "a.v1"); var rb = await Route(b, "b.v1"); await e.RouteAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "Activate", ra, ""), default);
        var state = await e.ProvidersAsync(f.Owner, f.Universe, default); var first = Assert.Single(state.Heads);
        await e.RouteAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "Activate", rb, first.RowVersion), default);
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => e.RouteAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "Rollback", ra, first.RowVersion), default));
        state = await e.ProvidersAsync(f.Owner, f.Universe, default); await e.RouteAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "Rollback", ra, state.Heads[0].RowVersion), default);
        await e.RouteAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "Disable", Guid.Empty, "", a), default);
        state = await e.ProvidersAsync(f.Owner, f.Universe, default); Assert.True(state.Connections.Single(x => x.ProfileId == a).Killed);
        var json = System.Text.Json.JsonSerializer.Serialize(state); Assert.DoesNotContain("SecretRef", json); Assert.DoesNotContain("secretValue", json, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<CoreAccessException>(() => e.RouteAsync(new(f.Owner.TenantId, "m13-editor"), f.Universe, new(Guid.NewGuid(), "Activate", rb, ""), default));
        await Assert.ThrowsAsync<CoreAccessException>(() => e.ProvidersAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, default));
    }
    [SqlFact]
    public async Task Manual_trends_with_unverified_claim_no_teaching_match_stale_time_or_missing_transcript_cannot_be_approved()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>(); var source = await SourceTestFixture.CreateAsync(s.GetRequiredService<ISourceRegistry>(), f.Owner, f.Universe);
        var brief = new CastingBrief(f.Bible, f.Episode, f.Audience, "en-IN", ["teamwork"], ["reflection"], ["understand-context"], ["Lead"], ["original-timeline"], [], new(4, 0, "INR"), []);
        var r = new EditorialTrendInput(Guid.NewGuid(), "unverified", "Three outlets repeat one unverified claim.", [], [source.Snapshot], DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1), 0, "INR", brief, "Original fictional preview; facts not verified.", null, true);
        var trend = await e.SubmitTrendAsync(f.Owner, f.Universe, r, default); Assert.Contains("UnverifiedEvent", trend.Blocks); Assert.Contains("NoSuitableTeachingMatch", trend.Blocks); Assert.Contains("TranscriptUnavailable", trend.Blocks); Assert.Contains("StaleCandidate", trend.Blocks);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => e.ReviewTrendAsync(f.Owner, f.Universe, trend.Id, new(Guid.NewGuid(), trend.RowVersion, "Approve", "fixture:fluent-preview"), default));
        await e.ReviewTrendAsync(f.Owner, f.Universe, trend.Id, new(Guid.NewGuid(), trend.RowVersion, "Hold", "fixture:needs-independent-evidence"), default); Assert.Equal("Held", Assert.Single(await e.TrendsAsync(f.Owner, f.Universe, default)).State);
    }
    [SqlFact]
    public async Task Author_editor_publisher_admin_and_http_header_boundaries_reject_unauthorized_operations()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>();
        var author = new CoreScope(f.Owner.TenantId, "m13-author"); var p = await e.StartAsync(author, f.Universe, EditorialTestFixture.Request(f), default); var db = s.GetRequiredService<CoreDbContext>();
        await Assert.ThrowsAsync<CoreAccessException>(() => e.ActAsync(author, f.Universe, p.Id, EditorialTestFixture.Action(p, "Approve"), default)); await Assert.ThrowsAsync<CoreAccessException>(() => e.ActAsync(new(f.Owner.TenantId, "m13-editor"), f.Universe, p.Id, EditorialTestFixture.Action(p, "Schedule"), default));
        await Assert.ThrowsAsync<CoreAccessException>(() => e.EditAsync(new(f.Owner.TenantId, "integration-test-Author"), f.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, "Not mine", "Unchanged", "Fiction", "Owned", "fixture:foreign-author"), default));
        using var a = root.CreateScope(); using var b = root.CreateScope(); var request = EditorialTestFixture.Request(f, "replay"); var replay = await Task.WhenAll(a.ServiceProvider.GetRequiredService<IEditorialWorkbench>().StartAsync(f.Owner, f.Universe, request, default), b.ServiceProvider.GetRequiredService<IEditorialWorkbench>().StartAsync(f.Owner, f.Universe, request, default)); Assert.Equal(replay[0].Id, replay[1].Id);
        using var host = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder => { builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Runtime:Enabled"] = "true", ["ConnectionStrings:ContentOS"] = LessonTestFixture.Connection, ["Staff:Administrator:0"] = "integration-test-Administrator", ["Staff:Author:0"] = "integration-test-Author", ["Staff:Editor:0"] = "integration-test-Editor", ["Staff:Publisher:0"] = "integration-test-Publisher", ["Staff:Reader:0"] = "integration-test-Reader", ["Factory:WorkerEnabled"] = "false", ["Workflows:WorkerEnabled"] = "false", ["Knowledge:IndexWorkerEnabled"] = "false" })); builder.ConfigureServices(TestAuthentication.Configure); });
        using var client = host.CreateClient(); var path = $"/api/v1/editorial/tenants/{f.Owner.TenantId}/universes/{f.Universe}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/items")).StatusCode); client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/items", request)).StatusCode); client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1"); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/staff/editorial")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/editorial/tenants/{Guid.NewGuid()}/universes/{f.Universe}/items")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Author"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/routes", new EditorialRouteCommand(Guid.NewGuid(), "Activate", Guid.NewGuid(), ""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/items/" + p.Id + "/actions", EditorialTestFixture.Action(p, "Approve"))).StatusCode);
    }
    [SqlFact]
    public async Task A_direct_episode_cast_retirement_expires_approval_without_a_factory_artifact()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s);
        var e = s.GetRequiredService<IEditorialWorkbench>(); var registry = s.GetRequiredService<IUniverseRegistry>();
        var rf = new RegistryTestFixture.Fixture(f.Owner, f.Universe, f.Audience, f.Bible, f.Episode);
        var character = await RegistryTestFixture.Character(s, rf, "direct-learner");
        var p = await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f, "direct-episode") with { Format = "Episode" }, default);
        var episode = (await registry.EpisodesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == f.Episode);
        var story = await registry.ComposeAsync(f.Owner, f.Universe, new(Guid.NewGuid(), episode.RowVersion, RegistryTestFixture.Brief(rf), "None",
            [new(character.CharacterId, "FictionalDialogue", "I pause and choose my next action.", ["curious"], [], true)], [], ContentRevisionId: p.RevisionId), default);
        await RegistryTestFixture.Review(s, f.Owner, f.Universe, story.Id);
        p = await EditorialTestFixture.Approved(s, f, p);
        await registry.ControlCharacterAsync(f.Owner, f.Universe, character.CharacterId, new(Guid.NewGuid(), character.RowVersion, "Retire", "fixture:character-retired"), default);
        var expired = await e.InspectAsync(f.Owner, f.Universe, p.Id, default);
        Assert.Contains("ApprovalExpired", expired.Blocks); Assert.All(expired.Approvals, a => Assert.False(a.Current));
        Assert.Contains("CurrentApprovedCastRequired", expired.Blocks);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => EditorialTestFixture.Approved(s, f, expired));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, p.ContentId, default));
    }
}
