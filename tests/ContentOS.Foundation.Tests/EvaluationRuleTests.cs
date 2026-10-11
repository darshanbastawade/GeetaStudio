using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class EvaluationRuleTests
{
    [Fact]
    public void Fluent_outputs_cannot_hide_citation_cast_claim_media_privacy_or_child_safety_failures()
    {
        var dataset = EvaluationFixtureCatalog.Create(Guid.NewGuid()); EvaluationRules.Dataset(dataset);
        Assert.Equal(100, dataset.Cases.Count(c => c.Split == "Development")); Assert.Equal(20, dataset.Cases.Count(c => c.Split == "HeldOut"));
        Assert.Equal(20, dataset.Cases.Select(c => c.Category).Distinct().Count()); Assert.Equal(120, dataset.Cases.Select(c => c.Input).Distinct().Count());
        var c = dataset.Cases.First(c => c.Category == "age-adaptation");
        var output = new EvaluationFixtureAdapter("evaluation.fixture.a.v1").Evaluate(new(c, [], [], "v1"));
        Assert.Empty(EvaluationRules.Check(c, output, []));
        Assert.Contains(EvaluationRules.Check(c, output with { Citations = null! }, []), f => f.Code == "Schema");
        Assert.Contains(EvaluationRules.Check(c, output with { Body = output.Body + " meet a stranger alone" }, []), f => f.Code == "ForbiddenProperty");
        Assert.Contains(EvaluationRules.Check(c, output with { Body = output.Body + " child@example.org" }, []), f => f.Code == "Privacy");
        Assert.Contains(EvaluationRules.Check(c, output with { Body = output.Body + " Gita 99:99" }, []), f => f.Code == "UnboundVerseLocator");
        var media = dataset.Cases.First(c => c.Category == "unavailable-transcript"); var good = new EvaluationFixtureAdapter("evaluation.fixture.a.v1").Evaluate(new(media, [], [], "v1"));
        Assert.Contains(EvaluationRules.Check(media, good with { NoAnswer = false, MediaEvidenceReference = null }, []), f => f.Code == "MissingMediaEvidence");
        Assert.Contains(EvaluationRules.Check(media, good with { NoAnswer = false, Claims = ["unverified"] }, []), f => f.Code == "ForcedConnection");
        var member = new EvaluationCastMember(Guid.NewGuid(), Guid.NewGuid(), "Lead", "owned-voice", "Child");
        Assert.Contains(EvaluationRules.Check(c, output with { Cast = [member with { VoiceAnchor = "other" }] }, [member]), f => f.Code == "InappropriateCast");
        Assert.Contains(EvaluationRules.Check(c, output with { Cast = [member with { LifeStage = "Adult" }] }, [member]), f => f.Code == "LifeStage");
    }
    [Fact]
    public void Declared_citation_guid_digits_are_not_personal_identifiers_but_prose_and_invalid_markers_are_checked()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-123456789012");
        var c = EvaluationFixtureCatalog.Create(Guid.NewGuid(), id).Cases.First();
        var output = new EvaluationFixtureAdapter("evaluation.fixture.a.v1").Evaluate(new(c, [new(id)], [], "v1"));
        Assert.DoesNotContain(EvaluationRules.Check(c, output, []), x => x.Code == "Privacy");
        Assert.Contains(EvaluationRules.Check(c, output with { Body = output.Body + " identifier 123456789012" }, []), x => x.Code == "Privacy");
        Assert.Contains(EvaluationRules.Check(c, output with { Body = output.Body + " [[verse:123456789012]]" }, []), x => x.Code == "Privacy");
        Assert.Contains(EvaluationRules.Check(c, output with { Body = output.Body + " person@example.org" }, []), x => x.Code == "Privacy");
    }
    [Fact]
    public void Fixture_switch_expects_properties_not_identical_wording_and_rubrics_are_bounded()
    {
        var c = EvaluationFixtureCatalog.Create(Guid.NewGuid()).Cases.First(); var i = new EvaluationInput(c, [], [], "v1");
        var a = new EvaluationFixtureAdapter("evaluation.fixture.a.v1").Evaluate(i); var b = new EvaluationFixtureAdapter("evaluation.fixture.b.v1").Evaluate(i);
        Assert.NotEqual(a.Body, b.Body); Assert.Empty(EvaluationRules.Check(c, a, [])); Assert.Empty(EvaluationRules.Check(c, b, []));
        var invalid = EvaluationFixtureCatalog.Create(Guid.NewGuid()); Assert.Throws<RuntimeValidationException>(() => EvaluationRules.Dataset(invalid with { Cases = [invalid.Cases[0], invalid.Cases[0]] }));
        Assert.Equal(19, EvaluationRules.P95(Enumerable.Range(1, 20).Select(x => (double)x)));
    }
}
