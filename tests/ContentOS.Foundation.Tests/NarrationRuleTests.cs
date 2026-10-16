using System.Globalization;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Foundation.Tests;

public sealed class NarrationRuleTests
{
    private static CreateWatch Request(string body)
    {
        var output = new ImportMedia(Guid.NewGuid(), "voice", 1, "voice.wav", "Controlled narration fixture; no genuine voice qualification", "Voice", [], "", new("Fixture", "fixture:split-test", [], false, true, "Not spoken narration"), Guid.NewGuid(), "en-IN", Guid.NewGuid());
        return new(Guid.NewGuid(), "narration", 1, Guid.NewGuid(), Guid.NewGuid(), [new("scene", 0, body.Length, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "fixture:generated-script", VoiceGeneration: new(Guid.NewGuid(), Guid.NewGuid(), output, body, [], [], 0))], ["Standard"]);
    }
    [Fact]
    public void Long_unicode_script_is_preserved_once_with_stable_per_segment_bindings()
    {
        var body = string.Concat(Enumerable.Repeat("A learner checks the context. 👩🏽‍🏫 Ask a calm question. नमस्ते। ", 6)); var input = Request(body);
        var plan = WatchNarration.Plan(input, body); Assert.True(plan.Scenes.Length > 1);
        Assert.Equal(body, string.Concat(plan.Scenes.Select(x => x.VoiceGeneration!.PromptOrText)));
        Assert.Equal(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(WatchNarration.Plan(input, body)));
        var boundaries = StringInfo.ParseCombiningCharacters(body).Append(body.Length).ToHashSet();
        Assert.All(plan.Scenes, x => { Assert.InRange(x.ScriptLength, 1, 160); Assert.Contains(x.ScriptStart, boundaries); Assert.Contains(x.ScriptStart + x.ScriptLength, boundaries); Assert.Equal(input.Scenes[0].CharacterId, x.CharacterId); Assert.Equal(input.Scenes[0].PresentationId, x.PresentationId); Assert.Equal(input.Scenes[0].VoiceGeneration!.ProfileId, x.VoiceGeneration!.ProfileId); Assert.Equal(body.Substring(x.ScriptStart, x.ScriptLength), x.VoiceGeneration.PromptOrText); Assert.Null(x.VoiceGeneration.Admission); });
        Assert.Equal(plan.Scenes.Length, plan.Scenes.Select(x => x.VoiceGeneration!.OperationId).Distinct().Count());
        Assert.NotEqual(plan.Scenes[0].VoiceGeneration!.OperationId, WatchNarration.Plan(input with { OperationId = Guid.NewGuid() }, body).Scenes[0].VoiceGeneration!.OperationId);
    }
    [Fact]
    public void Imported_audio_and_image_generation_cannot_be_duplicated_as_long_narration()
    {
        var body = new string('x', 200); var r = Request(body);
        Assert.Throws<EvidenceBlockedException>(() => WatchNarration.Plan(r with { Scenes = [r.Scenes[0] with { VoiceVersionId = Guid.NewGuid(), VoiceGeneration = null }] }, body));
        Assert.Throws<EvidenceBlockedException>(() => WatchNarration.Plan(r with { Scenes = [r.Scenes[0] with { ImageVersionId = null, ImageGeneration = r.Scenes[0].VoiceGeneration }] }, body));
        Assert.Throws<EvidenceBlockedException>(() => WatchNarration.Plan(r with { Scenes = [r.Scenes[0] with { VoiceGeneration = r.Scenes[0].VoiceGeneration! with { PromptOrText = "Different text" } }] }, body));
    }
    [Fact]
    public void Missing_context_and_oversized_graphemes_fail_instead_of_being_truncated()
    {
        var body = new string('x', 200); var r = Request(body);
        Assert.Throws<RuntimeValidationException>(() => WatchNarration.Plan(r with { Scenes = [r.Scenes[0] with { ScriptStart = 1 }] }, body));
        Assert.Throws<EvidenceBlockedException>(() => WatchNarration.Plan(r with { Scenes = [r.Scenes[0] with { ScriptLength = 199, VoiceGeneration = r.Scenes[0].VoiceGeneration! with { PromptOrText = body[..199] } }] }, body));
        var giant = "x" + new string('\u0301', 170); Assert.Throws<EvidenceBlockedException>(() => WatchNarration.Plan(Request(giant), giant));
        Assert.Throws<RuntimeValidationException>(() => WatchNarration.Plan(Request(new string('x', 3201)), new string('x', 3201)));
    }
    [Fact]
    public void Caption_density_uses_the_reviewed_font_and_margin_without_losing_script()
    {
        var body = string.Concat(Enumerable.Repeat("Observe and ask before deciding. ", 12)); var request = Request(body) with { Aspects = ["Vertical", "Standard"] };
        var plan = WatchNarration.Plan(request, body, new(CaptionSize: 32, SafeMarginPercent: 15));
        Assert.All(plan.Scenes, x => Assert.InRange(x.ScriptLength, 1, 40)); Assert.Equal(body, string.Concat(plan.Scenes.Select(x => x.VoiceGeneration!.PromptOrText)));
    }
    [Fact]
    public void Already_bounded_recording_scenes_remain_unchanged()
    {
        var r = Request("Complete script."); r = r with { Scenes = [r.Scenes[0] with { Key = new string('s', 80), VoiceGeneration = null, VoiceVersionId = Guid.NewGuid() }] };
        Assert.Equal(JsonSerializer.Serialize(r), JsonSerializer.Serialize(WatchNarration.Plan(r, "Complete script.")));
    }
}
