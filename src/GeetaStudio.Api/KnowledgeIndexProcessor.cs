using ContentOS.Application;
namespace GeetaStudio.Api;
// API-hosted background worker reuses its narrowly granted corpus/source read identity.
// The separate M01 Worker retains its existing runtime-only SQL privileges.
public sealed class KnowledgeIndexProcessor(IServiceScopeFactory scopes, ILogger<KnowledgeIndexProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); if (await scope.ServiceProvider.GetRequiredService<IKnowledgeIndexWorker>().ProcessNextAsync(stoppingToken)) continue; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning("Knowledge indexing unavailable: {ExceptionType}", e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
