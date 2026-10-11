using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IUniverseRegistry
{
    Task ProposeDefaultsAsync(CoreScope scope, Guid universe, RegistrySeedDocument data, CancellationToken ct);
    Task<Guid> RegisterBibleAsync(CoreScope scope, Guid universe, RegisterBible request, CancellationToken ct);
    Task<IReadOnlyList<RegistryRevisionView<UniverseBibleRules>>> BiblesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<RegistryCharacterView> RegisterCharacterAsync(CoreScope scope, Guid universe, RegisterRegistryCharacter request, CancellationToken ct);
    Task<IReadOnlyList<RegistryCharacterView>> CharactersAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ControlCharacterAsync(CoreScope scope, Guid universe, Guid character, CharacterLifecycle request, CancellationToken ct);
    Task<Guid> RegisterPresentationAsync(CoreScope scope, Guid universe, RegisterRegistryPresentation request, CancellationToken ct);
    Task<IReadOnlyList<RegistryRevisionView<RegistryPresentationRules>>> PresentationsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> RegisterRelationshipAsync(CoreScope scope, Guid universe, RegisterRegistryRelationship request, CancellationToken ct);
    Task<IReadOnlyList<RegistryRevisionView<RegistryRelationshipRules>>> RelationshipsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ReviewAsync(CoreScope scope, Guid universe, Guid revision, RegistryReview request, CancellationToken ct);
    Task<Guid> RegisterEpisodeAsync(CoreScope scope, Guid universe, RegisterRegistryEpisode request, CancellationToken ct);
    Task<IReadOnlyList<RegistryEpisodeView>> EpisodesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<RegistryCastPlan> CastAsync(CoreScope scope, Guid universe, CastingBrief request, CancellationToken ct);
    Task<RegistryRevisionView<RegistryEpisodeSnapshot>> ComposeAsync(CoreScope scope, Guid universe, ComposeRegistryEpisode request, CancellationToken ct);
    Task<IReadOnlyList<RegistryRevisionView<RegistryEpisodeSnapshot>>> StoriesAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<RegistryRevisionView<RegistryEpisodeSnapshot>> ExportAsync(CoreScope scope, Guid universe, Guid revision, CancellationToken ct);
}
