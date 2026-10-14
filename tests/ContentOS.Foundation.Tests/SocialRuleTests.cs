using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Integrations;
using Xunit;
namespace ContentOS.Foundation.Tests;

public sealed class SocialRuleTests
{
    private static SocialPayload Payload(string platform, string format, string text = "", string link = "") => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", platform, "fixture:account", format, "Title", text, "", "campaign", link, [], [], false, "en-IN", Guid.NewGuid(), "Fiction", [], []);
    [Fact]
    public void Destinations_enforce_formats_media_and_conservative_text_limits()
    {
        Assert.Throws<RuntimeValidationException>(() => SocialRules.Payload(Payload("YouTube", "Article"), SocialChannelProfiles.Capability("youtube.export.v1")));
        Assert.Throws<RuntimeValidationException>(() => SocialRules.Payload(Payload("Instagram", "Short"), SocialChannelProfiles.Capability("instagram.export.v1")));
        Assert.Throws<RuntimeValidationException>(() => SocialRules.Payload(Payload("X", "Link", new string('a', 281), "https://example.invalid"), SocialChannelProfiles.Capability("x.export.v1")));
        SocialRules.Payload(Payload("Facebook", "Article", "Original caption"), SocialChannelProfiles.Capability("facebook.export.v1"));
        Assert.Throws<RuntimeValidationException>(() => SocialRules.Payload(Payload("X", "Link", "caption", "https://user:password@example.invalid"), SocialChannelProfiles.Capability("x.export.v1")));
    }
    [Fact]
    public void Schedules_preserve_offset_instant_and_bound_attempts_deadline_and_timezone()
    {
        var when = DateTimeOffset.UtcNow.AddMinutes(5).ToOffset(TimeSpan.FromHours(5.5)); var r = new ScheduleSocial(Guid.NewGuid(), Guid.NewGuid(), when, "India Standard Time");
        Assert.Equal(when.UtcDateTime, SocialRules.Schedule(r).UtcDateTime);
        Assert.Throws<RuntimeValidationException>(() => SocialRules.Schedule(r with { Timezone = "invented-zone" })); Assert.Throws<RuntimeValidationException>(() => SocialRules.Schedule(r with { MaxAttempts = 4 })); Assert.Throws<RuntimeValidationException>(() => SocialRules.Schedule(r with { ScheduledUtc = DateTimeOffset.UtcNow.AddDays(31) }));
    }
    [Fact]
    public void Carousel_splits_preserve_graphemes_and_x_urls_use_conservative_weight()
    {
        const string text = "कर्म🙂 सीखना"; Assert.Equal(text, string.Concat(SocialRules.Slides(text, 3)));
        Assert.Throws<RuntimeValidationException>(() => SocialRules.Payload(Payload("X", "Link", new string('a', 268) + " https://x.co", "https://x.co"), SocialChannelProfiles.Capability("x.export.v1")));
    }
}
