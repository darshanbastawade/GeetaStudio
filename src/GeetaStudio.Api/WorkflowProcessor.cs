using ContentOS.Application;
namespace GeetaStudio.Api;
// Same restricted API identity as M06 indexing. Standalone M01 Worker is not granted source or AI access.
public sealed class WorkflowProcessor(IServiceScopeFactory scopes, ILogger<WorkflowProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); if (await scope.ServiceProvider.GetRequiredService<IWorkflowWorker>().ProcessNextAsync(stoppingToken)) continue; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning("Workflow processing unavailable: {ExceptionType}", e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
