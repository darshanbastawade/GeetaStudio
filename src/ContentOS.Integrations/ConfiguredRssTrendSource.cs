using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

public sealed record RssFeedPolicy(TrendLiveFeed Binding, bool Enabled = false, int TimeoutSeconds = 10, int MaxBytes = 65536);
public sealed class ConfiguredRssTrendSource(IEnumerable<RssFeedPolicy> policies, Func<HttpMessageHandler>? handlerFactory = null) : ITrendSource
{
    public string Key => "trend.configured-rss.v1";
    private RssFeedPolicy Policy(RegisterTrendConnector config)
    {
        var binding = config.LiveFeed ?? throw new RuntimeValidationException("Configured permitted RSS binding required; live intake is disabled by default.");
        CoreRules.Text(binding.Key, 100, "Feed profile key"); CoreRules.Text(binding.PermissionReference, 1000, "Feed permission reference"); CoreRules.Text(binding.Url, 2000, "Feed URL");
        var matches = policies.Where(x => x.Binding.Key == binding.Key).ToArray();
        if (matches.Length != 1 || !matches[0].Enabled || matches[0].Binding != binding || config.Mode != "Manual" || config.SourceAdapter != Key ||
            config.FixtureSignals.Length != 0 || config.FeedSnapshotId != binding.PermissionSnapshotId || binding.PermissionSnapshotId == Guid.Empty ||
            !binding.PermissionReference.StartsWith("manual:", StringComparison.Ordinal) || binding.PermissionReference.Length <= 7 ||
            matches[0].TimeoutSeconds is < 1 or > 30 || matches[0].MaxBytes is < 1 or > 65536 ||
            !Uri.TryCreate(binding.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            uri.HostNameType != UriHostNameType.Dns || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            throw new RuntimeValidationException("RSS source is disabled, unqualified or differs from its configured permission binding.");
        return matches[0];
    }
    public void Validate(RegisterTrendConnector config) => _ = Policy(config);
    public TrendDiscovery Discover(TrendDiscoveryRequest r, CancellationToken ct) => throw new RuntimeValidationException("Configured RSS requires asynchronous polling.");
    public async Task<TrendDiscovery> DiscoverAsync(TrendDiscoveryRequest r, CancellationToken ct)
    {
        var policy = Policy(r.Configuration);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(policy.TimeoutSeconds));
        try
        {
            using var client = new HttpClient((handlerFactory ?? RssFeedHttp.CreateHandler)()) { Timeout = Timeout.InfiniteTimeSpan };
            using var request = new HttpRequestMessage(HttpMethod.Get, policy.Binding.Url) { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
            request.Headers.ConnectionClose = true; request.Headers.UserAgent.ParseAdd("ContentOS-RSS/1.0"); request.Headers.Accept.ParseAdd("application/rss+xml, application/xml, text/xml");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK) throw new RuntimeValidationException("RSS request failed; redirects and retries are disabled.");
            if (response.Content.Headers.ContentType?.MediaType is not ("application/rss+xml" or "application/xml" or "text/xml") || response.Content.Headers.ContentEncoding.Count != 0 || response.Content.Headers.ContentLength > policy.MaxBytes)
                throw new RuntimeValidationException("RSS response type/size/encoding is not permitted.");
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token); using var buffer = new MemoryStream();
            var bytes = new byte[4096]; int count;
            while ((count = await input.ReadAsync(bytes, timeout.Token)) != 0)
            { if (buffer.Length + count > policy.MaxBytes) throw new RuntimeValidationException("RSS response exceeded its byte limit."); await buffer.WriteAsync(bytes.AsMemory(0, count), timeout.Token); }
            buffer.Position = 0;
            using var xml = XmlReader.Create(buffer, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var doc = XDocument.Load(xml); if (doc.Root?.Name != "rss" || doc.Root.Attribute("version")?.Value != "2.0" || doc.Root.Element("channel") == null) throw new RuntimeValidationException("RSS 2.0 channel required.");
            var signals = new List<TrendSignal>(); var now = DateTimeOffset.UtcNow;
            foreach (var item in doc.Root.Element("channel")!.Elements("item").Take(25))
            {
                timeout.Token.ThrowIfCancellationRequested();
                var url = item.Element("link")?.Value.Trim() ?? "";
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0) continue;
                var id = item.Element("guid")?.Value.Trim(); if (string.IsNullOrWhiteSpace(id)) id = uri.AbsoluteUri;
                // The ID is opaque. Hash it to preserve bounded stable identity without truncation collisions.
                var external = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(id)));
                DateTimeOffset? published = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time) ? time : null;
                var signal = new TrendSignal(external, uri.AbsoluteUri, new Uri(policy.Binding.Url).Host, false, "RSS", item.Element("title")?.Value.Trim() ?? "Untitled feed entry", uri.AbsoluteUri,
                    "Unclassified", r.Configuration.FeedRegion, r.Configuration.FeedLocale, null, published, null, now, [], null, false, [], [])
                { DiscoveryContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(item.ToString(SaveOptions.DisableFormatting)))) };
                TrendRules.Signal(signal); signals.Add(signal);
            }
            return new("trend.contract.v1", Key, ["ConfiguredPermittedRSSMetadata", "ArticleSubstanceUnavailable", "NoTranscript", "NoAutomaticVerification", "OneAttemptNoRedirects"], signals.ToArray());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new RuntimeValidationException("RSS request timed out.", "DeadlineExceeded"); }
        catch (Exception e) when (e is XmlException or HttpRequestException or IOException) { throw new RuntimeValidationException("RSS response unavailable or malformed."); }
    }
}
