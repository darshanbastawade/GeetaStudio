using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<FactoryPlan> FactoryPlans => Set<FactoryPlan>(); public DbSet<FactoryBatch> FactoryBatches => Set<FactoryBatch>(); public DbSet<FactoryWork> FactoryWorks => Set<FactoryWork>();
    public DbSet<FactoryArtifact> FactoryArtifacts => Set<FactoryArtifact>(); public DbSet<FactoryOperation> FactoryOperations => Set<FactoryOperation>();
    private static void ConfigureFactory(ModelBuilder m)
    {
        foreach (var t in new[] { typeof(FactoryPlan), typeof(FactoryBatch), typeof(FactoryWork), typeof(FactoryArtifact), typeof(FactoryOperation) })
        {
            m.Entity(t).HasBaseType((Type?)null); m.Entity(t).ToTable(t.Name, "factory"); m.Entity(t).HasKey("Id"); m.Entity(t).Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in m.Entity(t).Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
            m.Entity(t).HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
        }
        m.Entity<FactoryPlan>().Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)"); m.Entity<FactoryPlan>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<FactoryArtifact>().Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)"); m.Entity<FactoryArtifact>().Property(x => x.Treatment).HasColumnType("nvarchar(max)"); m.Entity<FactoryArtifact>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<FactoryBatch>().Property(x => x.BudgetJson).HasMaxLength(4000); m.Entity<FactoryBatch>().Property(x => x.GenerationCost).HasPrecision(18, 4);
        m.Entity<FactoryPlan>().HasAlternateKey(x => new { x.Id, x.UniverseId, x.TenantId }); m.Entity<FactoryBatch>().HasAlternateKey(x => new { x.Id, x.UniverseId, x.TenantId });
        m.Entity<FactoryArtifact>().HasAlternateKey(x => new { x.Id, x.UniverseId, x.TenantId });
        m.Entity<FactoryBatch>().HasOne<FactoryPlan>().WithMany().HasForeignKey(x => new { x.PlanId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<FactoryWork>().HasOne<FactoryBatch>().WithMany().HasForeignKey(x => new { x.BatchId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<FactoryWork>().HasOne<FactoryArtifact>().WithMany().HasForeignKey(x => new { x.ArtifactId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<FactoryArtifact>().HasOne<FactoryPlan>().WithMany().HasForeignKey(x => new { x.PlanId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ContentRevision>().HasAlternateKey(x => new { x.Id, x.ContentId });
        m.Entity<FactoryArtifact>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => new { x.CoreRevisionId, x.CoreContentId }).HasPrincipalKey(x => new { x.Id, x.ContentId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<LessonVariant>().HasAlternateKey(x => new { x.Id, x.UniverseId, x.TenantId });
        m.Entity<ContentItem>().HasAlternateKey(x => new { x.Id, x.UniverseId, x.TenantId });
        m.Entity<FactoryArtifact>().HasOne<ContentItem>().WithMany().HasForeignKey(x => new { x.CoreContentId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<FactoryArtifact>().HasOne<LessonVariant>().WithMany().HasForeignKey(x => new { x.LessonVariantId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<FactoryArtifact>().HasOne<FactoryArtifact>().WithMany().HasForeignKey(x => new { x.ParentArtifactId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<FactoryPlan>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<FactoryBatch>().HasIndex(x => new { x.TenantId, x.UniverseId, x.BusinessKey }).IsUnique();
        m.Entity<FactoryWork>().HasIndex(x => new { x.BatchId, x.BriefKey, x.Format }).IsUnique();
        m.Entity<FactoryWork>().HasIndex(x => x.WorkflowId).IsUnique().HasFilter("[WorkflowId] IS NOT NULL");
        m.Entity<FactoryWork>().HasOne<DurableWorkflow>().WithMany().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<FactoryArtifact>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Fingerprint }).IsUnique();
        m.Entity<FactoryOperation>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateFactoryChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State == EntityState.Deleted && e.Entity is FactoryPlan or FactoryBatch or FactoryWork or FactoryArtifact or FactoryOperation) throw new InvalidOperationException("Factory history cannot be deleted.");
        if (e.State != EntityState.Modified) return;
        if (e.Entity is FactoryWork && e.Property("WorkflowId").IsModified && e.Property("WorkflowId").OriginalValue != null) throw new InvalidOperationException("Factory child workflow is immutable.");
        if (e.Entity is FactoryOperation) throw new InvalidOperationException("Factory operation is immutable.");
        if (e.Entity is FactoryPlan or FactoryArtifact && (e.Property("ReviewStatus").OriginalValue as string is "Approved" or "Rejected" || e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("ReviewStatus" or "ReviewEvidence" or "UpdatedUtc" or "RowVersion")))) throw new InvalidOperationException("Factory reviewed inputs and artifacts are immutable; append a version.");
    }
}
