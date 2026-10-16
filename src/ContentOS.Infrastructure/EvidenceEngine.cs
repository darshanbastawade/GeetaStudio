using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed class EvidenceEngine(CoreDbContext db) : IEvidenceEngine
{
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string TextHash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private async Task AccessAsync(CoreScope s, Guid u, int level, CancellationToken ct)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.TenantId == s.TenantId && m.Actor == s.Actor, ct);
        if (member == null || level == 1 && member.Permission is not ("Administrator" or "Editor") || level == 2 && member.Permission != "Administrator" ||
            !await db.Universes.AnyAsync(x => x.Id == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
    }
    private static string Evidence(string value)
    {
        CoreRules.Text(value, 1000, "Review evidence");
        if (!value.StartsWith("fixture:", StringComparison.Ordinal) && !value.StartsWith("manual:", StringComparison.Ordinal) && !value.StartsWith("evidence:", StringComparison.Ordinal))
            throw new RuntimeValidationException("Explicit review evidence is required; model confidence or a URL is not verification.");
        return value;
    }
    private static void Expected(CoreRecord row, string value)
    {
        byte[] expected; try { expected = Convert.FromBase64String(value); } catch (Exception e) when (e is FormatException or ArgumentNullException) { throw new RuntimeValidationException("Invalid rowversion."); }
        if (!expected.SequenceEqual(row.RowVersion)) throw new CoreConcurrencyException();
    }
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new CoreConcurrencyException(); }
    }
    private void Audit(CoreScope s, Guid id, string action) => db.SourceAudits.Add(new SourceAudit { Id = Guid.NewGuid(), TenantId = s.TenantId, Actor = s.Actor, SubjectId = id, Action = action });
    private async Task<Guid> CommandAsync<T>(CoreScope s, Guid u, Guid operation, string kind, T request, Func<Task<Guid>> action, CancellationToken ct, int level = 1)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(s, u, level, ct);
        if (operation == Guid.Empty) throw new RuntimeValidationException("Stable operation ID is required.");
        var id = await CommandInTransaction(s, u, operation, kind, request, action, ct);
        await SaveAsync(ct); await tx.CommitAsync(ct); return id;
    }
    private async Task<Guid> CommandInTransaction<T>(CoreScope s, Guid u, Guid operation, string kind, T request, Func<Task<Guid>> action, CancellationToken ct)
    {
        var fingerprint = Hash(new { u, request });
        var prior = await db.EvidenceCommands.FromSqlInterpolated($"SELECT * FROM [evidence].[EvidenceCommand] WITH (UPDLOCK,HOLDLOCK) WHERE [TenantId]={s.TenantId} AND [OperationId]={operation}").SingleOrDefaultAsync(ct);
        if (prior != null)
        {
            if (prior.Actor != s.Actor || prior.Kind != kind || prior.Fingerprint != fingerprint) throw new RuntimeConflictException("Operation already belongs to a different evidence command.");
            return prior.ResultId;
        }
        var id = await action(); db.EvidenceCommands.Add(new EvidenceCommand { Id = Guid.NewGuid(), TenantId = s.TenantId, OperationId = operation, Actor = s.Actor, Kind = kind, Fingerprint = fingerprint, ResultId = id });
        Audit(s, id, kind); return id;
    }
    private async Task<ClaimRevision> RevisionAsync(Guid u, Guid id, CancellationToken ct) =>
        await db.ClaimRevisions.SingleOrDefaultAsync(r => r.Id == id && db.Claims.Any(c => c.Id == r.ClaimId && c.UniverseId == u), ct) ?? throw new CoreAccessException();
    public Task<Guid> RegisterRubricAsync(CoreScope s, Guid u, RegisterEvidenceRubric r, CancellationToken ct) =>
        CommandAsync(s, u, r.OperationId, "evidence.rubric", r, async () =>
        {
            CoreRules.Text(r.Key, 200, "Rubric key"); CoreRules.Text(r.Description, 2000, "Rubric criteria");
            if (r.Version < 1 || r.MinimumIndependentSources is < 2 or > 10) throw new RuntimeValidationException("Explicit rubric version and 2–10 independent sources required.");
            var old = await db.EvidenceRubrics.SingleOrDefaultAsync(x => x.UniverseId == u && x.Key == r.Key && x.Version == r.Version, ct);
            if (old != null) { if (old.Description != r.Description || old.MinimumIndependentSources != r.MinimumIndependentSources) throw new RuntimeConflictException("Rubric is immutable."); return old.Id; }
            var rubric = new EvidenceRubric { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key, Version = r.Version, Description = r.Description, MinimumIndependentSources = r.MinimumIndependentSources };
            db.EvidenceRubrics.Add(rubric); return rubric.Id;
        }, ct, 2);
    public Task<Guid> SubmitAsync(CoreScope s, Guid u, SubmitClaim r, CancellationToken ct) =>
        CommandAsync(s, u, r.OperationId, "evidence.claim-submitted", r, () => SubmitCore(s, u, r, ct), ct);
    // The factory accepts content, pending labels and its checkpoint atomically. Reuse the
    // same evidence validation/idempotency; never manufacture a review of generated text.
    internal async Task<Guid> SubmitInTransaction(CoreScope s, Guid u, SubmitClaim r, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || r.OperationId == Guid.Empty) throw new RuntimeValidationException("Owned transaction and stable operation required.");
        await AccessAsync(s, u, 1, ct);
        return await CommandInTransaction(s, u, r.OperationId, "evidence.claim-submitted", r, () => SubmitCore(s, u, r, ct), ct);
    }
    private async Task<Guid> SubmitCore(CoreScope s, Guid u, SubmitClaim r, CancellationToken ct)
    {
        CoreRules.Text(r.Key, 200, "Claim key"); CorpusRules.Exact(r.Statement);
        EvidenceRules.Classification(r.Type, r.Label, r.Author, r.Tradition, r.Uncertainty);
        if (r.Author == null || r.Tradition == null || r.Author.Length > 200 || r.Tradition.Length > 200 || r.Evidence == null || r.Evidence.Count > 20 ||
            !await db.Locales.AnyAsync(l => l.Id == r.Locale, ct)) throw new RuntimeValidationException("Invalid claim metadata/evidence.");
        if (!await db.EvidenceRubrics.AnyAsync(x => x.Id == r.RubricId && x.UniverseId == u && x.TenantId == s.TenantId, ct)) throw new CoreAccessException();
        var claim = await db.Claims.FromSqlInterpolated($"SELECT * FROM [evidence].[EvidenceClaim] WITH (UPDLOCK,HOLDLOCK) WHERE [UniverseId]={u} AND [Key]={r.Key}").SingleOrDefaultAsync(ct);
        if (claim == null)
        {
            if (!string.IsNullOrEmpty(r.ExpectedRowVersion) || r.CorrectionId != null) throw new RuntimeValidationException("New claim cannot replace existing history.");
            claim = new EvidenceClaim { Id = Guid.NewGuid(), TenantId = s.TenantId, UniverseId = u, Key = r.Key }; db.Claims.Add(claim);
        }
        else Expected(claim, r.ExpectedRowVersion);
        var revision = new ClaimRevision
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            Version = ++claim.CurrentVersion,
            Type = r.Type,
            Label = r.Label,
            Locale = r.Locale,
            Statement = r.Statement,
            Author = r.Author,
            Tradition = r.Tradition,
            Uncertainty = r.Uncertainty,
            RubricId = r.RubricId
        }; db.ClaimRevisions.Add(revision);
        foreach (var input in r.Evidence)
        {
            if (input.Relation is not ("Supporting" or "Contradicting")) throw new RuntimeValidationException("Evidence relation must be explicit.");
            CoreRules.Text(input.Locator, 1000, "Evidence locator"); CorpusRules.Exact(input.ExactText);
            var source = await (from snap in db.SourceSnapshots
                                join e in db.SourceEditions on snap.EditionId equals e.Id
                                join w in db.SourceWorks on e.WorkId equals w.Id
                                where snap.Id == input.SnapshotId && w.UniverseId == u && w.TenantId == s.TenantId
                                select snap).SingleOrDefaultAsync(ct) ?? throw new CoreAccessException();
            if (source.Text == null) throw new EvidenceBlockedException("EvidenceNotRetained");
            EvidenceRules.Span(source.Text, input.Start, input.Length);
            if (source.Text.Substring(input.Start, input.Length) != input.ExactText || input.Length != input.ExactText.Length || !TextHash(input.ExactText).Equals(input.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new EvidenceBlockedException("InvalidEvidenceSpan");
            if (input.CorpusTextId is Guid corpusText && !await db.CorpusTexts.AnyAsync(t => t.Id == corpusText && t.SnapshotId == source.Id && db.CorpusVerses.Any(v => v.Id == t.VerseId && db.CorpusManifests.Any(m => m.Id == v.ManifestId && m.UniverseId == u)), ct)) throw new CoreAccessException();
            db.ClaimSpans.Add(new ClaimEvidenceSpan
            {
                Id = Guid.NewGuid(),
                ClaimRevisionId = revision.Id,
                SnapshotId = source.Id,
                CorpusTextId = input.CorpusTextId,
                Start = input.Start,
                Length = input.Length,
                ExactText = input.ExactText,
                Sha256 = TextHash(input.ExactText),
                Locator = input.Locator,
                Relation = input.Relation
            });
        }
        if (r.CorrectionId is Guid correctionId)
        {
            await AccessAsync(s, u, 2, ct);
            var correction = await db.ClaimCorrections.SingleOrDefaultAsync(x => x.Id == correctionId, ct) ?? throw new CoreAccessException();
            if (correction.Status != "Open" || (await RevisionAsync(u, correction.ClaimRevisionId, ct)).ClaimId != claim.Id) throw new RuntimeConflictException("Correction must replace the same claim.");
            correction.Status = "Superseded"; correction.ReplacementRevisionId = revision.Id;
        }
        return revision.Id;
    }
    private async Task<string?> AssessAsync(CoreScope s, ClaimRevision r, IntendedUse use, CancellationToken ct)
    {
        var rubric = await db.EvidenceRubrics.AsNoTracking().SingleAsync(x => x.Id == r.RubricId, ct);
        var spans = await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == r.Id).ToListAsync(ct);
        var support = spans.Where(x => x.Relation == "Supporting").ToArray();
        if (r.Type is "Quotation" or "Translation")
        {
            if (r.Quality != "DirectSource" || r.Outcome != "Supported" || support.Length != 1 || support[0].ExactText != r.Statement || support[0].CorpusTextId == null) return "UnsupportedQuotation";
            var t = await db.CorpusTexts.AsNoTracking().SingleOrDefaultAsync(t => t.Id == support[0].CorpusTextId, ct);
            if (t == null || t.ExactText != r.Statement || t.SnapshotId != support[0].SnapshotId || t.Locale != r.Locale ||
                t.Kind != (r.Type == "Quotation" ? "DirectQuotation" : "Translation") || t.SourceLocator != support[0].Locator) return "UnsupportedQuotation";
            var v = await db.CorpusVerses.AsNoTracking().SingleAsync(v => v.Id == t.VerseId, ct);
            if (v.ReviewStatus != "Approved" || !await db.CorpusBindings.AnyAsync(b => b.ManifestId == v.ManifestId && b.Chapter == v.Chapter && b.Verse == v.Verse && b.VariantKey == v.VariantKey &&
                b.SnapshotId == t.SnapshotId && b.Kind == t.Kind && b.Locale == t.Locale && b.Sha256 == t.Sha256 && b.SourceLocator == t.SourceLocator && b.SourceChapter == v.Chapter && b.SourceVerse == v.Verse, ct)) return "UnreviewedCanonicalEvidence";
        }
        else if (r.Type == "SourceQuotation")
        {
            if (r.Quality != "DirectSource" || r.Outcome != "Supported" || support.Length != 1 || support[0].ExactText != r.Statement || support[0].CorpusTextId != null) return "UnsupportedQuotation";
        }
        else if (r.Type is "HistoricalClaim" or "NewsClaim")
        {
            var ids = support.Select(x => x.SnapshotId).ToArray();
            var count = await (from snap in db.SourceSnapshots join e in db.SourceEditions on snap.EditionId equals e.Id where ids.Contains(snap.Id) select e.WorkId).Distinct().CountAsync(ct);
            // A reviewed historical paraphrase need not repeat the identical wording in independent accounts.
            // Exact span/hash validation still applies on submission; corroboration is a reviewer decision.
            if (r.Quality != "Corroborated" || r.Outcome != "Supported" || count < rubric.MinimumIndependentSources || r.Type == "NewsClaim" && support.Any(x => x.ExactText != r.Statement) || spans.Any(x => x.Relation == "Contradicting")) return "InsufficientEvidence";
        }
        else if (r.Type == "Interpretation")
        {
            if (r.Quality != "Attributed" || r.Outcome is not ("Supported" or "Contested") || support.Length == 0) return "InsufficientEvidence";
        }
        else if (r.Quality != "Attributed" || r.Outcome is not ("Supported" or "Contested")) return "InsufficientEvidence";
        foreach (var span in spans)
        {
            var intended = use with { Adaptation = use.Adaptation || r.Type is "Interpretation" or "ModernAnalogy" or "FictionalScene" or "Suggestion" };
            var result = await new SourceRightsGuard(db).DecideAsync(s, span.SnapshotId, intended, ct);
            if (!result.Decision.Allowed) return result.Decision.Reason;
            db.RightsDecisions.Add(result.Decision);
        }
        return null;
    }
    internal async Task<ClaimRevision> RequireLessonClaimAsync(CoreScope s, Guid u, Guid id, IntendedUse use, CancellationToken ct)
    {
        var r = await RevisionAsync(u, id, ct);
        var current = await db.Claims.AsNoTracking().SingleAsync(x => x.Id == r.ClaimId, ct);
        if (r.ReviewStatus != "Approved" || current.CurrentVersion != r.Version ||
            await db.ClaimCorrections.AnyAsync(x => x.ClaimRevisionId == id && x.Status == "Open", ct))
            throw new EvidenceBlockedException("StaleOrUnreviewedLessonClaim");
        var failure = await AssessAsync(s, r, use, ct);
        if (failure != null) throw new EvidenceBlockedException(failure);
        return r;
    }
    public async Task ReviewAsync(CoreScope s, Guid u, Guid id, ReviewClaim r, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await AccessAsync(s, u, 2, ct);
        var revision = await RevisionAsync(u, id, ct); Expected(revision, r.ExpectedRowVersion);
        if (revision.ReviewStatus != "Pending" || r.Status is not ("Approved" or "Rejected") || r.Quality is not ("Insufficient" or "DirectSource" or "Attributed" or "Corroborated") || r.Outcome is not ("Supported" or "Contested" or "InsufficientEvidence"))
            throw new RuntimeConflictException("Review is frozen or has an invalid rubric outcome.");
        if ((await db.Claims.SingleAsync(c => c.Id == revision.ClaimId, ct)).CurrentVersion != revision.Version) throw new EvidenceBlockedException("StaleClaim");
        revision.Quality = r.Quality; revision.Outcome = r.Outcome;
        if (r.Status == "Approved")
        {
            var failure = await AssessAsync(s, revision, new("Read", revision.Locale, "IN", "Text"), ct);
            if (failure != null) throw new EvidenceBlockedException(failure);
        }
        revision.ReviewStatus = r.Status; revision.Reviewer = s.Actor; revision.ReviewEvidence = Evidence(r.EvidenceReference);
        Audit(s, id, "evidence.claim-reviewed"); await SaveAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<ClaimView> ViewAsync(EvidenceClaim claim, CancellationToken ct)
    {
        var views = new List<ClaimRevisionView>();
        foreach (var r in await db.ClaimRevisions.AsNoTracking().Where(r => r.ClaimId == claim.Id).OrderBy(r => r.Version).ToListAsync(ct))
            views.Add(new(r.Id, r.Version, r.Type, r.Label, r.Locale, r.Statement, r.Author, r.Tradition, r.Uncertainty, r.RubricId, r.ReviewStatus, r.Quality, r.Outcome, r.Reviewer, r.ReviewEvidence, Convert.ToBase64String(r.RowVersion),
                await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == r.Id).Select(x => new ClaimEvidenceView(x.Id, x.SnapshotId, x.CorpusTextId, x.Start, x.Length, x.ExactText, x.Sha256, x.Locator, x.Relation)).ToListAsync(ct)));
        return new(claim.Id, claim.Key, claim.CurrentVersion, Convert.ToBase64String(claim.RowVersion), views);
    }
    public async Task<IReadOnlyList<EvidenceRubricView>> RubricsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await AccessAsync(s, u, 0, ct);
        return await db.EvidenceRubrics.AsNoTracking().Where(r => r.UniverseId == u).OrderBy(r => r.Key).ThenBy(r => r.Version)
            .Take(100).Select(r => new EvidenceRubricView(r.Id, r.Key, r.Version, r.Description, r.MinimumIndependentSources)).ToListAsync(ct);
    }
    public async Task<ClaimView> InspectAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        await AccessAsync(s, u, 0, ct); return await ViewAsync(await db.Claims.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UniverseId == u, ct) ?? throw new CoreAccessException(), ct);
    }
    public async Task<IReadOnlyList<ClaimView>> ListAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await AccessAsync(s, u, 0, ct); var views = new List<ClaimView>();
        foreach (var c in await db.Claims.AsNoTracking().Where(c => c.UniverseId == u).OrderByDescending(c => c.CreatedUtc).Take(100).ToListAsync(ct)) views.Add(await ViewAsync(c, ct));
        return views;
    }
    public Task<Guid> DisagreeAsync(CoreScope s, Guid u, RegisterDisagreement r, CancellationToken ct) =>
        CommandAsync(s, u, r.OperationId, "evidence.disagreement", r, async () =>
        {
            var left = await RevisionAsync(u, r.LeftRevisionId, ct); var right = await RevisionAsync(u, r.RightRevisionId, ct);
            if (left.Id == right.Id || !(left.Type == "Interpretation" && right.Type == "Interpretation" || left.Type == "SourceQuotation" && right.Type == "SourceQuotation") || left.ReviewStatus != "Approved" || right.ReviewStatus != "Approved") throw new RuntimeValidationException("Retain two reviewed attributed interpretations or historical source accounts.");
            if (left.Type == "SourceQuotation")
            {
                await RequireLessonClaimAsync(s, u, left.Id, new("Read", left.Locale, "IN", "Text"), ct);
                await RequireLessonClaimAsync(s, u, right.Id, new("Read", right.Locale, "IN", "Text"), ct);
            }
            CoreRules.Text(r.Reason, 1000, "Disagreement reason");
            var item = new ClaimDisagreement { Id = Guid.NewGuid(), LeftRevisionId = left.Id, RightRevisionId = right.Id, Reason = r.Reason, EvidenceReference = Evidence(r.EvidenceReference) }; db.ClaimDisagreements.Add(item); return item.Id;
        }, ct, 2);
    public async Task<IReadOnlyList<DisagreementView>> DisagreementsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await AccessAsync(s, u, 0, ct);
        return await db.ClaimDisagreements.AsNoTracking().Where(d => db.ClaimRevisions.Any(r => r.Id == d.LeftRevisionId && db.Claims.Any(c => c.Id == r.ClaimId && c.UniverseId == u)))
            .Take(100).Select(d => new DisagreementView(d.Id, d.LeftRevisionId, d.RightRevisionId, d.Reason, d.EvidenceReference)).ToListAsync(ct);
    }
    public Task<Guid> LinkAsync(CoreScope s, Guid u, LinkClaim r, CancellationToken ct) =>
        CommandAsync(s, u, r.OperationId, "evidence.content-linked", r, async () =>
        {
            var claim = await RevisionAsync(u, r.ClaimRevisionId, ct);
            var revision = await db.Revisions.SingleOrDefaultAsync(x => x.Id == r.ContentRevisionId && db.Contents.Any(c => c.Id == x.ContentId && c.UniverseId == u && c.TenantId == s.TenantId), ct) ?? throw new CoreAccessException();
            var content = await db.Contents.SingleAsync(c => c.Id == revision.ContentId, ct);
            if (content.EditorialState is not ("Draft" or "Review") || revision.Version != content.CurrentVersion) throw new RuntimeConflictException("Link the current draft before approval.");
            EvidenceRules.Span(revision.Body, r.Start, r.Length);
            if (revision.Body.Substring(r.Start, r.Length) != claim.Statement || claim.Locale != content.LocaleId) throw new EvidenceBlockedException("ClaimTextMismatch");
            if (await db.ClaimLinks.AnyAsync(l => l.ContentRevisionId == revision.Id && l.Start < r.Start + r.Length && r.Start < l.Start + l.Length, ct)) throw new RuntimeValidationException("Claim segments cannot overlap.");
            var link = new ClaimContentLink { Id = Guid.NewGuid(), ClaimRevisionId = claim.Id, ContentRevisionId = revision.Id, Start = r.Start, Length = r.Length }; db.ClaimLinks.Add(link);
            foreach (var snap in await db.ClaimSpans.Where(x => x.ClaimRevisionId == claim.Id).Select(x => x.SnapshotId).Distinct().ToListAsync(ct))
                if (!await db.Provenance.AnyAsync(e => e.ContentRevisionId == revision.Id && e.SnapshotId == snap, ct))
                    db.Provenance.Add(new ProvenanceEdge { Id = Guid.NewGuid(), TenantId = s.TenantId, ContentRevisionId = revision.Id, SnapshotId = snap, Relation = "claim-evidence" });
            return link.Id;
        }, ct);
    public Task<Guid> CorrectAsync(CoreScope s, Guid u, RequestClaimCorrection r, CancellationToken ct) =>
        CommandAsync(s, u, r.OperationId, "evidence.correction-requested", r, async () =>
        {
            await RevisionAsync(u, r.ClaimRevisionId, ct); CoreRules.Text(r.Reason, 1000, "Correction reason");
            var correction = new ClaimCorrection { Id = Guid.NewGuid(), ClaimRevisionId = r.ClaimRevisionId, Reason = r.Reason, EvidenceReference = Evidence(r.EvidenceReference) };
            db.ClaimCorrections.Add(correction); return correction.Id;
        }, ct);
    public async Task<ClaimImpact> ImpactAsync(CoreScope s, Guid u, Guid id, CancellationToken ct)
    {
        await AccessAsync(s, u, 0, ct); await RevisionAsync(u, id, ct);
        var affected = (await db.ClaimLinks.Where(l => l.ClaimRevisionId == id).Select(l => l.ContentRevisionId).ToListAsync(ct)).ToHashSet();
        var edges = await db.Provenance.AsNoTracking().Where(e => e.ParentRevisionId != null && db.Revisions.Any(r => r.Id == e.ContentRevisionId && db.Contents.Any(c => c.Id == r.ContentId && c.UniverseId == u))).Take(10001).ToListAsync(ct);
        if (edges.Count > 10000) throw new RuntimeValidationException("Impact exceeds the local 10,000-edge bound.");
        bool changed; do { changed = false; foreach (var e in edges.Where(e => affected.Contains(e.ParentRevisionId!.Value))) if (e.ContentRevisionId is Guid target) changed |= affected.Add(target); } while (changed);
        return new(id, affected.ToArray(), await db.ClaimCorrections.Where(x => x.ClaimRevisionId == id).Select(x => new ClaimCorrectionView(x.Id, x.ClaimRevisionId, x.Reason, x.EvidenceReference, x.Status, x.ReplacementRevisionId)).ToListAsync(ct));
    }
    internal async Task<EvidenceGateResult> CheckContentAsync(CoreScope s, Guid revisionId, IntendedUse use, CancellationToken ct)
    {
        var revision = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == revisionId, ct) ?? throw new CoreAccessException();
        var content = await db.Contents.AsNoTracking().SingleAsync(c => c.Id == revision.ContentId, ct);
        if (use.Language != content.LocaleId) throw new RuntimeValidationException("Evidence use language must match content.");
        var links = await db.ClaimLinks.AsNoTracking().Where(l => l.ContentRevisionId == revisionId).OrderBy(l => l.Start).ToListAsync(ct);
        var ancestry = await new SourceRightsGuard(db).LineageAsync(revisionId, ct);
        var parents = ancestry.Edges.Where(e => e.ParentRevisionId != null).Select(e => e.ParentRevisionId!.Value).ToArray();
        var inherited = await db.ClaimLinks.Where(l => parents.Contains(l.ContentRevisionId)).Select(l => l.ClaimRevisionId).Distinct().ToListAsync(ct);
        foreach (var id in inherited)
        {
            if (await db.EditorialCorrections.AnyAsync(x => x.ClaimRevisionId == id, ct)) return new(false, "EditorialClaimCorrectionRequiresReplacement", []);
            var old = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            if (await db.Claims.AnyAsync(c => c.Id == old.ClaimId && c.CurrentVersion != old.Version, ct) ||
                await db.ClaimCorrections.AnyAsync(c => c.ClaimRevisionId == id && c.Status == "Open", ct)) return new(false, "StaleInheritedEvidence", []);
        }
        if (links.Count == 0) return new(!EvidenceRules.RequiresClaims(revision.Kind) && inherited.Count == 0,
            EvidenceRules.RequiresClaims(revision.Kind) || inherited.Count > 0 ? "InsufficientEvidence" : "ExplicitFictionOrSynthetic", []);
        var labels = new List<ClaimLabelView>(); var end = 0;
        foreach (var link in links)
        {
            if (link.Start < end || revision.Body[end..link.Start].Any(c => !char.IsWhiteSpace(c))) return new(false, "UnlabeledContent", labels);
            var claim = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == link.ClaimRevisionId, ct);
            var head = await db.Claims.AsNoTracking().SingleAsync(x => x.Id == claim.ClaimId, ct);
            if (await db.EditorialCorrections.AnyAsync(x => x.ClaimRevisionId == claim.Id, ct)) return new(false, "EditorialClaimCorrectionRequiresReplacement", []);
            if (head.CurrentVersion != claim.Version) return new(false, "StaleClaim", labels);
            if (await db.ClaimCorrections.AnyAsync(c => c.ClaimRevisionId == claim.Id && c.Status == "Open", ct)) return new(false, "CorrectionPending", labels);
            if (claim.ReviewStatus != "Approved") return new(false, "InsufficientEvidence", labels);
            if (claim.Locale != content.LocaleId || revision.Body.Substring(link.Start, link.Length) != claim.Statement) return new(false, "ClaimTextMismatch", labels);
            if (revision.Kind == "Canonical" && claim.Type != "Quotation" || revision.Kind == "Translation" && claim.Type != "Translation" ||
                revision.Kind == "CurrentEvent" && claim.Type is not ("HistoricalClaim" or "NewsClaim") ||
                revision.Kind == "Interpretation" && claim.Type != "Interpretation" ||
                revision.Kind == "Historical" && claim.Type is not ("SourceQuotation" or "HistoricalClaim" or "Interpretation" or "FictionalScene")) return new(false, "ContentLabelMismatch", labels);
            var failure = await AssessAsync(s, claim, use, ct); if (failure != null) return new(false, failure, labels);
            labels.Add(new(claim.Id, revisionId, link.Start, link.Length, claim.Label, claim.Author, claim.Tradition, claim.Uncertainty, claim.Version)); end = link.Start + link.Length;
        }
        if (revision.Body[end..].Any(c => !char.IsWhiteSpace(c))) return new(false, "UnlabeledContent", labels);

        return new(true, "Eligible", labels);
    }
    public async Task<EvidenceGateResult> CheckAsync(CoreScope s, Guid u, Guid id, IntendedUse use, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AccessAsync(s, u, 0, ct);
        if (!await db.Revisions.AnyAsync(r => r.Id == id && db.Contents.Any(c => c.Id == r.ContentId && c.UniverseId == u && c.TenantId == s.TenantId), ct)) throw new CoreAccessException();
        try { await new SourceRightsGuard(db).CheckAsync(s, id, use, ct); }
        catch (RightsBlockedException e) { Audit(s, id, "evidence.gate-denied"); await SaveAsync(ct); await tx.CommitAsync(ct); return new(false, e.Reason, []); }
        var result = await CheckContentAsync(s, id, use, ct); Audit(s, id, result.Eligible ? "evidence.gate-allowed" : "evidence.gate-denied"); await SaveAsync(ct); await tx.CommitAsync(ct); return result;
    }
}
