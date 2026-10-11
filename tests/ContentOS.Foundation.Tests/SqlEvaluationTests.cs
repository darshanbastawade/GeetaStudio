using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlEvaluationTests
{
    [SqlFact]
    public async Task Versioned_120_case_reports_switch_wording_and_keep_fixture_human_advisory_separate()
    {
        using var root = EvaluationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s);
        var e = s.GetRequiredService<IQualityEvaluation>(); var dataset = await e.RegisterDatasetAsync(f.Owner, f.Universe, EvaluationFixtureCatalog.Create(f.Audience), default);
        foreach (var c in dataset.Dataset.Cases) await e.ReviewAsync(f.Owner, f.Universe, EvaluationTestFixture.Review(dataset.Id, c.Key), default);
        var a = await e.RegisterCandidateAsync(f.Owner, f.Universe, EvaluationTestFixture.Candidate("a"), default);
        var b = await e.RegisterCandidateAsync(f.Owner, f.Universe, EvaluationTestFixture.Candidate("b", "evaluation.fixture.b.v1"), default);
        var baseline = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, a.Id, "Development"), default);
        Assert.Equal(100, baseline.Results.Length); Assert.All(baseline.Results, x => Assert.True(x.Passed)); Assert.Equal(0, baseline.Qualification.HumanAccepted);
        var candidate = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, b.Id, "Development", baseline.Id), default);
        Assert.All(candidate.Results, x => Assert.True(x.Passed)); Assert.NotEqual(baseline.Results[0].Output!.Body, candidate.Results[0].Output!.Body);
        var held = new RunEvaluation(Guid.NewGuid(), dataset.Id, b.Id, "HeldOut", baseline.Id);
        var release = await e.RunAsync(f.Owner, f.Universe, held, default); Assert.Equal(20, release.Results.Length);
        await e.ReviewAsync(f.Owner, f.Universe, EvaluationTestFixture.Review(dataset.Id, release.Results[0].CaseKey, release.Id), default);
        await e.AdviseAsync(f.Owner, f.Universe, new(Guid.NewGuid(), release.Id, release.Results[0].CaseKey, "fixture.judge", "judge.v1", "prompt.v1", 1, "Fluent fixture; advisory only.", "fixture:judge"), default);
        using var restart = root.CreateScope(); var persisted = (await restart.ServiceProvider.GetRequiredService<IQualityEvaluation>().ReportsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == release.Id);
        Assert.Equal(1, persisted.Qualification.AdvisoryCount); Assert.Equal(1, persisted.Qualification.FixtureAccepted); Assert.Equal(0, persisted.Qualification.HumanAccepted); Assert.Equal("Blocked", persisted.Qualification.Status);
        Assert.Contains("100HumanReviewedDevelopmentCasesRequired", persisted.Qualification.Blocks); Assert.Empty(persisted.Qualification.PassedCapabilitiesAndLocales);
        Assert.Equal(1, release.Qualification.RetrievalTarget); Assert.NotNull(release.Qualification.LatencyTargetMs);
        Assert.Equal(release.Id, (await e.RunAsync(f.Owner, f.Universe, held, default)).Id);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => e.RunAsync(f.Owner, f.Universe, held with { OperationId = Guid.NewGuid(), CandidateId = a.Id }, default));
        var refresh = await e.RegisterDatasetAsync(f.Owner, f.Universe, dataset.Dataset with { OperationId = Guid.NewGuid(), Version = 2 }, default);
        var refreshedBase = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), refresh.Id, a.Id, "Development"), default);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), refresh.Id, a.Id, "HeldOut", refreshedBase.Id), default));
        var list = await e.DatasetsAsync(f.Owner, f.Universe, default); Assert.All(list, d => Assert.DoesNotContain(d.Dataset.Cases, c => c.Split == "HeldOut")); Assert.Equal(120, list.Single(x => x.Id == dataset.Id).FixtureReviewedCases);
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var row = await db.EvaluationRuns.SingleAsync(x => x.Id == baseline.Id); row.SnapshotJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<RuntimeValidationException>(() => e.ReviewAsync(f.Owner, f.Universe, EvaluationTestFixture.Review(dataset.Id, release.Results[0].CaseKey, release.Id, "Human"), default));
    }
    [SqlFact]
    public async Task Fabricated_source_revoked_rights_unsafe_and_fluent_story_failures_block_with_real_corpus_and_cast()
    {
        using var root = EvaluationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s, "m12-owner");
        var db = s.GetRequiredService<CoreDbContext>(); var e = s.GetRequiredService<IQualityEvaluation>();
        var text = await (from t in db.CorpusTexts join v in db.CorpusVerses on t.VerseId equals v.Id join m in db.CorpusManifests on v.ManifestId equals m.Id where m.UniverseId == f.Universe && t.Locale == "en-IN" && v.Chapter == 1 && v.Verse == 1 select t.Id).FirstAsync();
        var source = EvaluationFixtureCatalog.Create(f.Audience, text, FactoryTestFixture.Cast(f)); source = source with { Cases = source.Cases.Where(c => c.Key.EndsWith("-1")).ToArray() };
        var dataset = await e.RegisterDatasetAsync(f.Owner, f.Universe, source, default);
        var a = await e.RegisterCandidateAsync(f.Owner, f.Universe, EvaluationTestFixture.Candidate("a"), default);
        var good = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, a.Id, "Development"), default); Assert.All(good.Results, x => Assert.True(x.Passed));
        foreach (var key in new[] { "evaluation.fabricated.v1", "evaluation.unsafe.v1", "evaluation.fluent-failure.v1" })
        {
            var c = await e.RegisterCandidateAsync(f.Owner, f.Universe, EvaluationTestFixture.Candidate(key, key), default);
            var bad = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, c.Id, "Development", good.Id), default);
            Assert.Equal("Blocked", bad.Qualification.Status); Assert.Contains("DeterministicFailures", bad.Qualification.Blocks);
            Assert.Contains(bad.Results.SelectMany(x => x.Deterministic), x => key == "evaluation.fabricated.v1" ? x.Code == "FabricatedCitation" : key == "evaluation.unsafe.v1" ? x.Code == "ForbiddenProperty" : x.Code == "InappropriateCast");
        }
        db.ChangeTracker.Clear(); var work = await db.SourceWorks.SingleAsync(x => x.Id == f.SourceWork); work.Revoked = true; await db.SaveChangesAsync();
        var after = (await e.ReportsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == good.Id);
        Assert.Contains(after.Results.SelectMany(x => x.Deterministic), x => x.Code == "Rights"); Assert.Null(after.Results.Single(x => x.CaseKey == "case-01-1").Output);
        Assert.Contains("DeterministicFailures", after.Qualification.Blocks);
    }
    [SqlFact]
    public async Task Current_guest_promotion_alias_retirement_wrong_voice_and_continuity_are_checked()
    {
        using var root = EvaluationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s, "m12-owner");
        var registry = s.GetRequiredService<IUniverseRegistry>();
        var guest = await registry.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "neerja-fixture", "Guest", RegistryTestFixture.Rules(f, "Neerja") with { Aliases = ["Neeraja"] }), default);
        await RegistryTestFixture.Review(s, f.Owner, f.Universe, guest.Profile.Id);
        await registry.ControlCharacterAsync(f.Owner, f.Universe, guest.CharacterId, new(Guid.NewGuid(), guest.RowVersion, "Activate", "fixture:guest"), default);
        guest = (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single();
        var e = s.GetRequiredService<IQualityEvaluation>(); var c = EvaluationFixtureCatalog.Create(f.Audience, cast: RegistryTestFixture.Brief(f)).Cases.First(c => c.Category == "unseen-guest");
        var db = s.GetRequiredService<CoreDbContext>(); Assert.Equal(guest.CharacterId, (await db.Aliases.SingleAsync(x => x.UniverseId == f.Universe && x.Name == "Neeraja")).CharacterId);
        var dataset = await e.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "cast", 1, "Actual registry fixture, no human claim.", [c with { ExpectedAlias = "Neeraja", ExpectedCharacterId = guest.CharacterId }]), default);
        var candidate = await e.RegisterCandidateAsync(f.Owner, f.Universe, EvaluationTestFixture.Candidate(), default);
        var run = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development"), default); Assert.True(Assert.Single(run.Results).Passed);
        await registry.ControlCharacterAsync(f.Owner, f.Universe, guest.CharacterId, new(Guid.NewGuid(), guest.RowVersion, "PromoteGuest", "fixture:promotion"), default);
        guest = (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single(); Assert.Equal("Recurring", guest.PersistenceScope);
        var promoted = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development"), default); Assert.Equal(guest.CharacterId, Assert.Single(promoted.Results[0].Output!.Cast).CharacterId);
        await registry.ControlCharacterAsync(f.Owner, f.Universe, guest.CharacterId, new(Guid.NewGuid(), guest.RowVersion, "Retire", "fixture:retirement"), default);
        var historical = (await e.ReportsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == run.Id); Assert.Contains(historical.Results[0].Deterministic, x => x.Code == "InappropriateCast");
        var extra = await RegistryTestFixture.Character(s, f, "episode-extra", scope: "EpisodeOnly");
        var nextEpisode = await registry.RegisterEpisodeAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "next-episode"), default);
        var noCast = c with
        {
            Key = "other-episode",
            Category = "continuity",
            ExpectNoAnswer = true,
            RequiredProperties = ["no suitable cast"],
            Cast = RegistryTestFixture.Brief(f) with { EpisodeId = nextEpisode, PinnedChoices = [new(extra.CharacterId, "Lead")] }
        };
        var other = await e.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "other-episode", 1, "Episode-only identity cannot cross episode scope.", [noCast]), default);
        var refused = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), other.Id, candidate.Id, "Development"), default);
        Assert.True(refused.Results[0].Passed); Assert.True(refused.Results[0].Output!.NoAnswer); Assert.Empty(refused.Results[0].Output!.Cast);
    }
    [SqlFact]
    public async Task Concurrent_replay_scope_http_header_and_reader_boundaries_are_enforced()
    {
        using var root = EvaluationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s);
        var e = s.GetRequiredService<IQualityEvaluation>(); var request = EvaluationFixtureCatalog.Create(f.Audience); request = request with { Cases = [request.Cases[0]] };
        using var a = root.CreateScope(); using var b = root.CreateScope(); var results = await Task.WhenAll(a.ServiceProvider.GetRequiredService<IQualityEvaluation>().RegisterDatasetAsync(f.Owner, f.Universe, request, default), b.ServiceProvider.GetRequiredService<IQualityEvaluation>().RegisterDatasetAsync(f.Owner, f.Universe, request, default)); Assert.Equal(results[0].Id, results[1].Id);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => e.RegisterDatasetAsync(f.Owner, f.Universe, request with { Rationale = "changed" }, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => e.DatasetsAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => e.RegisterDatasetAsync(new(f.Owner.TenantId, "m10-reader"), f.Universe, request with { OperationId = Guid.NewGuid(), Version = 2 }, default));
        using var host = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder => { builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Runtime:Enabled"] = "true", ["ConnectionStrings:ContentOS"] = LessonTestFixture.Connection, ["Staff:Administrator:0"] = "integration-test-Administrator", ["Staff:Reader:0"] = "integration-test-Reader", ["Factory:WorkerEnabled"] = "false", ["Workflows:WorkerEnabled"] = "false", ["Knowledge:IndexWorkerEnabled"] = "false" })); builder.ConfigureServices(TestAuthentication.Configure); });
        using var client = host.CreateClient(); var path = $"/api/v1/evaluation/tenants/{f.Owner.TenantId}/universes/{f.Universe}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/reports")).StatusCode); client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/datasets", request)).StatusCode); client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/staff/evaluation")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/evaluation/tenants/{Guid.NewGuid()}/universes/{f.Universe}/datasets")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/datasets", request)).StatusCode);
    }
    [SqlFact]
    public async Task Recorded_execution_requires_actual_scoped_body_prompt_route_cost_and_nonfixture_qualification()
    {
        using var root = EvaluationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s);
        var e = s.GetRequiredService<IQualityEvaluation>(); var admin = s.GetRequiredService<IAiAdministration>(); var router = s.GetRequiredService<IAiTaskRouter>();
        await admin.BudgetAsync(f.Owner, new("ai.text", "USD", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5), 1), default);
        var profile = await admin.RegisterAsync(f.Owner, new(SqlAiTests.Settings(streaming: false)), default);
        var route = await admin.RouteAsync(f.Owner, new("draft", "recorded.v1", [profile], .1m, "USD"), default); await admin.ActivateAsync(f.Owner, route, default);
        var ai = await router.GenerateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "draft", "Original controlled fixture answer.", Streaming: false), default);
        Assert.Equal("Completed", ai.State);
        var c = EvaluationFixtureCatalog.Create(f.Audience).Cases.First() with { RequiredProperties = [], ForbiddenProperties = [] };
        var dataset = await e.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "recorded", 1, "M07 fixture provenance counterexample.", [c]), default);
        var candidate = await e.RegisterCandidateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "recorded", 1, new("recorded.v1", ai.Snapshot.Route.PromptVersion, "retrieval-none.v1", "candidate.v1", "release.v1", "fixture.text.a", route)), default);
        var output = new EvaluationOutput(ai.Output!.Text, "en-IN", [], false, [], [], null, [], ai.Spent, "USD");
        var report = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development", RecordedOutputs: [new(c.Key, output, AiRunId: ai.Id)]), default);
        Assert.Contains(report.Results[0].Deterministic, x => x.Code == "ExecutionProvenance"); Assert.Equal("M07CompletedAttempts", report.Results[0].LatencyKind);
        Assert.Equal(ai.Spent, report.Qualification.Cost); Assert.Equal("USD", report.Qualification.Currency); Assert.Equal("Blocked", report.Qualification.Status);
        var unattested = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development", RecordedOutputs: [new(c.Key, output)]), default);
        Assert.Contains(unattested.Results[0].Deterministic, x => x.Code == "ExecutionProvenance"); Assert.Equal("ImportValidationOnly", unattested.Results[0].LatencyKind);
        await Assert.ThrowsAsync<CoreAccessException>(() => e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development", RecordedOutputs: [new(c.Key, output, AiRunId: Guid.NewGuid())]), default));
    }
    [SqlFact]
    public async Task Recorded_factory_rights_are_rechecked_and_arbitrary_transcript_references_are_not_evidence()
    {
        using var root = EvaluationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s, "m12-owner");
        var factory = s.GetRequiredService<IDraftFactory>(); var e = s.GetRequiredService<IQualityEvaluation>(); var request = FactoryTestFixture.Request(f, second: false);
        request = request with { Briefs = [request.Briefs[0] with { Formats = [request.Briefs[0].Formats[0]] }] };
        var plan = await FactoryTestFixture.Approved(s, f, request); await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(plan), default); await FactoryTestFixture.Drain(s);
        var artifact = Assert.Single(await factory.ArtifactsAsync(f.Owner, f.Universe, default));
        var c = EvaluationFixtureCatalog.Create(f.Audience).Cases.First() with { RequiredProperties = [], ForbiddenProperties = [] };
        var dataset = await e.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "artifact", 1, "Current artifact rights fixture.", [c]), default);
        var candidate = await e.RegisterCandidateAsync(f.Owner, f.Universe, EvaluationTestFixture.Candidate("recorded", "recorded.v1"), default);
        var output = new EvaluationOutput(artifact.Snapshot!.Output.Body, "en-IN", [], false, [], [], null, [], 0, "INR");
        var report = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development", RecordedOutputs: [new(c.Key, output, artifact.Id)]), default);
        Assert.NotNull(report.Results[0].Output); Assert.Equal(artifact.Id, report.Results[0].FactoryArtifactId);
        var mediaCase = c with { Key = "media", Category = "unavailable-transcript", RequiresMedia = true };
        var media = await e.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "media", 1, "Unqualified media reference.", [mediaCase]), default);
        var rejected = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), media.Id, candidate.Id, "Development", RecordedOutputs: [new("media", output with { MediaEvidenceReference = "fixture:a-transcript-that-does-not-exist" })]), default);
        Assert.Contains(rejected.Results[0].Deterministic, x => x.Code == "MissingMediaEvidence");
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var work = await db.SourceWorks.SingleAsync(x => x.Id == f.SourceWork); work.Revoked = true; await db.SaveChangesAsync();
        var current = (await e.ReportsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == report.Id);
        Assert.Null(current.Results[0].Output); Assert.Null(current.Results[0].Case); Assert.Contains(current.Results[0].Deterministic, x => x.Code == "Rights");
    }
    [SqlFact]
    public async Task Actual_owned_retrieval_is_measured_and_alias_cutover_cannot_silently_change_the_candidate()
    {
        using var root = EvaluationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await FactoryTestFixture.Setup(s, "m12-owner");
        var db = s.GetRequiredService<CoreDbContext>(); var target = await (from t in db.CorpusTexts join v in db.CorpusVerses on t.VerseId equals v.Id join m in db.CorpusManifests on v.ManifestId equals m.Id where m.UniverseId == f.Universe && t.Locale == "en-IN" && v.Chapter == 1 && v.Verse == 1 select new { t.Id, v.ManifestId }).FirstAsync();
        var admin = s.GetRequiredService<IKnowledgeAdministration>(); var worker = s.GetRequiredService<IKnowledgeIndexWorker>();
        var indexId = await admin.QueueAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m12-query", target.ManifestId, "en-IN", "IN", "sql-hybrid-v1", "concept-projection-v1"), default);
        KnowledgeIndexView index;
        for (var attempt = 0; ; attempt++)
        {
            index = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == indexId);
            if (index.State == "Ready") break;
            Assert.True(attempt < 20); await worker.ProcessNextAsync(default);
        }
        await admin.ValidateAsync(f.Owner, f.Universe, indexId, new(index.RowVersion, "fixture:retrieval-gate-behavior", [new("On the field of Kuru?", target.Id), new("How do I bake a pizza?", null)]), default);
        index = (await admin.IndicesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == indexId);
        await admin.ActivateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "m12-active", indexId, "", index.EmbeddingSpaceId), default);
        var all = EvaluationFixtureCatalog.Create(f.Audience, target.Id); var c = all.Cases.First(c => c.Category == "real-citation") with { RetrievalQuery = "On the field of Kuru?" };
        var no = all.Cases.First(c => c.Category == "no-answer") with { RetrievalQuery = "How do I bake a pizza?" };
        var e = s.GetRequiredService<IQualityEvaluation>(); var dataset = await e.RegisterDatasetAsync(f.Owner, f.Universe, all with { Cases = [c, no] }, default);
        var config = new EvaluationConfiguration("evaluation.fixture.a.v1", "prompt.v1", index.EmbeddingSpaceId, "retrieval-candidate.v1", "release.v1", Retrieval: new("m12-active", "", "en-IN", "IN", index.EmbeddingSpaceId, 1), RetrievalIndexId: indexId);
        var candidate = await e.RegisterCandidateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "retrieval", 1, config), default);
        var baseline = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development"), default);
        Assert.All(baseline.Results, x => Assert.True(x.Passed)); Assert.Equal(1, baseline.Qualification.RetrievalAccuracy);
        Assert.All(baseline.Results, x => Assert.Equal(indexId, x.Retrieval!.IndexId)); Assert.Empty(baseline.Results.Single(x => x.CaseKey == no.Key).Retrieval!.CorpusTextIds);
        db.ChangeTracker.Clear(); var alias = await db.KnowledgeAliases.SingleAsync(x => x.UniverseId == f.Universe && x.Key == "m12-active"); alias.ActiveIndexId = null; await db.SaveChangesAsync();
        var changed = await e.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development", baseline.Id), default);
        Assert.Contains(changed.Results.SelectMany(x => x.Deterministic), x => x.Code == "RetrievalBinding");
        var historical = (await e.ReportsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == baseline.Id);
        Assert.Contains("RetrievalConfigurationChanged", historical.Qualification.Blocks); Assert.Equal(indexId, historical.Results[0].Retrieval!.IndexId);
    }
}
