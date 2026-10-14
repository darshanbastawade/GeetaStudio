using ContentOS.Contracts;
namespace ContentOS.Application;

public interface ITalkWorkbench
{
    Task<Guid> ConfigureAsync(CoreScope scope, TalkPolicyRequest request, CancellationToken ct);
    Task ControlAsync(CoreScope scope, Guid policyId, bool enabled, CancellationToken ct);
    Task<TalkPolicyView[]> AdminPoliciesAsync(CoreScope scope, CancellationToken ct);
    Task<TalkProblemView[]> ProblemsAsync(CoreScope scope, CancellationToken ct);
    Task<TalkPolicyView[]> PoliciesAsync(CustomerScope scope, CancellationToken ct);
    Task<Guid> OpenAsync(CustomerScope scope, TalkOpen request, CancellationToken ct);
    Task<TalkSessionView> InspectAsync(CustomerScope scope, Guid sessionId, CancellationToken ct);
    Task<TalkSource> SourceAsync(CustomerScope scope, Guid sessionId, Guid turnId, Guid corpusTextId, CancellationToken ct);
    Task<TalkTurnView> AskAsync(CustomerScope scope, Guid sessionId, TalkAsk request, CancellationToken ct);
    Task CancelAsync(CustomerScope scope, Guid sessionId, CancellationToken ct);
    Task ForgetAsync(CustomerScope scope, Guid sessionId, CancellationToken ct);
    Task<TalkTurnView> ReconcileAsync(CustomerScope scope, Guid sessionId, Guid turnId, CancellationToken ct);
    Task<Guid> ReportAsync(CustomerScope scope, Guid sessionId, Guid turnId, TalkFeedback request, CancellationToken ct);
    Task PurgeAsync(CancellationToken ct);
}

