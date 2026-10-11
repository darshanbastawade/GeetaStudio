using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<KnowledgeConcept> KnowledgeConcepts => Set<KnowledgeConcept>();
    public DbSet<KnowledgeRelation> KnowledgeRelations => Set<KnowledgeRelation>();
    public DbSet<KnowledgeIndex> KnowledgeIndices => Set<KnowledgeIndex>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<KnowledgeAlias> KnowledgeAliases => Set<KnowledgeAlias>();
    public DbSet<KnowledgeCommand> KnowledgeCommands => Set<KnowledgeCommand>();
    private static void ConfigureKnowledge(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(KnowledgeConcept), typeof(KnowledgeRelation), typeof(KnowledgeIndex), typeof(KnowledgeChunk), typeof(KnowledgeAlias), typeof(KnowledgeCommand) })
        {
            m.Entity(type).HasBaseType((Type?)null); m.Entity(type).HasKey("Id"); m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion(); m.Entity(type).ToTable(type.Name, "knowledge");
            foreach (var p in m.Entity(type).Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
        }
        m.Entity<KnowledgeConcept>().Property(x => x.TermsJson).HasColumnType("nvarchar(max)");
        m.Entity<KnowledgeConcept>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<KnowledgeRelation>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<KnowledgeIndex>().Property(x => x.GraphJson).HasColumnType("nvarchar(max)");
        m.Entity<KnowledgeIndex>().Property(x => x.MaterialJson).HasColumnType("nvarchar(max)");
        m.Entity<KnowledgeIndex>().Property(x => x.ValidationJson).HasColumnType("nvarchar(max)");
        m.Entity<KnowledgeIndex>().Property(x => x.ValidationEvidence).HasMaxLength(1000);
        m.Entity<KnowledgeChunk>().Property(x => x.VectorJson).HasColumnType("nvarchar(max)");
        m.Entity<KnowledgeChunk>().Property(x => x.FeaturesJson).HasColumnType("nvarchar(max)");
        m.Entity<KnowledgeConcept>().HasIndex(x => new { x.UniverseId, x.Key }).IsUnique();
        m.Entity<KnowledgeIndex>().HasIndex(x => new { x.UniverseId, x.Key }).IsUnique();
        m.Entity<KnowledgeIndex>().HasIndex(x => x.State);
        m.Entity<KnowledgeAlias>().HasIndex(x => new { x.UniverseId, x.Locale, x.Key }).IsUnique();
        m.Entity<KnowledgeCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<KnowledgeChunk>().HasIndex(x => new { x.IndexId, x.StableChunkId }).IsUnique();
        m.Entity<KnowledgeConcept>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeConcept>().HasOne<SourceSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeConcept>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.ClaimRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeRelation>().HasOne<KnowledgeConcept>().WithMany().HasForeignKey(x => x.FromId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeRelation>().HasOne<KnowledgeConcept>().WithMany().HasForeignKey(x => x.ToId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeRelation>().HasOne<SourceSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeRelation>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.ClaimRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeIndex>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeIndex>().HasOne<CorpusManifest>().WithMany().HasForeignKey(x => x.ManifestId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeChunk>().HasOne<KnowledgeIndex>().WithMany().HasForeignKey(x => x.IndexId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeChunk>().HasOne<CorpusText>().WithMany().HasForeignKey(x => x.CorpusTextId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeAlias>().HasOne<Universe>().WithMany().HasForeignKey(x => x.UniverseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeAlias>().HasOne<KnowledgeIndex>().WithMany().HasForeignKey(x => x.ActiveIndexId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<KnowledgeAlias>().HasOne<KnowledgeIndex>().WithMany().HasForeignKey(x => x.PreviousIndexId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateKnowledgeChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State != EntityState.Modified) return;
        string[]? allowed = e.Entity switch
        {
            KnowledgeConcept or KnowledgeRelation => ["ReviewStatus", "Reviewer", "ReviewEvidence"],
            KnowledgeIndex => ["State", "Error", "ValidationEvidence", "ValidationJson"],
            KnowledgeAlias => ["ActiveIndexId", "PreviousIndexId"],
            KnowledgeChunk or KnowledgeCommand => [],
            _ => null
        };
        if (allowed != null && e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("UpdatedUtc" or "RowVersion") && !allowed.Contains(p.Metadata.Name))) throw new InvalidOperationException("Knowledge graph, embedding spaces and chunks are immutable.");
    }
}
