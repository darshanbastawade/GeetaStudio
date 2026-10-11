using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<EvaluationDataset> EvaluationDatasets => Set<EvaluationDataset>();
    public DbSet<EvaluationCandidate> EvaluationCandidates => Set<EvaluationCandidate>();
    public DbSet<EvaluationRun> EvaluationRuns => Set<EvaluationRun>();
    public DbSet<EvaluationReview> EvaluationReviews => Set<EvaluationReview>();
    public DbSet<EvaluationAdvice> EvaluationAdvices => Set<EvaluationAdvice>();
    public DbSet<EvaluationOperation> EvaluationOperations => Set<EvaluationOperation>();
    private static void ConfigureEvaluation(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(EvaluationDataset), typeof(EvaluationCandidate), typeof(EvaluationRun), typeof(EvaluationReview), typeof(EvaluationAdvice), typeof(EvaluationOperation) })
        {
            var e = m.Entity(type); e.HasBaseType((Type?)null); e.ToTable(type.Name, "evaluation"); e.HasKey("Id");
            e.Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in e.Metadata.GetProperties()) if (p.ClrType == typeof(string)) p.SetMaxLength(200);
            if (type != typeof(EvaluationOperation)) e.Property<string>("SnapshotJson").HasColumnType("nvarchar(max)");
            e.HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
            e.HasAlternateKey("Id", "UniverseId", "TenantId");
        }
        m.Entity<EvaluationDataset>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<EvaluationCandidate>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<EvaluationOperation>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<EvaluationRun>().HasOne<EvaluationDataset>().WithMany().HasForeignKey(x => new { x.DatasetId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EvaluationRun>().HasOne<EvaluationCandidate>().WithMany().HasForeignKey(x => new { x.CandidateId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EvaluationRun>().HasOne<EvaluationRun>().WithMany().HasForeignKey(x => new { x.BaselineRunId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EvaluationReview>().HasOne<EvaluationDataset>().WithMany().HasForeignKey(x => new { x.DatasetId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EvaluationReview>().HasOne<EvaluationRun>().WithMany().HasForeignKey(x => new { x.RunId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EvaluationAdvice>().HasOne<EvaluationRun>().WithMany().HasForeignKey(x => new { x.RunId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EvaluationReview>().HasIndex(x => new { x.DatasetId, x.CaseKey, x.RunId, x.Mode }).IsUnique();
    }
    private static void ValidateEvaluationChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.Entity is EvaluationDataset or EvaluationCandidate or EvaluationRun or EvaluationReview or EvaluationAdvice or EvaluationOperation && e.State is EntityState.Modified or EntityState.Deleted)
            throw new InvalidOperationException("Evaluation snapshots/reviews are immutable; append a version or new run.");
    }
}
