using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class EvidenceEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapEvidence(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/evidence/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/", (Guid tenant, Guid universe, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.ListAsync(Scope(tenant, c), universe, ct));
        g.MapGet("/claims/{claim:guid}", (Guid tenant, Guid universe, Guid claim, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.InspectAsync(Scope(tenant, c), universe, claim, ct));
        g.MapGet("/rubrics", (Guid tenant, Guid universe, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.RubricsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/rubrics", (Guid tenant, Guid universe, RegisterEvidenceRubric r, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.RegisterRubricAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/claims", (Guid tenant, Guid universe, SubmitClaim r, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.SubmitAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/revisions/{revision:guid}/review", async (Guid tenant, Guid universe, Guid revision, ReviewClaim r, IEvidenceEngine e, HttpContext c, CancellationToken ct) => { await e.ReviewAsync(Scope(tenant, c), universe, revision, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapGet("/disagreements", (Guid tenant, Guid universe, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.DisagreementsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/disagreements", (Guid tenant, Guid universe, RegisterDisagreement r, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.DisagreeAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/links", (Guid tenant, Guid universe, LinkClaim r, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.LinkAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/corrections", (Guid tenant, Guid universe, RequestClaimCorrection r, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.CorrectAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffOperate");
        g.MapGet("/revisions/{revision:guid}/impact", (Guid tenant, Guid universe, Guid revision, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.ImpactAsync(Scope(tenant, c), universe, revision, ct));
        g.MapPost("/content/{revision:guid}/evaluate", (Guid tenant, Guid universe, Guid revision, IntendedUse r, IEvidenceEngine e, HttpContext c, CancellationToken ct) => e.CheckAsync(Scope(tenant, c), universe, revision, r, ct));
        app.MapGet("/staff/evidence", () => Results.Content(EvidenceReviewPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
