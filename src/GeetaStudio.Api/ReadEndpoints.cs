using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;
using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class ReadEndpoints
{
    private static readonly Meter Meter = new("ContentOS.Read", "1.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("read.requests");
    private static void Headers(HttpContext c) { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Content-Type-Options"] = "nosniff"; }
    public static IServiceCollection AddReadLimits(this IServiceCollection s)
    {
        s.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("read", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        return s;
    }
    public static void MapRead(this WebApplication app)
    {
        var publicRead = app.MapGroup("/api/v1/read").AllowAnonymous().RequireRateLimiting("read");
        publicRead.AddEndpointFilter(async (c, next) =>
        {
            Headers(c.HttpContext); var result = await next(c); Requests.Add(1, new KeyValuePair<string, object?>("version", 1)); return result;
        });
        publicRead.MapGet("", async (IPublicRead r, HttpContext context, CancellationToken ct) =>
        {
            var rows = await r.DirectoryAsync(ct);
            if (context.Items[typeof(ReleaseDecision)] is ReleaseDecision decision)
            {
                var ids = decision.Features.Single(x => x.Key == "READ").Targets.Select(x => x.Target.ResourceId).ToArray();
                rows = rows.Where(x => ids.Contains(x.Id)).ToArray();
            }
            return rows;
        });
        publicRead.MapGet("/{key}", async (string key, IPublicRead r, HttpContext context, CancellationToken ct) =>
        {
            var book = await r.GetAsync(key, ct);
            if (book != null && context.Items[typeof(ReleaseDecision)] is ReleaseDecision decision && !decision.Features.Single(x => x.Key == "READ").Targets.Any(x => x.Target.ResourceId == book.Collection.Id)) return Results.NotFound();
            return book == null ? Results.NotFound() : Results.Ok(book);
        });
        publicRead.MapPost("/{key}/events", async (string key, ReadEngagement request, IPublicRead r, HttpContext c, CancellationToken ct) =>
        {
            if (c.Request.Headers["X-Read-Event-Version"] != "1") return Results.BadRequest();
            await r.EventAsync(key, request, ct); return Results.NoContent();
        });
        var staff = app.MapGroup("/api/v1/read-admin/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        static CoreScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User.Identity?.Name ?? throw new InvalidOperationException());
        staff.MapPost("/collections", (Guid tenant, Guid universe, RegisterReadCollection r, IReadWorkbench w, HttpContext c, CancellationToken ct) => w.RegisterAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        staff.MapPost("/pieces", (Guid tenant, Guid universe, AddReadPiece r, IReadWorkbench w, HttpContext c, CancellationToken ct) => w.AddAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        staff.MapGet("/collections/{id:guid}", (Guid tenant, Guid universe, Guid id, IReadWorkbench w, HttpContext c, CancellationToken ct) => w.InspectAsync(Scope(tenant, c), universe, id, ct));
        staff.MapPost("/collections/{id:guid}/evidence", async (Guid tenant, Guid universe, Guid id, ReadGateEvidence r, IReadWorkbench w, HttpContext c, CancellationToken ct) => { await w.EvidenceAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        staff.MapPost("/collections/{id:guid}/control", async (Guid tenant, Guid universe, Guid id, ControlReadCollection r, IReadWorkbench w, HttpContext c, CancellationToken ct) => { await w.ControlAsync(Scope(tenant, c), universe, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        app.MapGet("/api/v1/read-preview", async (IReadWorkbench w, HttpContext c, CancellationToken ct) =>
        {
            Headers(c); c.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            return await w.PrivateDirectoryAsync(c.User.Identity!.Name!, ct);
        })
            .RequireAuthorization("StaffRead").RequireRateLimiting("read");
        app.MapGet("/api/v1/read-preview/{id:guid}", async (Guid id, IReadWorkbench w, HttpContext c, CancellationToken ct) =>
        {
            Headers(c); c.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            var book = await w.PreviewAsync(c.User.Identity!.Name!, id, ct); return book == null ? Results.NotFound() : Results.Ok(book);
        }).RequireAuthorization("StaffRead").RequireRateLimiting("read");
        app.MapGet("/staff/read-collections", () => Results.Content(ReadAdminPage.Html, "text/html")).RequireAuthorization("StaffRead");
    }
}
