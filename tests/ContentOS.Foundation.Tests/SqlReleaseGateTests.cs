using ContentOS.Application;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlReleaseGateTests
{
    [SqlFact]
    public async Task Actual_module_store_rejects_fixture_m12_and_rechecks_source_withdrawal_and_scope()
    {
        using var root = ReadTestFixture.Services(); using var scope = root.CreateScope(); var services = scope.ServiceProvider;
        var f = await ReadTestFixture.Setup(services, "release-owner-" + Guid.NewGuid());
        var db = services.GetRequiredService<CoreDbContext>();
        var run = await db.EvaluationRuns.AsNoTracking().Where(x => x.TenantId == f.Owner.TenantId && x.UniverseId == f.Universe).OrderByDescending(x => x.CreatedUtc).Select(x => x.Id).FirstAsync();
        var target = new ReleaseTarget(f.Key, f.Owner.TenantId, f.Universe, f.Collection, run); var evidence = new ReleaseModuleEvidence(services);
        var before = await evidence.ObserveAsync("READ", target, f.Owner.Actor, default);
        Assert.Contains("M12:100HumanReviewedDevelopmentCasesRequired", before.Blocks);
        Assert.Contains("M12:GenuineAcceptanceRequired", before.Blocks);
        Assert.Contains("FixtureCollectionCannotRollOut", before.Blocks);
        Assert.Contains("GenuineSourceLineageRequired", before.Blocks);
        await Assert.ThrowsAsync<CoreAccessException>(() => evidence.ObserveAsync("READ", target, "unrelated-actor", default));
        var sources = services.GetRequiredService<ISourceRegistry>(); var work = await sources.InspectAsync(f.Owner, f.Universe, f.SourceWork, default);
        await sources.RevokeAsync(f.Owner, f.Universe, f.SourceWork, new(Guid.NewGuid(), null, "fixture:release-source-withdrawal", work.Work.RowVersion), default);
        var after = await evidence.ObserveAsync("READ", target, f.Owner.Actor, default);
        Assert.NotEqual(before.ContentFingerprint, after.ContentFingerprint);
        Assert.NotEmpty(after.Blocks);
    }
}
