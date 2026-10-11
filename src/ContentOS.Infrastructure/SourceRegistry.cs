using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed class SourceRegistry(CoreDbContext db, IContentCore core) : ISourceRegistry
{
    private async Task AccessAsync(CoreScope scope, Guid universe, int level, CancellationToken ct)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.TenantId == scope.TenantId && m.Actor == scope.Actor, ct);
        if (member == null || level == 2 && member.Permission != "Administrator" ||
            level == 1 && member.Permission is not ("Administrator" or "Editor") ||
            !await db.Universes.AnyAsync(u => u.Id == universe && u.TenantId == scope.TenantId, ct)) throw new CoreAccessException();
    }
    private static string Text(string value, int max = 200) { CoreRules.Text(value, max, "Source field"); return value.Trim(); }
    private static string Evidence(string value)
    {
        value = Text(value, 1000);
        if (!value.StartsWith("manual:", StringComparison.Ordinal) && !value.StartsWith("evidence:", StringComparison.Ordinal) && !value.StartsWith("fixture:", StringComparison.Ordinal))
            throw new RuntimeValidationException("A reviewed evidence reference is required; a URL alone is not permission.");
        return value;
    }
    private static void Expected(CoreRecord row, string value)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); } catch (Exception e) when (e is FormatException or ArgumentNullException) { throw new RuntimeValidationException("Invalid row version."); }
        if (!row.RowVersion.SequenceEqual(bytes)) throw new CoreConcurrencyException();
    }
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
    }
    private void Audit(CoreScope scope, Guid subject, string action) =>
        db.SourceAudits.Add(new SourceAudit { Id = Guid.NewGuid(), TenantId = scope.TenantId, SubjectId = subject, Actor = scope.Actor, Action = action });
    private async Task<Guid> CommandAsync<T>(CoreScope scope, Guid universe, Guid operation, string kind, T request, Func<Task<Guid>> action, CancellationToken ct, int level = 1)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, level, ct);
        if (operation == Guid.Empty) throw new RuntimeValidationException("A stable operation ID is required.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { universe, request }))));
        var prior = await db.SourceCommands.FromSqlInterpolated($"SELECT * FROM [sources].[SourceCommand] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {scope.TenantId} AND [OperationId] = {operation}").SingleOrDefaultAsync(ct);
        if (prior != null)
        {
            if (prior.Actor != scope.Actor || prior.Kind != kind || prior.Fingerprint != fingerprint) throw new RuntimeConflictException("Operation ID already belongs to a different source command.");
            return prior.ResultId;
        }
        var id = await action();
        db.SourceCommands.Add(new SourceCommand { Id = Guid.NewGuid(), TenantId = scope.TenantId, OperationId = operation, Actor = scope.Actor, Kind = kind, Fingerprint = fingerprint, ResultId = id });
        Audit(scope, id, kind); await SaveAsync(ct); await tx.CommitAsync(ct); return id;
    }
    private async Task<SourceWork> WorkAsync(Guid universe, Guid id, CancellationToken ct) =>
        await db.SourceWorks.SingleOrDefaultAsync(w => w.Id == id && w.UniverseId == universe, ct) ?? throw new CoreAccessException();
    private async Task<SourceEdition> EditionAsync(Guid universe, Guid id, CancellationToken ct) =>
        await db.SourceEditions.SingleOrDefaultAsync(e => e.Id == id && db.SourceWorks.Any(w => w.Id == e.WorkId && w.UniverseId == universe), ct) ?? throw new CoreAccessException();
    private static SourceWorkView View(SourceWork x) => new(x.Id, x.TenantId, x.UniverseId, x.Key, x.Title, x.Kind, x.Revoked, Convert.ToBase64String(x.RowVersion));
    private static EditionView View(SourceEdition x) => new(x.Id, x.WorkId, x.Key, x.Version, x.Language, x.Locator, x.PermissionId, x.Revoked, Convert.ToBase64String(x.RowVersion));
    private static SnapshotView View(SourceSnapshot x) => new(x.Id, x.EditionId, x.FileName, x.Sha256, x.Length, x.Text != null, x.EvidenceReference, x.ReviewStatus, x.RetrievedUtc, x.CreatedUtc, Convert.ToBase64String(x.RowVersion));
    private async Task<PermissionView> ViewAsync(SourcePermission p, CancellationToken ct) => new(p.Id, p.EditionId, p.Version, p.Status, p.EvidenceReference,
        await db.PermissionLanguages.Where(x => x.PermissionId == p.Id).Select(x => x.Value).ToListAsync(ct),
        await db.PermissionTerritories.Where(x => x.PermissionId == p.Id).Select(x => x.Value).ToListAsync(ct),
        await db.PermissionFormats.Where(x => x.PermissionId == p.Id).Select(x => x.Value).ToListAsync(ct),
        p.Commercial, p.Adaptation, p.Translation, p.AiProcessing, p.SnapshotRetention, p.RequiredAttribution, p.ExpiresUtc, Convert.ToBase64String(p.RowVersion));
    public Task<Guid> RegisterAsync(CoreScope scope, Guid universe, RegisterSource r, CancellationToken ct) =>
        CommandAsync(scope, universe, r.OperationId, "source.registered", r, async () =>
        {
            var key = Text(r.Key); var title = Text(r.Title);
            if (r.Kind is not ("Canonical" or "Translation" or "Interpretation" or "CurrentEvent" or "Fiction" or "Synthetic")) throw new RuntimeValidationException("Invalid source kind.");
            var existing = await db.SourceWorks.SingleOrDefaultAsync(w => w.UniverseId == universe && w.Key == key, ct);
            if (existing != null)
            {
                if (existing.Title != title || existing.Kind != r.Kind) throw new RuntimeConflictException("Register a new key for different work metadata.");
                return existing.Id;
            }
            var w = new SourceWork { Id = Guid.NewGuid(), TenantId = scope.TenantId, UniverseId = universe, Key = key, Title = title, Kind = r.Kind };
            db.SourceWorks.Add(w); return w.Id;
        }, ct);
    private async Task<Guid> PartyAsync(CoreScope scope, string name, CancellationToken ct)
    {
        name = Text(name);
        var existing = db.SourceParties.Local.SingleOrDefault(x => x.TenantId == scope.TenantId && x.Name == name) ??
            await db.SourceParties.SingleOrDefaultAsync(x => x.TenantId == scope.TenantId && x.Name == name, ct);
        if (existing != null) return existing.Id;
        var party = new SourceParty { Id = Guid.NewGuid(), TenantId = scope.TenantId, Name = name }; db.SourceParties.Add(party); return party.Id;
    }
    public Task<Guid> EditionAsync(CoreScope scope, Guid universe, Guid work, RegisterEdition r, CancellationToken ct) =>
        CommandAsync(scope, universe, r.OperationId, "source.edition", new { work, r }, async () =>
        {
            var w = await WorkAsync(universe, work, ct);
            if (w.Revoked) throw new RightsBlockedException("Revoked");
            var key = Text(r.Key); var locator = Text(r.Locator, 1000); var citation = Text(r.CitationFormat, 1000);
            if (!citation.Contains("{title}", StringComparison.Ordinal) || !citation.Contains("{locator}", StringComparison.Ordinal)) throw new RuntimeValidationException("Citation format requires title and locator tokens.");
            if (!await db.Locales.AnyAsync(l => l.Id == r.Language, ct)) throw new RuntimeValidationException("Unknown source locale.");
            var author = await PartyAsync(scope, r.Author, ct); var publisher = await PartyAsync(scope, r.Publisher, ct);
            Guid? translator = r.Translator == null ? null : await PartyAsync(scope, r.Translator, ct);
            var prior = await db.SourceEditions.SingleOrDefaultAsync(e => e.WorkId == work && e.Key == key, ct);
            if (prior != null)
            {
                if (prior.AuthorId != author || prior.PublisherId != publisher || prior.TranslatorId != translator || prior.Language != r.Language || prior.Locator != locator || prior.CitationFormat != citation)
                    throw new RuntimeConflictException("Edition metadata is immutable; register a new edition key.");
                return prior.Id;
            }
            var e = new SourceEdition
            {
                Id = Guid.NewGuid(),
                WorkId = work,
                Key = key,
                Version = (await db.SourceEditions.Where(e => e.WorkId == work).MaxAsync(e => (int?)e.Version, ct) ?? 0) + 1,
                AuthorId = author,
                PublisherId = publisher,
                TranslatorId = translator,
                Language = r.Language,
                Locator = locator,
                CitationFormat = citation
            };
            db.SourceEditions.Add(e); return e.Id;
        }, ct);
    public Task<Guid> PermissionAsync(CoreScope scope, Guid universe, Guid edition, GrantSourcePermission r, CancellationToken ct) =>
        CommandAsync(scope, universe, r.OperationId, "source.permission", new { edition, r }, async () =>
        {
            var e = await EditionAsync(universe, edition, ct);
            if (e.Revoked || (await WorkAsync(universe, e.WorkId, ct)).Revoked) throw new RightsBlockedException("Revoked");
            if (r.Languages == null || r.Territories == null || r.Formats == null || r.Languages.Length is < 1 or > 30 || r.Territories.Length is < 1 or > 30 || r.Formats.Length is < 1 or > 5)
                throw new RuntimeValidationException("Bounded explicit permission dimensions are required.");
            foreach (var l in r.Languages) if (!await db.Locales.AnyAsync(x => x.Id == l, ct)) throw new RuntimeValidationException("Unknown permission locale.");
            foreach (var t in r.Territories) Text(t, 40);
            foreach (var f in r.Formats) SourceRules.Use("Read", e.Language, r.Territories[0], f);
            if (r.ExpiresUtc <= DateTimeOffset.UtcNow) throw new RuntimeValidationException("New permission expiry must be in the future.");
            var p = new SourcePermission
            {
                Id = Guid.NewGuid(),
                EditionId = edition,
                Version = (await db.SourcePermissions.Where(p => p.EditionId == edition).MaxAsync(p => (int?)p.Version, ct) ?? 0) + 1,
                EvidenceReference = Evidence(r.EvidenceReference),
                Commercial = r.Commercial,
                Adaptation = r.Adaptation,
                Translation = r.Translation,
                AiProcessing = r.AiProcessing,
                SnapshotRetention = r.SnapshotRetention,
                RequiredAttribution = Text(r.RequiredAttribution, 1000),
                ExpiresUtc = r.ExpiresUtc
            };
            db.SourcePermissions.Add(p);
            foreach (var v in r.Languages.Distinct()) db.PermissionLanguages.Add(new PermissionLanguage { PermissionId = p.Id, Value = v });
            foreach (var v in r.Territories.Distinct()) db.PermissionTerritories.Add(new PermissionTerritory { PermissionId = p.Id, Value = v });
            foreach (var v in r.Formats.Distinct()) db.PermissionFormats.Add(new PermissionFormat { PermissionId = p.Id, Value = v });
            e.PermissionId = p.Id; return p.Id;
        }, ct, 2);
    private async Task ReviewAsync(CoreScope scope, Guid universe, Guid id, ReviewSource r, bool permission, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, 2, ct);
        if (r.Status is not ("Approved" or "Rejected")) throw new RuntimeValidationException("Review must explicitly approve or reject.");
        if (permission)
        {
            var p = await db.SourcePermissions.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new CoreAccessException();
            var e = await EditionAsync(universe, p.EditionId, ct);
            if (e.PermissionId != id || e.Revoked || (await WorkAsync(universe, e.WorkId, ct)).Revoked) throw new RuntimeConflictException("Only the current, unrevoked permission can be reviewed.");
            Expected(p, r.ExpectedRowVersion);
            if (p.Status != "Pending") throw new RuntimeConflictException("Reviewed permissions are immutable; add a new permission version.");
            p.Status = r.Status; p.ReviewedBy = scope.Actor; p.ReviewedUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            var s = await db.SourceSnapshots.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new CoreAccessException();
            await EditionAsync(universe, s.EditionId, ct); Expected(s, r.ExpectedRowVersion);
            if (s.ReviewStatus != "Pending") throw new RuntimeConflictException("Snapshot already reviewed.");
            s.ReviewStatus = r.Status; s.ReviewedBy = scope.Actor; s.ReviewedUtc = DateTimeOffset.UtcNow;
        }
        Audit(scope, id, permission ? "permission.reviewed" : "snapshot.reviewed"); await SaveAsync(ct); await tx.CommitAsync(ct);
    }
    public Task ReviewPermissionAsync(CoreScope s, Guid u, Guid id, ReviewSource r, CancellationToken ct) => ReviewAsync(s, u, id, r, true, ct);
    public Task ReviewSnapshotAsync(CoreScope s, Guid u, Guid id, ReviewSource r, CancellationToken ct) => ReviewAsync(s, u, id, r, false, ct);
    public Task<Guid> ImportAsync(CoreScope scope, Guid universe, Guid edition, ImportSourceFile r, CancellationToken ct) =>
        CommandAsync(scope, universe, r.OperationId, "source.file-imported", new { edition, r }, async () =>
        {
            var e = await EditionAsync(universe, edition, ct);
            if (e.Revoked || (await WorkAsync(universe, e.WorkId, ct)).Revoked) throw new RightsBlockedException("Revoked");
            if (r.Content == null || r.Content.Length is < 1 or > 65536 || string.IsNullOrWhiteSpace(r.FileName) || r.FileName.Length > 150 ||
                r.FileName.Contains('/') || r.FileName.Contains('\\') || r.FileName.Contains(':') || !r.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                throw new RuntimeValidationException("Import one UTF-8 .txt file, at most 64 KiB; paths are not accepted.");
            string text;
            try { text = new UTF8Encoding(false, true).GetString(r.Content); } catch (DecoderFallbackException) { throw new RuntimeValidationException("File is not valid UTF-8."); }
            var hash = Convert.ToHexString(SHA256.HashData(r.Content));
            if (!hash.Equals(r.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) throw new RuntimeValidationException("File checksum does not match.");
            var evidence = Evidence(r.EvidenceReference);
            if (r.RetrievedUtc > DateTimeOffset.UtcNow) throw new RuntimeValidationException("Retrieval time cannot be in the future.");
            var prior = await db.SourceSnapshots.SingleOrDefaultAsync(s => s.EditionId == edition && s.Sha256 == hash, ct);
            if (prior != null) return prior.Id;
            SourcePermission? p = e.PermissionId is Guid permissionId ? await db.SourcePermissions.SingleAsync(p => p.Id == permissionId, ct) : null;
            if (r.RetainFullSnapshot && (p == null || p.Status != "Approved" || !p.SnapshotRetention || p.ExpiresUtc <= DateTimeOffset.UtcNow))
                throw new RightsBlockedException("SnapshotRetentionNotPermitted");
            var s = new SourceSnapshot
            {
                Id = Guid.NewGuid(),
                EditionId = edition,
                ImportPermissionId = r.RetainFullSnapshot ? p!.Id : null,
                FileName = r.FileName,
                Sha256 = hash,
                Length = r.Content.Length,
                Text = r.RetainFullSnapshot ? text : null,
                EvidenceReference = evidence,
                RetrievedUtc = r.RetrievedUtc ?? DateTimeOffset.UtcNow
            };
            db.SourceSnapshots.Add(s); return s.Id;
        }, ct);
    public async Task<IReadOnlyList<SourceWorkView>> ListAsync(CoreScope scope, Guid universe, CancellationToken ct)
    {
        await AccessAsync(scope, universe, 0, ct);
        return (await db.SourceWorks.AsNoTracking().Where(w => w.UniverseId == universe).OrderBy(w => w.CreatedUtc).Take(100).ToListAsync(ct)).Select(View).ToArray();
    }
    public async Task<SourceDetail> InspectAsync(CoreScope scope, Guid universe, Guid work, CancellationToken ct)
    {
        await AccessAsync(scope, universe, 0, ct); var w = await WorkAsync(universe, work, ct);
        var editions = await db.SourceEditions.AsNoTracking().Where(e => e.WorkId == work).OrderBy(e => e.Version).Take(100).ToListAsync(ct);
        var ids = editions.Select(e => e.Id).ToArray();
        var permissions = new List<PermissionView>();
        foreach (var p in await db.SourcePermissions.AsNoTracking().Where(p => ids.Contains(p.EditionId)).Take(100).ToListAsync(ct)) permissions.Add(await ViewAsync(p, ct));
        return new(View(w), editions.Select(View).ToArray(), (await db.SourceSnapshots.AsNoTracking().Where(s => ids.Contains(s.EditionId)).Take(100).ToListAsync(ct)).Select(View).ToArray(), permissions);
    }
    public async Task<SourceReviewQueue> ReviewQueueAsync(CoreScope scope, Guid universe, CancellationToken ct)
    {
        await AccessAsync(scope, universe, 0, ct);
        var editions = db.SourceEditions.Where(e => db.SourceWorks.Any(w => w.Id == e.WorkId && w.UniverseId == universe)).Select(e => e.Id);
        var permissions = new List<PermissionView>();
        foreach (var p in await db.SourcePermissions.AsNoTracking().Where(p => editions.Contains(p.EditionId) && p.Status == "Pending").Take(100).ToListAsync(ct)) permissions.Add(await ViewAsync(p, ct));
        return new((await db.SourceSnapshots.AsNoTracking().Where(s => editions.Contains(s.EditionId) && s.ReviewStatus == "Pending").Take(100).ToListAsync(ct)).Select(View).ToArray(), permissions);
    }
    public async Task<RightsDecisionView> EvaluateAsync(CoreScope scope, Guid universe, Guid snapshot, IntendedUse use, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, 0, ct);
        var s = await db.SourceSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == snapshot, ct) ?? throw new CoreAccessException();
        await EditionAsync(universe, s.EditionId, ct);
        var result = await new SourceRightsGuard(db).DecideAsync(scope, snapshot, use, ct);
        db.RightsDecisions.Add(result.Decision); await SaveAsync(ct); await tx.CommitAsync(ct);
        return new(result.Decision.Id, snapshot, result.Decision.PermissionId, result.Decision.Allowed, result.Decision.Reason, use, result.Decision.CreatedUtc);
    }
    private async Task<ContentItem> RevisionContentAsync(CoreScope scope, Guid universe, Guid revision, CancellationToken ct) =>
        await (from r in db.Revisions
               join c in db.Contents on r.ContentId equals c.Id
               where r.Id == revision && c.UniverseId == universe && c.TenantId == scope.TenantId
               select c).SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
    public Task<Guid> LinkAsync(CoreScope scope, Guid universe, LinkEvidence r, CancellationToken ct) =>
        CommandAsync(scope, universe, r.OperationId, "source.linked", r, async () =>
        {
            if (r.SnapshotId.HasValue == r.ParentRevisionId.HasValue || r.ContentRevisionId.HasValue == r.AssetReferenceId.HasValue) throw new RuntimeValidationException("Select exactly one origin and one target.");
            Text(r.Relation);
            ContentItem target; Guid targetRevision;
            if (r.ContentRevisionId is Guid revision) { targetRevision = revision; target = await RevisionContentAsync(scope, universe, revision, ct); }
            else
            {
                var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == r.AssetReferenceId && a.TenantId == scope.TenantId, ct) ?? throw new CoreAccessException();
                targetRevision = asset.ContentRevisionId; target = await RevisionContentAsync(scope, universe, asset.ContentRevisionId, ct);
            }
            if (target.EditorialState is not ("Draft" or "Review")) throw new RuntimeConflictException("Evidence must be linked before approval.");
            if (r.SnapshotId is Guid snapshot)
            {
                var s = await db.SourceSnapshots.SingleOrDefaultAsync(s => s.Id == snapshot, ct) ?? throw new CoreAccessException();
                await EditionAsync(universe, s.EditionId, ct);
            }
            if (r.ParentRevisionId is Guid parent)
            {
                await RevisionContentAsync(scope, universe, parent, ct);
                var ancestry = await new SourceRightsGuard(db).LineageAsync(parent, ct);
                if (parent == targetRevision || ancestry.Edges.Any(e => e.ParentRevisionId == targetRevision || e.ContentRevisionId == targetRevision))
                    throw new RuntimeValidationException("Provenance cycles are forbidden.");
            }
            var edge = new ProvenanceEdge
            {
                Id = Guid.NewGuid(),
                TenantId = scope.TenantId,
                SnapshotId = r.SnapshotId,
                ParentRevisionId = r.ParentRevisionId,
                ContentRevisionId = r.ContentRevisionId,
                AssetReferenceId = r.AssetReferenceId,
                Relation = r.Relation
            };
            db.Provenance.Add(edge); return edge.Id;
        }, ct);
    public Task<Guid> RecordUseAsync(CoreScope scope, Guid universe, RecordSourceUse r, CancellationToken ct) =>
        CommandAsync(scope, universe, r.OperationId, "source.use-recorded", r, async () =>
        {
            await RevisionContentAsync(scope, universe, r.ContentRevisionId, ct);
            SourceRules.Use(r.Use.Purpose, r.Use.Language, r.Use.Territory, r.Use.Format);
            if (r.Published && r.Use.Purpose != "Publish" || r.Mode is not ("Fixture" or "ReportedExisting")) throw new RuntimeValidationException("Published ledger entries require Publish and Fixture or ReportedExisting mode.");
            var reference = Text(r.ExternalReference, 1000);
            await new SourceRightsGuard(db).CheckAsync(scope, r.ContentRevisionId, r.Use, ct);
            var gate = await new EvidenceEngine(db).CheckContentAsync(scope, r.ContentRevisionId, r.Use, ct);
            if (!gate.Eligible) throw new EvidenceBlockedException(gate.Reason);
            var use = new SourceUse
            {
                Id = Guid.NewGuid(),
                TenantId = scope.TenantId,
                OperationId = r.OperationId,
                ContentRevisionId = r.ContentRevisionId,
                Purpose = r.Use.Purpose,
                Published = r.Published,
                ExternalReference = reference,
                Mode = r.Mode
            };
            db.SourceUses.Add(use); return use.Id;
        }, ct);
    private async Task<List<ImpactedDerivative>> ImpactAsync(Guid universe, Guid work, Guid? edition, CancellationToken ct)
    {
        var snapshots = await db.SourceSnapshots.Where(s => db.SourceEditions.Any(e => e.Id == s.EditionId && e.WorkId == work && (edition == null || e.Id == edition))).Select(s => s.Id).ToListAsync(ct);
        var all = await db.Provenance.AsNoTracking().Where(e => db.Contents.Any(c => c.UniverseId == universe &&
            (db.Revisions.Any(r => r.ContentId == c.Id && (r.Id == e.ContentRevisionId || r.Id == e.ParentRevisionId)) ||
             db.Assets.Any(a => a.Id == e.AssetReferenceId && db.Revisions.Any(r => r.Id == a.ContentRevisionId && r.ContentId == c.Id))))).Take(10001).ToListAsync(ct);
        if (all.Count > 10000) throw new RuntimeValidationException("Impact exceeds the local 10,000-edge limit.");
        var revisions = new HashSet<Guid>(); var assets = new HashSet<Guid>(); bool changed;
        do
        {
            changed = false;
            foreach (var e in all.Where(e => e.SnapshotId is Guid s && snapshots.Contains(s) || e.ParentRevisionId is Guid p && revisions.Contains(p)))
            {
                if (e.ContentRevisionId is Guid r) changed |= revisions.Add(r);
                if (e.AssetReferenceId is Guid a) changed |= assets.Add(a);
            }
            if (assets.Count > 0)
                foreach (var revision in await db.Assets.Where(a => assets.Contains(a.Id)).Select(a => a.ContentRevisionId).ToListAsync(ct))
                    changed |= revisions.Add(revision);
        } while (changed);
        return revisions.Select(r => new ImpactedDerivative(r, null)).Concat(assets.Select(a => new ImpactedDerivative(null, a))).ToList();
    }
    public async Task<RevocationImpact> RevokeAsync(CoreScope scope, Guid universe, Guid work, RevokeSource r, CancellationToken ct)
    {
        var id = await CommandAsync(scope, universe, r.OperationId, "source.revoked", new { work, r }, async () =>
        {
            var w = await WorkAsync(universe, work, ct);
            if (r.EditionId is Guid edition)
            {
                var e = await EditionAsync(universe, edition, ct); if (e.WorkId != work) throw new CoreAccessException();
                Expected(e, r.ExpectedRowVersion); e.Revoked = true;
            }
            else { Expected(w, r.ExpectedRowVersion); w.Revoked = true; }
            var revocation = new SourceRevocation { Id = Guid.NewGuid(), TenantId = scope.TenantId, WorkId = work, EditionId = r.EditionId, OperationId = r.OperationId, Reason = Text(r.Reason, 1000), Actor = scope.Actor };
            db.SourceRevocations.Add(revocation);
            var impacted = await ImpactAsync(universe, work, r.EditionId, ct);
            var revisions = impacted.Where(x => x.ContentRevisionId != null).Select(x => x.ContentRevisionId!.Value).ToArray();
            foreach (var use in await db.SourceUses.Where(x => x.TenantId == scope.TenantId && x.Published && revisions.Contains(x.ContentRevisionId)).ToListAsync(ct))
                db.Remediation.Add(new RemediationTask { Id = Guid.NewGuid(), RevocationId = revocation.Id, SourceUseId = use.Id });
            return revocation.Id;
        }, ct, 2);
        var snapshots = db.SourceSnapshots.Where(s => db.SourceEditions.Any(e => e.Id == s.EditionId && e.WorkId == work && (r.EditionId == null || e.Id == r.EditionId))).Select(s => s.Id);
        var corpus = await (from t in db.CorpusTexts.AsNoTracking()
                            join v in db.CorpusVerses on t.VerseId equals v.Id
                            join m in db.CorpusManifests on v.ManifestId equals m.Id
                            where m.UniverseId == universe && snapshots.Contains(t.SnapshotId)
                            select new CorpusDerivative(m.Id, v.Id, t.Kind)).Take(10001).ToListAsync(ct);
        var annotations = await (from a in db.CorpusAnnotations.AsNoTracking()
                                 join v in db.CorpusVerses on a.VerseId equals v.Id
                                 join m in db.CorpusManifests on v.ManifestId equals m.Id
                                 where m.UniverseId == universe && snapshots.Contains(a.SnapshotId)
                                 select new CorpusDerivative(m.Id, v.Id, a.Kind)).Take(10001).ToListAsync(ct);
        return new(id, await ImpactAsync(universe, work, r.EditionId, ct), (await RemediationAsync(scope, universe, ct)).Where(x => x.RevocationId == id).ToArray(), corpus.Concat(annotations).ToArray());
    }
    public async Task<IReadOnlyList<RemediationView>> RemediationAsync(CoreScope scope, Guid universe, CancellationToken ct)
    {
        await AccessAsync(scope, universe, 0, ct);
        return await (from t in db.Remediation.AsNoTracking()
                      join u in db.SourceUses on t.SourceUseId equals u.Id
                      join r in db.Revisions on u.ContentRevisionId equals r.Id
                      join c in db.Contents on r.ContentId equals c.Id
                      where c.UniverseId == universe && c.TenantId == scope.TenantId
                      select new RemediationView(t.Id, t.RevocationId, u.Id, r.Id, u.ExternalReference, u.Mode, t.Status, t.ResolutionEvidence, Convert.ToBase64String(t.RowVersion))).Take(100).ToListAsync(ct);
    }
    public async Task ResolveAsync(CoreScope scope, Guid universe, Guid remediation, ResolveRemediation r, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(scope, universe, 2, ct);
        if (!(await RemediationAsync(scope, universe, ct)).Any(x => x.Id == remediation)) throw new CoreAccessException();
        var t = await db.Remediation.SingleAsync(t => t.Id == remediation, ct); Expected(t, r.ExpectedRowVersion);
        if (t.Status != "Open") throw new RuntimeConflictException("Task is already resolved.");
        t.Status = "Resolved"; t.ResolutionEvidence = Evidence(r.EvidenceReference); Audit(scope, t.Id, "source.remediation-resolved");
        await SaveAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<SourceAwareExport> ExportAsync(CoreScope scope, Guid universe, Guid content, IntendedUse use, CancellationToken ct)
    {
        await AccessAsync(scope, universe, 0, ct);
        return new(1, await core.ExportForUseAsync(scope, universe, content, use, ct), use);
    }
}




