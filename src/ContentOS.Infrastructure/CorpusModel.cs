using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<CorpusManifest> CorpusManifests => Set<CorpusManifest>();
    public DbSet<CorpusLocator> CorpusLocators => Set<CorpusLocator>();
    public DbSet<CorpusVerse> CorpusVerses => Set<CorpusVerse>();
    public DbSet<CorpusText> CorpusTexts => Set<CorpusText>();
    public DbSet<CorpusAnnotation> CorpusAnnotations => Set<CorpusAnnotation>();
    public DbSet<CorpusCommand> CorpusCommands => Set<CorpusCommand>();
    public DbSet<CorpusSourceBinding> CorpusBindings => Set<CorpusSourceBinding>();
    private static void ConfigureCorpus(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(CorpusManifest), typeof(CorpusVerse), typeof(CorpusText), typeof(CorpusAnnotation), typeof(CorpusCommand) })
        {
            m.Entity(type).HasBaseType((Type?)null); m.Entity(type).HasKey("Id");
            m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion(); m.Entity(type).ToTable(type.Name, "corpus");
        }
        m.Entity<CorpusLocator>().ToTable("CorpusLocator", "corpus").HasKey(x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey });
        m.Entity<CorpusSourceBinding>().ToTable("CorpusSourceBinding", "corpus").HasKey(x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey, x.Kind, x.Locale });
        m.Entity<CorpusSourceBinding>().Property(x => x.VariantKey).HasMaxLength(200);
        m.Entity<CorpusSourceBinding>().Property(x => x.Kind).HasMaxLength(40);
        m.Entity<CorpusSourceBinding>().Property(x => x.Locale).HasMaxLength(40);
        m.Entity<CorpusSourceBinding>().Property(x => x.Sha256).HasMaxLength(64);
        m.Entity<CorpusSourceBinding>().Property(x => x.SourceLocator).HasMaxLength(1000);
        m.Entity<CorpusSourceBinding>().HasOne<CorpusLocator>().WithMany().HasForeignKey(x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusSourceBinding>().HasOne<SourceSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusLocator>().Property(x => x.Note).HasMaxLength(1000);
        m.Entity<CorpusManifest>().Property(x => x.Edition).HasMaxLength(1000);
        m.Entity<CorpusText>().Property(x => x.ExactText).HasMaxLength(16000);
        m.Entity<CorpusText>().Property(x => x.SearchText).HasMaxLength(16000);
        m.Entity<CorpusText>().Property(x => x.SourceLocator).HasMaxLength(1000);
        m.Entity<CorpusAnnotation>().Property(x => x.Text).HasMaxLength(16000);
        m.Entity<CorpusVerse>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<CorpusAnnotation>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<CorpusManifest>().HasIndex(x => new { x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<CorpusVerse>().HasIndex(x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey }).IsUnique();
        m.Entity<CorpusText>().HasIndex(x => new { x.VerseId, x.Kind, x.Locale }).IsUnique();
        m.Entity<CorpusAnnotation>().HasIndex(x => new { x.VerseId, x.Key }).IsUnique();
        m.Entity<CorpusCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<CorpusManifest>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusLocator>().HasOne<CorpusManifest>().WithMany().HasForeignKey(x => x.ManifestId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusVerse>().HasOne<CorpusLocator>().WithMany().HasForeignKey(x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusText>().HasOne<CorpusVerse>().WithMany().HasForeignKey(x => x.VerseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusText>().HasOne<SourceSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusText>().HasOne<ContentLocale>().WithMany().HasForeignKey(x => x.Locale).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusAnnotation>().HasOne<CorpusVerse>().WithMany().HasForeignKey(x => x.VerseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusAnnotation>().HasOne<SourceSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CorpusAnnotation>().HasOne<ContentLocale>().WithMany().HasForeignKey(x => x.Locale).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateCorpusChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.State != EntityState.Modified) return;
        string[]? allowed = entry.Entity switch
        {
            CorpusVerse or CorpusAnnotation => ["ReviewStatus", "Reviewer", "ReviewEvidence"],
            CorpusManifest or CorpusLocator or CorpusSourceBinding or CorpusText or CorpusCommand => [],
            _ => null
        };
        if (allowed != null && entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("UpdatedUtc" or "RowVersion") && !allowed.Contains(p.Metadata.Name)))
            throw new InvalidOperationException("Corpus source text and inventory are immutable; register a new manifest version.");
    }
}
