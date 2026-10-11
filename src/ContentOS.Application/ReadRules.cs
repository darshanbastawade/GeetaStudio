using System.Text.RegularExpressions;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class ReadRules
{
    public static readonly string[] Gates = ["Rights", "PrivacyChild", "AccessibilityMobile", "BackupRestore", "Rollback", "Monitoring", "AbuseLimits"];
    public static void Collection(RegisterReadCollection r)
    {
        if (!Regex.IsMatch(r.Key, "^[a-z][a-z0-9-]{1,59}$") || r.Version < 1 || r.Mode is not ("Fixture" or "Public")) throw new RuntimeValidationException("Bounded stable key, version and explicit mode required.");
        CoreRules.Text(r.Title, 200, "Collection title"); CoreRules.Text(r.Rationale, 2000, "Pilot rationale");
    }
    public static void Evidence(ReadGateEvidence r)
    {
        if (!Gates.Contains(r.Kind) || r.Mode is not ("Fixture" or "Manual") || !r.Reference.StartsWith(r.Mode == "Manual" ? "manual:" : "fixture:", StringComparison.OrdinalIgnoreCase)
            || r.ExpiresUtc <= DateTimeOffset.UtcNow || r.ExpiresUtc > DateTimeOffset.UtcNow.AddDays(30)) throw new RuntimeValidationException("Current bounded, explicitly attested release evidence required.");
        CoreRules.Text(r.Reference, 1000, "Evidence"); CoreRules.Text(r.Notes, 2000, "Review notes");
    }
    public static bool CoversReaderSections(string body, LessonSections sections) =>
        !string.IsNullOrWhiteSpace(sections.PracticalAction) && !string.IsNullOrWhiteSpace(sections.Reflection)
        && body.Contains(sections.PracticalAction, StringComparison.Ordinal) && body.Contains(sections.Reflection, StringComparison.Ordinal);
    public static void Engagement(ReadEngagement r)
    {
        if (r.Version != 1 || !r.Consent || r.Name is not ("Read" or "Bookmark" or "Progress") || r.PieceId == Guid.Empty) throw new RuntimeValidationException("Version1 consented aggregate event required.");
    }
}
