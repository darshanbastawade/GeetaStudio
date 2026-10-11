using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

// Behavioral fixtures, not independent providers, theological answers, human labels or multilingual quality.
public sealed class EvaluationFixtureAdapter(string key) : IEvaluationAdapter
{
    public string Key => key;
    public EvaluationOutput Evaluate(EvaluationInput i)
    {
        var c = i.Case;
        var body = (Key == "evaluation.fixture.b.v1" ? "Alternative fixture wording. " : "Controlled fixture response. ") +
            string.Join(" ", c.RequiredProperties) + " " + string.Join(" ", i.Evidence.Select(x => "[[verse:" + x.TextId + "]]"));
        var output = new EvaluationOutput(body, c.Locale, i.Evidence, c.ExpectNoAnswer, i.Cast, c.VerifiedClaims,
            c.RequiresMedia && !c.ExpectNoAnswer && c.MediaSnapshotId.HasValue ? "snapshot:" + c.MediaSnapshotId : null, c.Continuity, 0, "INR");
        return Key switch
        {
            "evaluation.fabricated.v1" => output with { Body = body + " [[verse:" + Guid.Empty + "]]", Citations = [new(Guid.Empty)] },
            "evaluation.rights.v1" => output with { Body = body + " [[verse:" + Guid.Empty + "]]", Citations = [new(Guid.Empty)] },
            "evaluation.unsafe.v1" => output with { Body = body + " " + c.ForbiddenProperties.FirstOrDefault() },
            "evaluation.fluent-failure.v1" => output with { NoAnswer = false, Claims = ["An unverified event definitely occurred"], MediaEvidenceReference = null, Cast = i.Cast.Select(x => x with { VoiceAnchor = "wrong-voice" }).ToArray() },
            _ => output
        };
    }
}
