# M09 verification — 11 October 2026

Implementation complete; milestone **In Progress** because the full shared SQL regression is not green. No live sandbox/provider calls or production/public-release qualification. M10 not started.

## Executed results

| Check | Actual result |
|---|---|
| Locked restore/current .NET10 build | Passed, zero warnings/errors. No new package dependency. |
| Offline regression | 74 passed, 0 failed/skipped. |
| First full SQL run | 92/98 passed, six prior source/knowledge fixture query failures. All seven M09 cases passed. |
| Constrained-build full SQL run | 97/98 passed; SourceRegistry.ReviewQueueAsync timed out. All seven current M09 cases passed. |
| Final LowMemory full SQL run, including one-node test orchestration | 92/98 passed, six failures, zero skipped; details below. Verification exited 1 before its format step. |
| Fresh final focused M09 run | 10/10 passed, zero skipped: three offline policy tests plus seven real-SQL lesson cases, 32 seconds. |
| Actual Windows fixture process/HTTP | Passed: three variants, different age examples, identical source/interpretation/Sanskrit, new eligible Teen cast, fixture export denied, Windows staff page200. Owned API stopped. |
| Actual provisioned development API | Passed: Windows staff page200, 12 Draft proposals, identical replay IDs, existing private restricted API configuration. No fabricated approvals or changed source permissions. Owned API stopped. |
| PowerShell/VS Code syntax | verify/db-update/lesson-demo scripts and tasks/launch JSON parsed successfully. |
| JavaScript | node --check passed; temporary mock-DOM initialization/load/new-rule save/default proposal handler passed. Not browser visual QA. |
| Windows doctor | SDK10.0.401/Git/VS Code present; ports5100/5200 free, exit0. Not F5 evidence. |
| EF Core/Runtime models | No pending changes in both existing contexts. |
| Formatting | Passed independently after the failed SQL run; verify-no-changes exit0. |
| Development preservation | 10 runtime jobs, 3 universes, 8 characters, 50 corpus verses; core migration history9/runtime3; 12 new unreviewed lesson profiles. Existing characters/corpus/sample permissions unchanged. |

Exact principal commands actually executed:

~~~powershell
dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Locked -Sql -LowMemory
# GEETA_SQL_TESTS=1 was scoped to the focused invocation and restored:
dotnet test tests/ContentOS.Foundation.Tests/ContentOS.Foundation.Tests.csproj --no-build --no-restore -m:1 --filter 'FullyQualifiedName~SqlLessonTests|FullyQualifiedName~LessonRuleTests' --logger 'trx;LogFileName=M09-focused-final.trx'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/db-update.ps1 -ProvisionRuntime -NoBuild
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/lesson-demo.ps1 -ManageApi
powershell -NoProfile -ExecutionPolicy Bypass -File .local/m09-development-smoke.ps1
dotnet tool run dotnet-ef migrations has-pending-model-changes --context CoreDbContext --project src/ContentOS.Infrastructure --startup-project src/ContentOS.Infrastructure --no-build
dotnet tool run dotnet-ef migrations has-pending-model-changes --context RuntimeDbContext --project src/ContentOS.Infrastructure --startup-project src/ContentOS.Infrastructure --no-build
dotnet format GeetaStudio.sln --verify-no-changes --no-restore
node --check .local/m09-ui.js
node .local/m09-ui-smoke.cjs
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev-doctor.ps1
~~~

Raw logs and TRX stay ignored under .local/TestResults; these reports omit actor identities, credentials and raw corpus text.

## Final combined SQL failures

1. SqlSourceTests.Import_validation_retention_review_queue_and_immutable_history_are_enforced — SourceRegistry.ReviewQueueAsync line233 query timeout.
2. SqlLessonTests.Draft_defaults_are_repeatable_and_never_claim_human_review_or_locale_support — SourceRegistry.ReviewAsync line171 timeout during LessonTestFixture.Setup, before lesson assertions.
3. SqlLessonTests.Unsafe_examples_and_stale_review_versions_fail_before_accepting_a_variant — same source review setup timeout.
4. SqlKnowledgeTests.Adapter_cannot_return_invented_or_unfiltered_chunk_ids — existing EvidenceTestFixture.TranslationAsync / corpus source binding setup timeout.
5. SqlKnowledgeTests.Known_no_answer_swapped_adapter_and_same_dimension_cutover_rollback_preserve_contract — existing corpus import SaveChanges timeout.
6. SqlKnowledgeTests.Pending_edges_are_not_projected_and_reviewed_aliases_and_claim_corrections_are_pinned — existing KnowledgeService.ProcessNextAsync line186 rollback reports an already completed SqlTransaction. Underlying SQL/cleanup condition not resolved.

The two M09 cases passed in the preceding combined run and all ten focused M09 checks passed again afterward. That recheck does not turn the failed combined gate green.

SQL was observed at SLEEP_BPOOL_STEAL with no blocking session while Windows had436MB free and SQL process_physical_memory_low=1. Later the low-memory flag cleared at996MB available, then returned during another run. One development diagnostic login also timed out; the final preservation query and development API demonstration subsequently passed. This is evidence of intermittent SQL/host resource instability, not proof that memory pressure is the sole cause of every failure.

Idle build servers were shut down; no unrelated app/service/database was stopped, no SQL memory setting/password/timeout/fixture assertion changed, and no database reset/drop/cleanup deletion occurred. LowMemory limits build/test orchestration resources and restores its environment overrides. It cannot guarantee enough memory on the currently shared8GB workstation.

## Release and resume gates

Fresh interactive M09 F5/breakpoints and browser visual/mobile/accessibility checks not run. Native-speaker Marathi/Hindi meaning, names, glossary/transliteration/pronunciation, editorial audience suitability and genuine adaptation/translation rights remain unqualified. Fixture memory is explicitly test-only; no real translation vendor or complete multilingual quality claim.

Before qualifying the combined offline regression, rerun the unchanged full gate when SQL Server/workstation resources are stable; investigate SQL memory/waits and the existing M06 transaction cleanup if failures persist. Do not weaken timeouts/assertions or rewrite production source permissions. Consult docs/M9_LOCAL_RUN.md for exact run/demo/debug steps. M10 requires the owner's next explicit instruction.