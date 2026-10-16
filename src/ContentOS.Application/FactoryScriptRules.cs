using System.Text.Json;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class FactoryScriptRules
{
    public static FactoryGeneratedScript Parse(string text, Guid[] cast)
    {
        try
        {
            using var doc = JsonDocument.Parse(text); var root = doc.RootElement;
            string[] fields = ["schema", "hook", "scene", "dialogue", "decision", "reflection"];
            if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(fields.Order())) throw new JsonException();
            var script = root.Deserialize<FactoryGeneratedScript>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            if (script.Schema != "factory-script.v1" || script.Dialogue is not { Length: > 0 and <= 8 } || new[] { script.Hook, script.Scene, script.Decision, script.Reflection }.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 2000) || script.Dialogue.Any(x => !cast.Contains(x.CharacterId) || string.IsNullOrWhiteSpace(x.Text) || x.Text.Length > 1000)) throw new JsonException();
            foreach (var d in root.GetProperty("dialogue").EnumerateArray()) if (d.ValueKind != JsonValueKind.Object || !d.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(new[] { "characterId", "text" }.Order())) throw new JsonException();
            return script;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException or NullReferenceException) { throw new RuntimeValidationException("Invalid factory-script.v1 output or fabricated cast binding.", "SchemaFailure"); }
    }
}
