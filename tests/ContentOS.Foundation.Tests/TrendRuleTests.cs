using System.Xml;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class TrendRuleTests
{
    [Fact]
    public void Missing_incomparable_reset_and_permitted_metric_observations_remain_distinct()
    {
        Assert.Null(TrendRules.Momentum([])); var now = DateTimeOffset.UtcNow;
        Assert.Equal(50, TrendRules.Momentum([new("views", 100, now.AddHours(-1)), new("views", 150, now)]));
        Assert.Null(TrendRules.Momentum([new("views", 100, now.AddHours(-1)), new("likes", 150, now)]));
        Assert.Null(TrendRules.Momentum([new("views", 100, now.AddHours(-1)), new("views", 10, now)]));
        Assert.Throws<RuntimeValidationException>(() => TrendRules.Attest("Manual", "fixture:not-human"));
    }
    [Fact]
    public void Uploaded_rss_is_metadata_only_and_dtd_cannot_fetch_a_local_or_remote_resource()
    {
        var source = new UploadedRssTrendSource(); var config = new RegisterTrendConnector(Guid.NewGuid(), "rss", 1, source.Key, "trend.owned-text.a.v1", "Manual", "manual:permitted-upload", []);
        var xml = "<rss version=\"2.0\"><channel><item><guid>report-1</guid><title>Metadata is not proof</title><link>https://example.invalid/report</link><pubDate>" + DateTimeOffset.UtcNow.AddHours(-1).ToString("r") + "</pubDate></item></channel></rss>";
        var result = source.Discover(new(config, xml), default); Assert.Single(result.Signals); Assert.Empty(result.Signals[0].SubstanceSnapshotIds); Assert.Empty(result.Signals[0].Metrics);
        var undated = source.Discover(new(config, "<rss version=\"2.0\"><channel><item><guid>undated</guid><title>Undated metadata</title><link>https://example.invalid/undated</link></item></channel></rss>"), default); Assert.Null(Assert.Single(undated.Signals).PublishedUtc);
        Assert.Throws<XmlException>(() => source.Discover(new(config, "<!DOCTYPE rss [<!ENTITY data SYSTEM 'file:///C:/private.txt'>]><rss><channel>&data;</channel></rss>"), default));
    }
    [Fact]
    public void Separate_extractor_contract_never_invents_an_inaccessible_transcript()
    {
        var id = Guid.NewGuid(); var signal = TrendTestFixture.Signal([id]) with { Kind = "VideoMetadata", RequiresTranscript = true };
        var result = new OwnedTrendExtractorA().Extract(new(signal, new Dictionary<Guid, string> { { id, "Authorized article evidence only." } }), default);
        Assert.Equal("Unavailable", result.TranscriptAccess); Assert.Contains("article", result.Text!);
        var second = new OwnedTrendExtractorB().Extract(new(signal, new Dictionary<Guid, string> { { id, "Authorized article evidence only." } }), default);
        Assert.Equal(result.Contract, second.Contract); Assert.Equal("UnsupportedByAdapter", second.TranscriptAccess); Assert.NotEqual(result.Adapter, second.Adapter);
    }
}
