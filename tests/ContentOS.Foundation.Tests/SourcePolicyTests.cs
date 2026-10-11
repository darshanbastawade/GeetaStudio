using ContentOS.Domain;

namespace ContentOS.Foundation.Tests;

public sealed class SourcePolicyTests
{
    [Theory]
    [InlineData("Unknown", false, true, false, true, true, true, true, true, true, true, "Publish", true, false, false, false, "UnknownRights")]
    [InlineData("Approved", true, true, false, true, true, true, true, true, true, true, "Export", false, false, false, false, "Revoked")]
    [InlineData("Approved", false, false, false, true, true, true, true, true, true, true, "Export", false, false, false, false, "UnreviewedEvidence")]
    [InlineData("Approved", false, true, true, true, true, true, true, true, true, true, "Export", false, false, false, false, "Expired")]
    [InlineData("Approved", false, true, false, false, true, true, true, true, true, true, "Export", false, false, false, false, "LanguageNotPermitted")]
    [InlineData("Approved", false, true, false, true, false, true, true, true, true, true, "Export", false, false, false, false, "TerritoryNotPermitted")]
    [InlineData("Approved", false, true, false, true, true, false, true, true, true, true, "Export", false, false, false, false, "FormatNotPermitted")]
    [InlineData("Approved", false, true, false, true, true, true, false, true, true, true, "Publish", true, false, false, false, "CommercialUseNotPermitted")]
    [InlineData("Approved", false, true, false, true, true, true, true, false, true, true, "Generate", false, false, false, false, "AdaptationNotPermitted")]
    [InlineData("Approved", false, true, false, true, true, true, true, true, false, true, "Translate", false, false, false, false, "TranslationNotPermitted")]
    [InlineData("Approved", false, true, false, true, true, true, true, true, true, false, "Generate", false, false, false, false, "AiProcessingNotPermitted")]
    [InlineData("Approved", false, true, false, true, true, true, true, true, true, true, "Export", false, false, false, false, "Allowed")]
    public void Rights_are_specific_to_the_intended_use(string status, bool revoked, bool reviewed, bool expired,
        bool language, bool territory, bool format, bool commercialAllowed, bool adaptationAllowed, bool translationAllowed, bool aiAllowed,
        string purpose, bool commercial, bool adaptation, bool translation, bool ai, string expected)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(expected, SourceRules.Decide(status, revoked, reviewed, expired ? now.AddMinutes(-1) : null, now,
            language, territory, format, commercialAllowed, adaptationAllowed, translationAllowed, aiAllowed, purpose, commercial, adaptation, translation, ai));
    }
}
