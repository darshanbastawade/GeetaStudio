using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IReadWorkbench
{
    Task<ReadCollectionSummary[]> PrivateDirectoryAsync(string actor, CancellationToken ct);
    Task<Guid> RegisterAsync(CoreScope scope, Guid universe, RegisterReadCollection request, CancellationToken ct);
    Task<Guid> AddAsync(CoreScope scope, Guid universe, AddReadPiece request, CancellationToken ct);
    Task<ReadStaffView> InspectAsync(CoreScope scope, Guid universe, Guid collection, CancellationToken ct);
    Task EvidenceAsync(CoreScope scope, Guid universe, Guid collection, ReadGateEvidence request, CancellationToken ct);
    Task ControlAsync(CoreScope scope, Guid universe, Guid collection, ControlReadCollection request, CancellationToken ct);
    Task<ReadBook?> PreviewAsync(string actor, Guid collection, CancellationToken ct);
}
public interface IPublicRead
{
    Task<ReadCollectionSummary[]> DirectoryAsync(CancellationToken ct);
    Task<ReadBook?> GetAsync(string key, CancellationToken ct);
    Task EventAsync(string key, ReadEngagement request, CancellationToken ct);
}
