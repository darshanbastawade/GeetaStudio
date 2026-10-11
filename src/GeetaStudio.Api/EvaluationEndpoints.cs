using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class EvaluationEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapEvaluation(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/evaluation/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/datasets", (Guid tenant, Guid universe, IQualityEvaluation e, HttpContext c, CancellationToken ct) => e.DatasetsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/datasets", (Guid tenant, Guid universe, RegisterEvaluationDataset r, IQualityEvaluation e, HttpContext c, CancellationToken ct) => e.RegisterDatasetAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/candidates", (Guid tenant, Guid universe, IQualityEvaluation e, HttpContext c, CancellationToken ct) => e.CandidatesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/candidates", (Guid tenant, Guid universe, RegisterEvaluationCandidate r, IQualityEvaluation e, HttpContext c, CancellationToken ct) => e.RegisterCandidateAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/reports", (Guid tenant, Guid universe, IQualityEvaluation e, HttpContext c, CancellationToken ct) => e.ReportsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/runs", (Guid tenant, Guid universe, RunEvaluation r, IQualityEvaluation e, HttpContext c, CancellationToken ct) => e.RunAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/reviews", async (Guid tenant, Guid universe, EvaluationHumanReview r, IQualityEvaluation e, HttpContext c, CancellationToken ct) => { await e.ReviewAsync(Scope(tenant, c), universe, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/advisories", async (Guid tenant, Guid universe, EvaluationAdvisory r, IQualityEvaluation e, HttpContext c, CancellationToken ct) => { await e.AdviseAsync(Scope(tenant, c), universe, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        app.MapGet("/staff/evaluation", () => Results.Content(EvaluationPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
