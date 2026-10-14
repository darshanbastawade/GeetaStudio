using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.AspNetCore.Mvc;
namespace GeetaStudio.Api;

public static class CustomerEndpoints
{
    public static void MapCustomers(this WebApplication app)
    {
        static CustomerScope Scope(Guid t, HttpContext c) => new(t, c.User);
        static CoreScope Staff(Guid t, HttpContext c) => new(t, c.User.Identity!.Name!);
        var g = app.MapGroup("/api/v1/customer/tenants/{tenant:guid}").RequireAuthorization("Customer");
        g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        g.MapPost("/register", (Guid tenant, CustomerRegistration r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.RegisterAsync(Scope(tenant, c), r, ct));
        g.MapGet("/me", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.MeAsync(Scope(tenant, c), ct));
        g.MapPost("/identity/challenges", (Guid tenant, CustomerChallengeRequest r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ChallengeAsync(Scope(tenant, c), r, ct));
        g.MapPost("/identity/link", (Guid tenant, CustomerProof r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.LinkAsync(Scope(tenant, c), r, ct));
        g.MapPost("/identity/recover", (Guid tenant, CustomerProof r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.RecoverAsync(Scope(tenant, c), r, ct));
        g.MapGet("/households", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.HouseholdsAsync(Scope(tenant, c), ct));
        g.MapPost("/households", (Guid tenant, CustomerHouseholdRequest r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.HouseholdAsync(Scope(tenant, c), r, ct));
        g.MapPost("/households/{id:guid}/invite", (Guid tenant, Guid id, CustomerInviteRequest r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.InviteAsync(Scope(tenant, c), id, r, ct));
        g.MapPost("/households/{id:guid}/transfer", (Guid tenant, Guid id, CustomerTransfer r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.TransferAsync(Scope(tenant, c), id, r, ct));
        g.MapPost("/invites/{id:guid}/accept", (Guid tenant, Guid id, CustomerOperation r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.AcceptAsync(Scope(tenant, c), id, r, ct));
        g.MapGet("/profiles", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ProfilesAsync(Scope(tenant, c), ct));
        g.MapPost("/profiles", (Guid tenant, CustomerProfileRequest r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ProfileAsync(Scope(tenant, c), r, ct));
        g.MapPost("/profiles/{id:guid}/edit", (Guid tenant, Guid id, CustomerProfileEdit r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.EditProfileAsync(Scope(tenant, c), id, r, ct));
        g.MapPost("/profiles/{id:guid}/delete", (Guid tenant, Guid id, CustomerOperation r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.DeleteProfileAsync(Scope(tenant, c), id, r, ct));
        g.MapPost("/consents", (Guid tenant, CustomerConsentRequest r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ConsentAsync(Scope(tenant, c), r, ct));
        g.MapGet("/profiles/{id:guid}/progress", (Guid tenant, Guid id, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ProgressAsync(Scope(tenant, c), id, ct));
        g.MapPost("/profiles/{id:guid}/progress", (Guid tenant, Guid id, CustomerProgressRequest r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ProgressAsync(Scope(tenant, c), id, r, ct));
        g.MapGet("/export", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ExportAsync(Scope(tenant, c), ct));
        g.MapPost("/delete", (Guid tenant, CustomerOperation r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.DeleteAsync(Scope(tenant, c), r, ct));
        g.MapGet("/products", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ProductsAsync(Scope(tenant, c), ct));
        g.MapGet("/orders", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.OrdersAsync(Scope(tenant, c), ct));
        g.MapPost("/checkout", (Guid tenant, CustomerCheckout r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.CheckoutAsync(Scope(tenant, c), r, ct));
        g.MapPost("/orders/{id:guid}/submit", (Guid tenant, Guid id, CustomerOperation r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.SubmitAsync(Scope(tenant, c), id, ct));
        g.MapPost("/orders/{id:guid}/reconcile", (Guid tenant, Guid id, CustomerOperation r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ReconcileAsync(Scope(tenant, c), id, ct));
        g.MapPost("/orders/{id:guid}/change", (Guid tenant, Guid id, CustomerPaymentAction r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ChangePaymentAsync(Scope(tenant, c), id, r, ct));
        g.MapGet("/entitlements", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.EntitlementsAsync(Scope(tenant, c), ct));
        g.MapGet("/wallet", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.WalletAsync(Scope(tenant, c), ct));
        g.MapGet("/reservations", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ReservationsAsync(Scope(tenant, c), ct));
        g.MapPost("/reservations", (Guid tenant, CustomerReserve r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ReserveAsync(Scope(tenant, c), r, ct));
        g.MapPost("/reservations/{id:guid}/cancel", (Guid tenant, Guid id, CustomerOperation r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.CancelReservationAsync(Scope(tenant, c), id, r, ct));
        g.MapGet("/notifications", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.NotificationsAsync(Scope(tenant, c), ct));
        g.MapPost("/notifications/{id:guid}/deliver", (Guid tenant, Guid id, CustomerOperation r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.DeliverNoticeAsync(Scope(tenant, c), id, r, ct));
        var a = app.MapGroup("/api/v1/customer-admin/tenants/{tenant:guid}").RequireAuthorization("StaffAdmin");
        a.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        a.MapGet("/providers", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ProvidersAsync(Staff(tenant, c), ct));
        a.MapGet("/routes", (Guid tenant, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.RoutesAsync(Staff(tenant, c), ct));
        a.MapPost("/routes", (Guid tenant, CustomerRouteRequest r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.RouteAsync(Staff(tenant, c), r, ct));
        a.MapPost("/routes/{id:guid}/control", (Guid tenant, Guid id, CustomerRouteControl r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ControlRouteAsync(Staff(tenant, c), id, r, ct));
        a.MapPost("/products", (Guid tenant, CustomerProductRequest r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.ProductAsync(Staff(tenant, c), r, ct));
        a.MapPost("/grants", (Guid tenant, CustomerGrant r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.GrantAsync(Staff(tenant, c), r, ct));
        a.MapPost("/reservations/{id:guid}/dispatch", (Guid tenant, Guid id, CustomerDispatch r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.DispatchAsync(Staff(tenant, c), id, r, ct));
        a.MapPost("/reservations/{id:guid}/settle", (Guid tenant, Guid id, CustomerSettlement r, ICustomerWorkbench w, HttpContext c, CancellationToken ct) => w.SettleAsync(Staff(tenant, c), id, r, ct));
        // Fixture ingress only. No unauthenticated/live gateway has been qualified.
        if (app.Environment.IsDevelopment()) a.MapPost("/payment-events", (Guid tenant, CustomerSignedWebhook r, ICustomerWorkbench w, CancellationToken ct) => w.WebhookAsync(tenant, r, ct));
        app.MapGet("/account", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(CustomerPage.Html, "text/html"); }).RequireAuthorization("Customer");
        app.MapGet("/staff/customers", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(CustomerPage.AdminHtml, "text/html"); }).RequireAuthorization("StaffAdmin");
    }
}
