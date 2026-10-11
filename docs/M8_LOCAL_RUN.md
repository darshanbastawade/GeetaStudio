# M08 local run and debugging

Run from PowerShell in C:\dev\GeetaStudio. Existing .NET 10 SDK, SQL Server and privately provisioned Windows staff setup are reused. No live provider key is needed.

```powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql
```

Use three terminals, one host command in each:

```powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
```

Open http://localhost:5200 and follow Draft workflows and human review, or open http://localhost:5100/staff/workflows using your provisioned Windows account. The API hosts the M08 WorkflowProcessor and M06 index processor under its restricted API SQL identity. The separate GeetaStudio.Worker continues M01 runtime probes with runtime-only permissions.

## Demonstrate with an already running API

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\workflow-demo.ps1
```

The helper registers an original-fiction prompt/definition and fixture A route, runs retrieve → plan → draft → check → mandatory review, registers prompt version 2 and fixture B, inspects the original prompt/route checkpoint, records a clearly labeled fixture review, and confirms the accepted AI operation still has exactly one attempt. A new run selects version 2 explicitly and is cancelled as cleanup. It restores the previous active draft route when one existed. Fixture generation rates are zero and explicitly artificial; existing tenant budgets and reservations are preserved. No content is published.

To verify an actual worker-process restart, stop the API first and leave port 5100 free:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\workflow-demo.ps1 -ManageApi
```

This mode launches a hidden API process, waits for review, stops only its owned process and matching executable child, starts another API process, then approves the same persisted review checkpoint. It stops its owned host on completion. It never terminates an independently started host. The saved evidence records whether the actual restart ran; the ordinary mode must not be taken as restart evidence.

## VS Code debugging

Open C:\dev\GeetaStudio as a trusted folder. Stop CLI hosts to free ports. Select **Geeta Studio: all**, press F5, then **Tasks: Run Task → Geeta Studio: M8 workflow demo**. Do not use -ManageApi while the debugger owns port 5100.

Useful breakpoints:

- WorkflowRuntime.StartAsync: pinned prompt/definition/route/profile/input snapshots.
- WorkflowRuntime.Claim: SQL checkpoint claim, retry bound, lease token and original deadline.
- WorkflowRuntime.ProcessNextAsync: closed step dispatch and accepted AI operation reuse.
- WorkflowRuntime.Complete: lease fencing and atomic checkpoint advancement.
- WorkflowRuntime.ControlAsync: pause/resume/cancel and mandatory human review.
- WorkflowTools.RetrieveAsync: current source rights and untrusted evidence validation.
- AiService.Prepare: full-chain reservation with the workflow's explicitly pinned route.

To inspect review persistence manually, run the ordinary demo's operations through the staff page, wait for AwaitingReview, stop/restart the API debug session, inspect the same run ID, read the draft/check result and record review. Shift+F5 stops the compound.

The actual automated M08 restart demonstration is verified. A fresh interactive M08 F5/breakpoint and browser visual/accessibility session are not verified.

## Versioned artifacts and closed tools

Base: /api/v1/workflows/tenants/{tenantId}. Windows staff authentication is required. Data operations verify actual owning-tenant membership; mutations require Administrator and X-ContentOS-Staff: 1.

- GET/POST /universes/{universeId}/prompts: key/version, template, bounded declared inputs, locale, audience ID, generated-text.v1 output schema and required-evidence flag. Versions are immutable; identical registration replays, differing content conflicts.
- GET/POST /universes/{universeId}/definitions: explicit prompt and qualified AI route IDs, five typed steps, attempt limits, currency/cost/token/deadline/lease bounds and mandatory human review.
- POST /universes/{universeId}/runs: operationId, definitionId and declared input dictionary; optional sourceSnapshotIds, knowledge query, planJson and evidenceContentRevisionId.
- GET /runs and /runs/{id}: snapshots, canonical inputs, step result/binding/state/attempts, decisions and rowversion. Current source/evidence rights can withhold results.
- POST /runs/{id}/pause: only at a completed-step boundary. POST /resume resumes a nonexpired boundary pause; a blocked draft may resume only if its original AI operation has completed successfully with eligible output.
- POST /runs/{id}/cancel: transactionally fences new AI reservation/attempt admission and unfinished checkpoint completion, and signals cancellation to claimed work. It cannot undo a provider call already admitted; its original AI ledger binding remains inspectable and accountable.
- POST /runs/{id}/review: approve or reject only AwaitingReview, with a new operation ID, exact rowversion and explicit manual:/fixture: evidence reference. Identical control replay is idempotent. Approval completes the draft workflow; it does not publish, approve canonical scripture or bypass later editorial gates.
- GET /runs/{id}/checkpoint: exports paused/terminal compatible owned checkpoints; active execution must be paused or drained first.
- GET /tools: deployed schema metadata. Only evidence.retrieve.v1 and ai.generate.v1 are callable through typed application methods. There is no arbitrary endpoint, shell, MCP server or model-selected operation.

The supported definition steps are:

```json
[
  {"kind":"retrieve","tool":"evidence.retrieve.v1","maxAttempts":1},
  {"kind":"plan","tool":null,"maxAttempts":1},
  {"kind":"draft","tool":"ai.generate.v1","maxAttempts":1},
  {"kind":"check","tool":null,"maxAttempts":1},
  {"kind":"review","tool":null,"maxAttempts":1}
]
```

Choose the new definition ID for new runs after updating a prompt/route; prior definitions remain available deliberately. The route's promptVersion must be the registered key plus .v and version. No schema or policy changes are inferred from generated text.

## Bounds, evidence and recovery

Planning is deterministic. Optional supplied plan JSON must contain exactly the owned five-step sequence; loops, extra tool selections and malformed JSON fail visibly. Retrieval without evidence can proceed only for a prompt that explicitly does not require evidence; a required-evidence miss fails NoSupportedMatch.

Source IDs must be in the owned universe and permit generation/adaptation/AI processing. Current M04 Besant sample is READ-only and does not become an AI input by this milestone. Supplied retained snapshots and retrieved text are bounded data. Recognized instruction-bearing source text is rejected; all other text is JSON-delimited data and never selects routes/tools or waives review. This guard is deliberately conservative, not a universal prompt-injection classifier.

The optional evidenceContentRevisionId carries an existing M05 claim/evidence gate into generation, checking and review. M05's transaction-aware business gate is reused inside the workflow transaction. Claims and current source rights are checked again; an old approval cannot override revocation or stale evidence.

Only one bounded M07 AI operation is used per draft checkpoint. Its fallback chain is reserved by M07; the definition's maximum cost must cover the route's full limit, and its token ceiling covers every configured profile context/output bound. Retrieval currently permits only historical local concept-projection-v1/v2 spaces so remote embedding charges cannot sit outside the workflow limit. Remote retrieval/embedding workflows need a separately reviewed cost extension.

Checkpoint leases and fencing prevent a late worker from accepting results after cancellation or another claim. Safe read-step recovery is bounded by the configured attempt count; expired attempts can fail visibly rather than loop indefinitely. Submitted draft recovery always inspects the original AI operation. Completed accepted results are reused; in-progress/unknown usage blocks for M07 recovery/reconciliation, never a fresh submission. Reconciliation without an eligible completed output cannot resume generation. Cancellation and workflow expiry do not zero provider costs or erase unknown holds.

Prompts/inputs and bounded evidence/results are canonical, staff-protected SQL workflow state, unlike M07's operational prompt hash. They are needed for controlled recovery and inspection, and are never operational log content. Do not place credentials in prompts. Existing secret references remain private. No live provider was used in M08 verification.

## Alternative engine handoff

IWorkflowRuntime and IWorkflowWorker own business artifacts and checkpoints independently of the execution engine. workflow-checkpoint.v1 records operation IDs, immutable definitions/prompts, original route/profile versions, actor/tenant/universe, deadlines, ordered steps, accepted results and AI bindings.

Disable Workflows:WorkerEnabled to stop new processing, pause or drain at a boundary, and reconcile original AI operations before exporting. A future importer must validate ownership, schema/version, checksums against stored immutable artifacts, remaining deadlines/bounds, current rights and every external binding. Preserve accepted checkpoint and operation IDs and do not replay completed effects. Drain unsupported or in-flight engine states with the current engine. No generic import endpoint, automatic engine conversion or third-party agent framework is introduced by M08.

## Remaining verification

Live model/account/pricing/data policy and real fallback remain unqualified from M07. Hosted CI, cold-cache checkout, Azure SQL, production load/backup/restore/security qualification and human linguistic/theological quality review were not run. M09 is not started.

## Current regression-check limitation

The final build, formatting, all 70 offline tests and all M08 workflow tests passed. The established combined SQL command above did not pass reliably: the last run passed 83 of 88 SQL tests, with five existing core/corpus/evidence query timeouts. No assertion or timeout was relaxed. This keeps M08's full regression verification gate open.

For the final focused acceptance checks:

```powershell
$env:GEETA_SQL_TESTS = '1'
dotnet test .\GeetaStudio.sln --no-build --no-restore --filter 'FullyQualifiedName~Workflow|FullyQualifiedName~Route_switch_replay_snapshots_usage_cache_and_rollback_are_durable'
Remove-Item Env:\GEETA_SQL_TESTS
```

All 20 matching cases passed on final binaries. The optional diagnostic helper below attempts every discovered SQL class in a fresh test process and rejects failed, skipped or missing tests; it also encountered an existing core/evidence timeout here, so it is not a verified workaround:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-sql-isolated.ps1
```

Its .local/verification/sql-isolated TRX files and the combined failure names in M08 evidence are the next investigation inputs. Keep the default verification gate unchanged and resolve this before advancing to M09.

Cancellation follow-up: the original pre-dispatch race is fixed; 71 offline and 25 focused M08/M07 SQL checks passed. See [closure evidence](milestones/evidence/M08-cancellation-fix.md). Break in AiService.ValidateWorkflowClaim for admission fencing and WorkflowClaimCancellation.Watch for cross-process cancellation signaling. No run/debug configuration or database migration change is needed. The shared broad SQL regression remains open.
