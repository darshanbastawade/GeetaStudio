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
public sealed class SqlSocialRecoveryTests
{
    private sealed class Gate { internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    private sealed class GatedPublisher(ISocialEffectLedger ledger, Gate gate) : ISocialPublisher
    {
        public string Key => "social.gated.v1"; public SocialPublisherCapabilities Capabilities => new(true, true, true, "OfflineFixture", "gated.v1");
        public async Task<SocialOutcome> SubmitAsync(SocialDispatch r, CancellationToken ct) { var outcome = await ledger.CommitAsync(r, ct); gate.Entered.TrySetResult(); await gate.Release.Task.WaitAsync(ct); return outcome; }
        public Task<SocialOutcome> ReconcileAsync(SocialDispatch r, CancellationToken ct) => ledger.FindAsync(r, ct);
    }
    [SqlFact]
    public async Task Expired_submitting_lease_requires_original_reconciliation_and_fences_stale_completion()
    {
        var gate = new Gate(); using var root = SocialTestFixture.Services(c => c.AddSingleton<ISocialPublisher>(s => new GatedPublisher(s.GetRequiredService<ISocialEffectLedger>(), gate))); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s, "m18-lease"); var e = s.GetRequiredService<IEditorialWorkbench>(); var content = await EditorialTestFixture.Approved(s, f, await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default)); var account = await SocialTestFixture.Account(s, f, adapter: "social.gated.v1"); var pkg = await SocialTestFixture.Package(s, f, content, account); var social = s.GetRequiredService<ISocialPublishing>(); var pub = await social.ScheduleAsync(f.Owner, f.Universe, new(Guid.NewGuid(), pkg.Id, DateTimeOffset.UtcNow, "UTC"), default);
        using var worker = root.CreateScope(); var processing = worker.ServiceProvider.GetRequiredService<ISocialPublishing>().ProcessNextAsync(default);
        try { await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30)); await s.GetRequiredService<CoreDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE [social].[SocialPublication] SET [LeaseUntilUtc]={DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE [Id]={pub.Id}"); using var replacement = root.CreateScope(); await replacement.ServiceProvider.GetRequiredService<ISocialPublishing>().ProcessNextAsync(default); pub = (await social.PublicationsAsync(f.Owner, f.Universe, default)).Single(); Assert.Equal("Unknown", pub.State); }
        finally { gate.Release.TrySetResult(); await processing.WaitAsync(TimeSpan.FromSeconds(30)); }
        pub = (await social.PublicationsAsync(f.Owner, f.Universe, default)).Single(); Assert.Equal("Unknown", pub.State); Assert.Null(pub.RemoteId); await social.ControlAsync(f.Owner, f.Universe, pub.Id, new(Guid.NewGuid(), pub.RowVersion, "Reconcile", "fixture:expired-lease"), default); pub = (await social.PublicationsAsync(f.Owner, f.Universe, default)).Single(); Assert.Equal("Published", pub.State); Assert.Equal(1, pub.Attempts); Assert.Equal(1, await s.GetRequiredService<CoreDbContext>().SocialEffects.CountAsync(x => x.PublicationId == pub.Id));
    }
    [SqlFact]
    public async Task Withdrawal_preserves_unknown_remote_receipt_and_pending_correction_work()
    {
        using var root = SocialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s, "m18-withdraw"); var e = s.GetRequiredService<IEditorialWorkbench>(); var content = await EditorialTestFixture.Approved(s, f, await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default)); var account = await SocialTestFixture.Account(s, f, adapter: "social.unknown.v1"); var pkg = await SocialTestFixture.Package(s, f, content, account); var social = s.GetRequiredService<ISocialPublishing>(); var pub = await social.ScheduleAsync(f.Owner, f.Universe, new(Guid.NewGuid(), pkg.Id, DateTimeOffset.UtcNow, "UTC"), default); pub = await SocialTestFixture.Drain(s, f, pub.Id); await social.ControlAsync(f.Owner, f.Universe, pub.Id, new(Guid.NewGuid(), pub.RowVersion, "Withdraw", "fixture:local-withdrawal-not-takedown"), default); pub = (await social.PublicationsAsync(f.Owner, f.Universe, default)).Single(); await social.ControlAsync(f.Owner, f.Universe, pub.Id, new(Guid.NewGuid(), pub.RowVersion, "Reconcile", "fixture:original-unknown"), default); pub = (await social.PublicationsAsync(f.Owner, f.Universe, default)).Single(); Assert.Equal("Withdrawn", pub.State); Assert.NotNull(pub.RemoteId); Assert.Contains("ManualTakedown", pub.Outcome); Assert.Equal(1, await s.GetRequiredService<CoreDbContext>().EditorialCopies.CountAsync(x => x.ItemId == content.Id));
    }
    [SqlFact]
    public async Task Correction_before_late_receipt_adds_external_copy_remediation_and_withholds_package()
    {
        using var root = SocialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await EditorialTestFixture.Setup(s, "m18-late-correction"); var editor = s.GetRequiredService<IEditorialWorkbench>(); var sources = s.GetRequiredService<ISourceRegistry>(); var source = await SourceTestFixture.CreateAsync(sources, f.Owner, f.Universe); var content = await editor.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default); await sources.LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source.Snapshot, null, content.RevisionId, null, "adapted-from"), default); content = await EditorialTestFixture.Approved(s, f, await editor.InspectAsync(f.Owner, f.Universe, content.Id, default)); var a = await SocialTestFixture.Account(s, f, adapter: "social.unknown.v1"); var pkg = await SocialTestFixture.Package(s, f, content, a); var social = s.GetRequiredService<ISocialPublishing>(); var pub = await social.ScheduleAsync(f.Owner, f.Universe, new(Guid.NewGuid(), pkg.Id, DateTimeOffset.UtcNow, "UTC"), default); pub = await SocialTestFixture.Drain(s, f, pub.Id); Assert.Equal("Unknown", pub.State); var correction = await editor.CorrectAsync(f.Owner, f.Universe, new(Guid.NewGuid(), null, source.Snapshot, "Owned synthetic source correction", "fixture:actual-fixture-correction"), default); await social.ControlAsync(f.Owner, f.Universe, pub.Id, new(Guid.NewGuid(), pub.RowVersion, "Reconcile", "fixture:late-receipt"), default); pub = (await social.PublicationsAsync(f.Owner, f.Universe, default)).Single(); Assert.NotNull(pub.RemoteId); Assert.Contains("ManualRemediationRequired", pub.Outcome); var tasks = await editor.CorrectionsAsync(f.Owner, f.Universe, default); Assert.Contains(tasks, x => x.CorrectionId == correction && x.Kind == "External" && x.CopyId.HasValue); Assert.Null((await social.PackagesAsync(f.Owner, f.Universe, default)).Single().Snapshot); await Assert.ThrowsAsync<EvidenceBlockedException>(() => social.ExportAsync(f.Owner, f.Universe, pkg.Id, default)); Assert.Equal(1, await s.GetRequiredService<CoreDbContext>().SocialEffects.CountAsync(x => x.PublicationId == pub.Id));
    }
}
