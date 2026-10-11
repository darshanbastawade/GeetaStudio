using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<SourceWork> SourceWorks => Set<SourceWork>();
    public DbSet<SourceParty> SourceParties => Set<SourceParty>();
    public DbSet<SourceEdition> SourceEditions => Set<SourceEdition>();
    public DbSet<SourcePermission> SourcePermissions => Set<SourcePermission>();
    public DbSet<PermissionLanguage> PermissionLanguages => Set<PermissionLanguage>();
    public DbSet<PermissionTerritory> PermissionTerritories => Set<PermissionTerritory>();
    public DbSet<PermissionFormat> PermissionFormats => Set<PermissionFormat>();
    public DbSet<SourceSnapshot> SourceSnapshots => Set<SourceSnapshot>();
    public DbSet<ProvenanceEdge> Provenance => Set<ProvenanceEdge>();
    public DbSet<RightsDecision> RightsDecisions => Set<RightsDecision>();
    public DbSet<SourceUse> SourceUses => Set<SourceUse>();
    public DbSet<SourceRevocation> SourceRevocations => Set<SourceRevocation>();
    public DbSet<RemediationTask> Remediation => Set<RemediationTask>();
    public DbSet<SourceCommand> SourceCommands => Set<SourceCommand>();
    public DbSet<SourceAudit> SourceAudits => Set<SourceAudit>();
    private static void ConfigureSources(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(SourceWork), typeof(SourceParty), typeof(SourceEdition), typeof(SourcePermission), typeof(SourceSnapshot),
            typeof(ProvenanceEdge), typeof(RightsDecision), typeof(SourceUse), typeof(SourceRevocation), typeof(RemediationTask), typeof(SourceCommand), typeof(SourceAudit) })
        {
            m.Entity(type).HasBaseType((Type?)null);
            m.Entity(type).HasKey("Id");
            m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion();
            m.Entity(type).ToTable(type.Name, "sources");
        }
        m.Entity<PermissionLanguage>().ToTable("PermissionLanguage", "sources").HasKey(x => new { x.PermissionId, x.Value });
        m.Entity<PermissionTerritory>().ToTable("PermissionTerritory", "sources").HasKey(x => new { x.PermissionId, x.Value });
        m.Entity<PermissionFormat>().ToTable("PermissionFormat", "sources").HasKey(x => new { x.PermissionId, x.Value });
        m.Entity<SourceUse>().Property(x => x.ExternalReference).HasMaxLength(1000);
        m.Entity<SourceSnapshot>().Property(x => x.Text).HasMaxLength(65536);
        m.Entity<SourcePermission>().Property(x => x.RequiredAttribution).HasMaxLength(1000);
        m.Entity<SourcePermission>().Property(x => x.EvidenceReference).HasMaxLength(1000);
        m.Entity<SourceSnapshot>().Property(x => x.EvidenceReference).HasMaxLength(1000);
        m.Entity<SourceEdition>().Property(x => x.Locator).HasMaxLength(1000);
        m.Entity<SourceEdition>().Property(x => x.CitationFormat).HasMaxLength(1000);
        m.Entity<SourceRevocation>().Property(x => x.Reason).HasMaxLength(1000);
        m.Entity<RemediationTask>().Property(x => x.ResolutionEvidence).HasMaxLength(1000);
        m.Entity<SourceWork>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key }).IsUnique();
        m.Entity<SourceParty>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        m.Entity<SourceEdition>().HasIndex(x => new { x.WorkId, x.Key }).IsUnique();
        m.Entity<SourceEdition>().HasIndex(x => new { x.WorkId, x.Version }).IsUnique();
        m.Entity<SourcePermission>().HasIndex(x => new { x.EditionId, x.Version }).IsUnique();
        m.Entity<SourceSnapshot>().HasIndex(x => new { x.EditionId, x.Sha256 }).IsUnique();
        m.Entity<SourceCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<SourceUse>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<RemediationTask>().HasIndex(x => new { x.RevocationId, x.SourceUseId }).IsUnique();
        m.Entity<ProvenanceEdge>().HasIndex(x => x.ContentRevisionId);
        m.Entity<ProvenanceEdge>().HasIndex(x => x.ParentRevisionId);
        m.Entity<ProvenanceEdge>().HasIndex(x => x.SnapshotId);
        m.Entity<SourceWork>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceParty>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceEdition>().HasOne<SourceWork>().WithMany().HasForeignKey(x => x.WorkId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceEdition>().HasOne<SourceParty>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceEdition>().HasOne<SourceParty>().WithMany().HasForeignKey(x => x.TranslatorId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceEdition>().HasOne<SourceParty>().WithMany().HasForeignKey(x => x.PublisherId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceEdition>().HasOne<SourcePermission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceEdition>().HasOne<ContentLocale>().WithMany().HasForeignKey(x => x.Language).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourcePermission>().HasOne<SourceEdition>().WithMany().HasForeignKey(x => x.EditionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PermissionLanguage>().HasOne<SourcePermission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PermissionTerritory>().HasOne<SourcePermission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PermissionFormat>().HasOne<SourcePermission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceSnapshot>().HasOne<SourceEdition>().WithMany().HasForeignKey(x => x.EditionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceSnapshot>().HasOne<SourcePermission>().WithMany().HasForeignKey(x => x.ImportPermissionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ProvenanceEdge>().HasOne<SourceSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ProvenanceEdge>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.ParentRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ProvenanceEdge>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.ContentRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ProvenanceEdge>().HasOne<CoreAssetReference>().WithMany().HasForeignKey(x => x.AssetReferenceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RightsDecision>().HasOne<SourceSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RightsDecision>().HasOne<SourcePermission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceUse>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.ContentRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceRevocation>().HasOne<SourceWork>().WithMany().HasForeignKey(x => x.WorkId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceRevocation>().HasOne<SourceEdition>().WithMany().HasForeignKey(x => x.EditionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RemediationTask>().HasOne<SourceRevocation>().WithMany().HasForeignKey(x => x.RevocationId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RemediationTask>().HasOne<SourceUse>().WithMany().HasForeignKey(x => x.SourceUseId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateSourceChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.State != EntityState.Modified) return;
        string[]? allowed = entry.Entity switch
        {
            SourceWork => ["Revoked"],
            SourceEdition => ["PermissionId", "Revoked"],
            SourcePermission => ["Status", "ReviewedBy", "ReviewedUtc"],
            SourceSnapshot => ["ReviewStatus", "ReviewedBy", "ReviewedUtc"],
            RemediationTask => ["Status", "ResolutionEvidence"],
            SourceParty or ProvenanceEdge or RightsDecision or SourceUse or SourceRevocation or SourceCommand or SourceAudit or PermissionLanguage or PermissionTerritory or PermissionFormat => [],
            _ => null
        };
        if (allowed is not null && entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("UpdatedUtc" or "RowVersion") && !allowed.Contains(p.Metadata.Name)))
            throw new InvalidOperationException("Source editions, evidence, permission terms and provenance history are immutable; create a new version.");
    }
}


