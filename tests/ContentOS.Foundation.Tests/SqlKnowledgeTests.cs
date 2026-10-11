using System.Net;
using System.Net.Http.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlKnowledgeTests : IAsyncLifetime
{
    private static readonly Guid Tenant = CoreSeed.StableId("tenant:local"), Universe = CoreSeed.StableId("universe:geeta");
    private static readonly CoreScope Owner = new(Tenant, "m06-test-owner");
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string Connection = "Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
    private static ServiceProvider Services(IEmbeddingProvider? provider = null, IKnowledgeRankingAdapter? additional = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ContentOS"] = Connection }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddContentCore(config); services.AddSingleton<IEmbeddingProvider>(provider ?? new ReviewedConceptEmbedding());
        services.AddSingleton<IKnowledgeRankingAdapter, SqlHybridRanking>(); services.AddSingleton<IKnowledgeRankingAdapter, IndependentFixtureRanking>(); if (additional != null) services.AddSingleton(additional); return services.BuildServiceProvider();
    }
    public async Task InitializeAsync()
    {
        using var root = Services(); using var scope = root.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await CoreSeed.SeedAsync(db, Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M02-universes.json"), Owner.Actor);
        if (!await db.Locales.AnyAsync(l => l.Id == "sa-IN")) db.Locales.Add(new() { Id = "sa-IN", Name = "Sanskrit" });
        foreach (var pair in new[] { ("m06-reader", "Reader"), ("integration-test-Administrator", "Administrator"), ("integration-test-Reader", "Reader") })
            if (!await db.Members.AnyAsync(m => m.TenantId == Tenant && m.Actor == pair.Item1)) db.Members.Add(new() { TenantId = Tenant, Actor = pair.Item1, Permission = pair.Item2 });
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private sealed record Fixture(Guid Manifest, Guid Text, Guid Snapshot, Guid Claim);
    private static async Task<Fixture> Setup(IServiceProvider s)
    {
        var quote = await EvidenceTestFixture.TranslationAsync(s, Owner, Universe); var db = s.GetRequiredService<CoreDbContext>();
        var span = await db.ClaimSpans.AsNoTracking().SingleAsync(x => x.ClaimRevisionId == quote.Revision);
        var t = await db.CorpusTexts.AsNoTracking().SingleAsync(x => x.Id == span.CorpusTextId);
        var verse = await db.CorpusVerses.AsNoTracking().SingleAsync(x => x.Id == t.VerseId);
        var corpus = s.GetRequiredService<ICanonicalCorpus>();
        foreach (var item in await corpus.ChapterAsync(Owner, Universe, verse.ManifestId, 1, "IN", Ct))
            if (item.ReviewStatus == "Pending") await corpus.ReviewAsync(Owner, Universe, verse.ManifestId, item.Id, new(item.RowVersion, "Approved", "evidence:M04-automated-source-label-hash-checks"), Ct);
        return new(verse.ManifestId, t.Id, span.SnapshotId, quote.Revision);
    }
    private static async Task<KnowledgeIndexView> Build(IServiceProvider s, Fixture f, string adapter = "sql-hybrid-v1", string model = "concept-projection-v1")
    {
        var admin = s.GetRequiredService<IKnowledgeAdministration>();
        var id = await admin.QueueAsync(Owner, Universe, new(Guid.NewGuid(), "index-" + Guid.NewGuid(), f.Manifest, "en-IN", "IN", adapter, model), Ct);
        await s.GetRequiredService<IKnowledgeIndexWorker>().ProcessNextAsync(Ct);
        return (await admin.IndicesAsync(Owner, Universe, Ct)).Single(x => x.Id == id);
    }
    private static async Task<KnowledgeIndexView> Validate(IServiceProvider s, KnowledgeIndexView index, Guid target, bool valid = true)
    {
        var admin = s.GetRequiredService<IKnowledgeAdministration>();
        await admin.ValidateAsync(Owner, Universe, index.Id, new(index.RowVersion, "manual:M06-user-approved-two-labels-2026-10-10",
        [new("Where did the opposing armies gather?", valid ? target : Guid.NewGuid()), new("How do I bake a pizza?", null)]), Ct);
        return (await admin.IndicesAsync(Owner, Universe, Ct)).Single(x => x.Id == index.Id);
    }
    private static async Task<KnowledgeAliasView> Activate(IServiceProvider s, KnowledgeIndexView index, string key, string row = "")
    {
        var admin = s.GetRequiredService<IKnowledgeAdministration>(); await admin.ActivateAsync(Owner, Universe, new(Guid.NewGuid(), key, index.Id, row, index.EmbeddingSpaceId), Ct);
        return (await admin.AliasesAsync(Owner, Universe, Ct)).Single(x => x.Key == key);
    }
    [SqlFact]
    public async Task Known_no_answer_swapped_adapter_and_same_dimension_cutover_rollback_preserve_contract()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s);
        var first = await Validate(s, await Build(s, f), f.Text); Assert.NotNull(first.ValidationEvidence);
        var alias = await Activate(s, first, "alias-" + Guid.NewGuid()); var retriever = s.GetRequiredService<IKnowledgeRetriever>();
        var question = new KnowledgeQuery(alias.Key, "Where did the opposing armies gather?", "en-IN", "IN", first.EmbeddingSpaceId);
        var answer = await retriever.RetrieveAsync(Owner, Universe, question, Ct); Assert.Contains(answer.Evidence, x => x.CorpusTextId == f.Text && x.Verse == 1 && x.Label == "Translation" && x.Citation.RequiredAttribution.Contains("CC BY-SA"));
        try
        {
            var generation = await retriever.RetrieveAsync(Owner, Universe, question with { Use = new("Generate", "en-IN", "IN", "Text") }, Ct);
            Assert.Empty(generation.Evidence);
        }
        catch (KnowledgeBlockedException blocked)
        {
            Assert.Contains(blocked.Reason, new[] { "StaleGraphProvenance", "StaleClaimEvidence" });
        }
        Assert.Equal("NoSupportedMatch", (await retriever.RetrieveAsync(Owner, Universe, question with { Query = "How do I bake a pizza?" }, Ct)).Outcome);
        var next = await Validate(s, await Build(s, f, "fixture-ranking-v1", "concept-projection-v2"), f.Text);
        Assert.Equal(first.Dimensions, next.Dimensions); Assert.NotEqual(first.EmbeddingSpaceId, next.EmbeddingSpaceId);
        var admin = s.GetRequiredService<IKnowledgeAdministration>();
        await Assert.ThrowsAsync<KnowledgeBlockedException>(() => admin.ActivateAsync(Owner, Universe, new(Guid.NewGuid(), alias.Key, next.Id, alias.RowVersion, first.EmbeddingSpaceId), Ct));
        alias = await Activate(s, next, alias.Key, alias.RowVersion);
        await Assert.ThrowsAsync<KnowledgeBlockedException>(() => retriever.RetrieveAsync(Owner, Universe, question, Ct));
        var switched = await retriever.RetrieveAsync(Owner, Universe, question with { EmbeddingSpaceId = next.EmbeddingSpaceId }, Ct);
        Assert.Equal(answer.Evidence.Single(h => h.CorpusTextId == f.Text).ChunkId, switched.Evidence.Single(h => h.CorpusTextId == f.Text).ChunkId); Assert.Equal(answer.Evidence.Single(h => h.CorpusTextId == f.Text).ExactText, switched.Evidence.Single(h => h.CorpusTextId == f.Text).ExactText);
        alias = await Activate(s, first, alias.Key, alias.RowVersion); Assert.Equal(next.Id, alias.PreviousIndexId);
        Assert.Equal(first.Id, (await retriever.RetrieveAsync(Owner, Universe, question, Ct)).IndexId);
    }
    [SqlFact]
    public async Task Failed_shadow_validation_and_stale_alias_updates_cannot_activate()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s);
        var index = await Validate(s, await Build(s, f), f.Text, false); var admin = s.GetRequiredService<IKnowledgeAdministration>();
        Assert.Null(index.ValidationEvidence); await Assert.ThrowsAsync<KnowledgeBlockedException>(() => Activate(s, index, "failed-" + Guid.NewGuid()));
        index = await Validate(s, index, f.Text); var alias = await Activate(s, index, "valid-" + Guid.NewGuid()); var old = alias.RowVersion;
        alias = await Activate(s, index, alias.Key, alias.RowVersion);
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => Activate(s, index, alias.Key, old));
        index = await Validate(s, index, f.Text, false);
        var blocked = await Assert.ThrowsAsync<KnowledgeBlockedException>(() => s.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(Owner, Universe, new(alias.Key, "field", "en-IN", "IN", index.EmbeddingSpaceId), Ct));
        Assert.Equal("UnvalidatedIndex", blocked.Reason);
    }
    [SqlFact]
    public async Task Revoked_source_filters_evidence_and_citations_and_invalidates_cutover()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s);
        var index = await Validate(s, await Build(s, f), f.Text); var alias = await Activate(s, index, "rights-" + Guid.NewGuid());
        var db = s.GetRequiredService<CoreDbContext>(); var work = await (from snap in db.SourceSnapshots join edition in db.SourceEditions on snap.EditionId equals edition.Id where snap.Id == f.Snapshot select edition.WorkId).SingleAsync();
        var sources = s.GetRequiredService<ISourceRegistry>(); var detail = await sources.InspectAsync(Owner, Universe, work, Ct);
        await sources.RevokeAsync(Owner, Universe, work, new(Guid.NewGuid(), null, "fixture:M06-takedown", detail.Work.RowVersion), Ct);
        var result = await s.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(Owner, Universe, new(alias.Key, "field of Kuru", "en-IN", "IN", index.EmbeddingSpaceId), Ct);
        Assert.Empty(result.Evidence); Assert.Equal("NoSupportedMatch", result.Outcome);
        await Assert.ThrowsAsync<KnowledgeBlockedException>(() => Activate(s, index, alias.Key, alias.RowVersion));
        await Assert.ThrowsAsync<CoreAccessException>(() => s.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new(CoreSeed.StableId("tenant:isolated"), Owner.Actor), Universe, new(alias.Key, "field", "en-IN", "IN", index.EmbeddingSpaceId), Ct));
    }
    [SqlFact]
    public async Task Pending_edges_are_not_projected_and_reviewed_aliases_and_claim_corrections_are_pinned()
    {
        using var root = Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var admin = s.GetRequiredService<IKnowledgeAdministration>();
        var a = await admin.ConceptAsync(Owner, Universe, new(Guid.NewGuid(), "field-" + Guid.NewGuid(), f.Snapshot, [new("en-IN", "field", "Name"), new("en-IN", "battlefield", "Alias"), new("sa-IN", "क्षेत्र", "Translation")], f.Claim), Ct);
        var b = await admin.ConceptAsync(Owner, Universe, new(Guid.NewGuid(), "gathering-" + Guid.NewGuid(), f.Snapshot, [new("en-IN", "gather", "Name")]), Ct);
        foreach (var id in new[] { a, b }) { var c = (await admin.GraphAsync(Owner, Universe, Ct)).Concepts.Single(c => c.Id == id); await admin.ReviewAsync(Owner, Universe, id, new(c.RowVersion, "Approved", "manual:M06-controlled-concept-review"), Ct); }
        var edge = await admin.RelationAsync(Owner, Universe, new(Guid.NewGuid(), a, b, "Related", f.Snapshot), Ct);
        var first = await Build(s, f); var db = s.GetRequiredService<CoreDbContext>(); var graph = await db.KnowledgeIndices.AsNoTracking().Where(x => x.Id == first.Id).Select(x => x.GraphJson).SingleAsync();
        Assert.DoesNotContain(edge.ToString(), graph);
        var pending = (await admin.GraphAsync(Owner, Universe, Ct)).Relations.Single(e => e.Id == edge);
        await admin.ReviewAsync(Owner, Universe, edge, new(pending.RowVersion, "Approved", "manual:M06-reviewed-relation"), Ct);
        var second = await Validate(s, await Build(s, f), f.Text); Assert.NotEqual(first.EmbeddingSpaceId, second.EmbeddingSpaceId);
        var alias = await Activate(s, second, "concept-" + Guid.NewGuid()); var retriever = s.GetRequiredService<IKnowledgeRetriever>();
        Assert.Contains((await retriever.RetrieveAsync(Owner, Universe, new(alias.Key, "battlefield", "en-IN", "IN", second.EmbeddingSpaceId), Ct)).Evidence, h => h.CorpusTextId == f.Text);
        await s.GetRequiredService<IEvidenceEngine>().CorrectAsync(Owner, Universe, new(Guid.NewGuid(), f.Claim, "Attribution disputed", "fixture:M06-concept-correction"), Ct);
        Assert.Equal("StaleGraphProvenance", (await Assert.ThrowsAsync<KnowledgeBlockedException>(() => retriever.RetrieveAsync(Owner, Universe, new(alias.Key, "battlefield", "en-IN", "IN", second.EmbeddingSpaceId), Ct))).Reason);
    }
    private sealed class InterruptOnce : IEmbeddingProvider
    {
        private bool interrupted; private readonly ReviewedConceptEmbedding actual = new();
        public bool Supports(string model, int dimensions, string normalization) => actual.Supports(model, dimensions, normalization);
        public ValueTask<OwnedEmbedding> EmbedAsync(EmbeddingRequest r, CancellationToken ct) { if (!interrupted) { interrupted = true; throw new OperationCanceledException(); } return actual.EmbedAsync(r, ct); }
    }
    [SqlFact]
    public async Task Interrupted_index_build_is_atomic_and_recovers_with_pinned_space()
    {
        using var root = Services(new InterruptOnce()); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s); var admin = s.GetRequiredService<IKnowledgeAdministration>();
        var request = new QueueKnowledgeIndex(Guid.NewGuid(), "recover-" + Guid.NewGuid(), f.Manifest, "en-IN", "IN", "sql-hybrid-v1", "concept-projection-v1");
        var id = await admin.QueueAsync(Owner, Universe, request, Ct); Assert.Equal(id, await admin.QueueAsync(Owner, Universe, request, Ct));
        await Assert.ThrowsAsync<RuntimeConflictException>(() => admin.QueueAsync(Owner, Universe, request with { Model = "concept-projection-v2" }, Ct));
        await Assert.ThrowsAsync<OperationCanceledException>(() => s.GetRequiredService<IKnowledgeIndexWorker>().ProcessNextAsync(Ct));
        using var recovered = root.CreateScope(); var service = recovered.ServiceProvider; var before = (await service.GetRequiredService<IKnowledgeAdministration>().IndicesAsync(Owner, Universe, Ct)).Single(i => i.Id == id);
        Assert.Equal("Queued", before.State); Assert.Equal(0, before.Chunks);
        Assert.True(await service.GetRequiredService<IKnowledgeIndexWorker>().ProcessNextAsync(Ct));
        var after = (await service.GetRequiredService<IKnowledgeAdministration>().IndicesAsync(Owner, Universe, Ct)).Single(i => i.Id == id);
        Assert.Equal("Ready", after.State); Assert.Equal(before.EmbeddingSpaceId, after.EmbeddingSpaceId); Assert.Equal(25, after.Chunks);
        var db = service.GetRequiredService<CoreDbContext>(); var chunk = await db.KnowledgeChunks.FirstAsync(c => c.IndexId == id); chunk.VectorJson = "[]";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task Knowledge_api_requires_membership_header_admin_controls_and_authentication()
    {
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Staff:Administrator:0"] = "integration-test-Administrator",
                ["Staff:Reader:0"] = "integration-test-Reader",
                ["Runtime:Enabled"] = "true",
                ["Knowledge:IndexWorkerEnabled"] = "false",
                ["ConnectionStrings:ContentOS"] = Connection,
                ["Assets:Root"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "test-assets")
            }));
            builder.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = factory.CreateClient(); var url = $"/api/v1/knowledge/tenants/{Tenant}/universes/{Universe}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/staff/knowledge")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator"); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/staff/knowledge")).StatusCode);
        var request = new QueueKnowledgeIndex(Guid.NewGuid(), "api-" + Guid.NewGuid(), Guid.NewGuid(), "en-IN", "IN", "sql-hybrid-v1", "concept-projection-v1");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url + "/indices", request)).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1"); client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url + "/indices", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url + "/graph")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/knowledge/tenants/{CoreSeed.StableId("tenant:isolated")}/universes/{Universe}/graph")).StatusCode);
    }

    private sealed class InvalidRanking : IKnowledgeRankingAdapter
    {
        public string Key => "invalid-contract-fixture";
        public ValueTask<IReadOnlyList<KnowledgeScore>> RankAsync(KnowledgeRanking request, CancellationToken ct) =>
            ValueTask.FromResult<IReadOnlyList<KnowledgeScore>>([new(Guid.NewGuid(), 1)]);
    }
    [SqlFact]
    public async Task Adapter_cannot_return_invented_or_unfiltered_chunk_ids()
    {
        using var root = Services(additional: new InvalidRanking()); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await Setup(s);
        var index = await Build(s, f, "invalid-contract-fixture");
        var error = await Assert.ThrowsAsync<KnowledgeBlockedException>(() => Validate(s, index, f.Text));
        Assert.Equal("InvalidRetrievalContract", error.Reason);
        Assert.Null((await s.GetRequiredService<IKnowledgeAdministration>().IndicesAsync(Owner, Universe, Ct)).Single(i => i.Id == index.Id).ValidationEvidence);
    }
}
