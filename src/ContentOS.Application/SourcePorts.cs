using ContentOS.Contracts;

namespace ContentOS.Application;

public interface ISourceRegistry
{
    Task<Guid> RegisterAsync(CoreScope scope, Guid universe, RegisterSource request, CancellationToken ct);
    Task<Guid> EditionAsync(CoreScope scope, Guid universe, Guid work, RegisterEdition request, CancellationToken ct);
    Task<Guid> PermissionAsync(CoreScope scope, Guid universe, Guid edition, GrantSourcePermission request, CancellationToken ct);
    Task ReviewPermissionAsync(CoreScope scope, Guid universe, Guid permission, ReviewSource request, CancellationToken ct);
    Task<Guid> ImportAsync(CoreScope scope, Guid universe, Guid edition, ImportSourceFile request, CancellationToken ct);
    Task ReviewSnapshotAsync(CoreScope scope, Guid universe, Guid snapshot, ReviewSource request, CancellationToken ct);
    Task<IReadOnlyList<SourceWorkView>> ListAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<SourceDetail> InspectAsync(CoreScope scope, Guid universe, Guid work, CancellationToken ct);
    Task<SourceReviewQueue> ReviewQueueAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<RightsDecisionView> EvaluateAsync(CoreScope scope, Guid universe, Guid snapshot, IntendedUse use, CancellationToken ct);
    Task<Guid> LinkAsync(CoreScope scope, Guid universe, LinkEvidence request, CancellationToken ct);
    Task<Guid> RecordUseAsync(CoreScope scope, Guid universe, RecordSourceUse request, CancellationToken ct);
    Task<RevocationImpact> RevokeAsync(CoreScope scope, Guid universe, Guid work, RevokeSource request, CancellationToken ct);
    Task<IReadOnlyList<RemediationView>> RemediationAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task ResolveAsync(CoreScope scope, Guid universe, Guid remediation, ResolveRemediation request, CancellationToken ct);
    Task<SourceAwareExport> ExportAsync(CoreScope scope, Guid universe, Guid content, IntendedUse use, CancellationToken ct);
}
public interface ISourceRightsGuard
{
    Task<RightsBundle> CheckAsync(CoreScope scope, Guid revision, IntendedUse use, CancellationToken ct);
}
