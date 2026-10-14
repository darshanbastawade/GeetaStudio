# M27 local learning loop and bounded automation

From Windows PowerShell in `C:\dev\GeetaStudio`, use the pinned SDK 10.0.401 and compatible .NET/ASP.NET Core runtime 10.0.13 or later patch. Local SQL Server must be available. No paid provider, social account or external download is required with the existing package cache.

```powershell
Set-Location C:\dev\GeetaStudio
dotnet restore .\GeetaStudio.sln --locked-mode
dotnet build .\GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
.\scripts\db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
.\scripts\optimization-demo.ps1
```

The demo adds isolated private fixtures to the Test database, records separate baseline/development/held-out runs and eight synthetic guardrails, activates fixture B for a private adult cohort, generates through the pinned M07 route, fails a correction guardrail, restores fixture A and denies scheduling an unapproved child item. It starts the Windows-authenticated API on port 5100, verifies the staff page/report and durable rollback, restarts its API, then stops only its own processes. It preserves existing route heads and records. Results are in `.local/m27-demo/fixture.json`, `report.json`, API logs and `docs/milestones/evidence/M27-demo.json`. Do not run it alongside SQL tests or another host on port 5100.

For the normal Development database and existing Windows staff credentials:

```powershell
.\scripts\db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
dotnet run --project .\src\GeetaStudio.Api --no-build
```

Open `http://localhost:5100/staff/optimization`. Use your existing tenant/universe IDs from `/staff/core`. Existing API user-secrets and explicit staff-role mappings are retained; see `LOCAL_DEVELOPMENT.md`. Development provisioning adds only new table SELECT/INSERT and bounded experiment-column UPDATE permissions. The Test demo's IDs belong to the Test database; they will not appear in Development.

To inspect the Test fixture interactively and debug:

```powershell
.\scripts\optimization-demo.ps1 -DebugPause -ReuseFixture
```

The script prints its owned API process ID and pauses. Open this folder in VS Code, run **Geeta Studio: M27 attach Api**, select that exact process, and set a breakpoint in `OptimizationOperations.ReportAsync` or `OptimizationWorkbench.ControlAsync`. Open `/staff/optimization`, enter the tenant/universe IDs from `.local/m27-demo/fixture.json`, and load the report. Resume the debugger before continuing the script. VS Code tasks **Geeta Studio: M27 demo** and **Geeta Studio: M27 debug demo** invoke the same script. Existing API/Web/Worker launch and compound profiles remain available. A new interactive F5/breakpoint session is not claimed as verified.

Administrator requests require `X-ContentOS-Staff: 1`. The page supplies it. After loading a scope, select a trial and use its activation/rollback controls, measurement fields or report menu; recorded results and review gates appear in readable text. Detailed proposal/evaluation requests remain in the advanced section. Evidence-required templates require the evidence-answer route. Request examples for `/api/v1/optimization/tenants/{tenant}/universes/{universe}`:

- `POST /experiments`: `OptimizationProposal` with a new operation/key/version, explicit hypothesis, same scoped adult audience/locale, dimension Prompt/Provider/Retrieval/Narrative/Format/Schedule, `PrivateFixture` cohort, Shadow/Canary mode, immutable baseline/candidate IDs, all eight guardrails, case/sample minimums, dispatch/cost ceilings, currency and UTC deadline. The fixture helper demonstrates the complete payload; use `fixture.json` IDs only against Test. Prompt experiments additionally pin two registered M08 template IDs matching route prompt versions; generation supplies declared `inputs` and an empty request prompt. Runtime canary/shadow dispatch is enabled for prompt/provider generation only; the other dimensions retain recorded evaluation/configuration evidence.
- `POST /experiments/{id}/evaluation`: new operation ID plus baseline, development and held-out run IDs. Runs must have the same dataset, exact candidates, audience/locale and baseline lineage. Held-out cases cannot be rescored for tuning.
- `POST /experiments/{id}/measurements`: new operation ID, metric, value, sample count, `origin: "Fixture"`, fixture evidence reference and current observation time. A breach immediately rolls back the experiment.
- `POST /experiments/{id}/control`: new operation ID, action `Activate` or `Rollback`, the latest row version and fixture evidence reference. New decisions require new operation IDs; exact command replay is idempotent.
- `POST /automation`: new operation ID and kind CostReport/RightsAlert/FailedJobTriage/ProviderReview. DependencyReview additionally needs bounded version/documentation/date/deadline snapshots. ScheduleApproved additionally pins item/revision/row version and a UTC time within the existing 30-day CMS limit. It accepts only currently approved, private, adult, source-free original fiction and does not publish it.

The optional in-process API monitor is disabled by default. To enable recorded periodic rights/cost/triage reports and deadline/evidence rollback for explicitly activated fixtures, set `$env:Optimization__MonitorEnabled='true'` before starting the API. It sweeps every 60 seconds, rechecks the owner's administrator permission, and records at most one report per scope/UTC date. Failed candidate outcomes and submitted guardrail breaches roll back immediately; candidate attempts are fenced at admission and in-flight cancellation is polled every 200 ms. Stop the API and remove the environment override to disable the monitor. Reports/alerts stay in the workbench and send no messages.

Focused checks:

```powershell
$env:GEETA_SQL_TESTS='1'
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlOptimizationTests'
Remove-Item Env:GEETA_SQL_TESTS
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'Category!=Sql'
```

Real human learning, historian/religious/child-policy, consent, rights, production/API freshness and provider qualification remain pending. Quiz feedback is a measurement, not proof of learning effectiveness. M27 does not duplicate payments/publishing, move stateful provider resources or grant publication approval. All three M27 migrations are applied by the normal update command. Prototype generation-input redaction is irreversible, with original replay hashes and identities preserved. Schema rollback refuses to delete populated optimization evidence; disable experiments/monitor and use a compatible restore or reviewed forward fix.
