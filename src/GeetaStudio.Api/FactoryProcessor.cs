using ContentOS.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace GeetaStudio.Api;

public sealed class FactoryProcessor(IServiceScopeFactory scopes, ILogger<FactoryProcessor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); if (await scope.ServiceProvider.GetRequiredService<IDraftFactory>().ProcessNextAsync(stoppingToken)) continue; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { log.LogWarning("Draft factory iteration failed: {ExceptionType}", e.GetType().Name); }
            try { await Task.Delay(1000, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
