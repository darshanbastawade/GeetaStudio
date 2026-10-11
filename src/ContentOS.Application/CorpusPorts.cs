using ContentOS.Contracts;

namespace ContentOS.Application;

public interface ICanonicalCorpus
{
    Task<Guid> RegisterAsync(CoreScope scope, Guid universe, RegisterCorpus request, CancellationToken ct);
    Task<IReadOnlyList<CorpusManifestView>> ListAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<CorpusImportResult> ImportAsync(CoreScope scope, Guid universe, Guid manifest, ImportCorpus request, CancellationToken ct);
    Task ReviewAsync(CoreScope scope, Guid universe, Guid manifest, Guid verse, CorpusReview request, CancellationToken ct);
    Task<CorpusReport> ReportAsync(CoreScope scope, Guid universe, Guid manifest, string territory, CancellationToken ct);
    Task<IReadOnlyList<CorpusVerseView>> ChapterAsync(CoreScope scope, Guid universe, Guid manifest, int chapter, string territory, CancellationToken ct);
    Task<Guid> AnnotateAsync(CoreScope scope, Guid universe, Guid manifest, Guid verse, CorpusAnnotationInput request, CancellationToken ct);
    Task ReviewAnnotationAsync(CoreScope scope, Guid universe, Guid manifest, Guid annotation, CorpusReview request, CancellationToken ct);
}
