using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class CreationTestFixture
{
    internal static ServiceProvider Services(ICreationRenderer? replacement = null, Action<IServiceCollection>? configure = null) => WatchTestFixture.Services(s =>
    {
        s.AddCustomerWorkbench(); s.AddCreationStudio(); s.AddSingleton<IIdentityProvider, HostIdentityProvider>(); s.AddSingleton<INotificationProvider, InboxNotificationProvider>(); s.AddSingleton<ICreationRenderer>(replacement ?? new CreationRenderer()); s.AddSingleton<ICreationRenderer>(new CreationRenderer("create.local.b.v1"));
        configure?.Invoke(s);
    });
    internal sealed record Fixture(EditorialTestFixture.Fixture Editorial, CustomerScope Owner, Guid User, Guid Profile, Guid Household, Guid Template, Guid Snapshot);
    internal static async Task<Fixture> Setup(IServiceProvider s, string adapter = "create.local.a.v1", string format = "LearningPack", string? actor = null)
    {
        var f = await EditorialTestFixture.Setup(s, actor ?? "m23-" + Guid.NewGuid()); var w = s.GetRequiredService<ICustomerWorkbench>(); var owner = CustomerTestFixture.Scope(f.Owner.TenantId, f.Owner.Actor);
        var user = await w.RegisterAsync(owner, new(Guid.NewGuid(), "Controlled adult", true), default); var household = await w.HouseholdAsync(owner, new(Guid.NewGuid(), "Controlled family"), default); var profile = await w.ProfileAsync(owner, new(Guid.NewGuid(), household, "Controlled learner", "Child", "en-IN"), default);
        var c = new CustomerTestFixture.Fixture(f.Owner.TenantId, f.Universe, f.Owner, owner, user, household, profile); var db = s.GetRequiredService<CoreDbContext>(); var audience = await db.Audiences.SingleAsync(x => x.Id == f.Audience); audience.MinAge = 5; audience.MaxAge = 12; if (!await db.Locales.AnyAsync(x => x.Id == "sa-IN")) db.Locales.Add(new() { Id = "sa-IN", Name = "Sanskrit" }); await db.SaveChangesAsync();
        var text = await TalkCorpusFixture.FirstVerse(s, c); var snapshot = await db.CorpusTexts.AsNoTracking().Where(x => x.Id == text).Select(x => x.SnapshotId).SingleAsync();
        var editorial = s.GetRequiredService<IEditorialWorkbench>(); var brief = EditorialTestFixture.Request(f); var exactText = await db.CorpusTexts.AsNoTracking().Where(x => x.Id == text).Select(x => x.ExactText).SingleAsync(); var packet = await editorial.StartAsync(f.Owner, f.Universe, brief with { Draft = brief.Draft! with { Body = "Fictional learner framing: An original learner pauses before choosing.\nAttributed translation, Besant discourse 1 verse 1:\n" + exactText } }, default);
        db.Provenance.Add(new() { Id = Guid.NewGuid(), TenantId = f.Owner.TenantId, ContentRevisionId = packet.RevisionId, SnapshotId = snapshot, Relation = "Reference" }); await db.SaveChangesAsync(); packet = await EditorialTestFixture.Approved(s, f, await editorial.InspectAsync(f.Owner, f.Universe, packet.Id, default));
        var character = await RegistryTestFixture.Character(s, new(f.Owner, f.Universe, f.Audience, f.Bible, f.Episode), "fixture-create-mentor", "Mentor", "Recurring");
        var template = await s.GetRequiredService<ICreationStudio>().TemplateAsync(f.Owner, new(Guid.NewGuid(), f.Universe, "fixture-" + format.ToLowerInvariant(), 1, format, packet.Id, packet.RevisionId, ["Child"], ["patience", "kindness"], adapter, 2, "fixture:technical-template-not-human-review", [], CharacterProfileRevisionId: character.Profile.Id), default);
        await w.GrantAsync(f.Owner, new(Guid.NewGuid(), user, 10, DateTimeOffset.UtcNow.AddDays(1), "fixture:M23-controlled-credits"), default); return new(f, owner, user, profile, household, template, snapshot);
    }
    internal static async Task<CreationView> Drain(IServiceProvider s, Fixture f, Guid id)
    {
        var w = s.GetRequiredService<ICreationStudio>(); for (var i = 0; i < 100; i++) { var row = await w.InspectAsync(f.Owner, id, default); if (row.State is "Ready" or "Failed" or "Deleted") return row; await w.ProcessNextAsync(default); }
        throw new InvalidOperationException("Creation queue did not finish.");
    }
}
