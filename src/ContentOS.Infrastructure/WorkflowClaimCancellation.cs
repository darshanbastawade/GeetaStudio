using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

// A separate context observes committed cancellation/lease loss across worker processes.
// Database admission fencing remains authoritative; this signal also interrupts claimed tool/provider work.
internal sealed class WorkflowClaimCancellation : IAsyncDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly Task watching;
    public WorkflowClaimCancellation(IDbContextFactory<CoreDbContext> factory, Guid runId, Guid stepId, Guid lease, CancellationTokenSource work)
    {
        watching = Watch();
        async Task Watch()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(100, stop.Token);
                    await using var db = await factory.CreateDbContextAsync(stop.Token);
                    var now = DateTimeOffset.UtcNow;
                    var valid = await (from run in db.DurableWorkflows
                                       join step in db.WorkflowCheckpoints on run.Id equals step.RunId
                                       where run.Id == runId && run.State == "Running" && run.DeadlineUtc > now &&
                                       run.NextStep == step.Number && step.Id == stepId && step.State == "Running" &&
                                       step.LeaseToken == lease && step.LeaseUntilUtc > now
                                       select step.Id).AnyAsync(stop.Token);
                    valid = valid && !await db.FactoryWorks.AnyAsync(w => w.WorkflowId == runId &&
                        (w.State != "AwaitingWorkflow" && w.State != "Running" || db.FactoryBatches.Any(b => b.Id == w.BatchId &&
                            (b.State != "Queued" && b.State != "Running" || b.DeadlineUtc <= now))), stop.Token);
                    if (!valid) { await work.CancelAsync(); return; }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception) { await work.CancelAsync(); } // Fail closed if ownership cannot be observed.
        }
    }
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        await watching;
        stop.Dispose();
    }
}
