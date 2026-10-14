using System.Diagnostics.Metrics;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ContentOS.Application.CustomerRules;
namespace ContentOS.Infrastructure;

public sealed partial class CustomerWorkbench(CoreDbContext db, IEnumerable<IIdentityProvider> identities, IEnumerable<IPaymentProvider> payments, IEnumerable<INotificationProvider> notifications, TalkMemory? talkMemory = null) : ICustomerWorkbench
{
    private static readonly Meter Meter = new("ContentOS.Customers", "1.0");
    private static readonly Counter<long> Events = Meter.CreateCounter<long>("customers.commands");
    private static string Actor(CustomerScope s) { Require(s.Principal.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(s.Principal.Identity.Name), "Authenticated account required."); return s.Principal.Identity!.Name!.ToUpperInvariant(); }
    private static string Login(CustomerScope s) => Hash(s.TenantId + ":" + Actor(s));
    private static string Subject(Guid tenant, CustomerVerifiedIdentity v) => Hash(Json(new { tenant, v.Provider, v.Account, v.Environment, v.Subject }));
    private async Task Admin(CoreScope s, CancellationToken ct) { if (!await db.Members.AnyAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor && x.Permission == "Administrator", ct)) throw new CoreAccessException(); }
    private async Task<CustomerUser> Self(CustomerScope s, CancellationToken ct)
    {
        var host = await identities.Single(x => x.Key == "identity.windows.v1").VerifyAsync(s.Principal, "local-windows", Guid.Empty, null, ct);
        var hash = Subject(s.TenantId, host); var binding = await db.CustomerIdentities.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Provider == host.Provider && x.Account == host.Account && x.SubjectHash == hash && x.Enabled, ct);
        if (binding == null) throw new CoreAccessException(); return await User(s.TenantId, binding.UserId, ct);
    }
    private async Task<CustomerUser> User(Guid tenant, Guid user, CancellationToken ct) => await db.CustomerUsers.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == user && x.State == "Active", ct) ?? throw new CoreAccessException();
    private async Task<Guid> Command(Guid tenant, string actor, Guid op, string kind, object request, Func<Task<Guid>> act, CancellationToken ct)
    {
        Require(op != Guid.Empty, "Stable customer operation ID required."); db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Customers:" + tenant},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Customer lock unavailable',1;", ct);
            var actorHash = Hash(tenant + ":" + actor); var fingerprint = Hash(Json(request));
            var old = await db.CustomerCommands.SingleOrDefaultAsync(x => x.TenantId == tenant && x.OperationId == op, ct);
            if (old != null) { if (old.ActorHash != actorHash || old.Kind != kind || old.Fingerprint != fingerprint) throw new RuntimeConflictException("Customer operation replay differs."); return Read<Guid>(old.ResultJson); }
            var result = await act(); db.CustomerCommands.Add(new() { Id = Guid.NewGuid(), TenantId = tenant, OperationId = op, ActorHash = actorHash, Kind = kind, Fingerprint = fingerprint, ResultJson = Json(result) });
            db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = tenant, SubjectId = result, Actor = actorHash, Action = "customers." + kind });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new RuntimeConflictException("Customer state changed; reload before editing."); }
            await tx.CommitAsync(ct); Events.Add(1, new KeyValuePair<string, object?>("kind", kind)); return result;
        }
        catch { try { await tx.RollbackAsync(CancellationToken.None); } catch (Exception) { } db.ChangeTracker.Clear(); throw; }
    }
    private Task<Guid> Own(CustomerScope s, Guid op, string kind, object r, Func<CustomerUser, Task<Guid>> act, CancellationToken ct) => Command(s.TenantId, Actor(s), op, kind, r, async () => await act(await Self(s, ct)), ct);
    private Task<Guid> Staff(CoreScope s, Guid op, string kind, object r, Func<Task<Guid>> act, CancellationToken ct) => Command(s.TenantId, s.Actor.ToUpperInvariant(), op, kind, r, async () => { await Admin(s, ct); return await act(); }, ct);
    public Task<Guid> RegisterAsync(CustomerScope s, CustomerRegistration r, CancellationToken ct) => Command(s.TenantId, Actor(s), r.OperationId, "register", r, async () =>
    {
        Label(r.DisplayName); if (!await db.Tenants.AnyAsync(x => x.Id == s.TenantId, ct)) throw new CoreAccessException();
        var v = await identities.Single(x => x.Key == "identity.windows.v1").VerifyAsync(s.Principal, "local-windows", Guid.Empty, null, ct); var subject = Subject(s.TenantId, v);
        var old = await db.CustomerIdentities.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Provider == v.Provider && x.Account == v.Account && x.SubjectHash == subject, ct);
        if (old != null) { Require(old.Enabled, "Deleted/recovered identity cannot register another account."); return (await User(s.TenantId, old.UserId, ct)).Id; }
        var user = new CustomerUser { Id = Guid.NewGuid(), TenantId = s.TenantId, LoginHash = Login(s), DisplayName = r.DisplayName, AdultAttested = r.AdultAttested }; db.CustomerUsers.Add(user);
        db.CustomerIdentities.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = user.Id, Provider = v.Provider, Account = v.Account, Environment = v.Environment, SubjectHash = subject }); return user.Id;
    }, ct);
    public async Task<CustomerUserView> MeAsync(CustomerScope s, CancellationToken ct) { var u = await Self(s, ct); return new(u.Id, u.DisplayName, u.State, AdultAttested: u.AdultAttested); }
    public async Task<CustomerChallengeView> ChallengeAsync(CustomerScope s, CustomerChallengeRequest r, CancellationToken ct)
    {
        var id = await Command(s.TenantId, Actor(s), r.OperationId, "challenge", r, async () =>
        {
            Key(r.Account); Require(r.Purpose is "Link" or "Recover" && identities.Any(x => x.Key == r.Provider && x.Key != "identity.windows.v1"), "Deployed verified identity adapter required.");
            var user = r.Purpose == "Link" ? await Self(s, ct) : await User(s.TenantId, r.RecoveryUserId ?? Guid.Empty, ct);
            var x = new CustomerChallenge { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = user.Id, Provider = r.Provider, Account = r.Account, Purpose = r.Purpose, ActorHash = Login(s), ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) }; db.CustomerChallenges.Add(x); return x.Id;
        }, ct);
        var c = await db.CustomerChallenges.AsNoTracking().SingleAsync(x => x.TenantId == s.TenantId && x.Id == id, ct); return new(c.Id, c.Provider, c.Account, c.Purpose, c.ExpiresUtc);
    }
    private async Task<(CustomerChallenge Challenge, CustomerVerifiedIdentity Identity)> Proof(CustomerScope s, CustomerProof r, string purpose, CancellationToken ct)
    {
        var c = await db.CustomerChallenges.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == r.ChallengeId, ct) ?? throw new CoreAccessException();
        Require(!c.Used && c.ExpiresUtc > DateTimeOffset.UtcNow && c.ActorHash == Login(s) && c.Purpose == purpose && c.Provider == r.Provider && c.Account == r.Account, "Expired/used/mismatched identity challenge.");
        var adapter = identities.SingleOrDefault(x => x.Key == c.Provider) ?? throw new RuntimeValidationException("Identity adapter unavailable.");
        var v = await adapter.VerifyAsync(s.Principal, c.Account, c.Id, r.Assertion, ct); Require(v.Provider == c.Provider && v.Account == c.Account, "Identity adapter binding mismatch."); c.Used = true; return (c, v);
    }
    public Task<Guid> LinkAsync(CustomerScope s, CustomerProof r, CancellationToken ct) => Own(s, r.OperationId, "link", r, async u =>
    {
        var (c, v) = await Proof(s, r, "Link", ct); Require(c.UserId == u.Id, "Link target mismatch."); var hash = Subject(s.TenantId, v);
        var existing = await db.CustomerIdentities.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Provider == v.Provider && x.Account == v.Account && x.Environment == v.Environment && x.SubjectHash == hash, ct);
        if (existing != null) { if (existing.UserId != u.Id || !existing.Enabled) throw new CoreAccessException(); return existing.Id; }
        var row = new CustomerIdentity { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = u.Id, Provider = v.Provider, Account = v.Account, Environment = v.Environment, SubjectHash = hash }; db.CustomerIdentities.Add(row); return row.Id;
    }, ct);
    public Task<Guid> RecoverAsync(CustomerScope s, CustomerProof r, CancellationToken ct) => Command(s.TenantId, Actor(s), r.OperationId, "recover", r, async () =>
    {
        var (c, v) = await Proof(s, r, "Recover", ct); var hash = Subject(s.TenantId, v);
        if (!await db.CustomerIdentities.AnyAsync(x => x.TenantId == s.TenantId && x.UserId == c.UserId && x.Provider == v.Provider && x.Account == v.Account && x.Environment == v.Environment && x.SubjectHash == hash && x.Enabled, ct)) throw new CoreAccessException();
        var user = await User(s.TenantId, c.UserId, ct); var host = await identities.Single(x => x.Key == "identity.windows.v1").VerifyAsync(s.Principal, "local-windows", Guid.Empty, null, ct); var hostHash = Subject(s.TenantId, host);
        if (await db.CustomerIdentities.AnyAsync(x => x.TenantId == s.TenantId && x.Provider == host.Provider && x.SubjectHash == hostHash, ct)) throw new CoreAccessException();
        foreach (var old in await db.CustomerIdentities.Where(x => x.TenantId == s.TenantId && x.UserId == user.Id && x.Provider == host.Provider && x.Enabled).ToListAsync(ct)) old.Enabled = false;
        db.CustomerIdentities.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = user.Id, Provider = host.Provider, Account = host.Account, Environment = host.Environment, SubjectHash = hostHash }); user.LoginHash = Login(s); return user.Id;
    }, ct);
    public async Task<CustomerProviderInfo[]> ProvidersAsync(CoreScope s, CancellationToken ct)
    { await Admin(s, ct); return identities.Select(x => new CustomerProviderInfo("Identity", x.Key, x.Key == "identity.windows.v1" ? "Local" : "Test", false, "Trusted host or signed fixture; external/live recovery not qualified")).Concat(payments.Select(x => new CustomerProviderInfo("Payments", x.Key, "Test", false, "Disconnected signed test evidence; no live charges"))).Concat(notifications.Select(x => new CustomerProviderInfo("Notifications", x.Key, "Local", false, "Private in-app receipt; no email/SMS"))).ToArray(); }
}
public static class CustomerRegistrationExtensions
{
    public static IServiceCollection AddCustomerWorkbench(this IServiceCollection services)
    { services.AddScoped<CustomerWorkbench>(); services.AddScoped<ICustomerWorkbench>(s => s.GetRequiredService<CustomerWorkbench>()); services.AddScoped<ICustomerPaymentEvidenceStore, CustomerPaymentEvidenceStore>(); return services; }
}
