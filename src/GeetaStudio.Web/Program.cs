using System.Threading.RateLimiting;
using System.Xml.Linq;
using ContentOS.Contracts;
using GeetaStudio.Web;
using GeetaStudio.Web.Components;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var apiAddress = builder.Configuration["Api:BaseAddress"];
if (!Uri.TryCreate(apiAddress, UriKind.Absolute, out var apiUri)
    || (apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps)
    || (!builder.Environment.IsDevelopment() && apiUri.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException("Api:BaseAddress must be an absolute HTTP(S) URL; HTTPS is required outside Development.");
}
builder.Services.AddHttpClient<ApiHealthClient>(client =>
{
    client.BaseAddress = apiUri;
    client.Timeout = TimeSpan.FromSeconds(3);
});

builder.Services.AddHttpClient("read-public", c => { c.BaseAddress = apiUri; c.Timeout = TimeSpan.FromSeconds(20); });
var previewOwner = builder.Configuration["Read:PreviewOwner"] ?? "";
var previewAllowed = builder.Environment.IsDevelopment() && apiUri.IsLoopback && OperatingSystem.IsWindows()
    && System.Security.Principal.WindowsIdentity.GetCurrent().Name.Equals(previewOwner, StringComparison.OrdinalIgnoreCase);
builder.Services.AddHttpClient("read-preview", c => { c.BaseAddress = apiUri; c.Timeout = TimeSpan.FromSeconds(20); })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseDefaultCredentials = previewAllowed, AllowAutoRedirect = false });
builder.Services.AddScoped<ReadClient>();
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization(o => o.AddPolicy("ReaderPreview", p => p.RequireAuthenticatedUser().RequireAssertion(c =>
    previewAllowed && c.User.Identity?.Name?.Equals(previewOwner, StringComparison.OrdinalIgnoreCase) == true)));
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("reader", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// NTLM handshake challenges must not be replayed through the error page.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/preview") && context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>() is { } pages)
        pages.Enabled = false;
    await next(context);
});
app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.Use(async (c, next) =>
{
    if (c.Request.Path == "/development" && !app.Environment.IsDevelopment()) { c.Response.StatusCode = 404; return; }
    if (c.Request.Path.StartsWithSegments("/preview") && (c.Connection.RemoteIpAddress == null || !System.Net.IPAddress.IsLoopback(c.Connection.RemoteIpAddress)))
    { c.Response.StatusCode = 404; return; }
    if (c.Request.Path == "/" || c.Request.Path.StartsWithSegments("/read") || c.Request.Path.StartsWithSegments("/preview") || c.Request.Path == "/sitemap.xml")
    {
        c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (c.Request.Path == "/development" && !app.Environment.IsDevelopment()) { c.Response.StatusCode = 404; return; }
        if (c.Request.Path.StartsWithSegments("/preview")) c.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }
    await next(c);
});
app.UseAntiforgery();
app.MapGet("/sitemap.xml", async (ReadClient client, IConfiguration config, CancellationToken ct) =>
{
    var baseUri = config["Read:PublicBaseUri"] ?? (app.Environment.IsDevelopment() ? "http://localhost:5200/" : "");
    if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var root) || !app.Environment.IsDevelopment() && root.Scheme != "https") return Results.StatusCode(503);
    var ns = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9"); var urls = new List<string>();
    foreach (var collection in await client.Directory(ct))
    {
        var book = await client.Book(collection.Key, false, ct); if (book == null) continue;
        urls.Add("read/" + collection.Key);
        urls.Add($"read/{collection.Key}/universe");
        foreach (var chapter in book.Pieces.Select(x => x.Chapter).Distinct()) urls.Add($"read/{collection.Key}/chapter/{chapter}");
        foreach (var concept in book.Pieces.SelectMany(x => x.Concepts).Distinct()) urls.Add($"read/{collection.Key}/concept/{Uri.EscapeDataString(concept)}");
        foreach (var journey in book.Pieces.Select(x => x.Journey).Append("chronological").Distinct()) urls.Add($"read/{collection.Key}/journey/{Uri.EscapeDataString(journey)}");
        foreach (var piece in book.Pieces) urls.Add($"read/{collection.Key}/chapter/{piece.Chapter}/verse/{piece.Verse}?locale={Uri.EscapeDataString(piece.Locale)}&audience={piece.AudienceId}");
    }
    return Results.Content(new XDocument(new XElement(ns + "urlset", urls.Distinct().Select(path => new XElement(ns + "url", new XElement(ns + "loc", new Uri(root, path).AbsoluteUri))))).ToString(), "application/xml");
}).RequireRateLimiting("reader");
app.MapGet("/robots.txt", () => Results.Text("User-agent: *\nDisallow: /preview/\nDisallow: /development\nDisallow: /staff/\nSitemap: /sitemap.xml\n"));
app.MapPost("/read-events/{key}", async (string key, ReadEngagement r, ReadClient client, HttpContext c, CancellationToken ct) =>
{
    if (c.Request.Headers["X-Read-Event-Version"] != "1" || !r.Consent || r.Version != 1) return Results.BadRequest();
    await client.SendEvent(key, r, ct); return Results.NoContent();
}).DisableAntiforgery().RequireRateLimiting("reader");
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().RequireRateLimiting("reader");
app.Run();
