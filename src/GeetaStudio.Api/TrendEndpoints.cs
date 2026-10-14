using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Mvc;
namespace GeetaStudio.Api;

public static class TrendEndpoints
{
    public static void MapTrends(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/trends/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? throw new InvalidOperationException());
        g.MapGet("/connectors", (Guid tenant, Guid universe, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.ConnectorsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/connectors", (Guid tenant, Guid universe, RegisterTrendConnector r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.ConnectorAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/connectors/{id:guid}/disable", async (Guid tenant, Guid universe, Guid id, TrendChange r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => { await w.DisableAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/poll", (Guid tenant, Guid universe, PollTrends r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.PollAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/manual", (Guid tenant, Guid universe, SubmitTrendSignal r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.SubmitAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAuthor");
        g.MapGet("/clusters", (Guid tenant, Guid universe, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.ClustersAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/clusters/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, ReviewTrendCluster r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => { await w.ReviewClusterAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapPost("/clusters/{id:guid}/change", async (Guid tenant, Guid universe, Guid id, TrendChange r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => { await w.ChangeAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapGet("/teachings", (Guid tenant, Guid universe, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.TeachingsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/teachings", (Guid tenant, Guid universe, LinkTrendTeaching r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.LinkAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAuthor");
        g.MapPost("/teachings/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, TrendReview r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => { await w.ReviewTeachingAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapGet("/stories", (Guid tenant, Guid universe, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.StoriesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/stories", (Guid tenant, Guid universe, PrepareTrendStory r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.PrepareAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/stories/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, TrendReview r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => { await w.ReviewStoryAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapPost("/bind", async (Guid tenant, Guid universe, BindTrendStory r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => { await w.BindAsync(Scope(tenant, c), universe, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/remediations", (Guid tenant, Guid universe, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.RemediationsAsync(Scope(tenant, c), universe, ct));
        g.MapGet("/stats", (Guid tenant, Guid universe, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.StatsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/rank", (Guid tenant, Guid universe, RankTrends r, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => w.RankAsync(Scope(tenant, c), universe, r, ct));
        g.MapPost("/recheck", async (Guid tenant, Guid universe, [FromServices] ITrendWorkbench w, HttpContext c, CancellationToken ct) => { await w.RecheckAsync(Scope(tenant, c), universe, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        app.MapGet("/staff/trends", (HttpContext c) => { c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(TrendPage.Html, "text/html"); }).RequireAuthorization("StaffRead");
    }
}
