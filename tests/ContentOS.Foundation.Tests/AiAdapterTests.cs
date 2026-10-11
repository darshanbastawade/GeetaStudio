using System.Net;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class AiAdapterTests
{
    private sealed class Secrets : ISecretResolver
    {
        public ValueTask<ResolvedSecret?> ResolveAsync(string reference, CancellationToken cancellationToken = default) => ValueTask.FromResult<ResolvedSecret?>(new("fixture-credential"));
        public ValueTask<bool> IsAvailableAsync(string? reference, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls; public string? RequestBody; public Uri? Endpoint;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Calls++; RequestBody = await r.Content!.ReadAsStringAsync(ct); Endpoint = r.RequestUri;
            return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private static AiProviderCall Call(bool streaming = false) => new(Guid.NewGuid(), Guid.NewGuid(),
        SqlAiTests.Settings() with { Adapter = "openai.text.v1", Model = "configured-contract-model", Qualification = "Unqualified" },
        "original fixture", 128, [], streaming, "config:ProviderSecrets:fixture");
    [Fact]
    public async Task Real_adapter_maps_strict_schema_cache_usage_fixed_endpoint_and_no_provider_retries()
    {
        var handler = new Handler(HttpStatusCode.OK, """{"id":"contract-request","choices":[{"message":{"content":"{\"text\":\"generated fixture\",\"citations\":[]}"}}],"usage":{"prompt_tokens":40,"completion_tokens":20,"prompt_tokens_details":{"cached_tokens":30}}}""");
        var adapter = new OpenAiTextProvider(new HttpClient(handler), new Secrets()); var result = await adapter.GenerateAsync(Call(), CancellationToken.None);
        Assert.Equal("Success", result.Outcome); Assert.Equal(new AiUsage(40, 30, 20), result.Usage); Assert.Equal(1, handler.Calls);
        Assert.Equal("api.openai.com", handler.Endpoint!.Host); Assert.Equal("/v1/chat/completions", handler.Endpoint.AbsolutePath);
        using var body = JsonDocument.Parse(handler.RequestBody!); Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        Assert.True(body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.Equal(128, body.RootElement.GetProperty("max_completion_tokens").GetInt32()); Assert.False(body.RootElement.TryGetProperty("tools", out _));
    }
    [Theory]
    [InlineData(429, "insufficient_quota", "QuotaExhausted", true)]
    [InlineData(429, "rate_limit_exceeded", "RateLimited", true)]
    [InlineData(400, "content_policy_violation", "PolicyRejected", true)]
    [InlineData(400, "unsupported", "UnsupportedCapability", true)]
    [InlineData(401, "authentication", "Configuration", true)]
    [InlineData(503, "unavailable", "UnknownUsage", false)]
    public async Task Provider_errors_are_normalized_without_raw_provider_error_text(int status, string code, string expected, bool known)
    {
        var handler = new Handler((HttpStatusCode)status, JsonSerializer.Serialize(new { error = new { code, message = "sensitive provider message" } }));
        var result = await new OpenAiTextProvider(new HttpClient(handler), new Secrets()).GenerateAsync(Call(), CancellationToken.None);
        Assert.Equal(expected, result.Outcome); Assert.Equal(known, result.Usage != null); Assert.Null(result.Content); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Stream_maps_usage_and_refusal_and_reports_missing_final_usage()
    {
        var data = "data: {\"id\":\"stream-contract\",\"choices\":[{\"delta\":{\"content\":\"text\"}}]}\n\n" +
            "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":9,\"completion_tokens\":3}}\n\ndata: [DONE]\n";
        var handler = new Handler(HttpStatusCode.OK, data); var list = new List<AiStreamEvent>();
        await foreach (var e in new OpenAiTextProvider(new HttpClient(handler), new Secrets()).StreamAsync(Call(true), CancellationToken.None)) list.Add(e);
        Assert.Equal("text", list.First().Delta); Assert.Equal(new AiUsage(9, 0, 3), list.Last().Result!.Usage);
        var broken = new Handler(HttpStatusCode.OK, "data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n");
        list.Clear(); await foreach (var e in new OpenAiTextProvider(new HttpClient(broken), new Secrets()).StreamAsync(Call(true), CancellationToken.None)) list.Add(e);
        Assert.Equal("UnknownUsage", list.Last().Result!.Outcome); Assert.Null(list.Last().Result!.Usage);
    }
    [Fact]
    public async Task Behaviorally_independent_fixtures_have_distinct_output_and_usage()
    {
        var call = Call() with { Settings = SqlAiTests.Settings(), SecretRef = null }; var a = await new FixtureTextA().GenerateAsync(call, CancellationToken.None);
        var b = await new FixtureTextB().GenerateAsync(call with { Settings = SqlAiTests.Settings("fixture.text.b") }, CancellationToken.None);
        Assert.NotEqual(a.Content, b.Content); Assert.NotEqual(a.Usage, b.Usage);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FixtureTextA().GenerateAsync(call, new CancellationToken(true)));
    }
}
