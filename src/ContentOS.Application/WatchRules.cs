using System.Security.Cryptography;
using System.Text;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class WatchRules
{
    public static readonly string[] Aspects = ["Vertical", "Standard"];
    public static (int Width, int Height) Size(string aspect) => aspect switch { "Vertical" => (360, 640), "Standard" => (640, 360), _ => throw new RuntimeValidationException("Unsupported WATCH aspect ratio.") };
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static void Template(WatchTemplateRules r)
    { if (r.FrameRate != 24 || r.CaptionSize is < 20 or > 32 || r.SafeMarginPercent is < 8 or > 15 || r.Font != "Nirmala UI" || r.CreditSeconds is < 3 or > 12 || !System.Text.RegularExpressions.Regex.IsMatch(r.Background, "^#[0-9a-fA-F]{6}$")) throw new RuntimeValidationException("Bounded versioned WATCH layout required."); }
    public static void Script(CreateWatch r, string body)
    {
        CoreRules.Text(r.Key, 100, "Episode key"); if (r.Version < 1 || r.Scenes.Length is < 1 or > 24 || r.Aspects.Length is < 1 or > 2 || r.Aspects.Distinct().Count() != r.Aspects.Length || r.Aspects.Any(x => !Aspects.Contains(x)) || r.MaxMediaCost < 0 || r.MaxMediaCost > 1000 || r.DeadlineSeconds is < 30 or > 900 || body.Length > 3200) throw new RuntimeValidationException("Bounded WATCH scenes/aspects/cost/deadline required.");
        var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(body).Append(body.Length).ToHashSet(); var offset = 0; var keys = new HashSet<string>(); foreach (var s in r.Scenes) { CoreRules.Text(s.Key, 80, "Scene key"); if (!keys.Add(s.Key) || s.ScriptStart != offset || s.ScriptLength is < 1 or > 160 || s.ScriptStart + s.ScriptLength > body.Length || !boundaries.Contains(s.ScriptStart + s.ScriptLength) || s.CharacterId == Guid.Empty || s.PresentationId == Guid.Empty || (s.ImageVersionId.HasValue) == (s.ImageGeneration != null) || (s.VoiceVersionId.HasValue) == (s.VoiceGeneration != null)) throw new RuntimeValidationException("Scenes must cover the exact script once, in order, with one explicit image and voice source per scene."); offset += s.ScriptLength; }
        if (offset != body.Length) throw new EvidenceBlockedException("WATCH cannot silently omit approved script or factual context.");
    }
    public static WatchCreditPage[] CreditPages(WatchTimeline t)
    {
        if (t.Credits != null) return t.Credits;
        var texts = new List<string> { (t.FictionLabel.StartsWith("Historical learning", StringComparison.Ordinal) ? "Historical learning. " : "Original fiction. ") + t.Title + "\nEvidence and rights: accompanying manifest." };
        foreach (var c in t.Citations) { var text = c.Citation + "\n" + c.RequiredAttribution; var elements = System.Globalization.StringInfo.ParseCombiningCharacters(text); for (var i = 0; i < elements.Length; i += 180) { var from = elements[i]; var end = i + 180 < elements.Length ? elements[i + 180] : text.Length; texts.Add(text[from..end]); } }
        if (texts.Count > 12) throw new EvidenceBlockedException("CreditsRequireAnIndependentlyReviewedLongerEpisode");
        return texts.Select(x => new WatchCreditPage(x, Math.Max(t.Template.Configuration.Rules.CreditSeconds, Math.Ceiling(new System.Globalization.StringInfo(x).LengthInTextElements / 20.0)))).ToArray();
    }
    public static void Timing(WatchTimeline t, string aspect)
    { var duration = t.Scenes.Sum(x => x.DurationSeconds) + CreditPages(t).Sum(x => x.DurationSeconds); if (duration > (aspect == "Vertical" ? 60 : 120)) throw new EvidenceBlockedException("RequestedEpisodeDurationUnsupportedWithoutAnIndependentlyReviewedShorterScript"); foreach (var s in t.Scenes) { if (s.DurationSeconds is < .2 or > 30 || s.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length / s.DurationSeconds * 60 > 240 || new System.Globalization.StringInfo(s.Text).LengthInTextElements / s.DurationSeconds > 24) throw new EvidenceBlockedException("CaptionReadingRateOrSceneDurationRequiresCorrection"); } }
}
