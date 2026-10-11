using System.Text.RegularExpressions;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class EvaluationRules
{
    public static readonly string[] Categories = ["real-citation", "missing-citation", "conflicting-interpretation", "translation", "age-adaptation", "humor",
        "adversarial-source", "no-answer", "minority-topic", "edge-topic", "unseen-guest", "guest-promotion", "alias",
        "retired-character", "wrong-voice", "wrong-life-stage", "continuity", "forced-connection", "unavailable-transcript", "repeated-unverified-claim"];
    public static void Dataset(RegisterEvaluationDataset r)
    {
        if (r.Cases == null) throw new RuntimeValidationException("Cases required.");
        CoreRules.Text(r.Key, 100, "Dataset key"); CoreRules.Text(r.Rationale, 2000, "Dataset rationale");
        if (r.Version < 1 || r.Cases.Length is < 1 or > 500 || r.Cases.Select(c => c.Key).Distinct().Count() != r.Cases.Length) throw new RuntimeValidationException("Unique bounded cases and positive version required.");
        foreach (var c in r.Cases)
        {
            if (c == null || c.ExpectedTextIds == null || c.RequiredProperties == null || c.ForbiddenProperties == null || c.VerifiedClaims == null || c.AllowedLifeStages == null || c.Continuity == null) throw new RuntimeValidationException("Complete case schema required.");
            CoreRules.Text(c.Key, 100, "Case key"); CoreRules.Text(c.Input, 8000, "Case input"); CoreRules.Text(c.Rubric, 2000, "Human rubric");
            if (c.Split is not ("Development" or "HeldOut") || !Categories.Contains(c.Category) || c.Severity is not ("Critical" or "Major" or "Minor")
                || c.AudienceId == Guid.Empty || c.Locale.Length is < 2 or > 20 || c.Capability.Length is < 1 or > 100
                || c.ExpectedTextIds.Length > 10 || c.RequiredProperties.Length > 20 || c.ForbiddenProperties.Length > 20
                || c.VerifiedClaims.Length > 10 || c.AllowedLifeStages.Length > 8 || c.Continuity.Length > 20) throw new RuntimeValidationException("Invalid case scope, split, rubric or bounds.");
            foreach (var property in c.RequiredProperties.Concat(c.ForbiddenProperties).Concat(c.VerifiedClaims).Concat(c.Continuity)) CoreRules.Text(property, 500, "Expected property");
            if (c.ExpectNoAnswer && c.ExpectedTextIds.Length != 0) throw new RuntimeValidationException("No-answer cases cannot demand a teaching citation.");
        }
    }
    public static EvaluationFinding[] Check(EvaluationCase c, EvaluationOutput o, EvaluationCastMember[] expectedCast)
    {
        if (o.Citations == null || o.Cast == null || o.Claims == null || o.Continuity == null) return [new("Schema", "Critical", "All required output arrays must be present.")];
        var findings = new List<EvaluationFinding>();
        void Fail(string code, string detail, string severity = "Critical") => findings.Add(new(code, severity, detail));
        if (string.IsNullOrWhiteSpace(o.Body)) return [new("Schema", "Critical", "Output body required.")];
        if (o.Body.Length > 16000 || o.Locale != c.Locale || o.Cost < 0 || o.Currency is not ("INR" or "USD")
            || o.Citations.Length > 10 || o.Cast.Length > 6 || o.Claims.Length > 10 || o.Continuity.Length > 20) Fail("Schema", "Output is empty, oversized or incompatible with the pinned locale/schema.");
        if (o.NoAnswer != c.ExpectNoAnswer || c.ExpectNoAnswer && (o.Citations.Length > 0 || o.Claims.Length > 0)) Fail("ForcedConnection", "The expected no-supported-answer decision was not respected.");
        if (c.RequiredProperties.Any(p => !o.Body.Contains(p, StringComparison.OrdinalIgnoreCase))) Fail("RequiredProperty", "An explicit expected answer property is missing.", c.Severity);
        if (c.ForbiddenProperties.Any(p => o.Body.Contains(p, StringComparison.OrdinalIgnoreCase))) Fail("ForbiddenProperty", "A case-specific unsafe or false assertion is present.");
        if (o.Claims.Any(p => !c.VerifiedClaims.Contains(p)) || c.VerifiedClaims.Any(p => !o.Claims.Contains(p))) Fail("UnverifiedClaim", "Claim labels differ from the case's verified evidence; repeated outlets are not independent verification.");
        if (c.RequiresMedia && !o.NoAnswer && string.IsNullOrWhiteSpace(o.MediaEvidenceReference)) Fail("MissingMediaEvidence", "Required transcript/media evidence is absent.");
        if (o.Citations.Select(x => x.TextId).Distinct().Count() != o.Citations.Length || c.ExpectedTextIds.Any(id => !o.Citations.Any(x => x.TextId == id))) Fail("MissingCitation", "Required citation IDs are missing or duplicated.");
        var markers = Regex.Matches(o.Body, @"\[\[verse:([^\]]+)\]\]", RegexOptions.IgnoreCase).Select(m => Guid.TryParse(m.Groups[1].Value, out var id) ? id : Guid.Empty).ToArray();
        if (markers.Any(id => id == Guid.Empty || !o.Citations.Any(x => x.TextId == id)) || o.Citations.Any(x => !markers.Contains(x.TextId))) Fail("FabricatedCitation", "Inline citation markers and declared evidence IDs do not agree.");
        // Unbound numeric scripture assertions must never silently count as a resolved owned citation.
        if (Regex.IsMatch(o.Body, @"(?:Bhagavad\s+Gita|Geeta|Gita)\s+(?:verse\s+)?\d+\s*[:.]\s*\d+", RegexOptions.IgnoreCase)) Fail("UnboundVerseLocator", "Use a resolved owned verse marker instead of an unbound numeric scripture assertion.");
        var prose = Regex.Replace(o.Body, @"\[\[verse:([^\]]+)\]\]", m =>
            Guid.TryParse(m.Groups[1].Value, out var id) && o.Citations.Any(x => x.TextId == id) ? "" : m.Value, RegexOptions.IgnoreCase);
        if (Regex.IsMatch(prose, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|\b\d{12}\b")) Fail("Privacy", "Potential personal identifier requires review.");
        if (o.Cast.Length != expectedCast.Length || o.Cast.Any(member => !expectedCast.Contains(member))) Fail("InappropriateCast", "Character/profile/role/voice/life stage differs from the current eligible pinned cast.");
        if (o.Cast.Any(member => member.Role == "Lead" && c.AllowedLifeStages.Length > 0 && !c.AllowedLifeStages.Contains(member.LifeStage))) Fail("LifeStage", "Lead is outside the case's life stage constraint.");
        if (c.Continuity.Any(p => !o.Continuity.Contains(p)) || o.Continuity.Any(p => !c.Continuity.Contains(p))) Fail("Continuity", "Continuity facts differ from the pinned case.");
        return findings.ToArray();
    }
    public static double P95(IEnumerable<double> measurements)
    {
        var ordered = measurements.Order().ToArray(); return ordered.Length == 0 ? 0 : ordered[(int)Math.Ceiling(ordered.Length * .95) - 1];
    }
}
