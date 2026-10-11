using ContentOS.Application;
using ContentOS.Contracts;

namespace GeetaStudio.Api;

public static class CorpusEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapCorpus(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/corpus/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapGet("/", (Guid tenant, Guid universe, ICanonicalCorpus corpus, HttpContext c, CancellationToken ct) => corpus.ListAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/manifests", (Guid tenant, Guid universe, RegisterCorpus r, ICanonicalCorpus corpus, HttpContext c, CancellationToken ct) => corpus.RegisterAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/manifests/{manifest:guid}/imports", (Guid tenant, Guid universe, Guid manifest, ImportCorpus r, ICanonicalCorpus corpus, HttpContext c, CancellationToken ct) => corpus.ImportAsync(Scope(tenant, c), universe, manifest, r, ct)).RequireAuthorization("StaffOperate");
        g.MapGet("/manifests/{manifest:guid}/report", (Guid tenant, Guid universe, Guid manifest, string? territory, ICanonicalCorpus corpus, HttpContext c, CancellationToken ct) => corpus.ReportAsync(Scope(tenant, c), universe, manifest, territory ?? "IN", ct));
        g.MapGet("/manifests/{manifest:guid}/chapters/{chapter:int}", (Guid tenant, Guid universe, Guid manifest, int chapter, string? territory, ICanonicalCorpus corpus, HttpContext c, CancellationToken ct) => corpus.ChapterAsync(Scope(tenant, c), universe, manifest, chapter, territory ?? "IN", ct));
        g.MapPost("/manifests/{manifest:guid}/verses/{verse:guid}/review", async (Guid tenant, Guid universe, Guid manifest, Guid verse, CorpusReview r, ICanonicalCorpus corpus, HttpContext c, CancellationToken ct) => { await corpus.ReviewAsync(Scope(tenant, c), universe, manifest, verse, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/manifests/{manifest:guid}/verses/{verse:guid}/annotations", (Guid tenant, Guid universe, Guid manifest, Guid verse, CorpusAnnotationInput r, ICanonicalCorpus corpus, HttpContext c, CancellationToken ct) => corpus.AnnotateAsync(Scope(tenant, c), universe, manifest, verse, r, ct)).RequireAuthorization("StaffOperate");
        g.MapPost("/manifests/{manifest:guid}/annotations/{annotation:guid}/review", async (Guid tenant, Guid universe, Guid manifest, Guid annotation, CorpusReview r, ICanonicalCorpus corpus, HttpContext c, CancellationToken ct) => { await corpus.ReviewAnnotationAsync(Scope(tenant, c), universe, manifest, annotation, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        app.MapGet("/staff/read", () => Results.Content(CorpusReaderPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
