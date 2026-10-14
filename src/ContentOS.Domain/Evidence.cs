namespace ContentOS.Domain;

public sealed class EvidenceRubric : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public string Description { get; set; } = "";
    public int MinimumIndependentSources { get; set; } = 2;
}
public sealed class EvidenceClaim : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public int CurrentVersion { get; set; }
}
public sealed class ClaimRevision : CoreRecord
{
    public Guid ClaimId { get; set; }
    public int Version { get; set; }
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string Locale { get; set; } = "";
    public string Statement { get; set; } = "";
    public string Author { get; set; } = "";
    public string Tradition { get; set; } = "";
    public string Uncertainty { get; set; } = "";
    public Guid RubricId { get; set; }
    public string ReviewStatus { get; set; } = "Pending";
    public string Quality { get; set; } = "Insufficient";
    public string Outcome { get; set; } = "InsufficientEvidence";
    public string? Reviewer { get; set; }
    public string? ReviewEvidence { get; set; }
}
public sealed class ClaimEvidenceSpan : CoreRecord
{
    public Guid ClaimRevisionId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid? CorpusTextId { get; set; }
    public int Start { get; set; }
    public int Length { get; set; }
    public string ExactText { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Locator { get; set; } = "";
    public string Relation { get; set; } = "";
}
public sealed class ClaimDisagreement : CoreRecord
{
    public Guid LeftRevisionId { get; set; }
    public Guid RightRevisionId { get; set; }
    public string Reason { get; set; } = "";
    public string EvidenceReference { get; set; } = "";
}
public sealed class ClaimContentLink : CoreRecord
{
    public Guid ClaimRevisionId { get; set; }
    public Guid ContentRevisionId { get; set; }
    public int Start { get; set; }
    public int Length { get; set; }
}
public sealed class ClaimCorrection : CoreRecord
{
    public Guid ClaimRevisionId { get; set; }
    public string Reason { get; set; } = "";
    public string EvidenceReference { get; set; } = "";
    public string Status { get; set; } = "Open";
    public Guid? ReplacementRevisionId { get; set; }
}
public sealed class EvidenceCommand : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid ResultId { get; set; }
}
public sealed class EvidenceBlockedException(string reason) : Exception("Evidence gate blocked: " + reason)
{
    public string Reason { get; } = reason;
}
public static class EvidenceRules
{
    public static void Classification(string type, string label, string author, string tradition, string uncertainty)
    {
        var expected = type switch
        {
            "Quotation" => "Quotation",
            "SourceQuotation" => "SourceQuotation",
            "Translation" => "Translation",
            "Interpretation" => "Interpretation",
            "HistoricalClaim" => "HistoricalClaim",
            "NewsClaim" => "NewsClaim",
            "ModernAnalogy" => "ModernAnalogy",
            "FictionalScene" => "FictionalScene",
            "Suggestion" => "Suggestion",
            _ => throw new RuntimeValidationException("Unsupported claim type.")
        };
        if (label != expected || uncertainty is not ("SourceExact" or "Limited" or "Contested" or "Unverified" or "Fictional"))
            throw new RuntimeValidationException("Explicit claim label and uncertainty are required.");
        if (type is "Quotation" or "Translation" or "SourceQuotation" && uncertainty != "SourceExact")
            throw new RuntimeValidationException("Exact source readings must retain their source-exact label.");
        if (type == "Interpretation") { CoreRules.Text(author, 200, "Interpretation author"); CoreRules.Text(tradition, 200, "Interpretation tradition"); }
        if (type == "FictionalScene" && uncertainty != "Fictional") throw new RuntimeValidationException("Fiction must be labeled fictional.");
    }
    public static void Span(string text, int start, int length)
    {
        if (start < 0 || length < 1 || start > text.Length - length ||
            start > 0 && char.IsLowSurrogate(text[start]) || start + length < text.Length && char.IsLowSurrogate(text[start + length]))
            throw new RuntimeValidationException("Span must use valid UTF-16 character boundaries.");
    }
    public static bool RequiresClaims(string kind) => kind is "Canonical" or "Translation" or "Interpretation" or "CurrentEvent" or "Historical";
}
