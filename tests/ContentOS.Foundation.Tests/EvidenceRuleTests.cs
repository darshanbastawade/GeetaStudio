using ContentOS.Domain;
namespace ContentOS.Foundation.Tests;

public sealed class EvidenceRuleTests
{
    [Theory]
    [InlineData("Quotation", "Interpretation", "SourceExact")]
    [InlineData("Translation", "Translation", "Unverified")]
    [InlineData("FictionalScene", "FictionalScene", "SourceExact")]
    public void Misleading_labels_or_certainty_are_rejected(string type, string label, string uncertainty) =>
        Assert.Throws<RuntimeValidationException>(() => EvidenceRules.Classification(type, label, "author", "tradition", uncertainty));
    [Fact]
    public void Unattributed_interpretation_is_rejected() =>
        Assert.Throws<RuntimeValidationException>(() => EvidenceRules.Classification("Interpretation", "Interpretation", "", "", "Contested"));
    [Fact]
    public void Evidence_offsets_cannot_split_unicode_or_exceed_snapshot()
    {
        Assert.Throws<RuntimeValidationException>(() => EvidenceRules.Span("A😀B", 2, 1));
        Assert.Throws<RuntimeValidationException>(() => EvidenceRules.Span("A😀B", 0, 2));
        Assert.Throws<RuntimeValidationException>(() => EvidenceRules.Span("abc", 2, 2));
        EvidenceRules.Span("A😀B", 1, 2);
    }
}
