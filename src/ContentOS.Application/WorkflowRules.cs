using System.Text.Json;
using System.Text.RegularExpressions;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class WorkflowRules
{
    private static void Key(string key) { if (!Regex.IsMatch(key ?? "", @"^[a-z0-9][a-z0-9-]{0,79}$")) throw new RuntimeValidationException("Invalid artifact key."); }
    public static readonly string[] Sequence = ["retrieve", "plan", "draft", "check", "review"];
    public static void Prompt(RegisterPrompt p)
    {
        Key(p.Key); CoreRules.Text(p.Template, 4000, "Template");
        if (p.Version < 1 || p.Locale != "en-IN" || p.AudienceId == Guid.Empty || p.OutputSchema != "generated-text.v1" ||
            p.Inputs is null || p.Inputs.Length is < 1 or > 8 || p.Inputs.Select(x => x.Name).Distinct().Count() != p.Inputs.Length)
            throw new RuntimeValidationException("Unsupported prompt bindings/schema.");
        foreach (var x in p.Inputs) { Key(x.Name); if (x.MaxLength is < 1 or > 2000) throw new RuntimeValidationException("Input bound invalid."); }
        foreach (Match m in Regex.Matches(p.Template, @"\{\{([a-z0-9-]+)\}\}"))
            if (!p.Inputs.Any(x => x.Name == m.Groups[1].Value)) throw new RuntimeValidationException("Template references undeclared input.");
    }
    public static void Definition(RegisterWorkflow d)
    {
        Key(d.Key);
        if (d.Version < 1 || d.PromptId == Guid.Empty || d.RouteId == Guid.Empty || d.Steps is null ||
            !d.Steps.Select(x => x.Kind).SequenceEqual(Sequence) || !d.HumanReview || d.Currency is not ("USD" or "INR") ||
            d.MaxCost is <= 0 or > 100 || d.MaxTokens is < 256 or > 200000 || d.DeadlineSeconds is < 120 or > 3600 ||
            d.LeaseSeconds is < 30 or > 120 || d.LeaseSeconds >= d.DeadlineSeconds)
            throw new RuntimeValidationException("Only bounded retrieve/plan/draft/check/review workflows are supported.");
        for (var i = 0; i < d.Steps.Length; i++)
        {
            var expected = i == 0 ? "evidence.retrieve.v1" : i == 2 ? "ai.generate.v1" : null;
            if (d.Steps[i].Tool != expected || d.Steps[i].MaxAttempts is < 1 or > 3 || i >= 1 && d.Steps[i].MaxAttempts != 1)
                throw new RuntimeValidationException("Tool or retry policy unsupported.");
        }
    }
    public static void Plan(string? json)
    {
        if (json == null) return;
        if (json.Length > 1000) throw new RuntimeValidationException("Plan exceeds bound.", "InvalidPlan");
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || doc.RootElement.EnumerateObject().Count() != 1 ||
                !doc.RootElement.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array ||
                !steps.EnumerateArray().Select(x => x.GetString()).SequenceEqual(Sequence))
                throw new RuntimeValidationException("Plan cannot alter the owned sequence.", "InvalidPlan");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        { throw new RuntimeValidationException("Malformed plan.", "InvalidPlan"); }
    }
    public static string Render(RegisterPrompt p, Dictionary<string, string> values)
    {
        if (values == null || values.Keys.Any(k => !p.Inputs.Any(x => x.Name == k))) throw new RuntimeValidationException("Undeclared prompt input.");
        var text = p.Template;
        foreach (var input in p.Inputs)
        {
            if (!values.TryGetValue(input.Name, out var value)) { if (input.Required) throw new RuntimeValidationException("Required input missing."); value = ""; }
            if (value == null || value.Length > input.MaxLength || input.Required && string.IsNullOrWhiteSpace(value)) throw new RuntimeValidationException("Input exceeds its schema.");
            // One pass: input values never become template expressions.
            // Validate each declared input before a single template expansion.
        }
        text = Regex.Replace(text, @"\{\{([a-z0-9-]+)\}\}", m => JsonSerializer.Serialize(values.GetValueOrDefault(m.Groups[1].Value, "")));
        if (text.Length > 10000) throw new RuntimeValidationException("Rendered input exceeds bound.");
        return text;
    }
    public static void Untrusted(string text)
    {
        if (text.Length > 12000) throw new RuntimeValidationException("Evidence exceeds bound.", "UntrustedDataRejected");
        if (Regex.IsMatch(text, @"ignore\s+(all\s+|previous\s+|prior\s+)?instructions|system\s*prompt|invoke\s+(a\s+)?tool|bypass\s+(review|policy)|change\s+(the\s+)?route|reveal\s+(secrets|credentials)", RegexOptions.IgnoreCase))
            throw new RuntimeValidationException("Instruction-bearing evidence requires staff review.", "UntrustedDataRejected");
    }
}
