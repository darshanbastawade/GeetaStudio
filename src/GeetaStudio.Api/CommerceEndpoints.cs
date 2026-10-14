using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class CommerceEndpoints
{
    public static void MapCommerce(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/customer/tenants/{tenant:guid}/shop").RequireAuthorization("Customer"); g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        static CustomerScope S(Guid t, HttpContext c) => new(t, c.User);
        g.MapGet("/products", (Guid tenant, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.ProductsAsync(S(tenant, c), ct));
        g.MapPost("/products", (Guid tenant, CommerceProductRequest r, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.ProductAsync(S(tenant, c), r, ct)).RequireAuthorization("StaffAdmin");
        g.MapGet("/orders", (Guid tenant, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.OrdersAsync(S(tenant, c), ct));
        g.MapPost("/orders", (Guid tenant, CommercePurchase r, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.PurchaseAsync(S(tenant, c), r, ct));
        g.MapPost("/orders/{id:guid}/reconcile", (Guid tenant, Guid id, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.ReconcileAsync(S(tenant, c), id, ct));
        g.MapPost("/orders/{id:guid}/refund", (Guid tenant, Guid id, CustomerPaymentAction r, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.RefundAsync(S(tenant, c), id, r, ct));
        g.MapGet("/orders/{id:guid}/download", async (Guid tenant, Guid id, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => { var pack = await w.DownloadAsync(S(tenant, c), id, ct); return Results.File(System.Text.Encoding.UTF8.GetBytes(pack.Html), "text/html", "licensed-pack-v" + pack.Version + ".html"); });
        g.MapGet("/institutions", (Guid tenant, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.InstitutionsAsync(S(tenant, c), ct));
        g.MapPost("/institutions", (Guid tenant, CommerceInstitutionRequest r, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.InstitutionAsync(S(tenant, c), r, ct));
        g.MapPost("/institutions/{id:guid}/seats", (Guid tenant, Guid id, CommerceSeatRequest r, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.SeatAsync(S(tenant, c), id, r, ct));
        g.MapPost("/institutions/{id:guid}/activity", (Guid tenant, Guid id, CommerceActivityRequest r, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.ActivityAsync(S(tenant, c), id, r, ct));
        g.MapGet("/institutions/{id:guid}/cohorts", (Guid tenant, Guid id, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.CohortsAsync(S(tenant, c), id, ct));
        g.MapGet("/institutions/{id:guid}/products/{product:guid}/download", async (Guid tenant, Guid id, Guid product, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => { var pack = await w.InstitutionDownloadAsync(S(tenant, c), id, product, ct); return Results.File(System.Text.Encoding.UTF8.GetBytes(pack.Html), "text/html", "institution-pack-v" + pack.Version + ".html"); });
        g.MapGet("/offers", (Guid tenant, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.OffersAsync(S(tenant, c), ct));
        app.MapPost("/api/v1/customer-admin/tenants/{tenant:guid}/shop/routes", (Guid tenant, CommerceRouteRequest r, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.RouteAsync(new(tenant, c.User.Identity!.Name!), r, ct)).RequireAuthorization("StaffAdmin");
        app.MapPost("/api/v1/customer-admin/tenants/{tenant:guid}/shop/offers", (Guid tenant, CommerceOfferRequest r, ICommerceWorkbench w, HttpContext c, CancellationToken ct) => w.OfferAsync(new(tenant, c.User.Identity!.Name!), r, ct)).RequireAuthorization("StaffAdmin");
        app.MapPost("/api/v1/customer-admin/tenants/{tenant:guid}/shop/capture-fulfillment", (Guid tenant, Guid universe, AnalyticsCapture r, IAnalyticsWorkbench w, HttpContext c, CancellationToken ct) => w.CaptureCommerceAsync(new(tenant, c.User.Identity!.Name!), universe, r, ct)).RequireAuthorization("StaffAdmin");
        app.MapGet("/shop", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(CommercePage.Html, "text/html"); }).RequireAuthorization("Customer");
    }
}
public sealed class CommerceProcessor(IServiceScopeFactory scopes, ILogger<CommerceProcessor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct) { while (!ct.IsCancellationRequested) { try { using var scope = scopes.CreateScope(); if (await scope.ServiceProvider.GetRequiredService<ICommerceWorkbench>().ProcessNextAsync(ct)) continue; } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; } catch (Exception e) { log.LogWarning("Commerce processor unavailable: {ExceptionType}", e.GetType().Name); } await Task.Delay(1000, ct); } }
}
