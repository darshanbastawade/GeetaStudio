using ContentOS.Application;
using ContentOS.Domain;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class MediaRuleTests
{
    [Fact]
    public void Actual_media_structure_checksum_and_bounded_processing_are_required()
    {
        var png = MediaFiles.ReferenceCard("fictional-coach", ["#245441"]); var info = MediaFiles.Inspect(png); Assert.Equal("image/png", info.Mime); Assert.Equal(256, info.Width); Assert.Equal(MediaRules.Hash(png), info.Sha256);
        var corrupt = png.ToArray(); corrupt[20] ^= 1; Assert.Throws<RuntimeValidationException>(() => MediaFiles.Inspect(corrupt)); Assert.Throws<RuntimeValidationException>(() => MediaFiles.Inspect(System.Text.Encoding.UTF8.GetBytes("<svg><script/></svg>"))); Assert.Throws<RuntimeValidationException>(() => MediaFiles.Inspect(new byte[1048577]));
        var wav = ToneSpeechFixtureProvider.Tone(); Assert.Equal(.25, MediaFiles.Inspect(wav).DurationSeconds); Assert.Throws<RuntimeValidationException>(() => MediaFiles.Inspect(wav[..^2]));
    }
    [Fact]
    public async Task Reference_capability_is_not_silently_discarded_and_tones_do_not_claim_speech()
    {
        var reference = MediaFiles.ReferenceCard("preserved", ["#245441"]); var r = new ContentOS.Contracts.MediaProviderRequest(Guid.NewGuid(), "local:owned", "fixture", "en-IN", "preview", [reference], [], [], 1048576, 0);
        var a = await new ReferenceImageProvider().SubmitAsync(r, default); Assert.Equal(reference, a.Bytes); var b = await new BasicImageProvider().SubmitAsync(r, default); Assert.Equal("Unsupported", b.State); Assert.Null(b.Bytes);
        Assert.Contains("FixtureToneNotSpokenNarration", new ToneSpeechFixtureProvider().Capabilities.Coverage); Assert.False(new ToneSpeechFixtureProvider().Capabilities.PronunciationSubstitution);
    }
}
