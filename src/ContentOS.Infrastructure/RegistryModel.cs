using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<RegistryRevision> RegistryRevisions => Set<RegistryRevision>();
    public DbSet<RegistryEpisode> RegistryEpisodes => Set<RegistryEpisode>();
    public DbSet<RegistryOperation> RegistryOperations => Set<RegistryOperation>();
    private static void ConfigureRegistry(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(RegistryRevision), typeof(RegistryEpisode), typeof(RegistryOperation) })
        {
            m.Entity(type).HasBaseType((Type?)null); m.Entity(type).ToTable(type.Name, "registry"); m.Entity(type).HasKey("Id"); m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in m.Entity(type).Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
            m.Entity(type).HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
        }
        m.Entity<RegistryRevision>().Property(x => x.RulesJson).HasColumnType("nvarchar(max)");
        m.Entity<RegistryRevision>().Property(x => x.ReviewEvidence).HasMaxLength(1000); m.Entity<RegistryRevision>().Property(x => x.EscalationEvidence).HasMaxLength(1000);
        m.Entity<RegistryRevision>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Kind, x.Key, x.Version }).IsUnique();
        m.Entity<RegistryRevision>().HasOne<Character>().WithMany().HasForeignKey(x => new { x.CharacterId, x.UniverseId }).HasPrincipalKey(x => new { x.Id, x.UniverseId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CharacterRevision>().HasAlternateKey(x => new { x.Id, x.CharacterId });
        m.Entity<RegistryRevision>().HasOne<CharacterRevision>().WithMany().HasForeignKey(x => new { x.CoreRevisionId, x.CharacterId }).HasPrincipalKey(x => new { x.Id, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RegistryRevision>().HasOne<Character>().WithMany().HasForeignKey(x => new { x.OtherCharacterId, x.UniverseId }).HasPrincipalKey(x => new { x.Id, x.UniverseId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RegistryEpisode>().HasAlternateKey(x => new { x.Id, x.UniverseId, x.TenantId });
        m.Entity<RegistryRevision>().HasOne<RegistryEpisode>().WithMany().HasForeignKey(x => new { x.EpisodeId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RegistryEpisode>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key }).IsUnique();
        m.Entity<RegistryOperation>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateRegistryChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State == EntityState.Deleted && e.Entity is RegistryRevision or RegistryEpisode or RegistryOperation) throw new InvalidOperationException("Registry history cannot be deleted.");
        if (e.State != EntityState.Modified) return;
        if (e.Entity is RegistryOperation) throw new InvalidOperationException("Registry operations are immutable.");
        if (e.Entity is RegistryRevision && (e.Property("ReviewStatus").OriginalValue as string is "Approved" or "Rejected" ||
            e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("ReviewStatus" or "ReviewEvidence" or "EscalationEvidence" or "UpdatedUtc" or "RowVersion"))))
            throw new InvalidOperationException("Registry revisions are immutable; create a new reviewed version.");
    }
}
