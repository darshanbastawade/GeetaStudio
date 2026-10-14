using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class SocialTestFixture
{
    internal static ServiceProvider Services(Action<IServiceCollection>? configure = null) => WatchTestFixture.Services(services =>
    {
        services.AddSingleton<ISocialCapabilityCatalog, SocialChannelProfiles>(); services.AddSocialPublishing(); foreach (var key in new[] { "social.fixture.a.v1", "social.fixture.b.v1", "social.unknown.v1", "social.rate-limit.v1" }) services.AddSingleton<ISocialPublisher>(s => new SocialFixturePublisher(key, s.GetRequiredService<ISocialEffectLedger>())); configure?.Invoke(services);
    });
    internal static async Task<Guid> Account(IServiceProvider s, EditorialTestFixture.Fixture f, string platform = "X", string adapter = "social.fixture.a.v1", int version = 1, string key = "account")
    { var c = SocialChannelProfiles.Catalog.Single(x => x.Platform == platform); return await s.GetRequiredService<ISocialPublishing>().RegisterAccountAsync(f.Owner, f.Universe, new(Guid.NewGuid(), key, version, platform, "fixture:" + platform.ToLowerInvariant(), adapter, "Fixture", c.Key, c.RequiredScopes, null, "fixture:owned-social-test"), default); }
    internal static async Task<SocialPackageView> Package(IServiceProvider s, EditorialTestFixture.Fixture f, EditorialPacket packet, Guid account, string key = "package", string format = "Link", Guid[]? images = null, Guid? production = null)
    {
        var service = s.GetRequiredService<ISocialPublishing>(); var p = await service.PrepareAsync(f.Owner, f.Universe, new(Guid.NewGuid(), key, packet.Id, account, format, "fixture-campaign", "", format == "Link" ? "https://example.invalid/fixture" : "", images ?? [], production), default);
        await service.ReviewAsync(f.Owner, f.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, p.PayloadHash, "Approve", "Fixture", "fixture:technical-package-review"), default); return (await service.PackagesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == p.Id);
    }
    internal static async Task<SocialPublicationView> Drain(IServiceProvider s, EditorialTestFixture.Fixture f, Guid id)
    { var service = s.GetRequiredService<ISocialPublishing>(); for (var i = 0; i < 100; i++) { var p = (await service.PublicationsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == id); if (p.State is "Published" or "Unknown" or "Failed" or "Withdrawn") return p; await service.ProcessNextAsync(default); await Task.Delay(30); } throw new InvalidOperationException("Social queue did not settle."); }
}
