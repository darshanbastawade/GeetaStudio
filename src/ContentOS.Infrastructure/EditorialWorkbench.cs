using System.Data;
using System.Security.Cryptography;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed partial class EditorialWorkbench(CoreDbContext db, IQualityEvaluation evaluation, IUniverseRegistry registry, ILessonEngine lessons,
    IAiTaskRouter ai, IAiAdministration aiAdmin, IAssetStore assetStore, ISecretResolver secrets) : IEditorialWorkbench
{
    private static string Json<T>(T v) => EditorialBinding.Json(v);
    private static T Read<T>(string v) => EditorialBinding.Read<T>(v);
    private async Task<string> Access(CoreScope s, Guid u, string permission, CancellationToken ct)
    {
        if (!await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
        var role = await db.Members.Where(x => x.TenantId == s.TenantId && x.Actor == s.Actor).Select(x => x.Permission).SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
        var allowed = permission switch { "Read" => true, "Write" => role is "Author" or "Editor" or "Administrator", "Review" => role is "Editor" or "Administrator", "Publish" => role is "Publisher" or "Administrator", "Admin" => role == "Administrator", _ => false };
        if (!allowed) throw new CoreAccessException(); return role;
    }
    private async Task<Guid> Command<T>(CoreScope s, Guid u, Guid op, string kind, T request, string permission, Func<Task<Guid>> action, CancellationToken ct)
    {
        if (op == Guid.Empty) throw new RuntimeValidationException("Stable operation required."); db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.Editorial:" + s.TenantId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Editorial lock unavailable',1;", ct);
        await Access(s, u, permission, ct); var fingerprint = EditorialBinding.Hash(new { u, request });
        var prior = await db.EditorialOperations.SingleOrDefaultAsync(x => x.TenantId == s.TenantId && x.OperationId == op, ct);
        if (prior != null) { if (prior.Actor != s.Actor || prior.Kind != kind || prior.Fingerprint != fingerprint) throw new RuntimeConflictException("Editorial replay differs."); return prior.ResultId; }
        var id = await action(); db.EditorialOperations.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, OperationId = op, Kind = kind, Actor = s.Actor, Fingerprint = fingerprint, ResultId = id });
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = "editorial." + kind });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
        await tx.CommitAsync(ct); return id;
    }
    private async Task<EditorialItem> Item(CoreScope s, Guid u, Guid id, CancellationToken ct) => await db.EditorialItems.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
    private static void Expected(CoreRecord row, string expected) { byte[] value; try { value = Convert.FromBase64String(expected); } catch (FormatException) { throw new RuntimeValidationException("Invalid rowversion."); } if (!row.RowVersion.SequenceEqual(value)) throw new CoreConcurrencyException(); }
    private async Task Author(CoreScope s, Guid u, EditorialItem row, CancellationToken ct) { var role = await Access(s, u, "Write", ct); if (role == "Author" && row.Author != s.Actor) throw new CoreAccessException(); }
    private void History(CoreScope s, EditorialItem item, string action, string evidence) => db.EditorialHistories.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = item.UniverseId, ItemId = item.Id, Actor = s.Actor, Action = action, EvidenceReference = evidence });
    private async Task<ContentItem> Head(EditorialItem item, CancellationToken ct) => await db.Contents.SingleAsync(x => x.Id == item.ContentId, ct);
    private static void Invalidate(EditorialItem item, ContentItem head) { item.ApprovalId = null; item.EvaluationRunId = null; item.EvaluationCaseKey = null; item.ScheduledUtc = null; item.State = "Draft"; head.EditorialState = "Draft"; head.PublicKnowledge = false; }
    public async Task<EditorialPacket> StartAsync(CoreScope s, Guid u, StartEditorial r, CancellationToken ct)
    {
        var id = await Command(s, u, r.OperationId, "start", r, "Write", async () =>
        {
            CoreRules.Text(r.Key, 100, "Editorial key"); CoreRules.Text(r.Brief, 4000, "Brief"); if (!EditorialRules.Formats.Contains(r.Format) || (r.ContentId.HasValue) == (r.Draft != null)) throw new RuntimeValidationException("Choose an existing owned content or a new draft.");
            ContentItem head;
            if (r.ContentId.HasValue)
            {
                await Access(s, u, "Review", ct); head = await db.Contents.SingleOrDefaultAsync(x => x.Id == r.ContentId && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
                if (await db.EditorialItems.AnyAsync(x => x.ContentId == head.Id, ct)) throw new RuntimeConflictException("Content already has an editorial workbench.");
                head.EditorialState = "Draft"; head.PublicKnowledge = false;
            }
            else
            {
                var draft = r.Draft!; CoreRules.Revision(draft.Title, draft.Body, draft.Kind, draft.RightsStatus, draft.Provenance, false);
                if (!await db.Subjects.AnyAsync(x => x.Id == draft.SubjectId && x.UniverseId == u, ct) || !await db.Audiences.AnyAsync(x => x.Id == draft.AudienceId && x.UniverseId == u, ct) || !await db.Locales.AnyAsync(x => x.Id == draft.LocaleId, ct)) throw new CoreAccessException();
                head = new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, SubjectId = draft.SubjectId, AudienceId = draft.AudienceId, LocaleId = draft.LocaleId, CurrentVersion = 1 };
                db.Contents.Add(head); db.Revisions.Add(new() { Id = Guid.NewGuid(), ContentId = head.Id, Version = 1, OperationId = r.OperationId, Title = draft.Title, Body = draft.Body, Kind = draft.Kind, RightsStatus = draft.RightsStatus, Provenance = draft.Provenance });
            }
            var item = new EditorialItem { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ContentId = head.Id, Author = s.Actor, Key = r.Key, Brief = r.Brief, Format = r.Format, State = r.Draft == null ? "Draft" : "Brief", PublicIntended = r.PublicIntended };
            db.EditorialItems.Add(item); History(s, item, "Created", "Owned staff brief; not published."); return item.Id;
        }, ct); return await InspectAsync(s, u, id, ct);
    }
    public async Task<IReadOnlyList<EditorialSummary>> ListAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, "Read", ct); var rows = await db.EditorialItems.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.UpdatedUtc).Take(201).ToListAsync(ct);
        if (rows.Count > 200) throw new RuntimeValidationException("Editorial list capacity200; pagination required.");
        return rows.Select(x => new EditorialSummary(x.Id, x.Key, x.Format, x.State, x.ContentId, x.Author, Convert.ToBase64String(x.RowVersion))).ToArray();
    }
    public async Task<EditorialPacket> EditAsync(CoreScope s, Guid u, Guid id, EditEditorial r, CancellationToken ct)
    {
        await Command(s, u, r.OperationId, "edit", new { id, r }, "Write", async () =>
        {
            var item = await Item(s, u, id, ct); await Author(s, u, item, ct); Expected(item, r.ExpectedRowVersion); var head = await Head(item, ct); var old = await EditorialBinding.Revision(db, item, ct);
            CoreRules.Revision(r.Title, r.Body, r.Kind, r.RightsStatus, r.Provenance, false); head.CurrentVersion++;
            var revision = new ContentRevision { Id = Guid.NewGuid(), ContentId = head.Id, Version = head.CurrentVersion, OperationId = r.OperationId, Title = r.Title, Body = r.Body, Kind = r.Kind, RightsStatus = r.RightsStatus, Provenance = r.Provenance };
            db.Revisions.Add(revision); db.Provenance.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, ContentRevisionId = revision.Id, ParentRevisionId = old.Id, Relation = "revision-of" });
            if (old.Body == r.Body) foreach (var link in await db.ClaimLinks.AsNoTracking().Where(x => x.ContentRevisionId == old.Id).ToListAsync(ct)) db.ClaimLinks.Add(new() { Id = Guid.NewGuid(), ClaimRevisionId = link.ClaimRevisionId, ContentRevisionId = revision.Id, Start = link.Start, Length = link.Length });
            Invalidate(item, head); History(s, item, "Edited; approval invalidated", r.Provenance); return item.Id;
        }, ct); return await InspectAsync(s, u, id, ct);
    }
    private async Task<RegistryEpisodeSnapshot[]> ProductionCasts(CoreScope s, EditorialItem item, Guid[] related, CancellationToken ct)
    {
        var rows = await db.RegistryRevisions.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == item.UniverseId && x.Kind == "Story").ToListAsync(ct);
        var stories = rows.Select(x => Read<RegistryEpisodeSnapshot>(x.RulesJson)).Where(x => x.ContentRevisionId.HasValue && related.Contains(x.ContentRevisionId.Value));
        var factory = await db.FactoryArtifacts.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == item.UniverseId && related.Contains(x.CoreRevisionId)).Select(x => x.SnapshotJson).ToListAsync(ct);
        return stories.Concat(factory.Select(x => Read<FactoryArtifactSnapshot>(x).Input.Cast)).ToArray();
    }
    private async Task EvaluationContext(CoreScope s, EditorialItem item, ContentItem head, ContentRevision revision, EvaluationCaseResult result, CancellationToken ct)
    {
        if (result.Case?.AudienceId != head.AudienceId || result.Locale != head.LocaleId || result.Output?.Body != revision.Body)
            throw new EvidenceBlockedException("EvaluationAudienceOrRevisionMismatch");
        var lineage = await new SourceRightsGuard(db).LineageAsync(revision.Id, ct);
        var related = lineage.Edges.Where(x => x.ParentRevisionId.HasValue).Select(x => x.ParentRevisionId!.Value).Append(revision.Id).Distinct().ToArray();
        var cast = (await ProductionCasts(s, item, related, ct)).SelectMany(x => x.Cast).Select(x =>
            new EvaluationCastMember(x.Character.CharacterId, x.Character.Profile.Id, x.Role, x.Presentation?.Rules.VoiceAnchor ?? x.Character.Profile.Rules.VoiceAnchor, x.Character.Profile.Rules.LifeStage)).Distinct().ToArray();
        if (cast.Length > 0 && (result.Case.Cast == null || result.Output!.Cast.Length != cast.Length || cast.Any(x => !result.Output.Cast.Contains(x))))
            throw new EvidenceBlockedException("EvaluationProductionCastMismatch");
    }
    private async Task<(string[] Blocks, CitationExport[] Citations)> Rights(CoreScope s, EditorialItem item, string purpose, CancellationToken ct)
    {
        var revision = await EditorialBinding.Revision(db, item, ct); var head = await Head(item, ct); var lineage = await new SourceRightsGuard(db).LineageAsync(revision.Id, ct); var blocks = new List<string>(); var citations = new List<CitationExport>();
        if (lineage.Snapshots.Count == 0 && (revision.Kind is not ("Fiction" or "Synthetic") || revision.RightsStatus is not ("Owned" or "Synthetic"))) blocks.Add("MissingReviewedSourceEvidence");
        foreach (var source in lineage.Snapshots)
        {
            var d = await new SourceRightsGuard(db).DecideAsync(s, source, new(purpose, head.LocaleId, "IN", "Text", Adaptation: purpose != "Read" && revision.Kind is "Fiction" or "Synthetic" or "Interpretation"), ct);
            if (!d.Decision.Allowed) blocks.Add("Rights:" + d.Decision.Reason); else if (d.Citation != null) citations.Add(d.Citation);
        }
        var hasAiFactory = await db.FactoryArtifacts.AnyAsync(a => a.TenantId == s.TenantId && a.UniverseId == item.UniverseId && a.SnapshotJson.Contains("\"Generation\":") && (a.CoreRevisionId == revision.Id || lineage.Edges.Select(e => e.ParentRevisionId).Contains(a.CoreRevisionId)), ct);
        if (hasAiFactory) foreach (var source in lineage.Snapshots)
            if (!(await new SourceRightsGuard(db).DecideAsync(s, source, new("Generate", head.LocaleId, "IN", "Text", Adaptation: true, AiProcessing: true), ct)).Decision.Allowed) blocks.Add("Rights:FactoryAiPermissionWithdrawn");
        var gate = await new EvidenceEngine(db).CheckContentAsync(s, revision.Id, new(purpose, head.LocaleId, "IN", "Text"), ct); if (!gate.Eligible) blocks.Add("Evidence:" + gate.Reason);
        if (await db.EditorialCorrectionTasks.AnyAsync(x => x.ContentId == item.ContentId && x.Kind == "Internal" && x.State != "Resolved", ct)) blocks.Add("UnresolvedCorrection");
        foreach (var a in await db.Assets.AsNoTracking().Where(x => x.ContentRevisionId == revision.Id).ToListAsync(ct))
            if (a.RightsStatus is not ("Owned" or "Licensed" or "Synthetic") || await EditorialBinding.Storage(db, a.StorageAssetId, ct) == null) blocks.Add("AssetRightsOrAvailability");
        var related = lineage.Edges.Where(x => x.ParentRevisionId.HasValue).Select(x => x.ParentRevisionId!.Value).Append(revision.Id).ToArray();
        foreach (var story in await ProductionCasts(s, item, related, ct))
        {
            RegistryCastPlan plan;
            try { plan = await registry.CastAsync(s, item.UniverseId, story.Brief with { Mode = "Draft", PinnedChoices = story.Cast.Select(x => new PinnedCastChoice(x.Character.CharacterId, x.Role, x.Presentation?.Id)).ToArray() }, ct); }
            catch (RuntimeValidationException) { blocks.Add("CurrentApprovedCastRequired"); continue; }
            foreach (var cast in story.Cast)
            {
                var current = plan.Candidates.SingleOrDefault(x => x.Character.CharacterId == cast.Character.CharacterId && x.Role == cast.Role);
                if (current == null || !current.Eligible || current.Character.Status != "Active" || current.Character.Profile.ReviewStatus != "Approved" || current.Character.Profile.Id != cast.Character.Profile.Id || current.Presentation?.Id != cast.Presentation?.Id)
                    blocks.Add("CurrentApprovedCastRequired");
            }
            if (plan.MissingRoles.Length != 0) blocks.Add("CurrentApprovedCastRequired");
        }
        return (blocks.Distinct().ToArray(), citations.ToArray());
    }
    public async Task<EditorialPacket> ActAsync(CoreScope s, Guid u, Guid id, EditorialAction r, CancellationToken ct)
    {
        var permission = r.Action is "Schedule" or "PublishDryRun" ? "Publish" : r.Action is "Approve" or "Reject" or "Withdraw" or "Fail" ? "Review" : "Write";
        await Command(s, u, r.OperationId, "action", new { id, r }, permission, async () =>
        {
            var item = await Item(s, u, id, ct); Expected(item, r.ExpectedRowVersion); if (permission == "Write") await Author(s, u, item, ct);
            EditorialRules.Evidence(r.EvidenceReference, r.Mode); var head = await Head(item, ct); var revision = await EditorialBinding.Revision(db, item, ct);
            if (item.ApprovalId.HasValue && (await db.EditorialApprovals.SingleAsync(x => x.Id == item.ApprovalId, ct)).Fingerprint != await EditorialBinding.Fingerprint(db, item, ct)) Invalidate(item, head);
            var next = EditorialRules.Next(item.State, r.Action);
            if (r.Action == "Evaluate")
            {
                if (r.Evaluation == null) throw new RuntimeValidationException("Bind an actual M12 report and case.");
                var report = await evaluation.InspectAsync(s, u, r.Evaluation.ReportId, ct);
                var result = report.Results.SingleOrDefault(x => x.CaseKey == r.Evaluation.CaseKey) ?? throw new RuntimeValidationException("Case missing.");
                await EvaluationContext(s, item, head, revision, result, ct);
                if (result.Output?.Body != revision.Body || result.Locale != head.LocaleId || result.Deterministic.Any(x => x.Severity == "Critical")) throw new EvidenceBlockedException("EvaluationDoesNotMatchCurrentSafeDraft");
                item.EvaluationRunId = report.Id; item.EvaluationCaseKey = r.Evaluation.CaseKey;
            }
            if (r.Action == "Approve")
            {
                if (!item.EvaluationRunId.HasValue) throw new EvidenceBlockedException("CurrentEvaluationRequired");
                var report = await evaluation.InspectAsync(s, u, item.EvaluationRunId.Value, ct); var result = report.Results.Single(x => x.CaseKey == item.EvaluationCaseKey);
                await EvaluationContext(s, item, head, revision, result, ct);
                if (result.Output?.Body != revision.Body || result.Deterministic.Any(x => x.Severity == "Critical") || r.Mode == "Manual" && report.Qualification.Status != "TestedConditionsAccepted") throw new EvidenceBlockedException("HumanReleaseEvaluationRequired");
                var rights = await Rights(s, item, "Export", ct); if (rights.Blocks.Length > 0) throw new EvidenceBlockedException(string.Join(", ", rights.Blocks));
                var approval = new EditorialApproval { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ItemId = item.Id, RevisionId = revision.Id, Mode = r.Mode, Reviewer = s.Actor, EvidenceReference = r.EvidenceReference, Fingerprint = await EditorialBinding.Fingerprint(db, item, ct), SnapshotJson = Json(new { Report = report, Revision = revision, Policy = "editorial.v2" }) };
                db.EditorialApprovals.Add(approval); item.ApprovalId = approval.Id; head.EditorialState = r.Mode == "Manual" ? "Approved" : "Review";
            }
            if (r.Action is "Schedule" or "PublishDryRun")
            {
                await EditorialBinding.AssertExport(db, s, revision.Id, "Export", ct); var rights = await Rights(s, item, "Export", ct); if (rights.Blocks.Length > 0) throw new EvidenceBlockedException(string.Join(", ", rights.Blocks));
                if (r.Channel != "LocalPreview") throw new RuntimeValidationException("M13 publishes only explicit local dry runs; external delivery is M18.");
                item.Channel = r.Channel;
                if (r.Action == "Schedule") { if (!r.ScheduledUtc.HasValue || r.ScheduledUtc < DateTimeOffset.UtcNow.AddSeconds(-5) || r.ScheduledUtc > DateTimeOffset.UtcNow.AddDays(30)) throw new RuntimeValidationException("Bounded UTC schedule required."); item.ScheduledUtc = r.ScheduledUtc.Value.ToUniversalTime(); }
                else
                {
                    if (item.ScheduledUtc > DateTimeOffset.UtcNow) throw new RuntimeConflictException("Schedule not due.");
                    var copy = new EditorialCopy { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ItemId = item.Id, RevisionId = revision.Id, Channel = "LocalPreview", Mode = "DryRun", Reference = "fixture:local-preview/" + id, State = "Previewed" }; db.EditorialCopies.Add(copy);
                    db.SourceUses.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, OperationId = r.OperationId, ContentRevisionId = revision.Id, Purpose = "Export", Published = false, ExternalReference = copy.Reference, Mode = "Fixture" });
                }
            }
            if (r.Action is "Withdraw" or "Reject" or "Fail") { item.ApprovalId = null; item.ScheduledUtc = null; head.EditorialState = r.Action == "Withdraw" ? "Archived" : "Draft"; }
            item.State = next; head.PublicKnowledge = false; History(s, item, r.Action, r.EvidenceReference); return id;
        }, ct); return await InspectAsync(s, u, id, ct);
    }
    public async Task<EditorialPacket> AttachAssetAsync(CoreScope s, Guid u, Guid id, AttachEditorialAsset r, CancellationToken ct)
    {
        await Command(s, u, r.OperationId, "asset", new { id, r }, "Write", async () =>
        {
            var item = await Item(s, u, id, ct); await Author(s, u, item, ct); Expected(item, r.ExpectedRowVersion); CoreRules.Text(r.Slot, 100, "Asset slot"); EditorialRules.Media(r.MediaType, r.Content);
            EditorialRules.Evidence(r.EvidenceReference, r.EvidenceReference.Trim().StartsWith("fixture:", StringComparison.OrdinalIgnoreCase) ? "Fixture" : "Manual");
            var head = await Head(item, ct); var revision = await EditorialBinding.Revision(db, item, ct);
            var storage = await assetStore.PutAsync(r.Content, s.Actor, ct);
            var reference = new CoreAssetReference { Id = Guid.NewGuid(), TenantId = s.TenantId, ContentRevisionId = revision.Id, StorageAssetId = storage, RightsStatus = "Owned" }; db.Assets.Add(reference);
            db.EditorialAssets.Add(new() { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, ItemId = item.Id, RevisionId = revision.Id, AssetReferenceId = reference.Id, Slot = r.Slot, MediaType = r.MediaType, EvidenceReference = r.EvidenceReference });
            Invalidate(item, head); History(s, item, "Asset attached; approval invalidated", r.EvidenceReference); return id;
        }, ct); return await InspectAsync(s, u, id, ct);
    }
    public async Task<(byte[] Bytes, string MediaType)> PreviewAssetAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        await Access(s, u, "Read", ct); var asset = await db.EditorialAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == s.TenantId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        var item = await Item(s, u, asset.ItemId, ct); var rights = await Rights(s, item, "Read", ct); if (rights.Blocks.Any(x => x.StartsWith("Rights:"))) throw new RightsBlockedException("CurrentPreviewRightsRequired");
        var reference = await db.Assets.AsNoTracking().SingleAsync(x => x.Id == asset.AssetReferenceId, ct); var metadata = await EditorialBinding.Storage(db, reference.StorageAssetId, ct) ?? throw new CoreAccessException();
        if (reference.RightsStatus is not ("Owned" or "Licensed" or "Synthetic")) throw new RightsBlockedException("CurrentAssetRightsRequired");
        foreach (var snapshot in (await new SourceRightsGuard(db).LineageAsync(asset.RevisionId, ct)).Snapshots)
        {
            var decision = await new SourceRightsGuard(db).DecideAsync(s, snapshot, new("Read", (await Head(item, ct)).LocaleId, "IN", "Text"), ct);
            if (!decision.Decision.Allowed) throw new RightsBlockedException("CurrentPreviewRightsRequired");
        }
        var bytes = await assetStore.GetAsync(metadata.Id, metadata.Owner, ct) ?? throw new RuntimeValidationException("Stored asset unavailable.");
        if (Convert.ToHexString(SHA256.HashData(bytes)) != metadata.Sha256) throw new RuntimeValidationException("Stored checksum mismatch.");
        return (bytes, asset.MediaType);
    }
}
public static class EditorialServices { public static IServiceCollection AddEditorialWorkbench(this IServiceCollection s) => s.AddScoped<IEditorialWorkbench, EditorialWorkbench>(); }
