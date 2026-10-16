using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class FactoryEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapFactory(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/factory/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/generation-workflows", (Guid tenant, Guid universe, IWorkflowRuntime workflows, HttpContext c, CancellationToken ct) => workflows.DefinitionsAsync(Scope(tenant, c), universe, ct));
        g.MapGet("/plans", (Guid tenant, Guid universe, IDraftFactory f, HttpContext c, CancellationToken ct) => f.PlansAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/plans", (Guid tenant, Guid universe, RegisterFactoryPlan r, IDraftFactory f, HttpContext c, CancellationToken ct) => f.RegisterPlanAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/pilots", (Guid tenant, Guid universe, FactoryPilotRequest r, IDraftFactory f, HttpContext c, CancellationToken ct) => f.ProposePilotsAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/plans/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, FactoryReview r, IDraftFactory f, HttpContext c, CancellationToken ct) => { await f.ReviewPlanAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/plans/{id:guid}/estimate", (Guid tenant, Guid universe, Guid id, IDraftFactory f, HttpContext c, CancellationToken ct) => f.EstimateAsync(Scope(tenant, c), universe, id, ct));
        g.MapPost("/batches", (Guid tenant, Guid universe, StartFactoryBatch r, IDraftFactory f, HttpContext c, CancellationToken ct) => f.StartAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/batches", (Guid tenant, Guid universe, IDraftFactory f, HttpContext c, CancellationToken ct) => f.BatchesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/batches/{id:guid}/control", async (Guid tenant, Guid universe, Guid id, FactoryControl r, IDraftFactory f, HttpContext c, CancellationToken ct) => { await f.ControlAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/artifacts", (Guid tenant, Guid universe, IDraftFactory f, HttpContext c, CancellationToken ct) => f.ArtifactsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/artifacts/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, FactoryReview r, IDraftFactory f, HttpContext c, CancellationToken ct) => { await f.ReviewArtifactAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/coverage", (Guid tenant, Guid universe, IDraftFactory f, HttpContext c, CancellationToken ct) => f.CoverageAsync(Scope(tenant, c), universe, ct));
        app.MapGet("/staff/factory", () => Results.Content(FactoryPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
