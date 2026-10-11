using System.Data.Common;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

public sealed partial class SqlWorkflowTests
{
    [SqlFact]
    public async Task Cancellation_before_AI_admission_blocks_submission_even_if_tool_misses_cancellation()
    {
        var gate = new AdmissionGate();
        using var root = Services(x => { x.AddScoped<WorkflowTools>(); x.AddSingleton(gate); x.AddScoped<IToolInvoker, GatedTool>(); });
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s);
        var runtime = s.GetRequiredService<IWorkflowRuntime>();
        var run = await runtime.StartAsync(f.Owner, f.Universe, Request(f), Ct); await Advance(root, f, run.Id, 2);
        using var worker = root.CreateScope(); var processing = worker.ServiceProvider.GetRequiredService<IWorkflowWorker>().ProcessNextAsync(Ct);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var current = await runtime.InspectAsync(f.Owner, run.Id, Ct);
            Assert.Null(current.Steps[2].AiRunId);
            await runtime.ControlAsync(f.Owner, run.Id, "cancel", new(Guid.NewGuid(), current.RowVersion, "fixture:cancel-before-admission"), Ct);
        }
        finally { gate.Release.TrySetResult(); }
        await processing.WaitAsync(TimeSpan.FromSeconds(15));
        var cancelled = await runtime.InspectAsync(f.Owner, run.Id, Ct);
        Assert.Equal("Cancelled", cancelled.State); Assert.Null(cancelled.Steps[2].AiRunId); Assert.Null(cancelled.Steps[2].ResultJson);
        Assert.Empty(await s.GetRequiredService<IAiTaskRouter>().RunsAsync(f.Owner, Ct));
        var budget = Assert.Single(await s.GetRequiredService<IAiAdministration>().BudgetsAsync(f.Owner, Ct));
        Assert.Equal(0, budget.Reserved); Assert.Equal(0, budget.Spent);
    }
    [SqlFact]
    public async Task Replaced_lease_blocks_stale_worker_AI_admission()
    {
        var gate = new AdmissionGate();
        using var root = Services(x => { x.AddScoped<WorkflowTools>(); x.AddSingleton(gate); x.AddScoped<IToolInvoker, GatedTool>(); });
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s);
        var runtime = s.GetRequiredService<IWorkflowRuntime>();
        var run = await runtime.StartAsync(f.Owner, f.Universe, Request(f), Ct); await Advance(root, f, run.Id, 2);
        using var worker = root.CreateScope(); var processing = worker.ServiceProvider.GetRequiredService<IWorkflowWorker>().ProcessNextAsync(Ct);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var current = await runtime.InspectAsync(f.Owner, run.Id, Ct);
            await s.GetRequiredService<CoreDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [workflow].[WorkflowCheckpoint] SET [LeaseToken]={Guid.NewGuid()} WHERE [Id]={current.Steps[2].Id}");
        }
        finally { gate.Release.TrySetResult(); }
        await processing.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Empty(await s.GetRequiredService<IAiTaskRouter>().RunsAsync(f.Owner, Ct));
        var currentRun = await runtime.InspectAsync(f.Owner, run.Id, Ct);
        Assert.Equal("Running", currentRun.State); Assert.Null(currentRun.Steps[2].ResultJson);
        await runtime.ControlAsync(f.Owner, run.Id, "cancel", new(Guid.NewGuid(), currentRun.RowVersion, "fixture:cleanup-stale-worker"), Ct);
    }
    [SqlFact]
    public async Task Cancellation_after_reservation_blocks_attempt_and_releases_unused_budget()
    {
        var gate = new AdmissionGate(); gate.Release.TrySetResult();
        var sqlGate = new AttemptAdmissionGate();
        using var root = Services(x =>
        {
            x.AddScoped<WorkflowTools>(); x.AddSingleton(gate); x.AddScoped<IToolInvoker, GatedTool>();
            x.AddDbContextFactory<CoreDbContext>((_, o) => o.AddInterceptors(sqlGate));
        });
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s);
        var runtime = s.GetRequiredService<IWorkflowRuntime>();
        var run = await runtime.StartAsync(f.Owner, f.Universe, Request(f), Ct); await Advance(root, f, run.Id, 2);
        using var worker = root.CreateScope();
        sqlGate.Target = worker.ServiceProvider.GetRequiredService<CoreDbContext>().ContextId.InstanceId;
        var processing = worker.ServiceProvider.GetRequiredService<IWorkflowWorker>().ProcessNextAsync(Ct);
        try
        {
            await sqlGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var current = await runtime.InspectAsync(f.Owner, run.Id, Ct);
            Assert.NotNull(current.Steps[2].AiRunId);
            var reserved = await s.GetRequiredService<IAiTaskRouter>().InspectAsync(f.Owner, current.Steps[2].AiRunId!.Value, Ct);
            Assert.True(reserved.Reserved > 0); Assert.Empty(reserved.Attempts);
            await runtime.ControlAsync(f.Owner, run.Id, "cancel", new(Guid.NewGuid(), current.RowVersion, "fixture:cancel-before-attempt"), Ct);
        }
        finally { sqlGate.Release.TrySetResult(); }
        await processing.WaitAsync(TimeSpan.FromSeconds(15));
        var cancelled = await runtime.InspectAsync(f.Owner, run.Id, Ct);
        Assert.Equal("Cancelled", cancelled.State); Assert.Null(cancelled.Steps[2].ResultJson);
        var ai = await s.GetRequiredService<IAiTaskRouter>().InspectAsync(f.Owner, cancelled.Steps[2].AiRunId!.Value, Ct);
        Assert.Equal("Completed", ai.State); Assert.Equal("Cancelled", ai.Outcome); Assert.Empty(ai.Attempts);
        Assert.Equal(0, ai.Reserved); Assert.Equal(0, ai.Spent);
        var budget = Assert.Single(await s.GetRequiredService<IAiAdministration>().BudgetsAsync(f.Owner, Ct));
        Assert.Equal(0, budget.Reserved); Assert.Equal(0, budget.Spent);
    }
    private sealed class AdmissionGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class GatedTool(WorkflowTools inner, AdmissionGate gate) : IToolInvoker
    {
        public IReadOnlyList<WorkflowTool> Tools => inner.Tools;
        public Task<WorkflowEvidence> RetrieveAsync(CoreScope s, Guid u, StartWorkflow r, string locale, CancellationToken ct) => inner.RetrieveAsync(s, u, r, locale, ct);
        public async Task<AiRunView> GenerateAsync(CoreScope s, Guid u, GenerateAi r, CancellationToken ct)
        {
            gate.Entered.TrySetResult(); await gate.Release.Task;
            // Deliberately drop the signal: only the transactional state/lease fence can protect this call.
            return await inner.GenerateAsync(s, u, r, CancellationToken.None);
        }
    }
    private sealed class AttemptAdmissionGate : DbCommandInterceptor
    {
        public Guid Target { get; set; }
        private int workflowLocks;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            // Claim, reservation, then attempt: pause before the third transaction takes its workflow lock.
            if (data.Context?.ContextId.InstanceId == Target && command.CommandText.Contains("@Resource='ContentOS.Workflows'") && Interlocked.Increment(ref workflowLocks) == 3)
            { Entered.TrySetResult(); await Release.Task.WaitAsync(ct); }
            return result;
        }
    }
    private sealed class CancellationAwareProvider : ITextGenerationProvider
    {
        private readonly FixtureTextA inner = new();
        public string Key => inner.Key;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CancellationObserved { get; private set; }
        public void Validate(AiProviderSettings settings) => inner.Validate(settings);
        public async Task<AiProviderResult> GenerateAsync(AiProviderCall call, CancellationToken ct)
        {
            Entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { CancellationObserved = true; }
            // An already-submitted provider may still report authoritative usage after cancellation.
            return await inner.GenerateAsync(call, CancellationToken.None);
        }
        public IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall call, CancellationToken ct) => inner.StreamAsync(call, ct);
    }
}
