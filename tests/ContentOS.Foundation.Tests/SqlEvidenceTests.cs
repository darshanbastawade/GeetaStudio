using System.Net.Http.Json;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlEvidenceTests : IAsyncLifetime
{
    private static readonly Guid Tenant = CoreSeed.StableId("tenant:local");
    private static readonly Guid Universe = CoreSeed.StableId("universe:geeta");
    private static readonly CoreScope Owner = new(Tenant, "m05-test-owner");
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static ServiceProvider Services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ContentOS"] = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
        }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddContentCore(config); return services.BuildServiceProvider();
    }
    public async Task InitializeAsync()
    {
        using var root = Services(); using var scope = root.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await CoreSeed.SeedAsync(db, Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M02-universes.json"), Owner.Actor);
        if (!await db.Locales.AnyAsync(l => l.Id == "sa-IN")) db.Locales.Add(new ContentLocale { Id = "sa-IN", Name = "Sanskrit (India)" });
        if (!await db.Members.AnyAsync(m => m.TenantId == Tenant && m.Actor == "m05-reader"))
            db.Members.Add(new TenantMember { TenantId = Tenant, Actor = "m05-reader", Permission = "Reader" });
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private static async Task<ContentView> ContentAsync(IContentCore core, string text, string kind = "Interpretation")
    {
        var catalog = await core.CatalogAsync(Owner, Universe, Ct);
        return await core.CreateAsync(Owner, Universe, new(Guid.NewGuid(), catalog.Subjects[0].Id, catalog.Manifest.Audiences[0].Id,
            "en-IN", "Original M05 test fixture", text, kind, "Licensed", "Original fixture only"), Ct);
    }
    private static async Task<SubmitClaim> RequestAsync(IEvidenceEngine engine, Guid snapshot, string type, string statement)
    {
        var text = Encoding.UTF8.GetString(SourceTestFixture.Bytes);
        return new(Guid.NewGuid(), "claim-" + Guid.NewGuid(), "", type, type, "en-IN", statement, "Original test critic", "Original test viewpoint",
            type == "Quotation" ? "SourceExact" : "Contested", await EvidenceTestFixture.RubricAsync(engine, Owner, Universe),
            [new(snapshot, 0, text.Length, text, EvidenceTestFixture.Hash(text), "sentence 1", "Supporting")]);
    }
    [SqlFact]
    public async Task Conflicting_interpretations_remain_distinct_attributed_and_export_labeled()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var engine = s.GetRequiredService<IEvidenceEngine>(); var core = s.GetRequiredService<IContentCore>();
        var source = await SourceTestFixture.CreateAsync(s.GetRequiredService<ISourceRegistry>(), Owner, Universe);
        var left = await EvidenceTestFixture.InterpretationAsync(s, Owner, Universe, source.Snapshot, "Original viewpoint A: sharing tea is preferable.");
        var right = await EvidenceTestFixture.InterpretationAsync(s, Owner, Universe, source.Snapshot, "Original viewpoint B: saving tea is preferable.");
        var disagreement = await engine.DisagreeAsync(Owner, Universe, new(Guid.NewGuid(), left, right, "Opposing original fixture readings", "fixture:two-viewpoints"), Ct);
        Assert.Contains(await engine.DisagreementsAsync(Owner, Universe, Ct), d => d.Id == disagreement && d.LeftRevisionId == left && d.RightRevisionId == right);
        foreach (var id in new[] { left, right })
        {
            var revision = (await engine.ListAsync(Owner, Universe, Ct)).SelectMany(c => c.Revisions).Single(r => r.Id == id);
            var content = await ContentAsync(core, revision.Statement);
            await engine.LinkAsync(Owner, Universe, new(Guid.NewGuid(), id, content.Revisions[0].Id, 0, revision.Statement.Length), Ct);
            var export = await core.ExportAsync(Owner, Universe, content.Id, Ct);
            Assert.Contains(export.EvidenceLabels!, l => l.Label == "Interpretation" && l.Uncertainty == "Contested" && l.ClaimRevisionId == id);
            Assert.NotEmpty(export.Citations!);
        }
    }
    [SqlFact]
    public async Task Plausible_locator_cannot_approve_invented_quotation_or_forged_span()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var engine = s.GetRequiredService<IEvidenceEngine>();
        var source = await SourceTestFixture.CreateAsync(s.GetRequiredService<ISourceRegistry>(), Owner, Universe);
        var request = await RequestAsync(engine, source.Snapshot, "Quotation", "Invented scripture quotation");
        var id = await engine.SubmitAsync(Owner, Universe, request, Ct);
        var error = await Assert.ThrowsAsync<EvidenceBlockedException>(() => EvidenceTestFixture.ApproveAsync(engine, Owner, Universe, id, "DirectSource", "Supported"));
        Assert.Equal("UnsupportedQuotation", error.Reason);
        var forged = request.Evidence[0] with { ExactText = "Invented scripture quotation", Length = 26, Sha256 = EvidenceTestFixture.Hash("Invented scripture quotation") };
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => engine.SubmitAsync(Owner, Universe, request with { OperationId = Guid.NewGuid(), Key = "forged-" + Guid.NewGuid(), Evidence = [forged] }, Ct));
        Assert.Equal("Pending", (await engine.ListAsync(Owner, Universe, Ct)).SelectMany(c => c.Revisions).Single(r => r.Id == id).ReviewStatus);
    }
    [SqlFact]
    public async Task Historical_and_news_require_independent_exact_evidence_not_scripture_confidence()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var engine = s.GetRequiredService<IEvidenceEngine>();
        var sources = s.GetRequiredService<ISourceRegistry>(); var one = await SourceTestFixture.CreateAsync(sources, Owner, Universe);
        var two = await SourceTestFixture.CreateAsync(sources, Owner, Universe);
        var text = Encoding.UTF8.GetString(SourceTestFixture.Bytes);
        foreach (var type in new[] { "HistoricalClaim", "NewsClaim" })
        {
            var request = await RequestAsync(engine, one.Snapshot, type, text);
            var id = await engine.SubmitAsync(Owner, Universe, request, Ct);
            Assert.Equal("InsufficientEvidence", (await Assert.ThrowsAsync<EvidenceBlockedException>(() => EvidenceTestFixture.ApproveAsync(engine, Owner, Universe, id, "Corroborated", "Supported"))).Reason);
            var second = request.Evidence[0] with { SnapshotId = two.Snapshot };
            var supported = await engine.SubmitAsync(Owner, Universe, request with { OperationId = Guid.NewGuid(), Key = "corroborated-" + Guid.NewGuid(), Evidence = [request.Evidence[0], second] }, Ct);
            await EvidenceTestFixture.ApproveAsync(engine, Owner, Universe, supported, "Corroborated", "Supported");
            var contested = await engine.SubmitAsync(Owner, Universe, request with
            {
                OperationId = Guid.NewGuid(),
                Key = "contradiction-" + Guid.NewGuid(),
                Evidence = [request.Evidence[0], second, second with { Relation = "Contradicting" }]
            }, Ct);
            await Assert.ThrowsAsync<EvidenceBlockedException>(() => EvidenceTestFixture.ApproveAsync(engine, Owner, Universe, contested, "Corroborated", "Supported"));
        }
        // Synthetic exact statements test the gate mechanics, never qualify a real historical source.
    }
    [SqlFact]
    public async Task Correction_identifies_descendants_and_blocks_old_approval_export_and_ledger()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var engine = s.GetRequiredService<IEvidenceEngine>();
        var core = s.GetRequiredService<IContentCore>(); var sources = s.GetRequiredService<ISourceRegistry>();
        var source = await SourceTestFixture.CreateAsync(sources, Owner, Universe); const string body = "Original interpretation of sharing tea.";
        var claim = await EvidenceTestFixture.InterpretationAsync(s, Owner, Universe, source.Snapshot, body);
        var item = await ContentAsync(core, body);
        await engine.LinkAsync(Owner, Universe, new(Guid.NewGuid(), claim, item.Revisions[0].Id, 0, body.Length), Ct);
        item = await core.ReviseAsync(Owner, Universe, item.Id, new(Guid.NewGuid(), item.RowVersion, "Title correction", body, "Interpretation", "Licensed", "fixture"), Ct);
        Assert.True((await engine.CheckAsync(Owner, Universe, item.Revisions.Last().Id, new("Read", "en-IN", "IN", "Text"), Ct)).Eligible);
        var correction = await engine.CorrectAsync(Owner, Universe, new(Guid.NewGuid(), claim, "Attribution requires correction", "fixture:correction"), Ct);
        var impact = await engine.ImpactAsync(Owner, Universe, claim, Ct);
        Assert.Equal(2, impact.ContentRevisionIds.Count);
        Assert.False((await engine.CheckAsync(Owner, Universe, item.Revisions.Last().Id, new("Read", "en-IN", "IN", "Text"), Ct)).Eligible);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => core.ExportAsync(Owner, Universe, item.Id, Ct));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => sources.RecordUseAsync(Owner, Universe, new(Guid.NewGuid(), item.Revisions.Last().Id, new("Publish", "en-IN", "IN", "Text"), false, "fixture:M05-dry-run", "Fixture"), Ct));
        item = await core.TransitionAsync(Owner, Universe, item.Id, new(Guid.NewGuid(), item.RowVersion, "Review"), Ct);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => core.TransitionAsync(Owner, Universe, item.Id, new(Guid.NewGuid(), item.RowVersion, "Approved"), Ct));
        var head = (await engine.ListAsync(Owner, Universe, Ct)).Single(c => c.Revisions.Any(r => r.Id == claim));
        var request = await RequestAsync(engine, source.Snapshot, "Interpretation", body);
        var replacement = await engine.SubmitAsync(Owner, Universe, request with { Key = head.Key, ExpectedRowVersion = head.RowVersion, CorrectionId = correction, Author = "Corrected original fixture critic" }, Ct);
        await EvidenceTestFixture.ApproveAsync(engine, Owner, Universe, replacement, "Attributed", "Contested");
        var history = await engine.InspectAsync(Owner, Universe, head.Id, Ct);
        Assert.Equal(2, history.CurrentVersion); Assert.Equal(2, history.Revisions.Count);
        Assert.Equal("Superseded", (await engine.ImpactAsync(Owner, Universe, claim, Ct)).Corrections.Single().Status);
        Assert.False((await engine.CheckAsync(Owner, Universe, item.Revisions[0].Id, new("Read", "en-IN", "IN", "Text"), Ct)).Eligible);
    }
    [SqlFact]
    public async Task Commands_replay_immutably_and_review_requires_authorized_current_version()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var engine = s.GetRequiredService<IEvidenceEngine>();
        var source = await SourceTestFixture.CreateAsync(s.GetRequiredService<ISourceRegistry>(), Owner, Universe);
        var request = await RequestAsync(engine, source.Snapshot, "Interpretation", "Original fixture reading");
        var id = await engine.SubmitAsync(Owner, Universe, request, Ct);
        Assert.Equal(id, await engine.SubmitAsync(Owner, Universe, request, Ct));
        await Assert.ThrowsAsync<RuntimeConflictException>(() => engine.SubmitAsync(Owner, Universe, request with { Statement = "changed" }, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => engine.SubmitAsync(new(Tenant, "m05-reader"), Universe, request with { OperationId = Guid.NewGuid() }, Ct));
        await Assert.ThrowsAsync<CoreAccessException>(() => engine.InspectAsync(new(CoreSeed.StableId("tenant:isolated"), Owner.Actor), Universe, id, Ct));
        var head = (await engine.ListAsync(Owner, Universe, Ct)).Single(c => c.Revisions.Any(r => r.Id == id));
        await engine.SubmitAsync(Owner, Universe, request with { OperationId = Guid.NewGuid(), ExpectedRowVersion = head.RowVersion }, Ct);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => engine.ReviewAsync(Owner, Universe, id, new(head.Revisions[0].RowVersion, "Approved", "Attributed", "Contested", "fixture:stale"), Ct));
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => engine.SubmitAsync(Owner, Universe, request with { OperationId = Guid.NewGuid(), ExpectedRowVersion = head.RowVersion }, Ct));
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear();
        var span = await db.ClaimSpans.FirstAsync(x => x.ClaimRevisionId == id); span.ExactText = "overwrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task Unlabeled_segments_and_kind_mismatches_cannot_pass_the_gate()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var engine = s.GetRequiredService<IEvidenceEngine>(); var core = s.GetRequiredService<IContentCore>();
        var source = await SourceTestFixture.CreateAsync(s.GetRequiredService<ISourceRegistry>(), Owner, Universe);
        const string body = "Original interpretation.";
        var claim = await EvidenceTestFixture.InterpretationAsync(s, Owner, Universe, source.Snapshot, body);
        var item = await ContentAsync(core, body + " Unsupported extra assertion.");
        await engine.LinkAsync(Owner, Universe, new(Guid.NewGuid(), claim, item.Revisions[0].Id, 0, body.Length), Ct);
        Assert.Equal("UnlabeledContent", (await engine.CheckAsync(Owner, Universe, item.Revisions[0].Id, new("Read", "en-IN", "IN", "Text"), Ct)).Reason);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => engine.LinkAsync(Owner, Universe, new(Guid.NewGuid(), claim, item.Revisions[0].Id, 0, body.Length), Ct));
        var canonical = await ContentAsync(core, body, "Canonical");
        await engine.LinkAsync(Owner, Universe, new(Guid.NewGuid(), claim, canonical.Revisions[0].Id, 0, body.Length), Ct);
        Assert.Equal("ContentLabelMismatch", (await engine.CheckAsync(Owner, Universe, canonical.Revisions[0].Id, new("Read", "en-IN", "IN", "Text"), Ct)).Reason);
    }
    [SqlFact]
    public async Task Exact_translation_requires_bound_approved_corpus_and_exports_effective_version()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var engine = s.GetRequiredService<IEvidenceEngine>(); var core = s.GetRequiredService<IContentCore>();
        var evidence = await EvidenceTestFixture.TranslationAsync(s, Owner, Universe);
        var item = await ContentAsync(core, evidence.Text, "Translation");
        await engine.LinkAsync(Owner, Universe, new(Guid.NewGuid(), evidence.Revision, item.Revisions[0].Id, 0, evidence.Text.Length), Ct);
        var export = await core.ExportAsync(Owner, Universe, item.Id, Ct);
        Assert.Single(export.EvidenceLabels!); Assert.Equal("Translation", export.EvidenceLabels![0].Label);
        Assert.Equal(1, export.EvidenceLabels[0].EffectiveVersion); Assert.Contains(export.Citations!, c => c.RequiredAttribution.Contains("CC BY-SA"));
    }

    [SqlFact]
    public async Task Evidence_api_requires_staff_header_membership_and_admin_review()
    {
        using var root = Services(); using var setup = root.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<CoreDbContext>();
        foreach (var pair in new[] { ("integration-test-Administrator", "Administrator"), ("integration-test-Reader", "Reader") })
            if (!await db.Members.AnyAsync(m => m.TenantId == Tenant && m.Actor == pair.Item1))
                db.Members.Add(new TenantMember { TenantId = Tenant, Actor = pair.Item1, Permission = pair.Item2 });
        await db.SaveChangesAsync();
        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Staff:Administrator:0"] = "integration-test-Administrator",
                ["Staff:Reader:0"] = "integration-test-Reader",
                ["Runtime:Enabled"] = "true",
                ["ConnectionStrings:ContentOS"] = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;",
                ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets")
            }));
            builder.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = factory.CreateClient();
        var url = $"/api/v1/evidence/tenants/{Tenant}/universes/{Universe}";
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/staff/evidence")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/staff/evidence")).StatusCode);
        var rubric = new RegisterEvidenceRubric(Guid.NewGuid(), "api-" + Guid.NewGuid(), 1, "Explicit test assessment");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url + "/rubrics", rubric)).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.PostAsJsonAsync(url + "/rubrics", rubric)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url + "/rubrics", rubric with { OperationId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url + "/revisions/" + Guid.NewGuid() + "/review", new ReviewClaim("", "Approved", "Attributed", "Contested", "fixture:test"))).StatusCode);
        var foreign = CoreSeed.StableId("tenant:isolated");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/evidence/tenants/{foreign}/universes/{Universe}/")).StatusCode);
    }

    [SqlFact]
    public async Task Concurrent_claim_replay_creates_one_pinned_revision()
    {
        using var root = Services(); using var setup = root.CreateScope();
        var source = await SourceTestFixture.CreateAsync(setup.ServiceProvider.GetRequiredService<ISourceRegistry>(), Owner, Universe);
        var request = await RequestAsync(setup.ServiceProvider.GetRequiredService<IEvidenceEngine>(), source.Snapshot, "Interpretation", "Original concurrent fixture viewpoint");
        using var first = root.CreateScope(); using var second = root.CreateScope();
        var ids = await Task.WhenAll(first.ServiceProvider.GetRequiredService<IEvidenceEngine>().SubmitAsync(Owner, Universe, request, Ct),
            second.ServiceProvider.GetRequiredService<IEvidenceEngine>().SubmitAsync(Owner, Universe, request, Ct));
        Assert.Equal(ids[0], ids[1]);
        var db = setup.ServiceProvider.GetRequiredService<CoreDbContext>();
        Assert.Equal(1, await db.EvidenceCommands.CountAsync(c => c.OperationId == request.OperationId));
        Assert.Equal(1, await db.ClaimRevisions.CountAsync(r => r.Id == ids[0]));
    }
}
