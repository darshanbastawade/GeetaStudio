using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class SocialRules
{
    public static void Payload(SocialPayload p, SocialCapability c)
    {
        if (p.Platform != c.Platform || !c.Formats.Contains(p.Format)) throw new RuntimeValidationException("Unsupported destination format.");
        if (p.Title.EnumerateRunes().Count() > c.TitleLimit || TextLength(p.Text, p.Platform) > c.TextLimit || p.Media.Count(x => x.Kind == "Image") > c.MaxImages) throw new RuntimeValidationException("Destination length/media limits exceeded; edit and review a new package.");
        if (p.Format is "Short" or "Video" && !p.Media.Any(x => x.Kind == "Video" && (p.Format != "Short" || x.Aspect == "Vertical"))) throw new RuntimeValidationException("An approved matching WATCH master is required.");
        if (p.Format is "Image" or "Carousel" && p.Media.Count(x => x.Kind == "Image") < (p.Format == "Carousel" ? 2 : 1)) throw new RuntimeValidationException("Approved image media required.");
        if (p.Format == "Link" && p.Link.Length == 0) throw new RuntimeValidationException("Destination link required.");
        if (p.Link.Length > 0 && (!Uri.TryCreate(p.Link, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0)) throw new RuntimeValidationException("HTTPS link without credentials required.");
        if (p.Media.Any(x => x.Length > 31457280 || x.DurationSeconds > 180)) throw new RuntimeValidationException("Local export profile media ceiling exceeded; qualify a suitable versioned profile."); CoreRules.Text(p.CampaignId, 100, "Campaign identifier"); CoreRules.Text(p.Title, 100, "Destination title");
    }
    private static int TextLength(string text, string platform)
    {
        if (platform != "X") return text.EnumerateRunes().Count();
        // Conservative export subset: non-ASCII can consume two units; short URLs consume at least 23.
        var length = text.EnumerateRunes().Sum(x => x.Value <= 127 ? 1 : 2);
        foreach (System.Text.RegularExpressions.Match url in System.Text.RegularExpressions.Regex.Matches(text, @"https?://[^s]+"))
            length += Math.Max(0, 23 - url.Value.EnumerateRunes().Sum(x => x.Value <= 127 ? 1 : 2));
        return length;
    }
    public static string[] Slides(string text, int count)
    {
        if (count < 2 || count > 10) throw new RuntimeValidationException("Bounded carousel slide count required.");
        var elements = new List<string>(); var iterator = System.Globalization.StringInfo.GetTextElementEnumerator(text); while (iterator.MoveNext()) elements.Add(iterator.GetTextElement());
        return Enumerable.Range(0, count).Select(i => string.Concat(elements.Skip(i * elements.Count / count).Take((i + 1) * elements.Count / count - i * elements.Count / count))).ToArray();
    }
    public static DateTimeOffset Schedule(ScheduleSocial r)
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(r.Timezone); } catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new RuntimeValidationException("Known schedule timezone required."); }
        var now = DateTimeOffset.UtcNow;
        if (r.ScheduledUtc < now.AddSeconds(-30) || r.ScheduledUtc > now.AddDays(30) || r.DeadlineSeconds is < 30 or > 86400 || r.MaxAttempts is < 1 or > 3) throw new RuntimeValidationException("Bounded offset-bearing schedule/deadline/attempts required.");
        return r.ScheduledUtc.ToUniversalTime();
    }
}
