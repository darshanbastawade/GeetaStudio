using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;
// Validated imports are owned DTOs. These adapters never contact a vendor or accept endpoints.
public sealed class AnalyticsImportProvider(string key) : IChannelAnalyticsProvider
{
    public string Key => key;
    public AnalyticsProviderInfo Info => new(key, "analytics.events.v1", key.StartsWith("analytics.fixture.") ? "DisconnectedFixture" : "ValidatedImportOnly",
        ["ValidatedImport", "VersionedMetrics", "ExplicitMissingness"], 100, "Scoped staff evidence import; not platform OAuth",
        "Private SQL operational ledger; pseudonymous consent only; no vendor transfer", "docs/M19_LOCAL_RUN.md", "2026-10-13");
    public Task<AnalyticsEntryInput[]> NormalizeAsync(AnalyticsSourceRequest source, AnalyticsEntryInput[] entries, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AnalyticsRules.Require(source.Adapter == key && entries.Length is > 0 and <= 100, "Pinned deployed import profile required.");
        // Independent fixture implementations exercise route replacement without changing historical records.
        return key switch
        {
            "analytics.fixture.a.v1" => Task.FromResult(entries.Select(e => e with { }).ToArray()),
            "analytics.fixture.b.v1" => Task.FromResult(entries.ToList().ConvertAll(e => e with { }).ToArray()),
            "analytics.import.v1" => Task.FromResult(entries.ToArray()),
            _ => throw new InvalidOperationException("Unapproved analytics adapter.")
        };
    }
}
