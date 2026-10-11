using System.Diagnostics;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeetaStudio.Api;

public static class RuntimeEndpoints
{
    private static string Actor(HttpContext c) => c.User.Identity?.Name ?? "unknown";
    private static string Trace(HttpContext c) => Activity.Current?.TraceId.ToString() ?? c.TraceIdentifier;
    public static void MapRuntime(this WebApplication app)
    {
        app.MapGet("/health/ready", async (RuntimeDbContext db, CoreDbContext core, CancellationToken ct) =>
        {
            try
            {
                if (!await db.Database.CanConnectAsync(ct) || (await db.Database.GetPendingMigrationsAsync(ct)).Any())
                    return Results.Json(new { status = "NotReady" }, statusCode: 503);
                _ = await db.Budgets.AsNoTracking().CountAsync(ct);
                if ((await core.Database.GetPendingMigrationsAsync(ct)).Any()) return Results.Json(new { status = "NotReady" }, statusCode: 503);
                _ = await core.Tenants.AsNoTracking().CountAsync(ct);
                _ = await core.SourceWorks.AsNoTracking().CountAsync(ct);
                _ = await core.CorpusManifests.AsNoTracking().CountAsync(ct); _ = await core.Claims.AsNoTracking().CountAsync(ct);
                return Results.Ok(new { status = "Ready" });
            }
            catch (Exception) { return Results.Json(new { status = "NotReady" }, statusCode: 503); }
        }).AllowAnonymous();
        var group = app.MapGroup("/api/v1/runtime").RequireAuthorization("StaffRead");
        group.MapGet("/adapters", (RuntimeDbContext db, CancellationToken ct) => db.Definitions.AsNoTracking().ToListAsync(ct));
        group.MapGet("/providers", (IProviderConfigurationStore s, CancellationToken ct) => s.ProvidersAsync(ct));
        group.MapPost("/providers", async (RegisterProvider r, IProviderConfigurationStore s, HttpContext c, CancellationToken ct) =>
            Results.Ok(await s.RegisterAsync(r, Actor(c), Trace(c), ct))).RequireAuthorization("StaffAdmin");
        group.MapPost("/providers/{id:guid}/test", async (Guid id, IProviderConfigurationStore s, IRuntimeStore runtime, HttpContext c, CancellationToken ct) =>
        { await s.TestAsync(id, ct); await runtime.AuditAsync(Actor(c), "provider.validate-test", id, Trace(c), ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        group.MapPost("/providers/{id:guid}/enable", async (Guid id, IProviderConfigurationStore s, HttpContext c, CancellationToken ct) =>
        { await s.SetEnabledAsync(id, true, false, Actor(c), Trace(c), ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        group.MapPost("/providers/{id:guid}/disable", async (Guid id, IProviderConfigurationStore s, HttpContext c, CancellationToken ct) =>
        { await s.SetEnabledAsync(id, false, false, Actor(c), Trace(c), ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        group.MapPost("/providers/{id:guid}/kill", async (Guid id, IProviderConfigurationStore s, HttpContext c, CancellationToken ct) =>
        { await s.SetEnabledAsync(id, false, true, Actor(c), Trace(c), ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        group.MapPost("/revisions", async (CreateRevision r, IProviderConfigurationStore s, HttpContext c, CancellationToken ct) =>
            Results.Ok(new { id = await s.RevisionAsync(r, Actor(c), Trace(c), ct) })).RequireAuthorization("StaffAdmin");
        group.MapGet("/revisions", (RuntimeDbContext db, CancellationToken ct) => db.Revisions.AsNoTracking().OrderByDescending(r => r.CreatedUtc).Take(100).ToListAsync(ct));
        group.MapGet("/active-route", (RuntimeDbContext db, CancellationToken ct) => db.Routes.AsNoTracking().SingleOrDefaultAsync(ct));
        foreach (var action in new[] { "activate", "rollback" })
            group.MapPost("/revisions/{id:guid}/" + action, async (Guid id, IProviderConfigurationStore s, HttpContext c, CancellationToken ct) =>
            { await s.ActivateAsync(id, Actor(c), Trace(c), ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        group.MapGet("/jobs", (IRuntimeStore s, CancellationToken ct) => s.JobsAsync(ct));
        group.MapGet("/jobs/{id:guid}", async (Guid id, IRuntimeStore s, CancellationToken ct) =>
            await s.InspectAsync(id, ct) is { } job ? Results.Ok(job) : Results.NotFound());
        group.MapPost("/jobs", async (CreateJob r, IRuntimeStore s, HttpContext c, CancellationToken ct) =>
            Results.Ok(new { id = await s.CreateAsync(r, Actor(c), Trace(c), ct) })).RequireAuthorization("StaffOperate");
        group.MapPost("/jobs/{id:guid}/cancel", async (Guid id, IRuntimeStore s, HttpContext c, CancellationToken ct) =>
        { await s.CancelAsync(id, Actor(c), Trace(c), ct); return Results.NoContent(); }).RequireAuthorization("StaffOperate");
        group.MapPost("/jobs/{id:guid}/reconcile", async (Guid id, IRuntimeStore s, HttpContext c, CancellationToken ct) =>
        { await s.ReconcileAsync(id, Actor(c), Trace(c), ct); return Results.NoContent(); }).RequireAuthorization("StaffOperate");
        group.MapPost("/jobs/{id:guid}/retry", async (Guid id, IRuntimeStore s, HttpContext c, CancellationToken ct) =>
        { await s.RetryDeadLetterAsync(id, Actor(c), Trace(c), ct); return Results.NoContent(); }).RequireAuthorization("StaffOperate");
        group.MapGet("/audit", (RuntimeDbContext db, CancellationToken ct) => db.Audit.AsNoTracking().OrderByDescending(a => a.CreatedUtc).Take(100).ToListAsync(ct));
        group.MapGet("/budgets", (RuntimeDbContext db, CancellationToken ct) => db.Budgets.AsNoTracking().ToListAsync(ct));
        group.MapPost("/assets", async (HttpContext c, IAssetStore store, CancellationToken ct) =>
        {
            if (c.Request.ContentLength is null or < 1 or > 1048576) return Results.BadRequest(new { error = "Provide 1 to 1048576 bytes with Content-Length." });
            using var memory = new MemoryStream(); await c.Request.Body.CopyToAsync(memory, ct);
            return Results.Ok(new { id = await store.PutAsync(memory.ToArray(), Actor(c), ct) });
        }).RequireAuthorization("StaffOperate");
        group.MapGet("/assets/{id:guid}", async (Guid id, HttpContext c, IAssetStore store, CancellationToken ct) =>
            await store.GetAsync(id, Actor(c), ct) is { } bytes ? Results.File(bytes, "application/octet-stream") : Results.NotFound());
    }
}


