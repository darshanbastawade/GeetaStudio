using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public sealed partial class ReadService
{
    private static readonly JsonSerializerOptions HistoryOptions = new(JsonSerializerDefaults.Web);
    private static ReadHistoricalMetadata History(ReadPiece row) => JsonSerializer.Deserialize<ReadHistoricalMetadata>(row.HistoricalJson!, HistoryOptions) ?? throw new EvidenceBlockedException("HistoricalMetadataRequired");
    private async Task ValidateHistory(CoreScope s, Guid u, ReadHistoricalMetadata h, Guid anchorId, string locale, CancellationToken ct)
    {
        if (h.Dating == null || (h.Dating.StartsWith("Undated", StringComparison.Ordinal) ? h.StartYear != 0 || h.EndYear != 0 : h.StartYear is < 1 or > 9999 || h.EndYear < h.StartYear || h.EndYear > 9999) || h.Dating is not ("SourceYearCE" or "DisputedSourceYearCE" or "UndatedLearningScene" or "UndatedLegend") || h.People is not { Length: <= 20 } || h.Places is not { Length: <= 20 }) throw new RuntimeValidationException("Bounded historical dates, people, places and explicit source-calendar precision required.");
        foreach (var name in h.People.Concat(h.Places)) CoreRules.Text(name, 200, "Historical name");
        var engine = new EvidenceEngine(db);
        var anchor = await engine.RequireLessonClaimAsync(s, u, anchorId, new("Read", locale, "IN", "Text"), ct);
        var valid = h.Classification switch
        {
            "DocumentedFact" => anchor.Type == "HistoricalClaim",
            "HistoricalAccount" or "PopularLegend" or "DebatedClaim" => anchor.Type == "SourceQuotation",
            "DramatizedDialogue" => anchor.Type == "FictionalScene",
            _ => false
        };
        if (!valid || h.Classification == "DramatizedDialogue" && h.Dating != "UndatedLearningScene" || h.Dating == "UndatedLearningScene" && h.Classification != "DramatizedDialogue" || h.Dating == "UndatedLegend" && h.Classification != "PopularLegend") throw new EvidenceBlockedException("HistoricalClassificationMustMatchEvidence");
        if (h.Classification == "DebatedClaim")
        {
            var d = await db.ClaimDisagreements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == h.DisagreementId && (x.LeftRevisionId == anchorId || x.RightRevisionId == anchorId), ct) ?? throw new EvidenceBlockedException("BoundHistoricalDisagreementRequired");
            await engine.RequireLessonClaimAsync(s, u, d.LeftRevisionId == anchorId ? d.RightRevisionId : d.LeftRevisionId, new("Read", locale, "IN", "Text"), ct);
        }
        else if (h.DisagreementId != null) throw new RuntimeValidationException("Disagreement belongs to an explicitly debated entry.");
    }
    private async Task<Guid> AddHistorical(CoreScope s, Guid u, ReadCollection c, AddReadPiece r, CancellationToken ct)
    {
        if (r.CorpusVerseId != null || r.History == null) throw new RuntimeValidationException("Historical entries use source claims, never invented scripture verses.");
        var p = await editorial.InspectAsync(s, u, r.EditorialItemId, ct); var lesson = await lessons.InspectAsync(s, u, r.LessonVariantId, ct);
        if (p.SourceOutputWithheld || lesson.OutputWithheld || lesson.Sections == null || lesson.ReviewStatus != "Approved" || !p.Approvals.Any(x => x.Current) || p.Format != "Article" || p.Current?.Kind != "Historical") throw new EvidenceBlockedException("CurrentReviewedReadInputsRequired");
        var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == p.ContentId, ct);
        var anchor = lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId;
        await ValidateHistory(s, u, r.History, anchor, head.LocaleId, ct);
        if (head.AudienceId != lesson.Snapshot.Audience.Rules.Audience!.AudienceId || head.LocaleId != lesson.Snapshot.Locale.Rules.Locale!.Locale || lesson.Snapshot.Bundle.SourceIds.Any(x => !p.SourceSnapshotIds.Contains(x)) ||
            !await db.ClaimLinks.AnyAsync(x => x.ClaimRevisionId == anchor && x.ContentRevisionId == p.RevisionId, ct)) throw new EvidenceBlockedException("HistoricalLessonAndEditorialEvidenceMismatch");
        CoreRules.Text(r.Journey, 200, "Journey");
        if (r.Position is < 1 or > 1000 || await db.ReadPieces.AnyAsync(x => x.CollectionId == c.Id && x.EditorialItemId == r.EditorialItemId, ct)) throw new RuntimeConflictException("Unique historical entry and bounded position required.");
        var row = new ReadPiece { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, CollectionId = c.Id, EditorialItemId = p.Id, RevisionId = p.RevisionId, LessonVariantId = r.LessonVariantId, HistoricalJson = Json(r.History), Journey = r.Journey, Position = r.Position };
        db.ReadPieces.Add(row); return row.Id;
    }
    private async Task<byte[]> HistoricalVersion(ReadPiece row, CancellationToken ct)
    {
        var bundle = await (from l in db.LessonVariants join b in db.LessonBundles on l.BundleId equals b.Id where l.Id == row.LessonVariantId select b).SingleAsync(ct);
        return await db.ClaimRevisions.Where(x => x.Id == bundle.AnchorClaimRevisionId).Select(x => x.RowVersion).SingleAsync(ct);
    }
    private async Task<ReadPieceView?> HistoricalPiece(CoreScope s, ReadCollection c, ReadPiece row, EditorialPacket p, LessonVariantView lesson, ContentItem head, bool release, CancellationToken ct)
    {
        var history = History(row); var anchorId = lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId;
        await ValidateHistory(s, c.UniverseId, history, anchorId, head.LocaleId, ct);
        if (!await db.ClaimLinks.AnyAsync(x => x.ClaimRevisionId == anchorId && x.ContentRevisionId == row.RevisionId, ct)) return null;
        if (release && !await db.ClaimRevisions.AnyAsync(x => x.Id == anchorId && x.ReviewEvidence != null && x.ReviewEvidence.StartsWith("manual:"), ct)) return null;
        var texts = new List<ReadSource>();
        foreach (var span in await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == anchorId).OrderBy(x => x.Id).ToArrayAsync(ct))
        {
            var rights = await new SourceRightsGuard(db).DecideAsync(s, span.SnapshotId, new(release ? "Publish" : "Read", head.LocaleId, "IN", "Text"), ct);
            if (!rights.Decision.Allowed || rights.Citation == null) return null;
            texts.Add(new("HistoricalSource", head.LocaleId, span.ExactText, span.Locator, rights.Citation.Citation + "; " + rights.Citation.RequiredAttribution));
        }
        if (history.DisagreementId != null)
        {
            var d = await db.ClaimDisagreements.AsNoTracking().SingleAsync(x => x.Id == history.DisagreementId, ct);
            foreach (var span in await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == (d.LeftRevisionId == anchorId ? d.RightRevisionId : d.LeftRevisionId)).ToArrayAsync(ct))
            {
                var rights = await new SourceRightsGuard(db).DecideAsync(s, span.SnapshotId, new(release ? "Publish" : "Read", head.LocaleId, "IN", "Text"), ct);
                if (!rights.Decision.Allowed || rights.Citation == null) return null;
                texts.Add(new("DisputedHistoricalSource", head.LocaleId, span.ExactText, span.Locator, rights.Citation.Citation + "; " + rights.Citation.RequiredAttribution));
            }
        }
        foreach (var source in lesson.Snapshot.Bundle.SourceIds)
            if (!(await new SourceRightsGuard(db).DecideAsync(s, source, new(release ? "Publish" : "Read", head.LocaleId, "IN", "Text"), ct)).Decision.Allowed) return null;
        return new(row.Id, 1, row.Position, head.LocaleId, head.AudienceId, lesson.Snapshot.Audience.Rules.Key, p.Current!.Title, p.Current.Body,
            lesson.Sections!.PracticalAction, lesson.Sections.Reflection, texts.ToArray(), lesson.Sections.Concepts, row.Journey, row.Position, history);
    }
}
