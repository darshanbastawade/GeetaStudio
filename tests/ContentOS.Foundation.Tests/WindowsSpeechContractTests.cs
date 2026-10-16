using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Integrations;
namespace ContentOS.Foundation.Tests;

public sealed class WindowsSpeechContractTests
{
    private sealed class Engine : IWindowsSpeechEngine
    {
        public SpeechVoiceResource[] Catalog = [new("controlled-voice", ["en-US"])]; public int Calls;
        public Func<MediaProviderRequest, string, CancellationToken, Task>? Work;
        public SpeechVoiceResource[] Voices() => Catalog;
        public Task SynthesizeAsync(MediaProviderRequest r, string path, CancellationToken ct) { Calls++; return Work?.Invoke(r, path, ct) ?? File.WriteAllBytesAsync(path, ToneSpeechFixtureProvider.Tone(), ct); }
    }
    private static string Root() => Path.Combine(Path.GetTempPath(), "GeetaStudio-SpeechContracts", Guid.NewGuid().ToString("N"));
    private static MediaProviderRequest Request() => new(Guid.NewGuid(), "local:controlled", "controlled-voice", "en-US", "Controlled text, no human voice qualification.", [], [], [], 1048576, 0);
    [Fact]
    public async Task Legacy_bytes_without_receipt_are_not_adopted_or_regenerated()
    {
        var root = Root(); Directory.CreateDirectory(root); var r = Request(); var e = new Engine();
        var binding = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(r.AccountReference + "|" + r.ResourceKey)))[..16];
        await File.WriteAllBytesAsync(Path.Combine(root, binding + "-" + r.OperationId.ToString("N") + ".wav"), ToneSpeechFixtureProvider.Tone());
        var provider = new WindowsSpeechProvider(root, e); Assert.Equal("Unknown", (await provider.SubmitAsync(r, default)).State); Assert.Equal("Unknown", (await provider.ReconcileAsync(r, null, default)).State); Assert.Equal(0, e.Calls);
    }
    [Fact]
    public async Task Oversized_output_is_rejected_and_an_interrupted_operation_is_not_retried()
    {
        var e = new Engine(); var p = new WindowsSpeechProvider(Root(), e); var r = Request() with { MaxBytes = 128 };
        await Assert.ThrowsAsync<RuntimeConflictException>(() => p.SubmitAsync(r, default));
        Assert.Equal("Unknown", (await p.SubmitAsync(r, default)).State); Assert.Equal(1, e.Calls);
    }
    [Fact]
    public async Task Accepted_receipt_survives_restart_without_synthesis_or_locale_fallback()
    {
        var e = new Engine(); var root = Root(); var r = Request(); var p = new WindowsSpeechProvider(root, e); var first = await p.SubmitAsync(r, default); Assert.Equal("Completed", first.State); Assert.Equal(1, e.Calls);
        e.Catalog = []; var restart = new WindowsSpeechProvider(root, e);
        Assert.Equal(first.Bytes, (await restart.ReconcileAsync(r, first.ResourceId, default)).Bytes); Assert.Equal(first.Bytes, (await restart.SubmitAsync(r, default)).Bytes); Assert.Equal(1, e.Calls);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => restart.ReconcileAsync(r, "different-resource", default));
        var file = Directory.GetFiles(root, "*.wav").Single(); var corrupted = first.Bytes!.ToArray(); corrupted[^1] ^= 1; await File.WriteAllBytesAsync(file, corrupted);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => restart.ReconcileAsync(r, first.ResourceId, default)); Assert.Equal(1, e.Calls);
    }
    [Theory]
    [InlineData("text")]
    [InlineData("locale")]
    [InlineData("pronunciation")]
    [InlineData("limit")]
    public async Task Same_operation_cannot_reuse_bytes_for_changed_input(string change)
    {
        var p = new WindowsSpeechProvider(Root(), new Engine()); var r = Request(); await p.SubmitAsync(r, default);
        var changed = change switch { "text" => r with { PromptOrText = "Changed" }, "locale" => r with { Locale = "en-IN" }, "pronunciation" => r with { Pronunciation = [new("text", "teckst")] }, _ => r with { MaxBytes = 1000 } };
        await Assert.ThrowsAsync<RuntimeConflictException>(() => p.SubmitAsync(changed, default));
    }
    [Fact]
    public async Task Unknown_submission_is_not_repeated_after_cancellation_or_restart()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var e = new Engine { Work = async (_, _, ct) => { entered.SetResult(); try { await Task.Delay(Timeout.Infinite, ct); } finally { stopped.SetResult(); } } };
        var root = Root(); var p = new WindowsSpeechProvider(root, e); var r = Request(); using var cancel = new CancellationTokenSource(); var work = p.SubmitAsync(r, cancel.Token); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Unknown", (await new WindowsSpeechProvider(root, e).SubmitAsync(r, default)).State);
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work); Assert.True(stopped.Task.IsCompleted);
        var restart = new WindowsSpeechProvider(root, e); Assert.Equal("Unknown", (await restart.ReconcileAsync(r, null, default)).State); Assert.Equal("Unknown", (await restart.SubmitAsync(r, default)).State); Assert.Equal(1, e.Calls);
    }
    [Fact]
    public async Task Exact_voice_locale_and_stage_limits_are_enforced_before_dispatch()
    {
        var e = new Engine(); var p = new WindowsSpeechProvider(Root(), e); Assert.Equal(new[] { "en-US" }, p.Capabilities.Locales); Assert.DoesNotContain("en-IN", p.Capabilities.Locales);
        var profile = new RegisterMediaProfile(Guid.NewGuid(), "voice", 1, p.Key, "local:controlled", "Speech", "en-IN", "controlled-voice", 0, "INR", 0, "Fixture", "fixture:no-genuine-qualification");
        Assert.Throws<EvidenceBlockedException>(() => p.ValidateProfile(profile)); p.ValidateProfile(profile with { Locale = "en-US" });
        Assert.Equal("Unsupported", (await p.SubmitAsync(Request() with { Locale = "en-IN" }, default)).State);
        Assert.Equal("Unsupported", (await p.SubmitAsync(Request() with { ResourceKey = "missing" }, default)).State);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => p.SubmitAsync(Request() with { PromptOrText = new string('x', 161) }, default)); Assert.Equal(0, e.Calls);
    }
}
