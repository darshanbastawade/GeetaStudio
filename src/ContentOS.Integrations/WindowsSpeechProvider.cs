using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

public sealed class WindowsSpeechProvider(string spoolRoot, IWindowsSpeechEngine? engine = null) : ISpeechSynthesisProvider
{
    private readonly IWindowsSpeechEngine speech = engine ?? new SapiSpeechEngine();
    public string Key => "media.speech.windows-sapi.v1";
    public SpeechVoiceResource[] VoiceResources => speech.Voices();
    public MediaProviderCapabilities Capabilities => new("Speech", VoiceResources.SelectMany(x => x.Locales).Distinct().Order().ToArray(), false, true, false, true, true, "LocalWindowsPreview", ["ExactInstalledEnglishVoiceLocaleOnly", "ExplicitProviderBoundVoiceToken", "SpeakAsSubstitution", "HumanPronunciationAndRedistributionReviewPending", "NoNetwork", "ReceiptBoundSpeechSegmentsMax160Characters"]);
    public static string[] AvailableEnglishVoices() => new SapiSpeechEngine().Voices().Where(x => x.Locales.Contains("en-US")).Select(x => x.Key).ToArray();
    public void ValidateProfile(RegisterMediaProfile profile)
    {
        if (profile.AdapterKey != Key || profile.Kind != "Speech" || !VoiceResources.Any(x => x.Key == profile.ResourceKey && x.Locales.Contains(profile.Locale))) throw new EvidenceBlockedException("ExactInstalledSpeechVoiceAndLocaleRequired; no voice or language fallback.");
    }
    private string PathFor(MediaProviderRequest r)
    { var root = Path.GetFullPath(spoolRoot); Directory.CreateDirectory(root); var binding = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(r.AccountReference + "|" + r.ResourceKey)))[..16]; return Path.Combine(root, binding + "-" + r.OperationId.ToString("N") + ".wav"); }
    private static string Resource(MediaProviderRequest r) => "local-sapi:" + r.OperationId.ToString("N");
    private static string Fingerprint(MediaProviderRequest r) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Version = "sapi-receipt.v2", Request = r }))));
    private sealed record Receipt(string Fingerprint, string Sha256, long Length);
    private static void Input(MediaProviderRequest r)
    {
        if (r.OperationId == Guid.Empty || r.PromptOrText.Length is < 1 or > WatchNarration.SegmentLimit || r.ReferenceImages.Length != 0 || r.Pronunciation.Length > 20 || r.MaxBytes is < 1 or > 1048576 || r.Ceiling != 0) throw new RuntimeValidationException("BoundedLocalSpeechSegmentRequired");
        foreach (var p in r.Pronunciation) { CoreRules.Text(p.Word, 100, "Pronunciation word"); CoreRules.Text(p.SpeakAs, 100, "Pronunciation substitution"); }
    }
    private static void Binding(string path, string fingerprint)
    { if (File.Exists(path + ".request") && File.ReadAllText(path + ".request") != fingerprint) throw new RuntimeConflictException("OriginalSpeechOperationRequestChanged"); }
    private static async Task<MediaProviderResult> Accepted(MediaProviderRequest r, string path, CancellationToken ct)
    {
        var receipt = JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(path + ".receipt", ct)) ?? throw new RuntimeConflictException("SpeechReceiptUnavailable");
        if (receipt.Fingerprint != Fingerprint(r)) throw new RuntimeConflictException("OriginalSpeechOperationRequestChanged");
        var info = new FileInfo(path); if (!info.Exists || info.Length != receipt.Length || info.Length > r.MaxBytes) throw new RuntimeConflictException("OriginalSpeechBytesChanged");
        var bytes = await File.ReadAllBytesAsync(path, ct); MediaFiles.Inspect(bytes); if (MediaRules.Hash(bytes) != receipt.Sha256) throw new RuntimeConflictException("OriginalSpeechBytesChanged");
        return new("Completed", Resource(r), bytes, 0, "InstalledVoiceLocalPreviewNotHumanQualified");
    }
    public async Task<MediaProviderResult> SubmitAsync(MediaProviderRequest r, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Input(r); var path = PathFor(r); FileStream held;
        try { held = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return new("Unknown", Resource(r), null, null, "OriginalSpeechOperationInProgress"); }
        using (held)
        {
            var fingerprint = Fingerprint(r); Binding(path, fingerprint);
            if (File.Exists(path + ".receipt")) return await Accepted(r, path, ct);
            if (File.Exists(path + ".request") || File.Exists(path)) return new("Unknown", Resource(r), null, null, "OriginalSpeechReceiptRequiresReconciliationOrReview");
            if (!VoiceResources.Any(x => x.Key == r.ResourceKey && x.Locales.Contains(r.Locale))) return new("Unsupported", null, null, 0, "ExactInstalledSpeechVoiceAndLocaleRequired");
            await File.WriteAllTextAsync(path + ".request", fingerprint, Encoding.UTF8, ct);
            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; var receiptTmp = tmp + ".receipt";
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(30));
                await speech.SynthesizeAsync(r, tmp, deadline.Token);
                var info = new FileInfo(tmp); if (!info.Exists || info.Length > r.MaxBytes) throw new RuntimeConflictException("SpeechOutputExceededByteLimit");
                var bytes = await File.ReadAllBytesAsync(tmp, ct); MediaFiles.Inspect(bytes);
                var receipt = new Receipt(fingerprint, MediaRules.Hash(bytes), bytes.Length);
                await File.WriteAllTextAsync(receiptTmp, JsonSerializer.Serialize(receipt), Encoding.UTF8, ct);
                File.Move(tmp, path, false); File.Move(receiptTmp, path + ".receipt", false);
                return await Accepted(r, path, ct);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); if (File.Exists(receiptTmp)) File.Delete(receiptTmp); }
        }
    }
    public async Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest r, string? resource, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Input(r); if (resource != null && resource != Resource(r)) throw new RuntimeConflictException("OriginalSpeechResourceRequired");
        var path = PathFor(r); Binding(path, Fingerprint(r));
        return File.Exists(path + ".receipt") ? await Accepted(r, path, ct) : new("Unknown", resource, null, null, "OriginalSpeechOutputNotAccepted; no resubmission.");
    }
}
