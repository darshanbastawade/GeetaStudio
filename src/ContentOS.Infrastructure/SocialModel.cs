using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>(); public DbSet<SocialPackage> SocialPackages => Set<SocialPackage>(); public DbSet<SocialPublication> SocialPublications => Set<SocialPublication>(); public DbSet<SocialCommand> SocialCommands => Set<SocialCommand>(); public DbSet<SocialEffect> SocialEffects => Set<SocialEffect>(); public DbSet<SocialHistory> SocialHistory => Set<SocialHistory>(); public DbSet<SocialCallback> SocialCallbacks => Set<SocialCallback>(); public DbSet<SocialNotification> SocialNotifications => Set<SocialNotification>();
    private static readonly Type[] SocialTypes = [typeof(SocialAccount), typeof(SocialPackage), typeof(SocialPublication), typeof(SocialCommand), typeof(SocialEffect), typeof(SocialHistory), typeof(SocialCallback), typeof(SocialNotification)];
    private static void ConfigureSocial(ModelBuilder m)
    {
        foreach (var t in SocialTypes) { var e = m.Entity(t); e.HasBaseType((Type?)null); e.ToTable(t.Name, "social"); e.HasKey("Id"); e.Property("RowVersion").IsRowVersion(); e.HasAlternateKey("Id", "UniverseId", "TenantId"); e.HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict); foreach (var p in e.Metadata.GetProperties().Where(x => x.ClrType == typeof(string))) p.SetMaxLength(p.Name.EndsWith("Json") ? -1 : p.Name == "Detail" ? 2000 : 200); }
        m.Entity<SocialAccount>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<SocialPackage>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key }).IsUnique(); m.Entity<SocialPackage>().HasOne<SocialAccount>().WithMany().HasForeignKey(x => new { x.AccountId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SocialPackage>().HasOne<EditorialItem>().WithMany().HasForeignKey(x => new { x.EditorialItemId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict); m.Entity<SocialPackage>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SocialPublication>().HasOne<SocialPackage>().WithMany().HasForeignKey(x => new { x.PackageId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict); m.Entity<SocialPublication>().HasIndex(x => x.PackageId).IsUnique(); m.Entity<SocialPublication>().HasIndex(x => new { x.State, x.NextUtc, x.LeaseUntilUtc });
        foreach (var t in new[] { typeof(SocialHistory), typeof(SocialEffect), typeof(SocialCallback), typeof(SocialNotification) }) m.Entity(t).HasOne(typeof(SocialPublication)).WithMany().HasForeignKey("PublicationId", "UniverseId", "TenantId").HasPrincipalKey("Id", "UniverseId", "TenantId").OnDelete(DeleteBehavior.Restrict);
        m.Entity<SocialEffect>().HasIndex(x => x.PublicationId).IsUnique(); m.Entity<SocialCallback>().HasIndex(x => new { x.TenantId, x.EventId }).IsUnique(); m.Entity<SocialCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateSocialChange(EntityEntry e)
    {
        if (!SocialTypes.Contains(e.Entity.GetType())) return;
        if (e.State == EntityState.Deleted) throw new InvalidOperationException("Publishing history cannot be deleted.");
        if (e.State != EntityState.Modified) return;
        string[] allowed = e.Entity switch { SocialAccount => ["Enabled", "TokenState"], SocialPackage => ["ReviewStatus", "ReviewJson"], SocialPublication => ["State", "Attempts", "LeaseToken", "LeaseUntilUtc", "NextUtc", "RemoteId", "RemoteUrl", "Outcome", "EditorialCopyId"], SocialNotification => ["Acknowledged"], _ => [] };
        if (e.Entity is SocialPublication && e.Property("State").OriginalValue?.ToString() == "Draft") allowed = allowed.Concat(new[] { "Actor", "ScheduledUtc", "Timezone", "DeadlineUtc", "MaxAttempts" }).ToArray();
        if (e.Properties.Any(p => p.IsModified && p.Metadata.Name != "UpdatedUtc" && !allowed.Contains(p.Metadata.Name))) throw new InvalidOperationException("Publishing payload/destination/history is immutable; append and review a new package.");
        if (e.Entity is SocialPublication && e.Property("RemoteId").OriginalValue != null && e.Property("RemoteId").IsModified) throw new InvalidOperationException("Original remote binding is immutable.");
    }
}
