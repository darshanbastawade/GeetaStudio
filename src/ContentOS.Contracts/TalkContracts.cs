namespace ContentOS.Contracts;

public sealed record TalkPolicyRequest(Guid OperationId, Guid UniverseId, string Key, int Version,
    Guid PersonaRevisionId, Guid AudienceProfileId, Guid LocaleProfileId, string AgeBand,
    string Alias, string EmbeddingSpaceId, int MaxTurns = 12, int LifetimeMinutes = 30,
    long CreditsPerTurn = 1, int MemoryHours = 24, string Evidence = "fixture:M21-private-launch");
public sealed record TalkPolicyView(Guid Id, TalkPolicyRequest Settings, bool Enabled, string Qualification);
public sealed record TalkOpen(Guid OperationId, Guid PolicyId, Guid ProfileId, bool Remember = false);
public sealed record TalkAsk(Guid OperationId, string Question);
public sealed record TalkFeedback(Guid OperationId, string Category);
public sealed record TalkSource(Guid SnapshotId, Guid CorpusTextId, int Chapter, int Verse, string Label,
    string ExactText, string Locator, CitationExport Citation);
public sealed record TalkTurnView(Guid Id, Guid OperationId, string State, string Outcome, string? Question,
    string? Answer, TalkSource[] Sources, Guid? AiRunId, Guid? RouteId, Guid? ReservationId,
    long Credits, decimal ProviderCost, string? Currency, string Reflection, bool MemoryRetained);
public sealed record TalkSessionView(Guid Id, Guid ProfileId, Guid PolicyId, string Character,
    string Disclosure, string AgeBand, string Locale, DateTimeOffset ExpiresUtc, DateTimeOffset RetainUntilUtc,
    bool Remember, string State, int MaxTurns, long CreditsPerTurn, TalkTurnView[] Turns);
public sealed record TalkAiAdmission(Guid SessionId, Guid TurnId, Guid LeaseToken);
public sealed record TalkProblemView(Guid Id, Guid TurnId, string Category, string EvidenceJson, DateTimeOffset CreatedUtc);
