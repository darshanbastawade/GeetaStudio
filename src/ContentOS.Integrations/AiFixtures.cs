using System.Runtime.CompilerServices;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

public sealed class FixtureTextA : ITextGenerationProvider
{
    public string Key => "fixture.text.a";
    public void Validate(AiProviderSettings p)
    {
        if (p.Model != "fixture-a.v1" || p.Capability != "ai.text" || p.Scenario is not ("success" or "schema" or "refusal" or "quota" or "transient" or "unknown" or "slow" or "cache")) throw new ArgumentException("Invalid deployed fixture A profile.");
    }
    public async Task<AiProviderResult> GenerateAsync(AiProviderCall c, CancellationToken ct)
    {
        Validate(c.Settings); ct.ThrowIfCancellationRequested();
        if (c.Settings.Scenario == "slow") await Task.Delay(5000, ct);
        var usage = new AiUsage(40, c.Settings.Scenario == "cache" ? 30 : 0, 20);
        return c.Settings.Scenario switch
        {
            "schema" => new("Success", "{\"unexpected\":true}", usage),
            "refusal" => new("PolicyRejected", null, usage),
            "quota" => new("QuotaExhausted", null, new(0, 0, 0)),
            "transient" => new("Unavailable", null, usage),
            "unknown" => new("UnknownUsage", null, null),
            _ => new("Success", JsonSerializer.Serialize(new { text = TalkExtractiveFixture.Text(c) ?? "Fixture A: an original calm conversation.", citations = c.SourceIds }), usage, "fixture-a-" + c.AttemptId)
        };
    }
    public async IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall c, [EnumeratorCancellation] CancellationToken ct)
    {
        var r = await GenerateAsync(c, ct);
        if (r.Content != null) foreach (var part in r.Content.Chunk(12)) { ct.ThrowIfCancellationRequested(); yield return new("Delta", new string(part)); }
        yield return new("Usage", Result: r);
    }
}
public sealed class FixtureTextB : ITextGenerationProvider
{
    public string Key => "fixture.text.b";
    public void Validate(AiProviderSettings p)
    {
        if (p.Model != "fixture-b.v1" || p.Capability != "ai.text" || p.Scenario != "success") throw new ArgumentException("Invalid deployed fixture B profile.");
    }
    public Task<AiProviderResult> GenerateAsync(AiProviderCall c, CancellationToken ct)
    {
        Validate(c.Settings); ct.ThrowIfCancellationRequested();
        // Independent response/usage behavior; no invocation of fixture A.
        var body = new Dictionary<string, object> { ["citations"] = c.SourceIds, ["text"] = TalkExtractiveFixture.Text(c) ?? "Fixture B: listen, consider alternatives, then choose." };
        return Task.FromResult(new AiProviderResult("Success", JsonSerializer.Serialize(body), new(55, 0, 28), "fixture-b-" + c.AttemptId));
    }
    public async IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall c, [EnumeratorCancellation] CancellationToken ct)
    {
        var result = await GenerateAsync(c, ct);
        for (var i = 0; i < result.Content!.Length; i += 7) { ct.ThrowIfCancellationRequested(); yield return new("Delta", result.Content.Substring(i, Math.Min(7, result.Content.Length - i))); }
        yield return new("Usage", Result: result);
    }
}
public sealed class LocalAiEmbedding : IAiEmbeddingAdapter
{
    public string Key => "local.embedding.v1";
    public void Validate(AiProviderSettings p)
    {
        if (p.Capability != "ai.embedding" || p.Model is not ("concept-projection-v1" or "concept-projection-v2") || p.Structured || p.Streaming || p.Tools || p.Scenario != "success") throw new ArgumentException("Invalid local embedding profile.");
    }
    public async ValueTask<AiEmbeddingResult> EmbedAsync(AiEmbeddingCall c, CancellationToken ct)
    {
        Validate(c.Settings);
        var result = await new ReviewedConceptEmbedding().EmbedAsync(c.Request with { Model = c.Settings.Model }, ct);
        return new(result, new(System.Text.Encoding.UTF8.GetByteCount(c.Request.Text), 0, 0));
    }
}
