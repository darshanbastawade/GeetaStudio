namespace ContentOS.Contracts;

public sealed record CorpusLocatorInput(int Chapter, int Verse, string VariantKey, int? BaselineVerse, string Note);
public sealed record CorpusSourceBindingInput(int Chapter, int Verse, string VariantKey, string Kind, string Locale,
    Guid SnapshotId, int SourceChapter, int SourceVerse, string Sha256, string SourceLocator);
public sealed record RegisterCorpus(Guid OperationId, string Key, int Version, string Edition, int BaselineTarget,
    IReadOnlyList<CorpusLocatorInput> Inventory, IReadOnlyList<CorpusSourceBindingInput>? Bindings = null);
public sealed record CorpusManifestView(Guid Id, string Key, int Version, string Edition, int BaselineTarget, int ExpectedCount);
public sealed record CorpusTextInput(string Kind, string Locale, string ExactText, string Sha256, Guid SnapshotId, string SourceLocator);
public sealed record CorpusVerseInput(int Chapter, int Verse, string VariantKey, IReadOnlyList<CorpusTextInput> Texts);
public sealed record ImportCorpus(Guid OperationId, IReadOnlyList<CorpusVerseInput> Verses);
public sealed record CorpusImportResult(Guid ManifestId, int Submitted, int Added, int Unchanged);
public sealed record CorpusReview(string ExpectedRowVersion, string Status, string EvidenceReference);
public sealed record CorpusTextView(string Kind, string Locale, string ExactText, string Sha256, string SourceLocator, string Availability, CitationExport? Citation, Guid? Id = null);
public sealed record CorpusAnnotationInput(Guid OperationId, string Key, string Kind, string Locale, string Text, Guid SnapshotId);
public sealed record CorpusAnnotationView(Guid Id, string Key, string Kind, string Locale, string Text, string ReviewStatus, string RowVersion, CitationExport? Citation);
public sealed record CorpusVerseView(Guid Id, int Chapter, int Verse, string VariantKey, string ReviewStatus, string RowVersion,
    IReadOnlyList<CorpusTextView> Texts, IReadOnlyList<CorpusAnnotationView> Annotations);
public sealed record CorpusEntryReport(int Chapter, int Verse, string VariantKey, int? BaselineVerse, string Note, string State,
    IReadOnlyList<string> MissingComponents, IReadOnlyList<string> RestrictedComponents);
public sealed record CorpusReport(CorpusManifestView Manifest, int Imported, int ApprovedReadable, int Missing, int Restricted, IReadOnlyList<CorpusEntryReport> Entries);
