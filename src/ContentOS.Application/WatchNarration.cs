using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class WatchNarration
{
    public const int SegmentLimit = 160;
    public static CreateWatch Plan(CreateWatch request, string approvedBody, WatchTemplateRules? layout = null)
    {
        if (request.OperationId == Guid.Empty || approvedBody.Length is < 1 or > 3200 || request.Scenes.Length is < 1 or > 24 || request.Aspects.Length is < 1 or > 2) throw new RuntimeValidationException("Stable operation and bounded approved narration required.");
        var boundaries = StringInfo.ParseCombiningCharacters(approvedBody).Append(approvedBody.Length).ToArray();
        layout ??= new(); WatchRules.Template(layout);
        // Budget caption lines below even the tallest image allowed by the pinned
        // compositor. Keep conservative leading for the reviewed font and margins.
        var layoutLimit = request.Aspects.Select(aspect =>
        {
            var (_, height) = WatchRules.Size(aspect);
            var imageBottom = (height - height / 2) / 3.0 + height / 2;
            var available = height * (1 - layout.SafeMarginPercent / 100.0) - imageBottom;
            var lines = Math.Max(1, (int)Math.Floor(available / (layout.CaptionSize * 1.5)));
            return Math.Min(SegmentLimit, lines * (aspect == "Vertical" ? 20 : 40));
        }).Min();
        var scenes = new List<WatchSceneInput>(); var offset = 0;
        foreach (var input in request.Scenes)
        {
            CoreRules.Text(input.Key, 80, "Narration part key");
            if (input.ScriptStart != offset || input.ScriptLength < 1 || (long)offset + input.ScriptLength > approvedBody.Length || !boundaries.Contains(offset) || !boundaries.Contains(offset + input.ScriptLength)) throw new RuntimeValidationException("Narration parts must cover exact grapheme boundaries in script order.");
            var end = offset + input.ScriptLength;
            if (input.ScriptLength <= SegmentLimit && (input.VoiceGeneration == null || input.ScriptLength <= layoutLimit)) { scenes.Add(input); offset = end; continue; }
            CoreRules.Text(input.Key, 60, "Long narration part key");
            if (!input.ImageVersionId.HasValue || input.ImageGeneration != null || input.VoiceVersionId.HasValue || input.VoiceGeneration == null || input.VoiceGeneration.PromptOrText != approvedBody.Substring(offset, input.ScriptLength)) throw new EvidenceBlockedException("LongNarrationRequiresReviewedImageAndExactGeneratedSpeech; imported recordings need separately reviewed segment recordings.");
            while (offset < end)
            {
                var next = boundaries.LastOrDefault(x => x > offset && x <= Math.Min(end, offset + layoutLimit));
                if (next <= offset) throw new EvidenceBlockedException("NarrationGraphemeExceedsSupportedStageLimit");
                // Prefer a word boundary without removing or duplicating any whitespace.
                if (next < end) { var word = boundaries.LastOrDefault(x => x > offset + layoutLimit / 2 && x <= next && char.IsWhiteSpace(approvedBody[x - 1])); if (word > offset) next = word; }
                var key = input.Key + "-part" + offset.ToString("D4", CultureInfo.InvariantCulture);
                var operation = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(request.OperationId + "|narration.v1|" + key)).AsSpan(0, 16));
                var speech = input.VoiceGeneration with
                {
                    OperationId = operation,
                    PromptOrText = approvedBody[offset..next],
                    Admission = null,
                    Output = input.VoiceGeneration.Output with { OperationId = operation, Key = "narration-" + operation.ToString("N"), Version = 1 }
                };
                scenes.Add(input with { Key = key, ScriptStart = offset, ScriptLength = next - offset, VoiceGeneration = speech });
                offset = next;
            }
        }
        var result = request with { Scenes = scenes.ToArray() };
        WatchRules.Script(result, approvedBody);
        return result;
    }
}
