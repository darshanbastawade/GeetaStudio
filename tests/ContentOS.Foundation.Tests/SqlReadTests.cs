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
public sealed class SqlReadTests
{
    [SqlFact]
    public async Task Reviewed_preview_variants_survive_restart_and_withdrawal_or_revocation_removes_them()
    {
        using var root = ReadTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await ReadTestFixture.Setup(s);
        var read = s.GetRequiredService<IReadWorkbench>(); var publicRead = s.GetRequiredService<IPublicRead>();
        var book = (await read.PreviewAsync(f.Owner.Actor, f.Collection, default))!;
        Assert.Equal(4, book.Pieces.Length); Assert.Equal(2, book.AvailableVerses); Assert.Equal(2, book.ExpectedVerses); Assert.Contains(book.Pieces, p => p.Locale == "mr-IN");
        Assert.Equal(2, book.Pieces.Where(x => x.Chapter == 1 && x.Verse == 1 && x.Locale == "en-IN").Select(x => x.AudienceId).Distinct().Count());
        Assert.All(book.Pieces, p => { Assert.NotEmpty(p.Sources); Assert.Contains(p.Sources, x => x.Kind == "DirectQuotation" && x.Locale == "sa-IN"); Assert.All(p.Sources, x => Assert.NotEmpty(x.Attribution)); });
        using var restart = root.CreateScope(); Assert.Equal(4, (await restart.ServiceProvider.GetRequiredService<IReadWorkbench>().PreviewAsync(f.Owner.Actor, f.Collection, default))!.Pieces.Length);
        var status = await read.InspectAsync(f.Owner, f.Universe, f.Collection, default);
        Assert.Contains("FixtureCollectionCannotRollOut", status.ReleaseBlocks);
        foreach (var gate in ReadRules.Gates) await read.EvidenceAsync(f.Owner, f.Universe, f.Collection, new(Guid.NewGuid(), gate, "Fixture", "fixture:local-check-not-human-release", "Human acceptance pending.", DateTimeOffset.UtcNow.AddDays(1)), default);
        s.GetRequiredService<IConfiguration>()["Read:PublicEnabled"] = "true";
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => read.ControlAsync(f.Owner, f.Universe, f.Collection, new(Guid.NewGuid(), status.RowVersion, "Activate"), default));
        Assert.Empty(await publicRead.DirectoryAsync(default)); Assert.Null(await publicRead.GetAsync(f.Key, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => publicRead.EventAsync(f.Key, new(1, f.Pieces[0], "Read", true), default));
        var editorial = s.GetRequiredService<IEditorialWorkbench>(); await editorial.ActAsync(f.Owner, f.Universe, f.Items[0].Id, EditorialTestFixture.Action(f.Items[0], "Withdraw"), default);
        book = (await read.PreviewAsync(f.Owner.Actor, f.Collection, default))!; Assert.Equal(3, book.Pieces.Length); Assert.DoesNotContain(book.Pieces, x => x.Id == f.Pieces[0]);
        var sources = s.GetRequiredService<ISourceRegistry>(); var detail = await sources.InspectAsync(f.Owner, f.Universe, f.SourceWork, default);
        await sources.RevokeAsync(f.Owner, f.Universe, f.SourceWork, new(Guid.NewGuid(), null, "Fixture revocation", detail.Work.RowVersion), default);
        book = (await read.PreviewAsync(f.Owner.Actor, f.Collection, default))!; Assert.Empty(book.Pieces);
        Assert.Empty(await publicRead.DirectoryAsync(default));
    }
    [SqlFact]
    public async Task Collection_replay_scope_concurrency_and_disable_preserve_immutable_bindings()
    {
        using var root = ReadTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await ReadTestFixture.Setup(s); var read = s.GetRequiredService<IReadWorkbench>(); var db = s.GetRequiredService<CoreDbContext>();
        var manifest = await db.ReadCollections.Where(x => x.Id == f.Collection).Select(x => x.ManifestId).SingleAsync();
        var request = new RegisterReadCollection(Guid.NewGuid(), f.Key, 2, "Fixture new version", manifest, "Fixture", "Explicit reviewed pilot version");
        using var one = root.CreateScope(); using var two = root.CreateScope();
        var ids = await Task.WhenAll(one.ServiceProvider.GetRequiredService<IReadWorkbench>().RegisterAsync(f.Owner, f.Universe, request, default), two.ServiceProvider.GetRequiredService<IReadWorkbench>().RegisterAsync(f.Owner, f.Universe, request, default));
        Assert.Equal(ids[0], ids[1]);
        await Assert.ThrowsAsync<CoreAccessException>(() => read.InspectAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, f.Collection, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => read.InspectAsync(f.Owner, Guid.NewGuid(), f.Collection, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => read.ControlAsync(new(f.Owner.TenantId, "m09-reader"), f.Universe, f.Collection, new(Guid.NewGuid(), "", "Disable"), default));
        var status = await read.InspectAsync(f.Owner, f.Universe, f.Collection, default);
        await read.ControlAsync(f.Owner, f.Universe, f.Collection, new(Guid.NewGuid(), status.RowVersion, "Disable"), default);
        Assert.Null(await read.PreviewAsync(f.Owner.Actor, f.Collection, default));
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => read.ControlAsync(f.Owner, f.Universe, f.Collection, new(Guid.NewGuid(), status.RowVersion, "Disable"), default));
        db.ChangeTracker.Clear(); var piece = await db.ReadPieces.FirstAsync(x => x.CollectionId == f.Collection); piece.Position++;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Equal(4, await db.ReadPieces.CountAsync(x => x.CollectionId == f.Collection));
    }
    [SqlFact]
    public async Task Anonymous_routes_never_expose_fixture_drafts_and_staff_preview_is_scoped_and_no_store()
    {
        using var root = ReadTestFixture.Services(); using var scope = root.CreateScope(); var f = await ReadTestFixture.Setup(scope.ServiceProvider);
        using var host = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Runtime:Enabled"] = "true",
                ["ConnectionStrings:ContentOS"] = LessonTestFixture.Connection,
                ["Staff:Administrator:0"] = "integration-test-Administrator",
                ["Read:PublicEnabled"] = "true",
                ["Read:RetentionWorkerEnabled"] = "false",
                ["Factory:WorkerEnabled"] = "false",
                ["Workflows:WorkerEnabled"] = "false",
                ["Knowledge:IndexWorkerEnabled"] = "false"
            }));
            b.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/read-preview/" + f.Collection)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/staff/read-collections")).StatusCode);
        var publicResponse = await client.GetAsync("/api/v1/read/" + f.Key); Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode); Assert.True(publicResponse.Headers.CacheControl!.NoStore);
        Assert.Equal("[]", await client.GetStringAsync("/api/v1/read"));
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        var preview = await client.GetAsync("/api/v1/read-preview/" + f.Collection); Assert.Equal(HttpStatusCode.OK, preview.StatusCode); Assert.True(preview.Headers.CacheControl!.NoStore); Assert.Contains("noindex", preview.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Equal(4, (await preview.Content.ReadFromJsonAsync<ReadBook>())!.Pieces.Length);
        var path = $"/api/v1/read-admin/tenants/{f.Owner.TenantId}/universes/{f.Universe}/collections/{f.Collection}";
        var status = (await client.GetFromJsonAsync<ReadStaffView>(path))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/control", new ControlReadCollection(Guid.NewGuid(), status.RowVersion, "Activate"))).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(path + "/control", new ControlReadCollection(Guid.NewGuid(), status.RowVersion, "Activate"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/read-admin/tenants/{f.Owner.TenantId}/universes/{Guid.NewGuid()}/collections/{f.Collection}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        HttpStatusCode last = 0; for (var i = 0; i < 65; i++) last = (await client.GetAsync("/api/v1/read")).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
