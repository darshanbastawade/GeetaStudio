using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<EvidenceRubric> EvidenceRubrics => Set<EvidenceRubric>();
    public DbSet<EvidenceClaim> Claims => Set<EvidenceClaim>();
    public DbSet<ClaimRevision> ClaimRevisions => Set<ClaimRevision>();
    public DbSet<ClaimEvidenceSpan> ClaimSpans => Set<ClaimEvidenceSpan>();
    public DbSet<ClaimDisagreement> ClaimDisagreements => Set<ClaimDisagreement>();
    public DbSet<ClaimContentLink> ClaimLinks => Set<ClaimContentLink>();
    public DbSet<ClaimCorrection> ClaimCorrections => Set<ClaimCorrection>();
    public DbSet<EvidenceCommand> EvidenceCommands => Set<EvidenceCommand>();
    private static void ConfigureEvidence(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(EvidenceRubric), typeof(EvidenceClaim), typeof(ClaimRevision), typeof(ClaimEvidenceSpan), typeof(ClaimDisagreement), typeof(ClaimContentLink), typeof(ClaimCorrection), typeof(EvidenceCommand) })
        {
            m.Entity(type).HasBaseType((Type?)null); m.Entity(type).HasKey("Id");
            m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion(); m.Entity(type).ToTable(type.Name, "evidence");
            foreach (var p in m.Entity(type).Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
        }
        m.Entity<EvidenceRubric>().Property(x => x.Description).HasMaxLength(2000);
        m.Entity<ClaimRevision>().Property(x => x.Statement).HasMaxLength(16000);
        m.Entity<ClaimRevision>().Property(x => x.ReviewEvidence).HasMaxLength(1000);
        m.Entity<ClaimEvidenceSpan>().Property(x => x.ExactText).HasMaxLength(16000);
        m.Entity<ClaimEvidenceSpan>().Property(x => x.Locator).HasMaxLength(1000);
        m.Entity<ClaimCorrection>().Property(x => x.Reason).HasMaxLength(1000);
        m.Entity<ClaimCorrection>().Property(x => x.EvidenceReference).HasMaxLength(1000);
        m.Entity<ClaimDisagreement>().Property(x => x.Reason).HasMaxLength(1000);
        m.Entity<ClaimDisagreement>().Property(x => x.EvidenceReference).HasMaxLength(1000);
        m.Entity<EvidenceRubric>().HasIndex(x => new { x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<EvidenceClaim>().HasIndex(x => new { x.UniverseId, x.Key }).IsUnique();
        m.Entity<ClaimRevision>().HasIndex(x => new { x.ClaimId, x.Version }).IsUnique();
        m.Entity<EvidenceCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<ClaimContentLink>().HasIndex(x => new { x.ContentRevisionId, x.Start }).IsUnique();
        m.Entity<EvidenceRubric>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EvidenceClaim>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimRevision>().HasOne<EvidenceClaim>().WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimRevision>().HasOne<EvidenceRubric>().WithMany().HasForeignKey(x => x.RubricId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimEvidenceSpan>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.ClaimRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimEvidenceSpan>().HasOne<SourceSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimEvidenceSpan>().HasOne<CorpusText>().WithMany().HasForeignKey(x => x.CorpusTextId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimContentLink>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.ClaimRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimContentLink>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.ContentRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimDisagreement>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.LeftRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimDisagreement>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.RightRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimCorrection>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.ClaimRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClaimCorrection>().HasOne<ClaimRevision>().WithMany().HasForeignKey(x => x.ReplacementRevisionId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateEvidenceChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State != EntityState.Modified) return;
        string[]? allowed = e.Entity switch
        {
            EvidenceClaim => ["CurrentVersion"],
            ClaimRevision => ["ReviewStatus", "Quality", "Outcome", "Reviewer", "ReviewEvidence"],
            ClaimCorrection => ["Status", "ReplacementRevisionId"],
            EvidenceRubric or ClaimEvidenceSpan or ClaimDisagreement or ClaimContentLink or EvidenceCommand => [],
            _ => null
        };
        if (allowed != null && e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("UpdatedUtc" or "RowVersion") && !allowed.Contains(p.Metadata.Name)))
            throw new InvalidOperationException("Claim evidence/history is immutable; submit a superseding version.");
    }
}
