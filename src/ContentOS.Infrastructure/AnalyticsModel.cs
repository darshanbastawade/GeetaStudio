using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<AnalyticsSource> AnalyticsSources => Set<AnalyticsSource>();
    public DbSet<AnalyticsDefinition> AnalyticsDefinitions => Set<AnalyticsDefinition>();
    public DbSet<AnalyticsFx> AnalyticsRates => Set<AnalyticsFx>();
    public DbSet<AnalyticsConsent> AnalyticsConsents => Set<AnalyticsConsent>();
    public DbSet<AnalyticsEntry> AnalyticsEntries => Set<AnalyticsEntry>();
    public DbSet<AnalyticsForecast> AnalyticsForecasts => Set<AnalyticsForecast>();
    public DbSet<AnalyticsCommand> AnalyticsCommands => Set<AnalyticsCommand>();
    private static readonly Type[] AnalyticsTypes = [typeof(AnalyticsSource), typeof(AnalyticsDefinition), typeof(AnalyticsFx), typeof(AnalyticsConsent), typeof(AnalyticsEntry), typeof(AnalyticsForecast), typeof(AnalyticsCommand)];
    private static void ConfigureAnalytics(ModelBuilder m)
    {
        foreach (var t in AnalyticsTypes)
        {
            var e = m.Entity(t); e.HasBaseType((Type?)null); e.ToTable(t.Name, "analytics"); e.HasKey("Id"); e.Property("RowVersion").IsRowVersion();
            e.HasAlternateKey("Id", "UniverseId", "TenantId");
            e.HasOne(typeof(Universe)).WithMany().HasForeignKey("UniverseId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
            foreach (var p in e.Metadata.GetProperties().Where(x => x.ClrType == typeof(string))) p.SetMaxLength(p.Name.EndsWith("Json") ? -1 : 200);
            if (e.Metadata.FindProperty("Version") != null) e.HasIndex("TenantId", "UniverseId", "Key", "Version").IsUnique();
        }
        m.Entity<AnalyticsEntry>().Property(x => x.ReportingAmount).HasPrecision(28, 8);
        m.Entity<AnalyticsEntry>().HasIndex(x => new { x.TenantId, x.UniverseId, x.SourceSystem, x.AccountReference, x.EventId }).IsUnique();
        m.Entity<AnalyticsEntry>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Kind, x.EconomicKey }).IsUnique();
        m.Entity<AnalyticsEntry>().HasIndex(x => new { x.TenantId, x.UniverseId, x.OccurredUtc, x.CreatedUtc });
        m.Entity<AnalyticsCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<AnalyticsConsent>().HasIndex(x => new { x.TenantId, x.UniverseId, x.SubjectReference, x.Purpose }).IsUnique();
        foreach (var pair in new[] { (typeof(AnalyticsSource), "SourceId"), (typeof(AnalyticsDefinition), "DefinitionId"), (typeof(AnalyticsFx), "FxId"), (typeof(AnalyticsConsent), "ConsentId"), (typeof(AnalyticsEntry), "RelatedEntryId"), (typeof(SocialPublication), "PublicationId") })
            m.Entity<AnalyticsEntry>().HasOne(pair.Item1).WithMany().HasForeignKey(pair.Item2, "UniverseId", "TenantId").HasPrincipalKey("Id", "UniverseId", "TenantId").OnDelete(DeleteBehavior.Restrict);
        m.Entity<AnalyticsEntry>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateAnalyticsChange(EntityEntry e)
    {
        if (!AnalyticsTypes.Contains(e.Entity.GetType())) return;
        if (e.State == EntityState.Deleted) throw new InvalidOperationException("Analytics evidence cannot be deleted.");
        if (e.State != EntityState.Modified) return;
        var allowed = e.Entity is AnalyticsSource or AnalyticsConsent ? new[] { "Enabled", "UpdatedUtc" } : [];
        if (e.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name))) throw new InvalidOperationException("Analytics history is immutable; append a new version.");
        if (e.Entity is AnalyticsConsent && e.Property("Enabled").OriginalValue is false && e.Property("Enabled").CurrentValue is true) throw new InvalidOperationException("Revoked consent cannot be reactivated.");
    }
}
