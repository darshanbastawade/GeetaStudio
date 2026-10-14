using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<OptimizationExperiment> OptimizationExperiments => Set<OptimizationExperiment>();
    public DbSet<OptimizationEvent> OptimizationEvents => Set<OptimizationEvent>();
    public DbSet<OptimizationAutomationRun> OptimizationAutomationRuns => Set<OptimizationAutomationRun>();
    private static readonly Type[] OptimizationTypes = [typeof(OptimizationExperiment), typeof(OptimizationEvent), typeof(OptimizationAutomationRun)];
    private static void ConfigureOptimization(ModelBuilder m)
    {
        foreach (var t in OptimizationTypes)
        {
            var e = m.Entity(t); e.HasBaseType((Type?)null); e.ToTable(t.Name, "optimization"); e.HasKey("Id"); e.Property("RowVersion").IsRowVersion();
            e.HasAlternateKey("Id", "UniverseId", "TenantId");
            e.HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
            foreach (var p in e.Metadata.GetProperties().Where(x => x.ClrType == typeof(string))) p.SetMaxLength(p.Name.EndsWith("Json") ? -1 : 200);
        }
        m.Entity<OptimizationExperiment>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<OptimizationExperiment>().Property(x => x.ReservedCost).HasPrecision(18, 6);
        m.Entity<OptimizationEvent>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<OptimizationEvent>().HasOne<OptimizationExperiment>().WithMany().HasForeignKey(x => new { x.ExperimentId, x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UniverseId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OptimizationAutomationRun>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateOptimizationChange(EntityEntry e)
    {
        if (!OptimizationTypes.Contains(e.Entity.GetType())) return;
        if (e.State == EntityState.Deleted) throw new InvalidOperationException("Optimization evidence cannot be deleted.");
        var allowed = e.Entity is OptimizationExperiment ? new[] { "State", "Dispatches", "ReservedCost", "UpdatedUtc" } : [];
        if (e.State == EntityState.Modified && e.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name))) throw new InvalidOperationException("Optimization configuration and decision evidence are immutable.");
    }
}
