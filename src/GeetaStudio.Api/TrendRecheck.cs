using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace GeetaStudio.Api;

public sealed class TrendRecheck(IServiceScopeFactory scopes, ILogger<TrendRecheck> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
                var owners = await db.TrendClusters.AsNoTracking().Select(x => new { x.TenantId, x.UniverseId }).Distinct().Take(100).ToListAsync(ct);
                foreach (var owner in owners)
                {
                    var actor = await db.Members.Where(x => x.TenantId == owner.TenantId && (x.Permission == "Administrator" || x.Permission == "Editor")).Select(x => x.Actor).FirstOrDefaultAsync(ct);
                    if (actor != null) await scope.ServiceProvider.GetRequiredService<ITrendWorkbench>().RecheckAsync(new(owner.TenantId, actor), owner.UniverseId, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e) { log.LogWarning("Trend evidence recheck unavailable: {ExceptionType}", e.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
