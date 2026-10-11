using System.Data;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed partial class AiService
{
    internal async Task<string> PinEmbedding(CoreScope s, Guid universe, string model, CancellationToken ct)
    {
        await Access(s, false, ct, universe);
        if (model != "route:embedding") return model;
        var head = await db.AiRouteHeads.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Task == "embedding", ct) ?? throw new RuntimeValidationException("Configure the embedding task route.");
        var row = await db.AiRoutes.AsNoTracking().SingleAsync(x => x.Id == head.RouteId, ct); var r = Decode<AiRouteSettings>(row.SettingsJson);
        var profiles = await Profiles(s, r.Profiles, ct);
        if (profiles.Length != 1) throw new RuntimeValidationException("Embedding models cannot fallback across spaces.");
        Compatible(r, profiles[0].Settings);
        return "ai-embedding:" + row.Id + ":" + profiles[0].Id;
    }
    internal async ValueTask<OwnedEmbedding> Embed(CoreScope s, Guid universe, EmbeddingRequest request, CancellationToken ct)
    {
        await Access(s, false, ct, universe); var parts = request.Model.Split(':');
        if (parts.Length != 3 || !Guid.TryParse(parts[1], out var routeId) || !Guid.TryParse(parts[2], out var profileId)) throw new RuntimeValidationException("Invalid pinned embedding model.");
        var route = await db.AiRoutes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == routeId && x.TenantId == s.TenantId, ct) ?? throw new CoreAccessException();
        var settings = Decode<AiRouteSettings>(route.SettingsJson); var p = (await Profiles(s, [profileId], ct)).Single();
        if (settings.Task != "embedding" || settings.Profiles.Length != 1 || settings.Profiles[0] != profileId || request.Locale != p.Settings.Locale || request.Dimensions != 128 || request.Normalization != "l2") throw new RuntimeValidationException("Embedding space mismatch.");
        Compatible(settings, p.Settings); var bytes = Encoding.UTF8.GetByteCount(request.Text);
        if (bytes + 1024 > p.Settings.MaxInputBytes) throw new RuntimeValidationException("Embedding input limit.");
        var ceiling = Ceiling(p.Settings, bytes);
        db.ChangeTracker.Clear(); await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await Lock(s, ct);
            var now = DateTimeOffset.UtcNow;
            var budget = await db.AiBudgets.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.Capability == "ai.embedding" && x.Currency == settings.Currency && x.StartsUtc <= now && now < x.EndsUtc, ct) ?? throw new RuntimeValidationException("Configure the embedding capability period budget.");
            if (ceiling > settings.RunLimit || budget.Spent + budget.Reserved + ceiling > budget.Limit) throw new RuntimeValidationException("QuotaExhausted: embedding budget.");
            var snapshot = new AiExecutionSnapshot(route.Id, settings, [p], request.Locale, Hash(request), []);
            var row = new AiRun { Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = universe, Actor = s.Actor, BudgetId = budget.Id, RouteId = route.Id, Fingerprint = snapshot.RequestHash, SnapshotJson = Encode(snapshot), Reserved = ceiling, DeadlineUtc = now.AddSeconds(settings.DeadlineSeconds) };
            budget.Reserved += ceiling; db.AiRuns.Add(row); Audit(s, row.Id, "ai.embedding-reserved"); await Save(ct); await tx.CommitAsync(ct); await tx.DisposeAsync();
            return await ExecuteEmbedding(s, row, p, request, ceiling, ct);
        }
    }
    private async ValueTask<OwnedEmbedding> ExecuteEmbedding(CoreScope s, AiRun run, AiProfileView p, EmbeddingRequest request, decimal ceiling, CancellationToken ct)
    {
        // Budget commit precedes the provider call. Model/route remain pinned if the active head changes.
        var attempt = await Start(s, run.Id, p, 1, ceiling, ct, false);
        if (attempt.State != "Started") { await Finish(s, run.Id, attempt.Outcome, null, CancellationToken.None); throw new KnowledgeBlockedException(attempt.Outcome); }
        AiEmbeddingResult result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(Decode<AiExecutionSnapshot>(run.SnapshotJson).Route.DeadlineSeconds));
        try { result = await Embedding(p.Settings.Adapter).EmbedAsync(new(p.Settings, request), timeout.Token); }
        catch (Exception) { await FinishAttempt(s, attempt.Id, new("UnknownUsage", null, null), "UnknownUsage", CancellationToken.None); await Finish(s, run.Id, "UnknownUsage", null, CancellationToken.None); throw; }
        var valid = result.Embedding.Vector.Length == 128 && result.Embedding.Vector.All(float.IsFinite);
        await FinishAttempt(s, attempt.Id, new(valid ? "Success" : "SchemaFailure", null, result.Usage), valid ? "Success" : "SchemaFailure", CancellationToken.None);
        await Finish(s, run.Id, valid ? "Success" : "SchemaFailure", null, CancellationToken.None);
        if (!valid) throw new KnowledgeBlockedException("InvalidEmbeddingContract"); return result.Embedding;
    }
}
public sealed class RoutedEmbeddingProvider(IServiceScopeFactory scopes, ILegacyEmbeddingProvider legacy) : IRoutedEmbeddingProvider
{
    public bool Supports(string model, int dimensions, string normalization) =>
        dimensions == 128 && normalization == "l2" && (model == "route:embedding" || model.StartsWith("ai-embedding:") || legacy.Supports(model, dimensions, normalization));
    public async Task<string> PinAsync(CoreScope s, Guid universe, string model, CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); return await scope.ServiceProvider.GetRequiredService<AiService>().PinEmbedding(s, universe, model, ct);
    }
    public async ValueTask<OwnedEmbedding> EmbedAsync(EmbeddingRequest r, CancellationToken ct)
    {
        if (!r.Model.StartsWith("ai-embedding:")) return await legacy.EmbedAsync(r, ct); // Retain immutable M06 spaces.
        if (r.TenantId == null || r.Actor == null || r.UniverseId == null) throw new RuntimeValidationException("Scoped embedding context required.");
        using var scope = scopes.CreateScope(); return await scope.ServiceProvider.GetRequiredService<AiService>().Embed(new(r.TenantId.Value, r.Actor), r.UniverseId.Value, r, ct);
    }
}
