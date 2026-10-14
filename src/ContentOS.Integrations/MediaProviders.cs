using System.Buffers.Binary;
using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

public sealed class ReferenceImageProvider : IImageGenerationProvider
{
    public string Key => "media.image.reference.v1";
    public MediaProviderCapabilities Capabilities => new("Image", ["en-IN", "en-US", "hi-IN", "mr-IN"], true, false, false, true, true, "OfflineFixture", ["ControlledReferenceReuseOrProceduralCard", "NotPhotorealisticGeneration", "NoLiveNetwork"]);
    public Task<MediaProviderResult> SubmitAsync(MediaProviderRequest r, CancellationToken ct) { ct.ThrowIfCancellationRequested(); var bytes = r.ReferenceImages.Length > 0 ? r.ReferenceImages[0] : MediaFiles.ReferenceCard(r.PromptOrText, r.Palette); MediaFiles.Inspect(bytes); return Task.FromResult(new MediaProviderResult("Completed", "local-image:" + r.OperationId.ToString("N"), bytes, 0)); }
    public Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest r, string? resource, CancellationToken ct) => SubmitAsync(r, ct);
}
public sealed class BasicImageProvider : IImageGenerationProvider
{
    public string Key => "media.image.basic.v1";
    public MediaProviderCapabilities Capabilities => new("Image", ["en-IN", "en-US"], false, false, false, true, true, "OfflineFixture", ["ControlledProceduralCardOnly", "ReferenceImagesUnsupported", "NoLiveNetwork"]);
    public Task<MediaProviderResult> SubmitAsync(MediaProviderRequest r, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(r.ReferenceImages.Length > 0 ? new MediaProviderResult("Unsupported", null, null, 0, "CharacterReferenceUnsupported") : new MediaProviderResult("Completed", "local-basic:" + r.OperationId.ToString("N"), MediaFiles.ReferenceCard(r.PromptOrText, ["#843B62"]), 0)); }
    public Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest r, string? resource, CancellationToken ct) => SubmitAsync(r, ct);
}
public sealed class UnknownImageFixtureProvider : IImageGenerationProvider
{
    public string Key => "media.image.unknown-fixture.v1";
    public MediaProviderCapabilities Capabilities => new("Image", ["en-IN", "en-US"], true, false, false, true, true, "OfflineFixture", ["ControlledUnknownOutcomeThenReconciliation", "NoLiveNetwork"]);
    public Task<MediaProviderResult> SubmitAsync(MediaProviderRequest r, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(new MediaProviderResult("Unknown", "local-unknown:" + r.OperationId.ToString("N"), null, null, "ControlledTransportAmbiguity")); }
    public Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest r, string? resource, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(new MediaProviderResult("Completed", "local-unknown:" + r.OperationId.ToString("N"), r.ReferenceImages.Length > 0 ? r.ReferenceImages[0] : MediaFiles.ReferenceCard(r.PromptOrText, []), 0)); }
}
public sealed class ToneSpeechFixtureProvider : ISpeechSynthesisProvider
{
    public string Key => "media.speech.tone-fixture.v1";
    public MediaProviderCapabilities Capabilities => new("Speech", ["en-IN", "en-US"], false, false, false, true, true, "OfflineFixture", ["FixtureToneNotSpokenNarration", "PronunciationAndCloningUnsupported", "NoLiveNetwork"]);
    public static byte[] Tone()
    {
        const int n = 4000, rate = 16000; var b = new byte[44 + n * 2]; System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(b, 0); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4), b.Length - 8); System.Text.Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(b, 8); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(16), 16); BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(20), 1); BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(22), 1); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(24), rate); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(28), rate * 2); BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(32), 2); BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(34), 16); System.Text.Encoding.ASCII.GetBytes("data").CopyTo(b, 36); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(40), n * 2); for (var i = 0; i < n; i++) BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(44 + i * 2), (short)(Math.Sin(i * 2 * Math.PI * 440 / rate) * 2000)); return b;
    }
    public Task<MediaProviderResult> SubmitAsync(MediaProviderRequest r, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(r.Pronunciation.Length > 0 ? new MediaProviderResult("Unsupported", null, null, 0, "FixtureToneCannotQualifyPronunciation") : new MediaProviderResult("Completed", "local-tone:" + r.OperationId.ToString("N"), Tone(), 0, "FixtureToneNotSpokenNarration")); }
    public Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest r, string? resource, CancellationToken ct) => SubmitAsync(r, ct);
}
