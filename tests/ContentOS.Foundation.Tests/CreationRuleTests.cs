using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Integrations;
using Xunit;
namespace ContentOS.Foundation.Tests;

public sealed class CreationRuleTests
{
    [Fact]
    public async Task Bounded_personalization_and_printable_output_escape_untrusted_content_and_preserve_alt_and_credits()
    {
        Assert.Throws<ContentOS.Domain.RuntimeValidationException>(() => CreationRules.Input(new("someone@example.com", "patience"), ["patience"])); Assert.Throws<ContentOS.Domain.RuntimeValidationException>(() => CreationRules.Input(new("Learner", "arbitrary"), ["patience"]));
        var renderer = new CreationRenderer(); var result = await renderer.RenderAsync(new(Guid.NewGuid(), "Comic", new("Learner", "patience", "<b>Own reflection</b>"), "Title", "Reviewed <source>", [], [new("image/png", MediaFiles.ReferenceCard("fixture", []), "An original fictional card", "Owned")]), default);
        Assert.Contains("&lt;source&gt;", result.Html); Assert.Contains("&lt;b&gt;", result.Html); Assert.Contains("alt=\"An original fictional card\"", result.Html); Assert.Contains("size:A4", result.Html); Assert.Equal(0, result.Cost); Assert.DoesNotContain("<script", result.Html);
        Assert.Throws<ContentOS.Domain.RuntimeValidationException>(() => renderer.Validate(new("image/svg+xml", [65], "Active SVG", "Owned")));
    }
}
