using System.Net;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class RssHttpContractTests
{
    internal static readonly TrendLiveFeed Binding = new("controlled", "https://feed.example.invalid/news.xml", Guid.NewGuid(), "manual:fixture-contract-only-not-real-permission");
    internal static RegisterTrendConnector Config(TrendLiveFeed? binding = null) => new(Guid.NewGuid(), "controlled-rss", 1, "trend.configured-rss.v1", "trend.owned-text.a.v1", "Manual", "manual:fixture-configuration-not-live-qualification", [], (binding ?? Binding).PermissionSnapshotId) { LiveFeed = binding ?? Binding };
    internal static string Feed(string title = "Discovery only", string description = "Unverified source summary") => "<rss version=\"2.0\"><channel><item><guid isPermaLink=\"false\">opaque-source-id</guid><title>" + title + "</title><link>https://news.example.invalid/item</link><pubDate>" + DateTimeOffset.UtcNow.AddHours(-1).ToString("r") + "</pubDate><description>" + description + "</description><enclosure url=\"http://169.254.169.254/latest/meta-data\" /></item></channel></rss>";
    internal sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { public int Calls; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Calls++; return send(r, ct); } }
    internal static HttpResponseMessage Response(string xml, string type = "application/rss+xml") => new(HttpStatusCode.OK) { Content = new StringContent(xml, Encoding.UTF8, type) };
    private static ConfiguredRssTrendSource Source(Handler handler, int bytes = 65536, int seconds = 10) => new([new(Binding, true, seconds, bytes)], () => handler);
    [Fact]
    public async Task Same_and_changed_items_preserve_identity_and_times_without_fetching_article_or_inventing_substance()
    {
        var xml = Feed(); var handler = new Handler((r, _) => { Assert.Equal(Binding.Url, r.RequestUri!.AbsoluteUri); return Task.FromResult(Response(xml)); });
        var source = Source(handler); var first = await source.DiscoverAsync(new(Config(), null), default);
        var repeat = await source.DiscoverAsync(new(Config(), null), default); var a = Assert.Single(first.Signals); var b = Assert.Single(repeat.Signals);
        Assert.Equal(a.ExternalId, b.ExternalId); Assert.Equal(a.DiscoveryContentHash, b.DiscoveryContentHash); Assert.Equal(a.PublishedUtc, b.PublishedUtc); Assert.NotNull(a.PublishedUtc);
        Assert.Empty(a.SubstanceSnapshotIds); Assert.Empty(a.Metrics); Assert.Null(a.TranscriptSnapshotId);
        Assert.Equal("MetadataOnly", new OwnedTrendExtractorA().Extract(new(a, new Dictionary<Guid, string>()), default).EvidenceAccess);
        xml = xml.Replace("Unverified source summary", "Changed source summary"); var changed = Assert.Single((await source.DiscoverAsync(new(Config(), null), default)).Signals);
        Assert.Equal(a.ExternalId, changed.ExternalId); Assert.NotEqual(a.DiscoveryContentHash, changed.DiscoveryContentHash); Assert.Equal(3, handler.Calls);
    }
    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(308)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task Redirects_and_failures_are_not_followed_or_retried(int status)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Headers = { Location = new Uri("http://169.254.169.254/latest/meta-data") } }));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => Source(handler).DiscoverAsync(new(Config(), null), default)); Assert.Equal(1, handler.Calls);
    }
    [Theory]
    [InlineData("<rss>")]
    [InlineData("<!DOCTYPE rss [<!ENTITY data SYSTEM 'file:///private.txt'>]><rss><channel>&data;</channel></rss>")]
    [InlineData("<feed />")]
    public async Task Malformed_or_entity_bearing_documents_are_rejected(string xml)
    { var handler = new Handler((_, _) => Task.FromResult(Response(xml))); await Assert.ThrowsAsync<RuntimeValidationException>(() => Source(handler).DiscoverAsync(new(Config(), null), default)); }
    private sealed class UnknownLength(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes));
    }
    [Fact]
    public async Task Byte_limits_apply_to_streams_without_content_length_and_wrong_type_is_rejected()
    {
        var handler = new Handler((_, _) => { var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLength(new byte[1025]) }; r.Content.Headers.ContentType = new("application/xml"); return Task.FromResult(r); });
        await Assert.ThrowsAsync<RuntimeValidationException>(() => Source(handler, bytes: 1024).DiscoverAsync(new(Config(), null), default));
        var type = new Handler((_, _) => Task.FromResult(Response(Feed(), "text/html"))); await Assert.ThrowsAsync<RuntimeValidationException>(() => Source(type).DiscoverAsync(new(Config(), null), default));
    }
    [Fact]
    public async Task Timeout_and_caller_cancellation_are_bounded_without_retry()
    {
        var handler = new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Response(Feed()); });
        var e = await Assert.ThrowsAsync<RuntimeValidationException>(() => Source(handler, seconds: 1).DiscoverAsync(new(Config(), null), default)); Assert.Equal("DeadlineExceeded", e.Outcome); Assert.Equal(1, handler.Calls);
        using var stop = new CancellationTokenSource(); stop.Cancel(); var cancel = new Handler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Source(cancel).DiscoverAsync(new(Config(), null), stop.Token));
    }
    [Fact]
    public async Task Disabled_unlisted_or_changed_bindings_never_reach_transport()
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(Feed())));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => new ConfiguredRssTrendSource([]).DiscoverAsync(new(Config(), null), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => new ConfiguredRssTrendSource([new(Binding)], () => handler).DiscoverAsync(new(Config(), null), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => Source(handler).DiscoverAsync(new(Config(Binding with { Url = "https://other.example.invalid/feed" }), null), default)); Assert.Equal(0, handler.Calls);
    }
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.31.1.1")]
    [InlineData("192.168.1.1")]
    [InlineData("100.64.1.1")]
    [InlineData("168.63.129.16")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("2001:db8::1")]
    [InlineData("3fff::1")]
    [InlineData("192.88.99.2")]
    public void Private_link_local_metadata_and_transition_addresses_are_denied(string ip) => Assert.False(RssFeedHttp.PublicAddress(IPAddress.Parse(ip)));
    [Fact]
    public async Task Connection_pins_the_validated_address_and_rebinding_or_mixed_dns_cannot_connect()
    {
        var connections = 0; var resolves = 0;
        Task<IPAddress[]> Resolve(string _, CancellationToken ct) => Task.FromResult(new[] { IPAddress.Parse(++resolves == 1 ? "8.8.8.8" : "127.0.0.1") });
        ValueTask<Stream> Connect(IPAddress ip, int port, CancellationToken ct) { Assert.Equal(IPAddress.Parse("8.8.8.8"), ip); Assert.Equal(443, port); connections++; return ValueTask.FromResult<Stream>(new MemoryStream()); }
        using var stream = await RssFeedHttp.Connect("feed.example.invalid", 443, Resolve, Connect, default);
        await Assert.ThrowsAsync<RuntimeValidationException>(async () => await RssFeedHttp.Connect("feed.example.invalid", 443, Resolve, Connect, default));
        await Assert.ThrowsAsync<RuntimeValidationException>(async () => await RssFeedHttp.Connect("feed.example.invalid", 443, (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Loopback }), Connect, default)); Assert.Equal(1, connections);
    }
}
