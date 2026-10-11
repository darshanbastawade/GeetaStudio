using ContentOS.Domain;

namespace ContentOS.Foundation.Tests;

public sealed class CorePolicyTests
{
    [Theory]
    [InlineData("Fiction", "Owned", true)]
    [InlineData("Canonical", "Unverified", true)]
    [InlineData("ScriptureLike", "Owned", false)]
    public void Public_knowledge_and_content_kind_must_be_explicit(string kind, string rights, bool shared) =>
        Assert.Throws<RuntimeValidationException>(() => CoreRules.Revision("title", "body", kind, rights, "fixture", shared));
    [Theory]
    [InlineData("Draft", "Approved", "Owned")]
    [InlineData("Review", "Approved", "Unverified")]
    [InlineData("Archived", "Draft", "Owned")]
    public void Editorial_rules_block_skipped_review_unverified_rights_and_archived_changes(string before, string after, string rights) =>
        Assert.Throws<RuntimeValidationException>(() => CoreRules.Transition(before, after, rights));
}
