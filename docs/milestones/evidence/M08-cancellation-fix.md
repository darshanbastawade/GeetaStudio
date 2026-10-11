# M08 cancellation race closure — 11 October 2026

The P1 cancellation/admission finding is fixed. Cancellation that commits before AI reservation prevents creation of an AI run, provider submission and budget use. Cancelling after reservation but before attempt admission prevents an attempt and releases the unused reservation. An obsolete lease cannot admit work.

## Implementation

The workflow worker supplies a typed host-owned run/step/lease proof. It is ignored by HTTP JSON and excluded from serialized request fingerprints, preserving historical AI replay payloads.

AI reservation and every provider attempt validate tenant, universe, actor, run state, original deadline, current step, step operation and current unexpired lease inside their transactions. They acquire the same workflow application lock as cancellation, before the AI lock. No provider network call is performed while holding the workflow lock.

A separate database context observes committed cancellation or lease loss across worker processes and signals the claimed tool/provider cancellation token. This polling signal complements the transaction fence; correctness does not depend on a tool honoring that signal.

Already-admitted calls remain accountable. Returned authoritative usage is charged; unknown usage retains its reservation for reconciliation. Cancelled checkpoints cannot accept a late result. Cancellation cannot undo a provider call already admitted.

Changed implementation: AiContracts.cs, WorkflowRuntime.cs, WorkflowExecution.cs, AiExecution.cs and new WorkflowClaimCancellation.cs. Focused regressions: SqlWorkflowCancellationTests.cs, updated in-flight cancellation test in SqlWorkflowTests.cs, and JSON security/replay test in WorkflowRuleTests.cs. No database migration, package change or credential change is required.

## Checks actually run

- Locked restore/build, all 71 offline tests and formatting: passed; 0 build warnings/errors.
- Real SQL workflow and AI tests: 25/25 passed, 0 skipped (M08 15; M07 10).
- New SQL cases: cancellation before admission even when a tool deliberately drops the cancellation signal; stale lease rejection; cancellation after reservation before provider attempt, with unused reservation released.
- Updated in-flight regression waits for the provider to start, confirms it receives cancellation, and proves reported usage and original binding are retained while the workflow discards the result.
- Offline JSON regression confirms lease proof cannot be supplied through HTTP JSON and does not alter replay serialization.
- The original pre-dispatch SQL-interceptor counterexample was rebuilt and rerun: no AI run before cancellation; workflow Cancelled; no provider submitted afterward; no AI run afterward; fixVerified=true; liveCalls=0.
- Development doctor passed: SDK10.0.401; ports5100/5200 available. No application hosts were launched for this fix.

Exact verification commands, from C:\dev\GeetaStudio:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked

$env:GEETA_SQL_TESTS = '1'
dotnet test .\tests\ContentOS.Foundation.Tests\ContentOS.Foundation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~SqlWorkflowTests|FullyQualifiedName~SqlAiTests' --logger 'trx;LogFileName=M08-cancellation-fix.trx'
Remove-Item Env:\GEETA_SQL_TESTS

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev-doctor.ps1
~~~

SQL TRX: tests/ContentOS.Foundation.Tests/TestResults/M08-cancellation-fix.trx. The original reproduction harness is in the chat's work/m05-m08-validation directory and uses a new isolated tenant in GeetaStudio_M1Tests and local fixtures only.

## Run and debug locally

No setup change is needed. Restart an already-running API to load the fix. After a successful build, use three PowerShell terminals at C:\dev\GeetaStudio, one command per terminal:

~~~powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

Open http://localhost:5100/staff/workflows or the workflow link at http://localhost:5200 using the provisioned Windows staff account. The API hosts M08 workflow execution; the standalone Worker retains its M01 role.

For VS Code, stop CLI hosts, open the trusted C:\dev\GeetaStudio folder, select **Geeta Studio: all**, press F5, and run **Tasks: Run Task → Geeta Studio: M8 workflow demo**. Useful breakpoints: WorkflowRuntime.ControlAsync and ProcessNextAsync, AiService.ValidateWorkflowClaim and Start, and WorkflowClaimCancellation's Watch method. Shift+F5 stops the compound.

To run the existing fixture demonstration against a running API:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\workflow-demo.ps1
~~~

## Remaining limits

The prior combined SQL evidence/fixture regression remains unresolved and was not rerun as part of this focused fix. M08 therefore remains In Progress; this closure resolves only the P1 cancellation finding. The last M05–M08 review passed35/38 SQL cases, with three M06 cases failing during shared evidence/corpus fixture setup.

Fresh interactive F5/browser testing, live-provider cancellation/account settlement, hosted CI, Azure SQL and production qualification were not verified. No live calls, database resets, staging, commits, remote changes or deployment. M09 remains Not Started. Next work: investigate the shared SQL regression before qualifying M08.