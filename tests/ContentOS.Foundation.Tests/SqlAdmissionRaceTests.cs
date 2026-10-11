using ContentOS.Application;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

public sealed partial class SqlRuntimeTests
{
    [SqlFact]
    public async Task Kill_after_admission_blocks_effect_and_requires_original_binding_reconciliation()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var connection = await ConfigureAsync(s); var store = s.GetRequiredService<IRuntimeStore>();
        var id = await store.CreateAsync(new(), Actor, Trace, Ct); await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        var work = await store.ClaimAsync("admission-race", TimeSpan.FromSeconds(5), Ct); Assert.NotNull(work);
        Assert.Equal(ProviderError.None, await store.AdmitAsync(work, Ct));
        await s.GetRequiredService<IProviderConfigurationStore>().SetEnabledAsync(connection, false, true, Actor, Trace, Ct);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => s.GetRequiredService<ITestEffectLedger>()
            .SubmitAsync(work.OperationId, connection, work.Snapshot.AdapterKey, Ct));
        await store.CompleteAsync(work, new(ProviderError.PolicyRejected), Ct);
        Assert.Equal("Unknown", (await store.InspectAsync(id, Ct))!.Job.State);
        Assert.False(await s.GetRequiredService<RuntimeDbContext>().Effects.AnyAsync(e => e.Id == work.OperationId));
        await s.GetRequiredService<IProviderConfigurationStore>().SetEnabledAsync(connection, true, false, Actor, Trace, Ct);
        await store.ReconcileAsync(id, Actor, Trace, Ct);
        var runner = s.GetRequiredService<RuntimeRunner>(); await runner.RunOneAsync("authoritative-reconcile", TimeSpan.FromSeconds(5), Ct);
        Assert.Equal("Queued", (await store.InspectAsync(id, Ct))!.Job.State);
        await runner.RunOneAsync("bounded-original-resubmit", TimeSpan.FromSeconds(5), Ct);
        var final = (await store.InspectAsync(id, Ct))!;
        Assert.Equal("Completed", final.Job.State); Assert.Equal(work.OperationId, final.Operation!.Id);
        Assert.Single(await s.GetRequiredService<RuntimeDbContext>().Effects.Where(e => e.Id == work.OperationId).ToListAsync());
    }
}
