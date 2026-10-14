using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<TalkPolicy> TalkPolicies => Set<TalkPolicy>();
    public DbSet<TalkSession> TalkSessions => Set<TalkSession>();
    public DbSet<TalkTurn> TalkTurns => Set<TalkTurn>();
    public DbSet<TalkProblem> TalkProblems => Set<TalkProblem>();
    private static void ConfigureTalk(ModelBuilder m)
    {
        foreach (var t in new[] { typeof(TalkPolicy), typeof(TalkSession), typeof(TalkTurn), typeof(TalkProblem) })
        {
            var e = m.Entity(t); e.HasBaseType((Type?)null); e.ToTable(t.Name, "talk"); e.HasKey("Id"); e.Property("RowVersion").IsRowVersion(); e.HasAlternateKey("Id", "TenantId");
            e.HasOne(typeof(Tenant)).WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
            foreach (var p in e.Metadata.GetProperties().Where(x => x.ClrType == typeof(string))) p.SetMaxLength(p.Name.EndsWith("Json") || p.Name is "Question" or "Answer" ? -1 : 200);
        }
        m.Entity<TalkPolicy>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TalkPolicy>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<TalkPolicy>().HasIndex(x => new { x.TenantId, x.UniverseId, x.Key, x.Version }).IsUnique();
        m.Entity<TalkSession>().HasOne<CustomerProfile>().WithMany().HasForeignKey(x => new { x.ProfileId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TalkSession>().HasOne<CustomerUser>().WithMany().HasForeignKey(x => new { x.UserId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TalkSession>().HasOne<TalkPolicy>().WithMany().HasForeignKey(x => new { x.PolicyId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TalkSession>().HasIndex(x => new { x.TenantId, x.UserId, x.OperationId }).IsUnique();
        m.Entity<TalkTurn>().HasOne<TalkSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TalkTurn>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<TalkTurn>().HasOne<CustomerReservation>().WithMany().HasForeignKey(x => new { x.ReservationId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TalkTurn>().Property(x => x.ProviderCost).HasPrecision(18, 8);
        m.Entity<TalkProblem>().HasOne<TalkTurn>().WithMany().HasForeignKey(x => new { x.TurnId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TalkProblem>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateTalkChange(EntityEntry e)
    {
        if (e.Entity is not (TalkPolicy or TalkSession or TalkTurn or TalkProblem)) return;
        if (e.State == EntityState.Deleted) throw new InvalidOperationException("Redact conversation content; preserve accounting/evidence metadata.");
        if (e.State != EntityState.Modified) return;
        var allowed = e.Entity switch
        {
            TalkPolicy => new[] { "Enabled", "UpdatedUtc" },
            TalkSession => ["Remember", "State", "UpdatedUtc"],
            TalkTurn => ["State", "Outcome", "ReservationId", "AiRunId", "RouteId", "Question", "Answer", "SourcesJson", "Credits", "ProviderCost", "Currency", "UpdatedUtc"],
            _ => ["UpdatedUtc"]
        };
        if (e.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name))) throw new InvalidOperationException("TALK ownership, authority and original operation are immutable.");
    }
}
