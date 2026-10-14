using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<TrendConnector> TrendConnectors => Set<TrendConnector>();
    public DbSet<TrendCandidate> TrendCandidates => Set<TrendCandidate>();
    public DbSet<TrendCluster> TrendClusters => Set<TrendCluster>();
    public DbSet<TrendTeaching> TrendTeachings => Set<TrendTeaching>();
    public DbSet<TrendStoryBrief> TrendStoryBriefs => Set<TrendStoryBrief>();
    public DbSet<TrendBinding> TrendBindings => Set<TrendBinding>();
    public DbSet<TrendOperation> TrendOperations => Set<TrendOperation>();
    public DbSet<TrendRemediation> TrendRemediations => Set<TrendRemediation>();
    private static readonly Type[] TrendTypes = [typeof(TrendConnector), typeof(TrendCandidate), typeof(TrendCluster), typeof(TrendTeaching), typeof(TrendStoryBrief), typeof(TrendBinding), typeof(TrendOperation), typeof(TrendRemediation)];
    private static void ConfigureTrends(ModelBuilder m)
    {
        foreach (var type in TrendTypes)
        {
            var e = m.Entity(type); e.HasBaseType((Type?)null); e.ToTable(type.Name, "trends"); e.HasKey("Id"); e.Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in e.Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(p.Name.EndsWith("Json") ? null : p.Name is "Dilemma" or "Reason" ? 2000 : p.Name == "ReviewEvidence" ? 1000 : 200);
            e.HasAlternateKey("Id", "UniverseId", "TenantId");
            e.HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
        }
        m.Entity<TrendConnector>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<TrendCluster>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key }).IsUnique();
        m.Entity<TrendCandidate>().HasOne<TrendConnector>().WithMany().HasForeignKey(x => new { x.ConnectorId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        foreach (var type in new[] { typeof(TrendCandidate), typeof(TrendTeaching), typeof(TrendStoryBrief), typeof(TrendRemediation) })
            m.Entity(type).HasOne(typeof(TrendCluster)).WithMany().HasForeignKey("ClusterId", "UniverseId", "TenantId").HasPrincipalKey("Id", "UniverseId", "TenantId").OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrendCluster>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.ClaimRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrendTeaching>().HasOne<LessonVariant>().WithMany().HasForeignKey(x => x.LessonVariantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrendStoryBrief>().HasOne<TrendTeaching>().WithMany().HasForeignKey(x => new { x.TeachingId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrendStoryBrief>().HasOne<FactoryPlan>().WithMany().HasForeignKey(x => x.FactoryPlanId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrendStoryBrief>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<TrendStoryBrief>().HasIndex(x => x.FactoryPlanId).IsUnique().HasFilter("[FactoryPlanId] IS NOT NULL");
        foreach (var type in new[] { typeof(TrendBinding), typeof(TrendRemediation) })
            m.Entity(type).HasOne(typeof(TrendStoryBrief)).WithMany().HasForeignKey("BriefId", "UniverseId", "TenantId").HasPrincipalKey("Id", "UniverseId", "TenantId").OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrendBinding>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.ContentRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrendBinding>().HasIndex(x => new { x.BriefId, x.ContentRevisionId }).IsUnique();
        m.Entity<TrendOperation>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateTrendChange(EntityEntry e)
    {
        if (!TrendTypes.Contains(e.Entity.GetType())) return;
        if (e.State == EntityState.Deleted) throw new InvalidOperationException("Trend evidence/history cannot be deleted.");
        if (e.State != EntityState.Modified) return;
        var allowed = e.Entity switch
        {
            TrendConnector => new[] { "Enabled" },
            TrendCluster => ["State", "Dilemma", "ClaimRevisionId", "CheckedUtc", "ExpiresUtc", "CheckedFingerprint", "ReviewEvidence"],
            TrendTeaching => ["ReviewStatus", "ReviewEvidence"],
            TrendStoryBrief when (string)e.Property("State").OriginalValue! == "Preparing" => ["FactoryPlanId", "SnapshotJson", "State", "ReviewEvidence"],
            TrendStoryBrief => ["State", "ReviewEvidence"],
            TrendRemediation => ["State", "EditorialCorrectionId"],
            _ => Array.Empty<string>()
        };
        if (e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("UpdatedUtc" or "RowVersion") && !allowed.Contains(p.Metadata.Name)))
            throw new InvalidOperationException("Trend bindings and evidence snapshots are immutable; append a version.");
    }
}
