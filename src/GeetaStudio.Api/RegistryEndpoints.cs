using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class RegistryEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapRegistry(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/registry/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapPost("/proposals", async (Guid tenant, Guid universe, IUniverseRegistry a, IContentCore core, HttpContext c, IHostEnvironment env, CancellationToken ct) =>
        {
            var catalog = await core.CatalogAsync(Scope(tenant, c), universe, ct);
            var path = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "docs", "fixtures", "M10-bibles.json"));
            var data = JsonSerializer.Deserialize<RegistrySeedDocument[]>(await File.ReadAllTextAsync(path, ct), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var seed = data.SingleOrDefault(x => x.UniverseKey == catalog.Universe.Key) ?? throw new ContentOS.Domain.RuntimeValidationException("No proposed bible for this universe. Register your own rules.");
            await a.ProposeDefaultsAsync(Scope(tenant, c), universe, seed, ct); return Results.Ok();
        }).RequireAuthorization("StaffAdmin");
        g.MapGet("/bibles", (Guid tenant, Guid universe, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.BiblesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/bibles", (Guid tenant, Guid universe, RegisterBible r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.RegisterBibleAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/characters", (Guid tenant, Guid universe, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.CharactersAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/characters", (Guid tenant, Guid universe, RegisterRegistryCharacter r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.RegisterCharacterAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/characters/{id:guid}/lifecycle", (Guid tenant, Guid universe, Guid id, CharacterLifecycle r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.ControlCharacterAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/presentations", (Guid tenant, Guid universe, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.PresentationsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/presentations", (Guid tenant, Guid universe, RegisterRegistryPresentation r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.RegisterPresentationAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/relationships", (Guid tenant, Guid universe, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.RelationshipsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/relationships", (Guid tenant, Guid universe, RegisterRegistryRelationship r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.RegisterRelationshipAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/revisions/{id:guid}/review", (Guid tenant, Guid universe, Guid id, RegistryReview r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.ReviewAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/episodes", (Guid tenant, Guid universe, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.EpisodesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/episodes", (Guid tenant, Guid universe, RegisterRegistryEpisode r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.RegisterEpisodeAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/cast", (Guid tenant, Guid universe, CastingBrief r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.CastAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/stories", (Guid tenant, Guid universe, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.StoriesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/stories", (Guid tenant, Guid universe, ComposeRegistryEpisode r, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.ComposeAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/stories/{id:guid}/export", (Guid tenant, Guid universe, Guid id, IUniverseRegistry a, HttpContext c, CancellationToken ct) => a.ExportAsync(Scope(tenant, c), universe, id, ct));
        app.MapGet("/staff/characters", () => Results.Content(RegistryPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
