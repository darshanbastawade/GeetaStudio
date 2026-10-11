using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

internal static class RegistryTestFixture
{
    internal sealed record Fixture(CoreScope Owner, Guid Universe, Guid Audience, Guid Bible, Guid Episode);
    internal static ServiceProvider Services() => LessonTestFixture.ServicesWithRegistry();
    internal static UniverseBibleRules Bible(Guid audience, string title = "Fixture universe") => new(title, "Original fictional teamwork",
        ["Keep canonical identity", "Original dialogue is not scripture"], ["None", "Gentle", "Satire"], true, [new(audience, true, true, "Respectful")], ["real-suffering"],
        ["laugh at disability", "joke about victims"], ["scripture says", "Krishna said:"], "Fictional satire — editorial review");
    internal static RegistryCharacterRules Rules(Fixture f, string name = "Fixture learner", string role = "Lead", string stage = "Child") => new(name, stage, "Original fictional profile; not a historical or scripture speaker.", "Fictional",
        ["curious", "generous"], ["understand context"], ["impatient"], "Respectful", [role], ["teamwork"], ["reflection"], ["understand-context"], [f.Audience], ["en-IN"],
        ["original-timeline"], "Learning to pause", "Fixture pronunciation; human review pending", name + "-visual", name + "-voice", []);
    internal static async Task<Fixture> Setup(IServiceProvider s, string actor = "m10-owner")
    {
        var db = s.GetRequiredService<CoreDbContext>(); var tenant = Guid.NewGuid(); var u = Guid.NewGuid(); var org = Guid.NewGuid(); var audience = Guid.NewGuid();
        db.Organizations.Add(new() { Id = org, Name = "M10 fixture" }); db.Tenants.Add(new() { Id = tenant, OrganizationId = org, Key = "m10-" + tenant, Name = "M10 isolated fixture" });
        db.Universes.Add(new() { Id = u, TenantId = tenant, Key = "fixture", Name = "M10 original fiction fixture" }); db.Audiences.Add(new() { Id = audience, UniverseId = u, Key = "children", Name = "Fixture audience", MinAge = 8, MaxAge = 14 });
        foreach (var name in new[] { actor, "integration-test-Administrator" }.Distinct()) db.Members.Add(new() { TenantId = tenant, Actor = name, Permission = "Administrator" });
        foreach (var name in new[] { "m10-reader", "integration-test-Reader" }) db.Members.Add(new() { TenantId = tenant, Actor = name, Permission = "Reader" });
        if (!await db.Locales.AnyAsync(x => x.Id == "en-IN")) db.Locales.Add(new() { Id = "en-IN", Name = "English" });
        await db.SaveChangesAsync(); var owner = new CoreScope(tenant, actor); var registry = s.GetRequiredService<IUniverseRegistry>();
        var bible = await registry.RegisterBibleAsync(owner, u, new(Guid.NewGuid(), "bible", 1, Bible(audience)), default);
        await Review(s, owner, u, bible);
        var episode = await registry.RegisterEpisodeAsync(owner, u, new(Guid.NewGuid(), "teamwork"), default); return new(owner, u, audience, bible, episode);
    }
    internal static async Task Review(IServiceProvider s, CoreScope owner, Guid u, Guid id, string? escalation = null)
    {
        var db = s.GetRequiredService<CoreDbContext>(); var row = await db.RegistryRevisions.AsNoTracking().SingleAsync(x => x.Id == id);
        await s.GetRequiredService<IUniverseRegistry>().ReviewAsync(owner, u, id, new(Guid.NewGuid(), Convert.ToBase64String(row.RowVersion), true, "fixture:registry-behavior-not-human-qualification", escalation), default);
    }
    internal static async Task<RegistryCharacterView> Character(IServiceProvider s, Fixture f, string key, string role = "Lead", string scope = "Guest", bool active = true)
    {
        var registry = s.GetRequiredService<IUniverseRegistry>(); var rules = Rules(f, key, role, role == "Lead" ? "Child" : "Adult");
        if (scope == "EpisodeOnly") rules = rules with { EpisodeId = f.Episode };
        var c = await registry.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), key, scope, rules), default);
        if (active) { await Review(s, f.Owner, f.Universe, c.Profile.Id); await registry.ControlCharacterAsync(f.Owner, f.Universe, c.CharacterId, new(Guid.NewGuid(), c.RowVersion, "Activate", "fixture:suitable-identity"), default); }
        return (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single(x => x.CharacterId == c.CharacterId);
    }
    internal static CastingBrief Brief(Fixture f, string[]? roles = null) => new(f.Bible, f.Episode, f.Audience, "en-IN", ["teamwork"], ["reflection"], ["understand-context"], roles ?? ["Lead"], ["original-timeline"], [], new(4, 0, "INR"), []);
    internal static async Task<RegistryRevisionView<RegistryEpisodeSnapshot>> Story(IServiceProvider s, Fixture f, CastingBrief? brief = null)
    {
        var registry = s.GetRequiredService<IUniverseRegistry>(); var b = brief ?? Brief(f); var plan = await registry.CastAsync(f.Owner, f.Universe, b, default);
        var episode = (await registry.EpisodesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == b.EpisodeId);
        return await registry.ComposeAsync(f.Owner, f.Universe, new(Guid.NewGuid(), episode.RowVersion, b, "None",
            plan.Selected.Select(c => new RegistryStoryBeat(c.Character.CharacterId, "FictionalDialogue", "I pause and choose my next action.", ["curious"], [], c.Role == "Lead")).ToArray(), []), default);
    }
}
