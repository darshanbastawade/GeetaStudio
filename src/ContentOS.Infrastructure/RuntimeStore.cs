using System.Data;
using System.Diagnostics;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed class RuntimeStore(RuntimeDbContext db, ISecretResolver secrets, IDbContextFactory<RuntimeDbContext> factory) : IRuntimeStore, IProviderConfigurationStore, IJobTransport, ITestEffectLedger
{
    private static string Trace() => Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
    private AuditEntry AddAudit(string actor, string action, Guid? subject, string trace)
    {
        var entry = new AuditEntry { Id = Guid.NewGuid(), Actor = actor, Action = action, SubjectId = subject, TraceId = trace, CreatedUtc = DateTimeOffset.UtcNow };
        db.Audit.Add(entry);
        return entry;
    }
    public async Task AuditAsync(string actor, string action, Guid? subject, string trace, CancellationToken ct)
    { AddAudit(actor, action, subject, trace); await db.SaveChangesAsync(ct); }
    public async Task<ProviderView> RegisterAsync(RegisterProvider request, string actor, string trace, CancellationToken ct)
    {
        RuntimeRules.Validate(request.AdapterKey, request.Endpoint, request.Capability, request.Locale, request.MaxConcurrency, 1, 1, 60, "Asia/Kolkata");
        if (!await secrets.IsAvailableAsync(request.SecretRef, ct)) throw new RuntimeValidationException("Credential reference is invalid or unavailable.");
        var connection = new ProviderConnection { Id = Guid.NewGuid(), AdapterKey = request.AdapterKey, Endpoint = request.Endpoint, SecretRef = request.SecretRef, MaxConcurrency = request.MaxConcurrency };
        var profile = new CapabilityProfile { Id = Guid.NewGuid(), ConnectionId = connection.Id, Capability = request.Capability, Locale = request.Locale };
        db.Connections.Add(connection); db.Profiles.Add(profile);
        AddAudit(actor, "provider.register", connection.Id, trace);
        await db.SaveChangesAsync(ct);
        return new(connection.Id, profile.Id, connection.AdapterKey, true, false, true, connection.MaxConcurrency, null);
    }
    public async Task<IReadOnlyList<ProviderView>> ProvidersAsync(CancellationToken ct)
    {
        var rows = await (from c in db.Connections.AsNoTracking() join p in db.Profiles.AsNoTracking() on c.Id equals p.ConnectionId select new { c, p }).ToListAsync(ct);
        var result = new List<ProviderView>();
        foreach (var row in rows) result.Add(new(row.c.Id, row.p.Id, row.c.AdapterKey, row.c.Enabled, row.c.Killed,
            await secrets.IsAvailableAsync(row.c.SecretRef, ct), row.c.MaxConcurrency, row.c.CircuitUntilUtc));
        return result;
    }
    public async Task<Guid> RevisionAsync(CreateRevision request, string actor, string trace, CancellationToken ct)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Id == request.ProfileId, ct) ?? throw new RuntimeValidationException("Profile does not exist.");
        var connection = await db.Connections.SingleAsync(c => c.Id == profile.ConnectionId, ct);
        RuntimeRules.Validate(connection.AdapterKey, connection.Endpoint, profile.Capability, profile.Locale, connection.MaxConcurrency,
            request.MaxAttempts, request.Budget, request.DeadlineSeconds, request.Timezone);
        var revision = new RouteRevision
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            MaxAttempts = request.MaxAttempts,
            Budget = request.Budget,
            DeadlineSeconds = request.DeadlineSeconds,
            Timezone = request.Timezone,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        db.Revisions.Add(revision); AddAudit(actor, "route.create", revision.Id, trace); await db.SaveChangesAsync(ct); return revision.Id;
    }
    public async Task TestAsync(Guid connection, CancellationToken ct)
    {
        var c = await db.Connections.SingleOrDefaultAsync(c => c.Id == connection, ct) ?? throw new RuntimeValidationException("Connection does not exist.");
        if (!c.Enabled || c.Killed) throw new RuntimeConflictException("Provider is disabled or killed.");
        if (!await secrets.IsAvailableAsync(c.SecretRef, ct)) throw new RuntimeValidationException("Credential is unavailable.");
        var p = await db.Profiles.SingleAsync(p => p.ConnectionId == c.Id, ct);
        RuntimeRules.Validate(c.AdapterKey, c.Endpoint, p.Capability, p.Locale, c.MaxConcurrency, 1, 1, 60, "Asia/Kolkata");
        var definition = await db.Definitions.SingleAsync(d => d.Id == c.AdapterKey, ct);
        if (definition.Capability != p.Capability || definition.ContractVersion != p.ContractVersion || definition.ApprovedEndpoint != c.Endpoint || !definition.SupportsReconciliation || !definition.SupportsIdempotency)
            throw new RuntimeValidationException("Profile does not match the deployed, reviewed adapter definition.");
    }
    public async Task ActivateAsync(Guid revision, string actor, string trace, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.Revisions.SingleOrDefaultAsync(r => r.Id == revision, ct) ?? throw new RuntimeValidationException("Revision does not exist.");
        var p = await db.Profiles.SingleAsync(p => p.Id == r.ProfileId, ct);
        await TestAsync(p.ConnectionId, ct);
        var head = await db.Routes.SingleOrDefaultAsync(h => h.Id == RuntimeRules.Capability, ct);
        if (head is null) db.Routes.Add(new RouteHead { RevisionId = revision }); else head.RevisionId = revision;
        AddAudit(actor, "route.activate-or-rollback", revision, trace); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task SetEnabledAsync(Guid connection, bool enabled, bool killed, string actor, string trace, CancellationToken ct)
    {
        var c = await db.Connections.SingleOrDefaultAsync(c => c.Id == connection, ct) ?? throw new RuntimeValidationException("Connection does not exist.");
        c.Enabled = enabled; c.Killed = killed;
        AddAudit(actor, killed ? "provider.kill" : enabled ? "provider.enable" : "provider.disable", connection, trace);
        await db.SaveChangesAsync(ct);
    }
    public async Task<Guid> CreateAsync(CreateJob request, string actor, string trace, CancellationToken ct)
    {
        RuntimeRules.ValidateScenario(request.Scenario);
        if (string.IsNullOrWhiteSpace(request.Timezone)) throw new RuntimeValidationException("Schedule timezone is required.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.Timezone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new RuntimeValidationException("Invalid timezone."); }
        var now = DateTimeOffset.UtcNow; var scheduled = request.ScheduledUtc?.ToUniversalTime() ?? now;
        if (scheduled < now.AddMinutes(-1) || scheduled > now.AddDays(7)) throw new RuntimeValidationException("Schedule must be an offset-bearing instant within seven days.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var head = await db.Routes.SingleOrDefaultAsync(h => h.Id == RuntimeRules.Capability, ct) ?? throw new RuntimeValidationException("No active runtime route.");
        var r = await db.Revisions.SingleAsync(r => r.Id == head.RevisionId, ct);
        var p = await db.Profiles.SingleAsync(p => p.Id == r.ProfileId, ct);
        var c = await db.Connections.SingleAsync(c => c.Id == p.ConnectionId, ct);
        await TestAsync(c.Id, ct);
        var budget = await db.Budgets.SingleAsync(ct);
        if (budget.Spent + budget.Reserved + r.Budget > budget.Limit) throw new RuntimeConflictException("Budget quota exhausted.");
        budget.Reserved += r.Budget;
        var snapshot = new ExecutionSnapshot(r.Id, p.Id, c.Id, c.AdapterKey, p.Capability, p.Locale, p.ContractVersion, r.MaxAttempts, r.Budget, r.DeadlineSeconds);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ConnectionId = c.Id,
            Snapshot = JsonSerializer.Serialize(snapshot),
            Scenario = request.Scenario,
            ScheduledUtc = scheduled,
            DeadlineUtc = scheduled.AddSeconds(r.DeadlineSeconds),
            Timezone = request.Timezone,
            CreatedUtc = now
        };
        db.Jobs.Add(job); db.Reservations.Add(new BudgetReservation { Id = job.Id, Reserved = r.Budget });
        db.Outbox.Add(new OutboxMessage { Id = Guid.NewGuid(), JobId = job.Id, CreatedUtc = now });
        AddAudit(actor, "job.create", job.Id, trace); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return job.Id;
    }
    public async Task<int> DispatchAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var events = await db.Outbox.FromSqlRaw("SELECT TOP (20) * FROM [runtime].[Outbox] WITH (UPDLOCK, READPAST, ROWLOCK) WHERE [DispatchedUtc] IS NULL ORDER BY [CreatedUtc]").ToListAsync(ct);
        foreach (var message in events)
        {
            if (!await db.Inbox.AnyAsync(i => i.Id == message.Id, ct))
            {
                db.Inbox.Add(new InboxMessage { Id = message.Id, ReceivedUtc = DateTimeOffset.UtcNow });
                var job = await db.Jobs.SingleAsync(j => j.Id == message.JobId, ct);
                if (job.State == "PendingDispatch") job.State = "Queued";
            }
            message.DispatchedUtc = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return events.Count;
    }
    private static ExecutionSnapshot Snapshot(Job job) => JsonSerializer.Deserialize<ExecutionSnapshot>(job.Snapshot)!;
    private static JobView View(Job job) => new(job.Id, job.State, Snapshot(job), job.ScheduledUtc, job.Timezone, job.AttemptCount, job.CancelRequested, job.FailureReason, job.Result, job.LeaseOwner, job.LeaseUntilUtc, job.HeartbeatUtc);
    public async Task<IReadOnlyList<JobView>> JobsAsync(CancellationToken ct) => (await db.Jobs.AsNoTracking().OrderByDescending(j => j.CreatedUtc).Take(100).ToListAsync(ct)).Select(View).ToArray();
    public async Task<JobDetail?> InspectAsync(Guid job, CancellationToken ct)
    {
        var row = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == job, ct); if (row is null) return null;
        var attempts = await db.Attempts.AsNoTracking().Where(a => a.JobId == job).OrderBy(a => a.Number)
            .Select(a => new AttemptView(a.Id, a.Number, a.State, a.Error, a.TraceId, a.AuditId, a.Reconciliation, a.StartedUtc, a.EndedUtc, a.Usage)).ToListAsync(ct);
        var op = await db.Operations.AsNoTracking().SingleOrDefaultAsync(o => o.JobId == job, ct);
        return new(View(row), attempts, op is null ? null : new(op.Id, op.ConnectionId, op.AdapterKey, op.State, op.ExternalResourceId, op.LastAttemptId));
    }
    private async Task<Job> LockJobAsync(Guid id, CancellationToken ct) => await db.Jobs.FromSqlInterpolated($"SELECT * FROM [runtime].[Jobs] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync(ct) ?? throw new RuntimeValidationException("Job does not exist.");
    private async Task SettleAsync(Job job, CancellationToken ct)
    {
        var reservation = await db.Reservations.SingleAsync(r => r.Id == job.Id, ct);
        if (reservation.Settled) return;
        var budget = await db.Budgets.SingleAsync(ct);
        budget.Reserved -= reservation.Reserved; budget.Spent += reservation.Used; reservation.Settled = true;
    }
    public async Task CancelAsync(Guid id, string actor, string trace, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var job = await LockJobAsync(id, ct); job.CancelRequested = true;
        if (job.State is "Queued" or "PendingDispatch")
        {
            var op = await db.Operations.SingleOrDefaultAsync(o => o.JobId == id, ct);
            if (op is null || op.State is "Prepared" or "Rejected") { job.State = "Cancelled"; await SettleAsync(job, ct); }
            else job.State = "Unknown";
        }
        AddAudit(actor, "job.cancel-requested", id, trace); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task ReconcileAsync(Guid id, string actor, string trace, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var job = await LockJobAsync(id, ct);
        if (job.State != "Unknown") throw new RuntimeConflictException("Only unknown outcomes can be reconciled.");
        job.State = "Queued"; job.ScheduledUtc = DateTimeOffset.UtcNow;
        AddAudit(actor, "job.reconcile", id, trace); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task RetryDeadLetterAsync(Guid id, string actor, string trace, CancellationToken ct)
    {
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, ct) ?? throw new RuntimeValidationException("Job does not exist.");
        if (job.State != "DeadLetter") throw new RuntimeConflictException("Only terminal dead letters can be explicitly cloned; uncertain operations must be reconciled.");
        var clone = await CreateAsync(new CreateJob(job.Scenario, Timezone: job.Timezone), actor, trace, ct);
        await AuditAsync(actor, "job.dead-letter-clone:" + clone.ToString("N"), id, trace, ct);
    }
    public async Task<ClaimedWork?> ClaimAsync(string worker, TimeSpan lease, CancellationToken ct)
    {
        if (lease < TimeSpan.FromMilliseconds(100) || lease > TimeSpan.FromMinutes(2)) throw new ArgumentOutOfRangeException(nameof(lease));
        db.ChangeTracker.Clear();
        var now = DateTimeOffset.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await db.Jobs.FromSqlInterpolated($"SELECT TOP (1) j.* FROM [runtime].[Jobs] j WITH (UPDLOCK, READPAST, ROWLOCK) WHERE ((j.[State] = 'Queued' AND j.[ScheduledUtc] <= {now}) OR (j.[State] = 'Running' AND j.[LeaseUntilUtc] < {now})) AND EXISTS (SELECT 1 FROM [runtime].[Connections] c WHERE c.[Id] = j.[ConnectionId] AND (c.[CircuitUntilUtc] IS NULL OR c.[CircuitUntilUtc] <= {now} OR j.[DeadlineUtc] <= {now} OR j.[CancelRequested] = 1) AND (SELECT COUNT(*) FROM [runtime].[Jobs] running WHERE running.[ConnectionId] = j.[ConnectionId] AND running.[Id] <> j.[Id] AND running.[State] = 'Running' AND running.[LeaseUntilUtc] > {now}) < c.[MaxConcurrency]) ORDER BY j.[ScheduledUtc], j.[Id]").SingleOrDefaultAsync(ct);
        if (job is null) return null;
        var connection = await db.Connections.FromSqlInterpolated($"SELECT * FROM [runtime].[Connections] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {job.ConnectionId}").SingleAsync(ct);
        if (await db.Jobs.CountAsync(j => j.ConnectionId == job.ConnectionId && j.Id != job.Id && j.State == "Running" && j.LeaseUntilUtc > now, ct) >= connection.MaxConcurrency) return null;
        foreach (var previous in await db.Attempts.Where(a => a.JobId == job.Id && a.State == "Started").ToListAsync(ct))
        { previous.State = "LeaseExpired"; previous.EndedUtc = now; previous.Error = "Unknown"; }
        var snapshot = Snapshot(job);
        var operation = await db.Operations.SingleOrDefaultAsync(o => o.JobId == job.Id, ct);
        var reconciliation = operation?.State is "Submitted" or "Unknown";
        if (!reconciliation && (job.CancelRequested || now >= job.DeadlineUtc || job.AttemptCount >= snapshot.MaxAttempts))
        {
            job.State = job.CancelRequested ? "Cancelled" : "DeadLetter"; job.FailureReason = "Cancelled, deadline or attempt limit reached.";
            await SettleAsync(job, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
        }
        if (operation is null)
        {
            operation = new ExternalOperation { Id = Guid.NewGuid(), JobId = job.Id, ConnectionId = snapshot.ConnectionId, AdapterKey = snapshot.AdapterKey };
            db.Operations.Add(operation);
        }
        job.State = "Running"; job.LeaseOwner = worker; job.LeaseToken = Guid.NewGuid(); job.HeartbeatUtc = now; job.LeaseUntilUtc = now.Add(lease);
        if (!reconciliation) job.AttemptCount++;
        var trace = Trace(); var audit = AddAudit(worker, reconciliation ? "attempt.reconcile" : "attempt.start", job.Id, trace);
        var number = await db.Attempts.CountAsync(a => a.JobId == job.Id, ct) + 1;
        var attempt = new JobAttempt
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            LeaseToken = job.LeaseToken.Value,
            Number = number,
            TraceId = trace,
            AuditId = audit.Id,
            Reconciliation = reconciliation,
            StartedUtc = now
        };
        db.Attempts.Add(attempt); operation.LastAttemptId = attempt.Id;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(job.Id, attempt.Id, job.LeaseToken.Value, operation.Id, snapshot, job.Scenario, reconciliation, job.DeadlineUtc, trace);
    }
    public async Task<bool> HeartbeatAsync(Guid id, Guid token, TimeSpan lease, CancellationToken ct)
    {
        await using var heartbeatDb = await factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return await heartbeatDb.Jobs.Where(j => j.Id == id && j.LeaseToken == token && j.State == "Running" && j.LeaseUntilUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.HeartbeatUtc, now).SetProperty(j => j.LeaseUntilUtc, now.Add(lease)), ct) == 1;
    }
    public async Task<ProviderError> AdmitAsync(ClaimedWork work, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await LockJobAsync(work.JobId, ct);
        var c = await db.Connections.FromSqlInterpolated($"SELECT * FROM [runtime].[Connections] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {work.Snapshot.ConnectionId}").SingleAsync(ct);
        var now = DateTimeOffset.UtcNow;
        if (job.LeaseToken != work.LeaseToken || job.LeaseUntilUtc <= now || !c.Enabled || c.Killed || !await secrets.IsAvailableAsync(c.SecretRef, ct)) return ProviderError.PolicyRejected;
        if (!work.Reconciliation && (job.CancelRequested || job.DeadlineUtc <= now)) return ProviderError.PolicyRejected;
        if (c.CircuitUntilUtc > now) return ProviderError.CircuitOpen;
        var reservation = await db.Reservations.SingleAsync(r => r.Id == job.Id, ct);
        if (!work.Reconciliation)
        {
            if (reservation.Settled || reservation.Used + 1 > reservation.Reserved) return ProviderError.PolicyRejected;
            reservation.Used += 1;
            var op = await db.Operations.SingleAsync(o => o.Id == work.OperationId, ct); op.State = "Submitted";
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ProviderError.None;
    }
    public async Task CompleteAsync(ClaimedWork work, ProviderOutcome result, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var job = await LockJobAsync(work.JobId, ct);
        if (job.LeaseToken != work.LeaseToken || job.State != "Running" || job.LeaseUntilUtc <= DateTimeOffset.UtcNow) return;
        var attempt = await db.Attempts.SingleAsync(a => a.Id == work.AttemptId, ct);
        var op = await db.Operations.SingleAsync(o => o.Id == work.OperationId, ct);
        var c = await db.Connections.SingleAsync(c => c.Id == work.Snapshot.ConnectionId, ct);
        attempt.EndedUtc = DateTimeOffset.UtcNow; attempt.Error = result.Error == ProviderError.None ? null : result.Error.ToString();
        attempt.State = result.Error == ProviderError.None ? "Confirmed" : result.Error.ToString(); attempt.Usage = work.Reconciliation ? 0 : result.Usage;
        if (result.Error == ProviderError.CircuitOpen)
        {
            // Admission made no provider call: preserve budget, submission allowance and original binding.
            if (!work.Reconciliation) job.AttemptCount--;
            job.State = !work.Reconciliation && (job.CancelRequested || job.DeadlineUtc <= DateTimeOffset.UtcNow)
                ? (job.CancelRequested ? "Cancelled" : "DeadLetter") : "Queued";
            job.ScheduledUtc = c.CircuitUntilUtc ?? DateTimeOffset.UtcNow;
            if (!work.Reconciliation && job.ScheduledUtc > job.DeadlineUtc) job.ScheduledUtc = job.DeadlineUtc;
            job.FailureReason = "Circuit open; waiting for provider recovery within the original deadline.";
        }
        else if (result.AuthoritativeNotFound && work.Reconciliation)
        {
            op.State = "Prepared"; job.State = job.CancelRequested ? "Cancelled" : "Queued"; job.ScheduledUtc = DateTimeOffset.UtcNow;
            // No effect exists: keep consumed attempt accounting; a future submission is bounded by the original snapshot.
        }
        else if (result.Error == ProviderError.None)
        {
            if (work.Reconciliation)
            {
                var submitted = await db.Attempts.Where(a => a.JobId == job.Id && !a.Reconciliation).OrderByDescending(a => a.Number).FirstOrDefaultAsync(ct);
                if (submitted is not null) submitted.Usage = result.Usage;
            }
            op.State = "Confirmed"; op.ExternalResourceId = result.ResourceId; job.Result = result.Result;
            job.State = job.CancelRequested ? "Cancelled" : "Completed"; job.FailureReason = null; c.Failures = 0;
        }
        else if (result.Error == ProviderError.Unknown || work.Reconciliation)
        { op.State = "Unknown"; job.State = "Unknown"; job.FailureReason = "Unknown outcome; reconcile original binding before submission."; }
        else if (result.Error == ProviderError.PolicyRejected && op.State is "Submitted" or "Unknown")
        { job.State = "Unknown"; op.State = "Unknown"; job.FailureReason = "Provider calls blocked; original operation still requires reconciliation."; }
        else
        {
            op.State = "Rejected"; job.FailureReason = result.Error.ToString();
            if (result.Error is ProviderError.Unavailable or ProviderError.RateLimited)
            {
                c.Failures++; if (c.Failures >= 3) c.CircuitUntilUtc = DateTimeOffset.UtcNow.AddSeconds(30);
                job.State = job.AttemptCount < work.Snapshot.MaxAttempts && job.DeadlineUtc > DateTimeOffset.UtcNow ? "Queued" : "DeadLetter";
                job.ScheduledUtc = DateTimeOffset.UtcNow.AddMilliseconds(Math.Min(8000, 500 * (1 << job.AttemptCount)) + Random.Shared.Next(50, 200));
            }
            else job.State = job.CancelRequested ? "Cancelled" : "DeadLetter";
        }
        var workerActor = job.LeaseOwner ?? "runtime"; job.LeaseToken = null; job.LeaseOwner = null; job.LeaseUntilUtc = null;
        if (job.State is "Completed" or "Cancelled" or "DeadLetter") await SettleAsync(job, ct);
        AddAudit(workerActor, "attempt.finish:" + attempt.State, job.Id, work.TraceId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<string> SubmitAsync(Guid operation, Guid connection, string adapter, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var binding = await db.Operations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == operation, ct)
            ?? throw new RuntimeConflictException("Operation binding is unavailable.");
        if (binding.ConnectionId != connection || binding.AdapterKey != adapter) throw new RuntimeConflictException("Original resource binding does not match.");
        // Unique operation key is the provider's idempotency key, independent of worker/lease/attempt.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var job = await LockJobAsync(binding.JobId, ct);
        var provider = await db.Connections.FromSqlInterpolated($"SELECT * FROM [runtime].[Connections] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {connection}").SingleAsync(ct);
        if (!provider.Enabled || provider.Killed || job.CancelRequested || job.State != "Running" || job.LeaseUntilUtc <= DateTimeOffset.UtcNow)
            throw new RuntimeConflictException("Submission is blocked by current provider, cancellation or lease policy.");
        var resource = operation.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"IF NOT EXISTS (SELECT 1 FROM [runtime].[Effects] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {operation}) INSERT [runtime].[Effects] ([Id],[ConnectionId],[AdapterKey],[ResourceId],[CreatedUtc]) VALUES ({operation},{connection},{adapter},{resource},{DateTimeOffset.UtcNow})", ct);
        var result = await FindAsync(operation, connection, adapter, ct) ?? throw new RuntimeConflictException("Original resource binding does not match.");
        await tx.CommitAsync(ct); return result;
    }
    public Task<string?> FindAsync(Guid operation, Guid connection, string adapter, CancellationToken ct) => db.Effects.AsNoTracking()
        .Where(e => e.Id == operation && e.ConnectionId == connection && e.AdapterKey == adapter).Select(e => e.ResourceId).SingleOrDefaultAsync(ct);
}
