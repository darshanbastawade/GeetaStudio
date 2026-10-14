using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

internal static partial class HistoricalTestFixture
{
    internal sealed record ReuseProof(Guid Index, Guid ShadowIndex, string Alias, KnowledgeQuery Query, KnowledgeResult Match, KnowledgeResult NoMatch, Guid Workflow, string WorkflowState, Guid FactoryBatch, Guid FactoryArtifact, Guid SocialPackage, string PublishingStatus);
    internal static async Task<ReuseProof> Pipeline(IServiceProvider s, Fixture f)
    {
        var db = s.GetRequiredService<CoreDbContext>(); var admin = s.GetRequiredService<IKnowledgeAdministration>(); var retriever = s.GetRequiredService<IKnowledgeRetriever>();
        var target = await (from t in db.CorpusTexts join v in db.CorpusVerses on t.VerseId equals v.Id where v.ManifestId == f.Manifest && t.ExactText.Contains("6th  June,  1674") select t).AsNoTracking().SingleAsync();
        var alias = "history-" + Guid.NewGuid().ToString("N"); var built = new List<KnowledgeIndexView>();
        foreach (var adapter in new[] { "sql-hybrid-v1", "fixture-ranking-v1" })
        {
            var id = await admin.QueueAsync(f.Owner, f.Universe, new(Guid.NewGuid(), alias + "-" + adapter, f.Manifest, "en-IN", "IN", adapter, "concept-projection-v1"), default);
            await s.GetRequiredService<IKnowledgeIndexWorker>().ProcessAsync(f.Owner, f.Universe, id, default);
            var index = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == id);
            await admin.ValidateAsync(f.Owner, f.Universe, id, new(index.RowVersion, "fixture:M26-technical-exact-excerpt-label-not-human-semantic-qualification", [new(target.ExactText, target.Id), new("How do I bake a pizza?", null)]), default);
            built.Add((await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == id));
        }
        var aliasId = await admin.ActivateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), alias, built[0].Id, "", built[0].EmbeddingSpaceId), default);
        var query = new KnowledgeQuery(alias, target.ExactText, "en-IN", "IN", built[0].EmbeddingSpaceId);
        var match = await retriever.RetrieveAsync(f.Owner, f.Universe, query, default);
        var noMatch = await retriever.RetrieveAsync(f.Owner, f.Universe, query with { Query = "How do I bake a pizza?" }, default);
        var current = await db.KnowledgeAliases.AsNoTracking().SingleAsync(x => x.Id == aliasId);
        await admin.ActivateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), alias, built[1].Id, Convert.ToBase64String(current.RowVersion), built[1].EmbeddingSpaceId), default);
        var switched = await retriever.RetrieveAsync(f.Owner, f.Universe, query, default);
        if (switched.Evidence.FirstOrDefault()?.CorpusTextId != target.Id) throw new InvalidOperationException("Historical ranking switch changed the technical exact-excerpt answer.");
        var workflow = s.GetRequiredService<IWorkflowRuntime>(); var ai = s.GetRequiredService<IAiAdministration>();
        var prompt = await workflow.RegisterPromptAsync(f.Owner, f.Universe, new(alias, 1, "Prepare an explicitly unapproved historical learning draft about {{topic}}. Cite the provided source IDs. Retain uncertainty; never invent historical speech.", [new("topic", 500)], "en-IN", f.Audience, RequireEvidence: true), default);
        var settings = new AiProviderSettings("fixture.text.a", "fixture-a.v1", "ai.text", "en-IN", true, false, false, 32768, 128, new("fixture-zero.v1", "USD", 0, 0, 0), new(["Public", "Internal"], "local", 0, false, "owned-policy.v1"), "OfflineVerified", "fixture:M26-not-historical-qualification");
        var profile = await ai.RegisterAsync(f.Owner, new(settings), default);
        var route = await ai.RouteAsync(f.Owner, new("draft", alias, [profile], .01m, "USD", DeadlineSeconds: 30, PromptVersion: alias + ".v1"), default);
        var previousHead = await db.AiRouteHeads.AsNoTracking().Where(x => x.TenantId == f.Owner.TenantId && x.Task == "draft").Select(x => (Guid?)x.RouteId).SingleOrDefaultAsync();
        await ai.ActivateAsync(f.Owner, route, default);
        var now = DateTimeOffset.UtcNow;
        if (!await db.AiBudgets.AnyAsync(x => x.TenantId == f.Owner.TenantId && x.Capability == "ai.text" && x.Currency == "USD" && x.StartsUtc <= now && x.EndsUtc > now))
            await ai.BudgetAsync(f.Owner, new("ai.text", "USD", now, now.AddMinutes(30), .1m), default);
        var definition = await workflow.RegisterDefinitionAsync(f.Owner, f.Universe, new(alias, 1, prompt, route, WorkflowRules.Sequence.Select(x => new WorkflowStepDefinition(x, x == "retrieve" ? "evidence.retrieve.v1" : x == "draft" ? "ai.generate.v1" : null)).ToArray(), .01m), default);
        if (previousHead != null) await ai.ActivateAsync(f.Owner, previousHead.Value, default);
        var request = new StartWorkflow(Guid.NewGuid(), definition, new() { ["topic"] = "source accounts of the 1674 coronation" }, Query: query, EvidenceContentRevisionId: f.Items["coronation"].RevisionId);
        var run = await workflow.StartAsync(f.Owner, f.Universe, request, default);
        if ((await workflow.StartAsync(f.Owner, f.Universe, request, default)).Id != run.Id) throw new InvalidOperationException("Workflow replay differs.");
        for (var i = 0; i < 30; i++)
        {
            run = await workflow.InspectAsync(f.Owner, run.Id, default);
            if (run.State is "AwaitingReview" or "Failed" or "Blocked") break;
            await s.GetRequiredService<IWorkflowWorker>().ProcessNextAsync(default);
        }
        if (run.State != "AwaitingReview") throw new InvalidOperationException("Historical fixture workflow failed: " + run.Outcome);
        // Deliberately leave generated output awaiting human review. Recorded fixtures never qualify historical truth.
        var factory = s.GetRequiredService<IDraftFactory>(); var lesson = f.Lessons["coronation"];
        var cast = new CastingBrief(f.Bible, f.Episode, f.Audience, "en-IN", ["history"], ["reflection"], ["evaluate-historical-sources"], ["Lead"], [], [], new(2, 0, "INR"), [new(f.Guide.CharacterId, "Lead")]);
        var plan = await factory.RegisterPlanAsync(f.Owner, f.Universe, new(Guid.NewGuid(), alias, 1, "Historical literacy draft proposal", "Chronological", "local-template.a.v1", [new("coronation", lesson.Id, cast, [], ["history"], "evaluate-historical-sources", 1, 11, "Historical source chronology", 1, 50, [new("Explanation", "Preserve accounts and uncertainty; human history review pending.")])]), default);
        await factory.ReviewPlanAsync(f.Owner, f.Universe, plan.Id, new(Guid.NewGuid(), plan.RowVersion, true, "fixture:M26-technical-factory-contract"), default);
        var batch = await factory.StartAsync(f.Owner, f.Universe, new(Guid.NewGuid(), alias, plan.Id, new(0, "INR", 20000, 600, 1)), default);
        for (var i = 0; i < 30; i++) { batch = (await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == batch.Id); if (batch.State is "Completed" or "Failed") break; await factory.ProcessNextAsync(default); }
        var artifact = batch.Work.Single().ArtifactId ?? throw new InvalidOperationException("Historical factory draft failed: " + batch.Outcome + " / " + batch.Work.Single().Outcome);
        var publishing = s.GetRequiredService<ISocialPublishing>(); var capability = s.GetRequiredService<ISocialCapabilityCatalog>().All.Single(x => x.Platform == "Facebook");
        var account = await publishing.RegisterAccountAsync(f.Owner, f.Universe, new(Guid.NewGuid(), alias, 1, capability.Platform, "fixture:historical-local-export", "social.fixture.a.v1", "Fixture", capability.Key, capability.RequiredScopes, null, "fixture:M26-no-platform-contact"), default);
        var package = await publishing.PrepareAsync(f.Owner, f.Universe, new(Guid.NewGuid(), alias, f.Items["coronation"].Id, account, "Article", "history-private", "", "", []), default);
        await publishing.ReviewAsync(f.Owner, f.Universe, package.Id, new(Guid.NewGuid(), package.RowVersion, package.PayloadHash, "Approve", "Fixture", "fixture:M26-private-export-technical-check"), default);
        var export = await publishing.ExportAsync(f.Owner, f.Universe, package.Id, default);
        return new(built[0].Id, built[1].Id, alias, query, match, noMatch, run.Id, run.State, batch.Id, artifact, package.Id, export.Status);
    }
}
