using ContentOS.Contracts;
namespace ContentOS.Application;

public interface ITranslationAdapter
{
    string Key { get; }
    TranslationResult Translate(TranslationRequest request);
}
public interface ITranslationProvider
{
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken ct);
}
public interface ILessonEngine
{
    Task<IReadOnlyList<LessonProfileView>> ProposeDefaultsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> RegisterProfileAsync(CoreScope scope, Guid universe, RegisterLessonProfile request, CancellationToken ct);
    Task<IReadOnlyList<LessonProfileView>> ProfilesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewProfileAsync(CoreScope scope, Guid universe, Guid id, LessonReview request, CancellationToken ct);
    Task<LessonBundleView> RegisterBundleAsync(CoreScope scope, Guid universe, RegisterLessonBundle request, CancellationToken ct);
    Task<IReadOnlyList<LessonBundleView>> BundlesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> RegisterMemoryAsync(CoreScope scope, Guid universe, RegisterLessonMemory request, CancellationToken ct);
    Task<IReadOnlyList<LessonMemoryView>> MemoryAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewMemoryAsync(CoreScope scope, Guid universe, Guid id, LessonReview request, CancellationToken ct);
    Task<LessonVariantView> AdaptAsync(CoreScope scope, Guid universe, AdaptLesson request, CancellationToken ct);
    Task<IReadOnlyList<LessonVariantView>> VariantsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<LessonVariantView> InspectAsync(CoreScope scope, Guid universe, Guid id, CancellationToken ct);
    Task ReviewVariantAsync(CoreScope scope, Guid universe, Guid id, LessonReview request, CancellationToken ct);
    Task<LessonVariantView> ExportAsync(CoreScope scope, Guid universe, Guid id, CancellationToken ct);
    Task<IReadOnlyList<LessonCastCandidate>> CandidatesAsync(CoreScope scope, Guid universe, Guid writingProfile, CancellationToken ct);
    Task<IReadOnlyList<LessonCastCandidate>> CharactersAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> RegisterCharacterAsync(CoreScope scope, Guid universe, RegisterLessonCharacter request, CancellationToken ct);
}
