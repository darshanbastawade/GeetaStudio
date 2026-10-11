using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class EditorialRules
{
    public static readonly string[] Formats = ["Article", "Translation", "Episode", "Export", "Post"];
    public static void Evidence(string value, string mode)
    {
        CoreRules.Text(value, 1000, "Editorial evidence");
        if (mode is not ("Fixture" or "Manual") || !value.Trim().StartsWith(mode == "Fixture" ? "fixture:" : "manual:", StringComparison.OrdinalIgnoreCase)) throw new RuntimeValidationException("Explicit fixture: or manual: review reference required.");
    }
    public static string Next(string current, string action) => (current, action) switch
    {
        ("Brief", "Draft") => "Draft",
        ("Draft" or "Rejected" or "Failed" or "Corrected", "Evaluate") => "Evaluated",
        ("Evaluated", "Review") => "Review",
        ("Review", "Approve") => "Approved",
        ("Review" or "Evaluated", "Reject") => "Rejected",
        ("Approved", "Schedule") => "Scheduled",
        ("Scheduled", "PublishDryRun") => "Published",
        (_, "Withdraw") => "Withdrawn",
        (_, "Fail") => "Failed",
        _ => throw new RuntimeConflictException("Editorial action is not valid from the current state.")
    };
    public static void Media(string mime, byte[] content)
    {
        if (content.Length is < 1 or > 65536) throw new RuntimeValidationException("Preview asset must be1–65536 bytes.");
        var valid = mime switch
        {
            "audio/wav" => content.Length >= 44 && System.Text.Encoding.ASCII.GetString(content, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(content, 8, 4) == "WAVE",
            "image/png" => content.Length >= 8 && content.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "video/mp4" => content.Length >= 12 && System.Text.Encoding.ASCII.GetString(content, 4, 4) == "ftyp",
            "text/plain" => true,
            _ => false
        };
        if (!valid) throw new RuntimeValidationException("Supported preview types: validated WAV/PNG/MP4 headers or plain text. This does not qualify media quality.");
    }
}
