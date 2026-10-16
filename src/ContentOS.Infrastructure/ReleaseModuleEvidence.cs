using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed class ReleaseModuleEvidence(IServiceProvider services) : IReleaseModuleEvidence
{
    public async Task<ReleaseObservation> ObserveAsync(string feature, ReleaseTarget target, string actor, CancellationToken ct)
    {
        var db = services.GetRequiredService<CoreDbContext>(); var scope = new CoreScope(target.TenantId, actor);
        var blocks = new List<string>(); var references = new List<string>(); Guid[] resources = [target.ResourceId]; Guid[] dependencies = [];
        var quality = await services.GetRequiredService<IQualityEvaluation>().InspectAsync(scope, target.UniverseId, target.EvaluationRunId, ct);
        references.Add("evaluation:" + quality.Id);
        blocks.AddRange(quality.Qualification.Blocks.Select(x => "M12:" + x));
        if (quality.Qualification.Status != "TestedConditionsAccepted" || quality.HumanResults.Length == 0) blocks.Add("M12:GenuineAcceptanceRequired");
        object content; object provider;
        var lineage = new List<object>(); var dependencyFingerprint = "";
        if (feature == "READ")
        {
            var view = await services.GetRequiredService<IReadWorkbench>().InspectAsync(scope, target.UniverseId, target.ResourceId, ct);
            blocks.AddRange(view.ReleaseBlocks); if (view.Book.Collection.Key != target.Key || view.Book.Mode != "Public" || view.Book.Collection.State != "Active") blocks.Add("SelectedCollectionMismatchOrFixture");
            if (view.Book.Pieces.Any(x => x.Sources.Any(y => y.Attribution.StartsWith("Fixture", StringComparison.OrdinalIgnoreCase)))) blocks.Add("FixtureSourceRights");
            if (view.Book.Pieces.Any(piece => !quality.Results.Any(result => result.Passed && result.Case?.AudienceId == piece.AudienceId && result.Case.Locale == piece.Locale && result.Output != null && result.Output.Body.Contains(piece.Explanation, StringComparison.Ordinal) && result.Output.Body.Contains(piece.PracticalAction, StringComparison.Ordinal) && result.Output.Body.Contains(piece.Reflection, StringComparison.Ordinal)))) blocks.Add("M12:ExactReaderPiecesAcceptanceRequired");
            foreach (var revision in await db.ReadPieces.AsNoTracking().Where(x => x.CollectionId == target.ResourceId && x.TenantId == target.TenantId).Select(x => x.RevisionId).ToArrayAsync(ct)) lineage.Add(await SourceBinding(db, scope, revision, blocks, ct));
            content = new { view.Fingerprint, view.Book, Lineage = lineage, Quality = quality }; provider = quality.Configuration;
            references.Add("read:" + view.Book.Collection.Id + ":" + view.Fingerprint);
        }
        else if (feature == "WATCH")
        {
            var watch = services.GetRequiredService<IWatchFactory>(); var view = await watch.InspectAsync(scope, target.UniverseId, target.ResourceId, ct);
            blocks.AddRange(view.Blocks); references.Add("watch:" + view.Id + ":" + view.RowVersion);
            try { await watch.ExportAsync(scope, target.UniverseId, target.ResourceId, "Publish", ct); }
            catch (Exception e) when (e is EvidenceBlockedException or RightsBlockedException) { blocks.Add("WATCH:" + e.Message); }
            if (view.Snapshot.Profile.Configuration.Mode == "Fixture" || view.Timeline == null || view.Timeline.Scenes.Any(x => x.Image.License.Kind == "Fixture" || x.Voice.License.Kind == "Fixture" || x.Music?.License.Kind == "Fixture")) blocks.Add("FixtureOrMissingMedia");
            if (!quality.Results.Any(x => x.Passed && x.Case?.AudienceId == view.Timeline?.AudienceId && x.Output?.Body == view.Timeline?.Script)) blocks.Add("M12:ExactEpisodeAcceptanceRequired");
            var currentProfile = await db.WatchProfiles.AsNoTracking().SingleAsync(x => x.Id == view.Snapshot.Profile.Id && x.TenantId == target.TenantId && x.UniverseId == target.UniverseId, ct);
            var renderer = services.GetServices<IVideoRenderer>().SingleOrDefault(x => x.Key == view.Snapshot.Profile.Configuration.Renderer)?.Capabilities;
            if (!currentProfile.Enabled || renderer == null || !renderer.Configured || renderer.Version != view.Snapshot.Profile.Capabilities.Version || renderer.Qualification == "OfflineFixture") blocks.Add("CurrentQualifiedRendererBindingRequired");
            lineage.Add(await SourceBinding(db, scope, view.Snapshot.Script.RevisionId, blocks, ct));
            content = new { view, Lineage = lineage, Quality = quality }; provider = new { view.Snapshot.Profile, CurrentProfile = currentProfile.SnapshotJson, CurrentEnabled = currentProfile.Enabled, Renderer = renderer, quality.Configuration };
            resources = [view.Id, .. view.Artifacts.Select(x => x.Id)];
        }
        else if (feature == "ManualExport")
        {
            var row = await db.SocialPackages.AsNoTracking().SingleAsync(x => x.Id == target.ResourceId && x.TenantId == target.TenantId && x.UniverseId == target.UniverseId, ct);
            var export = await services.GetRequiredService<ISocialPublishing>().ExportAsync(scope, target.UniverseId, row.Id, ct);
            var review = row.ReviewJson == null ? null : JsonSerializer.Deserialize<SocialReview>(row.ReviewJson);
            if (review?.Mode != "Manual" || review.EvidenceReference.StartsWith("fixture:", StringComparison.OrdinalIgnoreCase)) blocks.Add("ManualPackageReviewRequired");
            if (export.Package.Snapshot?.Account.Mode != "ExportOnly") blocks.Add("NonFixtureManualExportAccountRequired");
            var production = export.Package.Snapshot?.Request.WatchProductionId;
            if (!production.HasValue) blocks.Add("SelectedWatchMasterRequired");
            else
            {
                dependencies = [production.Value];
                var watch = await ObserveAsync("WATCH", target with { ResourceId = production.Value }, actor, ct);
                blocks.AddRange(watch.Blocks.Select(x => "WATCH:" + x));
                references.AddRange(watch.References);
                dependencyFingerprint = ReleaseRules.Hash(new { watch.ContentFingerprint, watch.ProviderFingerprint });
            }
            content = new { export.Package, Dependency = dependencyFingerprint, Quality = quality }; provider = new { export.Package.Snapshot?.Account, quality.Configuration };
            references.Add("social-package:" + row.Id + ":" + row.PayloadHash);
        }
        else return new("", "", ["UnsupportedPilotFeature"], [], []);
        return new(ReleaseRules.Hash(content), ReleaseRules.Hash(provider), blocks.Distinct().ToArray(), references.ToArray(), resources, dependencies);
    }
    private static async Task<object> SourceBinding(CoreDbContext db, CoreScope scope, Guid revision, List<string> blocks, CancellationToken ct)
    {
        var lineage = await new SourceRightsGuard(db).LineageAsync(revision, ct);
        var snapshots = await db.SourceSnapshots.AsNoTracking().Where(x => lineage.Snapshots.Contains(x.Id)).ToArrayAsync(ct);
        var editions = await db.SourceEditions.AsNoTracking().Where(x => snapshots.Select(s => s.EditionId).Contains(x.Id)).ToArrayAsync(ct);
        var permissions = await db.SourcePermissions.AsNoTracking().Where(x => editions.Select(e => e.PermissionId).Contains(x.Id)).ToArrayAsync(ct);
        if (snapshots.Length == 0 || snapshots.Any(x => x.EvidenceReference.StartsWith("fixture:", StringComparison.OrdinalIgnoreCase)) || permissions.Any(x => x.EvidenceReference.StartsWith("fixture:", StringComparison.OrdinalIgnoreCase))) blocks.Add("GenuineSourceLineageRequired");
        var works = await db.SourceWorks.AsNoTracking().Where(x => editions.Select(e => e.WorkId).Contains(x.Id) && x.TenantId == scope.TenantId).ToArrayAsync(ct);
        return new { Snapshots = snapshots.Select(x => new { x.Id, x.Sha256, x.EvidenceReference, x.ReviewStatus, x.RowVersion }), Editions = editions.Select(x => new { x.Id, x.Version, x.PermissionId, x.Revoked, x.RowVersion }), Permissions = permissions, Works = works.Select(x => new { x.Id, x.Revoked, x.RowVersion }) };
    }
}
