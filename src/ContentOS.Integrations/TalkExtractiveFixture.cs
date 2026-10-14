using System.Text.Json;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

internal static class TalkExtractiveFixture
{
    // Controlled adapter behavior, never represented as human quality qualification.
    internal static string? Text(AiProviderCall c)
    {
        const string marker = "TALK_INPUT_JSON:"; var start = c.Prompt.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        var input = c.Prompt[(start + marker.Length)..]; var suffix = input.IndexOf("\nEvidence data:", StringComparison.Ordinal); if (suffix >= 0) input = input[..suffix];
        using var json = JsonDocument.Parse(input); return json.RootElement.GetProperty("evidence")[0].GetProperty("exactText").GetString();
    }
}
