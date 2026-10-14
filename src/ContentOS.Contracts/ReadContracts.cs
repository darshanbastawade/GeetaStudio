namespace ContentOS.Contracts;

public sealed record RegisterReadCollection(Guid OperationId, string Key, int Version, string Title, Guid? ManifestId, string Mode, string Rationale, int? ExpectedPieces = null);
public sealed record ReadHistoricalMetadata(string Classification, int StartYear, int EndYear, string Dating, string[] People, string[] Places, Guid? DisagreementId = null);
public sealed record AddReadPiece(Guid OperationId, Guid CollectionId, Guid? CorpusVerseId, Guid EditorialItemId, Guid LessonVariantId, string Journey, int Position, ReadHistoricalMetadata? History = null);
public sealed record ReadGateEvidence(Guid OperationId, string Kind, string Mode, string Reference, string Notes, DateTimeOffset ExpiresUtc);
public sealed record ControlReadCollection(Guid OperationId, string ExpectedRowVersion, string Action);
public sealed record ReadCollectionSummary(Guid Id, string Key, int Version, string Title, int Pieces, string State, string Universe = "");
public sealed record ReadSource(string Kind, string Locale, string Text, string Locator, string Attribution);
public sealed record ReadPieceView(Guid Id, int Chapter, int Verse, string Locale, Guid AudienceId, string Audience, string Title,
    string Explanation, string PracticalAction, string Reflection, ReadSource[] Sources, string[] Concepts, string Journey, int Position, ReadHistoricalMetadata? History = null);
public sealed record ReadBook(ReadCollectionSummary Collection, ReadPieceView[] Pieces, int ExpectedVerses, int ImportedVerses,
    int AvailableVerses, string Mode, string[] Blocks, string Universe, string MaterialKind = "Scripture");
public sealed record ReadStaffView(ReadBook Book, string RowVersion, string Fingerprint, string[] ReleaseBlocks, string[] EvidenceKinds);
public sealed record ReadEngagement(int Version, Guid PieceId, string Name, bool Consent);
