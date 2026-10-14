using ContentOS.Application;
namespace GeetaStudio.Api;

public sealed class TalkRetention(IServiceScopeFactory scopes, ILogger<TalkRetention> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<ITalkWorkbench>().PurgeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("TALK retention sweep failed; conversation content is withheld on reads pending a successful sweep."); }
        }
    }
}
