using ContentOS.Contracts;
namespace ContentOS.Application;

public interface IAnalyticsWorkbench
{
    Task<Guid> SourceAsync(CoreScope s, Guid u, AnalyticsSourceRequest r, CancellationToken ct);
    Task<AnalyticsSourceView[]> SourcesAsync(CoreScope s, Guid u, CancellationToken ct);
    Task<Guid> DefinitionAsync(CoreScope s, Guid u, AnalyticsDefinitionRequest r, CancellationToken ct);
    Task<AnalyticsDefinitionView[]> DefinitionsAsync(CoreScope s, Guid u, CancellationToken ct);
    Task<Guid> FxAsync(CoreScope s, Guid u, AnalyticsFxRequest r, CancellationToken ct);
    Task<AnalyticsFxView[]> RatesAsync(CoreScope s, Guid u, CancellationToken ct);
    Task<Guid> ConsentAsync(CoreScope s, Guid u, AnalyticsConsentRequest r, CancellationToken ct);
    Task<AnalyticsConsentView[]> ConsentsAsync(CoreScope s, Guid u, CancellationToken ct);
    Task ControlAsync(CoreScope s, Guid u, string kind, Guid id, AnalyticsControl r, CancellationToken ct);
    Task<AnalyticsImportResult> ImportAsync(CoreScope s, Guid u, AnalyticsImport r, CancellationToken ct);
    Task<AnalyticsImportResult> CaptureCustomerPaymentsAsync(CoreScope s, Guid u, AnalyticsCapture r, CancellationToken ct);
    Task<AnalyticsImportResult> CaptureCommerceAsync(CoreScope s, Guid u, AnalyticsCapture r, CancellationToken ct);
    Task<AnalyticsImportResult> CaptureAsync(CoreScope s, Guid u, AnalyticsCapture r, CancellationToken ct);
    Task<AnalyticsEntryView[]> EntriesAsync(CoreScope s, Guid u, CancellationToken ct, int skip = 0, int take = 100, DateTimeOffset? asOfUtc = null);
    Task<AnalyticsReport> ReportAsync(CoreScope s, Guid u, AnalyticsReportQuery r, CancellationToken ct);
    Task<Guid> ForecastAsync(CoreScope s, Guid u, AnalyticsForecastRequest r, CancellationToken ct);
    Task<AnalyticsForecastView[]> ForecastsAsync(CoreScope s, Guid u, CancellationToken ct);
    Task<AnalyticsProviderInfo[]> ProvidersAsync(CoreScope s, Guid u, CancellationToken ct);
}
public interface IChannelAnalyticsProvider
{
    string Key { get; }
    AnalyticsProviderInfo Info { get; }
    Task<AnalyticsEntryInput[]> NormalizeAsync(AnalyticsSourceRequest source, AnalyticsEntryInput[] entries, CancellationToken ct);
}
