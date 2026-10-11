using System.Net;
using System.Net.Http.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlFactoryTests
{
    [SqlFact]
    public async Task Two_verse_six_format_batch_pauses_restarts_resumes_and_replays_without_duplicate_content()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var p = await FactoryTestFixture.Approved(s, f);
        var req = FactoryTestFixture.Start(p); var batch = await factory.StartAsync(f.Owner, f.Universe, req, default); Assert.Equal(12, batch.Work.Length);
        Assert.True(await factory.ProcessNextAsync(default)); batch = (await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(b => b.Id == batch.Id);
        var first = Assert.Single(batch.Work, w => w.State == "Generated").ArtifactId;
        await factory.ControlAsync(f.Owner, f.Universe, batch.Id, new(Guid.NewGuid(), batch.RowVersion, "Pause"), default);
        using var restart = root.CreateScope(); var restarted = restart.ServiceProvider.GetRequiredService<IDraftFactory>(); batch = (await restarted.BatchesAsync(f.Owner, f.Universe, default)).Single(b => b.Id == batch.Id);
        Assert.Equal("Paused", batch.State); await restarted.ControlAsync(f.Owner, f.Universe, batch.Id, new(Guid.NewGuid(), batch.RowVersion, "Resume"), default);
        await FactoryTestFixture.Drain(restart.ServiceProvider);
        batch = (await restarted.BatchesAsync(f.Owner, f.Universe, default)).Single(b => b.Id == batch.Id); Assert.Equal("Completed", batch.State); Assert.Equal(first, batch.Work.Single(w => w.ArtifactId == first).ArtifactId);
        var replay = await restarted.StartAsync(f.Owner, f.Universe, req with { OperationId = Guid.NewGuid() }, default); Assert.Equal(batch.Id, replay.Id); Assert.Equal(12, (await restarted.ArtifactsAsync(f.Owner, f.Universe, default)).Count);
        var coverage = await restarted.CoverageAsync(f.Owner, f.Universe, default); Assert.Equal(12, coverage.Sum(c => c.Generated)); Assert.All(coverage, c => Assert.Equal(0, c.Published)); Assert.All(coverage, c => Assert.Equal(0, c.Reviewed)); Assert.Equal(0, batch.GenerationCost); Assert.True(batch.GeneratedCharacters <= batch.Budget.MaxGeneratedCharacters);
        var artifacts = await restarted.ArtifactsAsync(f.Owner, f.Universe, default); Assert.All(artifacts, a => { Assert.NotNull(a.Snapshot); Assert.NotEmpty(a.Snapshot!.Input.Lesson.Snapshot.Bundle.Citations); Assert.Single(a.Snapshot.Input.Cast.Cast); Assert.Equal(f.Character, a.Snapshot.Input.Cast.Cast[0].Character.CharacterId); });
    }
    [SqlFact]
    public async Task Changed_derivative_versions_only_it_while_unchanged_derivatives_reuse_stable_ids()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var req = FactoryTestFixture.Request(f, second: false); var p = await FactoryTestFixture.Approved(s, f, req);
        await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p), default); await FactoryTestFixture.Drain(s); var before = await factory.ArtifactsAsync(f.Owner, f.Universe, default);
        var formats = req.Briefs[0].Formats.Select(x => x.Kind == "Story" ? x with { Instruction = "Use the same source and add an explicit context-checking decision to this revised draft." } : x).ToArray();
        var next = await FactoryTestFixture.Approved(s, f, req with { OperationId = Guid.NewGuid(), Version = 2, Briefs = [req.Briefs[0] with { Formats = formats }] });
        var batch = await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(next, "explicit-revision"), default); await FactoryTestFixture.Drain(s);
        batch = (await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(b => b.Id == batch.Id); Assert.Equal(5, batch.Work.Count(w => w.State == "Reused")); Assert.Single(batch.Work, w => w.State == "Generated");
        var after = await factory.ArtifactsAsync(f.Owner, f.Universe, default); Assert.Equal(7, after.Count); var revision = after.Single(a => a.ParentArtifactId != null); var parent = before.Single(a => a.Snapshot!.Format == "Story"); Assert.Equal(parent.Id, revision.ParentArtifactId); Assert.Equal(parent.CoreContentId, revision.CoreContentId); Assert.NotEqual(parent.CoreRevisionId, revision.CoreRevisionId);
    }
    [SqlFact]
    public async Task Duplicate_treatment_is_reported_separately_from_draft_coverage_and_reviews()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var req = FactoryTestFixture.Request(f, second: false);
        var p = await FactoryTestFixture.Approved(s, f, req); await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p), default); await FactoryTestFixture.Drain(s);
        var duplicate = await FactoryTestFixture.Approved(s, f, req with { OperationId = Guid.NewGuid(), Key = "different-plan", Briefs = [req.Briefs[0] with { Key = "different-brief", Formats = req.Briefs[0].Formats.Select(x => x with { Instruction = "A reordered equivalent request; flag duplicate treatment." }).ToArray() }] });
        var batch = await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(duplicate, "duplicate"), default); await FactoryTestFixture.Drain(s);
        batch = (await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(b => b.Id == batch.Id); Assert.All(batch.Work, w => Assert.Equal("RejectedDuplicate", w.State));
        var a = (await factory.ArtifactsAsync(f.Owner, f.Universe, default))[0]; await factory.ReviewArtifactAsync(f.Owner, f.Universe, a.Id, new(Guid.NewGuid(), a.RowVersion, true, "fixture:editorial-review-only"), default);
        var coverage = await factory.CoverageAsync(f.Owner, f.Universe, default); Assert.Equal(6, coverage.Sum(c => c.RejectedDuplicates)); Assert.Equal(1, coverage.Sum(c => c.Reviewed)); Assert.All(coverage, c => Assert.Equal(0, c.Published));
    }
    [SqlFact]
    public async Task Budget_cancel_and_expired_original_deadline_prevent_new_accepted_output()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var p = await FactoryTestFixture.Approved(s, f, FactoryTestFixture.Request(f, second: false));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p) with { Budget = new(0, "INR", 500, 600, 1) }, default));
        var b = await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p), default); await factory.ControlAsync(f.Owner, f.Universe, b.Id, new(Guid.NewGuid(), b.RowVersion, "Cancel"), default); await FactoryTestFixture.Drain(s); Assert.Empty(await factory.ArtifactsAsync(f.Owner, f.Universe, default));
        Assert.All((await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == b.Id).Work, w => Assert.Equal("Cancelled", w.State));
        var expired = await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p, "expired"), default); var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var row = await db.FactoryBatches.SingleAsync(x => x.Id == expired.Id); row.DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); await factory.ProcessNextAsync(default);
        Assert.Equal("Failed", (await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == expired.Id).State); Assert.Empty(await factory.ArtifactsAsync(f.Owner, f.Universe, default));
    }
    [SqlFact]
    public async Task Revocation_withholds_source_bearing_snapshots_and_fails_unfinished_work()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var p = await FactoryTestFixture.Approved(s, f, FactoryTestFixture.Request(f, second: false));
        await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p), default); await factory.ProcessNextAsync(default);
        var sources = s.GetRequiredService<ISourceRegistry>(); var details = await sources.InspectAsync(f.Owner, f.Universe, f.SourceWork, default); await sources.RevokeAsync(f.Owner, f.Universe, f.SourceWork, new(Guid.NewGuid(), null, "fixture:withdraw-test-permission", details.Work.RowVersion), default);
        Assert.All(await factory.ArtifactsAsync(f.Owner, f.Universe, default), a => { Assert.True(a.OutputWithheld); Assert.Null(a.Snapshot); });
        Assert.True((await factory.PlansAsync(f.Owner, f.Universe, default)).Single().OutputWithheld); await FactoryTestFixture.Drain(s);
        var b = (await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(); Assert.Equal(5, b.Work.Count(w => w.State == "Failed"));
        var core = s.GetRequiredService<IContentCore>(); var db = s.GetRequiredService<CoreDbContext>(); var content = await db.FactoryArtifacts.Where(a => a.UniverseId == f.Universe).Select(a => a.CoreRevisionId).SingleAsync();
        await Assert.ThrowsAsync<RightsBlockedException>(() => new SourceRightsGuard(db).CheckAsync(f.Owner, content, new("Read", "en-IN", "IN", "Text"), default));
        var coverage = await factory.CoverageAsync(f.Owner, f.Universe, default); Assert.Equal(1, coverage.Sum(c => c.Generated)); Assert.Equal(1, coverage.Sum(c => c.Withheld)); Assert.All(coverage, c => Assert.Equal(0, c.PubliclyUsable));
    }
    [SqlFact]
    public async Task Invalid_life_stage_relationships_media_and_retired_cast_cannot_bypass_reviewed_planning()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var request = FactoryTestFixture.Request(f, second: false);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => factory.RegisterPlanAsync(f.Owner, f.Universe, request with { Briefs = [request.Briefs[0] with { Verse = 22 }] }, default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => factory.RegisterPlanAsync(f.Owner, f.Universe, request with { Briefs = [request.Briefs[0] with { Formats = [new("Story", "Krishna said: invented fictional quotation")] }] }, default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => factory.RegisterPlanAsync(f.Owner, f.Universe, request with { Briefs = [request.Briefs[0] with { RelationshipRevisionIds = [Guid.NewGuid()] }] }, default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => factory.RegisterPlanAsync(f.Owner, f.Universe, request with { Briefs = [request.Briefs[0] with { Cast = request.Briefs[0].Cast with { Mode = "Production", Budget = new(4, 0, "INR", true) } }] }, default));
        var p = await FactoryTestFixture.Approved(s, f, request); var registry = s.GetRequiredService<IUniverseRegistry>(); var c = (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single();
        await registry.ControlCharacterAsync(f.Owner, f.Universe, c.CharacterId, new(Guid.NewGuid(), c.RowVersion, "Retire", "fixture:retirement"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p), default));
    }
    [SqlFact]
    public async Task Concurrent_business_replay_and_staff_scope_headers_are_enforced()
    {
        using var root = FactoryTestFixture.Services(); using var setup = root.CreateScope(); var s = setup.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var p = await FactoryTestFixture.Approved(s, f, FactoryTestFixture.Request(f, second: false)); var req = FactoryTestFixture.Start(p);
        using var a = root.CreateScope(); using var b = root.CreateScope(); var results = await Task.WhenAll(a.ServiceProvider.GetRequiredService<IDraftFactory>().StartAsync(f.Owner, f.Universe, req, default), b.ServiceProvider.GetRequiredService<IDraftFactory>().StartAsync(f.Owner, f.Universe, req with { OperationId = Guid.NewGuid() }, default)); Assert.Equal(results[0].Id, results[1].Id);
        var factory = s.GetRequiredService<IDraftFactory>(); await Assert.ThrowsAsync<RuntimeConflictException>(() => factory.StartAsync(f.Owner, f.Universe, req with { OperationId = Guid.NewGuid(), Budget = req.Budget with { MaxItems = 59 } }, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => factory.StartAsync(new(f.Owner.TenantId, "m09-reader"), f.Universe, req with { OperationId = Guid.NewGuid() }, default));
        using var host = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder => { builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Runtime:Enabled"] = "true", ["ConnectionStrings:ContentOS"] = LessonTestFixture.Connection, ["Staff:Administrator:0"] = "integration-test-Administrator", ["Staff:Reader:0"] = "integration-test-Reader", ["Factory:WorkerEnabled"] = "false", ["Workflows:WorkerEnabled"] = "false", ["Knowledge:IndexWorkerEnabled"] = "false" })); builder.ConfigureServices(TestAuthentication.Configure); });
        using var client = host.CreateClient(); var path = $"/api/v1/factory/tenants/{f.Owner.TenantId}/universes/{f.Universe}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/plans")).StatusCode); client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/batches", req)).StatusCode); client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/factory/tenants/{Guid.NewGuid()}/universes/{f.Universe}/plans")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/batches", req)).StatusCode);
        await factory.ControlAsync(f.Owner, f.Universe, results[0].Id, new(Guid.NewGuid(), results[0].RowVersion, "Cancel"), default);
    }
    [SqlFact]
    public async Task Local_template_version_switch_and_reviewed_pilot_proposals_keep_old_snapshots()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>();
        var pilots = await factory.ProposePilotsAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "pilot-proposals", [f.SecondLesson, f.Lesson], FactoryTestFixture.Cast(f), []), default); Assert.Equal(2, pilots.Count); Assert.All(pilots, p => Assert.Equal("Draft", p.ReviewStatus)); Assert.Equal(new[] { 1, 2 }, pilots.Single(p => p.Snapshot!.Rules.JourneyKind == "Chronological").Snapshot!.Briefs.Select(b => b.Brief.Verse));
        var req = FactoryTestFixture.Request(f, second: false); var p = await FactoryTestFixture.Approved(s, f, req); await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p), default); await FactoryTestFixture.Drain(s);
        var next = await FactoryTestFixture.Approved(s, f, req with { OperationId = Guid.NewGuid(), Version = 2, Adapter = "local-template.b.v1" }); await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(next, "template-b"), default); await FactoryTestFixture.Drain(s);
        var all = await factory.ArtifactsAsync(f.Owner, f.Universe, default); Assert.Equal(6, all.Count(a => a.Snapshot!.Adapter == "local-template.a.v1")); Assert.Equal(6, all.Count(a => a.Snapshot!.Adapter == "local-template.b.v1"));
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var frozen = await db.FactoryArtifacts.SingleAsync(x => x.Id == all[0].Id); frozen.SnapshotJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task Expired_claim_is_recovered_once_and_two_workers_do_not_duplicate_accepted_artifacts()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var p = await FactoryTestFixture.Approved(s, f, FactoryTestFixture.Request(f, second: false));
        var batch = await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(p), default);
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var row = await db.FactoryWorks.Where(x => x.BatchId == batch.Id).OrderBy(x => x.Format).FirstAsync(); row.State = "Running"; row.Attempts = 1; row.LeaseToken = Guid.NewGuid(); row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        using var a = root.CreateScope(); using var b = root.CreateScope(); await Task.WhenAll(a.ServiceProvider.GetRequiredService<IDraftFactory>().ProcessNextAsync(default), b.ServiceProvider.GetRequiredService<IDraftFactory>().ProcessNextAsync(default));
        await FactoryTestFixture.Drain(s); var artifacts = await factory.ArtifactsAsync(f.Owner, f.Universe, default); Assert.Equal(6, artifacts.Count); Assert.Equal(6, artifacts.Select(x => x.CoreRevisionId).Distinct().Count());
        batch = (await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == batch.Id); Assert.Equal(2, batch.Work.Single(x => x.Id == row.Id).Attempts);
    }
    [SqlFact]
    public async Task An_unseen_compatible_mentor_is_allowed_while_an_adult_child_lead_is_rejected()
    {
        using var root = FactoryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s); var registry = s.GetRequiredService<IUniverseRegistry>(); var factory = s.GetRequiredService<IDraftFactory>(); var request = FactoryTestFixture.Request(f, second: false);
        var rf = new RegistryTestFixture.Fixture(f.Owner, f.Universe, f.Audience, f.Bible, f.Episode);
        var rules = RegistryTestFixture.Rules(rf, "Unseen coach", "Mentor", "Adult") with { SituationTags = ["collaboration"], SkillTags = [], LearningObjectives = ["understand-context"] };
        var coach = await registry.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "unseen-coach", "Guest", rules), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, coach.Profile.Id); await registry.ControlCharacterAsync(f.Owner, f.Universe, coach.CharacterId, new(Guid.NewGuid(), coach.RowVersion, "Activate", "fixture:coach"), default);
        var plan = await FactoryTestFixture.Approved(s, f, request with { Briefs = [request.Briefs[0] with { Cast = request.Briefs[0].Cast with { Roles = ["Lead", "Mentor"] } }] });
        Assert.Contains(plan.Snapshot!.Briefs[0].Cast.Cast, c => c.Character.CharacterId == coach.CharacterId && c.Role == "Mentor");
        var adult = await registry.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "adult-lead", "Guest", rules with { Name = "Adult lead", Roles = ["Lead"], VisualAnchor = "adult-lead-visual", VoiceAnchor = "adult-lead-voice" }), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, adult.Profile.Id); await registry.ControlCharacterAsync(f.Owner, f.Universe, adult.CharacterId, new(Guid.NewGuid(), adult.RowVersion, "Activate", "fixture:adult"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => factory.RegisterPlanAsync(f.Owner, f.Universe, request with { OperationId = Guid.NewGuid(), Key = "bad-age", Briefs = [request.Briefs[0] with { Cast = request.Briefs[0].Cast with { PinnedChoices = [new(adult.CharacterId, "Lead")] } }] }, default));
    }
}
