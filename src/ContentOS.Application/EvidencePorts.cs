using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IEvidenceEngine
{
    Task<Guid> RegisterRubricAsync(CoreScope scope, Guid universe, RegisterEvidenceRubric request, CancellationToken ct);
    Task<IReadOnlyList<EvidenceRubricView>> RubricsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> SubmitAsync(CoreScope scope, Guid universe, SubmitClaim request, CancellationToken ct);
    Task ReviewAsync(CoreScope scope, Guid universe, Guid revision, ReviewClaim request, CancellationToken ct);
    Task<IReadOnlyList<ClaimView>> ListAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<ClaimView> InspectAsync(CoreScope scope, Guid universe, Guid claim, CancellationToken ct);
    Task<Guid> DisagreeAsync(CoreScope scope, Guid universe, RegisterDisagreement request, CancellationToken ct);
    Task<IReadOnlyList<DisagreementView>> DisagreementsAsync(CoreScope scope, Guid universe, CancellationToken ct);
    Task<Guid> LinkAsync(CoreScope scope, Guid universe, LinkClaim request, CancellationToken ct);
    Task<Guid> CorrectAsync(CoreScope scope, Guid universe, RequestClaimCorrection request, CancellationToken ct);
    Task<ClaimImpact> ImpactAsync(CoreScope scope, Guid universe, Guid revision, CancellationToken ct);
    Task<EvidenceGateResult> CheckAsync(CoreScope scope, Guid universe, Guid contentRevision, IntendedUse use, CancellationToken ct);
}
