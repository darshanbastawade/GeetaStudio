using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlCustomerIdentityTests
{
    [SqlFact]
    public async Task Verified_linking_and_recovery_preserve_internal_user_and_wallet_and_refuse_cross_account_subjects()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CustomerTestFixture.Setup(s); var w = s.GetRequiredService<ICustomerWorkbench>(); await w.GrantAsync(f.Admin, new(Guid.NewGuid(), f.User, 50, DateTimeOffset.UtcNow.AddDays(1), "fixture:grant"), default);
        var c = await w.ChallengeAsync(f.Alice, new(Guid.NewGuid(), "identity.fixture.a.v1", "fixture-account"), default); var proof = CustomerTestFixture.Proof(f.Alice, c, "verified-subject"); var link = await w.LinkAsync(f.Alice, proof, default); Assert.Equal(link, await w.LinkAsync(f.Alice, proof, default));
        var b = await w.ChallengeAsync(f.Alice, new(Guid.NewGuid(), "identity.fixture.b.v1", "replacement-account"), default); await w.LinkAsync(f.Alice, CustomerTestFixture.Proof(f.Alice, b, "replacement-subject"), default); Assert.Equal(f.User, (await w.MeAsync(f.Alice, default)).Id);
        var other = CustomerTestFixture.Scope(f.Tenant, "fixture-other:" + f.User.ToString("N")); await w.RegisterAsync(other, new(Guid.NewGuid(), "Other fixture", true), default); var oc = await w.ChallengeAsync(other, new(Guid.NewGuid(), c.Provider, c.Account), default); await Assert.ThrowsAsync<CoreAccessException>(() => w.LinkAsync(other, CustomerTestFixture.Proof(other, oc, "verified-subject"), default));
        var recovered = CustomerTestFixture.Scope(f.Tenant, "fixture-recovered:" + f.User.ToString("N")); var rc = await w.ChallengeAsync(recovered, new(Guid.NewGuid(), c.Provider, c.Account, "Recover", f.User), default); await Assert.ThrowsAsync<CoreAccessException>(() => w.RecoverAsync(recovered, CustomerTestFixture.Proof(recovered, rc, "unowned-subject"), default));
        Assert.Equal(f.User, await w.RecoverAsync(recovered, CustomerTestFixture.Proof(recovered, rc, "verified-subject"), default)); Assert.Equal(50, (await w.WalletAsync(recovered, default)).Available); await Assert.ThrowsAsync<CoreAccessException>(() => w.MeAsync(f.Alice, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => w.MeAsync(recovered with { TenantId = Guid.NewGuid() }, default));
    }
    [SqlFact]
    public async Task Parent_consent_household_isolation_progress_versions_export_and_deletion_preserve_financial_audit()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CustomerTestFixture.Setup(s); var w = s.GetRequiredService<ICustomerWorkbench>(); var child = await w.ProfileAsync(f.Alice, new(Guid.NewGuid(), f.Household, "Fixture child", "Child", "en-IN"), default); Assert.False((await w.ProfilesAsync(f.Alice, default)).Single(x => x.Id == child).Personalization);
        var other = CustomerTestFixture.Scope(f.Tenant, "fixture-other:" + f.User.ToString("N")); var otherId = await w.RegisterAsync(other, new(Guid.NewGuid(), "Other adult", true), default); var invite = await w.InviteAsync(f.Alice, f.Household, new(Guid.NewGuid(), otherId, "Adult"), default); await Assert.ThrowsAsync<CoreAccessException>(() => w.AcceptAsync(f.Alice, invite, new(Guid.NewGuid()), default)); await w.AcceptAsync(other, invite, new(Guid.NewGuid()), default);
        await Assert.ThrowsAsync<CoreAccessException>(() => w.ConsentAsync(other, new(Guid.NewGuid(), child, "Progress", true, DateTimeOffset.UtcNow.AddDays(1), "fixture:unauthorized-adult"), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ProgressAsync(f.Alice, child, new(Guid.NewGuid(), "", "geeta-reading", 3, true), default)); await w.ConsentAsync(f.Alice, new(Guid.NewGuid(), child, "Progress", true, DateTimeOffset.UtcNow.AddDays(1), "fixture:parent-reviewed"), default);
        await w.ProgressAsync(f.Alice, child, new(Guid.NewGuid(), "", "geeta-reading", 3, true), default); var progress = (await w.ProgressAsync(f.Alice, child, default)).Single();
        await w.ProgressAsync(f.Alice, child, new(Guid.NewGuid(), progress.RowVersion, progress.Key, 4, true), default); await Assert.ThrowsAsync<RuntimeConflictException>(() => w.ProgressAsync(f.Alice, child, new(Guid.NewGuid(), progress.RowVersion, progress.Key, 5, false), default));
        await w.ConsentAsync(f.Alice, new(Guid.NewGuid(), child, "Personalization", true, DateTimeOffset.UtcNow.AddDays(1), "fixture:parent-personalization"), default); var p = (await w.ProfilesAsync(f.Alice, default)).Single(x => x.Id == child); await Assert.ThrowsAsync<RuntimeValidationException>(() => w.EditProfileAsync(f.Alice, child, new(Guid.NewGuid(), p.RowVersion, p.Alias, p.Locale, true), default));
        var adultProfile = await w.ProfileAsync(other, new(Guid.NewGuid(), f.Household, "Private adult", "Adult", "en-IN"), default); Assert.DoesNotContain((await w.ExportAsync(f.Alice, default)).Profiles, x => x.Id == adultProfile); await Assert.ThrowsAsync<CoreAccessException>(() => w.ProgressAsync(f.Alice, adultProfile, default));
        await w.ConsentAsync(f.Alice, new(Guid.NewGuid(), child, "Progress", false, DateTimeOffset.UtcNow, "fixture:withdrawn"), default); Assert.Empty(await w.ProgressAsync(f.Alice, child, default)); Assert.False(await s.GetRequiredService<CoreDbContext>().CustomerProgress.AnyAsync(x => x.TenantId == f.Tenant && x.ProfileId == child));
        await w.GrantAsync(f.Admin, new(Guid.NewGuid(), f.User, 20, DateTimeOffset.UtcNow.AddDays(1), "fixture:retained-credit-audit"), default); await Assert.ThrowsAsync<RuntimeValidationException>(() => w.DeleteAsync(f.Alice, new(Guid.NewGuid()), default)); await w.TransferAsync(f.Alice, f.Household, new(Guid.NewGuid(), otherId), default); await w.DeleteAsync(f.Alice, new(Guid.NewGuid()), default); await Assert.ThrowsAsync<CoreAccessException>(() => w.MeAsync(f.Alice, default));
        var db = s.GetRequiredService<CoreDbContext>(); var erased = await db.CustomerUsers.AsNoTracking().SingleAsync(x => x.Id == f.User); Assert.Equal("", erased.DisplayName); Assert.Equal("Deleted", erased.State); Assert.All(await db.CustomerProfiles.AsNoTracking().Where(x => x.UserId == f.User).ToArrayAsync(), x => { Assert.True(x.Deleted); Assert.Equal("", x.Alias); }); Assert.Single(await db.CustomerCredits.AsNoTracking().Where(x => x.UserId == f.User).ToArrayAsync()); Assert.DoesNotContain((await w.ProfilesAsync(other, default)), x => x.Id == child);
    }
    [SqlFact]
    public async Task Expired_grants_cannot_be_spent_and_successful_mutations_record_expiry_without_rewriting_history()
    {
        using var root = CustomerTestFixture.Services(); using var scope = root.CreateScope(); var f = await CustomerTestFixture.Setup(scope.ServiceProvider); var w = scope.ServiceProvider.GetRequiredService<ICustomerWorkbench>(); await w.GrantAsync(f.Admin, new(Guid.NewGuid(), f.User, 100, DateTimeOffset.UtcNow.AddSeconds(2), "fixture:short-expiry"), default); await Task.Delay(2300); Assert.Equal(0, (await w.WalletAsync(f.Alice, default)).Available);
        await w.GrantAsync(f.Admin, new(Guid.NewGuid(), f.User, 50, DateTimeOffset.UtcNow.AddDays(1), "fixture:new-grant"), default); var r = await w.ReserveAsync(f.Alice, new(Guid.NewGuid(), f.Profile, "after-expiry", 10), default); Assert.Equal(40, (await w.WalletAsync(f.Alice, default)).Available); Assert.Single((await w.WalletAsync(f.Alice, default)).Entries, x => x.Kind == "Expiry"); await w.CancelReservationAsync(f.Alice, r, new(Guid.NewGuid()), default); Assert.Equal(50, (await w.WalletAsync(f.Alice, default)).Available);
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>(); var original = await db.CustomerCredits.FirstAsync(x => x.UserId == f.User && x.Kind == "Grant"); original.Amount++; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
