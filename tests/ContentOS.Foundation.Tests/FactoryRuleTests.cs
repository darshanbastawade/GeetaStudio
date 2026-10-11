using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Foundation.Tests;

public sealed class FactoryRuleTests
{
    [Fact]
    public void Bounded_generation_and_missing_duplicate_limits_are_rejected()
    {
        Assert.Throws<RuntimeValidationException>(() => FactoryRules.Budget(new(0, "INR", 500, 1, 1)));
        Assert.Throws<RuntimeValidationException>(() => FactoryRules.Budget(new(-1, "INR", 500, 600, 1)));
        Assert.Equal(1, FactoryRules.Similarity("Pause, check the context!", "check context the pause"));
        Assert.True(FactoryRules.Similarity("Observe the playground turn.", "Verify a school gathering question.") < .7);
    }
    [Fact]
    public void Chronological_order_and_unknown_formats_do_not_silently_pass()
    {
        var f = new FactoryTestFixture.Fixture(new(Guid.NewGuid(), "fixture"), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var req = FactoryTestFixture.Request(f); FactoryRules.Plan(req);
        Assert.Throws<RuntimeValidationException>(() => FactoryRules.Plan(req with { Briefs = [req.Briefs[1] with { Position = 1 }, req.Briefs[0] with { Position = 2 }] }));
        Assert.Throws<RuntimeValidationException>(() => FactoryRules.Plan(req with { Briefs = [req.Briefs[0] with { Formats = [new("Publish", "Not allowed")] }] }));
    }
}
