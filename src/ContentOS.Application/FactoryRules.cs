using System.Text.RegularExpressions;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class FactoryRules
{
    public static readonly string[] Formats = ["Explanation", "Story", "ComicScript", "ShortVideoScript", "Carousel", "Reflection"];
    public static void Plan(RegisterFactoryPlan r)
    {
        CoreRules.Text(r.Key, 100, "Plan key"); CoreRules.Text(r.Title, 200, "Title");
        if (r.Version < 1 || r.JourneyKind is not ("Chronological" or "PracticalTheme") || r.DuplicateThreshold is < .7 or > 1 || r.Briefs == null || r.Briefs.Length is < 1 or > 10) throw new RuntimeValidationException("Bounded versioned plan required.");
        if (r.Briefs.Select(x => x.Key).Distinct().Count() != r.Briefs.Length) throw new RuntimeValidationException("Brief keys must be distinct.");
        foreach (var b in r.Briefs)
        {
            CoreRules.Text(b.Key, 100, "Brief key"); CoreRules.Text(b.LearningGoal, 500, "Learning goal"); CoreRules.Text(b.Journey, 200, "Journey");
            RegistryRules.Brief(b.Cast); LessonRules.Words(b.Concepts, 20, true);
            if (b.Position < 1 || b.Priority is < 0 or > 100 || b.Chapter < 1 || b.Verse < 1 || b.RelationshipRevisionIds == null || b.RelationshipRevisionIds.Length > 20 ||
               b.Formats == null || b.Formats.Length is < 1 or > 6 || b.Formats.Select(f => f.Kind).Distinct().Count() != b.Formats.Length) throw new RuntimeValidationException("Invalid brief dimensions.");
            foreach (var f in b.Formats) { CoreRules.Text(f.Instruction, 1000, "Format direction"); if (!Formats.Contains(f.Kind) || f.MaxCharacters is < 500 or > 16000 || f.EstimatedMediaCost is < 0 or > 100000 || f.ReviewMinutes is < 1 or > 240 || f.MediaMinutes is < 0 or > 1000) throw new RuntimeValidationException("Invalid bounded format."); }
        }
        if (r.JourneyKind == "Chronological" && !r.Briefs.OrderBy(b => b.Position).Select(b => b.Chapter * 1000 + b.Verse).SequenceEqual(r.Briefs.OrderBy(b => b.Chapter).ThenBy(b => b.Verse).Select(b => b.Chapter * 1000 + b.Verse))) throw new RuntimeValidationException("Chronological journey must follow approved verse order.");
    }
    public static void Budget(FactoryBudget b) { CoreRules.Text(b.Currency, 10, "Currency"); if (b.MaxGenerationCost < 0 || b.MaxGeneratedCharacters is < 500 or > 1000000 || b.DeadlineSeconds is < 5 or > 3600 || b.MaxItems is < 1 or > 60) throw new RuntimeValidationException("Invalid bounded batch budget."); }
    public static double Similarity(string a, string b)
    {
        HashSet<string> Tokens(string s) => Regex.Matches(s.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(x => x.Value).ToHashSet();
        var x = Tokens(a); var y = Tokens(b); return x.Count == 0 && y.Count == 0 ? 1 : (double)x.Intersect(y).Count() / Math.Max(1, x.Union(y).Count());
    }
}
