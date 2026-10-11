using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace GeetaStudio.Api;

public sealed class ReadRetention(IServiceScopeFactory scopes, ILogger<ReadRetention> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
                await db.ReadEventCounts.Where(x => x.Day < DateTime.UtcNow.Date.AddDays(-6)).ExecuteDeleteAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e) { log.LogWarning("READ retention unavailable: {ExceptionType}", e.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
