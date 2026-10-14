using System.Security.Principal;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Foundation.Tests;
using Microsoft.Extensions.DependencyInjection;

var repo = Environment.GetEnvironmentVariable("GEETA_REPOSITORY") ?? throw new InvalidOperationException("Explicit repository required.");
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("M27 Windows demo requires integrated Windows identity.");
var dir = Path.Combine(repo, ".local", "m27-demo"); Directory.CreateDirectory(dir);
using var root = OptimizationTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
var owner = WindowsIdentity.GetCurrent().Name; var f = await OptimizationTestFixture.Setup(s, actor: owner); var w = s.GetRequiredService<IOptimizationWorkbench>();
await OptimizationTestFixture.Activate(s, f); var candidate = await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f), default);
if (!candidate.CandidateAccepted || candidate.SelectedRouteId != f.CandidateRoute) throw new InvalidOperationException("Candidate did not activate.");
var rolled = await w.MeasureAsync(f.Owner, f.Universe, f.Experiment, new(Guid.NewGuid(), "CorrectionRate", .2m, 2, "Fixture", "fixture:demo-guardrail", DateTimeOffset.UtcNow), default);
var baseline = await w.GenerateAsync(f.Owner, f.Universe, f.Experiment, OptimizationTestFixture.Generate(f), default);
if (rolled.State != "RolledBack" || baseline.SelectedRouteId != f.BaselineRoute) throw new InvalidOperationException("Rollback failed.");
var editor = s.GetRequiredService<IEditorialWorkbench>(); var highRisk = await editor.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f.Editorial) with { PublicIntended = false }, default);
var denied = false; try { await w.AutomateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "ScheduleApproved", highRisk.Id, highRisk.RevisionId, DateTimeOffset.UtcNow.AddMinutes(5), ExpectedRowVersion: highRisk.RowVersion), default); } catch (RuntimeValidationException) { denied = true; }
if (!highRisk.HighRisk || !denied || highRisk.Approvals.Length != 0) throw new InvalidOperationException("High-risk human review fence failed.");
var report = await w.AutomateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "ProviderReview"), default);
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var evidence = new { verifiedUtc = DateTimeOffset.UtcNow, database = "GeetaStudio_M1Tests", tenant = f.Owner.TenantId, universe = f.Universe, experiment = f.Experiment, candidateRoute = candidate.SelectedRouteId, candidateAccepted = candidate.CandidateAccepted, rollbackState = rolled.State, baselineRoute = baseline.SelectedRouteId, highRiskId = highRisk.Id, highRiskApprovalDenied = denied, highRiskApprovals = highRisk.Approvals.Length, fixtureOnly = true, liveCalls = 0, actualCharges = 0, publicPublishing = false, report };
File.WriteAllText(Path.Combine(dir, "fixture.json"), JsonSerializer.Serialize(evidence, options));
Console.WriteLine("M27 candidate activated, guardrail rolled back, baseline restored, and high-risk automation denied. Private fixtures only.");
namespace ContentOS.Foundation.Tests { internal static class ArchitectureTests { public static string RepositoryRoot => Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!; } }
