using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GeetaStudio.Api;

public static class OperationalBoundary
{
    private static readonly Meter Meter = new("ContentOS.Operations", "1.0");
    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("http.duration", "ms");
    private static readonly Counter<long> Responses = Meter.CreateCounter<long>("http.responses");

    public static void UseOperationalBoundary(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                if (context.Request.Path.StartsWithSegments("/api") || context.User.Identity?.IsAuthenticated == true)
                    context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            try { await next(context); }
            finally
            {
                // Bounded categories only: never URLs, tenant/account IDs, bodies or query strings.
                var surface = context.Request.Path.StartsWithSegments("/api/v1/read") ? "Read" :
                    context.Request.Path.StartsWithSegments("/api/v1/customer") ? "Customer" :
                    context.Request.Path.StartsWithSegments("/health") ? "Health" : "StaffOrOther";
                var tags = new TagList { { "surface", surface }, { "statusClass", context.Response.StatusCode / 100 } };
                Latency.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
                Responses.Add(1, tags);
            }
        });
    }
}
