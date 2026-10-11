using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class LessonRuleTests
{
    private static AudienceLessonRules Audience => new(Guid.NewGuid(), 8, 14, "Simple", ["context"], 3000, "Warm", ["gambling"], ["context-goal"], "A friend asks a question.", "Share turns.", "Pause.", "What is fair?", new());
    private static LocaleLessonRules Locale => new("mr-IN", "en-IN", [new("context", "संदर्भ", "sandarbha", "review required")], "Keep source", "Reviewed only", ["context"], []);
    private static LessonSections Source => new("Arjuna asks.", "Arjuna · verse 1.1 · exact source", "An attributed context interpretation.", "A context example.", "Pause.", "Reflect.", ["context-goal"], ["Arjuna"], ["context"]);
    [Fact]
    public void Child_data_policy_and_consent_are_separate_from_configurable_writing_age_bands()
    {
        LessonRules.Profile(new(Guid.NewGuid(), "child", 1, "Audience", Audience: Audience));
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Profile(new(Guid.NewGuid(), "unsafe", 1, "Audience", Audience: Audience with { ChildPolicy = new(PersistentConversationProfile: true) })));
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Profile(new(Guid.NewGuid(), "unsafe", 1, "Audience", Audience: Audience with { ChildPolicy = new(BehavioralAdvertising: true) })));
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Profile(new(Guid.NewGuid(), "unsafe", 1, "Audience", Audience: Audience with { ChildPolicy = new(PersonalizationEnabled: true) })));
        LessonRules.Profile(new(Guid.NewGuid(), "custom-band", 1, "Audience", Audience: Audience with { MinAge = 9, MaxAge = 13 }));
    }
    [Fact]
    public void Translation_preserves_names_anchors_and_goals_and_never_relabels_Sanskrit()
    {
        var target = Source with { Situation = "Arjuna: संदर्भ", Interpretation = "संदर्भ", Example = "संदर्भ", PracticalAction = "संदर्भ", Reflection = "संदर्भ" };
        LessonRules.Memory(Source, target, Locale);
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Memory(Source, target with { SourceAnchor = "rewritten Sanskrit" }, Locale));
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Memory(Source, target with { Names = ["changed name"] }, Locale));
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Memory(Source, target with { LearningGoals = [] }, Locale));
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Profile(new(Guid.NewGuid(), "sa", 1, "Locale", Locale: Locale with { Locale = "sa-IN" })));
        Assert.Throws<RuntimeValidationException>(() => new ReviewedMemoryTranslationA().Translate(new(Source, Locale, new("reviewed-memory.a.v1"), [])));
    }
    [Fact]
    public void Unsuitable_examples_and_lost_learning_goals_fail_the_owned_gate()
    {
        LessonRules.Gate(Source, Audience, Locale);
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Gate(Source with { Example = "Try gambling" }, Audience, Locale));
        Assert.Throws<RuntimeValidationException>(() => LessonRules.Gate(Source with { LearningGoals = ["wrong"] }, Audience, Locale));
    }
}
