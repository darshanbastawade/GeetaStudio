using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class TrendWorkbench
{
    public async Task<TrendRankView[]> RankAsync(CoreScope s, Guid u, RankTrends r, CancellationToken ct)
    {
        await Access(s, u, "Read", ct);
        var audience = await db.Audiences.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.AudienceId && x.UniverseId == u, ct) ?? throw new CoreAccessException();
        var weights = new[] { r.Weights.Freshness, r.Weights.Momentum, r.Weights.Evidence, r.Weights.Novelty, r.Weights.Cost, r.Weights.TeachingRelevance };
        if (weights.Any(x => !double.IsFinite(x) || x < 0 || x > 1) || Math.Abs(weights.Sum() - 1) > .001) throw new RuntimeValidationException("Rank weights must be finite, nonnegative and total1.");
        var result = new List<TrendRankView>();
        foreach (var cluster in await ClustersAsync(s, u, ct))
        {
            var blocks = cluster.Blocks.ToList();
            if (cluster.Candidates.All(x => x.Signal.Locale != r.Locale)) blocks.Add("LocaleMismatch");
            if (audience.MinAge < 18 && cluster.Candidates.Any(x => x.Signal.Risks.Intersect(new[] { "Violence", "Tragedy", "Political", "ReligiousConflict", "RealPerson" }).Any()))
                blocks.Add("ChildSensitivityHumanReviewRequired");
            var fit = false;
            foreach (var teaching in await db.TrendTeachings.AsNoTracking().Where(x => x.ClusterId == cluster.Id && x.Outcome == "Matched" && x.ReviewStatus == "Approved").ToListAsync(ct))
            {
                var lesson = await lessons.InspectAsync(s, u, teaching.LessonVariantId, ct);
                if (!lesson.OutputWithheld && lesson.ReviewStatus == "Approved" && lesson.Snapshot.Audience.Rules.Audience!.AudienceId == r.AudienceId && lesson.Snapshot.Locale.Rules.Locale!.Locale == r.Locale) fit = true;
            }
            if (!fit) blocks.Add("NoReviewedAudienceTeachingConnection");
            var metrics = cluster.Candidates.Select(x => x.Momentum).Where(x => x.HasValue).Select(x => (double)x!.Value).ToArray();
            double? score = null;
            if (blocks.Count == 0)
            {
                var newest = cluster.Candidates.Max(x => x.Signal.UpdatedUtc ?? x.Signal.PublishedUtc)!.Value;
                var hours = Math.Max(0, (DateTimeOffset.UtcNow - newest).TotalHours);
                var briefs = await db.TrendStoryBriefs.AsNoTracking().Where(x => x.ClusterId == cluster.Id).ToListAsync(ct);
                var cost = briefs.Where(x => x.SnapshotJson.Length > 0).SelectMany(x => Read<TrendStorySnapshot>(x.SnapshotJson).Packages).Sum(x => x.EstimatedProductionCost);
                score = r.Weights.Freshness / (1 + hours) + r.Weights.Momentum * (metrics.Length == 0 ? 0 : Math.Min(1, metrics.Max() / 1000))
                    + r.Weights.Evidence * Math.Min(1, cluster.IndependentReports / 2.0)
                    + r.Weights.Novelty * (briefs.Any(x => x.State == "Approved") ? 0 : 1) + r.Weights.Cost / (1 + (double)cost) + r.Weights.TeachingRelevance;
            }
            result.Add(new(cluster.Id, blocks.Count == 0, score, metrics.Length == 0, blocks.Distinct().ToArray()));
        }
        return result.OrderByDescending(x => x.Eligible).ThenByDescending(x => x.Score).ToArray();
    }
}
