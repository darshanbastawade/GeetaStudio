using System.Data;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public sealed partial class DraftFactory
{
    private async Task<(Guid Work, Guid Token, Guid Tenant)?> Claim(CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var now = DateTimeOffset.UtcNow;
        var candidate = await db.FactoryBatches.AsNoTracking().Where(b => (b.State == "Queued" || b.State == "Running") && db.FactoryWorks.Any(w => w.BatchId == b.Id && (w.State == "Pending" || w.State == "Running" && w.LeaseUntilUtc <= now || w.State == "AwaitingWorkflow" && db.DurableWorkflows.Any(r => r.Id == w.WorkflowId && (r.State == "AwaitingReview" || r.State == "Completed" || r.State == "Failed" || r.State == "Cancelled" || r.State == "Rejected"))))).OrderBy(b => b.CreatedUtc).FirstOrDefaultAsync(ct);
        if (candidate == null) return null; await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Lock(candidate.TenantId, ct); var batch = await db.FactoryBatches.SingleAsync(b => b.Id == candidate.Id, ct);
        // Reload after the tenant admission lock: another worker or cancellation may have committed.
        await db.Entry(batch).ReloadAsync(ct); if (batch.State is not ("Queued" or "Running")) return null;
        var snap = Read<FactoryPlanSnapshot>((await Plan(new(batch.TenantId, batch.Actor), batch.UniverseId, batch.PlanId, ct)).SnapshotJson);
        var rows = await db.FactoryWorks.Where(w => w.BatchId == batch.Id && (w.State == "Pending" || w.State == "Running" && w.LeaseUntilUtc <= now || w.State == "AwaitingWorkflow" && db.DurableWorkflows.Any(r => r.Id == w.WorkflowId && (r.State == "AwaitingReview" || r.State == "Completed" || r.State == "Failed" || r.State == "Cancelled" || r.State == "Rejected")))).ToListAsync(ct);
        var work = rows.OrderByDescending(w => snap.Briefs.Single(b => b.Brief.Key == w.BriefKey).Brief.Priority).ThenBy(w => snap.Briefs.Single(b => b.Brief.Key == w.BriefKey).Brief.Position).ThenBy(w => Array.IndexOf(FactoryRules.Formats, w.Format)).FirstOrDefault();
        if (work == null) return null;
        if (batch.DeadlineUtc <= now) { batch.State = "Failed"; batch.Outcome = "DeadlineExceeded"; foreach (var unfinished in await db.FactoryWorks.Where(w => w.BatchId == batch.Id && (w.State == "Pending" || w.State == "Running" || w.State == "AwaitingWorkflow")).ToListAsync(ct)) { unfinished.State = "Failed"; unfinished.Outcome = "DeadlineExceeded"; unfinished.LeaseToken = null; unfinished.LeaseUntilUtc = null; } await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null; }
        if (work.Attempts >= 3 && work.WorkflowId == null) { work.State = "Failed"; work.Outcome = "AttemptsExhausted"; await Finish(batch, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null; }
        var token = Guid.NewGuid(); work.State = "Running"; work.Attempts++; work.LeaseToken = token; work.LeaseUntilUtc = now.AddSeconds(30); batch.State = "Running"; batch.Outcome = "";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return (work.Id, token, batch.TenantId);
    }
    private async Task Finish(FactoryBatch batch, CancellationToken ct)
    {
        var rows = await db.FactoryWorks.Where(w => w.BatchId == batch.Id).ToListAsync(ct);
        if (rows.All(w => w.State is not ("Pending" or "Running" or "AwaitingWorkflow"))) { batch.State = rows.Any(w => w.State is "Failed" or "RejectedDuplicate") ? "CompletedWithIssues" : "Completed"; batch.Outcome = batch.State == "Completed" ? "DraftInventoryOnly" : "InspectWorkOutcomes"; }
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (await ImportGeneration(ct)) return true;
        var claim = await Claim(ct); if (claim == null) return false;
        if (await QueueGeneration(claim.Value, ct)) return true;
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await Lock(claim.Value.Tenant, ct); var work = await db.FactoryWorks.SingleAsync(x => x.Id == claim.Value.Work, ct);
        var batch = await db.FactoryBatches.SingleAsync(x => x.Id == work.BatchId, ct);
        if (work.LeaseToken != claim.Value.Token || work.State != "Running") return true;
        if (batch.State is "Paused" or "Cancelled") { work.State = "Pending"; work.LeaseToken = null; work.LeaseUntilUtc = null; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true; }
        var scope = new CoreScope(batch.TenantId, batch.Actor); var plan = await Plan(scope, batch.UniverseId, batch.PlanId, ct); var snap = Read<FactoryPlanSnapshot>(plan.SnapshotJson);
        var input = snap.Briefs.Single(b => b.Brief.Key == work.BriefKey); var format = input.Brief.Formats.Single(f => f.Kind == work.Format);
        try
        {
            await Access(scope, batch.UniverseId, true, ct);
            await TrendFence.AssertPlan(db, plan.Id, ct);
            var trend = await TrendFence.Plan(db, plan.Id, ct);
            if (batch.DeadlineUtc <= DateTimeOffset.UtcNow) throw new RuntimeValidationException("Original deadline exceeded.", "DeadlineExceeded");
            await Recheck(scope, batch.UniverseId, input, true, true, ct);
            var fingerprint = Hash(new
            {
                Lesson = input.Brief.LessonVariantId,
                format.Kind,
                format.Instruction,
                Adapter = snap.Rules.Adapter,
                Cast = input.Cast.Cast.Select(c => new { c.Character.CharacterId, Profile = c.Character.Profile.Id, c.Role, Presentation = c.Presentation?.Id }),
                Relationships = input.Cast.Relationships.Select(r => r.Id)
            });
            if (trend != null) fingerprint = Hash(new { Base = fingerprint, trend.Id, trend.SnapshotJson });
            if (snap.GenerationWorkflow != null) fingerprint = Hash(new { Base = fingerprint, Work = work.Id, snap.GenerationWorkflow });
            var reusable = await db.FactoryArtifacts.AsNoTracking().SingleOrDefaultAsync(a => a.TenantId == batch.TenantId && a.UniverseId == batch.UniverseId && a.Fingerprint == fingerprint, ct);
            if (reusable != null)
            {
                if (reusable.ReviewStatus == "Rejected") throw new RuntimeValidationException("Existing derivative was rejected; explicitly revise its inputs.");
                if (Read<FactoryArtifactSnapshot>(reusable.SnapshotJson).Output.Body.Length > format.MaxCharacters) throw new RuntimeValidationException("Cached derivative exceeds the revised format limit; explicitly revise the treatment.");
                work.ArtifactId = reusable.Id; work.State = "Reused"; work.Outcome = "ExactPinnedDerivativeReused";
            }
            else
            {
                FactoryGenerationProvenance? generation = null;
                FactoryRenderResult output;
                if (snap.GenerationWorkflow != null)
                {
                    await RecheckGeneration(scope, batch.UniverseId, plan.Id, work.BriefKey, ct);
                    var generated = await GeneratedOutput(scope, work, input, format, ct); output = generated.Output; generation = generated.Provenance;
                    input = input with { Cast = input.Cast with { Beats = generation.Script.Dialogue.Select(d => new RegistryStoryBeat(d.CharacterId, "FictionalDialogue", d.Text, [], [], input.Cast.Cast.Single(x => x.Character.CharacterId == d.CharacterId).Role == "Lead")).ToArray() } };
                    RegistryRules.Story(input.Cast);
                }
                else output = templates.Single(t => t.Key == snap.Rules.Adapter).Render(new(input, format), ct);
                if (trend != null) output = TrendFence.Render(Read<TrendStorySnapshot>(trend.SnapshotJson), output, generation != null);
                if (generation == null) RegistryRules.Story(input.Cast with { Beats = input.Cast.Beats.Select(beat => beat with { Text = output.OriginalTreatment + " " + format.Instruction }).ToArray() });
                else
                {
                    var script = generation.Script;
                    var narrator = input.Cast.Cast.First().Character.CharacterId;
                    var sections = new[] { script.Hook, script.Scene, script.Decision, script.Reflection, format.Instruction };
                    RegistryRules.Story(input.Cast with { Beats = input.Cast.Beats.Concat(sections.Select(text => new RegistryStoryBeat(narrator, "FictionalNarration", text, [], []))).ToArray() });
                }
                if (output.TemplateVersion != snap.Rules.Adapter || output.Body.Length > format.MaxCharacters || string.IsNullOrWhiteSpace(output.Body)) throw new RuntimeValidationException("Template output violates bounded contract.");
                if (DateTimeOffset.UtcNow >= batch.DeadlineUtc || DateTimeOffset.UtcNow >= work.LeaseUntilUtc) throw new RuntimeValidationException("Generation lease/deadline expired.", "DeadlineExceeded");
                var prior = await db.FactoryArtifacts.AsNoTracking().Where(a => a.TenantId == batch.TenantId && a.UniverseId == batch.UniverseId && a.BriefKey == work.BriefKey && a.Format == work.Format && db.FactoryPlans.Any(p => p.Id == a.PlanId && p.Key == plan.Key)).OrderByDescending(a => a.CreatedUtc).FirstOrDefaultAsync(ct);
                var comparisons = await db.FactoryArtifacts.AsNoTracking().Where(a => a.TenantId == batch.TenantId && a.UniverseId == batch.UniverseId && a.AudienceId == input.Brief.Cast.AudienceId && a.Locale == input.Brief.Cast.Locale && a.Format == format.Kind && a.ReviewStatus != "Rejected").OrderByDescending(a => a.CreatedUtc).Take(501).ToListAsync(ct);
                if (comparisons.Count > 500) throw new RuntimeValidationException("Duplicate comparison cap reached; partition/archive explicitly.");
                var bodyHash = Hash(output.Body); var duplicate = prior == null && comparisons.Any(a => a.BodyHash == bodyHash || FactoryRules.Similarity(a.Treatment, output.OriginalTreatment) >= snap.Rules.DuplicateThreshold);
                if (duplicate) { work.State = "RejectedDuplicate"; work.Outcome = "TemplateOrTokenOverlapDuplicate"; }
                else
                {
                    var budget = Read<FactoryBudget>(batch.BudgetJson); if (batch.GeneratedCharacters + output.Body.Length > budget.MaxGeneratedCharacters) throw new RuntimeValidationException("Batch character budget exhausted.", "BudgetExceeded");
                    var subject = await db.Subjects.SingleOrDefaultAsync(s => s.UniverseId == batch.UniverseId && s.Key == "m11-drafts", ct);
                    if (subject == null) { subject = new() { Id = Guid.NewGuid(), UniverseId = batch.UniverseId, Key = "m11-drafts", Type = "Theme", Name = "Reviewed-input draft factory" }; db.Subjects.Add(subject); }
                    ContentItem content;
                    if (prior == null || prior.LessonVariantId != input.Brief.LessonVariantId || prior.AudienceId != input.Brief.Cast.AudienceId || prior.Locale != input.Brief.Cast.Locale) { content = new() { Id = Guid.NewGuid(), TenantId = batch.TenantId, UniverseId = batch.UniverseId, SubjectId = subject.Id, AudienceId = input.Brief.Cast.AudienceId, LocaleId = input.Brief.Cast.Locale, CurrentVersion = 1 }; db.Contents.Add(content); }
                    else { content = await db.Contents.SingleAsync(c => c.Id == prior.CoreContentId, ct); content.CurrentVersion++; content.EditorialState = "Draft"; }
                    var title = snap.Rules.Title + " — " + format.Kind; if (title.Length > 200) title = title[..200];
                    var revision = new ContentRevision { Id = Guid.NewGuid(), ContentId = content.Id, Version = content.CurrentVersion, OperationId = work.Id, Title = title, Body = output.Body, Kind = "Fiction", RightsStatus = "Licensed", Provenance = "M11 reviewed lesson " + input.Brief.LessonVariantId + "; original fiction and attributed evidence remain separately labeled." };
                    db.Revisions.Add(revision);
                    if (trend != null) TrendFence.Lineage(db, trend, revision.Id, output.Body, generation != null);
                    foreach (var source in input.Lesson.Snapshot.Bundle.SourceIds.Distinct()) db.Provenance.Add(new() { Id = Guid.NewGuid(), TenantId = batch.TenantId, ContentRevisionId = revision.Id, SnapshotId = source, Relation = "Adaptation" });
                    if (prior != null && prior.CoreContentId == content.Id) db.Provenance.Add(new() { Id = Guid.NewGuid(), TenantId = batch.TenantId, ContentRevisionId = revision.Id, ParentRevisionId = prior.CoreRevisionId, Relation = "Revision" });
                    if (generation != null)
                    {
                        var offset = trend == null ? 0 : "Verified factual context (separate from the story):\n".Length + Read<TrendStorySnapshot>(trend.SnapshotJson).FactualSummary.Length + 2;
                        foreach (var id in new[] { input.Lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId, input.Lesson.Snapshot.Bundle.Request.InterpretationClaimRevisionId })
                        {
                            var claimText = await db.ClaimRevisions.Where(x => x.Id == id).Select(x => x.Statement).SingleAsync(ct);
                            if (!output.Body.AsSpan(offset).StartsWith(claimText, StringComparison.Ordinal)) throw new EvidenceBlockedException("FactoryClaimTextMismatch");
                            db.ClaimLinks.Add(new() { Id = Guid.NewGuid(), ClaimRevisionId = id, ContentRevisionId = revision.Id, Start = offset, Length = claimText.Length }); offset += claimText.Length + 2;
                        }
                        var rubric = await db.ClaimRevisions.Where(x => x.Id == input.Lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId).Select(x => x.RubricId).SingleAsync(ct);
                        var claimId = await new EvidenceEngine(db).SubmitInTransaction(scope, batch.UniverseId,
                            new(work.Id, "factory-fiction-" + work.Id.ToString("N"), "", "FictionalScene", "FictionalScene", input.Brief.Cast.Locale, output.Body[offset..], "Generated original fiction", "", "Fictional", rubric, []), ct);
                        generation = generation with { FictionClaimRevisionId = claimId };
                        db.ClaimLinks.Add(new() { Id = Guid.NewGuid(), ClaimRevisionId = claimId, ContentRevisionId = revision.Id, Start = offset, Length = output.Body.Length - offset });
                    }
                    var artifact = new FactoryArtifact { Id = Guid.NewGuid(), TenantId = batch.TenantId, UniverseId = batch.UniverseId, PlanId = plan.Id, BriefKey = work.BriefKey, Format = work.Format, LessonVariantId = input.Brief.LessonVariantId, CoreContentId = content.Id, CoreRevisionId = revision.Id, ParentArtifactId = prior?.Id, AudienceId = input.Brief.Cast.AudienceId, Locale = input.Brief.Cast.Locale, Fingerprint = fingerprint, BodyHash = bodyHash, Treatment = output.OriginalTreatment, SnapshotJson = Json(new FactoryArtifactSnapshot(plan.Id, work.BriefKey, work.Format, input.Brief.LessonVariantId, input, snap.Rules.Adapter, output) { Generation = generation }) };
                    db.FactoryArtifacts.Add(artifact); work.ArtifactId = artifact.Id; work.State = "Generated"; work.Outcome = prior == null ? "NewDraft" : "ExplicitDerivativeRevision"; batch.GeneratedCharacters += output.Body.Length;
                }
            }
        }
        catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException or CoreAccessException or RuntimeValidationException or RuntimeConflictException)
        {
            work.State = "Failed"; work.Outcome = e is RuntimeValidationException v ? v.Outcome : e.GetType().Name;
        }
        if (snap.GenerationWorkflow != null) batch.GenerationCost = await (from w in db.FactoryWorks join step in db.WorkflowCheckpoints on w.WorkflowId equals (Guid?)step.RunId join ai in db.AiRuns on step.OperationId equals ai.OperationId where w.BatchId == batch.Id && ai.TenantId == batch.TenantId && step.Kind == "draft" select ai.Spent).SumAsync(ct);
        work.LeaseToken = null; work.LeaseUntilUtc = null; await Finish(batch, ct);
        db.SourceAudits.Add(new() { Id = Guid.NewGuid(), TenantId = batch.TenantId, Actor = batch.Actor, SubjectId = work.Id, Action = "factory.checkpoint." + work.State });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    private async Task<FactoryArtifactView> ArtifactView(CoreScope s, FactoryArtifact row, CancellationToken ct)
    {
        var snap = Read<FactoryArtifactSnapshot>(row.SnapshotJson); try
        {
            await TrendFence.AssertPlan(db, row.PlanId, ct); await Recheck(s, row.UniverseId, snap.Input, false, false, ct);
            if (snap.Generation != null) { await RecheckGeneration(s, row.UniverseId, row.PlanId, row.BriefKey, ct, false); if ((await services.GetRequiredService<IAiTaskRouter>().InspectAsync(s, snap.Generation.AiRunId, ct)).Output == null) throw new EvidenceBlockedException("AiOutputWithheld"); }
            return new(row.Id, row.CoreContentId, row.CoreRevisionId, row.ParentArtifactId, row.ReviewStatus, row.ReviewEvidence, Convert.ToBase64String(row.RowVersion), snap, false);
        }
        catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException or RuntimeValidationException) { return new(row.Id, row.CoreContentId, row.CoreRevisionId, row.ParentArtifactId, row.ReviewStatus, row.ReviewEvidence, Convert.ToBase64String(row.RowVersion), null, true); }
    }
    public async Task<IReadOnlyList<FactoryArtifactView>> ArtifactsAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct); var result = new List<FactoryArtifactView>(); foreach (var row in await db.FactoryArtifacts.AsNoTracking().Where(a => a.TenantId == s.TenantId && a.UniverseId == u).OrderByDescending(a => a.CreatedUtc).Take(500).ToListAsync(ct)) result.Add(await ArtifactView(s, row, ct)); return result;
    }
    public async Task ReviewArtifactAsync(CoreScope s, Guid u, Guid id, FactoryReview r, CancellationToken ct) => _ = await Command(s, u, r.OperationId, "artifact-review", new { id, r }, async () =>
    {
        RegistryRules.ReviewEvidence(r.EvidenceReference); var row = await db.FactoryArtifacts.SingleOrDefaultAsync(a => a.Id == id && a.TenantId == s.TenantId && a.UniverseId == u, ct) ?? throw new CoreAccessException();
        if (Convert.ToBase64String(row.RowVersion) != r.ExpectedRowVersion) throw new CoreConcurrencyException(); if (row.ReviewStatus != "Draft") throw new RuntimeConflictException("Artifact review frozen.");
        await TrendFence.AssertPlan(db, row.PlanId, ct);
        await Recheck(s, u, Read<FactoryArtifactSnapshot>(row.SnapshotJson).Input, false, false, ct); row.ReviewStatus = r.Approve ? "Approved" : "Rejected"; row.ReviewEvidence = r.EvidenceReference; return id;
    }, ct);
    private async Task<bool> PubliclyUsable(CoreScope scope, Guid universe, FactoryArtifact row, FactoryArtifactSnapshot snap, CancellationToken ct)
    {
        if (row.ReviewStatus != "Approved" || row.ReviewEvidence?.StartsWith("manual:") != true) return false;
        var p = await Plan(scope, universe, row.PlanId, ct); if (p.ReviewStatus != "Approved" || p.ReviewEvidence?.StartsWith("manual:") != true || snap.Input.Cast.Bible.ReviewEvidence?.StartsWith("manual:") != true ||
            snap.Input.Cast.Cast.Any(c => c.Character.Profile.ReviewEvidence?.StartsWith("manual:") != true || c.Presentation?.ReviewEvidence?.StartsWith("manual:") != true) || snap.Input.Cast.Relationships.Any(r => r.ReviewEvidence?.StartsWith("manual:") != true)) return false;
        try
        {
            var revision = await db.Revisions.AsNoTracking().SingleAsync(x => x.Id == row.CoreRevisionId, ct); var head = await db.Contents.AsNoTracking().SingleAsync(x => x.Id == row.CoreContentId, ct);
            if (head.EditorialState != "Approved" || head.CurrentVersion != revision.Version) return false;
            await lessons.ExportAsync(scope, universe, row.LessonVariantId, ct);
            var cast = await registry.CastAsync(scope, universe, snap.Input.Brief.Cast with { Mode = "Production", Budget = snap.Input.Brief.Cast.Budget with { RequireMedia = true } }, ct);
            if (cast.MissingRoles.Length > 0) return false;
            await new SourceRightsGuard(db).CheckAsync(scope, row.CoreRevisionId, new("Publish", row.Locale, "IN", "Text"), ct);
            return (await new EvidenceEngine(db).CheckContentAsync(scope, row.CoreRevisionId, new("Publish", row.Locale, "IN", "Text"), ct)).Eligible;
        }
        catch (Exception e) when (e is RightsBlockedException or EvidenceBlockedException or RuntimeValidationException) { return false; }
    }
    public async Task<IReadOnlyList<FactoryCoverage>> CoverageAsync(CoreScope s, Guid u, CancellationToken ct)
    {
        await Access(s, u, false, ct);
        var planRows = await db.FactoryPlans.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).OrderByDescending(x => x.CreatedUtc).Take(201).ToListAsync(ct);
        var raw = await db.FactoryArtifacts.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.UniverseId == u).Take(501).ToListAsync(ct);
        var work = await (from w in db.FactoryWorks.AsNoTracking() join b in db.FactoryBatches.AsNoTracking() on w.BatchId equals b.Id where w.TenantId == s.TenantId && w.UniverseId == u select new { Work = w, Plan = b.PlanId }).Take(5001).ToListAsync(ct);
        if (planRows.Count > 200 || raw.Count > 500 || work.Count > 5000) throw new RuntimeValidationException("Coverage capacity reached; pagination/archival is required, totals are not silently truncated.");
        var plans = planRows.ToDictionary(p => p.Id, p => Read<FactoryPlanSnapshot>(p.SnapshotJson));
        var metadata = raw.ToDictionary(a => a.Id, a => Read<FactoryArtifactSnapshot>(a.SnapshotJson));
        var views = await ArtifactsAsync(s, u, ct);
        var latest = planRows.Where(p => p.ReviewStatus != "Rejected" && !planRows.Any(n => n.Key == p.Key && n.Version > p.Version));
        var all = latest.SelectMany(p => plans[p.Id].Briefs.SelectMany(b => b.Brief.Formats.SelectMany(f => b.Brief.Concepts.Select(c => new { Plan = p.Id, Brief = b, Format = f.Kind, Concept = c })))).ToArray();
        var recorded = await db.SourceUses.AsNoTracking().Where(x => x.TenantId == s.TenantId && x.Published && x.Mode == "Recorded" && !x.ExternalReference.StartsWith("fixture:") && x.ExternalReference != "").Select(x => x.ContentRevisionId).Distinct().ToListAsync(ct);
        var result = new List<FactoryCoverage>();
        foreach (var group in all.GroupBy(x => new { x.Brief.Brief.Chapter, x.Brief.Brief.Verse, x.Concept, x.Brief.Brief.Cast.AudienceId, x.Brief.Brief.Cast.Locale, x.Format }))
        {
            bool Matches(FactoryBrief b, string format) => b.Chapter == group.Key.Chapter && b.Verse == group.Key.Verse && b.Concepts.Contains(group.Key.Concept) && b.Cast.AudienceId == group.Key.AudienceId && b.Cast.Locale == group.Key.Locale && format == group.Key.Format;
            var values = group.ToArray(); var selected = raw.Where(a => Matches(metadata[a.Id].Input.Brief, a.Format)).ToArray();
            var linked = work.Where(w => plans.TryGetValue(w.Plan, out var p) && p.Briefs.Any(b => b.Brief.Key == w.Work.BriefKey && Matches(b.Brief, w.Work.Format))).Select(w => w.Work).ToArray();
            var published = selected.Where(a => recorded.Contains(a.CoreRevisionId)).ToArray(); var usable = 0; foreach (var a in published) if (await PubliclyUsable(s, u, a, metadata[a.Id], ct)) usable++;
            var fatigue = metadata.Values.Where(a => a.Input.Brief.Concepts.Contains(group.Key.Concept)).Select(a => new { a.Input.Brief.LessonVariantId, a.Input.Brief.Cast.EpisodeId, a.BriefKey }).Distinct().Count();
            var priority = Math.Max(0, values.Max(x => x.Brief.Brief.Priority) - Math.Min(50, fatigue * 5));
            result.Add(new(group.Key.Chapter, group.Key.Verse, group.Key.Concept, group.Key.AudienceId, group.Key.Locale, group.Key.Format, values.Length, selected.Length, selected.Count(a => a.ReviewStatus == "Approved"), published.Length, usable, linked.Count(w => w.State == "Reused"), linked.Count(w => w.State == "RejectedDuplicate"), selected.Count(a => views.Single(v => v.Id == a.Id).OutputWithheld), fatigue, priority));
        }
        return result;
    }
    public async Task<IReadOnlyList<FactoryPlanView>> ProposePilotsAsync(CoreScope s, Guid u, FactoryPilotRequest r, CancellationToken ct)
    {
        if (r.LessonVariantIds == null || r.LessonVariantIds.Length is < 1 or > 3) throw new RuntimeValidationException("Select one to three reviewed pilot lessons.");
        var briefs = new List<FactoryBrief>(); var position = 0;
        foreach (var lessonId in r.LessonVariantIds.Distinct())
        {
            var lesson = await Lesson(s, u, lessonId, true, ct); var text = lesson.Snapshot.Bundle.Request.SanskritTextId ?? await db.ClaimSpans.Where(x => x.ClaimRevisionId == lesson.Snapshot.Bundle.Request.AnchorClaimRevisionId && x.CorpusTextId != null).Select(x => x.CorpusTextId!.Value).FirstAsync(ct);
            var verse = await (from t in db.CorpusTexts join v in db.CorpusVerses on t.VerseId equals v.Id where t.Id == text select v).SingleAsync(ct);
            briefs.Add(new("verse-" + verse.Chapter + "-" + verse.Verse, lessonId, r.Cast, r.Relationships, lesson.Sections!.Concepts, lesson.Sections.LearningGoals[0], verse.Chapter, verse.Verse, "Reviewed pilot", ++position, 50, FactoryRules.Formats.Select(f => new FactoryFormat(f, "Preserve source attribution and original fictional treatment.")).ToArray()));
        }
        var ordered = briefs.OrderBy(b => b.Chapter).ThenBy(b => b.Verse).Select((b, i) => b with { Position = i + 1 }).ToArray();
        Guid Op(string type) => new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(r.OperationId + type))[..16]);
        return [await RegisterPlanAsync(s,u,new(Op("chronological"),r.Key+"-chronological",1,"Chronological pilot","Chronological","local-template.a.v1",ordered),ct),
            await RegisterPlanAsync(s,u,new(Op("practical"),r.Key+"-practical",1,"Practical reflection pilot","PracticalTheme","local-template.a.v1",briefs.Select(b=>b with{Journey="Reflection and practical action"}).ToArray()),ct)];
    }
}
