using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Mvc;
namespace GeetaStudio.Api;

public static class MediaEndpoints
{
    public static void MapMedia(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/media/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        static CoreScope Scope(Guid t, HttpContext c) => new(t, c.User.Identity?.Name ?? throw new InvalidOperationException());
        g.MapGet("/assets", (Guid tenant, Guid universe, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.AssetsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/assets", (Guid tenant, Guid universe, ImportMedia r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.ImportAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAuthor");
        g.MapPost("/versions/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, ReviewMedia r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => { await w.ReviewAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapGet("/versions/{id:guid}/preview", async (Guid tenant, Guid universe, Guid id, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => { var e = await w.ExportAsync(Scope(tenant, c), universe, id, "Read", ct); return Results.File(e.Bytes, e.Version.Info.Mime); });
        g.MapGet("/versions/{id:guid}/export", (Guid tenant, Guid universe, Guid id, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.ExportAsync(Scope(tenant, c), universe, id, "Export", ct));
        g.MapPost("/move", (Guid tenant, Guid universe, MoveMedia r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.MoveAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/profiles", (Guid tenant, Guid universe, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.ProfilesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/profiles", (Guid tenant, Guid universe, RegisterMediaProfile r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.ProfileAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/profiles/{id:guid}/disable", async (Guid tenant, Guid universe, Guid id, MediaProfileControl r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => { await w.DisableAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/generations", (Guid tenant, Guid universe, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.GenerationsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/generations", (Guid tenant, Guid universe, GenerateMedia r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.GenerateAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/reconcile", async (Guid tenant, Guid universe, ReconcileMedia r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => { await w.ReconcileAsync(Scope(tenant, c), universe, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/callbacks", (Guid tenant, Guid universe, AcceptMediaResult r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.AcceptAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/packages", (Guid tenant, Guid universe, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.PackagesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/packages", (Guid tenant, Guid universe, RegisterCharacterMedia r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.PackageAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/packages/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, ReviewMedia r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => { await w.ReviewPackageAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapPost("/packages/{id:guid}/bind", (Guid tenant, Guid universe, Guid id, MediaBindRequest r, [FromServices] IMediaLibrary w, HttpContext c, CancellationToken ct) => w.BindPackageAsync(Scope(tenant, c), universe, id, r.OperationId, ct)).RequireAuthorization("StaffAdmin");
        app.MapGet("/staff/media", (HttpContext c) => { c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(MediaPage.Html, "text/html"); }).RequireAuthorization("StaffRead");
    }
}
public sealed record MediaBindRequest(Guid OperationId);
