using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class KnowledgeEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapKnowledge(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/knowledge/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/graph", (Guid tenant, Guid universe, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => k.GraphAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/concepts", (Guid tenant, Guid universe, RegisterConcept r, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => k.ConceptAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/relations", (Guid tenant, Guid universe, RegisterConceptRelation r, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => k.RelationAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/graph/{id:guid}/review", async (Guid tenant, Guid universe, Guid id, ReviewKnowledge r, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => { await k.ReviewAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/indices", (Guid tenant, Guid universe, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => k.IndicesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/indices", (Guid tenant, Guid universe, QueueKnowledgeIndex r, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => k.QueueAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/indices/{id:guid}/validate", async (Guid tenant, Guid universe, Guid id, ValidateKnowledgeIndex r, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => { await k.ValidateAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/aliases", (Guid tenant, Guid universe, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => k.AliasesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/aliases", (Guid tenant, Guid universe, ActivateKnowledgeIndex r, IKnowledgeAdministration k, HttpContext c, CancellationToken ct) => k.ActivateAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/search", (Guid tenant, Guid universe, KnowledgeQuery r, IKnowledgeRetriever k, HttpContext c, CancellationToken ct) => k.RetrieveAsync(Scope(tenant, c), universe, r, ct));
        app.MapGet("/staff/knowledge", () => Results.Content(KnowledgePage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
