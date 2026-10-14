using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class CreationRules
{
    public static void Require(bool ok, string message) { if (!ok) throw new RuntimeValidationException(message); }
    public static void Input(CreationInput r, string[] themes)
    {
        Require(!string.IsNullOrWhiteSpace(r.Nickname) && r.Nickname.Length <= 30 && r.Nickname.All(x => char.IsLetter(x) || x is ' ' or '-'), "Use a short fictional nickname, without personal identifiers.");
        Require(themes.Contains(r.Theme) && r.Reflection.Length <= 1000, "Choose a reviewed theme and bounded reflection.");
        Require(!r.Reflection.Any(x => char.IsControl(x) && x is not ('\n' or '\r' or '\t')) && !new[] { "system prompt", "api key", "execute", "tool call", "ignore instructions", "<script" }.Any(x => r.Reflection.Contains(x, StringComparison.OrdinalIgnoreCase)), "Untrusted instructions and active content are not personalization inputs.");
    }
}
