using System.Security.Cryptography;
using System.Text;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class TrendRules
{
    public static readonly string[] Kinds = ["Manual", "News", "RSS", "VideoMetadata", "EventCalendar"];
    public static readonly string[] Risks = ["RealPerson", "Violence", "Tragedy", "Political", "ReligiousConflict", "Allegation", "ChildFacing"];
    public static string ClusterKey(TrendSignal signal)
    {
        var key = string.IsNullOrWhiteSpace(signal.EventKey) ? signal.Headline : signal.EventKey;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(' ', key.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))));
    }
    public static void Attest(string mode, string reference)
    {
        if (mode is not ("Fixture" or "Manual") || !reference.StartsWith(mode == "Fixture" ? "fixture:" : "manual:", StringComparison.OrdinalIgnoreCase))
            throw new RuntimeValidationException("Explicit fixture/manual review evidence required.");
        CoreRules.Text(reference, 1000, "Review reference");
    }
    public static void Signal(TrendSignal r)
    {
        if (r.SubstanceSnapshotIds == null || r.Metrics == null || r.Risks == null || r.Url == null || r.EventKey?.Length > 500 || r.Url.Length > 2000 || r.Risks.Length > Risks.Length) throw new RuntimeValidationException("Bounded source metadata required.");
        foreach (var metric in r.Metrics) CoreRules.Text(metric.Name, 100, "Metric name");
        CoreRules.Text(r.ExternalId, 200, "Source identifier"); CoreRules.Text(r.Headline, 1000, "Headline"); CoreRules.Text(r.ReportOrigin, 200, "Original report identity");
        if (!Kinds.Contains(r.Kind) || (!(r.Kind == "Manual" && r.Url.Length == 0) && (!Uri.TryCreate(r.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0)) || r.ReportStatus is not ("Current" or "Corrected" or "Retracted") ||
            r.SubstanceSnapshotIds.Length > 10 || r.Metrics.Length > 20 || r.Risks.Any(x => !Risks.Contains(x)) ||
            r.PublishedUtc > DateTimeOffset.UtcNow.AddMinutes(5) || r.ObservedUtc > DateTimeOffset.UtcNow.AddMinutes(5) ||
            r.UpdatedUtc > DateTimeOffset.UtcNow.AddMinutes(5) || r.Metrics.Any(x => x.Value < 0 || x.Value > 1000000000000000m || x.ObservedUtc > DateTimeOffset.UtcNow.AddMinutes(5)))
            throw new RuntimeValidationException("Bounded timestamped permitted source metadata required.");
        CoreRules.Text(r.Locale, 20, "Locale"); CoreRules.Text(r.Region, 100, "Region"); CoreRules.Text(r.Category, 100, "Category");
    }
    public static decimal? Momentum(IEnumerable<TrendMetric> metrics)
    {
        var rows = metrics.OrderBy(x => x.ObservedUtc).ToArray();
        if (rows.Length < 2 || rows.Select(x => x.Name).Distinct().Count() != 1 || rows[^1].ObservedUtc <= rows[0].ObservedUtc || rows[^1].Value < rows[0].Value) return null;
        return (rows[^1].Value - rows[0].Value) / (decimal)(rows[^1].ObservedUtc - rows[0].ObservedUtc).TotalHours;
    }
}
