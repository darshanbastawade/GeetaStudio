# M05–M08 validation — 11 October 2026

**Follow-up:** the cancellation finding is now [fixed and verified](M08-cancellation-fix.md). The shared SQL regression remains open. The review results below describe the original checkpoint.

**Original sign-off: not ready.** Two actionable issues were found: a confirmed M08 cancellation/admission race and a reproducible shared SQL persistence/fixture regression affecting M06 validation. No application behavior was changed during this review; no M09 work, database reset, migration rewrite, credential change, staging, commit or deployment. No live provider call.

## Findings

### [P1] Cancelled workflows can admit a new AI call

Location: src/ContentOS.Infrastructure/WorkflowExecution.cs:73–82, especially the tools.GenerateAsync call at line 81; cancellation is committed in WorkflowRuntime.cs:164. Completion fencing is later in WorkflowExecution.cs:134–139.

A claimed draft does not revalidate its current workflow/lease authorization at AI admission. ControlAsync can commit Cancelled and clear the lease while the worker is preparing the step, yet ProcessNextAsync proceeds to GenerateAsync. Complete correctly rejects the late checkpoint, but that only prevents accepting the output; it does not prevent a new provider call/cost.

Deterministic reproduction used an EF command interceptor to pause the isolated worker at its existing-AI lookup, before tool dispatch. There was no AI run at cancellation. Cancellation committed successfully. Releasing the worker then created a successful AI operation with artificial fixture cost 0.00006000 USD; workflow state remained Cancelled.

This is distinct from cancellation after a provider has already been admitted/submitted. The existing in-flight cancellation test covers that later case and passes; it misses this earlier window.

Required fix: couple AI admission to the still-current workflow state, checkpoint operation and lease, with a transactionally enforced admission decision; propagate cancellation to claimed work. Preserve accounting/reconciliation for provider calls admitted before cancellation. Add the pre-dispatch race as a regression; a simple unlocked check still leaves a race.

### [P1] Current M06 regression validation is not green

Executed all M05–M08 SQL classes in one run. Three M06 cases failed during shared M05 evidence/corpus fixture setup:

- SqlKnowledgeTests.Adapter_cannot_return_invented_or_unfiltered_chunk_ids: SQL command timeout at EvidenceEngine.cs:107.
- SqlKnowledgeTests.Interrupted_index_build_is_atomic_and_recovers_with_pinned_space: connection closed while disposing the evidence submission transaction at EvidenceEngine.cs:51.
- SqlKnowledgeTests.Revoked_source_filters_evidence_and_citations_and_invalidates_cutover: SQL command timeout at EvidenceEngine.cs:107.

These failures occur before the respective M06 assertion paths; they do not prove those behaviors incorrect, and they also cannot qualify them as passing in this run. Previously reported intermittent timeouts remain reproducible. Root cause is not established; do not label them harmless or attribute them conclusively to a driver/server problem.

Required next investigation: capture the underlying command/connection failure and server/client timing around SourceSnapshot loading and transaction disposal, stabilize the fixture/persistence path, then rerun these three cases and the unchanged combined suite. Do not relax timeouts/assertions, omit tests or reset the database.

## Checks actually executed

| Check | Result |
|---|---|
| scripts/verify.ps1 -Locked | Passed: locked restore/build, 70 offline tests, formatting; no build warnings/errors |
| M05 / SqlEvidenceTests | 9 passed, 0 failed |
| M06 / SqlKnowledgeTests | 4 passed, 3 failed in shared setup |
| M07 / SqlAiTests | 10 passed, 0 failed |
| M08 / SqlWorkflowTests | 12 passed, 0 failed; pre-dispatch cancellation blind spot independently reproduced |
| Combined selected SQL classes | 35 passed / 38 total, 3 failed, no skips |
| CoreDbContext model/migration check | No pending model changes |
| RuntimeDbContext model/migration check | No pending model changes |
| Cancellation counterexample | Confirmed before tool dispatch and AI admission; fixture-only, live calls 0 |

SQL command:

```powershell
Set-Location C:\dev\GeetaStudio
$env:GEETA_SQL_TESTS = '1'
dotnet test .\GeetaStudio.sln --no-build --no-restore --filter 'FullyQualifiedName~SqlEvidenceTests|FullyQualifiedName~SqlKnowledgeTests|FullyQualifiedName~SqlAiTests|FullyQualifiedName~SqlWorkflowTests' --logger 'trx;LogFileName=M05-M08-validation.trx' --logger 'console;verbosity=normal'
```

Results: tests/ContentOS.Foundation.Tests/TestResults/M05-M08-validation.trx. Isolated reproduction harness lives in the chat's work/m05-m08-validation folder; it references the unchanged repository and uses only GeetaStudio_M1Tests with a new synthetic tenant/universe and controlled fixture provider. Two reproductions confirmed the race, with the stronger one pausing before tool dispatch. Application source was not patched.

## Requirement review and limits

M05: tested attributed disagreements, exact bound quotations/translations, unsupported historical claims, correction/supersession propagation, labels, authorization and operation replay. All nine SQL cases passed this run.

M06: inspected stable chunk/citation metadata, owned-universe/source filtering, current graph/claim rights, space compatibility, golden validation and alias cutover/rollback. Four SQL cases passed; three setup failures leave validation incomplete.

M07: tested full fallback reservations, currency/period/usage/cache accounting, unknown reconciliation, structured/streaming outcomes, kill/policy/source/tenant guards, route pinning, legacy replay compatibility and scoped embedding accounting. All ten SQL cases passed. Fixture success still does not qualify live vendor/model/account/pricing/policy behavior.

M08: tested bounded schemas/plans/tools, immutable prompt/route checkpoints, rights/review gates, lease recovery, existing in-flight cancellation accounting, auth/isolation, restart-style accepted-effect reuse and deadline controls. Twelve SQL cases passed; the new cancellation counterexample prevents sign-off.

No fresh interactive F5, browser visual/accessibility session, live provider/model, remote embedding, production/Azure SQL/load/restore/security qualification or hosted CI/cold-cache test was performed. Prior Windows restart evidence was inspected, not rerun here. M09 remains Not Started.
