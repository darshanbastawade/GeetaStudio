using GeetaStudio.Web;
using GeetaStudio.Web.Components;

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

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
