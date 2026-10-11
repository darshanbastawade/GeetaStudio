using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed class SourceRightsGuard(CoreDbContext db) : ISourceRightsGuard
{
    internal async Task<(List<ProvenanceEdge> Edges, HashSet<Guid> Snapshots)> LineageAsync(Guid revision, CancellationToken ct)
    {
        var edges = new List<ProvenanceEdge>();
        var snapshots = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        var pending = new Queue<Guid>(); pending.Enqueue(revision);
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current)) continue;
            if (visited.Count > 100) throw new RuntimeValidationException("Provenance exceeds the 100-revision traversal limit.");
            var found = await db.Provenance.AsNoTracking().Where(e => e.ContentRevisionId == current || db.Assets.Any(a => a.Id == e.AssetReferenceId && a.ContentRevisionId == current)).Take(101).ToListAsync(ct);
            if (found.Count > 100) throw new RuntimeValidationException("Provenance exceeds the per-revision limit.");
            edges.AddRange(found);
            foreach (var edge in found)
            {
                if (edge.SnapshotId is Guid snapshot) snapshots.Add(snapshot);
                if (edge.ParentRevisionId is Guid parent) pending.Enqueue(parent);
            }
        }
        return (edges, snapshots);
    }
    internal async Task<(RightsDecision Decision, CitationExport? Citation)> DecideAsync(CoreScope scope, Guid snapshot, IntendedUse use, CancellationToken ct)
    {
        SourceRules.Use(use.Purpose, use.Language, use.Territory, use.Format);
        var s = await db.SourceSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == snapshot, ct) ?? throw new CoreAccessException();
        var e = await db.SourceEditions.AsNoTracking().SingleAsync(e => e.Id == s.EditionId, ct);
        var w = await db.SourceWorks.AsNoTracking().SingleAsync(w => w.Id == e.WorkId, ct);
        use = use with
        {
            Adaptation = use.Adaptation || use.Purpose is "Generate" or "Adapt",
            Translation = use.Translation || use.Purpose == "Translate" || use.Language != e.Language,
            AiProcessing = use.AiProcessing || use.Purpose == "Generate"
        };
        var p = e.PermissionId is Guid permission ? await db.SourcePermissions.AsNoTracking().SingleAsync(p => p.Id == permission, ct) : null;
        var reason = SourceRules.Decide(p?.Status ?? "Unknown", w.Revoked || e.Revoked, s.ReviewStatus == "Approved",
            p?.ExpiresUtc, DateTimeOffset.UtcNow,
            p != null && await db.PermissionLanguages.AnyAsync(x => x.PermissionId == p.Id && x.Value == use.Language, ct),
            p != null && await db.PermissionTerritories.AnyAsync(x => x.PermissionId == p.Id && x.Value == use.Territory, ct),
            p != null && await db.PermissionFormats.AnyAsync(x => x.PermissionId == p.Id && x.Value == use.Format, ct),
            p?.Commercial ?? false, p?.Adaptation ?? false, p?.Translation ?? false, p?.AiProcessing ?? false,
            use.Purpose, use.Commercial, use.Adaptation, use.Translation || use.Language != e.Language, use.AiProcessing);
        var decision = new RightsDecision
        {
            Id = Guid.NewGuid(),
            TenantId = scope.TenantId,
            SnapshotId = snapshot,
            PermissionId = p?.Id,
            Actor = scope.Actor,
            Purpose = use.Purpose,
            Language = use.Language,
            Territory = use.Territory,
            Format = use.Format,
            Commercial = use.Commercial,
            Adaptation = use.Adaptation,
            Translation = use.Translation,
            AiProcessing = use.AiProcessing,
            Allowed = reason == "Allowed",
            Reason = reason
        };
        CitationExport? citation = null;
        if (decision.Allowed && p != null)
        {
            var author = await db.SourceParties.Where(x => x.Id == e.AuthorId).Select(x => x.Name).SingleAsync(ct);
            var translator = e.TranslatorId is Guid translatorId ? await db.SourceParties.Where(x => x.Id == translatorId).Select(x => x.Name).SingleAsync(ct) : "";
            var publisher = await db.SourceParties.Where(x => x.Id == e.PublisherId).Select(x => x.Name).SingleAsync(ct);
            var text = e.CitationFormat.Replace("{author}", author, StringComparison.Ordinal).Replace("{translator}", translator, StringComparison.Ordinal)
                .Replace("{title}", w.Title, StringComparison.Ordinal).Replace("{edition}", e.Key, StringComparison.Ordinal)
                .Replace("{publisher}", publisher, StringComparison.Ordinal).Replace("{locator}", e.Locator, StringComparison.Ordinal);
            citation = new(s.Id, e.Id, p.Id, text, p.RequiredAttribution, s.Sha256, s.EvidenceReference, s.Text != null);
        }
        return (decision, citation);
    }
    public async Task<RightsBundle> CheckAsync(CoreScope scope, Guid revision, IntendedUse use, CancellationToken ct)
    {
        SourceRules.Use(use.Purpose, use.Language, use.Territory, use.Format);
        var r = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == revision, ct) ?? throw new CoreAccessException();
        var content = await db.Contents.AsNoTracking().SingleAsync(c => c.Id == r.ContentId, ct);
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.TenantId == scope.TenantId && m.Actor == scope.Actor, ct) ?? throw new CoreAccessException();
        var owner = content.TenantId == scope.TenantId;
        var granted = use.Purpose == "Read"
            ? await db.Grants.AnyAsync(g => g.UniverseId == content.UniverseId && g.GranteeTenantId == scope.TenantId && g.Read, ct)
            : use.Purpose == "Export"
                ? await db.Grants.AnyAsync(g => g.UniverseId == content.UniverseId && g.GranteeTenantId == scope.TenantId && g.Export, ct)
                : await db.Grants.AnyAsync(g => g.UniverseId == content.UniverseId && g.GranteeTenantId == scope.TenantId && g.Write, ct);
        var publicRead = use.Purpose == "Read" && content.PublicKnowledge && content.EditorialState == "Approved" &&
            await db.Universes.AnyAsync(u => u.Id == content.UniverseId && u.PublicKnowledgeRead, ct);
        if (!owner && !granted && !publicRead) throw new CoreAccessException();
        if (use.Purpose is "Publish" or "Generate" or "Translate" or "Adapt" && member.Permission is not ("Administrator" or "Editor")) throw new CoreAccessException();
        if (use.Language != content.LocaleId) throw new RuntimeValidationException("Intended language must match the content locale.");
        var lineage = await LineageAsync(revision, ct);
        if (lineage.Snapshots.Count == 0 && (r.Kind is not ("Fiction" or "Synthetic") || r.RightsStatus is not ("Owned" or "Synthetic")))
            throw new RightsBlockedException("MissingReviewedEvidence");
        // Generated/interpretive content and revision derivations require adaptation permission even if the caller omits the flag.
        use = use with { Adaptation = use.Adaptation || r.Kind is "Synthetic" or "Fiction" or "Interpretation" || lineage.Edges.Any(e => e.ParentRevisionId != null) };
        var citations = new List<CitationExport>();
        foreach (var snapshot in lineage.Snapshots.Order())
        {
            var result = await DecideAsync(scope, snapshot, use, ct);
            if (!result.Decision.Allowed) throw new RightsBlockedException(result.Decision.Reason);
            db.RightsDecisions.Add(result.Decision);
            citations.Add(result.Citation!);
        }
        return new(citations, lineage.Edges.Select(e => new ProvenanceExport(e.Id, e.SnapshotId, e.ParentRevisionId, e.ContentRevisionId, e.AssetReferenceId, e.Relation)).ToArray());
    }
}





