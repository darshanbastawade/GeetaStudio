using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<ReadCollection> ReadCollections => Set<ReadCollection>();
    public DbSet<ReadPiece> ReadPieces => Set<ReadPiece>();
    public DbSet<ReadEvidence> ReadEvidenceEntries => Set<ReadEvidence>();
    public DbSet<ReadOperation> ReadOperations => Set<ReadOperation>();
    public DbSet<ReadEventCount> ReadEventCounts => Set<ReadEventCount>();
    private static void ConfigureRead(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(ReadCollection), typeof(ReadPiece), typeof(ReadEvidence), typeof(ReadOperation), typeof(ReadEventCount) })
        {
            var e = m.Entity(type); e.HasBaseType((Type?)null); e.ToTable(type.Name, "reader"); e.HasKey("Id"); e.Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in e.Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
        }
        foreach (var type in new[] { typeof(ReadCollection), typeof(ReadPiece), typeof(ReadEvidence), typeof(ReadOperation) })
        {
            m.Entity(type).HasAlternateKey("Id", "UniverseId", "TenantId");
            m.Entity(type).HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
        }
        m.Entity<ReadCollection>().Property(x => x.Rationale).HasMaxLength(2000);
        m.Entity<ReadCollection>().HasIndex(x => new { x.Key, x.Version }).IsUnique();
        m.Entity<ReadCollection>().HasIndex(x => x.Key).IsUnique().HasFilter("[State] = 'Active'");
        m.Entity<ReadCollection>().HasOne<CorpusManifest>().WithMany().HasForeignKey(x => x.ManifestId).OnDelete(DeleteBehavior.Restrict);
        foreach (var type in new[] { typeof(ReadPiece), typeof(ReadEvidence) })
            m.Entity(type).HasOne(typeof(ReadCollection)).WithMany().HasForeignKey("CollectionId", "UniverseId", "TenantId").HasPrincipalKey("Id", "UniverseId", "TenantId").OnDelete(DeleteBehavior.Restrict);
        m.Entity<ReadPiece>().HasIndex(x => new { x.CollectionId, x.EditorialItemId }).IsUnique();
        m.Entity<ReadPiece>().HasOne<EditorialItem>().WithMany().HasForeignKey(x => new { x.EditorialItemId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ReadPiece>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ReadPiece>().HasOne<CorpusVerse>().WithMany().HasForeignKey(x => x.CorpusVerseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ReadPiece>().HasOne<LessonVariant>().WithMany().HasForeignKey(x => x.LessonVariantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ReadEvidence>().Property(x => x.Reference).HasMaxLength(1000); m.Entity<ReadEvidence>().Property(x => x.Notes).HasMaxLength(2000);
        m.Entity<ReadOperation>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<ReadEventCount>().HasIndex(x => new { x.CollectionId, x.PieceId, x.Day, x.Version, x.Name }).IsUnique();
        m.Entity<ReadEventCount>().HasOne<ReadPiece>().WithMany().HasForeignKey(x => x.PieceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ReadEventCount>().HasOne<ReadCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateReadChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State == EntityState.Deleted && e.Entity is ReadCollection or ReadPiece or ReadEvidence or ReadOperation) throw new InvalidOperationException("Read publication history cannot be deleted.");
        if (e.State == EntityState.Modified && e.Entity is ReadPiece or ReadEvidence or ReadOperation) throw new InvalidOperationException("Read bindings/evidence/operations are immutable.");
        if (e.State == EntityState.Modified && e.Entity is ReadCollection && e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("State" or "UpdatedUtc" or "RowVersion")))
            throw new InvalidOperationException("Append an explicit collection version.");
    }
}
