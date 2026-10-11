using ContentOS.Application;
using ContentOS.Domain;
namespace ContentOS.Foundation.Tests;

public sealed class EditorialRuleTests
{
    [Fact]
    public void Approval_requires_review_and_publication_requires_scheduling_while_fixture_media_and_evidence_are_explicit()
    {
        Assert.Equal("Approved", EditorialRules.Next("Review", "Approve")); Assert.Throws<RuntimeConflictException>(() => EditorialRules.Next("Draft", "Approve"));
        Assert.Throws<RuntimeConflictException>(() => EditorialRules.Next("Review", "PublishDryRun"));
        Assert.Equal("Published", EditorialRules.Next("Scheduled", "PublishDryRun"));
        Assert.Throws<RuntimeValidationException>(() => EditorialRules.Evidence("fixture:looks-good", "Manual"));
        EditorialRules.Media("audio/wav", EditorialTestFixture.Wav()); Assert.Throws<RuntimeValidationException>(() => EditorialRules.Media("image/svg+xml", System.Text.Encoding.UTF8.GetBytes("<script>bad</script>")));
    }
}
