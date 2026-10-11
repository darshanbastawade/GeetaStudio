using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Foundation.Tests;

public sealed class RegistryRuleTests
{
    private static RegistryEpisodeSnapshot Story()
    {
        var f = new RegistryTestFixture.Fixture(new(Guid.NewGuid(), "fixture"), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var rules = RegistryTestFixture.Rules(f); var character = new RegistryCharacterView(Guid.NewGuid(), Guid.NewGuid(), "learner", "Core", "Active", "row", new(Guid.NewGuid(), "learner", 1, rules, "Approved", "fixture:profile", "row"), null);
        return new(RegistryTestFixture.Brief(f), "Gentle", new(f.Bible, "bible", 1, RegistryTestFixture.Bible(f.Audience), "Approved", "fixture:bible", "row"),
            [new(character, "Lead", true, ["Eligible"], null, 0)], [], [new(character.CharacterId, "FictionalDialogue", "I choose my next step.", ["curious"], [], true)], false, false, null, null, false, "Original fiction", "");
    }
    [Fact]
    public void Mandatory_forbidden_treatments_and_false_canonical_attribution_cannot_be_overridden()
    {
        var s = Story(); foreach (var treatment in new[] { "DisabilityRidicule", "ReligiousDemeaning", "RealSuffering", "FictionAsHistory", "disabilityridicule" })
            Assert.Throws<RuntimeValidationException>(() => RegistryRules.Story(s with { Beats = [s.Beats[0] with { HumorTreatments = [treatment] }] }));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Story(s with { Beats = [s.Beats[0] with { Label = "Scripture" }] }));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Story(s with { Beats = [s.Beats[0] with { ClaimsCanonicalAuthority = true }] }));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Story(s with { Beats = [s.Beats[0] with { Text = "Krishna said: invented words" }] }));
    }
    [Fact]
    public void Configured_humor_controls_and_editorial_escalation_ignore_model_confidence()
    {
        var s = Story(); Assert.False(RegistryRules.Story(s)); Assert.True(RegistryRules.Story(s with { ReferencesCurrentEvents = true })); Assert.True(RegistryRules.Story(s with { HumorStyle = "Satire" }));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Story(s with { Bible = s.Bible with { Rules = s.Bible.Rules with { HumorEnabled = false } } }));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Story(s with { Brief = s.Brief with { SubjectTags = ["real-suffering"] } }));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Story(s with { Beats = [s.Beats[0] with { MakesChoice = false }] }));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Story(s with { Beats = [s.Beats[0] with { TraitClaims = ["invented permanent trait"] }] }));
    }
    [Fact]
    public void Episode_scope_production_media_and_cast_budget_are_owned_constraints()
    {
        var s = Story(); RegistryRules.Character(s.Cast[0].Character.Profile.Rules, "Guest");
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Character(s.Cast[0].Character.Profile.Rules, "EpisodeOnly"));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Brief(s.Brief with { Mode = "Production" }));
        Assert.Throws<RuntimeValidationException>(() => RegistryRules.Brief(s.Brief with { Budget = new(1, -1, "INR") }));
    }
}
