using ContentOS.Domain;

namespace ContentOS.Foundation.Tests;

public sealed class CorpusPolicyTests
{
    [Fact]
    public void Search_normalization_does_not_change_the_exact_unicode_source()
    {
        var exact = "धर्मक्षेत्रे\n  कुरुक्षेत्रे\tCafe\u0301";
        var copy = exact;
        Assert.Equal("धर्मक्षेत्रे कुरुक्षेत्रे Café", CorpusRules.Normalize(exact));
        Assert.Equal(copy, exact);
        Assert.NotEqual(exact, CorpusRules.Normalize(exact));
    }
    [Theory]
    [InlineData(0, 1)]
    [InlineData(19, 1)]
    [InlineData(1, 0)]
    public void Invalid_canonical_locators_are_rejected(int chapter, int verse) =>
        Assert.Throws<RuntimeValidationException>(() => CorpusRules.Locator(chapter, verse));
    [Fact]
    public void Invalid_unicode_and_control_characters_cannot_be_marked_canonical()
    {
        Assert.Throws<RuntimeValidationException>(() => CorpusRules.Exact("bad\uFFFD"));
        Assert.Throws<RuntimeValidationException>(() => CorpusRules.Exact("bad\u0000"));
        Assert.Throws<RuntimeValidationException>(() => CorpusRules.Exact("bad\uD800"));
    }
}
