using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class WorkflowEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapWorkflows(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/workflows/tenants/{tenant:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/tools", () => new WorkflowTool[] {
            new("evidence.retrieve.v1", "workflow-inputs.v1", "workflow-evidence.v1", "Administrator+owner", "ReadWithRightsAudit"),
            new("ai.generate.v1", "generate-ai.v1", "generated-text.v1", "Administrator+owner", "BudgetedGeneration")
        });
        g.MapGet("/universes/{universe:guid}/prompts", (Guid tenant, Guid universe, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.PromptsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/universes/{universe:guid}/prompts", (Guid tenant, Guid universe, RegisterPrompt r, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.RegisterPromptAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/universes/{universe:guid}/definitions", (Guid tenant, Guid universe, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.DefinitionsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/universes/{universe:guid}/definitions", (Guid tenant, Guid universe, RegisterWorkflow r, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.RegisterDefinitionAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/universes/{universe:guid}/runs", (Guid tenant, Guid universe, StartWorkflow r, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.StartAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/runs", (Guid tenant, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.RunsAsync(Scope(tenant, c), ct));
        g.MapGet("/runs/{id:guid}", (Guid tenant, Guid id, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.InspectAsync(Scope(tenant, c), id, ct));
        g.MapGet("/runs/{id:guid}/checkpoint", (Guid tenant, Guid id, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.ExportCheckpointAsync(Scope(tenant, c), id, ct));
        foreach (var action in new[] { "pause", "resume", "cancel", "review" })
        {
            var captured = action;
            g.MapPost("/runs/{id:guid}/" + captured, (Guid tenant, Guid id, ControlWorkflow r, IWorkflowRuntime a, HttpContext c, CancellationToken ct) => a.ControlAsync(Scope(tenant, c), id, captured, r, ct)).RequireAuthorization("StaffAdmin");
        }
        app.MapGet("/staff/workflows", () => Results.Content(WorkflowPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
