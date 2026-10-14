using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class TalkEndpoints
{
    public static void MapTalk(this WebApplication app)
    {
        static CustomerScope Scope(Guid tenant, HttpContext c) => new(tenant, c.User);
        var g = app.MapGroup("/api/v1/customer/tenants/{tenant:guid}/talk").RequireAuthorization("Customer");
        g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        g.MapGet("/policies", (Guid tenant, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.PoliciesAsync(Scope(tenant, c), ct));
        g.MapPost("/sessions", (Guid tenant, TalkOpen r, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.OpenAsync(Scope(tenant, c), r, ct));
        g.MapGet("/sessions/{session:guid}", (Guid tenant, Guid session, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.InspectAsync(Scope(tenant, c), session, ct));
        g.MapGet("/sessions/{session:guid}/turns/{turn:guid}/sources/{text:guid}", async (Guid tenant, Guid session, Guid turn, Guid text, ITalkWorkbench w, HttpContext c, CancellationToken ct) => { var source = await w.SourceAsync(Scope(tenant, c), session, turn, text, ct); return Results.Text("Chapter " + source.Chapter + ", verse " + source.Verse + " · " + source.Label + "\n\n" + source.ExactText + "\n\n" + source.Locator + "\n" + source.Citation.Citation + "\n" + source.Citation.RequiredAttribution, "text/plain; charset=utf-8"); });
        g.MapPost("/sessions/{session:guid}/turns", (Guid tenant, Guid session, TalkAsk r, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.AskAsync(Scope(tenant, c), session, r, ct));
        g.MapPost("/sessions/{session:guid}/stream", async (Guid tenant, Guid session, TalkAsk r, ITalkWorkbench w, HttpContext c, CancellationToken ct) =>
        {
            // Never expose provider deltas before evidence/citation validation. Stream the validated answer.
            var answer = await w.AskAsync(Scope(tenant, c), session, r, ct); c.Response.ContentType = "application/x-ndjson";
            foreach (var part in (answer.Answer ?? "Answer content was not retained; inspect the original usage record.").Chunk(80)) { await c.Response.WriteAsync(JsonSerializer.Serialize(new { kind = "Delta", delta = new string(part) }) + "\n", ct); await c.Response.Body.FlushAsync(ct); }
            await c.Response.WriteAsync(JsonSerializer.Serialize(new { kind = "Completed", turn = answer }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "\n", ct);
        });
        g.MapPost("/sessions/{session:guid}/cancel", (Guid tenant, Guid session, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.CancelAsync(Scope(tenant, c), session, ct));
        g.MapPost("/sessions/{session:guid}/forget", (Guid tenant, Guid session, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.ForgetAsync(Scope(tenant, c), session, ct));
        g.MapPost("/sessions/{session:guid}/turns/{turn:guid}/reconcile", (Guid tenant, Guid session, Guid turn, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.ReconcileAsync(Scope(tenant, c), session, turn, ct));
        g.MapPost("/sessions/{session:guid}/turns/{turn:guid}/report", (Guid tenant, Guid session, Guid turn, TalkFeedback r, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.ReportAsync(Scope(tenant, c), session, turn, r, ct));
        var a = app.MapGroup("/api/v1/customer-admin/tenants/{tenant:guid}/talk").RequireAuthorization("StaffAdmin");
        a.MapGet("/policies", (Guid tenant, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.AdminPoliciesAsync(new(tenant, c.User.Identity!.Name!), ct));
        a.MapGet("/problems", (Guid tenant, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.ProblemsAsync(new(tenant, c.User.Identity!.Name!), ct));
        a.MapPost("/policies", (Guid tenant, TalkPolicyRequest r, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.ConfigureAsync(new(tenant, c.User.Identity!.Name!), r, ct));
        a.MapPost("/policies/{policy:guid}/enable", (Guid tenant, Guid policy, bool enabled, ITalkWorkbench w, HttpContext c, CancellationToken ct) => w.ControlAsync(new(tenant, c.User.Identity!.Name!), policy, enabled, ct));
        app.MapGet("/talk", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex"; c.Response.Headers["X-Content-Type-Options"] = "nosniff"; return Results.Content(TalkPage.Html, "text/html"); }).RequireAuthorization("Customer");
        app.MapGet("/staff/talk", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; return Results.Content(TalkAdminPage.Html, "text/html"); }).RequireAuthorization("StaffAdmin");
    }
}

