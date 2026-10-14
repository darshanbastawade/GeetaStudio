using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class OptimizationRules
{
    public static readonly string[] Metrics = ["CorrectionRate", "EditorialRework", "LearningFeedback", "Retention", "AudienceSatisfaction", "Conversion", "Cost", "OperationalFailure"];
    public static void Require(bool ok, string reason) { if (!ok) throw new RuntimeValidationException(reason); }
    public static void Proposal(OptimizationProposal r)
    {
        Require(r.OperationId != Guid.Empty && r.Version > 0 && r.Key.Length is > 0 and <= 100 && r.Hypothesis.Length is > 0 and <= 2000, "Versioned hypothesis required.");
        Require(new[] { "Prompt", "Provider", "Retrieval", "Narrative", "Format", "Schedule" }.Contains(r.Dimension), "Supported experiment dimension required.");
        Require(r.Mode is "Shadow" or "Canary" && r.Cohort == "PrivateFixture", "Only explicitly private fixture cohorts are qualified in this release.");
        Require(r.AudienceId != Guid.Empty && r.Locale.Length is > 0 and <= 20 && r.CandidateId != r.BaselineCandidateId, "Distinct baseline/candidate and audience/locale required.");
        Require(r.MinimumDevelopmentCases is >= 1 and <= 100 && r.MinimumHeldOutCases is >= 1 and <= 20, "Bounded development and held-out minimums required.");
        Require(r.MaxDispatches is >= 1 and <= 100 && r.MaxCost is >= 0 and <= 10 && r.Currency is "USD" or "INR", "Bounded dispatch and monetary limits required.");
        Require(r.EndsUtc > DateTimeOffset.UtcNow && r.EndsUtc <= DateTimeOffset.UtcNow.AddDays(30), "Bounded experiment deadline required.");
        Require(r.Guardrails.Length == Metrics.Length && r.Guardrails.Select(x => x.Metric).Distinct().Count() == Metrics.Length && Metrics.All(m => r.Guardrails.Any(g => g.Metric == m)), "All eight quality, usefulness and economic guardrails required.");
        Require(r.Guardrails.All(g => g.Minimum <= g.Maximum && g.MinimumSamples is >= 1 and <= 100000 && g.Minimum >= 0 && (g.Metric == "Cost" || g.Maximum <= 1)), "Valid guardrail ranges/sample minimums required.");
        Evidence(r.EvidenceReference);
    }
    public static void Evidence(string value) => Require(value.Length is > 0 and <= 200 && value.StartsWith("fixture:", StringComparison.Ordinal), "Private fixture evidence reference required; human acceptance remains separate.");
    public static bool Breach(OptimizationProposal p, OptimizationMeasurement r) => p.Guardrails.Any(g => g.Metric == r.Metric && (r.Value < g.Minimum || r.Value > g.Maximum));
}
