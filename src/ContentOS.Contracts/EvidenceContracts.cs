namespace ContentOS.Contracts;

public sealed record RegisterEvidenceRubric(Guid OperationId, string Key, int Version, string Description, int MinimumIndependentSources = 2);
public sealed record EvidenceRubricView(Guid Id, string Key, int Version, string Description, int MinimumIndependentSources);
public sealed record EvidenceSpanInput(Guid SnapshotId, int Start, int Length, string ExactText, string Sha256, string Locator, string Relation, Guid? CorpusTextId = null);
public sealed record SubmitClaim(Guid OperationId, string Key, string ExpectedRowVersion, string Type, string Label, string Locale, string Statement,
    string Author, string Tradition, string Uncertainty, Guid RubricId, IReadOnlyList<EvidenceSpanInput> Evidence, Guid? CorrectionId = null);
public sealed record ReviewClaim(string ExpectedRowVersion, string Status, string Quality, string Outcome, string EvidenceReference);
public sealed record RegisterDisagreement(Guid OperationId, Guid LeftRevisionId, Guid RightRevisionId, string Reason, string EvidenceReference);
public sealed record LinkClaim(Guid OperationId, Guid ClaimRevisionId, Guid ContentRevisionId, int Start, int Length);
public sealed record RequestClaimCorrection(Guid OperationId, Guid ClaimRevisionId, string Reason, string EvidenceReference);
public sealed record ClaimEvidenceView(Guid Id, Guid SnapshotId, Guid? CorpusTextId, int Start, int Length, string ExactText, string Sha256, string Locator, string Relation);
public sealed record ClaimRevisionView(Guid Id, int Version, string Type, string Label, string Locale, string Statement, string Author, string Tradition,
    string Uncertainty, Guid RubricId, string ReviewStatus, string Quality, string Outcome, string? Reviewer, string? ReviewEvidence, string RowVersion, IReadOnlyList<ClaimEvidenceView> Evidence);
public sealed record ClaimView(Guid Id, string Key, int CurrentVersion, string RowVersion, IReadOnlyList<ClaimRevisionView> Revisions);
public sealed record DisagreementView(Guid Id, Guid LeftRevisionId, Guid RightRevisionId, string Reason, string EvidenceReference);
public sealed record ClaimCorrectionView(Guid Id, Guid ClaimRevisionId, string Reason, string EvidenceReference, string Status, Guid? ReplacementRevisionId);
public sealed record ClaimImpact(Guid ClaimRevisionId, IReadOnlyList<Guid> ContentRevisionIds, IReadOnlyList<ClaimCorrectionView> Corrections);
public sealed record ClaimLabelView(Guid ClaimRevisionId, Guid ContentRevisionId, int Start, int Length, string Label, string Author, string Tradition, string Uncertainty, int EffectiveVersion);
public sealed record EvidenceGateResult(bool Eligible, string Reason, IReadOnlyList<ClaimLabelView> Labels);
