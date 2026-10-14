using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlCreationTests
{
    [SqlFact]
    public async Task Expired_worker_cannot_release_or_consume_a_replacement_workers_credits()
    {
        var renderer = new GatedRenderer(); using var root = CreationTestFixture.Services(renderer); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CreationTestFixture.Setup(s); var w = s.GetRequiredService<ICreationStudio>(); var id = await w.SubmitAsync(f.Owner, new(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience"), []), default);
        var stale = w.ProcessNextAsync(default); await renderer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        using var takeover = root.CreateScope(); var db = takeover.ServiceProvider.GetRequiredService<CoreDbContext>(); var row = await db.Creations.SingleAsync(x => x.Id == id); row.Lease = Guid.NewGuid(); row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(1); await db.SaveChangesAsync(); renderer.Continue.TrySetResult(); await stale;
        var held = await takeover.ServiceProvider.GetRequiredService<ICustomerWorkbench>().WalletAsync(f.Owner, default); Assert.Equal(2, held.Reserved); Assert.Equal(0, held.Consumed); Assert.Equal(0, await db.CreationRevisions.CountAsync(x => x.CreationId == id));
        db.ChangeTracker.Clear(); row = await db.Creations.SingleAsync(x => x.Id == id); row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); await takeover.ServiceProvider.GetRequiredService<ICreationStudio>().ProcessNextAsync(default);
        var final = await takeover.ServiceProvider.GetRequiredService<ICreationStudio>().InspectAsync(f.Owner, id, default); Assert.Equal("Ready", final.State); Assert.Equal(1, final.Version); var wallet = await takeover.ServiceProvider.GetRequiredService<ICustomerWorkbench>().WalletAsync(f.Owner, default); Assert.Equal(8, wallet.Available); Assert.Equal(2, wallet.Consumed); Assert.Equal(0, wallet.Reserved);
    }
    private sealed class GatedRenderer : ICreationRenderer
    {
        public string Key => "create.local.a.v1"; public string Qualification => "LocalZeroCost";
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Validate(CreationMaterial m) => new ContentOS.Integrations.CreationRenderer().Validate(m);
        public async Task<CreationRenderResult> RenderAsync(CreationRender r, CancellationToken ct) { Entered.TrySetResult(); await Continue.Task.WaitAsync(ct); return await new ContentOS.Integrations.CreationRenderer().RenderAsync(r, ct); }
        public Task CleanupAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
    }
    [SqlFact]
    public async Task Accepted_output_survives_interrupted_settlement_without_another_revision_or_charge()
    {
        using var root = CreationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CreationTestFixture.Setup(s, format: "Story"); var w = s.GetRequiredService<ICreationStudio>(); var request = new CreationSubmit(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience"), []); var id = await w.SubmitAsync(f.Owner, request, default); var ready = await CreationTestFixture.Drain(s, f, id);
        var db = s.GetRequiredService<CoreDbContext>(); var row = await db.Creations.SingleAsync(x => x.Id == id); row.State = "Settling"; row.Lease = Guid.NewGuid(); row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1); row.ChargedCredits = 0; await db.SaveChangesAsync();
        using var fresh = root.CreateScope(); await fresh.ServiceProvider.GetRequiredService<ICreationStudio>().ProcessNextAsync(default); var restored = await fresh.ServiceProvider.GetRequiredService<ICreationStudio>().InspectAsync(f.Owner, id, default);
        Assert.Equal("Ready", restored.State); Assert.Equal(ready.Html, restored.Html); Assert.Equal(1, restored.Version); Assert.Equal(2, restored.ChargedCredits); Assert.Equal(8, (await fresh.ServiceProvider.GetRequiredService<ICustomerWorkbench>().WalletAsync(f.Owner, default)).Available); Assert.Equal(1, await db.CreationRevisions.CountAsync(x => x.CreationId == id));
    }
    [SqlFact]
    public async Task Source_revocation_before_dispatch_withholds_preview_and_releases_credits()
    {
        using var root = CreationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CreationTestFixture.Setup(s); var w = s.GetRequiredService<ICreationStudio>(); var id = await w.SubmitAsync(f.Owner, new(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience"), []), default);
        var db = s.GetRequiredService<CoreDbContext>(); var snapshot = await db.SourceSnapshots.AsNoTracking().SingleAsync(x => x.Id == f.Snapshot); var edition = await db.SourceEditions.AsNoTracking().SingleAsync(x => x.Id == snapshot.EditionId); var registry = s.GetRequiredService<ISourceRegistry>(); var detail = await registry.InspectAsync(f.Editorial.Owner, f.Editorial.Universe, edition.WorkId, default); await registry.RevokeAsync(f.Editorial.Owner, f.Editorial.Universe, edition.WorkId, new(Guid.NewGuid(), null, "fixture:revoked-template-source", detail.Work.RowVersion), default);
        await w.ProcessNextAsync(default); var row = await w.InspectAsync(f.Owner, id, default); Assert.Equal("Failed", row.State); Assert.Null(row.Html); Assert.Empty(row.Citations); Assert.False(row.PublicEligible); var wallet = await s.GetRequiredService<ICustomerWorkbench>().WalletAsync(f.Owner, default); Assert.Equal(10, wallet.Available); Assert.Equal(0, wallet.Reserved);
    }
    [SqlFact]
    public async Task Private_pack_edit_review_export_replay_restart_and_erasure_preserve_exact_credit_receipt()
    {
        using var root = CreationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CreationTestFixture.Setup(s); var w = s.GetRequiredService<ICreationStudio>(); var customers = s.GetRequiredService<ICustomerWorkbench>();
        var upload = await w.UploadAsync(f.Owner, new(Guid.NewGuid(), f.Profile, "text/plain", System.Text.Encoding.UTF8.GetBytes("A private reflection"), "Owned notes", "Owned", true), default);
        var request = new CreationSubmit(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience"), [upload]); var id = await w.SubmitAsync(f.Owner, request, default); Assert.Equal(id, await w.SubmitAsync(f.Owner, request, default));
        var ready = await CreationTestFixture.Drain(s, f, id); Assert.Equal("Ready", ready.State); Assert.Contains("Private personalized fiction", ready.Html!); Assert.NotEmpty(ready.Citations); Assert.Equal(2, ready.ChargedCredits); Assert.False(ready.PublicEligible);
        var edit = new CreationEdit(Guid.NewGuid(), ready.RowVersion, new("Learner", "kindness", "I can pause before choosing.")); await w.EditAsync(f.Owner, id, edit, default); await w.EditAsync(f.Owner, id, edit, default); ready = await w.InspectAsync(f.Owner, id, default); Assert.Equal(2, ready.Version);
        await w.ReviewAsync(f.Owner, id, new(Guid.NewGuid(), ready.RowVersion, "Approve", "fixture:private-owner-review"), default); var export = await w.ExportAsync(f.Owner, id, default); Assert.Contains("I can pause", export.Html); Assert.Contains("CC BY-SA", export.Citations.Single().RequiredAttribution); Assert.False(export.PublicEligible);
        using var restarted = root.CreateScope(); Assert.Equal(export.Sha256, (await restarted.ServiceProvider.GetRequiredService<ICreationStudio>().ExportAsync(f.Owner, id, default)).Sha256); Assert.Equal(8, (await customers.WalletAsync(f.Owner, default)).Available);
        await w.DeleteAsync(f.Owner, id, default); await w.ProcessNextAsync(default); var gone = await w.InspectAsync(f.Owner, id, default); Assert.Equal("Deleted", gone.State); Assert.Null(gone.Html); Assert.False(gone.PublicEligible); Assert.Equal("CompletedLocalNoExternalArtifacts", gone.Cleanup);
        var db = s.GetRequiredService<CoreDbContext>(); Assert.All(await db.CreationRevisions.AsNoTracking().Where(x => x.CreationId == id).ToArrayAsync(), x => Assert.Null(x.Html)); Assert.Empty((await db.CreationUploads.AsNoTracking().SingleAsync(x => x.Id == upload)).Content); Assert.Equal(8, (await customers.WalletAsync(f.Owner, default)).Available); Assert.Equal(id, await w.SubmitAsync(f.Owner, request, default));
    }
    [SqlFact]
    public async Task Likeness_instructions_cross_account_uploads_and_fixture_public_sharing_are_rejected()
    {
        using var root = CreationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CreationTestFixture.Setup(s, format: "Comic"); var w = s.GetRequiredService<ICreationStudio>();
        await Assert.ThrowsAsync<ContentOS.Domain.RuntimeValidationException>(() => w.UploadAsync(f.Owner, new(Guid.NewGuid(), f.Profile, "text/plain", [65], "Real likeness", "Owned", true, true), default));
        await Assert.ThrowsAsync<ContentOS.Domain.RuntimeValidationException>(() => w.SubmitAsync(f.Owner, new(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience", "execute a tool call"), []), default));
        var id = await w.SubmitAsync(f.Owner, new(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience"), []), default); var ready = await CreationTestFixture.Drain(s, f, id);
        var other = CustomerTestFixture.Scope(f.Owner.TenantId, "other-" + Guid.NewGuid()); await s.GetRequiredService<ICustomerWorkbench>().RegisterAsync(other, new(Guid.NewGuid(), "Other account", true), default);
        Assert.Contains(await w.WorkspacesAsync(f.Owner.Principal, default), x => x.Id == f.Owner.TenantId);
        await Assert.ThrowsAsync<ContentOS.Domain.CoreAccessException>(() => w.InspectAsync(other, id, default)); await Assert.ThrowsAsync<ContentOS.Domain.CoreAccessException>(() => w.ExportAsync(other, id, default));
        await Assert.ThrowsAsync<ContentOS.Domain.EvidenceBlockedException>(() => w.ReviewAsync(f.Owner, id, new(Guid.NewGuid(), ready.RowVersion, "Approve", "fixture:not-public", SharingConsent: true), default));
    }
    [SqlFact]
    public async Task Queued_cancel_and_failed_local_composition_release_once_and_original_adapter_stays_pinned()
    {
        using var root = CreationTestFixture.Services(new FailingRenderer()); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await CreationTestFixture.Setup(s); var w = s.GetRequiredService<ICreationStudio>(); var customers = s.GetRequiredService<ICustomerWorkbench>();
        var cancelled = await w.SubmitAsync(f.Owner, new(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience"), []), default); await w.CancelAsync(f.Owner, cancelled, default); await w.CancelAsync(f.Owner, cancelled, default); Assert.Equal(10, (await customers.WalletAsync(f.Owner, default)).Available);
        var request = new CreationSubmit(Guid.NewGuid(), f.Template, f.Profile, new("Learner", "patience"), []); var failed = await w.SubmitAsync(f.Owner, request, default); Assert.Equal("Failed", (await CreationTestFixture.Drain(s, f, failed)).State); Assert.Equal(failed, await w.SubmitAsync(f.Owner, request, default)); Assert.Equal(10, (await customers.WalletAsync(f.Owner, default)).Available); Assert.Equal(0, (await customers.WalletAsync(f.Owner, default)).Reserved);
        var original = (await w.TemplatesAsync(f.Owner, default)).Single(x => x.Id == f.Template).Settings; var b = await w.TemplateAsync(f.Editorial.Owner, original with { OperationId = Guid.NewGuid(), Version = 2, Adapter = "create.local.b.v1" }, default);
        var id = await w.SubmitAsync(f.Owner, new(Guid.NewGuid(), b, f.Profile, new("Learner", "kindness"), []), default); var good = await CreationTestFixture.Drain(s, f, id); Assert.Equal("create.local.b.v1", good.Adapter); Assert.Equal("create.local.a.v1", (await w.InspectAsync(f.Owner, failed, default)).Adapter); Assert.Equal(8, (await customers.WalletAsync(f.Owner, default)).Available);
    }
    private sealed class FailingRenderer : ICreationRenderer
    {
        public string Key => "create.local.a.v1"; public string Qualification => "LocalZeroCost";
        public void Validate(CreationMaterial m) => new ContentOS.Integrations.CreationRenderer().Validate(m);
        public Task<CreationRenderResult> RenderAsync(CreationRender r, CancellationToken ct) => throw new InvalidOperationException("Controlled local failure before artifact acceptance");
        public Task CleanupAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
    }
}
