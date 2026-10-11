using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using GeetaStudio.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;

var builder = WebApplication.CreateBuilder(args);
if (args.Contains("--migrate-development") || args.Contains("--setup-development") || args.Contains("--seed-development"))
{
    try { await DevelopmentSetup.RunAsync(builder.Environment, args.Contains("--setup-development"), args.Contains("--seed-development")); }
    catch (Exception) { Console.Error.WriteLine("Development setup failed. Check the explicit local database, migration identity, and existing runtime credentials; no reset performed."); Environment.ExitCode = 1; }
    return;
}
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddTransient<IClaimsTransformation, StaffAccess>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StaffAdmin", p => p.RequireAuthenticatedUser().RequireRole("Administrator"));
    options.AddPolicy("StaffOperate", p => p.RequireAuthenticatedUser().RequireRole("Administrator", "Operator"));
    options.AddPolicy("StaffRead", p => p.RequireAuthenticatedUser().RequireRole("Administrator", "Operator", "Reader"));
});
var enabled = builder.Configuration.GetValue<bool>("Runtime:Enabled");
if (enabled)
{
    builder.Services.AddContentRuntime(builder.Configuration, builder.Environment.IsDevelopment());
    builder.Services.AddContentCore(builder.Configuration);
    builder.Services.AddSingleton<ILegacyEmbeddingProvider, ReviewedConceptEmbedding>();
    builder.Services.AddSingleton<ITextGenerationProvider, FixtureTextA>();
    builder.Services.AddSingleton<ITextGenerationProvider, FixtureTextB>();
    builder.Services.AddSingleton<IAiEmbeddingAdapter, LocalAiEmbedding>();
    builder.Services.AddSingleton<ITextGenerationProvider>(s => new OpenAiTextProvider(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) }, s.GetRequiredService<ISecretResolver>()));
    builder.Services.AddAiControlPlane(builder.Configuration);
    builder.Services.AddOwnedWorkflows();
    builder.Services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationA>();
    builder.Services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationB>();
    builder.Services.AddLessons();
    if (builder.Configuration.GetValue("Workflows:WorkerEnabled", true)) builder.Services.AddHostedService<WorkflowProcessor>();
    builder.Services.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>();
    builder.Services.AddSingleton<IKnowledgeRankingAdapter, IndependentFixtureRanking>();
    if (builder.Configuration.GetValue("Knowledge:IndexWorkerEnabled", true)) builder.Services.AddHostedService<KnowledgeIndexProcessor>();
    foreach (var key in RuntimeRules.Adapters)
        builder.Services.AddScoped<ITestOperationProvider>(s => new TestOperationProvider(key, s.GetRequiredService<ITestEffectLedger>()));
}
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 131072);
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
        if (enabled && context.Response.StatusCode is 401 or 403 && (context.Request.Path.StartsWithSegments("/api/v1/runtime") || context.Request.Path.StartsWithSegments("/api/v1/core") || context.Request.Path.StartsWithSegments("/api/v1/sources") || context.Request.Path.StartsWithSegments("/api/v1/corpus") || context.Request.Path.StartsWithSegments("/api/v1/evidence") || context.Request.Path.StartsWithSegments("/api/v1/knowledge") || context.Request.Path.StartsWithSegments("/api/v1/ai") || context.Request.Path.StartsWithSegments("/api/v1/workflows") || context.Request.Path.StartsWithSegments("/api/v1/lessons")))
            await context.RequestServices.GetRequiredService<IRuntimeStore>().AuditAsync(context.User.Identity?.Name ?? "anonymous", "admin.access-denied", null,
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier, context.RequestAborted);
    }
    catch (BadHttpRequestException e) { context.Response.StatusCode = e.StatusCode; await context.Response.WriteAsJsonAsync(new { error = "Invalid request body or content type." }); }
    catch (CoreAccessException e)
    {
        context.Response.StatusCode = 403;
        if (enabled) await context.RequestServices.GetRequiredService<IRuntimeStore>().AuditAsync(context.User.Identity?.Name ?? "anonymous",
            "core.scope-denied", null, context.TraceIdentifier, context.RequestAborted);
        await context.Response.WriteAsJsonAsync(new { error = e.Message });
    }
    catch (RightsBlockedException e)
    {
        context.Response.StatusCode = 422;
        if (enabled) await context.RequestServices.GetRequiredService<IRuntimeStore>().AuditAsync(context.User.Identity?.Name ?? "anonymous",
            "sources.rights-blocked", null, context.TraceIdentifier, context.RequestAborted);
        await context.Response.WriteAsJsonAsync(new { error = e.Message, outcome = e.Reason });
    }
    catch (EvidenceBlockedException e)
    {
        context.Response.StatusCode = 422;
        if (enabled) await context.RequestServices.GetRequiredService<IRuntimeStore>().AuditAsync(context.User.Identity?.Name ?? "anonymous", "evidence.gate-denied", null, context.TraceIdentifier, context.RequestAborted);
        await context.Response.WriteAsJsonAsync(new { error = e.Message, outcome = e.Reason });
    }
    catch (KnowledgeBlockedException e) { context.Response.StatusCode = 422; if (enabled) await context.RequestServices.GetRequiredService<IRuntimeStore>().AuditAsync(context.User.Identity?.Name ?? "anonymous", "knowledge.gate-denied", null, context.TraceIdentifier, context.RequestAborted); await context.Response.WriteAsJsonAsync(new { error = e.Message, outcome = e.Reason }); }
    catch (CoreConcurrencyException e) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = e.Message, outcome = "ConcurrencyConflict" }); }
    catch (RuntimeValidationException e) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = e.Message, outcome = e.Outcome }); }
    catch (RuntimeConflictException e) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = e.Message }); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception error)
    {
        app.Logger.LogWarning("Runtime request failed: {ExceptionType}", error.GetType().Name);
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new { error = "Runtime unavailable. Inspect redacted operational state and readiness." });
    }
});
app.UseAuthentication(); app.UseAuthorization();
// Integrated authentication is ambient in browsers. A custom action header prevents cross-site form submissions.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) && (context.Request.Path.StartsWithSegments("/api/v1/runtime") || context.Request.Path.StartsWithSegments("/api/v1/core") || context.Request.Path.StartsWithSegments("/api/v1/sources") || context.Request.Path.StartsWithSegments("/api/v1/corpus") || context.Request.Path.StartsWithSegments("/api/v1/evidence") || context.Request.Path.StartsWithSegments("/api/v1/knowledge") || context.Request.Path.StartsWithSegments("/api/v1/ai") || context.Request.Path.StartsWithSegments("/api/v1/workflows") || context.Request.Path.StartsWithSegments("/api/v1/lessons"))
        && context.Request.Headers["X-ContentOS-Staff"] != "1")
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "Staff action header X-ContentOS-Staff: 1 is required." });
        if (enabled) await context.RequestServices.GetRequiredService<IRuntimeStore>().AuditAsync(context.User.Identity?.Name ?? "anonymous", "admin.action-header-rejected", null,
            System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier, context.RequestAborted);
        return;
    }
    await next(context);
});
app.MapGet("/health/live", () => new HealthStatus("GeetaStudio.Api", "Healthy", DateTimeOffset.UtcNow)).AllowAnonymous();
if (app.Environment.IsDevelopment())
    app.MapGet("/api/v1/development/status", () => new DevelopmentStatus("Development", true,
        enabled ? "M1 local deterministic adapters; SQL-backed runtime enabled." : "M0 fixture: runtime disabled; no database required.")).AllowAnonymous();
if (enabled) { app.MapRuntime(); app.MapCore(); app.MapSources(); app.MapCorpus(); app.MapEvidence(); app.MapKnowledge(); app.MapAi(); app.MapWorkflows(); app.MapLessons(); }
else app.MapGet("/health/ready", () => Results.Json(new { status = "RuntimeDisabled" }, statusCode: 503));
app.Run();


