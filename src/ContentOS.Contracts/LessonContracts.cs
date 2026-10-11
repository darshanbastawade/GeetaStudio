namespace ContentOS.Contracts;

public sealed record ChildContentPolicy(bool MinimalData = true, bool PersistentConversationProfile = false,
    bool BehavioralAdvertising = false, bool PersonalizationEnabled = false, int MaxInteractions = 1);
public sealed record AudienceLessonRules(Guid AudienceId, int MinAge, int MaxAge, string ReadingLevel, string[] Vocabulary,
    int MaxCharacters, string Tone, string[] SensitiveTopics, string[] LearningGoals, string Situation, string Example,
    string PracticalAction, string Reflection, ChildContentPolicy ChildPolicy);
public sealed record WritingLessonRules(Guid AudienceProfileId, int MinAge, int MaxAge, Guid[] EligibleCharacters,
    string[] EligibleLifeStages, string[] LeadRoles, string[] SupportingRoles, string Setting, string ConflictComplexity,
    string Humor, int MaxCast = 3);
public sealed record LessonTerm(string Key, string Value, string Transliteration, string Pronunciation);
public sealed record LocaleLessonRules(string Locale, string SourceLocale, LessonTerm[] Glossary, string TransliterationRules,
    string PronunciationRules, string[] RequiredConcepts, string[] ForbiddenPhrases);
public sealed record TranslationRouteRules(string Adapter, int MaxCharacters = 12000, int DeadlineSeconds = 10);
public sealed record RegisterLessonProfile(Guid OperationId, string Key, int Version, string Kind,
    AudienceLessonRules? Audience = null, WritingLessonRules? Writing = null, LocaleLessonRules? Locale = null,
    TranslationRouteRules? Route = null);
public sealed record LessonProfileView(Guid Id, RegisterLessonProfile Rules, string ReviewStatus, string? ReviewEvidence, string RowVersion);
public sealed record LessonReview(Guid OperationId, string ExpectedRowVersion, bool Approve, string EvidenceReference);
public sealed record LessonSections(string Situation, string SourceAnchor, string Interpretation, string Example, string PracticalAction,
    string Reflection, string[] LearningGoals, string[] Names, string[] Concepts);
public sealed record RegisterLessonBundle(Guid OperationId, Guid AnchorClaimRevisionId, Guid InterpretationClaimRevisionId,
    Guid? SanskritTextId, string[] LearningGoals, string[] Names, string[] Concepts);
public sealed record LessonBundleSnapshot(RegisterLessonBundle Request, string Anchor, string Locator, string Interpretation,
    string InterpretationAuthor, string Tradition, string Uncertainty, string? Sanskrit, Guid[] SourceIds, CitationExport[] Citations);
public sealed record LessonBundleView(Guid Id, LessonBundleSnapshot Snapshot);
public sealed record RegisterLessonMemory(Guid OperationId, Guid LocaleProfileId, Guid BundleId, LessonSections Source, LessonSections Target,
    string MeaningReviewNotes);
public sealed record LessonMemoryView(Guid Id, RegisterLessonMemory Entry, string ReviewStatus, string? ReviewEvidence, string RowVersion);
public sealed record AdaptLesson(Guid OperationId, Guid BundleId, Guid AudienceProfileId, Guid WritingProfileId,
    Guid LocaleProfileId, Guid TranslationRouteId, Guid? ParentVariantId = null);
public sealed record LessonCastCandidate(Guid CharacterId, Guid RevisionId, string Name, string LifeStage, string Role);
public sealed record LessonVariantSnapshot(LessonBundleSnapshot Bundle, LessonProfileView Audience, LessonProfileView Writing,
    LessonProfileView Locale, LessonProfileView Route, LessonCastCandidate[] Cast, Guid? TranslationMemoryId);
public sealed record LessonVariantView(Guid Id, Guid BundleId, Guid? ParentVariantId, int Version, string ReviewStatus,
    LessonSections? Sections, LessonVariantSnapshot Snapshot, bool OutputWithheld, string RowVersion, string? ReviewEvidence);
public sealed record TranslationRequest(LessonSections Source, LocaleLessonRules Locale, TranslationRouteRules Route,
    IReadOnlyList<LessonMemoryView> ReviewedMemory);
public sealed record TranslationResult(LessonSections Sections, Guid? MemoryId, string Adapter);
public sealed record RegisterLessonCharacter(Guid OperationId, string Key, string Name, string LifeStage, string Biography,
    string EvidenceReference);
