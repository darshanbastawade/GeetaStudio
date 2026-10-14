using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

public sealed class FixtureTrendSourceA : ITrendSource
{
    public string Key => "trend.fixture.a.v1";
    public TrendDiscovery Discover(TrendDiscoveryRequest r, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return new("trend.contract.v1", Key, ["ControlledFixtureNews", "ControlledFixtureVideoMetadata", "ControlledFixtureCalendar", "NoLiveNetwork"], r.Configuration.FixtureSignals); }
}
public sealed class FixtureTrendSourceB : ITrendSource
{
    public string Key => "trend.fixture.b.v1";
    public TrendDiscovery Discover(TrendDiscoveryRequest r, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return new("trend.contract.v1", Key, ["ControlledFixtureNewsOnly", "VideoChartsUnsupported", "NoLiveNetwork"], r.Configuration.FixtureSignals.Where(x => x.Kind is "News" or "Manual").ToArray()); }
}
public sealed class UploadedRssTrendSource : ITrendSource
{
    public string Key => "trend.uploaded-rss.v1";
    public TrendDiscovery Discover(TrendDiscoveryRequest r, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (r.FeedXml == null) return new("trend.contract.v1", Key, ["AuthorizedUploadedRSSOnly", "LiveFetchUnsupported"], []);
        using var input = new StringReader(r.FeedXml);
        using var xml = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
        var doc = XDocument.Load(xml);
        var signals = new List<TrendSignal>();
        foreach (var item in doc.Descendants("item").Take(25))
        {
            var link = item.Element("link")?.Value ?? "";
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
            DateTimeOffset? published = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
            var id = item.Element("guid")?.Value ?? link;
            // RSS gives discovery metadata, never permission to retrieve or facts about the article.
            signals.Add(new(id, link, id, false, "RSS", item.Element("title")?.Value ?? "Untitled feed entry", link,
                "Unclassified", r.Configuration.FeedRegion, r.Configuration.FeedLocale, null, published, null, DateTimeOffset.UtcNow, [], null, false, [], []));
        }
        return new("trend.contract.v1", Key, ["AuthorizedUploadedRSSOnly", "ArticleSubstanceUnavailable", "LiveFetchUnsupported"], signals.ToArray());
    }
}
public sealed class OwnedTrendExtractorA : ITrendExtractor
{
    public string Key => "trend.owned-text.a.v1";
    public TrendExtraction Extract(TrendExtractionRequest r, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var ids = r.Signal.SubstanceSnapshotIds.Where(r.PermittedTexts.ContainsKey).ToArray();
        var transcript = r.Signal.TranscriptSnapshotId.HasValue && r.PermittedTexts.ContainsKey(r.Signal.TranscriptSnapshotId.Value);
        if (transcript) ids = ids.Append(r.Signal.TranscriptSnapshotId!.Value).Distinct().ToArray();
        return new("trend.contract.v1", Key, ids.Length == 0 ? "MetadataOnly" : "AuthorizedTextAvailable", transcript ? "AuthorizedTranscriptAvailable" : "Unavailable",
            ids, ids.Length == 0 ? null : string.Join("\n", ids.Select(id => r.PermittedTexts[id])));
    }
}
public sealed class OwnedTrendExtractorB : ITrendExtractor
{
    public string Key => "trend.owned-text.b.v1";
    public TrendExtraction Extract(TrendExtractionRequest r, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ids = r.Signal.SubstanceSnapshotIds.Where(id => id != r.Signal.TranscriptSnapshotId && r.PermittedTexts.ContainsKey(id)).ToArray();
        return new("trend.contract.v1", Key, ids.Length == 0 ? "MetadataOnly" : "AuthorizedTextAvailable", "UnsupportedByAdapter",
            ids, ids.Length == 0 ? null : string.Join("\n", ids.Select(id => r.PermittedTexts[id])));
    }
}
