using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Foundation.Tests;

public sealed class ReadRuleTests
{
    [Fact]
    public void Release_attestations_and_engagement_require_explicit_versions_modes_and_bounds()
    {
        ReadRules.Collection(new(Guid.NewGuid(), "geeta-read", 1, "A reviewed pilot", Guid.NewGuid(), "Fixture", "Human release pending."));
        Assert.Throws<RuntimeValidationException>(() => ReadRules.Collection(new(Guid.NewGuid(), "../private", 1, "Pilot", Guid.NewGuid(), "Public", "No unsafe route keys")));
        Assert.Throws<RuntimeValidationException>(() => ReadRules.Evidence(new(Guid.NewGuid(), "PrivacyChild", "Manual", "fixture:pending", "Not a human review", DateTimeOffset.UtcNow.AddDays(1))));
        Assert.Throws<RuntimeValidationException>(() => ReadRules.Evidence(new(Guid.NewGuid(), "BackupRestore", "Fixture", "fixture:expired", "Expired exercise", DateTimeOffset.UtcNow.AddDays(-1))));
        Assert.Throws<RuntimeValidationException>(() => ReadRules.Engagement(new(1, Guid.NewGuid(), "Read", false)));
        Assert.Throws<RuntimeValidationException>(() => ReadRules.Engagement(new(2, Guid.NewGuid(), "Read", true)));
        Assert.Throws<RuntimeValidationException>(() => ReadRules.Engagement(new(1, Guid.NewGuid(), "SearchText", true)));
    }
    [Fact]
    public void Public_reader_requires_action_and_reflection_in_the_exact_evaluated_body()
    {
        var sections = new LessonSections("Situation", "Source", "Interpretation", "Example", "Pause before deciding.", "What did you notice?", [], [], []);
        Assert.False(ReadRules.CoversReaderSections("A safe explanation only.", sections));
        Assert.False(ReadRules.CoversReaderSections("Pause before deciding.", sections));
        Assert.True(ReadRules.CoversReaderSections("An explanation. Pause before deciding. What did you notice?", sections));
        Assert.False(ReadRules.CoversReaderSections("Anything", sections with { PracticalAction = "" }));
    }
}
