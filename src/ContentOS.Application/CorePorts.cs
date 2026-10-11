using ContentOS.Contracts;

namespace ContentOS.Application;

// Scope comes from the authenticated actor plus an explicit tenant selection; the database verifies membership.
public sealed record CoreScope(Guid TenantId, string Actor);
public interface IContentCore
{
    Task<IReadOnlyList<TenantView>> TenantsAsync(string actor, CancellationToken ct);
    Task<IReadOnlyList<UniverseView>> UniversesAsync(CoreScope scope, CancellationToken ct);
    Task<UniverseCatalog> CatalogAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<IReadOnlyList<ContentView>> ListAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<ContentView> ReadAsync(CoreScope scope, Guid universe, Guid id, CancellationToken ct);
    Task<ContentView> CreateAsync(CoreScope scope, Guid universe, CreateContent request, CancellationToken ct);
    Task<ContentView> ReviseAsync(CoreScope scope, Guid universe, Guid id, ReviseContent request, CancellationToken ct);
    Task<ContentView> TransitionAsync(CoreScope scope, Guid universe, Guid id, ChangeEditorialState request, CancellationToken ct);
    Task<ContentExport> ExportAsync(CoreScope scope, Guid universe, Guid id, CancellationToken ct);
    Task<ContentExport> ExportForUseAsync(CoreScope scope, Guid universe, Guid id, IntendedUse use, CancellationToken ct);
    Task<ContentView> ImportAsync(CoreScope scope, Guid universe, ImportContent request, CancellationToken ct);
    Task ShareAsync(CoreScope scope, Guid universe, ShareUniverse request, CancellationToken ct);
}

