using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<CreationTemplate> CreationTemplates => Set<CreationTemplate>();
    public DbSet<PrivateCreation> Creations => Set<PrivateCreation>();
    public DbSet<CreationRevision> CreationRevisions => Set<CreationRevision>();
    public DbSet<CreationUpload> CreationUploads => Set<CreationUpload>();
    public DbSet<CreationCommand> CreationCommands => Set<CreationCommand>();
    public DbSet<CreationEvent> CreationEvents => Set<CreationEvent>();
    private static readonly Type[] CreationTypes = [typeof(CreationTemplate), typeof(PrivateCreation), typeof(CreationRevision), typeof(CreationUpload), typeof(CreationCommand), typeof(CreationEvent)];
    private static void ConfigureCreations(ModelBuilder m)
    {
        foreach (var t in CreationTypes)
        {
            var e = m.Entity(t); e.HasBaseType((Type?)null); e.ToTable(t.Name, "create"); e.HasKey("Id"); e.Property("RowVersion").IsRowVersion(); e.HasAlternateKey("Id", "TenantId"); e.HasOne(typeof(Tenant)).WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
            foreach (var p in e.Metadata.GetProperties().Where(x => x.ClrType == typeof(string))) p.SetMaxLength(p.Name.EndsWith("Json") || p.Name == "Html" ? -1 : 200);
        }
        m.Entity<CreationTemplate>().HasIndex(x => new { x.TenantId, x.Key, x.Version }).IsUnique(); m.Entity<CreationTemplate>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<CreationTemplate>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PrivateCreation>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique(); m.Entity<CreationUpload>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique(); m.Entity<CreationCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<CreationRevision>().HasIndex(x => new { x.CreationId, x.Version }).IsUnique();
        m.Entity<PrivateCreation>().Property(x => x.DeadlineUtc).HasDefaultValueSql("DATEADD(minute, 10, SYSDATETIMEOFFSET())");
        m.Entity<PrivateCreation>().HasOne<CreationTemplate>().WithMany().HasForeignKey(x => new { x.TemplateId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PrivateCreation>().HasOne<CustomerReservation>().WithMany().HasForeignKey(x => new { x.ReservationId, x.UserId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UserId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        foreach (var t in new[] { typeof(PrivateCreation), typeof(CreationUpload) }) { m.Entity(t).HasOne(typeof(CustomerUser)).WithMany().HasForeignKey("UserId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict); m.Entity(t).HasOne(typeof(CustomerProfile)).WithMany().HasForeignKey("ProfileId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict); }
        foreach (var t in new[] { typeof(CreationRevision), typeof(CreationCommand), typeof(CreationEvent) }) m.Entity(t).HasOne(typeof(PrivateCreation)).WithMany().HasForeignKey("CreationId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
    }
    private static void ValidateCreationChange(EntityEntry e)
    {
        if (!CreationTypes.Contains(e.Entity.GetType())) return;
        if (e.State == EntityState.Deleted) throw new InvalidOperationException("Use private creation erasure; retain minimal receipts.");
        if (e.State != EntityState.Modified) return;
        string[] allowed = e.Entity switch { CreationTemplate => ["Enabled", "UpdatedUtc"], PrivateCreation => ["InputJson", "UploadsJson", "State", "Outcome", "Version", "Attempts", "ReservationId", "Lease", "LeaseUntilUtc", "ChargedCredits", "Review", "PublicEligible", "Cleanup", "UpdatedUtc"], CreationRevision => ["Html", "UpdatedUtc"], CreationUpload => ["Content", "AltText", "Deleted", "UpdatedUtc"], _ => [] };
        if (e.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name))) throw new InvalidOperationException("Creation ownership, original provider, versions and receipts are immutable.");
        if (e.Entity is CreationRevision && e.Property("Html").IsModified && e.Property("Html").CurrentValue != null) throw new InvalidOperationException("Append a creation revision; only privacy redaction can change old content.");
    }
}
