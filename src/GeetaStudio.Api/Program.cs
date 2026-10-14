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
    options.AddPolicy("Customer", p => p.RequireAuthenticatedUser());
    options.AddPolicy("StaffAuthor", p => p.RequireAuthenticatedUser().RequireRole("Administrator", "Author", "Editor"));
    options.AddPolicy("StaffEditor", p => p.RequireAuthenticatedUser().RequireRole("Administrator", "Editor"));
    options.AddPolicy("StaffPublish", p => p.RequireAuthenticatedUser().RequireRole("Administrator", "Editor", "Publisher"));
    options.AddPolicy("StaffCms", p => p.RequireAuthenticatedUser().RequireRole("Administrator", "Author", "Editor", "Publisher"));
    options.AddPolicy("StaffAdmin", p => p.RequireAuthenticatedUser().RequireRole("Administrator"));
    options.AddPolicy("StaffOperate", p => p.RequireAuthenticatedUser().RequireRole("Administrator", "Operator"));
    options.AddPolicy("StaffRead", p => p.RequireAuthenticatedUser().RequireRole("Administrator", "Operator", "Reader", "Author", "Editor", "Publisher"));
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
    builder.Services.AddLessons(); builder.Services.AddUniverseRegistry();
    builder.Services.AddSingleton<IFactoryTemplate, LocalDraftTemplateA>(); builder.Services.AddSingleton<IFactoryTemplate, LocalDraftTemplateB>(); builder.Services.AddDraftFactory(); builder.Services.AddQualityEvaluation(); builder.Services.AddEditorialWorkbench(); builder.Services.AddReadWorkbench(); builder.Services.AddTrendWorkbench(); builder.Services.AddSingleton<ITrendSource, FixtureTrendSourceA>(); builder.Services.AddSingleton<ITrendSource, FixtureTrendSourceB>(); builder.Services.AddSingleton<ITrendSource, UploadedRssTrendSource>(); builder.Services.AddSingleton<ITrendExtractor, OwnedTrendExtractorA>(); builder.Services.AddSingleton<ITrendExtractor, OwnedTrendExtractorB>(); if (builder.Configuration.GetValue("Trends:RecheckWorkerEnabled", true)) builder.Services.AddHostedService<TrendRecheck>(); foreach (var key in new[] { "evaluation.fixture.a.v1", "evaluation.fixture.b.v1", "evaluation.fabricated.v1", "evaluation.unsafe.v1", "evaluation.fluent-failure.v1" }) builder.Services.AddSingleton<IEvaluationAdapter>(new EvaluationFixtureAdapter(key));
    if (builder.Configuration.GetValue("Read:RetentionWorkerEnabled", true)) builder.Services.AddHostedService<ReadRetention>();
    if (builder.Configuration.GetValue("Factory:WorkerEnabled", false)) builder.Services.AddHostedService<FactoryProcessor>();
    if (builder.Configuration.GetValue("Workflows:WorkerEnabled", true)) builder.Services.AddHostedService<WorkflowProcessor>();
    builder.Services.AddCommerceWorkbench(); foreach (var key in new[] { "delivery.fixture.a.v1", "delivery.fixture.b.v1", "delivery.unknown.v1", "email.fixture.inbox.v1", "print.fixture.v1" }) builder.Services.AddSingleton<ICommerceDelivery>(s => new CommerceDeliveryAdapter(key, s.GetRequiredService<ICommerceDeliveryLedger>())); if (builder.Configuration.GetValue("Commerce:WorkerEnabled", true)) builder.Services.AddHostedService<CommerceProcessor>(); builder.Services.AddCreationStudio(); builder.Services.AddSingleton<ICreationRenderer>(new CreationRenderer()); builder.Services.AddSingleton<ICreationRenderer>(new CreationRenderer("create.local.b.v1")); if (builder.Configuration.GetValue("Create:WorkerEnabled", true)) builder.Services.AddHostedService<CreationProcessor>(); builder.Services.AddCustomerWorkbench(); builder.Services.AddTalkWorkbench(); if (builder.Configuration.GetValue("Talk:RetentionWorkerEnabled", true)) builder.Services.AddHostedService<TalkRetention>(); builder.Services.AddSingleton<IIdentityProvider, HostIdentityProvider>(); builder.Services.AddSingleton<INotificationProvider, InboxNotificationProvider>();
    var customerFixtureKey = builder.Configuration["Customers:FixtureSigningKey"];
    if (builder.Environment.IsDevelopment() && customerFixtureKey is { Length: >= 32 })
    {
        foreach (var key in new[] { "identity.fixture.a.v1", "identity.fixture.b.v1" }) builder.Services.AddSingleton<IIdentityProvider>(new FixtureIdentityProvider(key, customerFixtureKey));
        foreach (var key in new[] { "payment.fixture.a.v1", "payment.fixture.b.v1", "payment.unknown.v1" }) builder.Services.AddScoped<IPaymentProvider>(s => new FixturePaymentProvider(key, s.GetRequiredService<ICustomerPaymentEvidenceStore>(), customerFixtureKey));
    }
    builder.Services.AddOptimization(); builder.Services.AddHostedService<OptimizationMonitor>();
    builder.Services.AddAnalyticsWorkbench(); foreach (var key in new[] { "analytics.import.v1", "analytics.fixture.a.v1", "analytics.fixture.b.v1" }) builder.Services.AddSingleton<IChannelAnalyticsProvider>(new AnalyticsImportProvider(key));
    builder.Services.AddSingleton<ISocialCapabilityCatalog, SocialChannelProfiles>(); builder.Services.AddSocialPublishing(); foreach (var key in new[] { "social.fixture.a.v1", "social.fixture.b.v1", "social.unknown.v1", "social.rate-limit.v1" }) builder.Services.AddSingleton<ISocialPublisher>(s => new SocialFixturePublisher(key, s.GetRequiredService<ISocialEffectLedger>())); if (builder.Configuration.GetValue("Social:WorkerEnabled", true)) builder.Services.AddHostedService<SocialProcessor>();
    builder.Services.AddWatchFactory();
    builder.Services.AddSingleton<ISubtitleProvider, ApprovedSceneSubtitles>();
    var watchToolsPath = builder.Configuration["Watch:ToolsConfig"] ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".local", "M17-tools.json"));
    var watchTools = File.Exists(watchToolsPath) ? System.Text.Json.JsonSerializer.Deserialize<WatchToolConfiguration>(File.ReadAllText(watchToolsPath), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })! : new WatchToolConfiguration("", "", "", "", "", "", Path.GetFullPath(".local/watch-renderer"));
    builder.Configuration["Assets:Stores:watch"] ??= Path.GetFullPath((builder.Configuration["Assets:Root"] ?? ".local/assets") + ".watch");
    var watchRenderer = new FfmpegWatchRenderer(watchTools); builder.Services.AddSingleton<IVideoRenderer>(watchRenderer); builder.Services.AddSingleton<IVideoRenderer>(new FfmpegWatchRenderer(watchTools, "watch.ffmpeg.test-b.v1")); builder.Services.AddSingleton<IVideoRenderer>(new WatchRenderFixture(watchRenderer, "watch.fail-once.v1")); builder.Services.AddSingleton<IVideoRenderer>(new WatchRenderFixture(watchRenderer, "watch.unknown.v1"));
    if (builder.Configuration.GetValue("Watch:WorkerEnabled", true)) builder.Services.AddHostedService<WatchProcessor>();
    builder.Services.AddMediaLibrary();
    builder.Services.AddSingleton<IMediaFileInspector, MediaFileInspector>();
    builder.Services.AddSingleton<IImageGenerationProvider, ReferenceImageProvider>();
    builder.Services.AddSingleton<IImageGenerationProvider, BasicImageProvider>();
    builder.Services.AddSingleton<IImageGenerationProvider, UnknownImageFixtureProvider>();
    builder.Services.AddSingleton<ISpeechSynthesisProvider, ToneSpeechFixtureProvider>();
    builder.Services.AddSingleton<ISpeechSynthesisProvider>(_ => new WindowsSpeechProvider(builder.Configuration["Media:ProviderSpool"] ?? Path.GetFullPath((builder.Configuration["Assets:Root"] ?? ".local/assets") + ".media-spool")));
    builder.Configuration["Assets:Stores:archive"] ??= Path.GetFullPath((builder.Configuration["Assets:Root"] ?? ".local/assets") + ".archive");
    if (builder.Configuration.GetValue("Media:WorkerEnabled", true)) builder.Services.AddHostedService<MediaProcessor>();
    builder.Services.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>();
    builder.Services.AddSingleton<IKnowledgeRankingAdapter, IndependentFixtureRanking>();
    if (builder.Configuration.GetValue("Knowledge:IndexWorkerEnabled", true)) builder.Services.AddHostedService<KnowledgeIndexProcessor>();
    foreach (var key in RuntimeRules.Adapters)
        builder.Services.AddScoped<ITestOperationProvider>(s => new TestOperationProvider(key, s.GetRequiredService<ITestEffectLedger>()));
}
builder.Services.AddReadLimits();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 131072);
var app = builder.Build();
app.UseOperationalBoundary();
app.Use(async (c, next) => { if (c.Request.Path.StartsWithSegments("/api/v1/media")) { var size = c.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>(); if (size is { IsReadOnly: false }) size.MaxRequestBodySize = 1572864; } await next(c); });
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
        if (enabled && context.Response.StatusCode is 401 or 403 && (context.Request.Path.StartsWithSegments("/api/v1/customer") || context.Request.Path.StartsWithSegments("/api/v1/customer-admin") || context.Request.Path.StartsWithSegments("/api/v1/runtime") || context.Request.Path.StartsWithSegments("/api/v1/core") || context.Request.Path.StartsWithSegments("/api/v1/sources") || context.Request.Path.StartsWithSegments("/api/v1/corpus") || context.Request.Path.StartsWithSegments("/api/v1/evidence") || context.Request.Path.StartsWithSegments("/api/v1/knowledge") || context.Request.Path.StartsWithSegments("/api/v1/ai") || context.Request.Path.StartsWithSegments("/api/v1/workflows") || context.Request.Path.StartsWithSegments("/api/v1/lessons") || context.Request.Path.StartsWithSegments("/api/v1/registry") || context.Request.Path.StartsWithSegments("/api/v1/factory") || context.Request.Path.StartsWithSegments("/api/v1/evaluation") || context.Request.Path.StartsWithSegments("/api/v1/editorial") || context.Request.Path.StartsWithSegments("/api/v1/read-admin") || context.Request.Path.StartsWithSegments("/api/v1/trends") || context.Request.Path.StartsWithSegments("/api/v1/media") || context.Request.Path.StartsWithSegments("/api/v1/watch")))
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
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/read") || context.Request.Path.StartsWithSegments("/api/v1/read-preview") || context.Request.Path.StartsWithSegments("/api/v1/read-admin") || context.Request.Path.StartsWithSegments("/api/v1/trends") || context.Request.Path.StartsWithSegments("/api/v1/media") || context.Request.Path.StartsWithSegments("/api/v1/watch"))
    { context.Response.Headers.CacheControl = "no-store"; context.Response.Headers["X-Content-Type-Options"] = "nosniff"; }
    await next(context);
});
app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
// Integrated authentication is ambient in browsers. A custom action header prevents cross-site form submissions.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) && (context.Request.Path.StartsWithSegments("/api/v1/optimization") || context.Request.Path.StartsWithSegments("/api/v1/customer") || context.Request.Path.StartsWithSegments("/api/v1/customer-admin") || context.Request.Path.StartsWithSegments("/api/v1/runtime") || context.Request.Path.StartsWithSegments("/api/v1/core") || context.Request.Path.StartsWithSegments("/api/v1/sources") || context.Request.Path.StartsWithSegments("/api/v1/corpus") || context.Request.Path.StartsWithSegments("/api/v1/evidence") || context.Request.Path.StartsWithSegments("/api/v1/knowledge") || context.Request.Path.StartsWithSegments("/api/v1/ai") || context.Request.Path.StartsWithSegments("/api/v1/workflows") || context.Request.Path.StartsWithSegments("/api/v1/lessons") || context.Request.Path.StartsWithSegments("/api/v1/registry") || context.Request.Path.StartsWithSegments("/api/v1/factory") || context.Request.Path.StartsWithSegments("/api/v1/evaluation") || context.Request.Path.StartsWithSegments("/api/v1/editorial") || context.Request.Path.StartsWithSegments("/api/v1/read-admin") || context.Request.Path.StartsWithSegments("/api/v1/trends") || context.Request.Path.StartsWithSegments("/api/v1/media") || context.Request.Path.StartsWithSegments("/api/v1/watch"))
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
if (enabled) { app.MapRuntime(); app.MapCore(); app.MapSources(); app.MapCorpus(); app.MapEvidence(); app.MapKnowledge(); app.MapAi(); app.MapWorkflows(); app.MapLessons(); app.MapRegistry(); app.MapFactory(); app.MapEvaluation(); app.MapEditorial(); app.MapRead(); app.MapTrends(); app.MapMedia(); app.MapWatch(); app.MapSocial(); app.MapAnalytics(); app.MapOptimization(); app.MapCustomers(); app.MapTalk(); app.MapCreations(); app.MapCommerce(); }
else app.MapGet("/health/ready", () => Results.Json(new { status = "RuntimeDisabled" }, statusCode: 503));
app.Run();
