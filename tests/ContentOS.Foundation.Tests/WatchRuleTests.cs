using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class WatchRuleTests
{
    [Fact]
    public void Complete_script_coverage_and_subtitle_boundaries_are_explicit()
    { var request = new CreateWatch(Guid.NewGuid(), "episode", 1, Guid.NewGuid(), Guid.NewGuid(), [new("scene", 0, 4, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "fixture:recording")], ["Vertical"]); WatchRules.Script(request, "test"); Assert.Throws<EvidenceBlockedException>(() => WatchRules.Script(request, "test missing context")); Assert.Throws<RuntimeValidationException>(() => WatchRules.Script(request with { Scenes = [request.Scenes[0] with { ScriptStart = 1 }] }, "test")); var subtitles = new ApprovedSceneSubtitles(); var cues = new[] { new WatchCue(.123, 3.456, "यह कल्पना है। <script> -->", "scene") }; Assert.Contains("00:00:00.123 --> 00:00:03.456", subtitles.Vtt(cues)); Assert.Contains("00:00:00,123 --> 00:00:03,456", subtitles.Srt(cues)); Assert.DoesNotContain("<script>", subtitles.Vtt(cues)); }
    [Fact]
    public void Layout_and_renderer_config_do_not_accept_arbitrary_tools_or_unsupported_shapes()
    { Assert.Throws<RuntimeValidationException>(() => WatchRules.Template(new(Font: "arbitrary tool"))); Assert.Throws<RuntimeValidationException>(() => WatchRules.Size("automatic-trim")); var renderer = new FfmpegWatchRenderer(new("", "", "", "", "", "", ".local")); Assert.False(renderer.Capabilities.Configured); Assert.True(renderer.Capabilities.IndicShaping); }
}
