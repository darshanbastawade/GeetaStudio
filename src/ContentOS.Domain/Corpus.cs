using System.Text;

namespace ContentOS.Domain;

public sealed class CorpusManifest : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid UniverseId { get; set; }
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public string Edition { get; set; } = "";
    public string MaterialKind { get; set; } = "Scripture";
    public int BaselineTarget { get; set; } = 700;
    public string Fingerprint { get; set; } = "";
}
public sealed class CorpusLocator
{
    public Guid ManifestId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string VariantKey { get; set; } = "";
    public int? BaselineVerse { get; set; }
    public string Note { get; set; } = "";
}
public sealed class CorpusSourceBinding
{
    public Guid ManifestId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string VariantKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Locale { get; set; } = "";
    public Guid SnapshotId { get; set; }
    public int SourceChapter { get; set; }
    public int SourceVerse { get; set; }
    public string Sha256 { get; set; } = "";
    public string SourceLocator { get; set; } = "";
}
public sealed class CorpusVerse : CoreRecord
{
    public Guid ManifestId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string VariantKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string ReviewStatus { get; set; } = "Pending";
    public string? Reviewer { get; set; }
    public string? ReviewEvidence { get; set; }
}
public sealed class CorpusText : CoreRecord
{
    public Guid VerseId { get; set; }
    public string Kind { get; set; } = "";
    public string Locale { get; set; } = "";
    public string ExactText { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string SearchText { get; set; } = "";
    public Guid SnapshotId { get; set; }
    public string SourceLocator { get; set; } = "";
}
public sealed class CorpusAnnotation : CoreRecord
{
    public Guid VerseId { get; set; }
    public string Key { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Locale { get; set; } = "";
    public string Text { get; set; } = "";
    public Guid SnapshotId { get; set; }
    public string ReviewStatus { get; set; } = "Pending";
    public string? Reviewer { get; set; }
    public string? ReviewEvidence { get; set; }
}
public sealed class CorpusCommand : CoreRecord
{
    public Guid TenantId { get; set; }
    public Guid OperationId { get; set; }
    public string Actor { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid ResultId { get; set; }
}
public static class CorpusRules
{
    public static void Locator(int chapter, int verse)
    {
        if (chapter is < 1 or > 18 || verse is < 1 or > 200) throw new RuntimeValidationException("Invalid chapter/verse locator.");
    }
    public static void Exact(string text)
    {
        CoreRules.Text(text, 16000, "Source text");
        try { _ = new UTF8Encoding(false, true).GetBytes(text); }
        catch (EncoderFallbackException) { throw new RuntimeValidationException("Invalid Unicode source text."); }
        if (text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')) || text.Contains('\uFFFD'))
            throw new RuntimeValidationException("Invalid source control/replacement characters.");
    }
    public static void QuotationLocator(string text, int verse)
    {
        var markers = System.Text.RegularExpressions.Regex.Matches(text, @"॥\s*([०-९0-9]+)\s*॥");
        if (markers.Count == 0) throw new RuntimeValidationException("Sanskrit source quotation requires its edition verse marker.");
        var digits = markers[^1].Groups[1].Value.Select(c => c is >= '०' and <= '९' ? (char)('0' + c - '०') : c).ToArray();
        if (!int.TryParse(new string(digits), out var sourceVerse) || sourceVerse != verse)
            throw new RuntimeValidationException("Sanskrit source marker does not match its declared edition locator.");
    }
    // Search normalization is a derived field; canonical source text is never overwritten.
    public static string Normalize(string text) => string.Join(" ", text.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

