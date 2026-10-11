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
public sealed class SqlCoreTests : IAsyncLifetime
{
    private const string Connection = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
    private const string Actor = "m02-test-owner";
    private const string Other = "m02-test-other";
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid Local = CoreSeed.StableId("tenant:local");
    private static readonly Guid Isolated = CoreSeed.StableId("tenant:isolated");
    private static readonly Guid Geeta = CoreSeed.StableId("universe:geeta");
    private static readonly Guid Synthetic = CoreSeed.StableId("universe:synthetic");
    private static readonly Guid Private = CoreSeed.StableId("universe:private-fixture");
    private static CoreScope Owner => new(Local, Actor);
    private static CoreScope Outsider => new(Isolated, Other);
    private static ServiceProvider Services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ContentOS"] = Connection }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddContentCore(config); return services.BuildServiceProvider();
    }
    private static string Fixture => Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M02-universes.json");
    public async Task InitializeAsync()
    {
        using var root = Services(); using var scope = root.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await CoreSeed.SeedAsync(db, Fixture, Actor);
        foreach (var tuple in new[] { (Local, "integration-test-Administrator", "Administrator"), (Local, "integration-test-Reader", "Reader"), (Isolated, Other, "Administrator") })
            if (!await db.Members.AnyAsync(m => m.TenantId == tuple.Item1 && m.Actor == tuple.Item2))
                db.Members.Add(new TenantMember { TenantId = tuple.Item1, Actor = tuple.Item2, Permission = tuple.Item3 });
        if (!await db.Locales.AnyAsync(l => l.Id == "sa-IN")) db.Locales.Add(new ContentLocale { Id = "sa-IN", Name = "Sanskrit (India)" });
        await db.SaveChangesAsync();
        // Revoke only test-owned sharing flags; retain all records and previous content history.
        await db.Grants.Where(g => g.UniverseId == Geeta && g.GranteeTenantId == Isolated)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Read, false).SetProperty(g => g.Write, false).SetProperty(g => g.Export, false));
        await db.Universes.Where(u => u.Id == Geeta).ExecuteUpdateAsync(s => s.SetProperty(u => u.PublicKnowledgeRead, false));
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private static async Task<CreateContent> RequestAsync(IContentCore core, CoreScope scope, Guid universe, string kind = "Synthetic", string rights = "Synthetic", bool shared = false)
    {
        var catalog = await core.CatalogAsync(scope, universe, Ct);
        return new(Guid.NewGuid(), catalog.Subjects[0].Id, catalog.Manifest.Audiences[0].Id, "en-IN",
            "M02 deterministic fixture", "Original test content; no factual or scripture claim.", kind, rights, "M02 test fixture", shared);
    }
    [SqlFact]
    public async Task Both_universes_use_the_same_create_revision_export_operations_and_retry_is_idempotent()
    {
        using var root = Services(); using var scope = root.CreateScope(); var core = scope.ServiceProvider.GetRequiredService<IContentCore>();
        foreach (var universe in new[] { Geeta, Synthetic })
        {
            var request = await RequestAsync(core, Owner, universe);
            var first = await core.CreateAsync(Owner, universe, request, Ct);
            Assert.Equal(first.Id, (await core.CreateAsync(Owner, universe, request, Ct)).Id);
            var updated = await core.ReviseAsync(Owner, universe, first.Id, new(Guid.NewGuid(), first.RowVersion, "Updated", "Updated original fixture", "Synthetic", "Synthetic", "test lineage"), Ct);
            Assert.Equal(2, updated.CurrentVersion); Assert.Equal(first.Revisions[0], updated.Revisions[0]);
            var export = await core.ExportAsync(Owner, universe, first.Id, Ct);
            Assert.Equal(1, export.SchemaVersion); Assert.Equal(Local, export.TenantId); Assert.Equal(universe, export.UniverseId);
            Assert.Equal(2, export.Content.Revisions.Count);
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            Assert.Equal(2, await db.Events.CountAsync(e => e.ContentId == first.Id));
            Assert.Equal(1, await db.Operations.CountAsync(o => o.Id == request.OperationId && o.TenantId == Local));
        }
    }
    [SqlFact]
    public async Task Tenant_scope_and_record_queries_reject_cross_tenant_reads_writes_exports_and_forged_scope()
    {
        using var root = Services(); using var scope = root.CreateScope(); var core = scope.ServiceProvider.GetRequiredService<IContentCore>();
        var item = await core.CreateAsync(Owner, Geeta, await RequestAsync(core, Owner, Geeta), Ct);
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ReadAsync(Outsider, Geeta, item.Id, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ListAsync(Outsider, Geeta, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ExportAsync(Outsider, Geeta, item.Id, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ReviseAsync(Outsider, Geeta, item.Id, new(Guid.NewGuid(), item.RowVersion, "x", "x", "Synthetic", "Synthetic", "x"), Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ReadAsync(new(Local, Other), Geeta, item.Id, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => core.CatalogAsync(Owner, Private, Ct));
        Assert.DoesNotContain(await core.UniversesAsync(Outsider, Ct), u => u.Id == Geeta);
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ReadAsync(Owner, Synthetic, item.Id, Ct));
    }
    [SqlFact]
    public async Task Explicit_grants_are_separate_for_read_write_and_export_and_revocation_takes_effect()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var core = s.GetRequiredService<IContentCore>();
        var item = await core.CreateAsync(Owner, Geeta, await RequestAsync(core, Owner, Geeta), Ct);
        var catalog = await core.CatalogAsync(Owner, Geeta, Ct);
        await core.ShareAsync(Owner, Geeta, new(Isolated, true, false, false, catalog.Universe.RowVersion), Ct);
        Assert.Equal(item.Id, (await core.ReadAsync(Outsider, Geeta, item.Id, Ct)).Id);
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ExportAsync(Outsider, Geeta, item.Id, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => core.CreateAsync(Outsider, Geeta, awaitableRequest(item), Ct));
        catalog = await core.CatalogAsync(Owner, Geeta, Ct);
        await core.ShareAsync(Owner, Geeta, new(Isolated, true, true, true, catalog.Universe.RowVersion), Ct);
        Assert.Equal(item.Id, (await core.ExportAsync(Outsider, Geeta, item.Id, Ct)).Content.Id);
        var revised = await core.ReviseAsync(Outsider, Geeta, item.Id, new(Guid.NewGuid(), item.RowVersion, "Shared editor", "Explicit grant", "Synthetic", "Synthetic", "fixture"), Ct);
        Assert.Equal(Local, revised.TenantId);
        catalog = await core.CatalogAsync(Owner, Geeta, Ct);
        await core.ShareAsync(Owner, Geeta, new(Isolated, false, false, false, catalog.Universe.RowVersion), Ct);
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ReadAsync(Outsider, Geeta, item.Id, Ct));
        static CreateContent awaitableRequest(ContentView v) => new(Guid.NewGuid(), v.SubjectId, v.AudienceId, v.LocaleId, "x", "x", "Synthetic", "Synthetic", "fixture");
    }
    [SqlFact]
    public async Task Public_knowledge_shares_only_approved_current_revision_and_never_exports_or_writes()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var core = s.GetRequiredService<IContentCore>();
        var db = s.GetRequiredService<CoreDbContext>();
        await db.Universes.Where(u => u.Id == Geeta).ExecuteUpdateAsync(p => p.SetProperty(u => u.PublicKnowledgeRead, true));
        try
        {
            var evidence = await EvidenceTestFixture.TranslationAsync(s, Owner, Geeta);
            var publicItem = await core.CreateAsync(Owner, Geeta, (await RequestAsync(core, Owner, Geeta, "Translation", "Licensed", true)) with { Body = evidence.Text }, Ct);
            await Assert.ThrowsAsync<CoreAccessException>(() => core.ReadAsync(Outsider, Geeta, publicItem.Id, Ct));
            publicItem = await core.ReviseAsync(Owner, Geeta, publicItem.Id, new(Guid.NewGuid(), publicItem.RowVersion, "Current test revision", evidence.Text, "Translation", "Licensed", "M04 stored licensed sample"), Ct);
            await s.GetRequiredService<IEvidenceEngine>().LinkAsync(Owner, Geeta, new(Guid.NewGuid(), evidence.Revision, publicItem.Revisions.Last().Id, 0, evidence.Text.Length), Ct);
            publicItem = await core.TransitionAsync(Owner, Geeta, publicItem.Id, new(Guid.NewGuid(), publicItem.RowVersion, "Review"), Ct);
            publicItem = await core.TransitionAsync(Owner, Geeta, publicItem.Id, new(Guid.NewGuid(), publicItem.RowVersion, "Approved"), Ct);
            var read = await core.ReadAsync(Outsider, Geeta, publicItem.Id, Ct);
            Assert.Single(read.Revisions); Assert.Equal(2, read.Revisions[0].Version);
            await Assert.ThrowsAsync<CoreAccessException>(() => core.ExportAsync(Outsider, Geeta, publicItem.Id, Ct));
            await Assert.ThrowsAsync<CoreAccessException>(() => core.ReviseAsync(Outsider, Geeta, publicItem.Id, new(Guid.NewGuid(), publicItem.RowVersion, "x", "x", "Canonical", "PublicDomain", "x"), Ct));
            var privateItem = await core.CreateAsync(Owner, Geeta, await RequestAsync(core, Owner, Geeta), Ct);
            Assert.DoesNotContain(await core.ListAsync(Outsider, Geeta, Ct), c => c.Id == privateItem.Id);
        }
        finally { await db.Universes.Where(u => u.Id == Geeta).ExecuteUpdateAsync(p => p.SetProperty(u => u.PublicKnowledgeRead, false)); }
    }
    [SqlFact]
    public async Task Stale_updates_conflict_and_revision_history_is_immutable()
    {
        using var root = Services(); using var scope = root.CreateScope(); var core = scope.ServiceProvider.GetRequiredService<IContentCore>();
        var item = await core.CreateAsync(Owner, Synthetic, await RequestAsync(core, Owner, Synthetic), Ct);
        using var other = root.CreateScope(); var second = other.ServiceProvider.GetRequiredService<IContentCore>();
        var stale = await second.ReadAsync(Owner, Synthetic, item.Id, Ct);
        await core.ReviseAsync(Owner, Synthetic, item.Id, new(Guid.NewGuid(), item.RowVersion, "first", "first", "Synthetic", "Synthetic", "fixture"), Ct);
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => second.ReviseAsync(Owner, Synthetic, item.Id, new(Guid.NewGuid(), stale.RowVersion, "stale", "stale", "Synthetic", "Synthetic", "fixture"), Ct));
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear();
        var revision = await db.Revisions.SingleAsync(r => r.Id == item.Revisions[0].Id); revision.Body = "overwrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task Seeds_preserve_ids_edits_history_and_character_alias_identity()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var core = s.GetRequiredService<IContentCore>(); var before = await core.CatalogAsync(Owner, Geeta, Ct);
        var db = s.GetRequiredService<CoreDbContext>(); var count = await db.Characters.CountAsync(); var revisions = await db.Revisions.CountAsync();
        await CoreSeed.SeedAsync(db, Fixture, Actor); await CoreSeed.SeedAsync(db, Fixture, Actor);
        var after = await core.CatalogAsync(Owner, Geeta, Ct);
        Assert.Equal(before.Manifest.Id, after.Manifest.Id); Assert.Equal(count, await db.Characters.CountAsync()); Assert.Equal(revisions, await db.Revisions.CountAsync());
        Assert.Equal(7, after.Characters.Count);
        var neerja = Assert.Single(after.Characters, c => c.DisplayName == "Neerja");
        Assert.Contains("Neeraja", neerja.Aliases);
        Assert.Equal(neerja.CharacterId, (await db.Aliases.SingleAsync(a => a.UniverseId == Geeta && a.Name == "Neeraja")).CharacterId);
        Assert.All(after.Characters, c => Assert.Equal("Draft", c.Status)); // Seeded text is not approved media.
    }
    [SqlFact]
    public async Task Imports_validate_versions_scope_and_ids_and_do_not_overwrite_existing_data()
    {
        using var root = Services(); using var scope = root.CreateScope(); var core = scope.ServiceProvider.GetRequiredService<IContentCore>();
        var original = await core.CreateAsync(Owner, Synthetic, await RequestAsync(core, Owner, Synthetic), Ct);
        var exported = await core.ExportAsync(Owner, Synthetic, original.Id, Ct);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => core.ImportAsync(Owner, Synthetic, new(Guid.NewGuid(), null!), Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => core.ImportAsync(Owner, Synthetic, new(Guid.NewGuid(), exported with { SchemaVersion = 99 }), Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => core.ImportAsync(Owner, Geeta, new(Guid.NewGuid(), exported), Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ImportAsync(Outsider, Synthetic, new(Guid.NewGuid(), exported), Ct));
        await Assert.ThrowsAsync<RuntimeConflictException>(() => core.ImportAsync(Owner, Synthetic, new(Guid.NewGuid(), exported), Ct));
        var portable = exported with { Content = original with { Id = Guid.NewGuid(), Revisions = original.Revisions.Select(r => r with { Id = Guid.NewGuid(), OperationId = Guid.NewGuid() }).ToArray() } };
        var request = new ImportContent(Guid.NewGuid(), portable);
        var imported = await core.ImportAsync(Owner, Synthetic, request, Ct);
        Assert.Equal(portable.Content.Id, imported.Id); Assert.Equal(portable.Content.Revisions[0].Id, imported.Revisions[0].Id);
        Assert.Equal(imported.Id, (await core.ImportAsync(Owner, Synthetic, request, Ct)).Id);
        Assert.Equal(original.Revisions[0].Body, (await core.ReadAsync(Owner, Synthetic, original.Id, Ct)).Revisions[0].Body);
    }
    [SqlFact]
    public async Task Wrong_catalog_references_and_operation_id_reuse_are_rejected()
    {
        using var root = Services(); using var scope = root.CreateScope(); var core = scope.ServiceProvider.GetRequiredService<IContentCore>();
        var request = await RequestAsync(core, Owner, Synthetic);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => core.CreateAsync(Owner, Geeta, request, Ct));
        await core.CreateAsync(Owner, Synthetic, request, Ct);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => core.CreateAsync(Owner, Synthetic, request with { Body = "different command" }, Ct));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => core.CreateAsync(Owner, Synthetic, request with { OperationId = Guid.Empty }, Ct));
    }
    [SqlFact]
    public async Task Core_api_enforces_staff_membership_action_header_and_explicit_concurrency_response()
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
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/core/tenants")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        var tenants = (await client.GetFromJsonAsync<TenantView[]>("/api/v1/core/tenants"))!;
        Assert.Contains(tenants, t => t.Id == Local); Assert.DoesNotContain(tenants, t => t.Id == Isolated);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/core/tenants/{Isolated}/universes")).StatusCode);
        var catalog = (await client.GetFromJsonAsync<UniverseCatalog>($"/api/v1/core/tenants/{Local}/universes/{Synthetic}/catalog"))!;
        var request = new CreateContent(Guid.NewGuid(), catalog.Subjects[0].Id, catalog.Manifest.Audiences[0].Id, "en-IN", "API fixture", "original", "Synthetic", "Synthetic", "fixture");
        var url = $"/api/v1/core/tenants/{Local}/universes/{Synthetic}/content";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, request)).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        var created = (await (await client.PostAsJsonAsync(url, request)).Content.ReadFromJsonAsync<ContentView>())!;
        var revised = new ReviseContent(Guid.NewGuid(), created.RowVersion, "next", "next", "Synthetic", "Synthetic", "fixture");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url + "/" + created.Id + "/revisions", revised)).StatusCode);
        var conflict = await client.PostAsJsonAsync(url + "/" + created.Id + "/revisions", revised with { OperationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode); Assert.Contains("ConcurrencyConflict", await conflict.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url, request with { OperationId = Guid.NewGuid() })).StatusCode);
    }
    [SqlFact]
    public async Task Concurrent_repeated_create_uses_one_operation_and_one_content_identity()
    {
        using var root = Services(); using var setup = root.CreateScope();
        var request = await RequestAsync(setup.ServiceProvider.GetRequiredService<IContentCore>(), Owner, Synthetic);
        using var a = root.CreateScope(); using var b = root.CreateScope();
        var replies = await Task.WhenAll(a.ServiceProvider.GetRequiredService<IContentCore>().CreateAsync(Owner, Synthetic, request, Ct),
            b.ServiceProvider.GetRequiredService<IContentCore>().CreateAsync(Owner, Synthetic, request, Ct));
        Assert.Equal(replies[0].Id, replies[1].Id);
        var db = setup.ServiceProvider.GetRequiredService<CoreDbContext>();
        Assert.Equal(1, await db.Events.CountAsync(e => e.OperationId == request.OperationId));
    }
    [SqlFact]
    public async Task Legacy_character_normalization_keeps_identity_and_old_revision()
    {
        using var root = Services(); using var scope = root.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var fixture = System.Text.Json.JsonSerializer.Deserialize<CoreFixture>(await File.ReadAllTextAsync(Fixture),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var key = "legacy-" + Guid.NewGuid().ToString("N");
        var custom = fixture.Universes[0] with { Key = key };
        var universe = new Universe { Id = CoreSeed.StableId("universe:" + key), TenantId = Local, Key = key, Name = "Legacy normalization fixture" };
        var character = new Character { Id = Guid.NewGuid(), UniverseId = universe.Id, Key = "legacy-child" };
        var old = new CharacterRevision { Id = Guid.NewGuid(), CharacterId = character.Id, Version = 1, DisplayName = "Neeraja", LifeStage = "Child", Biography = "Legacy biography remains immutable." };
        db.Universes.Add(universe); db.Characters.Add(character); db.CharacterRevisions.Add(old); await db.SaveChangesAsync();
        var path = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", key + ".json");
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(fixture with { Universes = [custom, fixture.Universes[1]] }));
        try
        {
            await CoreSeed.SeedAsync(db, path, Actor); await CoreSeed.SeedAsync(db, path, Actor);
            var revisions = await db.CharacterRevisions.AsNoTracking().Where(r => r.CharacterId == character.Id).OrderBy(r => r.Version).ToListAsync();
            Assert.Equal(2, revisions.Count); Assert.Equal(old.Id, revisions[0].Id); Assert.Equal("Neeraja", revisions[0].DisplayName);
            Assert.Equal("Neerja", revisions[1].DisplayName);
            Assert.Equal(old.Biography, revisions[1].Biography);
            Assert.Equal(character.Id, (await db.Aliases.SingleAsync(a => a.UniverseId == universe.Id && a.Name == "Neeraja")).CharacterId);
        }
        finally { File.Delete(path); }
    }
    [SqlFact]
    public async Task Asset_workflow_presentation_and_frozen_cast_enforce_ownership_and_universe_binding()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var core = s.GetRequiredService<IContentCore>();
        var content = await core.CreateAsync(Owner, Geeta, await RequestAsync(core, Owner, Geeta), Ct);
        var catalog = await core.CatalogAsync(Owner, Geeta, Ct);
        var db = s.GetRequiredService<CoreDbContext>();
        db.Assets.Add(new CoreAssetReference { Id = Guid.NewGuid(), TenantId = Isolated, ContentRevisionId = content.Revisions[0].Id, StorageAssetId = Guid.NewGuid() });
        await Assert.ThrowsAsync<RuntimeValidationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Workflows.Add(new WorkflowRun { Id = Guid.NewGuid(), TenantId = Isolated, ContentRevisionId = content.Revisions[0].Id, OperationId = Guid.NewGuid() });
        await Assert.ThrowsAsync<RuntimeValidationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var asset = new CoreAssetReference { Id = Guid.NewGuid(), TenantId = Local, ContentRevisionId = content.Revisions[0].Id, StorageAssetId = Guid.NewGuid() };
        db.Assets.Add(asset);
        db.Workflows.Add(new WorkflowRun { Id = Guid.NewGuid(), TenantId = Local, ContentRevisionId = content.Revisions[0].Id, OperationId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var presentation = new PresentationProfile
        {
            Id = Guid.NewGuid(),
            CharacterRevisionId = catalog.Characters[0].RevisionId,
            AudienceId = catalog.Manifest.Audiences[0].Id,
            LocaleId = "en-IN",
            VisualAssetReferenceId = asset.Id
        };
        db.Presentations.Add(presentation); await db.SaveChangesAsync();
        var synthetic = await core.CreateAsync(Owner, Synthetic, await RequestAsync(core, Owner, Synthetic), Ct);
        db.Cast.Add(new EpisodeCastEntry { ContentRevisionId = synthetic.Revisions[0].Id, CharacterRevisionId = presentation.CharacterRevisionId, PresentationProfileId = presentation.Id, Role = "guest" });
        await Assert.ThrowsAsync<RuntimeValidationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Cast.Add(new EpisodeCastEntry { ContentRevisionId = content.Revisions[0].Id, CharacterRevisionId = presentation.CharacterRevisionId, PresentationProfileId = presentation.Id, Role = "guide" });
        await db.SaveChangesAsync();
        var frozen = await db.Cast.SingleAsync(c => c.ContentRevisionId == content.Revisions[0].Id); frozen.Role = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [SqlFact]
    public async Task Write_only_mutations_and_replays_return_metadata_without_revision_bodies()
    {
        using var root = Services(); using var scope = root.CreateScope(); var core = scope.ServiceProvider.GetRequiredService<IContentCore>();
        var request = await RequestAsync(core, Owner, Geeta);
        var original = await core.CreateAsync(Owner, Geeta, request, Ct);
        var catalog = await core.CatalogAsync(Owner, Geeta, Ct);
        await core.ShareAsync(Owner, Geeta, new(Isolated, false, true, false, catalog.Universe.RowVersion), Ct);
        await Assert.ThrowsAsync<CoreAccessException>(() => core.ReadAsync(Outsider, Geeta, original.Id, Ct));
        var revision = new ReviseContent(Guid.NewGuid(), original.RowVersion, "Shared writer", "New text", "Synthetic", "Synthetic", "fixture");
        var changed = await core.ReviseAsync(Outsider, Geeta, original.Id, revision, Ct);
        Assert.Empty(changed.Revisions);
        Assert.Empty((await core.ReviseAsync(Outsider, Geeta, original.Id, revision, Ct)).Revisions);
        var transition = new ChangeEditorialState(Guid.NewGuid(), changed.RowVersion, "Review");
        Assert.Empty((await core.TransitionAsync(Outsider, Geeta, original.Id, transition, Ct)).Revisions);
        Assert.Empty((await core.TransitionAsync(Outsider, Geeta, original.Id, transition, Ct)).Revisions);
        var create = request with { OperationId = Guid.NewGuid() };
        var created = await core.CreateAsync(Outsider, Geeta, create, Ct);
        Assert.Empty(created.Revisions);
        Assert.Empty((await core.CreateAsync(Outsider, Geeta, create, Ct)).Revisions);
        var doc = await core.ExportAsync(Owner, Geeta, created.Id, Ct);
        doc = doc with { Content = doc.Content with { Id = Guid.NewGuid(), Revisions = doc.Content.Revisions.Select(r => r with { Id = Guid.NewGuid(), OperationId = Guid.NewGuid() }).ToArray() } };
        var import = new ImportContent(Guid.NewGuid(), doc);
        Assert.Empty((await core.ImportAsync(Outsider, Geeta, import, Ct)).Revisions);
        Assert.Empty((await core.ImportAsync(Outsider, Geeta, import, Ct)).Revisions);
        Assert.NotEmpty((await core.ReadAsync(Owner, Geeta, original.Id, Ct)).Revisions);
    }
    [SqlFact]
    public async Task Shared_reads_recheck_unknown_current_and_revoked_historical_evidence()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var core = s.GetRequiredService<IContentCore>(); var sources = s.GetRequiredService<ISourceRegistry>();
        var item = await core.CreateAsync(Owner, Geeta, (await RequestAsync(core, Owner, Geeta)) with { Kind = "Interpretation", RightsStatus = "Licensed" }, Ct);
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Geeta);
        await sources.LinkAsync(Owner, Geeta, new(Guid.NewGuid(), source.Snapshot, null, item.Revisions[0].Id, null, "fixture"), Ct);
        var claimRevision = await EvidenceTestFixture.InterpretationAsync(s, Owner, Geeta, source.Snapshot, item.Revisions[0].Body);
        await s.GetRequiredService<IEvidenceEngine>().LinkAsync(Owner, Geeta, new(Guid.NewGuid(), claimRevision, item.Revisions[0].Id, 0, item.Revisions[0].Body.Length), Ct);
        var catalog = await core.CatalogAsync(Owner, Geeta, Ct);
        await core.ShareAsync(Owner, Geeta, new(Isolated, true, false, false, catalog.Universe.RowVersion), Ct);
        var db = s.GetRequiredService<CoreDbContext>();
        await db.Contents.Where(c => c.Id == item.Id).ExecuteUpdateAsync(x => x.SetProperty(c => c.CreatedUtc, new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        item = await core.ReadAsync(Owner, Geeta, item.Id, Ct);
        Assert.Contains(await core.ListAsync(Outsider, Geeta, Ct), r => r.Id == item.Id);
        Assert.NotEmpty((await core.ReadAsync(Outsider, Geeta, item.Id, Ct)).Revisions);
        item = await core.ReviseAsync(Owner, Geeta, item.Id, new(Guid.NewGuid(), item.RowVersion, "Next revision", "Next body", "Translation", "Licensed", "fixture"), Ct);
        var work = (await sources.InspectAsync(Owner, Geeta, source.Work, Ct)).Work;
        await sources.RevokeAsync(Owner, Geeta, source.Work, new(Guid.NewGuid(), null, "fixture:review-regression", work.RowVersion), Ct);
        await Assert.ThrowsAsync<RightsBlockedException>(() => core.ReadAsync(Outsider, Geeta, item.Id, Ct));
        Assert.DoesNotContain(await core.ListAsync(Outsider, Geeta, Ct), r => r.Id == item.Id);
        Assert.Equal(2, (await core.ReadAsync(Owner, Geeta, item.Id, Ct)).Revisions.Count);
        var unknown = await SourceTestFixture.CreateAsync(sources, Owner, Geeta, false);
        var draft = await core.CreateAsync(Owner, Geeta, (await RequestAsync(core, Owner, Geeta)) with { Kind = "Translation", RightsStatus = "Licensed" }, Ct);
        await sources.LinkAsync(Owner, Geeta, new(Guid.NewGuid(), unknown.Snapshot, null, draft.Revisions[0].Id, null, "fixture"), Ct);
        await Assert.ThrowsAsync<RightsBlockedException>(() => core.ReadAsync(Outsider, Geeta, draft.Id, Ct));
    }
}

