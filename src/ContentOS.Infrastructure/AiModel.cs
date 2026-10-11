using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<AiProfile> AiProfiles => Set<AiProfile>();
    public DbSet<AiRoute> AiRoutes => Set<AiRoute>();
    public DbSet<AiRouteHead> AiRouteHeads => Set<AiRouteHead>();
    public DbSet<AiBudget> AiBudgets => Set<AiBudget>();
    public DbSet<AiRun> AiRuns => Set<AiRun>();
    public DbSet<AiAttempt> AiAttempts => Set<AiAttempt>();
    private static void ConfigureAi(ModelBuilder m)
    {
        foreach (var type in new[] { typeof(AiProfile), typeof(AiRoute), typeof(AiRouteHead), typeof(AiBudget), typeof(AiRun), typeof(AiAttempt) })
        {
            m.Entity(type).HasBaseType((Type?)null); m.Entity(type).HasKey("Id"); m.Entity(type).ToTable(type.Name, "ai");
            m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion();
            foreach (var p in m.Entity(type).Metadata.GetProperties())
            {
                if (p.ClrType == typeof(string)) p.SetMaxLength(200);
                if (p.ClrType == typeof(decimal)) { p.SetPrecision(20); p.SetScale(8); }
            }
        }
        m.Entity<AiProfile>().Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");
        m.Entity<AiRoute>().Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");
        m.Entity<AiRun>().Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        m.Entity<AiRun>().Property(x => x.OutputJson).HasColumnType("nvarchar(max)");
        m.Entity<AiAttempt>().Property(x => x.ReconciliationEvidence).HasMaxLength(1000);
        m.Entity<AiProfile>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiRoute>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiRouteHead>().HasOne<AiRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiBudget>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiRun>().HasOne<AiBudget>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiRun>().HasOne<AiRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiRun>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiAttempt>().HasOne<AiRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiAttempt>().HasOne<AiProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AiRun>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<AiAttempt>().HasIndex(x => new { x.RunId, x.Number }).IsUnique();
        m.Entity<AiRouteHead>().HasIndex(x => new { x.TenantId, x.Task }).IsUnique();
        m.Entity<AiProfile>().HasIndex(x => x.ConnectionId).IsUnique();
        m.Entity<AiBudget>().HasIndex(x => new { x.TenantId, x.Capability, x.Currency, x.StartsUtc });
    }
    private static void ValidateAiChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        if (e.State != EntityState.Modified) return;
        if (e.Entity is AiProfile or AiRoute) throw new InvalidOperationException("AI profiles and route versions are immutable.");
        string[] frozen = e.Entity switch
        {
            AiRun => ["TenantId", "UniverseId", "OperationId", "Actor", "Fingerprint", "SnapshotJson", "BudgetId", "RouteId", "DeadlineUtc"],
            AiAttempt => ["RunId", "ProfileId", "ConnectionId", "Number", "Ceiling"],
            AiBudget => ["TenantId", "Capability", "Currency", "StartsUtc", "EndsUtc", "Limit"],
            AiRouteHead => ["TenantId", "Task"],
            _ => []
        };
        if (e.Properties.Any(p => p.IsModified && frozen.Contains(p.Metadata.Name))) throw new InvalidOperationException("Pinned AI execution/budget identity is immutable.");
    }
}
