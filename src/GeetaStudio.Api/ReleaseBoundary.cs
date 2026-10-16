using ContentOS.Application;
using ContentOS.Infrastructure;
namespace GeetaStudio.Api;

public static class ReleaseBoundary
{
    public static bool Enforced(IHostEnvironment environment, IConfiguration config) => !environment.IsDevelopment() || config.GetValue<bool>("Release:Enforce");
    public static IServiceCollection AddReleaseGate(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var root = Path.GetFullPath(configuration["Release:Root"] ?? (Directory.Exists(Path.Combine(environment.ContentRootPath, "ops")) ? environment.ContentRootPath : Path.Combine(environment.ContentRootPath, "..", "..")));
        var code = ReleaseGate.BinaryFingerprint(AppContext.BaseDirectory);
        services.AddScoped<IReleaseModuleEvidence, ReleaseModuleEvidence>();
        services.AddScoped<IReleaseGate>(s => new ReleaseGate(configuration, s.GetRequiredService<IReleaseModuleEvidence>(), root, code, environment.EnvironmentName));
        return services;
    }
    public static bool Allows(ReleaseDecision decision, string method, string path)
    {
        var parts = path.Trim('/').Split('/');
        if (parts.Length < 3 || parts[0] != "api" || parts[1] != "v1") return false;
        var feature = parts[2] switch { "read" => "READ", "watch" => "WATCH", "social" => "ManualExport", _ => "" };
        var selected = decision.Features.SingleOrDefault(x => x.Key == feature && x.Selected && x.Ready);
        if (selected == null || decision.Blocks.Length > 0) return false;
        if (feature == "READ")
            return method == "GET" && (parts.Length == 3 || parts.Length == 4 && selected.Targets.Any(x => x.Target.Key == parts[3]))
                || method == "POST" && parts.Length == 5 && parts[4] == "events" && selected.Targets.Any(x => x.Target.Key == parts[3]);
        if (method != "GET" || parts.Length < 9 || parts[3] != "tenants" || parts[5] != "universes"
            || !Guid.TryParse(parts[4], out var tenant) || !Guid.TryParse(parts[6], out var universe) || !Guid.TryParse(parts[8], out var id)) return false;
        return selected.Targets.Any(x => x.Target.TenantId == tenant && x.Target.UniverseId == universe && x.ResourceIds.Contains(id)) &&
            (feature == "WATCH" && (parts[7] == "productions" && (parts.Length == 9 || parts.Length == 10 && parts[9] is "export" or "captions.vtt")
                || parts[7] == "artifacts" && parts.Length == 10 && parts[9] == "preview")
            || feature == "ManualExport" && parts[7] == "packages" && parts.Length == 10 && parts[9] == "export");
    }
    public static void UseReleaseBoundary(this WebApplication app)
    {
        if (!Enforced(app.Environment, app.Configuration)) return;
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            if (path.StartsWith("/health/", StringComparison.Ordinal) || path == "/api/v1/release/decision") { await next(context); return; }
            // Public pilot exposes only current selected READ content and authenticated selected WATCH/manual exports.
            // Staff authoring remains available in the separate private Development environment.
            if (!(path.StartsWith("/api/v1/read/", StringComparison.Ordinal) || path == "/api/v1/read" || path.StartsWith("/api/v1/watch/", StringComparison.Ordinal) || path.StartsWith("/api/v1/social/", StringComparison.Ordinal)))
            { context.Response.StatusCode = 404; return; }
            var actor = app.Configuration["Release:EvidenceActor"] ?? Environment.UserDomainName + "\\" + Environment.UserName;
            try
            {
                var decision = await context.RequestServices.GetRequiredService<IReleaseGate>().EvaluateAsync(actor, context.RequestAborted);
                if (Allows(decision, context.Request.Method, path)) { context.Items[typeof(ReleaseDecision)] = decision; await next(context); return; }
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return; }
            catch (Exception) { /* Evidence store unavailable: no release routes. */ }
            context.Response.StatusCode = 404;
        });
    }
}
