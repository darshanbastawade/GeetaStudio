using ContentOS.Application;
namespace GeetaStudio.Api;

public sealed class MediaProcessor(IServiceScopeFactory scopes, ILogger<MediaProcessor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); if (await scope.ServiceProvider.GetRequiredService<IMediaLibrary>().ProcessNextAsync(Environment.MachineName + "-media", ct)) continue; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e) { log.LogWarning("Media processor unavailable: {ExceptionType}", e.GetType().Name); }
            await Task.Delay(1000, ct);
        }
    }
}
