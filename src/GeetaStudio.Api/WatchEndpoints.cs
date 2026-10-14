using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Mvc;
namespace GeetaStudio.Api;

public static class WatchEndpoints
{
    public static void MapWatch(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/watch/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead"); static CoreScope Scope(Guid t, HttpContext c) => new(t, c.User.Identity?.Name ?? throw new InvalidOperationException());
        g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        g.MapGet("/templates", (Guid tenant, Guid universe, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => w.TemplatesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/templates", (Guid tenant, Guid universe, RegisterWatchTemplate r, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => w.TemplateAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/templates/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, WatchReview r, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => { await w.ReviewTemplateAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapGet("/profiles", (Guid tenant, Guid universe, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => w.ProfilesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/profiles", (Guid tenant, Guid universe, RegisterWatchProfile r, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => w.ProfileAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/profiles/{id:guid}/disable", async (Guid tenant, Guid universe, Guid id, WatchControl r, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => { await w.DisableProfileAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/productions", (Guid tenant, Guid universe, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => w.ListAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/productions", (Guid tenant, Guid universe, CreateWatch r, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => w.CreateAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/productions/{id:guid}", (Guid tenant, Guid universe, Guid id, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => w.InspectAsync(Scope(tenant, c), universe, id, ct));
        g.MapPost("/productions/{id:guid}/control", async (Guid tenant, Guid universe, Guid id, WatchControl r, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => { await w.ControlAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/productions/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, WatchReview r, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => { await w.ReviewAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapGet("/productions/{id:guid}/export", (Guid tenant, Guid universe, Guid id, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => w.ExportAsync(Scope(tenant, c), universe, id, "Export", ct));
        g.MapGet("/productions/{id:guid}/captions.vtt", async (Guid tenant, Guid universe, Guid id, [FromServices] IWatchFactory w, [FromServices] ISubtitleProvider captions, HttpContext c, CancellationToken ct) => { var view = await w.InspectAsync(Scope(tenant, c), universe, id, ct); if (view.Timeline == null || view.Blocks.Any(x => x != "FinalEpisodeReviewRequired")) return Results.Conflict(); return Results.Text(captions.Vtt(view.Timeline.Captions), "text/vtt"); });
        g.MapGet("/artifacts/{id:guid}/preview", async (Guid tenant, Guid universe, Guid id, [FromServices] IWatchFactory w, HttpContext c, CancellationToken ct) => { var output = await w.PreviewAsync(Scope(tenant, c), universe, id, ct); return Results.File(output.Bytes, output.Mime, enableRangeProcessing: true); });
        app.MapGet("/staff/watch", (HttpContext c) => { c.Response.Headers["X-Robots-Tag"] = "noindex"; c.Response.Headers.CacheControl = "no-store"; return Results.Content(WatchPage.Html, "text/html"); }).RequireAuthorization("StaffRead");
    }
}
