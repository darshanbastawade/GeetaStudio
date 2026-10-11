using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class RegistryRules
{
    private static readonly string[] ForbiddenTreatments = ["DisabilityRidicule", "ReligiousDemeaning", "RealSuffering", "FictionAsHistory"];
    public static void Bible(UniverseBibleRules r)
    {
        CoreRules.Text(r.Title, 200, "Bible title"); CoreRules.Text(r.Setting, 2000, "Setting");
        LessonRules.Words(r.ContinuityRules, 30, true); LessonRules.Words(r.HumorStyles, 10, true);
        LessonRules.Words(r.DisabledSubjectTags, 30); LessonRules.Words(r.ForbiddenPhrases, 30); LessonRules.Words(r.ForbiddenAttributions, 30, true);
        CoreRules.Text(r.SatireLabel, 500, "Satire label");
        if (r.Audiences == null || r.Audiences.Length > 50 || r.Audiences.Select(x => x.AudienceId).Distinct().Count() != r.Audiences.Length ||
            r.PreferredMinSpeakers < 1 || r.PreferredMaxSpeakers < r.PreferredMinSpeakers || r.PreferredMaxSpeakers > 20) Bad("Invalid editorial cast/audience guidelines.");
        foreach (var a in r.Audiences) CoreRules.Text(a.Tone, 200, "Audience tone");
    }
    public static void Character(RegistryCharacterRules r, string scope)
    {
        if (scope is not ("Core" or "Recurring" or "Guest" or "EpisodeOnly") ||
            r.FictionalStatus is not ("Fictional" or "RealPerson" or "Historical")) Bad("Unsupported identity or persistence scope.");
        if (scope == "EpisodeOnly" && r.EpisodeId == null) Bad("Episode-only identity must be scoped to a registered episode.");
        foreach (var x in new[] { r.Name, r.LifeStage, r.SpeechStyle, r.ArcState, r.PronunciationGuide, r.VisualAnchor, r.VoiceAnchor }) CoreRules.Text(x, 200, "Character guide");
        CoreRules.Text(r.Biography, 2000, "Biography");
        foreach (var a in new[] { r.Traits, r.Goals, r.Flaws, r.Roles, r.SituationTags, r.SkillTags, r.LearningObjectives, r.Locales, r.ContinuityTags, r.Aliases }) LessonRules.Words(a, 30);
        if (r.Traits.Length == 0 || r.Roles.Length == 0 || r.AudienceIds == null || r.AudienceIds.Length is < 1 or > 50 || r.Locales.Length == 0 ||
            r.AudienceIds.Distinct().Count() != r.AudienceIds.Length || r.IdentityEvidence?.Length > 20) Bad("Bounded traits, roles, locales and audience eligibility required.");
        if (r.StoryDevice is not ("OrdinaryFiction" or "FictionalFlawedReasoning")) Bad("Unsupported fictional storytelling device.");
        if (r.FictionalStatus != "Fictional" && r.StoryDevice != "OrdinaryFiction") Bad("Real/historical identity is not a fictional temptation device.");
    }
    public static void Brief(CastingBrief r)
    {
        foreach (var a in new[] { r.SituationTags, r.SkillTags, r.LearningObjectives, r.Roles, r.ContinuityTags, r.SubjectTags }) LessonRules.Words(a, 20);
        if (r.Roles.Length == 0 || r.LearningObjectives.Length == 0 || r.Budget.MaxSpeakingCharacters is < 1 or > 20 ||
            r.Roles.Length > r.Budget.MaxSpeakingCharacters || r.Budget.MaxEstimatedCost < 0 || r.Budget.MaxEstimatedCost > 100000 ||
            r.Mode is not ("Draft" or "Production") || r.PinnedChoices == null || r.PinnedChoices.Length > r.Roles.Length ||
            r.PinnedChoices.Select(x => x.CharacterId).Distinct().Count() != r.PinnedChoices.Length ||
            r.PinnedChoices.Select(x => x.Role).Distinct().Count() != r.PinnedChoices.Length) Bad("Invalid bounded casting brief.");
        CoreRules.Text(r.Locale, 30, "Locale"); CoreRules.Text(r.Budget.Currency, 10, "Currency");
        if (r.Mode == "Production" && !r.Budget.RequireMedia) Bad("Production requires qualified media.");
    }
    public static bool Story(RegistryEpisodeSnapshot r)
    {
        var bible = r.Bible.Rules; Bible(bible);
        if (r.Beats == null || r.Beats.Length is < 1 or > 60 || r.Relationships.Length > 30) Bad("Bounded reviewed story beats required.");
        var audience = bible.Audiences.SingleOrDefault(x => x.AudienceId == r.Brief.AudienceId);
        var enabled = bible.HumorEnabled && audience?.Enabled != false && !r.Brief.SubjectTags.Any(bible.DisabledSubjectTags.Contains);
        if (r.HumorStyle != "None" && (!enabled || !bible.HumorStyles.Contains(r.HumorStyle))) Bad("Humor disabled or unsupported for this universe/audience/subject.");
        if (r.HumorStyle == "Satire" && audience?.SatireEnabled == false) Bad("Satire disabled for this audience.");
        foreach (var b in r.Beats)
        {
            if (b.Label is not ("FictionalDialogue" or "FictionalNarration") || b.ClaimsCanonicalAuthority) Bad("Generated dialogue is fiction; canonical source text must come through verified evidence.");
            CoreRules.Text(b.Text, 2000, "Story beat"); LessonRules.Words(b.TraitClaims, 20); LessonRules.Words(b.HumorTreatments, 20);
            var character = r.Cast.SingleOrDefault(c => c.Character.CharacterId == b.SpeakerCharacterId) ?? throw new RuntimeValidationException("Beat speaker must be a registered selected character.");
            if (!b.TraitClaims.All(character.Character.Profile.Rules.Traits.Contains)) Bad("Story invents unsupported permanent character traits.");
            if (b.HumorTreatments.Any(x => ForbiddenTreatments.Contains(x, StringComparer.OrdinalIgnoreCase)) ||
                bible.ForbiddenPhrases.Concat(bible.ForbiddenAttributions).Any(p => b.Text.Contains(p, StringComparison.OrdinalIgnoreCase))) Bad("Forbidden humor or invented canonical attribution.");
            if (r.HumorStyle == "None" && b.HumorTreatments.Length != 0) Bad("Humor treatments are disabled.");
        }
        foreach (var lead in r.Cast.Where(c => c.Role == "Lead"))
            if (!r.Beats.Any(b => b.SpeakerCharacterId == lead.Character.CharacterId && b.MakesChoice)) Bad("Every protagonist must exercise agency.");
        return r.ReferencesRealPeople || r.ReferencesCurrentEvents || r.HumorStyle == "Satire" || r.Cast.Any(c => c.Character.Profile.Rules.FictionalStatus != "Fictional");
    }
    public static void ReviewEvidence(string value) { CoreRules.Text(value, 1000, "Review evidence"); if (!value.StartsWith("manual:") && !value.StartsWith("fixture:")) Bad("Explicit human or labeled fixture review required."); }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Bad(string reason) => throw new RuntimeValidationException(reason, "RegistryPolicyRejected");
}
