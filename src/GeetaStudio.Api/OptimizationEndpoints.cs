using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Mvc;
namespace GeetaStudio.Api;

public static class OptimizationEndpoints
{
    public static void MapOptimization(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/optimization/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        static CoreScope Scope(Guid t, HttpContext c) => new(t, c.User.Identity?.Name ?? throw new InvalidOperationException());
        g.MapGet("/experiments", (Guid tenant, Guid universe, [FromServices] IOptimizationWorkbench w, HttpContext c, CancellationToken ct) => w.ListAsync(Scope(tenant, c), universe, ct));
        g.MapGet("/report", (Guid tenant, Guid universe, [FromServices] IOptimizationWorkbench w, HttpContext c, CancellationToken ct) => w.ReportAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/experiments", (Guid tenant, Guid universe, OptimizationProposal r, [FromServices] IOptimizationWorkbench w, HttpContext c, CancellationToken ct) => w.ProposeAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/experiments/{id:guid}/evaluation", (Guid tenant, Guid universe, Guid id, OptimizationEvaluation r, [FromServices] IOptimizationWorkbench w, HttpContext c, CancellationToken ct) => w.EvaluateAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/experiments/{id:guid}/measurements", (Guid tenant, Guid universe, Guid id, OptimizationMeasurement r, [FromServices] IOptimizationWorkbench w, HttpContext c, CancellationToken ct) => w.MeasureAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/experiments/{id:guid}/control", (Guid tenant, Guid universe, Guid id, OptimizationControl r, [FromServices] IOptimizationWorkbench w, HttpContext c, CancellationToken ct) => w.ControlAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/experiments/{id:guid}/generate", (Guid tenant, Guid universe, Guid id, OptimizationGenerate r, [FromServices] IOptimizationWorkbench w, HttpContext c, CancellationToken ct) => w.GenerateAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/automation", (Guid tenant, Guid universe, OptimizationAutomation r, [FromServices] IOptimizationWorkbench w, HttpContext c, CancellationToken ct) => w.AutomateAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        app.MapGet("/staff/optimization", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(OptimizationPage.Html, "text/html"); }).RequireAuthorization("StaffRead");
    }
}
