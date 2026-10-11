using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class LessonEndpoints
{
    private static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? "");
    public static void MapLessons(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/lessons/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        g.MapPost("/proposals", (Guid tenant, Guid universe, ILessonEngine a, HttpContext c, CancellationToken ct) => a.ProposeDefaultsAsync(Scope(tenant, c), universe, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/profiles", (Guid tenant, Guid universe, ILessonEngine a, HttpContext c, CancellationToken ct) => a.ProfilesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/profiles", (Guid tenant, Guid universe, RegisterLessonProfile r, ILessonEngine a, HttpContext c, CancellationToken ct) => a.RegisterProfileAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/profiles/{id:guid}/review", (Guid tenant, Guid universe, Guid id, LessonReview r, ILessonEngine a, HttpContext c, CancellationToken ct) => a.ReviewProfileAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/bundles", (Guid tenant, Guid universe, ILessonEngine a, HttpContext c, CancellationToken ct) => a.BundlesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/bundles", (Guid tenant, Guid universe, RegisterLessonBundle r, ILessonEngine a, HttpContext c, CancellationToken ct) => a.RegisterBundleAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/memory", (Guid tenant, Guid universe, ILessonEngine a, HttpContext c, CancellationToken ct) => a.MemoryAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/memory", (Guid tenant, Guid universe, RegisterLessonMemory r, ILessonEngine a, HttpContext c, CancellationToken ct) => a.RegisterMemoryAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/memory/{id:guid}/review", (Guid tenant, Guid universe, Guid id, LessonReview r, ILessonEngine a, HttpContext c, CancellationToken ct) => a.ReviewMemoryAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/variants", (Guid tenant, Guid universe, ILessonEngine a, HttpContext c, CancellationToken ct) => a.VariantsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/variants", (Guid tenant, Guid universe, AdaptLesson r, ILessonEngine a, HttpContext c, CancellationToken ct) => a.AdaptAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/variants/{id:guid}", (Guid tenant, Guid universe, Guid id, ILessonEngine a, HttpContext c, CancellationToken ct) => a.InspectAsync(Scope(tenant, c), universe, id, ct));
        g.MapPost("/variants/{id:guid}/review", (Guid tenant, Guid universe, Guid id, LessonReview r, ILessonEngine a, HttpContext c, CancellationToken ct) => a.ReviewVariantAsync(Scope(tenant, c), universe, id, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/variants/{id:guid}/export", (Guid tenant, Guid universe, Guid id, ILessonEngine a, HttpContext c, CancellationToken ct) => a.ExportAsync(Scope(tenant, c), universe, id, ct));
        g.MapGet("/writing/{id:guid}/candidates", (Guid tenant, Guid universe, Guid id, ILessonEngine a, HttpContext c, CancellationToken ct) => a.CandidatesAsync(Scope(tenant, c), universe, id, ct));
        g.MapGet("/characters", (Guid tenant, Guid universe, ILessonEngine a, HttpContext c, CancellationToken ct) => a.CharactersAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/characters", (Guid tenant, Guid universe, RegisterLessonCharacter r, ILessonEngine a, HttpContext c, CancellationToken ct) => a.RegisterCharacterAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        app.MapGet("/staff/lessons", () => Results.Content(LessonPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
