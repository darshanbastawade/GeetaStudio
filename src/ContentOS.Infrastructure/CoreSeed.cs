using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed record CoreFixture(int SchemaVersion, LocaleFixture[] Locales, UniverseFixture[] Universes);
public sealed record LocaleFixture(string Id, string Name);
public sealed record UniverseFixture(string Key, string Name, string TenantKey, Dictionary<string, string> Terminology,
    string[] SubjectTypes, Dictionary<string, string> SourceRules, Dictionary<string, string> EditorialPolicies,
    AudienceFixture[] Audiences, SubjectFixture[] Subjects, CharacterFixture[] Characters);
public sealed record AudienceFixture(string Key, string Name, int MinAge, int MaxAge);
public sealed record SubjectFixture(string Key, string Type, string Name);
public sealed record CharacterFixture(string Key, string DisplayName, string LifeStage, string Biography, string[] Aliases);

public static class CoreSeed
{
    public static Guid StableId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("ContentOS.M02.fixture.v1:" + key)).AsSpan(0, 16));
    public static async Task SeedAsync(CoreDbContext db, string file, string administrator, CancellationToken ct = default)
    {
        var fixture = JsonSerializer.Deserialize<CoreFixture>(await File.ReadAllTextAsync(file, ct), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new RuntimeValidationException("Missing fixture.");
        if (fixture.SchemaVersion != 1 || fixture.Universes.Length < 2) throw new RuntimeValidationException("Unsupported fixture manifest.");
        CoreRules.Text(administrator, 200, "Seed administrator");
        foreach (var u in fixture.Universes)
        {
            CoreRules.Text(u.Key, 100, "Universe key"); CoreRules.Text(u.Name, 200, "Universe name");
            if (u.TenantKey is not ("local" or "isolated") || u.Subjects.Any(s => !u.SubjectTypes.Contains(s.Type)) ||
                u.Audiences.Any(a => a.MinAge < 0 || a.MaxAge < a.MinAge || a.MaxAge > 120))
                throw new RuntimeValidationException("Invalid fixture relationships or audience range.");
        }
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        foreach (var key in fixture.Universes.Select(u => u.TenantKey).Distinct())
        {
            var org = await db.Organizations.SingleOrDefaultAsync(o => o.Id == StableId("organization:" + key), ct);
            if (org is null) { org = new Organization { Id = StableId("organization:" + key), Name = key + " fixture organization" }; db.Organizations.Add(org); }
            var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Key == "m02-" + key, ct);
            if (tenant is null) { tenant = new Tenant { Id = StableId("tenant:" + key), OrganizationId = org.Id, Key = "m02-" + key, Name = key + " development tenant" }; db.Tenants.Add(tenant); }
            if (key == "local" && !await db.Members.AnyAsync(m => m.TenantId == tenant.Id && m.Actor == administrator, ct))
                db.Members.Add(new TenantMember { TenantId = tenant.Id, Actor = administrator, Permission = "Administrator" });
        }
        foreach (var l in fixture.Locales)
            if (!await db.Locales.AnyAsync(x => x.Id == l.Id, ct)) db.Locales.Add(new ContentLocale { Id = l.Id, Name = l.Name });
        await db.SaveChangesAsync(ct);
        foreach (var u in fixture.Universes)
        {
            var tenant = await db.Tenants.SingleAsync(t => t.Key == "m02-" + u.TenantKey, ct);
            var universe = await db.Universes.SingleOrDefaultAsync(x => x.TenantId == tenant.Id && x.Key == u.Key, ct);
            if (universe is null) { universe = new Universe { Id = StableId("universe:" + u.Key), TenantId = tenant.Id, Key = u.Key, Name = u.Name }; db.Universes.Add(universe); }
            var pack = await db.Packs.SingleOrDefaultAsync(p => p.UniverseId == universe.Id && p.Version == 1, ct);
            if (pack is not null) continue; // Preserve every existing pack, profile and identity; reruns never overwrite edits.
            pack = new UniversePack { Id = StableId("pack:" + u.Key), UniverseId = universe.Id }; db.Packs.Add(pack);
            foreach (var pair in u.Terminology) db.Terms.Add(new PackTerm { PackId = pack.Id, Key = pair.Key, Value = pair.Value });
            foreach (var key in u.SubjectTypes) db.SubjectTypes.Add(new PackSubjectType { PackId = pack.Id, Key = key });
            foreach (var pair in u.SourceRules) db.SourceRules.Add(new SourceRule { PackId = pack.Id, Kind = pair.Key, Rule = pair.Value });
            foreach (var pair in u.EditorialPolicies) db.Policies.Add(new EditorialPolicy { PackId = pack.Id, Key = pair.Key, Rule = pair.Value });
            foreach (var a in u.Audiences)
            {
                var audience = await db.Audiences.SingleOrDefaultAsync(x => x.UniverseId == universe.Id && x.Key == a.Key, ct);
                if (audience is null) { audience = new AudienceProfile { Id = StableId("audience:" + u.Key + ":" + a.Key), UniverseId = universe.Id, Key = a.Key, Name = a.Name, MinAge = a.MinAge, MaxAge = a.MaxAge }; db.Audiences.Add(audience); }
                db.PackAudiences.Add(new PackAudience { PackId = pack.Id, AudienceId = audience.Id });
            }
            foreach (var subject in u.Subjects)
                if (!await db.Subjects.AnyAsync(x => x.UniverseId == universe.Id && x.Key == subject.Key, ct))
                    db.Subjects.Add(new Subject { Id = StableId("subject:" + u.Key + ":" + subject.Key), UniverseId = universe.Id, Key = subject.Key, Type = subject.Type, Name = subject.Name });
            foreach (var c in u.Characters)
            {
                // Resolve prior keys/aliases before assigning an identity; normalization never replaces a stored CharacterId.
                var character = await db.Characters.SingleOrDefaultAsync(x => x.UniverseId == universe.Id &&
                    (x.Key == c.Key || db.Aliases.Any(a => a.CharacterId == x.Id && c.Aliases.Contains(a.Name)) ||
                     db.CharacterRevisions.Any(r => r.CharacterId == x.Id && c.Aliases.Contains(r.DisplayName))), ct);
                if (character is null) { character = new Character { Id = StableId("character:" + u.Key + ":" + c.Key), UniverseId = universe.Id, Key = c.Key }; db.Characters.Add(character); }
                var previous = await db.CharacterRevisions.Where(r => r.CharacterId == character.Id).OrderByDescending(r => r.Version).FirstOrDefaultAsync(ct);
                if (previous is null || (c.Aliases.Contains(previous.DisplayName) && previous.DisplayName != c.DisplayName))
                    db.CharacterRevisions.Add(new CharacterRevision
                    {
                        Id = StableId("character-revision:" + character.Id + ":" + ((previous?.Version ?? 0) + 1)),
                        CharacterId = character.Id,
                        Version = (previous?.Version ?? 0) + 1,
                        DisplayName = c.DisplayName,
                        LifeStage = previous?.LifeStage ?? c.LifeStage,
                        Biography = previous?.Biography ?? c.Biography,
                        FictionalStatus = previous?.FictionalStatus ?? "Fictional"
                    });
                foreach (var alias in c.Aliases)
                    if (!await db.Aliases.AnyAsync(a => a.UniverseId == universe.Id && a.Name == alias, ct))
                        db.Aliases.Add(new CharacterAlias { UniverseId = universe.Id, CharacterId = character.Id, Name = alias });
                db.PackCharacters.Add(new PackCharacter { PackId = pack.Id, CharacterId = character.Id });
            }
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
