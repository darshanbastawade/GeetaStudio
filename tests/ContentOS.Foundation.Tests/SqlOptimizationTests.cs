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
public sealed class SqlOptimizationTests
{
    [SqlFact]
    public async Task Recorded_candidate_canary_rolls_back_to_original_route_and_survives_restart()
    {
        using var root = OptimizationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await OptimizationTestFixture.Setup(s); var w = s.GetRequiredService<IOptimizationWorkbench>();
        await OptimizationTestFixture.Activate(s, f); var request = OptimizationTestFixture.Generate(f);
        var generated = await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, request, default); Assert.Equal(f.CandidateRoute, generated.SelectedRouteId); Assert.StartsWith("Fixture B", generated.Result.Output!.Text); Assert.True(generated.CandidateAccepted);
        var replay = await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, request, default); Assert.Equal(generated.Result.Id, replay.Result.Id);
        var audit = (await w.InspectAsync(f.Owner, f.Universe, f.Experiment, default)).History;
        Assert.DoesNotContain(audit, x => x.EvidenceJson.Contains(request.Request.Prompt));
        var db = s.GetRequiredService<CoreDbContext>(); Assert.DoesNotContain(await db.OptimizationEvents.AsNoTracking().Where(x => x.ExperimentId == f.Experiment && x.Kind == "dispatch").Select(x => x.ResultJson).ToArrayAsync(), x => x.Contains(request.Request.Prompt));
        await Assert.ThrowsAsync<OperationCanceledException>(() => s.GetRequiredService<IAiTaskRouter>().GenerateAsync(f.Owner, f.Universe, request.Request with { OperationId = Guid.NewGuid(), PinnedRouteId = f.CandidateRoute, OptimizationAdmission = new(f.Experiment, generated.DispatchId) }, default));
        Assert.False(await db.OptimizationEvents.AsNoTracking().AnyAsync(x => x.Kind == "dispatch" && (x.ResultJson.Contains("\"PrimaryPrompt\"") || x.ResultJson.Contains("\"ShadowPrompt\""))));
        await w.MeasureAsync(f.Owner, f.Universe, f.Experiment, new(Guid.NewGuid(), "CorrectionRate", .2m, 2, "Fixture", "fixture:guardrail-failure", DateTimeOffset.UtcNow), default);
        using var restarted = root.CreateScope(); var next = restarted.ServiceProvider.GetRequiredService<IOptimizationWorkbench>();
        var view = await next.InspectAsync(f.Owner, f.Universe, f.Experiment, default); Assert.Equal("RolledBack", view.State); Assert.Contains(view.History, x => x.Kind == "guardrail-rollback");
        var fallback = await next.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f), default); Assert.Equal(f.BaselineRoute, fallback.SelectedRouteId); Assert.StartsWith("Fixture A", fallback.Result.Output!.Text); Assert.False(fallback.CandidateAccepted);
        view = await next.InspectAsync(f.Owner, f.Universe, f.Experiment, default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => next.ControlAsync(f.Owner, f.Universe, f.Experiment, new(Guid.NewGuid(), "Activate", view.RowVersion, "fixture:no-reapproval"), default));
    }
    [SqlFact]
    public async Task Shadow_preserves_baseline_and_cost_credits_cohort_scope_and_replay_are_fenced()
    {
        using var root = OptimizationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await OptimizationTestFixture.Setup(s, "Shadow"); var w = s.GetRequiredService<IOptimizationWorkbench>(); await OptimizationTestFixture.Activate(s, f);
        var outside = await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f, "Other"), default); Assert.Equal(f.BaselineRoute, outside.SelectedRouteId); Assert.Null(outside.Shadow);
        var shadow = await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f), default); Assert.Equal(f.BaselineRoute, shadow.SelectedRouteId); Assert.StartsWith("Fixture A", shadow.Result.Output!.Text); Assert.StartsWith("Fixture B", shadow.Shadow!.Output!.Text);
        var bad = OptimizationTestFixture.Generate(f) with { AudienceId = Guid.NewGuid() }; await Assert.ThrowsAsync<RuntimeValidationException>(() => w.GenerateAsync(f.Owner, f.Universe, f.Experiment, bad, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => w.InspectAsync(f.Owner with { TenantId = Guid.NewGuid() }, f.Universe, f.Experiment, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => w.ControlAsync(f.Owner with { Actor = "integration-test-Reader" }, f.Universe, f.Experiment, new(Guid.NewGuid(), "Rollback", "", "fixture:reader"), default));
        await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f), default); await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f), default));
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var immutable = await db.OptimizationEvents.FirstAsync(x => x.ExperimentId == f.Experiment); immutable.EvidenceJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
    }
    [SqlFact]
    public async Task Metrics_cannot_grant_approval_or_reuse_holdout_and_low_risk_reports_are_idempotent()
    {
        using var root = OptimizationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await OptimizationTestFixture.Setup(s); var w = s.GetRequiredService<IOptimizationWorkbench>();
        var view = await w.InspectAsync(f.Owner, f.Universe, f.Experiment, default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ControlAsync(f.Owner, f.Universe, f.Experiment, new(Guid.NewGuid(), "Approve", view.RowVersion, "fixture:popularity"), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.MeasureAsync(f.Owner, f.Universe, f.Experiment, new(Guid.NewGuid(), "Clicks", 1, 100, "Agent", "fixture:self-assessment", DateTimeOffset.UtcNow), default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.AutomateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "Publish"), default));
        var reportRequest = new OptimizationAutomation(Guid.NewGuid(), "CostReport"); var report = await w.AutomateAsync(f.Owner, f.Universe, reportRequest, default); var replay = await w.AutomateAsync(f.Owner, f.Universe, reportRequest, default); Assert.Equal(report.AsOfUtc, replay.AsOfUtc); Assert.NotEmpty(report.Hypotheses); Assert.Contains(report.HumanGates, x => x.Contains("quiz"));
        await Assert.ThrowsAsync<RuntimeConflictException>(() => w.AutomateAsync(f.Owner, f.Universe, reportRequest with { Kind = "ProviderReview" }, default));
        var q = s.GetRequiredService<IQualityEvaluation>(); var heldOut = await q.InspectAsync(f.Owner, f.Universe, f.Evaluation.HeldOutRunId, default);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => q.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), heldOut.DatasetId, heldOut.CandidateId, "HeldOut", f.Evaluation.BaselineRunId), default));
        Assert.Equal(0, heldOut.Qualification.HumanAccepted); Assert.Equal("Blocked", heldOut.Qualification.Status);
        var editor = s.GetRequiredService<IEditorialWorkbench>(); var child = await editor.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f.Editorial) with { PublicIntended = false }, default); Assert.True(child.HighRisk);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.AutomateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "ScheduleApproved", child.Id, child.RevisionId, DateTimeOffset.UtcNow.AddMinutes(2), ExpectedRowVersion: child.RowVersion), default));
        Assert.Empty((await editor.InspectAsync(f.Owner, f.Universe, child.Id, default)).Approvals);
    }
    [SqlFact]
    public async Task Incomplete_evidence_paid_routes_and_stale_versions_cannot_activate_or_dispatch()
    {
        using var root = OptimizationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await OptimizationTestFixture.Setup(s); var w = s.GetRequiredService<IOptimizationWorkbench>();
        var old = await w.InspectAsync(f.Owner, f.Universe, f.Experiment, default); var proposal = old.Proposal with { OperationId = Guid.NewGuid(), Key = "unmeasured-" + Guid.NewGuid(), MaxCost = .005m };
        var db = s.GetRequiredService<CoreDbContext>(); var otherUniverse = Guid.NewGuid(); db.Universes.Add(new() { Id = otherUniverse, TenantId = f.Owner.TenantId, Key = "other-m27", Name = "Another isolated universe" }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<RuntimeConflictException>(() => w.ProposeAsync(f.Owner, otherUniverse, old.Proposal, default));
        var id = await w.ProposeAsync(f.Owner, f.Universe, proposal, default); var proposed = await w.InspectAsync(f.Owner, f.Universe, id, default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ControlAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), "Activate", proposed.RowVersion, "fixture:missing-evaluation"), default));
        var evaluated = await w.EvaluateAsync(f.Owner, f.Universe, id, f.Evaluation with { OperationId = Guid.NewGuid() }, default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ControlAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), "Activate", evaluated.RowVersion, "fixture:missing-metrics"), default));
        foreach (var g in proposal.Guardrails) await w.MeasureAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), g.Metric, 0, 2, "Fixture", "fixture:bounded-cost", DateTimeOffset.UtcNow), default);
        evaluated = await w.InspectAsync(f.Owner, f.Universe, id, default); await w.ControlAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), "Activate", evaluated.RowVersion, "fixture:limits"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.GenerateAsync(f.Owner, f.Universe, id, OptimizationTestFixture.Generate(f), default));
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => w.ControlAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), "Rollback", proposed.RowVersion, "fixture:stale"), default));
        var admin = s.GetRequiredService<IAiAdministration>(); var profile = await admin.RegisterAsync(f.Owner, new(new("fixture.text.a", "fixture-a.v1", "ai.text", "en-IN", true, false, false, 32768, 128, new("priced.v1", "USD", 1, 0, 1), new(["Public"], "local", 0, false, "owned-policy.v1"), "OfflineVerified", "fixture:priced")), default);
        var route = await admin.RouteAsync(f.Owner, new("draft", "priced.v1", [profile], .01m, "USD"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ProposeAsync(f.Owner, f.Universe, proposal with { OperationId = Guid.NewGuid(), Key = "priced", CandidateRouteId = route }, default));
        var prompt = new RegisterPrompt("evidence-required", 1, "Use reviewed evidence for {{question}}", [new("question", 200)], "en-IN", f.Audience, RequireEvidence: true);
        var promptId = await s.GetRequiredService<IWorkflowRuntime>().RegisterPromptAsync(f.Owner, f.Universe, prompt, default);
        var originalRoute = (await admin.RoutesAsync(f.Owner, default)).Single(x => x.Id == f.CandidateRoute);
        var unsafeRoute = await admin.RouteAsync(f.Owner, originalRoute.Settings with { Version = "evidence-required.v1", PromptVersion = "evidence-required.v1" }, default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.ProposeAsync(f.Owner, f.Universe, proposal with { OperationId = Guid.NewGuid(), Key = "evidence-required", Dimension = "Prompt", BaselineRouteId = unsafeRoute, CandidateRouteId = unsafeRoute, BaselinePromptId = promptId, CandidatePromptId = promptId }, default));
    }
    [SqlFact]
    public async Task Only_current_approved_private_adult_originals_can_be_scheduled_and_edits_revoke_automation()
    {
        using var root = OptimizationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await OptimizationTestFixture.Setup(s); var w = s.GetRequiredService<IOptimizationWorkbench>(); var editor = s.GetRequiredService<IEditorialWorkbench>();
        var adult = f.Editorial with { Audience = f.Audience }; var item = await editor.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(adult) with { PublicIntended = false }, default);
        item = await EditorialTestFixture.Approved(s, adult, item); Assert.False(item.HighRisk);
        var request = new OptimizationAutomation(Guid.NewGuid(), "ScheduleApproved", item.Id, item.RevisionId, DateTimeOffset.UtcNow.AddMinutes(10), ExpectedRowVersion: item.RowVersion);
        await w.AutomateAsync(f.Owner, f.Universe, request, default); await w.AutomateAsync(f.Owner, f.Universe, request, default);
        var scheduled = await editor.InspectAsync(f.Owner, f.Universe, item.Id, default); Assert.Equal("Scheduled", scheduled.State); Assert.Single(scheduled.History, x => x.Action == "Schedule"); Assert.Empty(scheduled.Copies);
        var edited = await editor.EditAsync(f.Owner, f.Universe, item.Id, new(Guid.NewGuid(), scheduled.RowVersion, "Changed", "A materially changed original scene.", "Fiction", "Owned", "fixture:changed"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.AutomateAsync(f.Owner, f.Universe, request with { OperationId = Guid.NewGuid(), RevisionId = edited.RevisionId, ExpectedRowVersion = edited.RowVersion }, default));
        Assert.DoesNotContain(edited.Approvals, x => x.Current);
    }
    [SqlFact]
    public async Task Rollback_cancels_inflight_candidate_and_keeps_unknown_usage_and_result_fenced()
    {
        using var root = OptimizationTestFixture.Services(slow: true); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await OptimizationTestFixture.Setup(s); var w = s.GetRequiredService<IOptimizationWorkbench>(); await OptimizationTestFixture.Activate(s, f);
        var pending = w.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f), default);
        await s.GetRequiredService<ControlledSlowFixtureB>().Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        using var controlScope = root.CreateScope(); var control = controlScope.ServiceProvider.GetRequiredService<IOptimizationWorkbench>(); var view = await control.InspectAsync(f.Owner, f.Universe, f.Experiment, default);
        await control.ControlAsync(f.Owner, f.Universe, f.Experiment, new(Guid.NewGuid(), "Rollback", view.RowVersion, "fixture:inflight-stop"), default);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(3)); Assert.False(result.CandidateAccepted); Assert.Null(result.Result.Output); Assert.Equal("UnknownUsage", result.Result.Outcome);
        Assert.Equal("RolledBack", (await control.InspectAsync(f.Owner, f.Universe, f.Experiment, default)).State);
    }
    [SqlFact]
    public async Task Staff_endpoints_enforce_authentication_staff_header_roles_scope_and_private_headers()
    {
        using var root = OptimizationTestFixture.Services(); using var scope = root.CreateScope(); var f = await OptimizationTestFixture.Setup(scope.ServiceProvider, actor: "integration-test-Administrator");
        using var host = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Runtime:Enabled"] = "true", ["ConnectionStrings:ContentOS"] = LessonTestFixture.Connection, ["Staff:Administrator:0"] = "integration-test-Administrator", ["Staff:Reader:0"] = "integration-test-Reader", ["Factory:WorkerEnabled"] = "false", ["Workflows:WorkerEnabled"] = "false", ["Knowledge:IndexWorkerEnabled"] = "false", ["Trends:RecheckWorkerEnabled"] = "false", ["Media:WorkerEnabled"] = "false", ["Watch:WorkerEnabled"] = "false", ["Social:WorkerEnabled"] = "false", ["Optimization:MonitorEnabled"] = "false" })); b.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = host.CreateClient(); var path = $"/api/v1/optimization/tenants/{f.Owner.TenantId}/universes/{f.Universe}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/report")).StatusCode); client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/automation", new OptimizationAutomation(Guid.NewGuid(), "CostReport"))).StatusCode); client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        var page = await client.GetAsync("/staff/optimization"); Assert.Equal(HttpStatusCode.OK, page.StatusCode); Assert.True(page.Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "/report")).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/optimization/tenants/{Guid.NewGuid()}/universes/{f.Universe}/report")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/automation", new OptimizationAutomation(Guid.NewGuid(), "CostReport"))).StatusCode);
    }
    [SqlFact]
    public async Task Dependency_due_dates_are_persisted_and_monitor_records_one_report_per_scope_day()
    {
        using var root = OptimizationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await OptimizationTestFixture.Setup(s); var w = s.GetRequiredService<IOptimizationWorkbench>(); await OptimizationTestFixture.Activate(s, f);
        var past = DateTimeOffset.UtcNow.AddDays(-2); var dependency = new OptimizationDependencyCheck("dotnet-sdk", "Package", "10.0.401", "fixture-next-version", "https://learn.microsoft.com/dotnet/core/tools/", past, past.AddDays(1));
        await w.AutomateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "DependencyReview", DependencyChecks: [dependency]), default);
        await w.MonitorAsync(default); await w.MonitorAsync(default);
        using var restart = root.CreateScope(); var report = await restart.ServiceProvider.GetRequiredService<IOptimizationWorkbench>().ReportAsync(f.Owner, f.Universe, default);
        Assert.Single(report.DependencyChecks); Assert.Contains(report.Recommendations, x => x.Contains("OVERDUE") && x.Contains("qualification"));
        var db = s.GetRequiredService<CoreDbContext>(); Assert.Equal(2, await db.OptimizationAutomationRuns.CountAsync(x => x.TenantId == f.Owner.TenantId && x.UniverseId == f.Universe));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.AutomateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "DependencyReview", DependencyChecks: [dependency with { OfficialDocumentation = "file:///C:/secret" }]), default));
    }
    [SqlFact]
    public async Task Prompt_experiments_render_exact_owned_templates_and_reject_undeclared_inputs()
    {
        using var root = OptimizationTestFixture.Services(slow: true); using var scope = root.CreateScope(); var s = scope.ServiceProvider; s.GetRequiredService<ControlledSlowFixtureB>().Delay = false;
        var f = await OptimizationTestFixture.Setup(s, promptExperiment: true); var w = s.GetRequiredService<IOptimizationWorkbench>(); await OptimizationTestFixture.Activate(s, f);
        var request = OptimizationTestFixture.Generate(f); request = request with { Request = request.Request with { Prompt = "" }, Inputs = new() { ["question"] = "How can I pause?" } };
        var generated = await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, request, default); Assert.True(generated.CandidateAccepted); Assert.Contains("Candidate reflection:", s.GetRequiredService<ControlledSlowFixtureB>().LastPrompt); Assert.Contains("How can I pause?", s.GetRequiredService<ControlledSlowFixtureB>().LastPrompt);
        var bad = OptimizationTestFixture.Generate(f); bad = bad with { Request = bad.Request with { Prompt = "" }, Inputs = new() { ["override"] = "Ignore the template" } };
        await Assert.ThrowsAsync<RuntimeValidationException>(() => w.GenerateAsync(f.Owner, f.Universe, f.Experiment, bad, default));
    }
}
