using ContentOS.Application;
using ContentOS.Domain;
namespace ContentOS.Foundation.Tests;

public sealed class OptimizationRuleTests
{
    [Fact]
    public void Publishing_live_cohorts_missing_guardrails_and_unbounded_spend_are_rejected()
    {
        var r = OptimizationTestFixture.Proposal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); OptimizationRules.Proposal(r);
        Assert.Throws<RuntimeValidationException>(() => OptimizationRules.Proposal(r with { Dimension = "Payment" }));
        Assert.Throws<RuntimeValidationException>(() => OptimizationRules.Proposal(r with { Cohort = "Children" }));
        Assert.Throws<RuntimeValidationException>(() => OptimizationRules.Proposal(r with { Guardrails = r.Guardrails[..7] }));
        Assert.Throws<RuntimeValidationException>(() => OptimizationRules.Proposal(r with { MaxCost = 1000 }));
    }
}
