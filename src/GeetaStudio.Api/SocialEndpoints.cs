using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Mvc;
namespace GeetaStudio.Api;

public static class SocialEndpoints
{
    public static void MapSocial(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/social/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        static CoreScope Scope(Guid t, HttpContext c) => new(t, c.User.Identity?.Name ?? throw new InvalidOperationException());
        g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        g.MapGet("/capabilities", (Guid tenant, Guid universe, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.CapabilitiesAsync(Scope(tenant, c), universe, ct));
        g.MapGet("/accounts", (Guid tenant, Guid universe, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.AccountsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/accounts", (Guid tenant, Guid universe, RegisterSocialAccount r, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.RegisterAccountAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/accounts/{id:guid}/control", async (Guid tenant, Guid universe, Guid id, SocialAccountControl r, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => { await w.ControlAccountAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/packages", (Guid tenant, Guid universe, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.PackagesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/packages", (Guid tenant, Guid universe, CreateSocialPackage r, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.PrepareAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAuthor");
        g.MapPost("/packages/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, SocialReview r, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => { await w.ReviewAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        g.MapGet("/packages/{id:guid}/export", (Guid tenant, Guid universe, Guid id, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.ExportAsync(Scope(tenant, c), universe, id, ct));
        g.MapPost("/publications", (Guid tenant, Guid universe, ScheduleSocial r, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.ScheduleAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffPublish");
        g.MapGet("/publications", (Guid tenant, Guid universe, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.PublicationsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/publications/{id:guid}/control", async (Guid tenant, Guid universe, Guid id, SocialControl r, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => { await w.ControlAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffPublish");
        // This authenticated staff receipt is a local fixture callback/manual record, not a public vendor webhook.
        g.MapPost("/publications/{id:guid}/receipt", async (Guid tenant, Guid universe, Guid id, SocialReceipt r, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => { await w.ReceiptAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffPublish");
        g.MapGet("/notifications", (Guid tenant, Guid universe, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => w.NotificationsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/notifications/{id:guid}/ack", async (Guid tenant, Guid universe, Guid id, SocialControl r, [FromServices] ISocialPublishing w, HttpContext c, CancellationToken ct) => { await w.AcknowledgeAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); });
        app.MapGet("/staff/social", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(SocialPage.Html, "text/html"); }).RequireAuthorization("StaffRead");
    }
}
