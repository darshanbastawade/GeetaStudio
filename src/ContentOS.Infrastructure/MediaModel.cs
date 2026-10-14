using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<MediaVersion> MediaVersions => Set<MediaVersion>();
    public DbSet<CharacterMediaPackage> MediaPackages => Set<CharacterMediaPackage>();
    public DbSet<MediaProfile> MediaProfiles => Set<MediaProfile>();
    public DbSet<MediaGeneration> MediaGenerations => Set<MediaGeneration>();
    public DbSet<MediaCommand> MediaCommands => Set<MediaCommand>();
    private static readonly Type[] MediaTypes = [typeof(MediaAsset), typeof(MediaVersion), typeof(CharacterMediaPackage), typeof(MediaProfile), typeof(MediaGeneration), typeof(MediaCommand)];
    private static void ConfigureMedia(ModelBuilder m)
    {
        foreach (var type in MediaTypes)
        {
            var e = m.Entity(type); e.ToTable(type.Name, "media"); e.HasKey("Id"); e.Property("RowVersion").IsRowVersion(); e.HasAlternateKey("Id", "UniverseId", "TenantId"); e.HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
            foreach (var p in e.Metadata.GetProperties().Where(x => x.ClrType == typeof(string))) p.SetMaxLength(p.Name.EndsWith("Json") ? -1 : p.Name is "ReviewEvidence" or "Outcome" ? 1000 : 200);
        }
        m.Entity<MediaAsset>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key }).IsUnique();
        m.Entity<MediaVersion>().HasIndex(x => new { x.AssetId, x.Version }).IsUnique();
        m.Entity<MediaVersion>().HasIndex(x => x.AssetReferenceId).IsUnique();
        m.Entity<MediaVersion>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Sha256, x.Length });
        m.Entity<MediaVersion>().HasOne<MediaAsset>().WithMany().HasForeignKey(x => new { x.AssetId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<MediaVersion>().HasOne<CoreAssetReference>().WithMany().HasForeignKey(x => x.AssetReferenceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CharacterMediaPackage>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<MediaProfile>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<MediaGeneration>().HasOne<MediaProfile>().WithMany().HasForeignKey(x => new { x.ProfileId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<MediaGeneration>().Property(x => x.ReservedCost).HasPrecision(18, 4); m.Entity<MediaGeneration>().Property(x => x.ActualCost).HasPrecision(18, 4);
        m.Entity<MediaGeneration>().HasIndex(x => new { x.State, x.LeaseUntilUtc });
        m.Entity<MediaCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateMediaChange(EntityEntry e)
    {
        if (!MediaTypes.Contains(e.Entity.GetType())) return;
        if (e.State == EntityState.Deleted) throw new InvalidOperationException("Media identity/history cannot be deleted.");
        if (e.State != EntityState.Modified) return;
        string[] allowed = e.Entity switch
        {
            MediaAsset => ["CurrentVersion"],
            MediaVersion => ["ReviewStatus", "ReviewEvidence", "ReviewJson"],
            CharacterMediaPackage => ["ReviewStatus", "ReviewEvidence", "ReviewJson", "PresentationRevisionId"],
            MediaProfile => ["Enabled"],
            MediaGeneration => ["State", "ExternalResourceId", "ResultSha256", "AcceptedVersionId", "ActualCost", "Outcome", "LeaseToken", "LeaseUntilUtc"],
            _ => []
        };
        if (e.Properties.Any(p => p.IsModified && p.Metadata.Name != "UpdatedUtc" && !allowed.Contains(p.Metadata.Name))) throw new InvalidOperationException("Media snapshots and bindings are immutable.");
    }
}
