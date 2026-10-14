using System.Globalization;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

public sealed class ApprovedSceneSubtitles : ISubtitleProvider
{
    public string Key => "watch.approved-scene-cues.v1";
    public WatchCue[] Compile(WatchScene[] scenes) => scenes.Select(x => new WatchCue(x.StartSeconds, x.StartSeconds + x.DurationSeconds, x.Text, x.Key)).ToArray();
    private static string Time(double seconds, bool srt) { var t = TimeSpan.FromMilliseconds(Math.Round(seconds * 1000)); return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}{(srt ? ',' : '.')}{t.Milliseconds:000}"; }
    private static string Text(string text) => text.Replace("\r", "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    public string Vtt(WatchCue[] cues) => "WEBVTT\n\n" + string.Join("\n\n", cues.Select(x => Time(x.StartSeconds, false) + " --> " + Time(x.EndSeconds, false) + "\n" + Text(x.Text))) + "\n";
    public string Srt(WatchCue[] cues) => string.Join("\n\n", cues.Select((x, i) => (i + 1) + "\n" + Time(x.StartSeconds, true) + " --> " + Time(x.EndSeconds, true) + "\n" + Text(x.Text))) + "\n";
}
