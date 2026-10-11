using ContentOS.Application;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

public sealed partial class SqlRuntimeTests
{
    [SqlFact]
    public async Task Saturated_provider_does_not_block_other_providers_and_capacity_remains_enforced()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        await ConfigureAsync(s); var store = s.GetRequiredService<IRuntimeStore>();
        var first = await store.CreateAsync(new(), Actor, Trace, Ct);
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        var held = await store.ClaimAsync("held-a", TimeSpan.FromSeconds(30), Ct); Assert.NotNull(held);
        var queued = await store.CreateAsync(new(), Actor, Trace, Ct);
        await ConfigureAsync(s, "test.b");
        var other = await store.CreateAsync(new(), Actor, Trace, Ct);
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        using var worker = root.CreateScope(); var next = worker.ServiceProvider.GetRequiredService<IRuntimeStore>();
        var b = await next.ClaimAsync("available-b", TimeSpan.FromSeconds(30), Ct);
        Assert.NotNull(b); Assert.Equal(other, b.JobId);
        Assert.Equal("Queued", (await next.InspectAsync(queued, Ct))!.Job.State);
        Assert.Null(await next.ClaimAsync("no-extra-a-capacity", TimeSpan.FromSeconds(5), Ct));
        await next.CompleteAsync(b, new(ProviderError.InvalidInput), Ct);
        await store.CompleteAsync(held, new(ProviderError.InvalidInput), Ct);
        Assert.True(await s.GetRequiredService<RuntimeRunner>().RunOneAsync("released-a", TimeSpan.FromSeconds(5), Ct));
        Assert.Equal("Completed", (await store.InspectAsync(queued, Ct))!.Job.State);
        Assert.Equal(first, held.JobId);
    }

    [SqlFact]
    public async Task Open_circuit_waits_without_spending_attempts_or_budget_then_resumes()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var connection = await ConfigureAsync(s, attempts: 1); var store = s.GetRequiredService<IRuntimeStore>();
        var id = await store.CreateAsync(new(), Actor, Trace, Ct);
        var db = s.GetRequiredService<RuntimeDbContext>();
        await db.Connections.Where(c => c.Id == connection).ExecuteUpdateAsync(p => p.SetProperty(c => c.CircuitUntilUtc, DateTimeOffset.UtcNow.AddSeconds(30)));
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        Assert.False(await s.GetRequiredService<RuntimeRunner>().RunOneAsync("circuit-wait", TimeSpan.FromSeconds(5), Ct));
        var waiting = (await store.InspectAsync(id, Ct))!;
        Assert.Equal("Queued", waiting.Job.State); Assert.Empty(waiting.Attempts);
        var budget = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal(0, budget.Used); Assert.False(budget.Settled);
        await db.Connections.Where(c => c.Id == connection).ExecuteUpdateAsync(p => p.SetProperty(c => c.CircuitUntilUtc, (DateTimeOffset?)null));
        Assert.True(await s.GetRequiredService<RuntimeRunner>().RunOneAsync("circuit-recovered", TimeSpan.FromSeconds(5), Ct));
        Assert.Equal("Completed", (await store.InspectAsync(id, Ct))!.Job.State);
        Assert.Equal(1, (await store.InspectAsync(id, Ct))!.Job.Attempts);
    }

    [SqlFact]
    public async Task Circuit_opened_after_claim_defers_admission_and_preserves_original_limits()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var connection = await ConfigureAsync(s, attempts: 1); var store = s.GetRequiredService<IRuntimeStore>();
        var id = await store.CreateAsync(new(), Actor, Trace, Ct);
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        var work = await store.ClaimAsync("circuit-race", TimeSpan.FromSeconds(5), Ct); Assert.NotNull(work);
        var db = s.GetRequiredService<RuntimeDbContext>();
        await db.Connections.Where(c => c.Id == connection).ExecuteUpdateAsync(p => p.SetProperty(c => c.CircuitUntilUtc, DateTimeOffset.UtcNow.AddSeconds(30)));
        var admission = await store.AdmitAsync(work, Ct); Assert.Equal(ProviderError.CircuitOpen, admission);
        await store.CompleteAsync(work, new(admission), Ct);
        var waiting = (await store.InspectAsync(id, Ct))!;
        Assert.Equal("Queued", waiting.Job.State); Assert.Equal(0, waiting.Job.Attempts);
        Assert.Equal("CircuitOpen", Assert.Single(waiting.Attempts).State);
        Assert.Equal(0, (await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id)).Used);
        Assert.False(await db.Effects.AnyAsync(e => e.Id == work.OperationId));
        await db.Connections.Where(c => c.Id == connection).ExecuteUpdateAsync(p => p.SetProperty(c => c.CircuitUntilUtc, (DateTimeOffset?)null));
        await db.Jobs.Where(j => j.Id == id).ExecuteUpdateAsync(p => p.SetProperty(j => j.ScheduledUtc, DateTimeOffset.UtcNow));
        Assert.True(await s.GetRequiredService<RuntimeRunner>().RunOneAsync("circuit-race-recovered", TimeSpan.FromSeconds(5), Ct));
        var final = (await store.InspectAsync(id, Ct))!;
        Assert.Equal("Completed", final.Job.State); Assert.Equal(work.OperationId, final.Operation!.Id);
        Assert.Equal(1, final.Job.Attempts); Assert.Single(await db.Effects.Where(e => e.Id == work.OperationId).ToListAsync());
    }

    [SqlFact]
    public async Task Circuit_wait_does_not_extend_original_deadline()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var connection = await ConfigureAsync(s); var store = s.GetRequiredService<IRuntimeStore>();
        var id = await store.CreateAsync(new(), Actor, Trace, Ct);
        await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        var db = s.GetRequiredService<RuntimeDbContext>();
        await db.Connections.Where(c => c.Id == connection).ExecuteUpdateAsync(p => p.SetProperty(c => c.CircuitUntilUtc, DateTimeOffset.UtcNow.AddMinutes(1)));
        await db.Jobs.Where(j => j.Id == id).ExecuteUpdateAsync(p => p.SetProperty(j => j.DeadlineUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
        Assert.Null(await store.ClaimAsync("circuit-deadline", TimeSpan.FromSeconds(5), Ct));
        Assert.Equal("DeadLetter", (await store.InspectAsync(id, Ct))!.Job.State);
        Assert.True((await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id)).Settled);
        Assert.False(await db.Operations.AnyAsync(o => o.JobId == id));
    }
}
