using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<EditorialAttempt> EditorialAttempts => Set<EditorialAttempt>();
    public DbSet<EditorialItem> EditorialItems => Set<EditorialItem>();
    public DbSet<EditorialApproval> EditorialApprovals => Set<EditorialApproval>();
    public DbSet<EditorialAsset> EditorialAssets => Set<EditorialAsset>();
    public DbSet<EditorialCopy> EditorialCopies => Set<EditorialCopy>();
    public DbSet<EditorialCorrectionRecord> EditorialCorrections => Set<EditorialCorrectionRecord>();
    public DbSet<EditorialCorrectionTask> EditorialCorrectionTasks => Set<EditorialCorrectionTask>();
    public DbSet<EditorialOperation> EditorialOperations => Set<EditorialOperation>();
    public DbSet<EditorialHistory> EditorialHistories => Set<EditorialHistory>();
    public DbSet<EditorialTrend> EditorialTrends => Set<EditorialTrend>();
    private static void ConfigureEditorial(ModelBuilder m)
    {
        foreach (var t in new[] { typeof(EditorialItem), typeof(EditorialApproval), typeof(EditorialAsset), typeof(EditorialCopy), typeof(EditorialCorrectionRecord), typeof(EditorialCorrectionTask), typeof(EditorialOperation), typeof(EditorialAttempt), typeof(EditorialHistory), typeof(EditorialTrend) })
        {
            var e = m.Entity(t); e.HasBaseType((Type?)null); e.ToTable(t.Name, "editorial"); e.HasKey("Id"); e.Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in e.Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
            e.HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
            e.HasAlternateKey("Id", "UniverseId", "TenantId");
        }
        m.Entity<EditorialItem>().Property(x => x.Brief).HasMaxLength(4000);
        m.Entity<EditorialApproval>().Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        m.Entity<EditorialTrend>().Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        foreach (var t in new[] { typeof(EditorialApproval), typeof(EditorialAsset), typeof(EditorialCorrectionRecord), typeof(EditorialCorrectionTask), typeof(EditorialAttempt), typeof(EditorialHistory) })
            m.Entity(t).Property<string>("EvidenceReference").HasMaxLength(1000);
        m.Entity<EditorialTrend>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<EditorialCorrectionRecord>().Property(x => x.Reason).HasMaxLength(2000); m.Entity<EditorialCorrectionTask>().Property(x => x.Reason).HasMaxLength(2000);
        m.Entity<EditorialCopy>().Property(x => x.Reference).HasMaxLength(1000);
        m.Entity<EditorialItem>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key }).IsUnique();
        m.Entity<EditorialItem>().HasIndex(x => x.ContentId).IsUnique();
        m.Entity<EditorialItem>().HasOne<ContentItem>().WithMany().HasForeignKey(x => new { x.ContentId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        foreach (var t in new[] { typeof(EditorialApproval), typeof(EditorialAsset), typeof(EditorialCopy), typeof(EditorialHistory) })
            m.Entity(t).HasOne(typeof(EditorialItem)).WithMany().HasForeignKey("ItemId", "UniverseId", "TenantId").HasPrincipalKey("Id", "UniverseId", "TenantId").OnDelete(DeleteBehavior.Restrict);
        foreach (var t in new[] { typeof(EditorialApproval), typeof(EditorialAsset), typeof(EditorialCopy), typeof(EditorialCorrectionTask) })
            m.Entity(t).HasOne(typeof(ContentRevision)).WithMany().HasForeignKey("RevisionId").OnDelete(DeleteBehavior.Restrict);
        m.Entity<EditorialAsset>().HasOne<CoreAssetReference>().WithMany().HasForeignKey(x => x.AssetReferenceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EditorialCorrectionTask>().HasOne<EditorialCorrectionRecord>().WithMany().HasForeignKey(x => new { x.CorrectionId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EditorialCorrectionTask>().HasOne<ContentItem>().WithMany().HasForeignKey(x => new { x.ContentId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EditorialCorrectionTask>().HasOne<EditorialCopy>().WithMany().HasForeignKey(x => new { x.CopyId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EditorialAttempt>().HasOne<EditorialCorrectionTask>().WithMany().HasForeignKey(x => new { x.TaskId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EditorialAttempt>().HasOne<ContentItem>().WithMany().HasForeignKey(x => new { x.ReplacementContentId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EditorialOperation>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateEditorialChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State == EntityState.Deleted && e.Entity is EditorialAttempt or EditorialItem or EditorialApproval or EditorialAsset or EditorialCopy or EditorialCorrectionRecord or EditorialCorrectionTask or EditorialOperation or EditorialHistory or EditorialTrend) throw new InvalidOperationException("Editorial history cannot be deleted.");
        if (e.State == EntityState.Modified && e.Entity is EditorialAttempt or EditorialApproval or EditorialAsset or EditorialCorrectionRecord or EditorialOperation or EditorialHistory) throw new InvalidOperationException("Editorial evidence/history immutable.");
    }
}
