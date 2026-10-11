using System.Diagnostics;
using ContentOS.Contracts;
using ContentOS.Domain;

namespace ContentOS.Application;

public sealed class RuntimeRunner(IRuntimeStore store, IEnumerable<ITestOperationProvider> providers, ITelemetryExporter telemetry)
{
    public static readonly ActivitySource Traces = new("ContentOS.Runtime", "1.0.0");
    public async Task<bool> RunOneAsync(string worker, TimeSpan lease, CancellationToken ct)
    {
        var work = await store.ClaimAsync(worker, lease, ct);
        if (work is null) return false;
        using var activity = Traces.StartActivity("runtime.attempt", ActivityKind.Internal,
            new ActivityContext(ActivityTraceId.CreateFromString(work.TraceId.AsSpan()), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));
        activity?.SetTag("job.id", work.JobId);
        activity?.SetTag("attempt.id", work.AttemptId);
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = RenewAsync(work, lease, heartbeatStop.Token);
        try
        {
            ProviderOutcome outcome;
            var admission = await store.AdmitAsync(work, ct);
            if (admission != ProviderError.None) outcome = new(admission);
            else
            {
                var adapter = providers.SingleOrDefault(p => p.Key == work.Snapshot.AdapterKey);
                if (adapter is null) outcome = new(ProviderError.Configuration);
                else
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var remaining = work.Reconciliation ? TimeSpan.FromSeconds(10) : work.DeadlineUtc - DateTimeOffset.UtcNow;
                    deadline.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1));
                    try
                    {
                        outcome = work.Reconciliation
                            ? await adapter.ReconcileAsync(work, deadline.Token)
                            : await adapter.ExecuteAsync(work, deadline.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { outcome = new(ProviderError.Unknown); }
                    catch (OperationCanceledException) { throw; }
                    catch (RuntimeConflictException) { outcome = new(ProviderError.PolicyRejected); }
                    catch (Exception) { outcome = new(ProviderError.Unknown); }
                }
            }
            await store.CompleteAsync(work, outcome, ct);
            telemetry.Export(new RuntimeEvent(work.JobId, work.AttemptId, outcome.Error.ToString(), work.TraceId));
            return true;
        }
        finally
        {
            heartbeatStop.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }
    }
    private async Task RenewAsync(ClaimedWork work, TimeSpan lease, CancellationToken ct)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, lease.TotalMilliseconds / 3)), ct);
            if (!await store.HeartbeatAsync(work.JobId, work.LeaseToken, lease, ct)) return;
        }
    }
}
