using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IEmbeddingProvider
{
    bool Supports(string model, int dimensions, string normalization);
    ValueTask<OwnedEmbedding> EmbedAsync(EmbeddingRequest request, CancellationToken ct);
}
public interface IKnowledgeRankingAdapter
{
    string Key { get; }
    ValueTask<IReadOnlyList<KnowledgeScore>> RankAsync(KnowledgeRanking request, CancellationToken ct);
}
public interface IKnowledgeRetriever { Task<KnowledgeResult> RetrieveAsync(CoreScope scope, Guid universe, KnowledgeQuery request, CancellationToken ct); }
public interface IKnowledgeIndexWorker { Task<bool> ProcessNextAsync(CancellationToken ct); }
public interface IKnowledgeAdministration
{
    Task<Guid> ConceptAsync(CoreScope scope, Guid universe, RegisterConcept request, CancellationToken ct);
    Task<Guid> RelationAsync(CoreScope scope, Guid universe, RegisterConceptRelation request, CancellationToken ct);
    Task ReviewAsync(CoreScope scope, Guid universe, Guid id, ReviewKnowledge request, CancellationToken ct);
    Task<KnowledgeGraph> GraphAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> QueueAsync(CoreScope scope, Guid universe, QueueKnowledgeIndex request, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeIndexView>> IndicesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ValidateAsync(CoreScope scope, Guid universe, Guid index, ValidateKnowledgeIndex request, CancellationToken ct);
    Task<Guid> ActivateAsync(CoreScope scope, Guid universe, ActivateKnowledgeIndex request, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeAliasView>> AliasesAsync(CoreScope scope, Guid universe, CancellationToken ct);
}
