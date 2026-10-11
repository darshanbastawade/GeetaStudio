using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<PromptRevision> PromptRevisions => Set<PromptRevision>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<DurableWorkflow> DurableWorkflows => Set<DurableWorkflow>();
    public DbSet<WorkflowCheckpoint> WorkflowCheckpoints => Set<WorkflowCheckpoint>();
    public DbSet<WorkflowDecision> WorkflowDecisions => Set<WorkflowDecision>();
    private static void ConfigureWorkflows(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(PromptRevision), typeof(WorkflowDefinition), typeof(DurableWorkflow), typeof(WorkflowCheckpoint), typeof(WorkflowDecision) })
        {
            m.Entity(type).HasBaseType((Type?)null); m.Entity(type).HasKey("Id"); m.Entity(type).ToTable(type.Name, "workflow");
            m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in m.Entity(type).Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
        }
        m.Entity<PromptRevision>().Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");
        m.Entity<WorkflowDefinition>().Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");
        m.Entity<DurableWorkflow>().Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        m.Entity<DurableWorkflow>().Property(x => x.InputsJson).HasColumnType("nvarchar(max)");
        m.Entity<WorkflowCheckpoint>().Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
        m.Entity<WorkflowDecision>().Property(x => x.EvidenceReference).HasMaxLength(1000);
        m.Entity<PromptRevision>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<WorkflowDefinition>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<DurableWorkflow>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<WorkflowCheckpoint>().HasIndex(x => new { x.RunId, x.Number }).IsUnique();
        m.Entity<WorkflowCheckpoint>().HasIndex(x => x.OperationId).IsUnique();
        m.Entity<WorkflowDecision>().HasIndex(x => new { x.RunId, x.OperationId }).IsUnique();
        m.Entity<PromptRevision>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkflowDefinition>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkflowDefinition>().HasOne<PromptRevision>().WithMany().HasForeignKey(x => x.PromptId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<DurableWorkflow>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<DurableWorkflow>().HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkflowCheckpoint>().HasOne<DurableWorkflow>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkflowCheckpoint>().HasOne<AiRun>().WithMany().HasForeignKey(x => x.AiRunId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkflowDecision>().HasOne<DurableWorkflow>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateWorkflowChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State != EntityState.Modified) return;
        if (e.Entity is PromptRevision or WorkflowDefinition or WorkflowDecision) throw new InvalidOperationException("Workflow artifacts and decisions are immutable.");
        string[] frozen = e.Entity switch
        {
            DurableWorkflow => ["TenantId", "UniverseId", "DefinitionId", "OperationId", "Actor", "Fingerprint", "SnapshotJson", "InputsJson", "DeadlineUtc"],
            WorkflowCheckpoint => ["RunId", "Number", "Kind", "OperationId"],
            _ => []
        };
        if (e.Properties.Any(p => p.IsModified && frozen.Contains(p.Metadata.Name))) throw new InvalidOperationException("Workflow snapshot/binding is immutable.");
        if (e.Entity is WorkflowCheckpoint && e.Property("State").OriginalValue as string == "Completed")
            throw new InvalidOperationException("Accepted checkpoints are immutable.");
    }
}
