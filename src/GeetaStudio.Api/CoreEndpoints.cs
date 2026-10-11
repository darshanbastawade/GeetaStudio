using ContentOS.Application;
using ContentOS.Contracts;

namespace GeetaStudio.Api;

public static class CoreEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext context) => new(tenant, context.User.Identity?.Name ?? "");
    public static void MapCore(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/core").RequireAuthorization("StaffRead");
        group.MapGet("/tenants", (IContentCore core, HttpContext c, CancellationToken ct) => core.TenantsAsync(c.User.Identity?.Name ?? "", ct));
        group.MapGet("/tenants/{tenant:guid}/universes", (Guid tenant, IContentCore core, HttpContext c, CancellationToken ct) => core.UniversesAsync(Scope(tenant, c), ct));
        group.MapGet("/tenants/{tenant:guid}/universes/{universe:guid}/catalog",
            (Guid tenant, Guid universe, IContentCore core, HttpContext c, CancellationToken ct) => core.CatalogAsync(Scope(tenant, c), universe, ct));
        group.MapGet("/tenants/{tenant:guid}/universes/{universe:guid}/content",
            (Guid tenant, Guid universe, IContentCore core, HttpContext c, CancellationToken ct) => core.ListAsync(Scope(tenant, c), universe, ct));
        group.MapGet("/tenants/{tenant:guid}/universes/{universe:guid}/content/{id:guid}",
            (Guid tenant, Guid universe, Guid id, IContentCore core, HttpContext c, CancellationToken ct) => core.ReadAsync(Scope(tenant, c), universe, id, ct));
        group.MapPost("/tenants/{tenant:guid}/universes/{universe:guid}/content",
            (Guid tenant, Guid universe, CreateContent r, IContentCore core, HttpContext c, CancellationToken ct) => core.CreateAsync(Scope(tenant, c), universe, r, ct))
            .RequireAuthorization("StaffOperate");
        group.MapPost("/tenants/{tenant:guid}/universes/{universe:guid}/content/{id:guid}/revisions",
            (Guid tenant, Guid universe, Guid id, ReviseContent r, IContentCore core, HttpContext c, CancellationToken ct) => core.ReviseAsync(Scope(tenant, c), universe, id, r, ct))
            .RequireAuthorization("StaffOperate");
        group.MapPost("/tenants/{tenant:guid}/universes/{universe:guid}/content/{id:guid}/editorial-state",
            (Guid tenant, Guid universe, Guid id, ChangeEditorialState r, IContentCore core, HttpContext c, CancellationToken ct) => core.TransitionAsync(Scope(tenant, c), universe, id, r, ct))
            .RequireAuthorization("StaffOperate");
        group.MapGet("/tenants/{tenant:guid}/universes/{universe:guid}/content/{id:guid}/export",
            (Guid tenant, Guid universe, Guid id, IContentCore core, HttpContext c, CancellationToken ct) => core.ExportAsync(Scope(tenant, c), universe, id, ct));
        group.MapPost("/tenants/{tenant:guid}/universes/{universe:guid}/import",
            (Guid tenant, Guid universe, ImportContent r, IContentCore core, HttpContext c, CancellationToken ct) => core.ImportAsync(Scope(tenant, c), universe, r, ct))
            .RequireAuthorization("StaffOperate");
        group.MapPost("/tenants/{tenant:guid}/universes/{universe:guid}/grants",
            async (Guid tenant, Guid universe, ShareUniverse r, IContentCore core, IRuntimeStore audit, HttpContext c, CancellationToken ct) =>
            {
                await core.ShareAsync(Scope(tenant, c), universe, r, ct);
                await audit.AuditAsync(c.User.Identity!.Name!, "core.universe-grant", universe, c.TraceIdentifier, ct);
                return Results.NoContent();
            }).RequireAuthorization("StaffAdmin");
    }
}
