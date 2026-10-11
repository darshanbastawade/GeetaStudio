using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class LessonRules
{
    public static readonly string[] TranslationAdapters = ["reviewed-memory.a.v1", "reviewed-memory.b.v1"];
    public static void Profile(RegisterLessonProfile r)
    {
        CoreRules.Text(r.Key, 100, "Profile key");
        if (r.Version < 1 || new object?[] { r.Audience, r.Writing, r.Locale, r.Route }.Count(x => x != null) != 1) Bad("Exactly one versioned profile kind is required.");
        if (r.Kind == "Audience" && r.Audience is { } a)
        {
            Age(a.MinAge, a.MaxAge); CoreRules.Text(a.ReadingLevel, 100, "Reading level"); CoreRules.Text(a.Tone, 200, "Tone");
            if (a.AudienceId == Guid.Empty || a.MaxCharacters is < 300 or > 12000 || a.ChildPolicy == null ||
                !a.ChildPolicy.MinimalData || a.ChildPolicy.PersistentConversationProfile || a.ChildPolicy.BehavioralAdvertising ||
                a.ChildPolicy.PersonalizationEnabled || a.ChildPolicy.MaxInteractions is < 0 or > 3) Bad("Conservative data/interaction policy required; child personalization awaits consent flows.");
            Words(a.Vocabulary, 30); Words(a.SensitiveTopics, 30); Words(a.LearningGoals, 10, true);
            foreach (var x in new[] { a.Situation, a.Example, a.PracticalAction, a.Reflection }) CoreRules.Text(x, 2000, "Audience treatment");
        }
        else if (r.Kind == "Writing" && r.Writing is { } w)
        {
            Age(w.MinAge, w.MaxAge); if (w.AudienceProfileId == Guid.Empty || w.MaxCast is < 1 or > 5 || w.EligibleCharacters == null || w.EligibleCharacters.Length > 50) Bad("Invalid writing bindings.");
            Words(w.EligibleLifeStages, 10, true); Words(w.LeadRoles, 10, true); Words(w.SupportingRoles, 10);
            CoreRules.Text(w.Setting, 200, "Setting"); CoreRules.Text(w.ConflictComplexity, 200, "Conflict"); CoreRules.Text(w.Humor, 200, "Humor");
        }
        else if (r.Kind == "Locale" && r.Locale is { } l)
        {
            if (l.Locale == "sa-IN" || l.SourceLocale != "en-IN" || string.IsNullOrWhiteSpace(l.Locale) || l.Locale.Length > 30) Bad("Sanskrit is immutable source text; supported delivery locales require explicit profiles.");
            if (l.Glossary == null || l.Glossary.Length > 30 || l.Glossary.Select(x => x.Key).Distinct().Count() != l.Glossary.Length) Bad("Invalid terminology glossary.");
            foreach (var t in l.Glossary) foreach (var x in new[] { t.Key, t.Value, t.Transliteration, t.Pronunciation }) CoreRules.Text(x, 200, "Glossary");
            Words(l.RequiredConcepts, 20); Words(l.ForbiddenPhrases, 30);
            CoreRules.Text(l.TransliterationRules, 1000, "Transliteration"); CoreRules.Text(l.PronunciationRules, 1000, "Pronunciation");
        }
        else if (r.Kind == "TranslationRoute" && r.Route is { } route)
        {
            if (!TranslationAdapters.Contains(route.Adapter) || route.MaxCharacters is < 300 or > 16000 || route.DeadlineSeconds is < 1 or > 30) Bad("Only bounded reviewed-memory adapters are deployed.");
        }
        else Bad("Profile kind does not match its rules.");
    }
    private static void Age(int min, int max) { if (min < 0 || max > 120 || min > max) Bad("Invalid configurable age band."); }
    public static void Words(string[]? words, int max, bool required = false)
    {
        if (words == null || words.Length > max || required && words.Length == 0 || words.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 500) || words.Distinct().Count() != words.Length) Bad("Invalid bounded vocabulary/goals.");
    }
    public static void Memory(LessonSections source, LessonSections target, LocaleLessonRules locale)
    {
        Sections(source); Sections(target);
        if (source.SourceAnchor != target.SourceAnchor || !source.Names.SequenceEqual(target.Names) || !source.Concepts.SequenceEqual(target.Concepts) ||
            !source.LearningGoals.SequenceEqual(target.LearningGoals)) Bad("Memory must preserve anchor, names, concept keys and learning-goal identifiers.");
        var translated = string.Join("\n", new[] { target.Situation, target.Interpretation, target.Example, target.PracticalAction, target.Reflection });
        foreach (var name in source.Names) if (!(translated + target.SourceAnchor).Contains(name, StringComparison.Ordinal)) Bad("Translation must preserve names exactly.");
        foreach (var term in locale.Glossary.Where(x => source.Concepts.Contains(x.Key))) if (!translated.Contains(term.Value, StringComparison.Ordinal)) Bad("Translation misses configured terminology.");
        if (locale.Locale != locale.SourceLocale && translated == string.Join("\n", new[] { source.Situation, source.Interpretation, source.Example, source.PracticalAction, source.Reflection })) Bad("An unchanged source is not a supported translation.");
    }
    public static void Sections(LessonSections s)
    {
        if (s == null) Bad("Lesson sections required.");
        foreach (var x in new[] { s!.Situation, s.SourceAnchor, s.Interpretation, s.Example, s.PracticalAction, s.Reflection }) CoreRules.Text(x, 4000, "Lesson section");
        Words(s.LearningGoals, 10, true); Words(s.Names, 20); Words(s.Concepts, 20);
    }
    public static void Gate(LessonSections s, AudienceLessonRules a, LocaleLessonRules l)
    {
        Sections(s);
        var text = string.Join("\n", new[] { s.Situation, s.Interpretation, s.Example, s.PracticalAction, s.Reflection });
        if (text.Length > a.MaxCharacters || a.SensitiveTopics.Concat(l.ForbiddenPhrases).Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase)))
            Bad("Audience/locale content policy rejected this example.");
        if (!a.LearningGoals.All(s.LearningGoals.Contains) || !l.RequiredConcepts.All(s.Concepts.Contains)) Bad("Learning goals or key concept identifiers were lost.");
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Bad(string message) => throw new RuntimeValidationException(message, "LessonPolicyRejected");
}
