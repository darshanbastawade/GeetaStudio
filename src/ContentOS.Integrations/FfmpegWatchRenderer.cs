using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

public sealed record WatchToolConfiguration(string Ffmpeg, string FfmpegSha256, string Ffprobe, string FfprobeSha256, string FontFile, string FontSha256, string SpoolRoot);
public sealed class FfmpegWatchRenderer(WatchToolConfiguration tools, string key = "watch.ffmpeg.v1") : IVideoRenderer
{
    public string Key => key;
    private bool Configured => File.Exists(tools.Ffmpeg) && File.Exists(tools.Ffprobe) && File.Exists(tools.FontFile) && tools.FfmpegSha256.Length == 64 && tools.FfprobeSha256.Length == 64 && tools.FontSha256.Length == 64;
    public WatchRendererCapabilities Capabilities => new(Configured, true, ["Vertical", "Standard"], ["en-IN", "en-US", "hi-IN", "mr-IN", "sa-IN"], true, true, "LocalRenderer", Configured ? WatchRules.Hash("watch.composition.v4:" + tools.FfmpegSha256 + tools.FfprobeSha256 + tools.FontSha256 + Key) : "Unavailable");
    private static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void Verified(string path, string expected) { if (!Path.IsPathFullyQualified(path) || !File.Exists(path) || HashFile(path) != expected.ToUpperInvariant()) throw new EvidenceBlockedException("ApprovedRendererBinaryOrFontChecksumRequired"); }
    private string DirectoryFor(WatchRenderRequest r) { var root = Path.GetFullPath(tools.SpoolRoot); var path = Path.Combine(root, WatchRules.Hash(r.AccountReference + "|" + r.ResourceKey)[..16], r.OperationId.ToString("N")); Directory.CreateDirectory(path); return path; }
    private string Fingerprint(WatchRenderRequest r) => WatchRules.Hash(JsonSerializer.Serialize(new { r.OperationId, r.AccountReference, r.ResourceKey, r.Aspect, r.Timeline, Version = Capabilities.Version, Materials = r.Materials.Select(x => new { x.SceneKey, Image = MediaRules.Hash(x.Image), Voice = MediaRules.Hash(x.Voice), Music = x.Music == null ? null : MediaRules.Hash(x.Music) }) }));
    private string Resource(WatchRenderRequest r) => "local-render:" + r.OperationId.ToString("N") + ":" + WatchRules.Hash(r.AccountReference + "|" + r.ResourceKey + "|" + Capabilities.Version)[..16];
    private async Task<string> Run(string executable, IEnumerable<string> args, string directory, CancellationToken ct, bool diagnostics = false)
    {
        var psi = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory, RedirectStandardError = true, RedirectStandardOutput = true }; foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment.Clear(); foreach (var k in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "PATH" }) if (Environment.GetEnvironmentVariable(k) is string value) psi.Environment[k] = value;
        using var p = Process.Start(psi) ?? throw new RuntimeConflictException("Renderer process could not start.");
        async Task<string> Bounded(StreamReader reader) { var result = new StringBuilder(); var buffer = new char[2048]; int n; while ((n = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0) { if (result.Length + n > 65536) throw new RuntimeConflictException("Renderer diagnostic bound exceeded."); result.Append(buffer, 0, n); } return result.ToString(); }
        var stdout = Bounded(p.StandardOutput); var stderr = Bounded(p.StandardError);
        try { await p.WaitForExitAsync(ct); var output = await stdout; var errors = await stderr; if (p.ExitCode != 0) throw new RuntimeConflictException("Owned renderer exited with code " + p.ExitCode + "; operator diagnostics: " + errors[..Math.Min(errors.Length, 500)]); return diagnostics ? output + errors : output; }
        catch { if (!p.HasExited) p.Kill(true); await p.WaitForExitAsync(CancellationToken.None); throw; }
    }
    private static string Number(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Escape(string text) => text.Replace("\\", "／").Replace("{", "(").Replace("}", ")").Replace("\r", "").Replace("\n", "\\N");
    private static string Wrapped(string text, int limit)
    { var lines = new List<string>(); var current = new StringBuilder(); foreach (var word in text.Replace("\r", "").Replace("\n", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)) { var elements = StringInfo.GetTextElementEnumerator(word); var pieces = new List<string>(); var piece = new StringBuilder(); var count = 0; while (elements.MoveNext()) { piece.Append(elements.GetTextElement()); if (++count == limit) { pieces.Add(piece.ToString()); piece.Clear(); count = 0; } } if (piece.Length > 0) pieces.Add(piece.ToString()); foreach (var part in pieces) { if (new StringInfo(current.ToString() + " " + part).LengthInTextElements > limit && current.Length > 0) { lines.Add(current.ToString()); current.Clear(); } if (current.Length > 0) current.Append(' '); current.Append(part); } } if (current.Length > 0) lines.Add(current.ToString()); return Escape(string.Join("\n", lines)); }
    private static string Ass(WatchTimeline t, string aspect, string text, double duration, bool credits = false)
    {
        var (w, h) = WatchRules.Size(aspect); var rules = t.Template.Configuration.Rules; var margin = w * rules.SafeMarginPercent / 100; var vm = h * rules.SafeMarginPercent / 100;
        string stamp(double sec) { var a = TimeSpan.FromSeconds(sec); return $"{(int)a.TotalHours}:{a.Minutes:00}:{a.Seconds:00}.{a.Milliseconds / 10:00}"; }
        return $"[Script Info]\nScriptType: v4.00+\nPlayResX: {w}\nPlayResY: {h}\nWrapStyle: 0\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Default,{rules.Font},{(credits ? 20 : rules.CaptionSize)},&H00FFFFFF,&H00FFFFFF,&H00000000,&H90000000,0,0,0,0,100,100,0,0,3,1,0,{(credits ? 5 : 2)},{margin},{margin},{vm},1\nStyle: Banner,{rules.Font},16,&H00FFFFFF,&H00FFFFFF,&H00000000,&H90000000,0,0,0,0,100,100,0,0,3,1,0,8,{margin},{margin},{vm},1\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\nDialogue: 0,0:00:00.00,{stamp(duration)},Default,,0,0,0,,{Wrapped(text, aspect == "Vertical" ? 20 : 40)}\n" + (credits ? "" : $"Dialogue: 1,0:00:00.00,{stamp(duration)},Banner,,0,0,0,,{Escape(t.EventLabel.Length > 0 ? t.EventLabel : t.FictionLabel)}\n");
    }
    public async Task<WatchRenderResult> SubmitAsync(WatchRenderRequest r, CancellationToken ct)
    {
        if (!Configured) throw new EvidenceBlockedException("ConfigurePinnedLocalRendererTools"); Verified(tools.Ffmpeg, tools.FfmpegSha256); Verified(tools.Ffprobe, tools.FfprobeSha256); Verified(tools.FontFile, tools.FontSha256);
        if (!Capabilities.Aspects.Contains(r.Aspect) || !Capabilities.Locales.Contains(r.Timeline.Locale) || r.Materials.Length != r.Timeline.Scenes.Length) throw new EvidenceBlockedException("UnsupportedRendererTimelineRequirement"); WatchRules.Timing(r.Timeline, r.Aspect);
        var dir = DirectoryFor(r); if (File.Exists(Path.Combine(dir, "accepted.json"))) return await ReconcileAsync(r, Resource(r), ct);
        var fonts = Path.Combine(dir, "fonts"); Directory.CreateDirectory(fonts); File.Copy(tools.FontFile, Path.Combine(fonts, "Nirmala.ttc"), true); var (w, h) = WatchRules.Size(r.Aspect); var rules = r.Timeline.Template.Configuration.Rules; var parts = new List<string>();
        for (var i = 0; i < r.Materials.Length; i++)
        {
            var m = r.Materials[i]; var scene = r.Timeline.Scenes[i]; if (m.SceneKey != scene.Key || MediaRules.Hash(m.Image) != scene.Image.Info.Sha256 || MediaRules.Hash(m.Voice) != scene.Voice.Info.Sha256) throw new RuntimeConflictException("Owned scene asset checksum mismatch."); MediaFiles.Inspect(m.Image); MediaFiles.Inspect(m.Voice);
            await File.WriteAllBytesAsync(Path.Combine(dir, $"image{i}.png"), m.Image, ct); await File.WriteAllBytesAsync(Path.Combine(dir, $"voice{i}.wav"), m.Voice, ct); await File.WriteAllTextAsync(Path.Combine(dir, $"scene{i}.ass"), Ass(r.Timeline, r.Aspect, scene.Text, scene.DurationSeconds), new UTF8Encoding(false), ct);
            var args = new List<string> { "-nostdin", "-y", "-v", "error", "-protocol_whitelist", "file,pipe", "-loop", "1", "-i", $"image{i}.png", "-i", $"voice{i}.wav" };
            if (m.Music != null) { MediaFiles.Inspect(m.Music); if (scene.Music == null || MediaRules.Hash(m.Music) != scene.Music.Info.Sha256) throw new RuntimeConflictException("Music binding mismatch."); await File.WriteAllBytesAsync(Path.Combine(dir, $"music{i}.wav"), m.Music, ct); args.AddRange(["-stream_loop", "-1", "-i", $"music{i}.wav", "-filter_complex", "[1:a]aformat=channel_layouts=mono,aresample=48000,volume=0.65,alimiter=limit=0.70:level=false[voice];[2:a]volume=0.12[music];[voice][music]amix=inputs=2:duration=first,aresample=48000,volume=0.65,alimiter=limit=0.70:level=false,apad[a]", "-map", "0:v", "-map", "[a]"]); } else args.AddRange(["-af", "aresample=48000,volume=0.65,alimiter=limit=0.70:level=false,apad"]);
            args.AddRange(["-vf", $"scale={w * 4 / 5}:{h / 2}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/3:color=0x{rules.Background[1..]},ass=scene{i}.ass:fontsdir=fonts:shaping=complex", "-t", Number(scene.DurationSeconds), "-r", "24", "-c:v", "libx264", "-preset", "ultrafast", "-crf", key == "watch.ffmpeg.test-b.v1" ? "26" : "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k", "-ar", "48000", "-ac", "1", "-threads", "1", "-movflags", "+faststart", $"part{i}.mp4"]);
            await Run(tools.Ffmpeg, args, dir, ct); parts.Add($"file 'part{i}.mp4'");
        }
        var pages = WatchRules.CreditPages(r.Timeline);
        for (var i = 0; i < pages.Length; i++)
        {
            var page = pages[i]; var credit = $"credit{i}"; await File.WriteAllTextAsync(Path.Combine(dir, credit + ".ass"), Ass(r.Timeline, r.Aspect, page.Text, page.DurationSeconds, true), new UTF8Encoding(false), ct);
            await Run(tools.Ffmpeg, ["-nostdin", "-y", "-v", "error", "-f", "lavfi", "-i", $"color=c=0x{rules.Background[1..]}:s={w}x{h}:r=24", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=mono", "-vf", $"ass={credit}.ass:fontsdir=fonts:shaping=complex", "-t", Number(page.DurationSeconds), "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k", "-threads", "1", credit + ".mp4"], dir, ct); parts.Add($"file '{credit}.mp4'");
        }
        await File.WriteAllLinesAsync(Path.Combine(dir, "parts.txt"), parts, new UTF8Encoding(false), ct);
        await Run(tools.Ffmpeg, ["-nostdin", "-y", "-v", "error", "-protocol_whitelist", "file,pipe", "-f", "concat", "-safe", "1", "-i", "parts.txt", "-c", "copy", "-movflags", "+faststart", "master.mp4"], dir, ct);
        var master = Path.Combine(dir, "master.mp4"); if (new FileInfo(master).Length is < 1 or > 33554432) throw new RuntimeConflictException("WATCH master exceeds32MiB."); var bytes = await File.ReadAllBytesAsync(master, ct); var info = await InspectAsync(bytes, ct); if (!info.Playable) throw new RuntimeConflictException("Renderer produced an invalid master.");
        await File.WriteAllTextAsync(Path.Combine(dir, "accepted.json"), JsonSerializer.Serialize(new { Resource = Resource(r), Sha256 = MediaRules.Hash(bytes), Version = Capabilities.Version, Fingerprint = Fingerprint(r) }), ct);
        return new("Completed", Resource(r), bytes, "video/mp4", 0, Capabilities.Version, info.Warnings, "OwnedLocalComposition");
    }
    public async Task<WatchRenderResult> ReconcileAsync(WatchRenderRequest r, string? resource, CancellationToken ct)
    {
        if (resource != null && resource != Resource(r)) throw new RuntimeConflictException("Original renderer/account/resource required."); var dir = DirectoryFor(r); var receipt = Path.Combine(dir, "accepted.json"); var path = Path.Combine(dir, "master.mp4"); if (!File.Exists(receipt) || !File.Exists(path)) return new("Unknown", resource, null, "video/mp4", 0, Capabilities.Version, [], "OriginalRendererOutputNotYetConfirmed");
        if (new FileInfo(path).Length > 33554432) throw new RuntimeConflictException("Master size bound exceeded."); var bytes = await File.ReadAllBytesAsync(path, ct); using var record = JsonDocument.Parse(await File.ReadAllTextAsync(receipt, ct)); if (!record.RootElement.TryGetProperty("Fingerprint", out var fingerprint) || fingerprint.GetString() != Fingerprint(r) || record.RootElement.GetProperty("Sha256").GetString() != MediaRules.Hash(bytes) || record.RootElement.GetProperty("Resource").GetString() != Resource(r) || record.RootElement.GetProperty("Version").GetString() != Capabilities.Version) throw new RuntimeConflictException("Original renderer receipt changed."); return new("Completed", Resource(r), bytes, "video/mp4", 0, Capabilities.Version, ["HumanVisualListeningAndNativeLanguageReviewRequired"], "OriginalOwnedMasterReconciled");
    }
    public async Task<WatchFileInfo> InspectAsync(byte[] master, CancellationToken ct)
    {
        if (master.Length is < 1 or > 33554432) throw new RuntimeValidationException("Bounded owned WATCH master required."); Verified(tools.Ffprobe, tools.FfprobeSha256); Verified(tools.Ffmpeg, tools.FfmpegSha256);
        var dir = Path.Combine(Path.GetFullPath(tools.SpoolRoot), "qa", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); await File.WriteAllBytesAsync(Path.Combine(dir, "probe.mp4"), master, ct);
        var output = await Run(tools.Ffprobe, ["-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "stream=codec_type,codec_name,width,height:format=duration", "-of", "json", "probe.mp4"], dir, ct); using var json = JsonDocument.Parse(output); var streams = json.RootElement.GetProperty("streams").EnumerateArray().ToArray(); var video = streams.Single(x => x.GetProperty("codec_type").GetString() == "video"); var audio = streams.Single(x => x.GetProperty("codec_type").GetString() == "audio"); var duration = double.Parse(json.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture); var width = video.GetProperty("width").GetInt32(); var height = video.GetProperty("height").GetInt32();
        var peak = await Run(tools.Ffmpeg, ["-nostdin", "-v", "info", "-i", "probe.mp4", "-af", "volumedetect", "-sn", "-f", "null", "-"], dir, ct, true); // Structural audio checks do not establish listening quality.
        var valid = duration is > 0 and <= 132 && video.GetProperty("codec_name").GetString() == "h264" && audio.GetProperty("codec_name").GetString() == "aac" && (width == 360 && height == 640 || width == 640 && height == 360);
        var match = System.Text.RegularExpressions.Regex.Match(peak, @"max_volume: ([\-0-9.]+) dB"); double? peakDb = match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        return new(duration, width, height, peakDb, valid && peakDb.HasValue && peakDb.Value < -.05, ["SceneLevelTimingNotWordAlignment", "HumanVisualListeningAndNativeLanguageReviewRequired"]);
    }
}
public sealed class WatchRenderFixture(IVideoRenderer inner, string key) : IVideoRenderer
{
    public string Key => key; public WatchRendererCapabilities Capabilities => inner.Capabilities with { Qualification = "OfflineFixture", Version = WatchRules.Hash(inner.Capabilities.Version + key) };
    private bool FirstFailure(Guid op) { var directory = Path.Combine(Path.GetTempPath(), "GeetaStudio", "watch-failure-fixture"); Directory.CreateDirectory(directory); var path = Path.Combine(directory, WatchRules.Hash(key)[..16] + "-" + op.ToString("N") + ".marker"); try { using var marker = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); return true; } catch (IOException) when (File.Exists(path)) { return false; } }
    public async Task<WatchRenderResult> SubmitAsync(WatchRenderRequest r, CancellationToken ct)
    { if (key == "watch.fail-once.v1" && FirstFailure(r.OperationId)) return new("Failed", null, null, "video/mp4", 0, Capabilities.Version, [], "ControlledPreSubmissionFailure"); var result = await inner.SubmitAsync(r, ct); return key == "watch.unknown.v1" ? result with { State = "Unknown", Master = null, RendererVersion = Capabilities.Version } : result with { RendererVersion = Capabilities.Version }; }
    public async Task<WatchRenderResult> ReconcileAsync(WatchRenderRequest r, string? resource, CancellationToken ct) => (await inner.ReconcileAsync(r, resource, ct)) with { RendererVersion = Capabilities.Version };
    public Task<WatchFileInfo> InspectAsync(byte[] master, CancellationToken ct) => inner.InspectAsync(master, ct);
}
