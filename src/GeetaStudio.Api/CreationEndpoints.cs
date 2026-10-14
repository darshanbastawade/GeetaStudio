using ContentOS.Application;
using ContentOS.Contracts;
namespace GeetaStudio.Api;

public static class CreationEndpoints
{
    public static void MapCreations(this WebApplication app)
    {
        app.MapGet("/api/v1/customer/create/workspaces", async (ICreationStudio w, HttpContext c, CancellationToken ct) => { c.Response.Headers.CacheControl = "no-store"; return await w.WorkspacesAsync(c.User, ct); }).RequireAuthorization("Customer");
        var g = app.MapGroup("/api/v1/customer/tenants/{tenant:guid}/create").RequireAuthorization("Customer");
        g.AddEndpointFilter(async (c, next) => { c.HttpContext.Response.Headers.CacheControl = "no-store"; c.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex"; return await next(c); });
        static CustomerScope S(Guid t, HttpContext c) => new(t, c.User);
        g.MapGet("/templates", (Guid tenant, ICreationStudio w, HttpContext c, CancellationToken ct) => w.TemplatesAsync(S(tenant, c), ct));
        g.MapGet("/creations", (Guid tenant, ICreationStudio w, HttpContext c, CancellationToken ct) => w.ListAsync(S(tenant, c), ct));
        g.MapGet("/notices", (Guid tenant, ICreationStudio w, HttpContext c, CancellationToken ct) => w.NoticesAsync(S(tenant, c), ct));
        g.MapPost("/uploads", (Guid tenant, CreationUploadRequest r, ICreationStudio w, HttpContext c, CancellationToken ct) => w.UploadAsync(S(tenant, c), r, ct));
        g.MapPost("/uploads/{id:guid}/delete", (Guid tenant, Guid id, ICreationStudio w, HttpContext c, CancellationToken ct) => w.DeleteUploadAsync(S(tenant, c), id, ct));
        g.MapPost("/creations", (Guid tenant, CreationSubmit r, ICreationStudio w, HttpContext c, CancellationToken ct) => w.SubmitAsync(S(tenant, c), r, ct));
        g.MapGet("/creations/{id:guid}", (Guid tenant, Guid id, ICreationStudio w, HttpContext c, CancellationToken ct) => w.InspectAsync(S(tenant, c), id, ct));
        g.MapPost("/creations/{id:guid}/edit", (Guid tenant, Guid id, CreationEdit r, ICreationStudio w, HttpContext c, CancellationToken ct) => w.EditAsync(S(tenant, c), id, r, ct));
        g.MapPost("/creations/{id:guid}/review", (Guid tenant, Guid id, CreationReview r, ICreationStudio w, HttpContext c, CancellationToken ct) => w.ReviewAsync(S(tenant, c), id, r, ct));
        g.MapPost("/creations/{id:guid}/cancel", (Guid tenant, Guid id, ICreationStudio w, HttpContext c, CancellationToken ct) => w.CancelAsync(S(tenant, c), id, ct));
        g.MapPost("/creations/{id:guid}/delete", (Guid tenant, Guid id, ICreationStudio w, HttpContext c, CancellationToken ct) => w.DeleteAsync(S(tenant, c), id, ct));
        g.MapGet("/creations/{id:guid}/export", (Guid tenant, Guid id, ICreationStudio w, HttpContext c, CancellationToken ct) => w.ExportAsync(S(tenant, c), id, ct));
        g.MapGet("/creations/{id:guid}/download", async (Guid tenant, Guid id, ICreationStudio w, HttpContext c, CancellationToken ct) =>
        {
            var pack = await w.ExportAsync(S(tenant, c), id, ct); using var stream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                void Add(string name, byte[] bytes) { using var file = archive.CreateEntry(name).Open(); file.Write(bytes); }
                Add("learning-pack.html", System.Text.Encoding.UTF8.GetBytes(pack.Html)); Add("credits-and-lineage.json", System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(pack with { Html = "", Videos = [] }));
                foreach (var video in pack.Videos ?? []) { Add("approved-master.mp4", video.Content); Add("captions.vtt", System.Text.Encoding.UTF8.GetBytes(video.Vtt)); Add("captions.srt", System.Text.Encoding.UTF8.GetBytes(video.Srt)); }
            }
            return Results.File(stream.ToArray(), "application/zip", "private-pack-v" + pack.Version + ".zip");
        });
        app.MapPost("/api/v1/customer-admin/tenants/{tenant:guid}/create/templates", (Guid tenant, CreationTemplateRequest r, ICreationStudio w, HttpContext c, CancellationToken ct) => w.TemplateAsync(new(tenant, c.User.Identity!.Name!), r, ct)).RequireAuthorization("StaffAdmin");
        app.MapPost("/api/v1/customer-admin/tenants/{tenant:guid}/create/templates/{id:guid}/enable", (Guid tenant, Guid id, bool enabled, ICreationStudio w, HttpContext c, CancellationToken ct) => w.ControlTemplateAsync(new(tenant, c.User.Identity!.Name!), id, enabled, ct)).RequireAuthorization("StaffAdmin");
        app.MapGet("/create", (HttpContext c) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex"; return Results.Content(CreationPage.Html, "text/html"); }).RequireAuthorization("Customer");
    }
}
public sealed class CreationProcessor(IServiceScopeFactory scopes, ILogger<CreationProcessor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct) { while (!ct.IsCancellationRequested) { try { using var scope = scopes.CreateScope(); if (await scope.ServiceProvider.GetRequiredService<ICreationStudio>().ProcessNextAsync(ct)) continue; } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; } catch (Exception e) { log.LogWarning("Creation queue unavailable: {ExceptionType}", e.GetType().Name); } await Task.Delay(1000, ct); } }
}
