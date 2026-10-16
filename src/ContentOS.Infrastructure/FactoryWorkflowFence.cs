using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

// Lock order shared by factory controls, M8 and M7: workflow -> factory -> AI.
internal static class FactoryWorkflowFence
{
    internal static Task WorkflowLock(CoreDbContext db, CancellationToken ct) => db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='ContentOS.Workflows',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Workflow lock unavailable',1;", ct);
    private static Task Lock(CoreDbContext db, Guid tenant, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Factory:" + tenant},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Factory lock unavailable',1;", ct);
    internal static async Task Bind(CoreDbContext db, IServiceProvider services, CoreScope scope, Guid universe, DurableWorkflow run, WorkflowSnapshot snapshot, FactoryWorkflowAdmission proof, CancellationToken ct)
    {
        await Lock(db, scope.TenantId, ct);
        var work = await db.FactoryWorks.SingleOrDefaultAsync(w => w.Id == proof.WorkId && w.TenantId == scope.TenantId && w.UniverseId == universe, ct) ?? throw new CoreAccessException();
        var batch = await db.FactoryBatches.AsNoTracking().SingleAsync(b => b.Id == work.BatchId, ct);
        if (work.WorkflowId != null || work.State != "Running" || work.LeaseToken != proof.LeaseToken || work.LeaseUntilUtc <= DateTimeOffset.UtcNow || work.LeaseUntilUtc == null || run.OperationId != work.Id) throw new RuntimeValidationException("Factory lease no longer permits child admission.");
        var plan = await Check(db, services, scope, batch, work, ct);
        if (plan.GenerationWorkflow == null || EditorialBinding.Hash(plan.GenerationWorkflow) != EditorialBinding.Hash(snapshot)) throw new RuntimeValidationException("Pinned generation workflow changed.");
        run.DeadlineUtc = run.DeadlineUtc < batch.DeadlineUtc ? run.DeadlineUtc : batch.DeadlineUtc;
        work.WorkflowId = run.Id; work.State = "AwaitingWorkflow"; work.Outcome = "WorkflowQueued"; work.LeaseToken = null; work.LeaseUntilUtc = null;
    }
    internal static async Task Validate(CoreDbContext db, IServiceProvider? services, CoreScope scope, Guid runId, CancellationToken ct)
    {
        var work = await db.FactoryWorks.AsNoTracking().SingleOrDefaultAsync(w => w.WorkflowId == runId, ct);
        if (work == null) return;
        if (services == null) throw new RuntimeValidationException("Factory admission services unavailable.");
        // Inspection may be outside a transaction; dispatch is always inside the M7 workflow lock.
        if (db.Database.CurrentTransaction != null) await Lock(db, scope.TenantId, ct);
        var batch = await db.FactoryBatches.AsNoTracking().SingleAsync(b => b.Id == work.BatchId, ct);
        var run = await db.DurableWorkflows.AsNoTracking().SingleAsync(w => w.Id == runId, ct);
        if (work.TenantId != scope.TenantId || run.Actor != scope.Actor || run.OperationId != work.Id || run.UniverseId != work.UniverseId || work.State is not ("AwaitingWorkflow" or "Running" or "Generated" or "Reused")) throw new CoreAccessException();
        // Completed inventory remains inspectable; new admission still requires a live parent.
        if (work.State is "Generated" or "Reused")
        {
            await ((DraftFactory)services.GetRequiredService<IDraftFactory>()).RecheckGeneration(scope, work.UniverseId, batch.PlanId, work.BriefKey, ct, false); return;
        }
        await Check(db, services, scope, batch, work, ct);
    }
    private static async Task<FactoryPlanSnapshot> Check(CoreDbContext db, IServiceProvider services, CoreScope scope, FactoryBatch batch, FactoryWork work, CancellationToken ct)
    {
        if (batch.Actor != scope.Actor || batch.TenantId != scope.TenantId || batch.State is not ("Queued" or "Running") || batch.DeadlineUtc <= DateTimeOffset.UtcNow) throw new RuntimeValidationException("Factory batch no longer permits generation.", "FactoryAdmissionBlocked");
        var plan = await ((DraftFactory)services.GetRequiredService<IDraftFactory>()).RecheckGeneration(scope, work.UniverseId, batch.PlanId, work.BriefKey, ct);
        var generation = plan.GenerationWorkflow ?? throw new RuntimeValidationException("Pinned generation workflow required.");
        var budget = EditorialBinding.Read<FactoryBudget>(batch.BudgetJson);
        var count = await db.FactoryWorks.CountAsync(w => w.BatchId == batch.Id, ct);
        var charges = await (from w in db.FactoryWorks join step in db.WorkflowCheckpoints on w.WorkflowId equals (Guid?)step.RunId join ai in db.AiRuns on step.OperationId equals ai.OperationId where w.BatchId == batch.Id && ai.TenantId == scope.TenantId && step.Kind == "draft" select new { ai.Reserved, ai.Spent }).ToListAsync(ct);
        var bound = generation.Route.Settings.RunLimit;
        if (budget.Currency != generation.Route.Settings.Currency || count * bound + charges.Sum(x => Math.Max(0, x.Reserved + x.Spent - bound)) > budget.MaxGenerationCost) throw new RuntimeValidationException("Factory aggregate M7 exposure exceeds batch limit.", "QuotaExhausted");
        return plan;
    }
}
