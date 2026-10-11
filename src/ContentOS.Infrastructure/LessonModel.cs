using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<LessonProfile> LessonProfiles => Set<LessonProfile>();
    public DbSet<LessonBundle> LessonBundles => Set<LessonBundle>();
    public DbSet<LessonMemory> LessonMemories => Set<LessonMemory>();
    public DbSet<LessonVariant> LessonVariants => Set<LessonVariant>();
    public DbSet<LessonCommand> LessonCommands => Set<LessonCommand>();
    private static void ConfigureLessons(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(LessonProfile), typeof(LessonBundle), typeof(LessonMemory), typeof(LessonVariant), typeof(LessonCommand) })
        {
            m.Entity(type).HasBaseType((Type?)null); m.Entity(type).HasKey("Id"); m.Entity(type).ToTable(type.Name, "lessons");
            m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in m.Entity(type).Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
            m.Entity(type).HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
        }
        m.Entity<LessonProfile>().Property(x => x.RulesJson).HasColumnType("nvarchar(max)");
        m.Entity<LessonBundle>().Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        m.Entity<LessonMemory>().Property(x => x.EntryJson).HasColumnType("nvarchar(max)");
        m.Entity<LessonVariant>().Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        m.Entity<LessonVariant>().Property(x => x.SectionsJson).HasColumnType("nvarchar(max)");
        m.Entity<LessonProfile>().Property(x => x.Key).HasMaxLength(100);
        m.Entity<LessonProfile>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<LessonMemory>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<LessonVariant>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<LessonProfile>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<LessonCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<LessonBundle>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.AnchorClaimRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<LessonBundle>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.InterpretationClaimRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<LessonMemory>().HasOne<LessonBundle>().WithMany().HasForeignKey(x => x.BundleId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<LessonMemory>().HasOne<LessonProfile>().WithMany().HasForeignKey(x => x.LocaleProfileId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<LessonVariant>().HasOne<LessonBundle>().WithMany().HasForeignKey(x => x.BundleId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<LessonVariant>().HasOne<LessonVariant>().WithMany().HasForeignKey(x => x.ParentVariantId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateLessonChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State != EntityState.Modified) return;
        if (e.Entity is LessonCommand or LessonBundle) throw new InvalidOperationException("Lesson evidence and operations are immutable.");
        if (e.Entity is LessonProfile or LessonMemory or LessonVariant && e.Property("ReviewStatus").OriginalValue as string is "Approved" or "Rejected")
            throw new InvalidOperationException("Reviewed lesson artifacts are frozen; create a new revision.");
        if (e.Entity is LessonProfile or LessonMemory or LessonVariant &&
            e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("ReviewStatus" or "ReviewEvidence" or "UpdatedUtc" or "RowVersion")))
            throw new InvalidOperationException("Lesson profile, memory and variant revisions are immutable; create a linked revision.");
    }
}
