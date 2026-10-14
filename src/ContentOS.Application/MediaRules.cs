using System.Security.Cryptography;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class MediaRules
{
    public static readonly string[] Kinds = ["CharacterSheet", "Pose", "Scene", "Voice", "Music", "Other"];
    public static void Attest(string mode, string value) { if (mode is not ("Fixture" or "Manual") || value == null || !value.StartsWith(mode == "Fixture" ? "fixture:" : "manual:", StringComparison.OrdinalIgnoreCase)) throw new RuntimeValidationException("Explicit fixture/manual media review required."); CoreRules.Text(value, 1000, "Media evidence"); }
    public static void License(MediaLicense l)
    {
        if (l.Kind is not ("OwnedOriginal" or "Licensed" or "PublicDomain" or "Fixture") || l.SourceSnapshotIds == null || l.SourceSnapshotIds.Length > 20) throw new RuntimeValidationException("Bounded explicit media license required.");
        Attest(l.Kind == "Fixture" ? "Fixture" : "Manual", l.EvidenceReference);
        if (l.Kind is "Licensed" or "PublicDomain" && l.SourceSnapshotIds.Length == 0) throw new RuntimeValidationException("Licensed/public-domain media needs actual M3 sources.");
        CoreRules.Text(l.Attribution, 2000, "Media attribution");
    }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static void Input(ImportMedia r)
    {
        CoreRules.Text(r.Key, 100, "Asset key"); CoreRules.Text(r.FileName, 100, "Display filename"); CoreRules.Text(r.AccessibilityDescription, 2000, "Accessibility description"); CoreRules.Text(r.Locale, 20, "Locale");
        if (r.FileName != Path.GetFileName(r.FileName) || r.FileName.IndexOfAny(['/', '\\', ':']) >= 0 || r.Version < 1 || !Kinds.Contains(r.Kind) || r.Bytes == null || r.Bytes.Length is < 1 or > 1048576 || Hash(r.Bytes) != r.ExpectedSha256 || (r.ParentVersionIds?.Length ?? 0) > 20) throw new RuntimeValidationException("Safe bounded media file, exact checksum and explicit version required.");
        License(r.License);
    }
    public static void Capability(MediaProviderCapabilities c, GenerateMedia r)
    {
        if (!c.Locales.Contains(r.Output.Locale) || r.ReferenceVersionIds.Length > 1 || r.ReferenceVersionIds.Length > 0 && !c.ReferenceImages || r.Pronunciation.Length > 0 && !c.PronunciationSubstitution)
            throw new EvidenceBlockedException("UnsupportedMediaRequirement: locale, character reference or pronunciation cannot be discarded.");
        if (r.Output.License.LikenessConsentReference != null || r.Output.License.VoiceConsentReference != null) { if (!c.Cloning) throw new EvidenceBlockedException("CloningUnsupportedByMediaProvider"); }
        if (r.DeadlineSeconds is < 10 or > 300 || r.MaxCost < 0 || r.MaxCost > 1000 || r.Pronunciation.Length > 20) throw new RuntimeValidationException("Bounded media cost/deadline/pronunciation required.");
        CoreRules.Text(r.PromptOrText, 1000, "Media prompt/text");
        foreach (var p in r.Pronunciation) { CoreRules.Text(p.Word, 100, "Pronunciation word"); CoreRules.Text(p.SpeakAs, 100, "Spoken substitution"); }
    }
}
