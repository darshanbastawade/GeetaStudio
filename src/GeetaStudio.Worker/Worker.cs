using ContentOS.Application;

namespace GeetaStudio.Worker;

public sealed class Worker(ILogger<Worker> logger, IServiceScopeFactory scopes, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Runtime:Enabled"))
        {
            logger.LogInformation("Runtime disabled: worker idle; no database or provider calls.");
            try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            return;
        }
        var worker = Guid.NewGuid().ToString("N");
        logger.LogInformation("SQL runtime worker started {WorkerId}", worker);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IJobTransport>().DispatchAsync(stoppingToken);
                    if (!await scope.ServiceProvider.GetRequiredService<RuntimeRunner>().RunOneAsync(worker, TimeSpan.FromSeconds(9), stoppingToken))
                        await Task.Delay(500, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { logger.LogWarning("Runtime iteration failed; durable state will be recovered after lease expiry."); await Task.Delay(1000, stoppingToken); }
            }
        }
        finally { logger.LogInformation("SQL runtime worker stopped {WorkerId}; unfinished leases remain recoverable", worker); }
    }
}
