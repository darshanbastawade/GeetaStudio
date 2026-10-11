using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.Extensions.AI;
namespace ContentOS.Integrations;

// Fixed reviewed protocol/endpoint. Redirects and SDK retries are disabled by composition.
public sealed class OpenAiTextProvider(HttpClient client, ISecretResolver secrets) : ITextGenerationProvider
{
    public string Key => "openai.text.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public void Validate(AiProviderSettings p)
    {
        if (p.Capability != "ai.text" || !p.Structured || p.Tools || p.Scenario != "success" ||
            !System.Text.RegularExpressions.Regex.IsMatch(p.Model, "^[A-Za-z0-9_.:-]{1,150}$"))
            throw new ArgumentException("This protocol supports qualified structured text, bounded completion tokens and optional streaming only.");
    }
    private async Task<HttpRequestMessage> Request(AiProviderCall c, CancellationToken ct)
    {
        Validate(c.Settings);
        if (c.SecretRef == null) throw new InvalidOperationException("Missing configured credential reference.");
        using var credential = await secrets.ResolveAsync(c.SecretRef, ct) ?? throw new InvalidOperationException("Credential unavailable.");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new string(credential.Value.Span));
        var body = new Dictionary<string, object?>
        {
            ["model"] = c.Settings.Model,
            ["messages"] = new[] { new { role = "user", content = c.Prompt } },
            ["max_completion_tokens"] = c.OutputLimit,
            ["store"] = false,
            ["stream"] = c.Streaming,
            ["response_format"] = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "generated_text_v1",
                    strict = true,
                    schema = new { type = "object", properties = new { text = new { type = "string" }, citations = new { type = "array", items = new { type = "string" } } }, required = new[] { "text", "citations" }, additionalProperties = false }
                }
            }
        };
        if (c.Streaming) body["stream_options"] = new { include_usage = true };
        request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json"); return request;
    }
    private static AiUsage? Usage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object) return null;
        if (!u.TryGetProperty("prompt_tokens", out var input) || !u.TryGetProperty("completion_tokens", out var output)) return null;
        var cached = u.TryGetProperty("prompt_tokens_details", out var detail) && detail.TryGetProperty("cached_tokens", out var count) ? count.GetInt64() : 0;
        var frameworkUsage = new UsageDetails { InputTokenCount = input.GetInt64(), OutputTokenCount = output.GetInt64(), CachedInputTokenCount = cached };
        return new(frameworkUsage.InputTokenCount!.Value, frameworkUsage.CachedInputTokenCount!.Value, frameworkUsage.OutputTokenCount!.Value);
    }
    private static string? Id(JsonElement root) => root.TryGetProperty("id", out var id) ? id.GetString() : null;
    private static AiProviderResult Error(HttpStatusCode status, string body)
    {
        string? code = null;
        try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var c)) code = c.GetString(); } catch (JsonException) { }
        var outcome = status switch
        {
            HttpStatusCode.TooManyRequests => code == "insufficient_quota" ? "QuotaExhausted" : "RateLimited",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Configuration",
            HttpStatusCode.BadRequest => code == "content_policy_violation" ? "PolicyRejected" : "UnsupportedCapability",
            _ => "UnknownUsage"
        };
        return new(outcome, null, outcome == "UnknownUsage" ? null : new(0, 0, 0));
    }
    private static async Task<string> BoundedBody(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream(); var bytes = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(bytes, ct)) > 0) { if (memory.Length + read > 131072) throw new InvalidDataException("Provider response too large."); await memory.WriteAsync(bytes.AsMemory(0, read), ct); }
        return Encoding.UTF8.GetString(memory.ToArray());
    }
    public async Task<AiProviderResult> GenerateAsync(AiProviderCall c, CancellationToken ct)
    {
        using var request = await Request(c with { Streaming = false }, ct);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await BoundedBody(response, ct); if (!response.IsSuccessStatusCode) return Error(response.StatusCode, body);
        using var doc = JsonDocument.Parse(body); var root = doc.RootElement; var message = root.GetProperty("choices")[0].GetProperty("message");
        if (message.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(refusal.GetString())) return new("PolicyRejected", null, Usage(root), Id(root));
        // Framework chat/usage DTOs stay inside this adapter; owned contracts cross the boundary.
        var chat = new ChatResponse(new ChatMessage(ChatRole.Assistant, message.GetProperty("content").GetString()));
        return new("Success", chat.Text, Usage(root), Id(root));
    }
    public async IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall c, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = await Request(c with { Streaming = true }, ct);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) { yield return new("Usage", Result: Error(response.StatusCode, await BoundedBody(response, ct))); yield break; }
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var reader = new StreamReader(stream, Encoding.UTF8);
        AiUsage? usage = null; string? requestId = null; var refused = false; var total = 0; var completed = false;
        // Bound lines without StreamReader.ReadLine allocating an unbounded provider line.
        var line = new StringBuilder(); var one = new char[1];
        while (await reader.ReadAsync(one.AsMemory(), ct) > 0)
        {
            if (++total > 262144 || line.Length > 65536) throw new InvalidDataException("Provider stream too large.");
            if (one[0] != '\n') { if (one[0] != '\r') line.Append(one[0]); continue; }
            var value = line.ToString(); line.Clear(); if (!value.StartsWith("data:")) continue; var data = value[5..].Trim();
            if (data == "[DONE]") { completed = true; break; }
            using var doc = JsonDocument.Parse(data); var root = doc.RootElement; usage = Usage(root) ?? usage; requestId = Id(root) ?? requestId;
            if (!root.TryGetProperty("choices", out var choices)) continue;
            foreach (var choice in choices.EnumerateArray())
            {
                if (!choice.TryGetProperty("delta", out var delta)) continue;
                if (delta.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String) refused = true;
                if (delta.TryGetProperty("content", out var text) && text.ValueKind == JsonValueKind.String) yield return new("Delta", text.GetString());
            }
        }
        yield return new("Usage", Result: new(!completed ? "UnknownUsage" : refused ? "PolicyRejected" : "Success", null, completed ? usage : null, requestId));
    }
}
