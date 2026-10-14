using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext
{
    public DbSet<CustomerUser> CustomerUsers => Set<CustomerUser>();
    public DbSet<CustomerIdentity> CustomerIdentities => Set<CustomerIdentity>();
    public DbSet<CustomerChallenge> CustomerChallenges => Set<CustomerChallenge>();
    public DbSet<CustomerHousehold> CustomerHouseholds => Set<CustomerHousehold>();
    public DbSet<CustomerMember> CustomerMembers => Set<CustomerMember>();
    public DbSet<CustomerInvite> CustomerInvites => Set<CustomerInvite>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<CustomerConsent> CustomerConsents => Set<CustomerConsent>();
    public DbSet<CustomerProgress> CustomerProgress => Set<CustomerProgress>();
    public DbSet<CustomerProduct> CustomerProducts => Set<CustomerProduct>();
    public DbSet<CustomerPaymentRoute> CustomerPaymentRoutes => Set<CustomerPaymentRoute>();
    public DbSet<CustomerPaymentBinding> CustomerPaymentBindings => Set<CustomerPaymentBinding>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
    public DbSet<CustomerPaymentEvent> CustomerPaymentEvents => Set<CustomerPaymentEvent>();
    public DbSet<CustomerCredit> CustomerCredits => Set<CustomerCredit>();
    public DbSet<CustomerReservation> CustomerReservations => Set<CustomerReservation>();
    public DbSet<CustomerNotification> CustomerNotifications => Set<CustomerNotification>();
    public DbSet<CustomerCommand> CustomerCommands => Set<CustomerCommand>();
    private static readonly Type[] CustomerTypes = [typeof(CustomerUser), typeof(CustomerIdentity), typeof(CustomerChallenge), typeof(CustomerHousehold), typeof(CustomerMember), typeof(CustomerInvite), typeof(CustomerProfile), typeof(CustomerConsent), typeof(CustomerProgress), typeof(CustomerProduct), typeof(CustomerPaymentRoute), typeof(CustomerPaymentBinding), typeof(CustomerOrder), typeof(CustomerPaymentEvent), typeof(CustomerCredit), typeof(CustomerReservation), typeof(CustomerNotification), typeof(CustomerCommand)];
    private static void ConfigureCustomers(ModelBuilder m)
    {
        foreach (var type in CustomerTypes)
        {
            var e = m.Entity(type); e.HasBaseType((Type?)null); e.ToTable(type.Name, "customers"); e.HasKey("Id"); e.Property("RowVersion").IsRowVersion();
            e.HasAlternateKey("Id", "TenantId"); e.HasOne(typeof(Tenant)).WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
            foreach (var property in e.Metadata.GetProperties().Where(x => x.ClrType == typeof(string))) property.SetMaxLength(property.Name.EndsWith("Json") ? -1 : 200);
            if (e.Metadata.FindProperty("UserId") != null) e.HasOne(typeof(CustomerUser)).WithMany().HasForeignKey("UserId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
            if (e.Metadata.FindProperty("HouseholdId") != null) e.HasOne(typeof(CustomerHousehold)).WithMany().HasForeignKey("HouseholdId", "TenantId").HasPrincipalKey("Id", "TenantId").OnDelete(DeleteBehavior.Restrict);
        }
        m.Entity<CustomerUser>().HasIndex(x => new { x.TenantId, x.LoginHash }).IsUnique();
        m.Entity<CustomerIdentity>().HasIndex(x => new { x.TenantId, x.Provider, x.Account, x.Environment, x.SubjectHash }).IsUnique();
        m.Entity<CustomerMember>().HasIndex(x => new { x.TenantId, x.HouseholdId, x.UserId }).IsUnique();
        m.Entity<CustomerProgress>().HasIndex(x => new { x.TenantId, x.ProfileId, x.Key }).IsUnique();
        foreach (var t in new[] { typeof(CustomerProfile), typeof(CustomerReservation) }) m.Entity(t).HasAlternateKey("Id", "UserId", "TenantId");
        m.Entity<CustomerProgress>().HasOne<CustomerProfile>().WithMany().HasForeignKey(x => new { x.ProfileId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerConsent>().HasOne<CustomerProfile>().WithMany().HasForeignKey(x => new { x.ProfileId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerReservation>().HasOne<CustomerProfile>().WithMany().HasForeignKey(x => new { x.ProfileId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerOrder>().HasAlternateKey(x => new { x.Id, x.UserId, x.TenantId });
        m.Entity<CustomerPaymentBinding>().HasAlternateKey(x => new { x.Id, x.UserId, x.TenantId });
        m.Entity<CustomerCredit>().HasOne<CustomerOrder>().WithMany().HasForeignKey(x => new { x.OrderId, x.UserId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UserId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerPaymentRoute>().HasIndex(x => x.TenantId).IsUnique().HasFilter("[Active] = 1");
        m.Entity<CustomerProduct>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerProduct>().HasIndex(x => new { x.TenantId, x.Key, x.Version }).IsUnique();
        m.Entity<CustomerPaymentRoute>().HasIndex(x => new { x.TenantId, x.Key, x.Version }).IsUnique();
        m.Entity<CustomerPaymentBinding>().HasIndex(x => new { x.TenantId, x.UserId, x.Provider, x.Account, x.Environment }).IsUnique();
        m.Entity<CustomerOrder>().HasOne<CustomerProduct>().WithMany().HasForeignKey(x => new { x.ProductId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerOrder>().HasOne<CustomerPaymentRoute>().WithMany().HasForeignKey(x => new { x.RouteId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerOrder>().HasOne<CustomerPaymentBinding>().WithMany().HasForeignKey(x => new { x.BindingId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerPaymentEvent>().HasOne<CustomerOrder>().WithMany().HasForeignKey(x => new { x.OrderId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerPaymentEvent>().HasIndex(x => new { x.TenantId, x.Provider, x.Account, x.Environment, x.EventId }).IsUnique();
        m.Entity<CustomerPaymentEvent>().HasIndex(x => new { x.TenantId, x.OrderId, x.Sequence }).IsUnique();
        m.Entity<CustomerReservation>().HasIndex(x => new { x.TenantId, x.UserId, x.UsageKey }).IsUnique();
        m.Entity<CustomerCredit>().HasAlternateKey(x => new { x.Id, x.UserId, x.TenantId });
        m.Entity<CustomerCredit>().HasOne<CustomerCredit>().WithMany().HasForeignKey(x => new { x.LotId, x.UserId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UserId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerCredit>().HasOne<CustomerReservation>().WithMany().HasForeignKey(x => new { x.ReservationId, x.UserId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.UserId, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerNotification>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        m.Entity<CustomerNotification>().HasOne<CustomerOrder>().WithMany().HasForeignKey(x => new { x.OrderId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CustomerCommand>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
    }
    private static void ValidateCustomerChange(EntityEntry e)
    {
        if (!CustomerTypes.Contains(e.Entity.GetType())) return;
        if (e.State == EntityState.Modified && e.Entity is CustomerOrder && e.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("State" or "ExternalReference" or "SubscriptionReference" or "Sequence" or "RefundedMinor" or "EndsUtc" or "PaidUtc" or "Lease" or "LeaseEndsUtc" or "RequestedAction" or "UpdatedUtc"))) throw new InvalidOperationException("Original customer/order/catalog/provider binding is immutable.");
        if (e.State == EntityState.Modified && e.Entity is CustomerProfile && e.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("Alias" or "Locale" or "Personalization" or "Deleted" or "UpdatedUtc"))) throw new InvalidOperationException("Profile identity, household and age band are immutable.");
        if (e.State == EntityState.Modified && e.Entity is CustomerMember && e.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("Role" or "Enabled" or "UpdatedUtc"))) throw new InvalidOperationException("Household membership identity is immutable.");
        if (e.State == EntityState.Modified && e.Entity is CustomerReservation && e.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("State" or "Provider" or "Account" or "Resource" or "Consumed" or "OutcomeHash" or "UpdatedUtc"))) throw new InvalidOperationException("Reservation amount, expiry, owner and usage binding are immutable.");
        if (e.State == EntityState.Modified && e.Entity is CustomerIdentity && e.Property("Enabled").OriginalValue is false && e.Property("Enabled").CurrentValue is true) throw new InvalidOperationException("Disabled identity cannot be reactivated.");
        if (e.State == EntityState.Deleted && e.Entity is not ContentOS.Domain.CustomerProgress) throw new InvalidOperationException("Retain minimal customer audit/financial evidence; erase profile fields through privacy controls.");
        if (e.State == EntityState.Modified && e.Entity is CustomerCredit or CustomerPaymentEvent or CustomerProduct or CustomerConsent or CustomerCommand or CustomerPaymentBinding) throw new InvalidOperationException("Customer ledger/catalog/evidence is append-only.");
        if (e.State == EntityState.Modified && e.Entity is CustomerPaymentRoute && e.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("Enabled" or "Active" or "UpdatedUtc"))) throw new InvalidOperationException("Append a payment route version; original bindings cannot change.");
        if (e.State == EntityState.Modified && e.Entity is CustomerIdentity && e.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("Enabled" or "UpdatedUtc"))) throw new InvalidOperationException("Identity bindings cannot be reassigned.");
    }
}
