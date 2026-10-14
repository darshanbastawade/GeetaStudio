using ContentOS.Application;
namespace GeetaStudio.Api;

public sealed class WatchProcessor(IServiceScopeFactory scopes, ILogger<WatchProcessor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    { while (!ct.IsCancellationRequested) { try { using var scope = scopes.CreateScope(); if (await scope.ServiceProvider.GetRequiredService<IWatchFactory>().ProcessNextAsync(ct)) continue; } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; } catch (Exception e) { log.LogWarning("WATCH processor unavailable: {ExceptionType}", e.GetType().Name); } await Task.Delay(1000, ct); } }
}
