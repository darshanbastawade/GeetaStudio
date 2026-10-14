using System.Data;
using System.Diagnostics.Metrics;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ContentOS.Application.CustomerRules;
namespace ContentOS.Infrastructure;

public sealed partial class TalkWorkbench(CoreDbContext db, CustomerWorkbench customers, IAiTaskRouter ai,
    IKnowledgeRetriever retrieval, IServiceScopeFactory scopes, TalkMemory transient) : ITalkWorkbench
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions) ?? throw new RuntimeValidationException("Invalid TALK evidence JSON.");
    private static readonly Meter Meter = new("ContentOS.Talk", "1.0");
    private static readonly Counter<long> Turns = Meter.CreateCounter<long>("talk.turns");
    private static CoreScope Staff(TalkPolicy p) => new(p.TenantId, p.Actor);
    private async Task Admin(CoreScope s, CancellationToken ct)
    { if (!await db.Members.AnyAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor && x.Permission == "Administrator", ct)) throw new CoreAccessException(); }
    internal static Task Lock(CoreDbContext db, Guid tenant, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Talk:" + tenant},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'TALK lock unavailable',1;", ct);
    private async Task<T> Change<T>(Guid tenant, Func<Task<T>> action, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(db, tenant, ct);
        try { var result = await action(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result; }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    private void Audit(Guid tenant, Guid id, string action) => db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = tenant, SubjectId = id, Actor = "talk-service", Action = "talk." + action });
    private async Task<(RegistryCharacterRules Persona, RegisterLessonProfile Audience, RegisterLessonProfile Locale)> Check(TalkPolicy p, CancellationToken ct)
    {
        Require(p.Enabled, "TALK policy disabled."); await Admin(Staff(p), ct); var r = Read<TalkPolicyRequest>(p.SettingsJson);
        var persona = await db.RegistryRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.PersonaRevisionId && x.TenantId == p.TenantId && x.UniverseId == p.UniverseId && x.Kind == "Character" && x.ReviewStatus == "Approved", ct) ?? throw new CoreAccessException();
        var character = await db.Characters.AsNoTracking().SingleAsync(x => x.Id == persona.CharacterId, ct);
        Require(character.Status == "Active" && !await db.CharacterRevisions.AnyAsync(x => x.CharacterId == character.Id && x.Id != persona.CoreRevisionId && x.Version > db.CharacterRevisions.Where(y => y.Id == persona.CoreRevisionId).Select(y => y.Version).First(), ct), "Persona revision retired/replaced; renewed review required.");
        var rules = Read<RegistryCharacterRules>(persona.RulesJson);
        var audience = await db.LessonProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.AudienceProfileId && x.TenantId == p.TenantId && x.UniverseId == p.UniverseId && x.Kind == "Audience" && x.ReviewStatus == "Approved", ct) ?? throw new CoreAccessException();
        var locale = await db.LessonProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.LocaleProfileId && x.TenantId == p.TenantId && x.UniverseId == p.UniverseId && x.Kind == "Locale" && x.ReviewStatus == "Approved", ct) ?? throw new CoreAccessException();
        var a = Read<RegisterLessonProfile>(audience.RulesJson); var l = Read<RegisterLessonProfile>(locale.RulesJson);
        Require(a.Audience != null && l.Locale?.Locale == "en-IN" && rules.Locales.Contains("en-IN") && rules.AudienceIds.Contains(a.Audience.AudienceId) && rules.FictionalStatus == "Fictional" && rules.Roles.Contains("Mentor"), "Reviewed fictional mentor/audience/English capabilities required.");
        var min = r.AgeBand == "Adult" ? 18 : r.AgeBand == "Teen" ? 13 : 5; var max = r.AgeBand == "Adult" ? 120 : r.AgeBand == "Teen" ? 17 : 12;
        Require(a.Audience!.MinAge <= min && a.Audience.MaxAge >= max, "Audience does not cover the selected age band.");
        Require(r.AgeBand == "Adult" || r.MaxTurns <= a.Audience.ChildPolicy.MaxInteractions, "Minor session exceeds the reviewed audience interaction limit."); return (rules, a, l);
    }
    public Task<Guid> ConfigureAsync(CoreScope s, TalkPolicyRequest r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        await Admin(s, ct); Require(r.OperationId != Guid.Empty && r.Version > 0 && r.AgeBand is "Adult" or "Teen" or "Child" && r.MaxTurns is >= 1 and <= 24 && r.LifetimeMinutes is >= 5 and <= 60 && r.MemoryHours is >= 1 and <= 24 && r.CreditsPerTurn is >= 1 and <= 100, "Bounded launch policy required."); Key(r.Key); Key(r.Alias); Key(r.EmbeddingSpaceId); Key(r.Evidence);
        Require(r.Evidence.StartsWith("fixture:") || r.Evidence.StartsWith("evidence:"), "Explicit private qualification evidence required.");
        var old = await db.TalkPolicies.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct);
        if (old != null) { if (old.Actor != s.Actor || Json(Read<TalkPolicyRequest>(old.SettingsJson)) != Json(r)) throw new RuntimeConflictException("TALK policy replay differs."); return old.Id; }
        var p = new TalkPolicy { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = r.UniverseId, OperationId = r.OperationId, Actor = s.Actor, Key = r.Key, Version = r.Version, SettingsJson = Json(r) }; await Check(p, ct); db.TalkPolicies.Add(p); Audit(s.TenantId, p.Id, "policy-reviewed-private"); return p.Id;
    }, ct);
    public async Task ControlAsync(CoreScope s, Guid policyId, bool enabled, CancellationToken ct) => await Change(s.TenantId, async () =>
    { await Admin(s, ct); var p = await db.TalkPolicies.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Id == policyId, ct) ?? throw new CoreAccessException(); p.Enabled = enabled; if (enabled) await Check(p, ct); Audit(s.TenantId, p.Id, "policy-switch"); return true; }, ct);
    public async Task<TalkPolicyView[]> PoliciesAsync(CustomerScope s, CancellationToken ct)
    { await customers.MeAsync(s, ct); return (await db.TalkPolicies.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.Enabled).Take(50).ToArrayAsync(ct)).Select(x => new TalkPolicyView(x.Id, Read<TalkPolicyRequest>(x.SettingsJson), x.Enabled, "OfflineVerified/private-launch-only")).ToArray(); }
    public async Task<TalkPolicyView[]> AdminPoliciesAsync(CoreScope s, CancellationToken ct)
    { await Admin(s, ct); return (await db.TalkPolicies.AsNoTracking().Where(x => x.TenantId == s.TenantId).Take(50).ToArrayAsync(ct)).Select(x => new TalkPolicyView(x.Id, Read<TalkPolicyRequest>(x.SettingsJson), x.Enabled, "OfflineVerified/private-launch-only")).ToArray(); }
    public async Task<TalkProblemView[]> ProblemsAsync(CoreScope s, CancellationToken ct)
    {
        await Admin(s, ct);
        var rows = await db.TalkProblems.AsNoTracking().Where(x => x.TenantId == s.TenantId).OrderByDescending(x => x.CreatedUtc).Take(100).ToArrayAsync(ct);
        return rows.Select(x => new TalkProblemView(x.Id, x.TurnId, x.Category, MinimalProblemEvidence(x.EvidenceJson), x.CreatedUtc)).ToArray();
    }
    private static string MinimalProblemEvidence(string evidence)
    {
        // Legacy reports may hold copied passages; textual diagnostics are never exposed.
        using var document = JsonDocument.Parse(evidence);
        var fields = document.RootElement.EnumerateObject().Where(x => x.Name is "id" or "aiRunId" or "routeId" or "fingerprint" or "sourceReferences")
            .ToDictionary(x => x.Name, x => x.Value.Clone());
        return Json(new { metadata = fields, sourcesJson = "[]", retention = "IdentifiersOnly" });
    }
    public Task<Guid> OpenAsync(CustomerScope s, TalkOpen r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        Require(r.OperationId != Guid.Empty, "Stable operation required."); var authority = await customers.TalkAuthority(s, r.ProfileId, ct);
        var policy = await db.TalkPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.PolicyId && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException(); await Check(policy, ct); var config = Read<TalkPolicyRequest>(policy.SettingsJson);
        Require(authority.Profile.AgeBand == config.AgeBand && authority.Profile.Locale == "en-IN" && (!r.Remember || authority.Memory), "Profile/locale mismatch or current adult memory consent missing.");
        var old = await db.TalkSessions.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.UserId == authority.User.Id && x.OperationId == r.OperationId, ct);
        if (old != null) { if (old.ProfileId != r.ProfileId || old.PolicyId != r.PolicyId || old.Remember != r.Remember) throw new RuntimeConflictException("Session replay differs."); return old.Id; }
        Require(await db.TalkSessions.CountAsync(x => x.TenantId == s.TenantId && x.UserId == authority.User.Id && x.State == "Open" && x.ExpiresUtc > DateTimeOffset.UtcNow, ct) < 3, "At most three active sessions per account.");
        var now = DateTimeOffset.UtcNow; var x = new TalkSession { Id = Guid.NewGuid(), TenantId = s.TenantId, UserId = authority.User.Id, ProfileId = r.ProfileId, PolicyId = r.PolicyId, OperationId = r.OperationId, Remember = r.Remember, ExpiresUtc = now.AddMinutes(config.LifetimeMinutes), RetainUntilUtc = r.Remember ? now.AddHours(config.MemoryHours) : now.AddMinutes(config.LifetimeMinutes) }; db.TalkSessions.Add(x); Audit(s.TenantId, x.Id, "session-open"); return x.Id;
    }, ct);
    private async Task<(TalkSession Session, TalkPolicy Policy, bool Memory)> Own(CustomerScope s, Guid id, CancellationToken ct)
    {
        var x = await db.TalkSessions.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        var authority = await customers.TalkAuthority(s, x.ProfileId, ct); if (x.UserId != authority.User.Id) throw new CoreAccessException();
        var p = await db.TalkPolicies.AsNoTracking().SingleAsync(p => p.Id == x.PolicyId && p.TenantId == s.TenantId, ct); return (x, p, x.Remember && authority.Memory && x.RetainUntilUtc > DateTimeOffset.UtcNow);
    }
    public async Task<TalkSessionView> InspectAsync(CustomerScope s, Guid id, CancellationToken ct)
    {
        await PurgeAsync(ct); var (x, p, memory) = await Own(s, id, ct); var cfg = Read<TalkPolicyRequest>(p.SettingsJson); var persona = await db.RegistryRevisions.AsNoTracking().SingleAsync(r => r.Id == cfg.PersonaRevisionId, ct); var rules = Read<RegistryCharacterRules>(persona.RulesJson);
        var rows = await db.TalkTurns.AsNoTracking().Where(t => t.SessionId == id && t.TenantId == s.TenantId).OrderBy(t => t.CreatedUtc).Take(24).ToArrayAsync(ct); var result = new List<TalkTurnView>();
        foreach (var t in rows) result.Add(await View(t, p, memory, ct));
        return new(x.Id, x.ProfileId, p.Id, rules.Name, "AI fictional character; interpretations are reflections, not divine or human authority. Private controlled sample; human/live launch qualification pending.", cfg.AgeBand, "en-IN", x.ExpiresUtc, x.RetainUntilUtc, memory, x.State, cfg.MaxTurns, cfg.CreditsPerTurn, result.ToArray());
    }
    public async Task<TalkSource> SourceAsync(CustomerScope s, Guid id, Guid turn, Guid corpusText, CancellationToken ct)
    {
        var (_, p, memory) = await Own(s, id, ct); var row = await db.TalkTurns.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == id && x.TenantId == s.TenantId && x.Id == turn, ct) ?? throw new CoreAccessException();
        return (await View(row, p, memory, ct)).Sources.FirstOrDefault(x => x.CorpusTextId == corpusText) ?? throw new RightsBlockedException("Current cited source evidence is withheld.");
    }
    private async Task<TalkTurnView> View(TalkTurn t, TalkPolicy p, bool memory, CancellationToken ct, string? ephemeralQuestion = null, string? ephemeralAnswer = null)
    {
        var sources = Read<TalkSource[]>(t.SourcesJson); var answer = memory ? t.Answer : ephemeralAnswer; var withheld = false;
        foreach (var source in sources)
        {
            var decision = await new SourceRightsGuard(db).DecideAsync(Staff(p), source.SnapshotId, new("Generate", "en-IN", "IN", "Text", Adaptation: true, AiProcessing: true), ct);
            var current = await (from text in db.CorpusTexts join verse in db.CorpusVerses on text.VerseId equals verse.Id join manifest in db.CorpusManifests on verse.ManifestId equals manifest.Id where text.Id == source.CorpusTextId && text.SnapshotId == source.SnapshotId && text.ExactText.Contains(source.ExactText) && verse.ReviewStatus == "Approved" && manifest.TenantId == p.TenantId && manifest.UniverseId == p.UniverseId select text.Id).AnyAsync(ct);
            if (!decision.Decision.Allowed || !current) { answer = TalkRules.SafeAnswer("EvidenceWithheld"); sources = []; withheld = true; break; }
        }
        return new(t.Id, t.OperationId, t.State, withheld ? "EvidenceWithheld" : t.Outcome, memory ? t.Question : ephemeralQuestion, answer, sources, t.AiRunId, t.RouteId, t.ReservationId, t.Credits, t.ProviderCost, t.Currency, "What does the cited passage say, and what remains your own interpretation?", memory && t.Answer != null && !withheld);
    }
    public async Task CancelAsync(CustomerScope s, Guid id, CancellationToken ct) => await Change(s.TenantId, async () =>
    { var (x, _, _) = await Own(s, id, ct); x.State = "Cancelled"; transient.Forget(id); foreach (var t in await db.TalkTurns.Where(t => t.SessionId == id && t.State == "Preparing").ToArrayAsync(ct)) t.State = "Cancelled"; Audit(s.TenantId, id, "cancel"); return true; }, ct);
    public async Task ForgetAsync(CustomerScope s, Guid id, CancellationToken ct) => await Change(s.TenantId, async () =>
    { var (x, _, _) = await Own(s, id, ct); x.Remember = false; x.State = "Deleted"; transient.Forget(id); foreach (var t in await db.TalkTurns.Where(t => t.SessionId == id).ToArrayAsync(ct)) { t.Question = null; t.Answer = null; } Audit(s.TenantId, id, "forget"); return true; }, ct);
    public Task<Guid> ReportAsync(CustomerScope s, Guid id, Guid turnId, TalkFeedback r, CancellationToken ct) => Change(s.TenantId, async () =>
    {
        await Own(s, id, ct); Require(r.OperationId != Guid.Empty && r.Category is "Citation" or "Unsafe" or "Incorrect" or "Privacy" or "Other", "Bounded problem category required; do not submit sensitive free text."); var t = await db.TalkTurns.SingleOrDefaultAsync(x => x.SessionId == id && x.TenantId == s.TenantId && x.Id == turnId, ct) ?? throw new CoreAccessException();
        var old = await db.TalkProblems.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == r.OperationId, ct); if (old != null) { if (old.TurnId != t.Id || old.Category != r.Category) throw new RuntimeConflictException("Report replay differs."); return old.Id; }
        var row = new TalkProblem { Id = Guid.NewGuid(), TenantId = s.TenantId, TurnId = t.Id, OperationId = r.OperationId, Category = r.Category, EvidenceJson = Json(new { t.Id, t.AiRunId, t.RouteId, sourcesJson = "[]", sourceReferences = Read<TalkSource[]>(t.SourcesJson).Select(x => new { x.SnapshotId, x.CorpusTextId }), t.Fingerprint }) }; db.TalkProblems.Add(row); Audit(s.TenantId, row.Id, "problem-reported"); return row.Id;
    }, ct);
}
public static class TalkServices
{
    public static IServiceCollection AddTalkWorkbench(this IServiceCollection s) { s.AddSingleton<TalkMemory>(); s.AddScoped<ITalkWorkbench, TalkWorkbench>(); return s; }
}


