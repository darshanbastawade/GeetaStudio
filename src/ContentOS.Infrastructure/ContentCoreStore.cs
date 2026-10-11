using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed class ContentCoreStore(CoreDbContext db) : IContentCore
{
    private enum Permission { Read, Write, Export, Manage }
    public async Task<IReadOnlyList<TenantView>> TenantsAsync(string actor, CancellationToken ct) =>
        await (from t in db.Tenants.AsNoTracking()
               join m in db.Members.AsNoTracking() on t.Id equals m.TenantId
               where m.Actor == actor
               select new TenantView(t.Id, t.Key, t.Name, m.Permission)).ToListAsync(ct);
    private async Task<(Universe Universe, bool FullRead)> AccessAsync(CoreScope scope, Guid universe, Permission permission, CancellationToken ct)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.TenantId == scope.TenantId && m.Actor == scope.Actor, ct)
            ?? throw new CoreAccessException();
        if (permission == Permission.Manage && member.Permission != "Administrator") throw new CoreAccessException();
        if (permission == Permission.Write && member.Permission is not ("Administrator" or "Editor")) throw new CoreAccessException();
        var query = db.Universes.AsNoTracking().Where(u => u.Id == universe);
        query = permission switch
        {
            Permission.Read => query.Where(u => u.TenantId == scope.TenantId || u.PublicKnowledgeRead ||
                db.Grants.Any(g => g.UniverseId == u.Id && g.GranteeTenantId == scope.TenantId && g.Read)),
            Permission.Write => query.Where(u => u.TenantId == scope.TenantId ||
                db.Grants.Any(g => g.UniverseId == u.Id && g.GranteeTenantId == scope.TenantId && g.Write)),
            Permission.Export => query.Where(u => u.TenantId == scope.TenantId ||
                db.Grants.Any(g => g.UniverseId == u.Id && g.GranteeTenantId == scope.TenantId && g.Export)),
            _ => query.Where(u => u.TenantId == scope.TenantId)
        };
        var row = await query.SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
        var fullRead = row.TenantId == scope.TenantId ||
            await db.Grants.AnyAsync(g => g.UniverseId == row.Id && g.GranteeTenantId == scope.TenantId && g.Read, ct);
        return (row, fullRead);
    }
    public async Task<IReadOnlyList<UniverseView>> UniversesAsync(CoreScope scope, CancellationToken ct)
    {
        if (!await db.Members.AnyAsync(m => m.TenantId == scope.TenantId && m.Actor == scope.Actor, ct)) throw new CoreAccessException();
        return await db.Universes.AsNoTracking().Where(u => u.TenantId == scope.TenantId || u.PublicKnowledgeRead ||
            db.Grants.Any(g => g.UniverseId == u.Id && g.GranteeTenantId == scope.TenantId && g.Read))
            .Select(u => new UniverseView(u.Id, u.TenantId, u.Key, u.Name, u.PublicKnowledgeRead, Convert.ToBase64String(u.RowVersion))).ToListAsync(ct);
    }
    public async Task<UniverseCatalog> CatalogAsync(CoreScope scope, Guid universe, CancellationToken ct)
    {
        var access = await AccessAsync(scope, universe, Permission.Read, ct);
        if (!access.FullRead) throw new CoreAccessException(); // Public knowledge shares individual approved records, not private manifests.
        var pack = await db.Packs.AsNoTracking().Where(p => p.UniverseId == universe).OrderByDescending(p => p.Version).FirstAsync(ct);
        var audiences = await db.Audiences.AsNoTracking().Where(a => a.UniverseId == universe)
            .Select(a => new AudienceView(a.Id, a.Key, a.Name, a.MinAge, a.MaxAge)).ToListAsync(ct);
        var manifest = new UniversePackManifest(pack.SchemaVersion, pack.Version, pack.Id, universe,
            await db.Terms.Where(t => t.PackId == pack.Id).ToDictionaryAsync(t => t.Key, t => t.Value, ct),
            await db.SubjectTypes.Where(t => t.PackId == pack.Id).Select(t => t.Key).ToListAsync(ct),
            await db.SourceRules.Where(t => t.PackId == pack.Id).ToDictionaryAsync(t => t.Kind, t => t.Rule, ct),
            audiences.Where(a => db.PackAudiences.Any(p => p.PackId == pack.Id && p.AudienceId == a.Id)).ToArray(),
            await db.PackCharacters.Where(t => t.PackId == pack.Id).Select(t => t.CharacterId).ToListAsync(ct),
            await db.Policies.Where(t => t.PackId == pack.Id).ToDictionaryAsync(t => t.Key, t => t.Rule, ct));
        var characters = new List<CharacterProfile>();
        foreach (var c in await db.Characters.AsNoTracking().Where(c => c.UniverseId == universe).ToListAsync(ct))
        {
            var r = await db.CharacterRevisions.AsNoTracking().Where(r => r.CharacterId == c.Id).OrderByDescending(r => r.Version).FirstAsync(ct);
            characters.Add(new(c.Id, r.Id, r.Version, c.Key, r.DisplayName, r.LifeStage, r.Biography, r.FictionalStatus,
                c.PersistenceScope, c.Status, await db.Aliases.Where(a => a.CharacterId == c.Id).Select(a => a.Name).ToListAsync(ct)));
        }
        return new(new(access.Universe.Id, access.Universe.TenantId, access.Universe.Key, access.Universe.Name, access.Universe.PublicKnowledgeRead, Convert.ToBase64String(access.Universe.RowVersion)),
            manifest, await db.Subjects.AsNoTracking().Where(s => s.UniverseId == universe).Select(s => new SubjectView(s.Id, s.Key, s.Type, s.Name)).ToListAsync(ct),
            characters, await db.Locales.Select(l => l.Id).ToListAsync(ct));
    }
    private IQueryable<ContentItem> Visible(CoreScope scope, Guid universe, bool fullRead) =>
        db.Contents.AsNoTracking().Where(c => c.UniverseId == universe &&
            (fullRead || (c.PublicKnowledge && c.EditorialState == "Approved")));
    private async Task<ContentView> ViewAsync(ContentItem item, CancellationToken ct, bool currentOnly = false) =>
        new(item.Id, item.TenantId, item.UniverseId, item.SubjectId, item.AudienceId, item.LocaleId, item.CurrentVersion,
            item.EditorialState, item.PublicKnowledge, Convert.ToBase64String(item.RowVersion), item.CreatedUtc, item.UpdatedUtc,
            await db.Revisions.AsNoTracking().Where(r => r.ContentId == item.Id && (!currentOnly || r.Version == item.CurrentVersion)).OrderBy(r => r.Version)
                .Select(r => new RevisionContract(r.Id, r.Version, r.OperationId, r.Title, r.Body, r.Kind, r.RightsStatus, r.Provenance, r.CreatedUtc)).ToListAsync(ct));
    private async Task<ContentView> MutationViewAsync(CoreScope scope, ContentItem item, CancellationToken ct)
    {
        var view = await ViewAsync(item, ct);
        return item.TenantId == scope.TenantId ? view : view with { Revisions = [], EvidenceLabels = [] };
    }
    private async Task<ContentView> CheckReadAsync(CoreScope scope, ContentView view, CancellationToken ct)
    {
        if (view.TenantId == scope.TenantId) return view; // Owning staff can inspect evidence for review.
        var labels = new List<ClaimLabelView>();
        foreach (var revision in view.Revisions)
        {
            var use = new IntendedUse("Read", view.LocaleId, "IN", "Text");
            await new SourceRightsGuard(db).CheckAsync(scope, revision.Id, use, ct);
            var gate = await new EvidenceEngine(db).CheckContentAsync(scope, revision.Id, use, ct);
            if (!gate.Eligible) throw new EvidenceBlockedException(gate.Reason);
            labels.AddRange(gate.Labels);
        }
        return view with { EvidenceLabels = labels };
    }
    public async Task<IReadOnlyList<ContentView>> ListAsync(CoreScope scope, Guid universe, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var a = await AccessAsync(scope, universe, Permission.Read, ct);
        var result = new List<ContentView>();
        foreach (var row in await Visible(scope, universe, a.FullRead).OrderBy(c => c.CreatedUtc).Take(100).ToListAsync(ct))
        {
            var view = await ViewAsync(row, ct, !a.FullRead);
            try { view = await CheckReadAsync(scope, view, ct); }
            catch (RightsBlockedException) { continue; }
            catch (EvidenceBlockedException) { continue; }
            result.Add(view);
        }
        await SaveAsync(ct); await tx.CommitAsync(ct);
        return result;
    }
    public async Task<ContentView> ReadAsync(CoreScope scope, Guid universe, Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var a = await AccessAsync(scope, universe, Permission.Read, ct);
        var row = await Visible(scope, universe, a.FullRead).SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new CoreAccessException();
        var view = await ViewAsync(row, ct, !a.FullRead);
        view = await CheckReadAsync(scope, view, ct); await SaveAsync(ct); await tx.CommitAsync(ct);
        return view;
    }
    private async Task ValidateReferencesAsync(Guid universe, Guid subject, Guid audience, string locale, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == subject && s.UniverseId == universe, ct) ||
            !await db.Audiences.AnyAsync(a => a.Id == audience && a.UniverseId == universe, ct) || !await db.Locales.AnyAsync(l => l.Id == locale, ct))
            throw new RuntimeValidationException("Subject, audience or locale does not belong to the selected catalog.");
    }
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private async Task<Guid?> ReplayAsync(CoreScope scope, Guid operation, string kind, string fingerprint, CancellationToken ct)
    {
        if (operation == Guid.Empty) throw new RuntimeValidationException("A stable business operation ID is required.");
        var prior = await db.Operations.FromSqlInterpolated($"SELECT * FROM [core].[Operations] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {scope.TenantId} AND [Id] = {operation}").SingleOrDefaultAsync(ct);
        if (prior is null) return null;
        if (prior.Actor != scope.Actor || prior.Kind != kind || prior.Fingerprint != fingerprint)
            throw new RuntimeConflictException("Operation ID was already used with a different command.");
        return prior.ContentId;
    }
    private void Record(CoreScope scope, ContentItem item, ContentRevision revision, Guid operation, string kind, string fingerprint)
    {
        db.Operations.Add(new CoreOperation { Id = operation, TenantId = scope.TenantId, Actor = scope.Actor, Kind = kind, Fingerprint = fingerprint, ContentId = item.Id });
        db.Events.Add(new CoreEvent
        {
            Id = Guid.NewGuid(),
            TenantId = item.TenantId,
            UniverseId = item.UniverseId,
            ContentId = item.Id,
            RevisionId = revision.Id,
            OperationId = operation,
            Type = kind,
            Actor = scope.Actor
        });
    }
    private static void Expected(ContentItem item, string version)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(version); } catch (Exception e) when (e is FormatException or ArgumentNullException) { throw new RuntimeValidationException("Invalid row version."); }
        if (!item.RowVersion.SequenceEqual(expected)) throw new CoreConcurrencyException();
    }
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
    }
    public async Task<ContentView> CreateAsync(CoreScope scope, Guid universe, CreateContent request, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var a = await AccessAsync(scope, universe, Permission.Write, ct);
        CoreRules.Revision(request.Title, request.Body, request.Kind, request.RightsStatus, request.Provenance, request.PublicKnowledge);
        await ValidateReferencesAsync(universe, request.SubjectId, request.AudienceId, request.LocaleId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, Permission.Write, ct);
        var fingerprint = Fingerprint(new { universe, request });
        var replay = await ReplayAsync(scope, request.OperationId, "content.created.v1", fingerprint, ct);
        if (replay is not null) return await MutationViewAsync(scope, await db.Contents.AsNoTracking().SingleAsync(c => c.Id == replay, ct), ct);
        var item = new ContentItem
        {
            Id = Guid.NewGuid(),
            TenantId = a.Universe.TenantId,
            UniverseId = universe,
            SubjectId = request.SubjectId,
            AudienceId = request.AudienceId,
            LocaleId = request.LocaleId,
            CurrentVersion = 1,
            PublicKnowledge = request.PublicKnowledge
        };
        var revision = new ContentRevision
        {
            Id = Guid.NewGuid(),
            ContentId = item.Id,
            Version = 1,
            OperationId = request.OperationId,
            Title = request.Title,
            Body = request.Body,
            Kind = request.Kind,
            RightsStatus = request.RightsStatus,
            Provenance = request.Provenance
        };
        db.Contents.Add(item); db.Revisions.Add(revision); Record(scope, item, revision, request.OperationId, "content.created.v1", fingerprint);
        await SaveAsync(ct); await tx.CommitAsync(ct); return await MutationViewAsync(scope, item, ct);
    }
    private async Task<ContentItem> LockAsync(Guid universe, Guid id, CancellationToken ct) =>
        await db.Contents.FromSqlInterpolated($"SELECT * FROM [core].[Contents] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id} AND [UniverseId] = {universe}")
            .SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
    public async Task<ContentView> ReviseAsync(CoreScope scope, Guid universe, Guid id, ReviseContent request, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await AccessAsync(scope, universe, Permission.Write, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, Permission.Write, ct);
        var item = await LockAsync(universe, id, ct);
        var fingerprint = Fingerprint(new { universe, id, request });
        if (await ReplayAsync(scope, request.OperationId, "content.revised.v1", fingerprint, ct) is not null) return await MutationViewAsync(scope, item, ct);
        Expected(item, request.ExpectedRowVersion);
        CoreRules.Revision(request.Title, request.Body, request.Kind, request.RightsStatus, request.Provenance, item.PublicKnowledge);
        if (item.EditorialState == "Archived") throw new RuntimeConflictException("Archived content cannot be revised.");
        item.CurrentVersion++; item.EditorialState = "Draft";
        var revision = new ContentRevision
        {
            Id = Guid.NewGuid(),
            ContentId = id,
            Version = item.CurrentVersion,
            OperationId = request.OperationId,
            Title = request.Title,
            Body = request.Body,
            Kind = request.Kind,
            RightsStatus = request.RightsStatus,
            Provenance = request.Provenance
        };
        var parent = await db.Revisions.Where(r => r.ContentId == id && r.Version == item.CurrentVersion - 1).Select(r => r.Id).SingleAsync(ct);
        if (await db.Provenance.AnyAsync(e => e.ContentRevisionId == parent || db.Assets.Any(a => a.Id == e.AssetReferenceId && a.ContentRevisionId == parent), ct) || await db.ClaimLinks.AnyAsync(l => l.ContentRevisionId == parent, ct))
            db.Provenance.Add(new ProvenanceEdge { Id = Guid.NewGuid(), TenantId = item.TenantId, ParentRevisionId = parent, ContentRevisionId = revision.Id, Relation = "revision-of" });
        if (await db.Revisions.AnyAsync(r => r.Id == parent && r.Body == request.Body, ct))
            foreach (var link in await db.ClaimLinks.AsNoTracking().Where(l => l.ContentRevisionId == parent).ToListAsync(ct))
                db.ClaimLinks.Add(new ClaimContentLink { Id = Guid.NewGuid(), ContentRevisionId = revision.Id, ClaimRevisionId = link.ClaimRevisionId, Start = link.Start, Length = link.Length });
        db.Revisions.Add(revision); Record(scope, item, revision, request.OperationId, "content.revised.v1", fingerprint);
        await SaveAsync(ct); await tx.CommitAsync(ct); return await MutationViewAsync(scope, item, ct);
    }
    public async Task<ContentView> TransitionAsync(CoreScope scope, Guid universe, Guid id, ChangeEditorialState request, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await AccessAsync(scope, universe, Permission.Write, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, Permission.Write, ct);
        var item = await LockAsync(universe, id, ct); var fingerprint = Fingerprint(new { universe, id, request });
        if (await ReplayAsync(scope, request.OperationId, "content.editorial-state.v1", fingerprint, ct) is not null) return await MutationViewAsync(scope, item, ct);
        Expected(item, request.ExpectedRowVersion);
        var revision = await db.Revisions.SingleAsync(r => r.ContentId == id && r.Version == item.CurrentVersion, ct);
        CoreRules.Transition(item.EditorialState, request.State, revision.RightsStatus);
        if (request.State == "Approved")
        {
            var use = new IntendedUse("Export", item.LocaleId, "IN", "Text");
            await new SourceRightsGuard(db).CheckAsync(scope, revision.Id, use, ct);
            var gate = await new EvidenceEngine(db).CheckContentAsync(scope, revision.Id, use, ct);
            if (!gate.Eligible) throw new EvidenceBlockedException(gate.Reason);
        }
        item.EditorialState = request.State; Record(scope, item, revision, request.OperationId, "content.editorial-state.v1", fingerprint);
        await SaveAsync(ct); await tx.CommitAsync(ct); return await MutationViewAsync(scope, item, ct);
    }
    public async Task<ContentExport> ExportAsync(CoreScope scope, Guid universe, Guid id, CancellationToken ct)
    {
        await AccessAsync(scope, universe, Permission.Export, ct);
        var locale = await db.Contents.Where(c => c.Id == id && c.UniverseId == universe).Select(c => c.LocaleId).SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
        return await ExportForUseAsync(scope, universe, id, new("Export", locale, "IN", "Text"), ct);
    }
    public async Task<ContentExport> ExportForUseAsync(CoreScope scope, Guid universe, Guid id, IntendedUse use, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var a = await AccessAsync(scope, universe, Permission.Export, ct);
        var item = await db.Contents.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.UniverseId == universe, ct) ?? throw new CoreAccessException();
        SourceRules.Use(use.Purpose, use.Language, use.Territory, use.Format);
        if (use.Purpose != "Export") throw new RuntimeValidationException("Export requires an Export intended use.");
        var content = await ViewAsync(item, ct);
        var citations = new List<CitationExport>(); var edges = new List<ProvenanceExport>(); var labels = new List<ClaimLabelView>();
        foreach (var revision in content.Revisions)
        {
            var bundle = await new SourceRightsGuard(db).CheckAsync(scope, revision.Id, use, ct);
            var gate = await new EvidenceEngine(db).CheckContentAsync(scope, revision.Id, use, ct);
            if (!gate.Eligible) throw new EvidenceBlockedException(gate.Reason);
            labels.AddRange(gate.Labels); citations.AddRange(bundle.Citations); edges.AddRange(bundle.Provenance);
        }
        await SaveAsync(ct); await tx.CommitAsync(ct);
        return new(1, a.Universe.TenantId, universe, content, citations.DistinctBy(c => c.SnapshotId).ToArray(), edges.DistinctBy(e => e.Id).ToArray(), labels.ToArray());
    }
    public async Task<ContentView> ImportAsync(CoreScope scope, Guid universe, ImportContent request, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var a = await AccessAsync(scope, universe, Permission.Write, ct);
        var doc = request.Document ?? throw new RuntimeValidationException("Import document is required.");
        if (doc.Citations?.Count > 0 || doc.Provenance?.Count > 0 || doc.EvidenceLabels?.Count > 0)
            throw new RuntimeValidationException("Source-derived exports require explicit reviewed evidence rebinding; the core import cannot discard provenance.");
        var content = doc.Content ?? throw new RuntimeValidationException("Import content is required.");
        if (content.Revisions is null || content.Revisions.Any(r => r is null)) throw new RuntimeValidationException("Import revisions are required.");
        if (doc.SchemaVersion != 1 || doc.UniverseId != universe || doc.TenantId != a.Universe.TenantId ||
            content.UniverseId != universe || content.TenantId != doc.TenantId || content.Id == Guid.Empty || content.CurrentVersion < 1 ||
            content.Revisions.Count != content.CurrentVersion || content.Revisions.Count > 100 ||
            content.Revisions.Select(r => r.Id).Distinct().Count() != content.Revisions.Count ||
            content.Revisions.Select(r => r.OperationId).Distinct().Count() != content.Revisions.Count ||
            content.Revisions.Where((r, i) => r.Version != i + 1 || r.Id == Guid.Empty || r.OperationId == Guid.Empty || r.CreatedUtc.Offset != TimeSpan.Zero).Any() ||
            content.CreatedUtc.Offset != TimeSpan.Zero || content.UpdatedUtc.Offset != TimeSpan.Zero ||
            content.EditorialState is not ("Draft" or "Review" or "Approved" or "Archived"))
            throw new RuntimeValidationException("Unsupported or inconsistent content export envelope.");
        if (content.EditorialState is "Approved" or "Archived" && content.Revisions.Any(r => r.Kind is not ("Fiction" or "Synthetic") || r.RightsStatus is not ("Owned" or "Synthetic")))
            throw new RightsBlockedException("ImportedEvidenceRequiresReview");
        foreach (var r in content.Revisions) CoreRules.Revision(r.Title, r.Body, r.Kind, r.RightsStatus, r.Provenance, content.PublicKnowledge);
        if (content.EditorialState is "Approved" or "Archived" && content.Revisions[^1].RightsStatus == "Unverified")
            throw new RuntimeValidationException("Approved imports require recorded rights.");
        await ValidateReferencesAsync(universe, content.SubjectId, content.AudienceId, content.LocaleId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, Permission.Write, ct);
        var fingerprint = Fingerprint(request);
        var replay = await ReplayAsync(scope, request.OperationId, "content.imported.v1", fingerprint, ct);
        if (replay is not null) return await MutationViewAsync(scope, await db.Contents.AsNoTracking().SingleAsync(c => c.Id == replay, ct), ct);
        if (await db.Contents.AnyAsync(c => c.Id == content.Id, ct) || await db.Revisions.AnyAsync(r => content.Revisions.Select(x => x.Id).Contains(r.Id), ct))
            throw new RuntimeConflictException("Imported IDs already exist; no existing history is overwritten.");
        var item = new ContentItem
        {
            Id = content.Id,
            TenantId = content.TenantId,
            UniverseId = universe,
            SubjectId = content.SubjectId,
            AudienceId = content.AudienceId,
            LocaleId = content.LocaleId,
            CurrentVersion = content.CurrentVersion,
            EditorialState = content.EditorialState,
            PublicKnowledge = content.PublicKnowledge,
            CreatedUtc = content.CreatedUtc
        };
        db.Contents.Add(item);
        foreach (var r in content.Revisions) db.Revisions.Add(new ContentRevision
        {
            Id = r.Id,
            ContentId = item.Id,
            Version = r.Version,
            OperationId = r.OperationId,
            Title = r.Title,
            Body = r.Body,
            Kind = r.Kind,
            RightsStatus = r.RightsStatus,
            Provenance = r.Provenance,
            CreatedUtc = r.CreatedUtc
        });
        var latest = db.Revisions.Local.Single(r => r.ContentId == item.Id && r.Version == item.CurrentVersion);
        Record(scope, item, latest, request.OperationId, "content.imported.v1", fingerprint);
        await SaveAsync(ct); await tx.CommitAsync(ct); return await MutationViewAsync(scope, item, ct);
    }
    public async Task ShareAsync(CoreScope scope, Guid universe, ShareUniverse request, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await AccessAsync(scope, universe, Permission.Manage, ct);
        if (!await db.Tenants.AnyAsync(t => t.Id == request.GranteeTenantId, ct) || request.GranteeTenantId == scope.TenantId)
            throw new RuntimeValidationException("Select an existing different tenant.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, Permission.Manage, ct);
        var row = await db.Universes.FromSqlInterpolated($"SELECT * FROM [core].[Universes] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {universe} AND [TenantId] = {scope.TenantId}").SingleAsync(ct);
        byte[] expected;
        try { expected = Convert.FromBase64String(request.ExpectedRowVersion); } catch (Exception e) when (e is FormatException or ArgumentNullException) { throw new RuntimeValidationException("Invalid row version."); }
        if (!row.RowVersion.SequenceEqual(expected)) throw new CoreConcurrencyException();
        var grant = await db.Grants.SingleOrDefaultAsync(g => g.UniverseId == universe && g.GranteeTenantId == request.GranteeTenantId, ct);
        if (grant is null) { grant = new UniverseGrant { UniverseId = universe, GranteeTenantId = request.GranteeTenantId }; db.Grants.Add(grant); }
        grant.Read = request.Read; grant.Write = request.Write; grant.Export = request.Export;
        db.Entry(row).Property(u => u.UpdatedUtc).IsModified = true;
        await SaveAsync(ct); await tx.CommitAsync(ct);
    }
}


