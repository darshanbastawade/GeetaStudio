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
public sealed class SqlSourceTests : IAsyncLifetime
{
    private const string Connection = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
    private const string Actor = "m03-test-owner";
    private static readonly Guid Local = CoreSeed.StableId("tenant:local");
    private static readonly Guid Universe = CoreSeed.StableId("universe:synthetic");
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static CoreScope Owner => new(Local, Actor);
    private static ServiceProvider Services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ContentOS"] = Connection }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddContentCore(config); return services.BuildServiceProvider();
    }
    public async Task InitializeAsync()
    {
        using var root = Services(); using var scope = root.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await CoreSeed.SeedAsync(db, Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M02-universes.json"), Actor);
        foreach (var pair in new[] { ("integration-test-Administrator", "Administrator"), ("integration-test-Reader", "Reader"), ("m03-reader", "Reader") })
            if (!await db.Members.AnyAsync(m => m.TenantId == Local && m.Actor == pair.Item1))
                db.Members.Add(new TenantMember { TenantId = Local, Actor = pair.Item1, Permission = pair.Item2 });
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private static async Task<ContentView> ContentAsync(IContentCore core, string kind = "Synthetic", string rights = "Synthetic")
    {
        var catalog = await core.CatalogAsync(Owner, Universe, Ct);
        return await core.CreateAsync(Owner, Universe, new(Guid.NewGuid(), catalog.Subjects[0].Id, catalog.Manifest.Audiences[0].Id,
            "en-IN", "M03 original fixture", "A traveller shares tea.", kind, rights, "Original test fixture, no third-party claim"), Ct);
    }
    [SqlFact]
    public async Task Unknown_rights_deny_commercial_publication_and_core_export_even_with_claimed_licensed_status()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var sources = s.GetRequiredService<ISourceRegistry>(); var core = s.GetRequiredService<IContentCore>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe, false);
        var decision = await sources.EvaluateAsync(Owner, Universe, source.Snapshot, new("Publish", "en-IN", "IN", "Text", true), Ct);
        Assert.False(decision.Allowed); Assert.Equal("UnknownRights", decision.Reason);
        var content = await ContentAsync(core, "Interpretation", "Licensed");
        await sources.LinkAsync(Owner, Universe, new(Guid.NewGuid(), source.Snapshot, null, content.Revisions[0].Id, null, "based-on"), Ct);
        await Assert.ThrowsAsync<RightsBlockedException>(() => core.ExportAsync(Owner, Universe, content.Id, Ct));
        await Assert.ThrowsAsync<RightsBlockedException>(() => sources.RecordUseAsync(Owner, Universe, new(Guid.NewGuid(), content.Revisions[0].Id, new("Publish", "en-IN", "IN", "Text", true), true, "fixture:publication"), Ct));
        var detail = await sources.InspectAsync(Owner, Universe, source.Work, Ct);
        Assert.False(detail.Snapshots.Single().SnapshotRetained);
        var db = s.GetRequiredService<CoreDbContext>();
        Assert.True(await db.RightsDecisions.AnyAsync(d => d.Id == decision.Id && !d.Allowed));
        Assert.False(await db.SourceUses.AnyAsync(u => u.ContentRevisionId == content.Revisions[0].Id));
    }
    [SqlFact]
    public async Task Attributed_export_preserves_evidence_after_reimport_new_edition_and_revision()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var sources = s.GetRequiredService<ISourceRegistry>(); var core = s.GetRequiredService<IContentCore>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe);
        var import = new ImportSourceFile(Guid.NewGuid(), "original.txt", SourceTestFixture.Bytes, SourceTestFixture.Checksum, "fixture:original-test-sentence", true);
        Assert.Equal(source.Snapshot, await sources.ImportAsync(Owner, Universe, source.Edition, import, Ct));
        Assert.Equal(source.Snapshot, await sources.ImportAsync(Owner, Universe, source.Edition, import, Ct));
        var next = await sources.EditionAsync(Owner, Universe, source.Work, new(Guid.NewGuid(), "translated-v2", "ContentOS test fixture author", "Modern fixture translator", "Test", "hi-IN", "sentence 1"), Ct);
        var changed = await sources.ImportAsync(Owner, Universe, next, import with { OperationId = Guid.NewGuid(), RetainFullSnapshot = false }, Ct);
        Assert.NotEqual(source.Snapshot, changed);
        Assert.Equal("UnknownRights", (await sources.EvaluateAsync(Owner, Universe, changed, new("Publish", "hi-IN", "IN", "Text", true), Ct)).Reason);
        var content = await ContentAsync(core);
        await sources.LinkAsync(Owner, Universe, new(Guid.NewGuid(), source.Snapshot, null, content.Revisions[0].Id, null, "based-on"), Ct);
        var revised = await core.ReviseAsync(Owner, Universe, content.Id, new(Guid.NewGuid(), content.RowVersion, "Revision", "Original continuation", "Synthetic", "Synthetic", "fixture"), Ct);
        var exported = await core.ExportAsync(Owner, Universe, content.Id, Ct);
        var citation = Assert.Single(exported.Citations!);
        Assert.Equal(source.Snapshot, citation.SnapshotId); Assert.Equal(SourceTestFixture.Checksum, citation.Sha256);
        Assert.Equal("Credit: ContentOS original test fixture", citation.RequiredAttribution);
        Assert.Contains("sentence 1", citation.Citation);
        Assert.Contains(exported.Provenance!, e => e.ParentRevisionId == content.Revisions[0].Id && e.ContentRevisionId == revised.Revisions[1].Id);
        var detail = await sources.InspectAsync(Owner, Universe, source.Work, Ct);
        Assert.Equal(new[] { 1, 2 }, detail.Editions.Select(e => e.Version));
        Assert.Equal(2, detail.Snapshots.Count);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => core.ImportAsync(Owner, Universe, new(Guid.NewGuid(), exported), Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => sources.ExportAsync(Owner, Universe, content.Id, new("Export", "hi-IN", "IN", "Text"), Ct));
    }
    [SqlFact]
    public async Task Revocation_propagates_through_revisions_and_assets_and_creates_published_remediation()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var sources = s.GetRequiredService<ISourceRegistry>(); var core = s.GetRequiredService<IContentCore>(); var db = s.GetRequiredService<CoreDbContext>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe);
        var first = await ContentAsync(core); var second = await ContentAsync(core);
        await sources.LinkAsync(Owner, Universe, new(Guid.NewGuid(), source.Snapshot, null, first.Revisions[0].Id, null, "based-on"), Ct);
        await sources.LinkAsync(Owner, Universe, new(Guid.NewGuid(), null, first.Revisions[0].Id, second.Revisions[0].Id, null, "adaptation-of"), Ct);
        var asset = new CoreAssetReference { Id = Guid.NewGuid(), TenantId = Local, ContentRevisionId = second.Revisions[0].Id, StorageAssetId = Guid.NewGuid(), RightsStatus = "Licensed" };
        db.Assets.Add(asset); await db.SaveChangesAsync();
        await sources.LinkAsync(Owner, Universe, new(Guid.NewGuid(), source.Snapshot, null, null, asset.Id, "illustrates"), Ct);
        var use = new RecordSourceUse(Guid.NewGuid(), second.Revisions[0].Id, new("Publish", "en-IN", "IN", "Text", true), true, "fixture:local-publication", "Fixture");
        var useId = await sources.RecordUseAsync(Owner, Universe, use, Ct);
        Assert.Equal(useId, await sources.RecordUseAsync(Owner, Universe, use, Ct));
        var detail = await sources.InspectAsync(Owner, Universe, source.Work, Ct);
        var revoke = new RevokeSource(Guid.NewGuid(), null, "Fixture takedown request", detail.Work.RowVersion);
        var impact = await sources.RevokeAsync(Owner, Universe, source.Work, revoke, Ct);
        Assert.Contains(impact.Derivatives, x => x.ContentRevisionId == second.Revisions[0].Id);
        Assert.Contains(impact.Derivatives, x => x.AssetReferenceId == asset.Id);
        var task = Assert.Single(impact.Remediation); Assert.Equal("Open", task.Status); Assert.Equal("Fixture", task.Mode);
        Assert.Equal(impact.Id, (await sources.RevokeAsync(Owner, Universe, source.Work, revoke, Ct)).Id);
        await Assert.ThrowsAsync<RightsBlockedException>(() => core.ExportAsync(Owner, Universe, second.Id, Ct));
        await Assert.ThrowsAsync<RightsBlockedException>(() => sources.RecordUseAsync(Owner, Universe, use with { OperationId = Guid.NewGuid() }, Ct));
        await sources.ResolveAsync(Owner, Universe, task.Id, new(task.RowVersion, "fixture:removed-local-publication"), Ct);
        Assert.Equal("Resolved", (await sources.RemediationAsync(Owner, Universe, Ct)).Single(x => x.Id == task.Id).Status);
        Assert.Equal("Revoked", (await sources.EvaluateAsync(Owner, Universe, source.Snapshot, new("Generate", "en-IN", "IN", "Text"), Ct)).Reason);
    }
    [SqlFact]
    public async Task Scope_membership_and_admin_review_reject_forged_tenant_reader_and_cross_universe_links()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var sources = s.GetRequiredService<ISourceRegistry>(); var core = s.GetRequiredService<IContentCore>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe);
        await Assert.ThrowsAsync<CoreAccessException>(() => sources.ListAsync(new(Local, "unrecognized"), Universe, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => sources.ListAsync(new(CoreSeed.StableId("tenant:isolated"), Actor), Universe, Ct));
        var detail = await sources.InspectAsync(Owner, Universe, source.Work, Ct);
        await Assert.ThrowsAsync<CoreAccessException>(() => sources.RevokeAsync(new(Local, "m03-reader"), Universe, source.Work, new(Guid.NewGuid(), null, "no", detail.Work.RowVersion), Ct));
        var otherUniverse = CoreSeed.StableId("universe:geeta");
        var catalog = await core.CatalogAsync(Owner, otherUniverse, Ct);
        var foreign = await core.CreateAsync(Owner, otherUniverse, new(Guid.NewGuid(), catalog.Subjects[0].Id, catalog.Manifest.Audiences[0].Id, "en-IN", "x", "x", "Synthetic", "Synthetic", "fixture"), Ct);
        await Assert.ThrowsAsync<CoreAccessException>(() => sources.LinkAsync(Owner, Universe, new(Guid.NewGuid(), source.Snapshot, null, foreign.Revisions[0].Id, null, "x"), Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => s.GetRequiredService<ISourceRightsGuard>().CheckAsync(new(Local, "unrecognized"), foreign.Revisions[0].Id, new("Export", "en-IN", "IN", "Text"), Ct));
    }
    [SqlFact]
    public async Task Import_validation_retention_review_queue_and_immutable_history_are_enforced()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var sources = s.GetRequiredService<ISourceRegistry>(); var db = s.GetRequiredService<CoreDbContext>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe, false);
        var import = new ImportSourceFile(Guid.NewGuid(), "next.txt", [65], Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 65 })), "fixture:test", false);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => sources.ImportAsync(Owner, Universe, source.Edition, import with { ExpectedSha256 = "bad" }, Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => sources.ImportAsync(Owner, Universe, source.Edition, import with { FileName = "../file.txt" }, Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => sources.ImportAsync(Owner, Universe, source.Edition, import with { EvidenceReference = "https://example.com" }, Ct));
        await Assert.ThrowsAsync<RightsBlockedException>(() => sources.ImportAsync(Owner, Universe, source.Edition, import with { RetainFullSnapshot = true }, Ct));
        var snapshot = await sources.ImportAsync(Owner, Universe, source.Edition, import, Ct);
        var queue = await sources.ReviewQueueAsync(Owner, Universe, Ct); var pending = Assert.Single(queue.Snapshots, x => x.Id == snapshot);
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => sources.ReviewSnapshotAsync(Owner, Universe, snapshot, new(Convert.ToBase64String([1]), "Approved"), Ct));
        await sources.ReviewSnapshotAsync(Owner, Universe, snapshot, new(pending.RowVersion, "Rejected"), Ct);
        Assert.DoesNotContain((await sources.ReviewQueueAsync(Owner, Universe, Ct)).Snapshots, x => x.Id == snapshot);
    }
    [SqlFact]
    public async Task Concurrent_import_is_one_snapshot_and_operation_reuse_cannot_change_payload()
    {
        using var root = Services(); using var setup = root.CreateScope(); var sources = setup.ServiceProvider.GetRequiredService<ISourceRegistry>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe);
        var request = new ImportSourceFile(Guid.NewGuid(), "same.txt", SourceTestFixture.Bytes, SourceTestFixture.Checksum, "fixture:concurrent", false);
        using var a = root.CreateScope(); using var b = root.CreateScope();
        var replies = await Task.WhenAll(a.ServiceProvider.GetRequiredService<ISourceRegistry>().ImportAsync(Owner, Universe, source.Edition, request, Ct),
            b.ServiceProvider.GetRequiredService<ISourceRegistry>().ImportAsync(Owner, Universe, source.Edition, request, Ct));
        Assert.Equal(replies[0], replies[1]);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => sources.ImportAsync(Owner, Universe, source.Edition, request with { EvidenceReference = "fixture:changed" }, Ct));
        var db = setup.ServiceProvider.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear();
        var snapshot = await db.SourceSnapshots.SingleAsync(x => x.Id == source.Snapshot); snapshot.Text = "overwrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task New_pending_permission_restricts_existing_uses_and_approval_does_not_bypass_dimensions()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var sources = s.GetRequiredService<ISourceRegistry>(); var core = s.GetRequiredService<IContentCore>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe);
        var content = await ContentAsync(core);
        await sources.LinkAsync(Owner, Universe, new(Guid.NewGuid(), source.Snapshot, null, content.Revisions[0].Id, null, "based-on"), Ct);
        var permission = await sources.PermissionAsync(Owner, Universe, source.Edition, new(Guid.NewGuid(), "fixture:restricted-version",
            ["en-IN", "hi-IN"], ["IN"], ["Text"], false, false, false, false, false, "Fixture credit"), Ct);
        Assert.Equal("UnknownRights", (await sources.EvaluateAsync(Owner, Universe, source.Snapshot, new("Export", "en-IN", "IN", "Text"), Ct)).Reason);
        var detail = await sources.InspectAsync(Owner, Universe, source.Work, Ct);
        await sources.ReviewPermissionAsync(Owner, Universe, permission, new(detail.Permissions.Single(p => p.Id == permission).RowVersion, "Approved"), Ct);
        Assert.Equal("CommercialUseNotPermitted", (await sources.EvaluateAsync(Owner, Universe, source.Snapshot, new("Publish", "en-IN", "IN", "Text", true), Ct)).Reason);
        Assert.Equal("TerritoryNotPermitted", (await sources.EvaluateAsync(Owner, Universe, source.Snapshot, new("Export", "en-IN", "US", "Text"), Ct)).Reason);
        Assert.Equal("FormatNotPermitted", (await sources.EvaluateAsync(Owner, Universe, source.Snapshot, new("Export", "en-IN", "IN", "Video"), Ct)).Reason);
        var translated = await sources.EvaluateAsync(Owner, Universe, source.Snapshot, new("Export", "hi-IN", "IN", "Text"), Ct);
        Assert.Equal("TranslationNotPermitted", translated.Reason);
        var generated = await sources.EvaluateAsync(Owner, Universe, source.Snapshot, new("Generate", "en-IN", "IN", "Text"), Ct);
        var decisionDb = s.GetRequiredService<CoreDbContext>();
        var effective = await decisionDb.RightsDecisions.AsNoTracking().SingleAsync(d => d.Id == generated.Id);
        Assert.True(effective.Adaptation); Assert.True(effective.AiProcessing);
        Assert.True((await decisionDb.RightsDecisions.AsNoTracking().SingleAsync(d => d.Id == translated.Id)).Translation);
        var blocked = await Assert.ThrowsAsync<RightsBlockedException>(() => core.ExportAsync(Owner, Universe, content.Id, Ct));
        Assert.Equal("AdaptationNotPermitted", blocked.Reason);
    }
    [SqlFact]
    public async Task Asset_only_evidence_survives_revision_and_cycle_attempts_are_rejected()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var sources = s.GetRequiredService<ISourceRegistry>(); var core = s.GetRequiredService<IContentCore>(); var db = s.GetRequiredService<CoreDbContext>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe);
        var content = await ContentAsync(core);
        var asset = new CoreAssetReference { Id = Guid.NewGuid(), TenantId = Local, ContentRevisionId = content.Revisions[0].Id, StorageAssetId = Guid.NewGuid() };
        db.Assets.Add(asset); await db.SaveChangesAsync();
        await sources.LinkAsync(Owner, Universe, new(Guid.NewGuid(), source.Snapshot, null, null, asset.Id, "asset-evidence"), Ct);
        var revised = await core.ReviseAsync(Owner, Universe, content.Id, new(Guid.NewGuid(), content.RowVersion, "next", "next", "Synthetic", "Synthetic", "fixture"), Ct);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => sources.LinkAsync(Owner, Universe,
            new(Guid.NewGuid(), null, revised.Revisions[1].Id, content.Revisions[0].Id, null, "cycle"), Ct));
        var export = await core.ExportAsync(Owner, Universe, content.Id, Ct);
        Assert.Contains(export.Provenance!, e => e.AssetReferenceId == asset.Id);
        Assert.Contains(export.Provenance!, e => e.ContentRevisionId == revised.Revisions[1].Id);
        var detail = await sources.InspectAsync(Owner, Universe, source.Work, Ct);
        var impact = await sources.RevokeAsync(Owner, Universe, source.Work, new(Guid.NewGuid(), source.Edition, "fixture edition revoked", detail.Editions.Single().RowVersion), Ct);
        Assert.Contains(impact.Derivatives, e => e.ContentRevisionId == revised.Revisions[1].Id);
        await Assert.ThrowsAsync<RightsBlockedException>(() => core.ExportAsync(Owner, Universe, content.Id, Ct));
    }
    [SqlFact]
    public async Task Api_enforces_staff_header_and_rights_denial_and_serves_review_foundation()
    {
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Staff:Administrator:0"] = "integration-test-Administrator",
                ["Staff:Reader:0"] = "integration-test-Reader",
                ["Runtime:Enabled"] = "true",
                ["ConnectionStrings:ContentOS"] = Connection,
                ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets")
            }));
            builder.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = factory.CreateClient();
        var path = $"/api/v1/sources/tenants/{Local}/universes/{Universe}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/review-queue")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/staff/sources")).StatusCode);
        var request = new RegisterSource(Guid.NewGuid(), "api-" + Guid.NewGuid(), "API fixture", "Synthetic");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/works", request)).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/works", request)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/works", request with { OperationId = Guid.NewGuid() })).StatusCode);
    }
}



