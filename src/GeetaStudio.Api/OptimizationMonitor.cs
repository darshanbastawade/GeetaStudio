using ContentOS.Application;
namespace GeetaStudio.Api;

public sealed class OptimizationMonitor(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<OptimizationMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Optimization:MonitorEnabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IOptimizationWorkbench>().MonitorAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Optimization monitoring failed ({Type}); retry next sweep.", ex.GetType().Name); }
        }
    }
}
