using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class AiEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapAi(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/ai/tenants/{tenant:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/profiles", (Guid tenant, IAiAdministration a, HttpContext c, CancellationToken ct) => a.ProfilesAsync(Scope(tenant, c), ct));
        g.MapPost("/profiles", (Guid tenant, RegisterAiProfile r, IAiAdministration a, HttpContext c, CancellationToken ct) => a.RegisterAsync(Scope(tenant, c), r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/profiles/{id:guid}/switch", async (Guid tenant, Guid id, AiConnectionSwitch r, IAiAdministration a, HttpContext c, CancellationToken ct) => { await a.SwitchAsync(Scope(tenant, c), id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/routes", (Guid tenant, IAiAdministration a, HttpContext c, CancellationToken ct) => a.RoutesAsync(Scope(tenant, c), ct));
        g.MapPost("/routes", (Guid tenant, AiRouteSettings r, IAiAdministration a, HttpContext c, CancellationToken ct) => a.RouteAsync(Scope(tenant, c), r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/routes/{id:guid}/activate", async (Guid tenant, Guid id, IAiAdministration a, HttpContext c, CancellationToken ct) => { await a.ActivateAsync(Scope(tenant, c), id, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/budgets", (Guid tenant, IAiAdministration a, HttpContext c, CancellationToken ct) => a.BudgetsAsync(Scope(tenant, c), ct));
        g.MapPost("/budgets", (Guid tenant, ConfigureAiBudget r, IAiAdministration a, HttpContext c, CancellationToken ct) => a.BudgetAsync(Scope(tenant, c), r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/runs", (Guid tenant, IAiTaskRouter a, HttpContext c, CancellationToken ct) => a.RunsAsync(Scope(tenant, c), ct));
        g.MapGet("/runs/{id:guid}", (Guid tenant, Guid id, IAiTaskRouter a, HttpContext c, CancellationToken ct) => a.InspectAsync(Scope(tenant, c), id, ct));
        g.MapPost("/runs/{id:guid}/recover", async (Guid tenant, Guid id, IAiAdministration a, HttpContext c, CancellationToken ct) => { await a.RecoverAsync(Scope(tenant, c), id, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/runs/{id:guid}/attempts/{attempt:guid}/reconcile", async (Guid tenant, Guid id, Guid attempt, AiReconcile r, IAiAdministration a, HttpContext c, CancellationToken ct) => { await a.ReconcileAsync(Scope(tenant, c), id, attempt, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/universes/{universe:guid}/generate", (Guid tenant, Guid universe, GenerateAi r, IAiTaskRouter a, HttpContext c, CancellationToken ct) => a.GenerateAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/universes/{universe:guid}/stream", async (Guid tenant, Guid universe, GenerateAi r, IAiTaskRouter a, HttpContext c, CancellationToken ct) =>
        {
            await foreach (var e in a.StreamAsync(Scope(tenant, c), universe, r, ct))
            {
                c.Response.ContentType = "application/x-ndjson"; c.Response.Headers.CacheControl = "no-store";
                await c.Response.WriteAsync(JsonSerializer.Serialize(e, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "\n", ct); await c.Response.Body.FlushAsync(ct);
            }
        }).RequireAuthorization("StaffAdmin");
        app.MapGet("/staff/ai", () => Results.Content(AiPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
