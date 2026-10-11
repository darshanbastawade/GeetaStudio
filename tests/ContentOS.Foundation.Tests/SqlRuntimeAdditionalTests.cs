using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

public sealed partial class SqlRuntimeTests
{
    [SqlFact]
    public async Task Heartbeat_and_connection_concurrency_prevent_a_second_worker_starting()
    {
        using var root = Services(); using var setup = root.CreateScope(); var s = setup.ServiceProvider; await ConfigureAsync(s);
        var store = s.GetRequiredService<IRuntimeStore>(); var firstId = await store.CreateAsync(new(), Actor, Trace, Ct);
        var secondId = await store.CreateAsync(new(), Actor, Trace, Ct); await s.GetRequiredService<IJobTransport>().DispatchAsync(Ct);
        using var first = root.CreateScope(); var firstStore = first.ServiceProvider.GetRequiredService<IRuntimeStore>();
        var work = await firstStore.ClaimAsync("heartbeat-a", TimeSpan.FromMilliseconds(600), Ct); Assert.NotNull(work);
        Assert.Equal(firstId, work.JobId); await Task.Delay(300);
        Assert.True(await firstStore.HeartbeatAsync(firstId, work.LeaseToken, TimeSpan.FromSeconds(5), Ct)); await Task.Delay(400);
        using var contender = root.CreateScope(); Assert.Null(await contender.ServiceProvider.GetRequiredService<IRuntimeStore>().ClaimAsync("heartbeat-b", TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(ProviderError.None, await firstStore.AdmitAsync(work, Ct));
        var adapter = first.ServiceProvider.GetServices<ITestOperationProvider>().Single(p => p.Key == work.Snapshot.AdapterKey);
        await firstStore.CompleteAsync(work, await adapter.ExecuteAsync(work, Ct), Ct);
        await contender.ServiceProvider.GetRequiredService<RuntimeRunner>().RunOneAsync("heartbeat-b", TimeSpan.FromSeconds(5), Ct);
        Assert.Equal("Completed", (await store.InspectAsync(secondId, Ct))!.Job.State);
    }
    [SqlFact]
    public async Task Snapshot_immutability_rollback_and_UTC_survive_persistence()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; await ConfigureAsync(s);
        var store = s.GetRequiredService<IRuntimeStore>(); var id = await store.CreateAsync(new(ScheduledUtc: DateTimeOffset.UtcNow.AddMinutes(1).ToOffset(TimeSpan.FromHours(5.5))), Actor, Trace, Ct);
        var before = (await store.InspectAsync(id, Ct))!.Job; await ConfigureAsync(s, "test.b");
        await s.GetRequiredService<IProviderConfigurationStore>().ActivateAsync(before.Snapshot.RevisionId, Actor, Trace, Ct);
        var after = await store.CreateAsync(new(), Actor, Trace, Ct); Assert.Equal(before.Snapshot.RevisionId, (await store.InspectAsync(after, Ct))!.Job.Snapshot.RevisionId);
        var db = s.GetRequiredService<RuntimeDbContext>(); db.ChangeTracker.Clear(); var row = await db.Jobs.SingleAsync(j => j.Id == id);
        Assert.Equal(TimeSpan.Zero, row.ScheduledUtc.Offset); Assert.Equal("Asia/Kolkata", row.Timezone);
        row.Snapshot = "modified"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Equal(before.Snapshot, (await store.InspectAsync(id, Ct))!.Job.Snapshot);
        await store.CancelAsync(id, Actor, Trace, Ct); await store.CancelAsync(after, Actor, Trace, Ct);
    }
    [SqlFact]
    public async Task Budget_quota_is_checked_atomically_before_job_and_outbox_creation()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; await ConfigureAsync(s);
        var db = s.GetRequiredService<RuntimeDbContext>(); var budget = await db.Budgets.SingleAsync(); var originalLimit = budget.Limit;
        try
        {
            budget.Limit = budget.Spent + budget.Reserved; await db.SaveChangesAsync();
            var count = await db.Jobs.CountAsync();
            await Assert.ThrowsAsync<RuntimeConflictException>(() => s.GetRequiredService<IRuntimeStore>().CreateAsync(new(), Actor, Trace, Ct));
            Assert.Equal(count, await db.Jobs.CountAsync());
        }
        finally { db.ChangeTracker.Clear(); budget = await db.Budgets.SingleAsync(); budget.Limit = originalLimit; await db.SaveChangesAsync(); }
    }
    [SqlFact]
    public async Task Actual_runtime_logins_have_no_schema_or_server_admin_permissions()
    {
        foreach (var host in new[] { "geetastudio-api-52ed9b61-c102-4716-9bd4-901652979e8c", "dotnet-GeetaStudio.Worker-b45aea23-7d36-4708-83da-a606622b04ef" })
        {
            var config = new ConfigurationBuilder().AddUserSecrets(host).Build(); var connection = config.GetConnectionString("ContentOS");
            if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Provision restricted local runtime identities with db-update.ps1 -ProvisionRuntime first.");
            await using var db = new RuntimeDbContext(new DbContextOptionsBuilder<RuntimeDbContext>().UseSqlServer(connection).Options);
            await db.Database.OpenConnectionAsync(); await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT IS_SRVROLEMEMBER('sysadmin'), HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER'), HAS_PERMS_BY_NAME('runtime.Jobs', 'OBJECT', 'DELETE')";
            await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
            Assert.Equal(0, reader.GetInt32(0)); Assert.Equal(0, reader.GetInt32(1)); Assert.Equal(0, reader.GetInt32(2));
        }
    }
}
