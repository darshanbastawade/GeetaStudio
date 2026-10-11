using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed class RuntimeDbContext(DbContextOptions<RuntimeDbContext> options) : DbContext(options)
{
    public DbSet<ProviderDefinition> Definitions => Set<ProviderDefinition>();
    public DbSet<ProviderConnection> Connections => Set<ProviderConnection>();
    public DbSet<CapabilityProfile> Profiles => Set<CapabilityProfile>();
    public DbSet<RouteRevision> Revisions => Set<RouteRevision>();
    public DbSet<RouteHead> Routes => Set<RouteHead>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobAttempt> Attempts => Set<JobAttempt>();
    public DbSet<ExternalOperation> Operations => Set<ExternalOperation>();
    public DbSet<ProbeEffect> Effects => Set<ProbeEffect>();
    public DbSet<BudgetPolicy> Budgets => Set<BudgetPolicy>();
    public DbSet<BudgetReservation> Reservations => Set<BudgetReservation>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<Asset> Assets => Set<Asset>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("runtime");
        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(string)) property.SetMaxLength(256);
                if (property.ClrType == typeof(decimal)) { property.SetPrecision(18); property.SetScale(4); }
            }
        }
        model.Entity<Job>().Property(j => j.Snapshot).HasMaxLength(4000);
        model.Entity<Job>().HasIndex(j => new { j.State, j.ScheduledUtc, j.LeaseUntilUtc });
        model.Entity<Job>().HasIndex(j => new { j.ConnectionId, j.State, j.LeaseUntilUtc });
        model.Entity<JobAttempt>().HasIndex(a => new { a.JobId, a.Number }).IsUnique();
        model.Entity<ExternalOperation>().HasIndex(o => o.JobId).IsUnique();
        model.Entity<CapabilityProfile>().HasOne<ProviderConnection>().WithMany().HasForeignKey(p => p.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RouteRevision>().HasOne<CapabilityProfile>().WithMany().HasForeignKey(r => r.ProfileId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RouteHead>().HasOne<RouteRevision>().WithMany().HasForeignKey(r => r.RevisionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Job>().HasOne<ProviderConnection>().WithMany().HasForeignKey(j => j.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<JobAttempt>().HasOne<Job>().WithMany().HasForeignKey(a => a.JobId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<JobAttempt>().HasOne<AuditEntry>().WithMany().HasForeignKey(a => a.AuditId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ExternalOperation>().HasOne<Job>().WithMany().HasForeignKey(o => o.JobId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BudgetReservation>().HasOne<Job>().WithOne().HasForeignKey<BudgetReservation>(r => r.Id).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OutboxMessage>().HasOne<Job>().WithMany().HasForeignKey(o => o.JobId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ProviderDefinition>().HasData(RuntimeRules.Adapters.Select(key => new ProviderDefinition { Id = key }));
        model.Entity<ProviderDefinition>().HasData(AiRules.Adapters.Select(key => new ProviderDefinition { Id = key, Capability = key == "local.embedding.v1" ? "ai.embedding" : "ai.text", ApprovedEndpoint = key == "openai.text.v1" ? "approved:openai-chat-v1" : "local:ai-fixture", SupportsIdempotency = false, SupportsReconciliation = false, Qualification = "ProfileQualificationRequired" }));
        model.Entity<ProviderConnection>().HasOne<ProviderDefinition>().WithMany().HasForeignKey(c => c.AdapterKey).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BudgetPolicy>().HasData(new BudgetPolicy());
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var property in entry.Properties)
                if (property.CurrentValue is DateTimeOffset instant) property.CurrentValue = instant.ToUniversalTime();
            if (entry.Entity is RouteRevision or CapabilityProfile or ProviderDefinition && entry.State == EntityState.Modified)
                throw new InvalidOperationException("Revisions and capability profiles are immutable; create a new revision.");
            if (entry.Entity is Job && entry.State == EntityState.Modified && entry.Property(nameof(Job.Snapshot)).IsModified)
                throw new InvalidOperationException("Execution snapshots are immutable.");
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
