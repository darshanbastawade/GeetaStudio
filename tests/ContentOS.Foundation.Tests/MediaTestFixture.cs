using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class MediaTestFixture
{
    internal static ServiceProvider Services()
    {
        using var basis = RegistryTestFixture.Services(); var original = basis.GetRequiredService<IConfiguration>(); var config = new ConfigurationBuilder().AddConfiguration(original).AddInMemoryCollection(new Dictionary<string, string?> { ["Assets:Stores:archive"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "m16-archive"), ["Media:ProviderSpool"] = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "m16-provider-spool") }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(config); services.AddContentRuntime(config, true); services.AddContentCore(config); services.AddLessons(); services.AddUniverseRegistry(); services.AddMediaLibrary();
        services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationA>(); services.AddSingleton<ITranslationAdapter, ReviewedMemoryTranslationB>(); services.AddSingleton<IMediaFileInspector, MediaFileInspector>();
        services.AddSingleton<IImageGenerationProvider, ReferenceImageProvider>(); services.AddSingleton<IImageGenerationProvider, BasicImageProvider>(); services.AddSingleton<IImageGenerationProvider, UnknownImageFixtureProvider>(); services.AddSingleton<ISpeechSynthesisProvider, ToneSpeechFixtureProvider>(); services.AddSingleton<ISpeechSynthesisProvider>(_ => new WindowsSpeechProvider(config["Media:ProviderSpool"]!)); return services.BuildServiceProvider();
    }
    internal static MediaLicense License => new("Fixture", "fixture:M16-original-placeholder-not-public", [], false, true, "Controlled media fixture; human identity/quality review pending.");
    internal static ImportMedia Upload(RegistryTestFixture.Fixture f, RegistryCharacterView c, string key, byte[] bytes, string kind = "CharacterSheet", int version = 1) => new(Guid.NewGuid(), key, version, kind == "Voice" ? "voice.wav" : "sheet.png", "Controlled " + kind + " for " + c.Profile.Rules.Name + "; placeholder preview, human quality pending.", kind, bytes, MediaRules.Hash(bytes), License, f.Audience, c.Profile.Rules.Locales[0], c.Profile.Id, []);
    internal static async Task Review(IServiceProvider s, RegistryTestFixture.Fixture f, MediaVersionView v)
    { await s.GetRequiredService<IMediaLibrary>().ReviewAsync(f.Owner, f.Universe, v.Id, new(Guid.NewGuid(), v.RowVersion, "Approve", "Fixture", "fixture:M16-technical-quality-only", []), default); }
    internal static async Task<Guid> Profile(IServiceProvider s, RegistryTestFixture.Fixture f, string adapter = "media.image.reference.v1", string kind = "Image", string locale = "en-IN", string resource = "fixture-resource")
    { return await s.GetRequiredService<IMediaLibrary>().ProfileAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "profile-" + Guid.NewGuid(), 1, adapter, "local:fixture-account", kind, locale, resource, 0, "INR", 0, "Fixture", "fixture:M16-profile"), default); }
    internal static async Task Drain(IServiceProvider s) { var library = s.GetRequiredService<IMediaLibrary>(); for (var i = 0; i < 20; i++) if (!await library.ProcessNextAsync("m16-fixture-worker", default)) return; throw new InvalidOperationException("Media queue failed to drain."); }
    internal static async Task<CharacterMediaView> Package(IServiceProvider s, RegistryTestFixture.Fixture f, RegistryCharacterView c, MediaVersionView image, MediaVersionView voice, string key = "guest-media", string voiceKey = "owned-recording.v1", string? voiceResource = null)
    {
        var library = s.GetRequiredService<IMediaLibrary>(); var id = await library.PackageAsync(f.Owner, f.Universe, new(Guid.NewGuid(), key, 1, new(c.Profile.Id, f.Audience, c.Profile.Rules.Locales[0], "Original teacher presentation", c.Profile.Rules.VisualAnchor, c.Profile.Rules.VoiceAnchor, ["#245441", "#F8F8F5"], ["Preserve identity anchors; original fiction", "No implied scripture authority"], ["Front", "Side"], ["Community classroom"], [], image.Id, voice.Id, [], voiceKey, voiceResource ?? voice.Id.ToString("D"))), default);
        var p = (await library.PackagesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == id); await library.ReviewPackageAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), p.RowVersion, "Approve", "Fixture", "fixture:M16-package-review-not-human-release", []), default); return (await library.PackagesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == id);
    }
}
