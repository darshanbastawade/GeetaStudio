using ContentOS.Application;
using ContentOS.Contracts;

namespace GeetaStudio.Api;

public static class SourceEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapSources(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/sources/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/", (Guid tenant, Guid universe, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.ListAsync(Scope(tenant, c), universe, ct));
        g.MapGet("/works/{work:guid}", (Guid tenant, Guid universe, Guid work, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.InspectAsync(Scope(tenant, c), universe, work, ct));
        g.MapGet("/review-queue", (Guid tenant, Guid universe, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.ReviewQueueAsync(Scope(tenant, c), universe, ct));
        g.MapGet("/remediation", (Guid tenant, Guid universe, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.RemediationAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/works", (Guid tenant, Guid universe, RegisterSource r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.RegisterAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/works/{work:guid}/editions", (Guid tenant, Guid universe, Guid work, RegisterEdition r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.EditionAsync(Scope(tenant, c), universe, work, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/editions/{edition:guid}/permissions", (Guid tenant, Guid universe, Guid edition, GrantSourcePermission r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.PermissionAsync(Scope(tenant, c), universe, edition, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/permissions/{permission:guid}/review", async (Guid tenant, Guid universe, Guid permission, ReviewSource r, ISourceRegistry s, HttpContext c, CancellationToken ct) => { await s.ReviewPermissionAsync(Scope(tenant, c), universe, permission, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/editions/{edition:guid}/files", (Guid tenant, Guid universe, Guid edition, ImportSourceFile r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.ImportAsync(Scope(tenant, c), universe, edition, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/snapshots/{snapshot:guid}/review", async (Guid tenant, Guid universe, Guid snapshot, ReviewSource r, ISourceRegistry s, HttpContext c, CancellationToken ct) => { await s.ReviewSnapshotAsync(Scope(tenant, c), universe, snapshot, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/snapshots/{snapshot:guid}/evaluate", (Guid tenant, Guid universe, Guid snapshot, IntendedUse r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.EvaluateAsync(Scope(tenant, c), universe, snapshot, r, ct));
        g.MapPost("/provenance", (Guid tenant, Guid universe, LinkEvidence r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.LinkAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/uses", (Guid tenant, Guid universe, RecordSourceUse r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.RecordUseAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/works/{work:guid}/revoke", (Guid tenant, Guid universe, Guid work, RevokeSource r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.RevokeAsync(Scope(tenant, c), universe, work, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/remediation/{id:guid}/resolve", async (Guid tenant, Guid universe, Guid id, ResolveRemediation r, ISourceRegistry s, HttpContext c, CancellationToken ct) => { await s.ResolveAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/content/{content:guid}/export", (Guid tenant, Guid universe, Guid content, IntendedUse r, ISourceRegistry s, HttpContext c, CancellationToken ct) => s.ExportAsync(Scope(tenant, c), universe, content, r, ct));
        app.MapGet("/staff/sources", () => Results.Content(SourceAdminPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
