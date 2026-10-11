using System.Security.Cryptography;
using System.Text;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class UniverseRegistry
{
    public async Task ProposeDefaultsAsync(ContentOS.Application.CoreScope s, Guid u, RegistrySeedDocument data, CancellationToken ct)
    {
        await Access(s, u, true, ct);
        if ((await db.Universes.AsNoTracking().SingleAsync(x => x.Id == u, ct)).Key != data.UniverseKey) throw new RuntimeValidationException("Seed data belongs to a different universe.");
        Guid Op(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(s.TenantId + ":" + u + ":M10.proposal.v1:" + key))[..16]);
        var audiences = await db.Audiences.AsNoTracking().Where(x => x.UniverseId == u).ToListAsync(ct);
        var bible = data.Bible with { Audiences = audiences.Select(a => new AudienceHumorRule(a.Id, true, !a.Key.Contains("children", StringComparison.Ordinal), "Editorial review of audience suitability")).ToArray() };
        await RegisterBibleAsync(s, u, new(Op("bible"), "universe-bible", 1, bible), ct);
        foreach (var seed in data.Characters)
        {
            var c = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.UniverseId == u && x.Key == seed.Key, ct);
            if (c == null) throw new RuntimeValidationException("Import the accepted universe identity seed first; existing canonical identity is never fabricated or overwritten.");
            if (await db.RegistryRevisions.AnyAsync(x => x.Kind == "Character" && x.CharacterId == c.Id, ct)) continue;
            var revision = await db.CharacterRevisions.AsNoTracking().Where(x => x.CharacterId == c.Id).OrderByDescending(x => x.Version).FirstAsync(ct);
            var eligible = audiences.Where(a => seed.AudienceKeys.Contains(a.Key)).Select(a => a.Id).ToArray();
            if (eligible.Length == 0) throw new RuntimeValidationException("Seed has no registered audience binding.");
            var aliases = await db.Aliases.Where(x => x.CharacterId == c.Id).Select(x => x.Name).ToArrayAsync(ct);
            var rules = new RegistryCharacterRules(revision.DisplayName, revision.LifeStage, revision.Biography, revision.FictionalStatus, seed.Traits, seed.Goals, seed.Flaws,
                seed.SpeechStyle, seed.Roles, seed.SituationTags, seed.SkillTags, seed.Objectives, eligible, seed.Locales, seed.ContinuityTags, seed.ArcState,
                seed.PronunciationGuide, seed.VisualAnchor, seed.VoiceAnchor, aliases, StoryDevice: seed.StoryDevice);
            // Preserve the existing scope; proposing a profile cannot promote or activate an existing identity.
            var profile = await RegisterCharacterAsync(s, u, new(Op("profile-" + seed.Key), c.Key, c.PersistenceScope, rules, c.Id, Convert.ToBase64String(c.RowVersion)), ct);
            foreach (var style in seed.PresentationStyles ?? ["Default"])
                await RegisterPresentationAsync(s, u, new(Op("presentation-" + seed.Key + "-" + style), seed.Key + "-" + style, 1,
                    new(profile.Profile.Id, eligible[0], seed.Locales[0], style, seed.VisualAnchor, seed.VoiceAnchor, seed.PronunciationGuide, null, null, 0, "INR")), ct);
        }
    }
}
