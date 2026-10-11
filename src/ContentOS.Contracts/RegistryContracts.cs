namespace ContentOS.Contracts;

public sealed record RegistryReview(Guid OperationId, string ExpectedRowVersion, bool Approve, string EvidenceReference, string? EscalationEvidence = null);
public sealed record RegistryRevisionView<T>(Guid Id, string Key, int Version, T Rules, string ReviewStatus, string? ReviewEvidence, string RowVersion);
public sealed record AudienceHumorRule(Guid AudienceId, bool Enabled, bool SatireEnabled, string Tone);
public sealed record UniverseBibleRules(string Title, string Setting, string[] ContinuityRules, string[] HumorStyles,
    bool HumorEnabled, AudienceHumorRule[] Audiences, string[] DisabledSubjectTags, string[] ForbiddenPhrases,
    string[] ForbiddenAttributions, string SatireLabel, int PreferredMinSpeakers = 2, int PreferredMaxSpeakers = 4);
public sealed record RegisterBible(Guid OperationId, string Key, int Version, UniverseBibleRules Rules);
public sealed record RegistryCharacterRules(string Name, string LifeStage, string Biography, string FictionalStatus,
    string[] Traits, string[] Goals, string[] Flaws, string SpeechStyle, string[] Roles, string[] SituationTags,
    string[] SkillTags, string[] LearningObjectives, Guid[] AudienceIds, string[] Locales, string[] ContinuityTags,
    string ArcState, string PronunciationGuide, string VisualAnchor, string VoiceAnchor, string[] Aliases,
    Guid? EpisodeId = null, Guid[]? IdentityEvidence = null, string? IdentityPermissionReference = null,
    string StoryDevice = "OrdinaryFiction");
public sealed record RegisterRegistryCharacter(Guid OperationId, string Key, string PersistenceScope, RegistryCharacterRules Rules,
    Guid? CharacterId = null, string? ExpectedRowVersion = null, Guid? DistinctFromCharacterId = null);
public sealed record RegistryCharacterView(Guid CharacterId, Guid CoreRevisionId, string Key, string PersistenceScope, string Status,
    string RowVersion, RegistryRevisionView<RegistryCharacterRules> Profile, Guid? DistinctFromCharacterId);
public sealed record CharacterLifecycle(Guid OperationId, string ExpectedRowVersion, string Action, string EvidenceReference);
public sealed record RegistryPresentationRules(Guid ProfileRevisionId, Guid AudienceId, string Locale, string Style,
    string VisualAnchor, string VoiceAnchor, string PronunciationGuide, Guid? VisualAssetReferenceId, Guid? VoiceAssetReferenceId,
    decimal EstimatedCost, string Currency);
public sealed record RegisterRegistryPresentation(Guid OperationId, string Key, int Version, RegistryPresentationRules Rules);
public sealed record RegistryRelationshipRules(Guid FromCharacterId, Guid ToCharacterId, string Kind, string Continuity,
    string ArcState);
public sealed record RegisterRegistryRelationship(Guid OperationId, string Key, int Version, RegistryRelationshipRules Rules);
public sealed record RegisterRegistryEpisode(Guid OperationId, string Key);
public sealed record RegistryEpisodeView(Guid Id, string Key, int CurrentVersion, string RowVersion);
public sealed record CastBudget(int MaxSpeakingCharacters, decimal MaxEstimatedCost, string Currency, bool RequireMedia = false);
public sealed record PinnedCastChoice(Guid CharacterId, string Role, Guid? PresentationRevisionId = null);
public sealed record CastingBrief(Guid BibleRevisionId, Guid EpisodeId, Guid AudienceId, string Locale, string[] SituationTags,
    string[] SkillTags, string[] LearningObjectives, string[] Roles, string[] ContinuityTags, string[] SubjectTags,
    CastBudget Budget, PinnedCastChoice[] PinnedChoices, string Mode = "Draft");
public sealed record RegistryCastCandidate(RegistryCharacterView Character, string Role, bool Eligible, string[] Reasons,
    RegistryRevisionView<RegistryPresentationRules>? Presentation, decimal EstimatedCost);
public sealed record RegistryCastPlan(RegistryCastCandidate[] Candidates, RegistryCastCandidate[] Selected,
    RegistryCharacterRules[] GuestProposals, string[] MissingRoles);
public sealed record RegistryStoryBeat(Guid SpeakerCharacterId, string Label, string Text, string[] TraitClaims,
    string[] HumorTreatments, bool MakesChoice = false, bool ClaimsCanonicalAuthority = false);
public sealed record ComposeRegistryEpisode(Guid OperationId, string ExpectedRowVersion, CastingBrief Brief, string HumorStyle,
    RegistryStoryBeat[] Beats, Guid[] RelationshipRevisionIds, bool ReferencesRealPeople = false, bool ReferencesCurrentEvents = false,
    Guid? LessonVariantId = null, Guid? ContentRevisionId = null);
public sealed record RegistryEpisodeSnapshot(CastingBrief Brief, string HumorStyle,
    RegistryRevisionView<UniverseBibleRules> Bible, RegistryCastCandidate[] Cast,
    RegistryRevisionView<RegistryRelationshipRules>[] Relationships, RegistryStoryBeat[] Beats,
    bool ReferencesRealPeople, bool ReferencesCurrentEvents, Guid? LessonVariantId, Guid? ContentRevisionId,
    bool EditorialEscalationRequired, string FictionLabel, string SatireLabel);
public sealed record RegistrySeedCharacter(string Key, string PersistenceScope, string[] Traits, string[] Goals, string[] Flaws,
    string SpeechStyle, string[] Roles, string[] SituationTags, string[] SkillTags, string[] Objectives,
    string[] AudienceKeys, string[] Locales, string[] ContinuityTags, string ArcState, string PronunciationGuide,
    string VisualAnchor, string VoiceAnchor, string StoryDevice = "OrdinaryFiction", string[]? PresentationStyles = null);
public sealed record RegistrySeedDocument(string UniverseKey, UniverseBibleRules Bible, RegistrySeedCharacter[] Characters);
