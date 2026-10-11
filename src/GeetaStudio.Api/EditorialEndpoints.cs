using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class EditorialEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapEditorial(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/editorial/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/items", (Guid tenant, Guid universe, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.ListAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/items", (Guid tenant, Guid universe, StartEditorial r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.StartAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAuthor");
        g.MapGet("/items/{id:guid}", (Guid tenant, Guid universe, Guid id, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.InspectAsync(Scope(tenant, c), universe, id, ct));
        g.MapPost("/items/{id:guid}/edit", (Guid tenant, Guid universe, Guid id, EditEditorial r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.EditAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAuthor");
        g.MapPost("/items/{id:guid}/actions", (Guid tenant, Guid universe, Guid id, EditorialAction r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.ActAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffCms");
        g.MapPost("/items/{id:guid}/assets", (Guid tenant, Guid universe, Guid id, AttachEditorialAsset r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.AttachAssetAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAuthor");
        g.MapGet("/assets/{id:guid}/preview", async (Guid tenant, Guid universe, Guid id, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => { var asset = await e.PreviewAssetAsync(Scope(tenant, c), universe, id, ct); c.Response.Headers["X-Content-Type-Options"] = "nosniff"; return Results.File(asset.Bytes, asset.MediaType); });
        g.MapGet("/corrections", (Guid tenant, Guid universe, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.CorrectionsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/corrections", (Guid tenant, Guid universe, EditorialCorrection r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.CorrectAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffEditor");
        g.MapPost("/corrections/{id:guid}/resolve", async (Guid tenant, Guid universe, Guid id, ResolveEditorialCorrection r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => { await e.ResolveCorrectionAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffCms");
        g.MapGet("/providers", (Guid tenant, Guid universe, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.ProvidersAsync(Scope(tenant, c), universe, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/routes", (Guid tenant, Guid universe, EditorialRouteCommand r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.RouteAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/trends", (Guid tenant, Guid universe, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.TrendsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/trends", (Guid tenant, Guid universe, EditorialTrendInput r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => e.SubmitTrendAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAuthor");
        g.MapPost("/trends/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, ReviewEditorialTrend r, IEditorialWorkbench e, HttpContext c, CancellationToken ct) => { await e.ReviewTrendAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffEditor");
        app.MapGet("/staff/editorial", () => Results.Content(EditorialPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
