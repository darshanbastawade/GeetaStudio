using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Mvc;
namespace GeetaStudio.Api;

public static class AnalyticsEndpoints
{
    public static void MapAnalytics(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/analytics/tenants/{tenant:guid}/universes/{universe:guid}").RequireAuthorization("StaffRead");
        static CoreScope Scope(Guid t, HttpContext c) => new(t, c.User.Identity?.Name ?? throw new InvalidOperationException());
        g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        g.MapGet("/providers", (Guid tenant, Guid universe, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.ProvidersAsync(Scope(tenant, c), universe, ct));
        g.MapGet("/sources", (Guid tenant, Guid universe, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.SourcesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/sources", (Guid tenant, Guid universe, AnalyticsSourceRequest r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.SourceAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/definitions", (Guid tenant, Guid universe, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.DefinitionsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/definitions", (Guid tenant, Guid universe, AnalyticsDefinitionRequest r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.DefinitionAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/rates", (Guid tenant, Guid universe, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.RatesAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/rates", (Guid tenant, Guid universe, AnalyticsFxRequest r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.FxAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/consents", (Guid tenant, Guid universe, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.ConsentsAsync(Scope(tenant, c), universe, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/consents", (Guid tenant, Guid universe, AnalyticsConsentRequest r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.ConsentAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/{kind}/{id:guid}/control", async (Guid tenant, Guid universe, string kind, Guid id, AnalyticsControl r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => { await w.ControlAsync(Scope(tenant, c), universe, kind, id, r, ct); return Results.NoContent(); }).RequireAuthorization("StaffAdmin");
        g.MapPost("/imports", (Guid tenant, Guid universe, AnalyticsImport r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.ImportAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/capture-customer-payments", (Guid tenant, Guid universe, AnalyticsCapture r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.CaptureCustomerPaymentsAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapPost("/capture", (Guid tenant, Guid universe, AnalyticsCapture r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.CaptureAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/entries", (Guid tenant, Guid universe, int? skip, int? take, DateTimeOffset? asOfUtc, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.EntriesAsync(Scope(tenant, c), universe, ct, skip ?? 0, take ?? 100, asOfUtc)).RequireAuthorization("StaffAdmin");
        g.MapGet("/report", (Guid tenant, Guid universe, string currency, DateTimeOffset startsUtc, DateTimeOffset endsUtc, DateTimeOffset? asOfUtc, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.ReportAsync(Scope(tenant, c), universe, new(currency, startsUtc, endsUtc, asOfUtc), ct));
        g.MapGet("/forecasts", (Guid tenant, Guid universe, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.ForecastsAsync(Scope(tenant, c), universe, ct));
        g.MapPost("/forecasts", (Guid tenant, Guid universe, AnalyticsForecastRequest r, [FromServices] IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.ForecastAsync(Scope(tenant, c), universe, r, ct)).RequireAuthorization("StaffAdmin");
        app.MapGet("/staff/analytics", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(AnalyticsPage.Html, "text/html"); }).RequireAuthorization("StaffRead");
    }
}
