using System.Security.Cryptography;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class LessonEngine
{
    public async Task<IReadOnlyList<LessonProfileView>> ProposeDefaultsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, true, ct);
        Guid Op(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(s.TenantId + ":" + u + ":M09.proposed.v1:" + key))[..16]);
        var profiles = new List<RegisterLessonProfile>();
        foreach (var seed in new[] {
            (Key:"children", Min:8, Max:14, Reading:"Short concrete sentences", Tone:"Warm and reassuring", Setting:"a playground", Conflict:"One everyday disagreement", Stage:"Child",
                Situation:"Two friends pause before deciding whose turn comes next. Understand the context first.",
                Example:"Look at each friend's turn before choosing a fair next step.", Action:"Pause, name the situation, and choose one fair action.", Reflection:"What would you check before acting?"),
            (Key:"youth", Min:15, Max:23, Reading:"Reflective accessible prose", Tone:"Respectful and exploratory", Setting:"a school or college team", Conflict:"A choice with competing goals", Stage:"Teen",
                Situation:"A team faces disagreement over a shared task. Understand the context before defending your position.",
                Example:"Separate what actually happened from assumptions about a teammate's intentions.", Action:"List the facts and assumptions, then explain your next step.", Reflection:"How might another person's view change your decision?"),
            (Key:"adult", Min:24, Max:120, Reading:"Nuanced practical prose", Tone:"Calm and direct", Setting:"a workplace", Conflict:"Responsibilities with uncertain consequences", Stage:"Adult",
                Situation:"A work disagreement involves incomplete information and several responsibilities. Understand the context before responding.",
                Example:"Compare obligations and available facts without mistaking an assumption for evidence.", Action:"Identify uncertainty, ask one clarifying question, and select a proportionate action.", Reflection:"Which assumption would most change your decision?")
        })
        {
            var audienceId = await Command(s, u, Op("catalog-" + seed.Key), "audience-catalog", new { seed.Key, seed.Min, seed.Max }, async () =>
            {
                var old = await db.Audiences.SingleOrDefaultAsync(x => x.UniverseId == u && x.Key == "m09-" + seed.Key, ct);
                if (old != null) return old.Id;
                var a = new AudienceProfile { Id = Guid.NewGuid(), UniverseId = u, Key = "m09-" + seed.Key, Name = seed.Key + " proposed writing audience", MinAge = seed.Min, MaxAge = seed.Max };
                db.Audiences.Add(a); return a.Id;
            }, ct);
            var audience = new RegisterLessonProfile(Op(seed.Key), seed.Key, 1, "Audience", Audience: new(audienceId, seed.Min, seed.Max, seed.Reading,
                ["context", "evidence", "fairness"], 6000, seed.Tone, ["gambling", "graphic violence", "sexual content", "behavioral advertising"],
                ["understand-context"], seed.Situation, seed.Example, seed.Action, seed.Reflection, new()));
            var aid = await RegisterProfileAsync(s, u, audience, ct); profiles.Add(audience);
            if (seed.Key == "youth")
            {
                foreach (var band in new[] { (Key: "writing-15-18", Min: 15, Max: 18, Stage: "Teen", Setting: "school and family life"), (Key: "writing-19-23", Min: 19, Max: 23, Stage: "YoungAdult", Setting: "college and the transition to work") })
                    profiles.Add(new(Op(band.Key), band.Key, 1, "Writing", Writing: new(aid, band.Min, band.Max, [], [band.Stage], ["protagonist"], ["peer", "mentor"], band.Setting, seed.Conflict, "Gentle situational humor")));
            }
            else profiles.Add(new(Op("writing-" + seed.Key), "writing-" + seed.Key, 1, "Writing", Writing: new(aid, seed.Min, seed.Max, [], [seed.Stage], ["protagonist"], ["peer", "mentor"], seed.Setting, seed.Conflict, "Gentle situational humor")));
        }
        foreach (var locale in new[] { (Code: "en-IN", Word: "context"), (Code: "mr-IN", Word: "संदर्भ"), (Code: "hi-IN", Word: "संदर्भ") })
            profiles.Add(new(Op(locale.Code), locale.Code, 1, "Locale", Locale: new(locale.Code, "en-IN", [new("context", locale.Word, "context", "Human pronunciation review required")],
                "Keep canonical Sanskrit and source anchors unchanged; use reviewed transliteration entries only.", "Keep names unchanged; pronunciation is proposed until reviewed.", ["context"], [])));
        profiles.Add(new(Op("translation-a"), "translation-a", 1, "TranslationRoute", Route: new("reviewed-memory.a.v1")));
        profiles.Add(new(Op("translation-b"), "translation-b", 1, "TranslationRoute", Route: new("reviewed-memory.b.v1")));
        foreach (var p in profiles.Where(x => x.Kind != "Audience")) await RegisterProfileAsync(s, u, p, ct);
        return await ProfilesAsync(s, u, ct); // Every seed stays Draft; catalog presence never qualifies a language.
    }
}
