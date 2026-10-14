using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure;

public sealed class ConfigurationSecretResolver(IConfiguration configuration) : ISecretResolver
{
    public ValueTask<ResolvedSecret?> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? value = null;
        if (Regex.IsMatch(reference, "^env:GEETA_PROVIDER_[A-Z0-9_]{1,80}$"))
            value = Environment.GetEnvironmentVariable(reference[4..]);
        else if (Regex.IsMatch(reference, "^config:ProviderSecrets:[A-Za-z0-9_-]{1,80}$")) value = configuration[reference[7..]];
        return ValueTask.FromResult(string.IsNullOrWhiteSpace(value) ? null : new ResolvedSecret(value));
    }
    public async ValueTask<bool> IsAvailableAsync(string? reference, CancellationToken cancellationToken = default)
    {
        if (reference is null) return true; // Local test adapters need no authentication.
        using var secret = await ResolveAsync(reference, cancellationToken);
        return secret is not null;
    }
}

public static class RuntimeServices
{
    public static IServiceCollection AddContentRuntime(this IServiceCollection services, IConfiguration configuration, bool development)
    {
        string ValidatedConnection()
        {
            var connection = configuration.GetConnectionString("ContentOS");
            if (string.IsNullOrWhiteSpace(connection)) throw new RuntimeValidationException("Configure ConnectionStrings:ContentOS outside source control before enabling runtime.");
            SqlConnectionStringBuilder parsed;
            try { parsed = new SqlConnectionStringBuilder(connection); }
            catch (ArgumentException) { throw new RuntimeValidationException("Invalid database configuration."); }
            if (development && parsed.InitialCatalog is not ("GeetaStudio_Dev" or "GeetaStudio_M1Tests")) throw new RuntimeValidationException("Development runtime requires a dedicated GeetaStudio database.");
            if (!development && (parsed.TrustServerCertificate || parsed.Encrypt == SqlConnectionEncryptOption.Optional)) throw new RuntimeValidationException("Production requires validated encrypted SQL connections.");
            return connection;
        }
        _ = ValidatedConnection();
        // Match Core's deferred configuration resolution; revalidate the final host configuration before use.
        services.AddDbContextFactory<RuntimeDbContext>(o => o.UseSqlServer(ValidatedConnection()).EnableSensitiveDataLogging(false).EnableDetailedErrors(false));
        services.AddSingleton<ISecretResolver, ConfigurationSecretResolver>();
        services.AddScoped<RuntimeStore>();
        services.AddScoped<IRuntimeStore>(s => s.GetRequiredService<RuntimeStore>());
        services.AddScoped<IProviderConfigurationStore>(s => s.GetRequiredService<RuntimeStore>());
        services.AddScoped<ITestEffectLedger>(s => s.GetRequiredService<RuntimeStore>());
        services.AddScoped<IJobTransport>(s => s.GetRequiredService<RuntimeStore>());
        services.AddScoped<IAssetStore, LocalAssetStore>();
        services.AddSingleton<ITelemetryExporter, LocalTelemetryExporter>();
        services.AddScoped<RuntimeRunner>();
        return services;
    }
}

public sealed class LocalTelemetryExporter(ILogger<LocalTelemetryExporter> logger) : ITelemetryExporter, IDisposable
{
    private readonly System.Diagnostics.ActivityListener _listener = Listen(logger);
    private static System.Diagnostics.ActivityListener Listen(ILogger logger)
    {
        var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = source => source.Name == "ContentOS.Runtime",
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => logger.LogInformation("Runtime trace {TraceId} span {SpanId} duration {DurationMs}", activity.TraceId, activity.SpanId, activity.Duration.TotalMilliseconds)
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);
        return listener;
    }
    public void Dispose() => _listener.Dispose();
    private static readonly Meter Metrics = new("ContentOS.Runtime", "1.0.0");
    private static readonly Counter<long> Attempts = Metrics.CreateCounter<long>("runtime.attempts");
    public void Export(RuntimeEvent runtimeEvent)
    {
        Attempts.Add(1, new KeyValuePair<string, object?>("state", runtimeEvent.State));
        logger.LogInformation("Runtime attempt {AttemptId} job {JobId} state {State} trace {TraceId}", runtimeEvent.AttemptId, runtimeEvent.JobId, runtimeEvent.State, runtimeEvent.TraceId);
    }
}

public sealed class LocalAssetStore(RuntimeDbContext db, IConfiguration configuration) : IAssetStore
{
    private readonly string _root = Path.GetFullPath(configuration["Assets:Root"] ?? throw new RuntimeValidationException("Configure an asset root."));
    public async Task<Guid> PutAsync(ReadOnlyMemory<byte> content, string actor, CancellationToken ct)
    {
        if (content.Length is < 1 or > 1048576) throw new RuntimeValidationException("M1 assets must contain 1 to 1048576 bytes.");
        Directory.CreateDirectory(_root);
        var id = Guid.NewGuid(); var path = Path.Combine(_root, id.ToString("N") + ".bin");
        try
        {
            await File.WriteAllBytesAsync(path, content.ToArray(), ct);
            db.Assets.Add(new Asset { Id = id, Length = content.Length, Sha256 = Convert.ToHexString(SHA256.HashData(content.Span)), Owner = actor, CreatedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); return id;
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    public async Task<byte[]?> GetAsync(Guid id, string actor, CancellationToken ct)
    {
        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.Owner == actor, ct);
        if (asset is null) return null;
        var mapping = await db.AssetLocations.AsNoTracking().Where(x => x.AssetId == id).Select(x => x.StoreKey).SingleOrDefaultAsync(ct);
        var root = mapping == null || mapping == "primary" ? _root : Path.GetFullPath(configuration["Assets:Stores:" + mapping] ?? throw new RuntimeConflictException("Asset storage mapping unavailable."));
        var path = Path.Combine(root, id.ToString("N") + ".bin");
        if (!File.Exists(path)) throw new RuntimeConflictException("Asset bytes are unavailable; storage reconciliation required.");
        if (new FileInfo(path).Length != asset.Length) throw new RuntimeConflictException("Asset size mismatch; storage reconciliation required.");
        var content = await File.ReadAllBytesAsync(path, ct);
        if (content.LongLength != asset.Length || Convert.ToHexString(SHA256.HashData(content)) != asset.Sha256)
            throw new RuntimeConflictException("Asset checksum mismatch; storage reconciliation required.");
        return content;
    }
}
