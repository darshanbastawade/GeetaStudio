using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ContentOS.Foundation.Tests;

// Permanent acceptance regressions for the M23–M27 review findings.
[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlCommerceAdmissionTests
{
    [SqlFact]
    public async Task Mixed_SKUs_cannot_expand_a_smaller_license_and_roster_reduction_restores_access()
    {
        using var root = CommerceTestFixture.Services(); using var scope = root.CreateScope();
        var s = scope.ServiceProvider; var f = await CommerceTestFixture.Setup(s);
        var shop = s.GetRequiredService<ICommerceWorkbench>();
        var customers = s.GetRequiredService<ICustomerWorkbench>();
        var license = new CommerceLicense("PrivateTest", 30, 1, "PinnedVersionNotifyCorrections", "fixture:review-contract", "Review probe only", true);
        var small = await shop.ProductAsync(f.Creation.Owner, new(Guid.NewGuid(), "review-one-seat", 1, f.CreationId, "Institutional", "INR", 1000, license), default);
        foreach (var product in new[] { f.Institutional, small })
        {
            var order = await shop.PurchaseAsync(f.Creation.Owner, new(Guid.NewGuid(), product, InstitutionId: f.Institution), default);
            await CommerceTestFixture.Paid(s, f, order);
        }
        var learners = new List<(CustomerScope Scope, Guid User)>();
        for (var i = 0; i < 2; i++)
        {
            var learner = CustomerTestFixture.Scope(f.Creation.Owner.TenantId, "review-seat-" + Guid.NewGuid());
            var user = await customers.RegisterAsync(learner, new(Guid.NewGuid(), "Controlled adult", true), default);
            await shop.SeatAsync(f.Creation.Owner, f.Institution, new(Guid.NewGuid(), user, "Review"), default);
            learners.Add((learner, user));
        }
        foreach (var learner in learners)
            await Assert.ThrowsAsync<RuntimeValidationException>(() => shop.InstitutionDownloadAsync(learner.Scope, f.Institution, small, default));
        Assert.NotEmpty((await shop.InstitutionDownloadAsync(learners[1].Scope, f.Institution, f.Institutional, default)).Html);
        await shop.SeatAsync(f.Creation.Owner, f.Institution, new(Guid.NewGuid(), learners[0].User, "Review", Enabled: false), default);
        await Assert.ThrowsAsync<CoreAccessException>(() => shop.InstitutionDownloadAsync(learners[0].Scope, f.Institution, small, default));
        Assert.NotEmpty((await shop.InstitutionDownloadAsync(learners[1].Scope, f.Institution, small, default)).Html);
        var smallOrder = (await shop.OrdersAsync(f.Creation.Owner, default)).Single(x => x.ProductId == small);
        var payment = await CommerceTestFixture.Payment(s, f, smallOrder.Id);
        await CommerceTestFixture.Send(s, f, CustomerTestFixture.Evidence(payment, 3, "Refunded", payment.PriceMinor, DateTimeOffset.UtcNow.AddMinutes(-1)));
        // Payment authority applies immediately, even before commerce reconciliation.
        await Assert.ThrowsAsync<RuntimeValidationException>(() => shop.InstitutionDownloadAsync(learners[1].Scope, f.Institution, small, default));
        Assert.NotEmpty((await shop.InstitutionDownloadAsync(learners[1].Scope, f.Institution, f.Institutional, default)).Html);
    }

    [SqlFact]
    public Task Refund_after_claim_cancels_and_blocks_delivery() => RefundRace(true);

    [SqlFact]
    public Task Payment_refund_without_commerce_reconciliation_blocks_delivery() => RefundRace(false);

    private static async Task RefundRace(bool reconcile)
    {
        PausedDelivery? paused = null;
        using var root = CreationTestFixture.Services(configure: services =>
        {
            services.AddCommerceWorkbench();
            foreach (var key in new[] { "payment.fixture.a.v1", "payment.fixture.b.v1", "payment.unknown.v1" })
                services.AddScoped<IPaymentProvider>(p => new FixturePaymentProvider(key, p.GetRequiredService<ICustomerPaymentEvidenceStore>(), CustomerTestFixture.Secret));
            services.AddSingleton<ICommerceDelivery>(p => paused = new PausedDelivery(p.GetRequiredService<ICommerceDeliveryLedger>()));
        });
        using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await CommerceTestFixture.Setup(s); var shop = s.GetRequiredService<ICommerceWorkbench>();
        var id = await shop.PurchaseAsync(f.Creation.Owner, new(Guid.NewGuid(), f.Digital), default);
        await CommerceTestFixture.Paid(s, f, id);
        var adapter = root.GetServices<ICommerceDelivery>().OfType<PausedDelivery>().Single(); adapter.Order = id;
        var work = Task.Run(async () =>
        {
            using var workerScope = root.CreateScope(); var worker = workerScope.ServiceProvider.GetRequiredService<ICommerceWorkbench>();
            for (var i = 0; i < 200 && !adapter.Entered.Task.IsCompleted; i++) await worker.ProcessNextAsync(default);
        });
        try
        {
            await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var payment = await CommerceTestFixture.Payment(s, f, id);
            await CommerceTestFixture.Send(s, f, CustomerTestFixture.Evidence(payment, 3, "Refunded", payment.PriceMinor, DateTimeOffset.UtcNow.AddMinutes(-1)));
            if (reconcile) Assert.Equal("Revoked", (await shop.ReconcileAsync(f.Creation.Owner, id, default)).State);
            await adapter.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { adapter.Release.TrySetResult(); }
        await work.WaitAsync(TimeSpan.FromSeconds(20));
        var db = s.GetRequiredService<CoreDbContext>();
        Assert.Equal(0, await db.CommerceEffects.CountAsync(x => db.CommerceDeliveries.Any(d => d.Id == x.DeliveryId && d.OrderId == id)));
        Assert.Equal("Withheld", (await shop.OrdersAsync(f.Creation.Owner, default)).Single(x => x.Id == id).DeliveryState);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => shop.DownloadAsync(f.Creation.Owner, id, default));
    }

    [SqlFact]
    public async Task Final_effect_admission_rejects_stale_expired_leases_and_current_source_revocation()
    {
        using var root = CommerceTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await CommerceTestFixture.Setup(s); var shop = s.GetRequiredService<ICommerceWorkbench>();
        var id = await shop.PurchaseAsync(f.Creation.Owner, new(Guid.NewGuid(), f.Digital), default); await CommerceTestFixture.Paid(s, f, id);
        var db = s.GetRequiredService<CoreDbContext>(); var row = await db.CommerceDeliveries.SingleAsync(x => x.OrderId == id);
        var token = Guid.NewGuid(); row.State = "Submitting"; row.Lease = token; row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(1); await db.SaveChangesAsync();
        var dispatch = System.Text.Json.JsonSerializer.Deserialize<CommerceDispatch>(row.RequestJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var ledger = s.GetRequiredService<ICommerceDeliveryLedger>();
        Assert.Equal("Withheld", (await ledger.CommitAsync(dispatch with { LeaseToken = Guid.NewGuid() }, default)).State);
        row.LeaseUntilUtc = null; await db.SaveChangesAsync();
        Assert.Equal("Withheld", (await ledger.CommitAsync(dispatch with { LeaseToken = token }, default)).State);
        row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        Assert.Equal("Withheld", (await ledger.CommitAsync(dispatch with { LeaseToken = token }, default)).State);
        row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(1); await db.SaveChangesAsync();
        var snapshot = await db.SourceSnapshots.AsNoTracking().SingleAsync(x => x.Id == f.Creation.Snapshot);
        var edition = await db.SourceEditions.AsNoTracking().SingleAsync(x => x.Id == snapshot.EditionId);
        var registry = s.GetRequiredService<ISourceRegistry>(); var work = await registry.InspectAsync(f.Creation.Editorial.Owner, f.Creation.Editorial.Universe, edition.WorkId, default);
        await registry.RevokeAsync(f.Creation.Editorial.Owner, f.Creation.Editorial.Universe, edition.WorkId, new(Guid.NewGuid(), null, "fixture:fulfillment-rights-withdrawal", work.Work.RowVersion), default);
        Assert.Equal("Withheld", (await ledger.CommitAsync(dispatch with { LeaseToken = token }, default)).State);
        Assert.Equal(0, await db.CommerceEffects.CountAsync(x => x.DeliveryId == row.Id));
    }

    [SqlFact]
    public async Task Refund_preserves_authoritative_original_effect_receipt_and_replay()
    {
        using var root = CommerceTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await CommerceTestFixture.Setup(s); var shop = s.GetRequiredService<ICommerceWorkbench>();
        var id = await shop.PurchaseAsync(f.Creation.Owner, new(Guid.NewGuid(), f.Digital), default); await CommerceTestFixture.Paid(s, f, id); await CommerceTestFixture.Drain(s, f, id);
        var db = s.GetRequiredService<CoreDbContext>(); var row = await db.CommerceDeliveries.AsNoTracking().SingleAsync(x => x.OrderId == id);
        var dispatch = System.Text.Json.JsonSerializer.Deserialize<CommerceDispatch>(row.RequestJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var ledger = s.GetRequiredService<ICommerceDeliveryLedger>(); var original = await ledger.FindAsync(dispatch, default);
        var payment = await CommerceTestFixture.Payment(s, f, id);
        await CommerceTestFixture.Send(s, f, CustomerTestFixture.Evidence(payment, 3, "Refunded", payment.PriceMinor, DateTimeOffset.UtcNow.AddMinutes(-1))); await shop.ReconcileAsync(f.Creation.Owner, id, default);
        Assert.Equal(original, await ledger.CommitAsync(dispatch, default));
        Assert.Equal("Delivered", (await shop.OrdersAsync(f.Creation.Owner, default)).Single(x => x.Id == id).DeliveryState);
        Assert.Equal(1, await db.CommerceEffects.CountAsync(x => x.DeliveryId == row.Id));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => shop.DownloadAsync(f.Creation.Owner, id, default));
    }
    private sealed class PausedDelivery(ICommerceDeliveryLedger ledger) : ICommerceDelivery
    {
        public Guid Order { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Key => "delivery.fixture.a.v1";
        public string Qualification => "OfflineFixture";
        public async Task<CommerceReceipt> SubmitAsync(CommerceDispatch request, CancellationToken ct)
        {
            if (request.OrderId == Order)
            {
                using var registration = ct.Register(() => Cancelled.TrySetResult());
                Entered.TrySetResult(); await Release.Task;
                // Deliberately uncooperative provider: final admission must still fail closed.
                return await ledger.CommitAsync(request, CancellationToken.None);
            }
            return await ledger.CommitAsync(request, ct);
        }
        public Task<CommerceReceipt> ReconcileAsync(CommerceDispatch request, CancellationToken ct) => ledger.FindAsync(request, ct);
    }
}
