using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

// Closed typed registry. No string-to-code dispatch, network URL, MCP server or model-selected tool.
public sealed class WorkflowTools(CoreDbContext db, IKnowledgeRetriever retrieval, IAiTaskRouter ai) : IToolInvoker
{
    public IReadOnlyList<WorkflowTool> Tools { get; } =
    [
        new("evidence.retrieve.v1", "workflow-inputs.v1", "workflow-evidence.v1", "Administrator+owner", "ReadWithRightsAudit"),
        new("ai.generate.v1", "generate-ai.v1", "generated-text.v1", "Administrator+owner", "BudgetedGeneration")
    ];
    public async Task<WorkflowEvidence> RetrieveAsync(CoreScope s, Guid u, StartWorkflow r, string locale, CancellationToken ct)
    {
        var membership = await db.Members.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor, ct);
        if (membership?.Permission != "Administrator" || !await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
        var factoryWork = await db.FactoryWorks.AsNoTracking().SingleOrDefaultAsync(w => w.Id == r.OperationId && w.WorkflowId != null && w.TenantId == s.TenantId && w.UniverseId == u, ct);
        var ids = new HashSet<Guid>(r.SourceSnapshotIds ?? []); var data = new List<string>(); KnowledgeResult? found = null;
        if (r.Query != null)
        {
            // Historical local embeddings only here: additional remote embedding costs are not outside the workflow bound.
            var model = await (from alias in db.KnowledgeAliases
                               join index in db.KnowledgeIndices on alias.ActiveIndexId equals index.Id
                               where alias.UniverseId == u && alias.Locale == r.Query.Locale && alias.Key == r.Query.Alias && index.EmbeddingSpaceId == r.Query.EmbeddingSpaceId
                               select index.Model).SingleOrDefaultAsync(ct);
            if (model is not ("concept-projection-v1" or "concept-projection-v2")) throw new RuntimeValidationException("Workflow retrieval requires a local historical embedding space.");
            found = await retrieval.RetrieveAsync(s, u, r.Query with { Use = new("Generate", locale, r.Query.Territory, "Text", Adaptation: true, AiProcessing: true) }, ct);
            if (found.Model is not ("concept-projection-v1" or "concept-projection-v2")) throw new RuntimeValidationException("Workflow retrieval requires a local historical embedding space.");
            foreach (var hit in found.Evidence) { ids.Add(hit.SnapshotId); data.Add(hit.SnapshotId + ": " + hit.ExactText); }
        }
        if (ids.Count > 10) throw new RuntimeValidationException("Evidence source limit.");
        foreach (var id in ids)
        {
            var snapshot = await (from snap in db.SourceSnapshots
                                  join e in db.SourceEditions on snap.EditionId equals e.Id
                                  join w in db.SourceWorks on e.WorkId equals w.Id
                                  where snap.Id == id && w.TenantId == s.TenantId && w.UniverseId == u
                                  select snap).AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
            var decision = await new SourceRightsGuard(db).DecideAsync(s, id, new("Generate", locale, "IN", "Text", Adaptation: true, AiProcessing: true), ct);
            if (!decision.Decision.Allowed) throw new RightsBlockedException(decision.Decision.Reason);
            if (factoryWork == null && r.SourceSnapshotIds?.Contains(id) == true)
            {
                if (snapshot.Text == null) throw new RuntimeValidationException("Source has no retained text.");
                data.Add(id + ": " + snapshot.Text);
            }
        }
        if (factoryWork != null)
        {
            var planJson = await (from b in db.FactoryBatches join p in db.FactoryPlans on b.PlanId equals p.Id where b.Id == factoryWork.BatchId select p.SnapshotJson).SingleAsync(ct);
            var input = EditorialBinding.Read<FactoryPlanSnapshot>(planJson).Briefs.Single(b => b.Brief.Key == factoryWork.BriefKey);
            data.Add(input.Lesson.Sections!.SourceAnchor + "\nAttributed interpretation: " + input.Lesson.Sections.Interpretation);
            var trend = await TrendFence.Plan(db, (await db.FactoryBatches.AsNoTracking().SingleAsync(b => b.Id == factoryWork.BatchId, ct)).PlanId, ct);
            if (trend != null) { var t = EditorialBinding.Read<TrendStorySnapshot>(trend.SnapshotJson); data.Add("Verified event context (untrusted data): " + t.FactualSummary + "\nAnalogy: " + t.Analogy); }
        }
        var text = string.Join("\n", data); WorkflowRules.Untrusted(text);
        return new(ids.OrderBy(x => x).ToArray(), text, found);
    }
    public Task<AiRunView> GenerateAsync(CoreScope scope, Guid universe, GenerateAi request, CancellationToken ct)
    {
        if (request.PinnedRouteId == null || request.Tools || request.Streaming) throw new RuntimeValidationException("Workflow tools require a pinned bounded generation.");
        return ai.GenerateAsync(scope, universe, request, ct);
    }
}
