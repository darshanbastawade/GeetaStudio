using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

// Adapter-local seam: tests use controlled bytes; production uses installed SAPI.
public interface IWindowsSpeechEngine
{
    SpeechVoiceResource[] Voices();
    Task SynthesizeAsync(MediaProviderRequest request, string destination, CancellationToken ct);
}
public sealed class SapiSpeechEngine : IWindowsSpeechEngine
{
    private static void Release(object? value) { if (OperatingSystem.IsWindows() && value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
    public SpeechVoiceResource[] Voices()
    {
        if (!OperatingSystem.IsWindows()) return [];
        object? instance = null, collection = null;
        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice"); if (type == null) return [];
            instance = Activator.CreateInstance(type); dynamic voice = instance!; collection = voice.GetVoices("", ""); dynamic tokens = collection; var result = new List<SpeechVoiceResource>();
            for (var i = 0; i < (int)tokens.Count; i++)
            {
                object tokenObject = tokens.Item(i); dynamic token = tokenObject;
                try
                {
                    var locales = new List<string>();
                    foreach (var raw in ((string)token.GetAttribute("Language")).Split(';'))
                        if (int.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lcid))
                            try { var culture = CultureInfo.GetCultureInfo(lcid); if (culture.TwoLetterISOLanguageName == "en" && !culture.IsNeutralCulture) locales.Add(culture.Name); } catch (CultureNotFoundException) { }
                    if (locales.Count > 0) result.Add(new((string)token.Id, locales.Distinct().Order().ToArray()));
                }
                finally { Release(tokenObject); }
            }
            return result.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        }
        finally { Release(collection); Release(instance); }
    }
    public Task SynthesizeAsync(MediaProviderRequest r, string destination, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            object? instance = null, file = null, collection = null, selected = null; bool opened = false; Exception? failure = null;
            try
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                ct.ThrowIfCancellationRequested(); instance = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice")!); file = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpFileStream")!);
                dynamic voice = instance!; dynamic stream = file!; collection = voice.GetVoices("", ""); dynamic tokens = collection;
                for (var i = 0; i < (int)tokens.Count; i++) { object token = tokens.Item(i); if ((string)((dynamic)token).Id == r.ResourceKey) { selected = token; break; } Release(token); }
                if (selected == null) throw new EvidenceBlockedException("PinnedSpeechVoiceUnavailable; default voice forbidden.");
                var language = (string)((dynamic)selected).GetAttribute("Language");
                if (!language.Split(';').Any(x => int.TryParse(x, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lcid) && CultureInfo.GetCultureInfo(lcid).Name == r.Locale)) throw new EvidenceBlockedException("PinnedSpeechLocaleChanged");
                voice.Voice = (dynamic)selected;
                var text = r.PromptOrText;
                foreach (var p in r.Pronunciation) text = Regex.Replace(text, "\\b" + Regex.Escape(p.Word) + "\\b", _ => p.SpeakAs, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
                if (text.Length > 1000) throw new EvidenceBlockedException("PronunciationExpansionLimitExceeded");
                stream.Format.Type = 18; stream.Open(destination, 3, false); opened = true; voice.AudioOutputStream = stream;
                voice.Speak(text, 17); // Async + IsNotXML: text cannot inject SAPI markup.
                while (!(bool)voice.WaitUntilDone(50))
                {
                    if (ct.IsCancellationRequested || new FileInfo(destination).Length > r.MaxBytes) { voice.Speak("", 3); ct.ThrowIfCancellationRequested(); throw new EvidenceBlockedException("SpeechOutputExceededByteLimit"); }
                }
                ct.ThrowIfCancellationRequested(); stream.Close(); opened = false;
            }
            catch (Exception e) { failure = e; }
            finally { if (opened && file != null) try { if (instance != null) ((dynamic)instance).Speak("", 3); ((dynamic)file).Close(); } catch (COMException) { } Release(selected); Release(collection); Release(file); Release(instance); }
            if (failure is OperationCanceledException) completion.SetCanceled(ct); else if (failure != null) completion.SetException(failure); else completion.SetResult();
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
