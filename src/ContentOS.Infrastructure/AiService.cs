using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace ContentOS.Infrastructure;

public sealed partial class AiService(CoreDbContext db, RuntimeDbContext runtime, ISecretResolver secrets,
    IEnumerable<ITextGenerationProvider> texts, IEnumerable<IAiEmbeddingAdapter> embeddings,
    IConfiguration configuration, ILogger<AiService> logger) : IAiAdministration, IAiTaskRouter
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Encode<T>(T x) => JsonSerializer.Serialize(x, Json);
    internal static T Decode<T>(string x) => JsonSerializer.Deserialize<T>(x, Json)!;
    private static string Hash<T>(T x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Encode(x))));
    private async Task Access(CoreScope s, bool admin, CancellationToken ct, Guid? u = null)
    {
        var m = await db.Members.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Actor == s.Actor, ct);
        if (m == null || admin && m.Permission != "Administrator" || u.HasValue && !await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
    }
    private async Task Lock(CoreScope s, CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.AI:" + s.TenantId}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000,'AI lock unavailable',1;", ct);
    private void Audit(CoreScope s, Guid id, string action) => db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = action });
    private async Task Save(CancellationToken ct) { try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); } }
    private static void Reference(string x) { CoreRules.Text(x, 1000, "Evidence"); if (!x.StartsWith("fixture:") && !x.StartsWith("manual:") && !x.StartsWith("evidence:")) throw new RuntimeValidationException("Explicit evidence required."); }
    private ITextGenerationProvider Text(string key) => texts.SingleOrDefault(x => x.Key == key) ?? throw new RuntimeValidationException("Text adapter not deployed.");
    private IAiEmbeddingAdapter Embedding(string key) => embeddings.SingleOrDefault(x => x.Key == key) ?? throw new RuntimeValidationException("Embedding adapter not deployed.");
    private static string Endpoint(string key) => key == "openai.text.v1" ? "approved:openai-chat-v1" : "local:ai-fixture";
    private void ValidateProfile(AiProviderSettings p)
    {
        if (!AiRules.Adapters.Contains(p.Adapter) || p.Capability is not ("ai.text" or "ai.embedding") || p.Locale != "en-IN" || p.MaxInputBytes is < 256 or > 32768 || p.MaxOutputTokens is < 16 or > 4096 || p.Tools) throw new RuntimeValidationException("Unsupported deployed features/limits.");
        CoreRules.Text(p.Model, 200, "Model"); Reference(p.EvidenceReference); CoreRules.Text(p.Pricing.Version, 100, "Pricing"); CoreRules.Text(p.Policy.PolicyVersion, 100, "Policy");
        if (p.Pricing.Currency is not ("USD" or "INR") || p.Pricing.InputPerMillion < 0 || p.Pricing.CachedInputPerMillion < 0 || p.Pricing.CachedInputPerMillion > p.Pricing.InputPerMillion || p.Pricing.OutputPerMillion < 0 || p.Pricing.InputPerMillion > 1000 || p.Pricing.OutputPerMillion > 1000) throw new RuntimeValidationException("Invalid currency/rates.");
        if (p.Policy.Classifications == null || p.Policy.Classifications.Length == 0 || p.Policy.Classifications.Any(x => x is not ("Public" or "Internal")) || p.Policy.RetentionDays is < 0 or > 365 || p.Policy.Region is not ("local" or "US" or "EU" or "IN")) throw new RuntimeValidationException("Invalid data policy.");
        if (p.Adapter == "openai.text.v1")
        {
            if (p.Qualification == "LiveSandboxVerified" && p.EvidenceReference.StartsWith("fixture:")) throw new RuntimeValidationException("A fixture cannot qualify a live model.");
            if (p.Qualification is not ("Unqualified" or "LiveSandboxVerified") || p.Policy.Training || p.Policy.RetentionDays < 30 || p.Policy.Region != "US" || p.Policy.Classifications.Any(x => x != "Public")) throw new RuntimeValidationException("Only reviewed public-data US/default-retention profiles supported; account ZDR/residency unqualified.");
        }
        else if (p.Qualification != "OfflineVerified" || p.Policy.Region != "local" || p.Policy.RetentionDays != 0 || p.Policy.Training || !p.EvidenceReference.StartsWith("fixture:")) throw new RuntimeValidationException("Explicit local fixture qualification required.");
        try { if (p.Capability == "ai.text") Text(p.Adapter).Validate(p); else Embedding(p.Adapter).Validate(p); }
        catch (ArgumentException) { throw new RuntimeValidationException("Unsupported validated adapter settings."); }
    }
    public async Task<Guid> RegisterAsync(CoreScope s, RegisterAiProfile r, CancellationToken ct)
    {
        await Access(s, true, ct); ValidateProfile(r.Settings);
        if (r.MaxConcurrency is < 1 or > 4) throw new RuntimeValidationException("Concurrency must be 1–4.");
        if (r.Settings.Adapter == "openai.text.v1")
        { if (r.SecretRef == null || !await secrets.IsAvailableAsync(r.SecretRef, ct)) throw new RuntimeValidationException("Configure the private provider credential reference first."); }
        else if (r.SecretRef != null) throw new RuntimeValidationException("Local fixtures must not receive credentials.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(s, ct);
        var connection = Guid.NewGuid(); var capability = Guid.NewGuid(); var endpoint = Endpoint(r.Settings.Adapter);
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO [runtime].[Connections] ([Id],[AdapterKey],[Endpoint],[SecretRef],[Enabled],[Killed],[MaxConcurrency],[Failures],[CircuitUntilUtc]) VALUES ({connection},{r.Settings.Adapter},{endpoint},{r.SecretRef},1,0,{r.MaxConcurrency},0,NULL);
        INSERT INTO [runtime].[Profiles] ([Id],[ConnectionId],[Capability],[Locale],[ContractVersion]) VALUES ({capability},{connection},{r.Settings.Capability},{r.Settings.Locale},1);", ct);
        var row = new AiProfile { Id = Guid.NewGuid(), TenantId = s.TenantId, ConnectionId = connection, SettingsJson = Encode(r.Settings) }; db.AiProfiles.Add(row); Audit(s, row.Id, "ai.profile-register"); await Save(ct); await tx.CommitAsync(ct); return row.Id;
    }
    public async Task<IReadOnlyList<AiProfileView>> ProfilesAsync(CoreScope s, CancellationToken ct)
    {
        await Access(s, false, ct);
        return (await db.AiProfiles.AsNoTracking().Where(x => x.TenantId == s.TenantId).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)).Select(x => new AiProfileView(x.Id, x.ConnectionId, Decode<AiProviderSettings>(x.SettingsJson))).ToArray();
    }
    private async Task<AiProfileView[]> Profiles(CoreScope s, Guid[] ids, CancellationToken ct)
    {
        if (ids == null || ids.Length is < 1 or > 3 || ids.Distinct().Count() != ids.Length) throw new RuntimeValidationException("1–3 distinct qualified profiles required.");
        var rows = await db.AiProfiles.AsNoTracking().Where(x => x.TenantId == s.TenantId && ids.Contains(x.Id)).ToListAsync(ct);
        if (rows.Count != ids.Length) throw new CoreAccessException();
        return ids.Select(id => { var p = rows.Single(x => x.Id == id); return new AiProfileView(id, p.ConnectionId, Decode<AiProviderSettings>(p.SettingsJson)); }).ToArray();
    }
    private void Compatible(AiRouteSettings r, AiProviderSettings p)
    {
        ValidateProfile(p);
        if (p.Qualification == "Unqualified" || r.Currency != p.Pricing.Currency || p.Capability != (r.Task == "embedding" ? "ai.embedding" : "ai.text") || r.Structured && !p.Structured || r.Streaming && !p.Streaming || r.Tools && !p.Tools) throw new RuntimeValidationException("Unsupported feature, currency or unqualified provider.");
        if (!p.Policy.Classifications.Contains(r.Classification) || r.Region != p.Policy.Region || p.Policy.RetentionDays > r.MaxRetentionDays || p.Policy.Training && !r.AllowTraining || r.PolicyVersion != p.Policy.PolicyVersion) throw new RuntimeValidationException("Every fallback must preserve whole-chain data policy.");
    }
    public async Task<Guid> RouteAsync(CoreScope s, AiRouteSettings r, CancellationToken ct)
    {
        await Access(s, true, ct); if (!AiRules.Tasks.Contains(r.Task) || r.RunLimit <= 0 || r.RunLimit > 100 || r.DeadlineSeconds is < 5 or > 300 || string.IsNullOrWhiteSpace(r.PromptVersion)) throw new RuntimeValidationException("Invalid bounded task profile.");
        CoreRules.Text(r.PromptVersion, 100, "Prompt version");
        if (r.PromptVersion != "generated-text.v1" && !await db.PromptRevisions.AnyAsync(x => x.TenantId == s.TenantId && x.Key + ".v" + x.Version == r.PromptVersion, ct)) throw new RuntimeValidationException("Prompt revision is not registered for this tenant.");
        CoreRules.Text(r.Version, 100, "Task version"); foreach (var p in await Profiles(s, r.Profiles, ct)) Compatible(r, p.Settings);
        var row = new AiRoute { Id = Guid.NewGuid(), TenantId = s.TenantId, Task = r.Task, SettingsJson = Encode(r) }; db.AiRoutes.Add(row); Audit(s, row.Id, "ai.route-register"); await Save(ct); return row.Id;
    }
    public async Task ActivateAsync(CoreScope s, Guid id, CancellationToken ct)
    {
        var ownsTransaction = db.Database.CurrentTransaction == null; if (ownsTransaction) db.ChangeTracker.Clear(); await using var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null; await Lock(s, ct); await Access(s, true, ct);
        var row = await db.AiRoutes.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException(); var r = Decode<AiRouteSettings>(row.SettingsJson);
        foreach (var p in await Profiles(s, r.Profiles, ct)) { Compatible(r, p.Settings); if (await Admission(p, false, ct) != "Success") throw new RuntimeValidationException("Route provider currently unavailable."); }
        var head = await db.AiRouteHeads.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Task == row.Task, ct);
        if (head == null) db.AiRouteHeads.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Task = row.Task, RouteId = id }); else head.RouteId = id;
        Audit(s, id, "ai.route-activate-or-rollback"); await Save(ct); if (tx != null) await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<AiRouteView>> RoutesAsync(CoreScope s, CancellationToken ct)
    {
        await Access(s, false, ct); var active = await db.AiRouteHeads.AsNoTracking().Where(x => x.TenantId == s.TenantId).Select(x => x.RouteId).ToListAsync(ct);
        return (await db.AiRoutes.AsNoTracking().Where(x => x.TenantId == s.TenantId).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(ct)).Select(x => new AiRouteView(x.Id, Decode<AiRouteSettings>(x.SettingsJson), active.Contains(x.Id))).ToArray();
    }
    public async Task<Guid> BudgetAsync(CoreScope s, ConfigureAiBudget r, CancellationToken ct)
    {
        if (r.Capability is not ("ai.text" or "ai.embedding") || r.Currency is not ("USD" or "INR") || r.Limit <= 0 || r.Limit > 10000 || r.StartsUtc >= r.EndsUtc || r.EndsUtc - r.StartsUtc > TimeSpan.FromDays(366)) throw new RuntimeValidationException("Invalid period budget.");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(s, ct); await Access(s, true, ct);
        if (await db.AiBudgets.AnyAsync(x => x.TenantId == s.TenantId && x.Capability == r.Capability && x.Currency == r.Currency && x.StartsUtc < r.EndsUtc && r.StartsUtc < x.EndsUtc, ct)) throw new RuntimeConflictException("Budget periods cannot overlap or reset reservations.");
        var row = new AiBudget { Id = Guid.NewGuid(), TenantId = s.TenantId, Capability = r.Capability, Currency = r.Currency, StartsUtc = r.StartsUtc.ToUniversalTime(), EndsUtc = r.EndsUtc.ToUniversalTime(), Limit = r.Limit };
        db.AiBudgets.Add(row); Audit(s, row.Id, "ai.budget-create"); await Save(ct); await tx.CommitAsync(ct); return row.Id;
    }
    public async Task<IReadOnlyList<AiBudgetView>> BudgetsAsync(CoreScope s, CancellationToken ct)
    {
        await Access(s, false, ct); return (await db.AiBudgets.AsNoTracking().Where(x => x.TenantId == s.TenantId).OrderByDescending(x => x.StartsUtc).Take(100).ToListAsync(ct)).Select(x => new AiBudgetView(x.Id, x.Capability, x.Currency, x.StartsUtc, x.EndsUtc, x.Limit, x.Reserved, x.Spent, Convert.ToBase64String(x.RowVersion))).ToArray();
    }
    public async Task SwitchAsync(CoreScope s, Guid id, AiConnectionSwitch r, CancellationToken ct)
    {
        await Access(s, true, ct); var p = await db.AiProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [runtime].[Connections] SET [Enabled]={r.Enabled},[Killed]={r.Killed} WHERE [Id]={p.ConnectionId}", ct);
        Audit(s, id, r.Killed ? "ai.provider-kill" : "ai.provider-switch"); await Save(ct);
    }
    private async Task<string> Admission(AiProfileView p, bool concurrency, CancellationToken ct)
    {
        runtime.ChangeTracker.Clear(); var c = await runtime.Connections.AsNoTracking().SingleAsync(x => x.Id == p.ConnectionId, ct);
        if (c.Killed || !c.Enabled) return "Configuration";
        if (c.CircuitUntilUtc > DateTimeOffset.UtcNow) return "CircuitOpen";
        if (c.AdapterKey != p.Settings.Adapter || c.Endpoint != Endpoint(c.AdapterKey) || !await secrets.IsAvailableAsync(c.SecretRef, ct)) return "Configuration";
        if (p.Settings.Adapter == "openai.text.v1" && !string.Equals(configuration["Ai:LiveEnabled"], "true", StringComparison.OrdinalIgnoreCase)) return "LiveDisabled";
        if (concurrency && await db.AiAttempts.CountAsync(x => x.ConnectionId == p.ConnectionId && x.State == "Started", ct) >= c.MaxConcurrency) return "RateLimited";
        return "Success";
    }
    internal static decimal Cost(AiPricing p, AiUsage u)
    {
        if (u.InputTokens < 0 || u.CachedInputTokens < 0 || u.CachedInputTokens > u.InputTokens || u.OutputTokens < 0 || u.InputTokens > 10000000 || u.OutputTokens > 10000000) throw new RuntimeValidationException("Invalid actual usage.");
        return decimal.Ceiling(((u.InputTokens - u.CachedInputTokens) * p.InputPerMillion + u.CachedInputTokens * p.CachedInputPerMillion + u.OutputTokens * p.OutputPerMillion) / 1000000m * 100000000m) / 100000000m;
    }
    private static decimal Ceiling(AiProviderSettings p, int bytes) => Cost(p.Pricing, new(bytes + 1024, 0, p.MaxOutputTokens));
}
