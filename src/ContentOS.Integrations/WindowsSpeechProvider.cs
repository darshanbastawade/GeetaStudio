using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

public sealed class WindowsSpeechProvider(string spoolRoot) : ISpeechSynthesisProvider
{
    public string Key => "media.speech.windows-sapi.v1";
    public MediaProviderCapabilities Capabilities => new("Speech", ["en-US"], false, true, false, true, true, "LocalWindowsPreview", ["InstalledEnglishVoiceOnly", "ExplicitProviderBoundVoiceToken", "SpeakAsSubstitution", "HumanPronunciationAndRedistributionReviewPending", "NoNetwork"]);
    public static string[] AvailableEnglishVoices()
    {
        if (!OperatingSystem.IsWindows()) return [];
        object? instance = null; try { instance = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice")!); dynamic voice = instance!; dynamic tokens = voice.GetVoices("Language=409", ""); var values = new List<string>(); for (var i = 0; i < (int)tokens.Count; i++) values.Add((string)tokens.Item(i).Id); return values.ToArray(); } finally { if (instance != null && Marshal.IsComObject(instance)) Marshal.FinalReleaseComObject(instance); }
    }
    private string PathFor(MediaProviderRequest r)
    {
        var root = Path.GetFullPath(spoolRoot);
        Directory.CreateDirectory(root); var binding = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(r.AccountReference + "|" + r.ResourceKey)))[..16]; return Path.Combine(root, binding + "-" + r.OperationId.ToString("N") + ".wav");
    }
    public async Task<MediaProviderResult> SubmitAsync(MediaProviderRequest r, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || r.Locale != "en-US" || r.PromptOrText.Length > 160 || r.ReferenceImages.Length != 0 || !AvailableEnglishVoices().Contains(r.ResourceKey)) return new("Unsupported", null, null, 0, "ExactInstalledEnglishVoiceAndBoundedTextRequired");
        var path = PathFor(r); if (File.Exists(path)) return await ReconcileAsync(r, "local-sapi:" + r.OperationId.ToString("N"), ct);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            if (!OperatingSystem.IsWindows()) { completion.SetException(new PlatformNotSupportedException()); return; }
            object? instance = null, fileInstance = null; var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                instance = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice")!); fileInstance = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpFileStream")!); dynamic voice = instance!; dynamic stream = fileInstance!; dynamic tokens = voice.GetVoices("Language=409", "");
                for (var i = 0; i < (int)tokens.Count; i++) { dynamic token = tokens.Item(i); if ((string)token.Id == r.ResourceKey) { voice.Voice = token; break; } }
                stream.Format.Type = 18; stream.Open(tmp, 3, false); voice.AudioOutputStream = stream; var text = r.PromptOrText;
                foreach (var p in r.Pronunciation) text = Regex.Replace(text, "\\b" + Regex.Escape(p.Word) + "\\b", _ => p.SpeakAs, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
                voice.Speak(text, 16); stream.Close(); var bytes = File.ReadAllBytes(tmp); MediaFiles.Inspect(bytes); File.Move(tmp, path, false); completion.SetResult();
            }
            catch (Exception e) { completion.SetException(e); }
            finally { if (File.Exists(tmp)) File.Delete(tmp); if (fileInstance != null && Marshal.IsComObject(fileInstance)) Marshal.FinalReleaseComObject(fileInstance); if (instance != null && Marshal.IsComObject(instance)) Marshal.FinalReleaseComObject(instance); }
        })
        { IsBackground = true }; thread.SetApartmentState(ApartmentState.STA); thread.Start(); await completion.Task.WaitAsync(ct); return await ReconcileAsync(r, "local-sapi:" + r.OperationId.ToString("N"), ct);
    }
    public async Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest r, string? resource, CancellationToken ct)
    { var path = PathFor(r); if (!File.Exists(path)) return new("Unknown", resource, null, null, "OriginalLocalVoiceOutputNotYetAvailable"); var bytes = await File.ReadAllBytesAsync(path, ct); MediaFiles.Inspect(bytes); return new("Completed", "local-sapi:" + r.OperationId.ToString("N"), bytes, 0, "InstalledVoiceLocalPreviewNotHumanQualified"); }
}
