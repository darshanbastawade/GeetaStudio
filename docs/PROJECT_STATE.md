# Project state — resume here

Accepted roadmap 2.1; updated 11 October 2026. M0 foundation is preserved. M1 runtime, M2 reusable core, M3 source registry, M4 initial canonical corpus, M5 claim/evidence engine, M6 knowledge retrieval and M7 AI routing/cost accounting are Offline Verified locally. M4 contains 25 reviewed Sanskrit/English pairs, with 676 declared edition entries still missing.

- Current milestone: M09 — implementation complete; In Progress because combined SQL regression is not green. Final focused M09 10/10 and actual Windows fixture/development acceptance passed. M08 cancellation/admission fix preserved; M10 not started.
- Branch / commit: milestone/m00, unborn HEAD. Baseline/new files remain untracked. No staging, commits, branch changes, push or deployment.
- Latest M09 checks: locked build 0 warnings/errors; 74 offline passed; fresh focused 10/10 (7 SQL + 3 policy). Final combined SQL 92/98, six shared source/setup/transaction failures, 0 skipped; previous combined 97/98. Independent formatting and both model checks passed. Windows staff HTTP200 and proposal replay passed; owned APIs stopped and ports free.
- Real process acceptance: `scripts/test-runtime-recovery.ps1` — two workers contended; owned worker crashed after SQL effect commit; same operation recovered by reconciliation, 2 attempts / exactly 1 effect. [Sanitized evidence](milestones/evidence/M01-recovery.json).
- Local runtime: GeetaStudio_Dev and GeetaStudio_M1Tests migrated using additive M1Runtime/M1ProviderDefinitions; separate restricted API/worker SQL logins configured privately. No database reset/drop or existing login password reset.
- Provider switch: test.a → test.b affects new jobs without rebuilding; existing snapshots/resource bindings retained. Kill/disable blocks further calls; Unknown must be reconciled.
- Security: Windows Negotiate plus explicit staff roles/policies, audit, action-header protection; limited endpoint/secret namespaces; no production authentication shortcut or live provider selected.
- M0 review gap resolved: doctor now checks IPv4 and supported IPv6; both port conflicts return 1 and free ports 0. Regression helper passed.
- Run/debug: [M9 local runbook](M9_LOCAL_RUN.md), [M8 local runbook](M8_LOCAL_RUN.md), [M7 local runbook](M7_LOCAL_RUN.md), [M6 local runbook](M6_LOCAL_RUN.md), [M5 local runbook](M5_LOCAL_RUN.md), [M4 local runbook](M4_LOCAL_RUN.md), [M3 local runbook](M3_LOCAL_RUN.md), [M2 local runbook](M2_LOCAL_RUN.md), [M1 runtime guide](M1_LOCAL_RUN.md). API5100/Web5200/Worker Development profiles retained. Existing compound configuration passes checks; M0 actual F5 evidence retained. Separate interactive M1/M2/M3/M4 F5 sessions not repeated.
- Remaining verification: hosted GitHub CI, cold-cache checkout, M1/M2/M3/M4/M5/M6/M7/M8/M9 interactive F5/browser click-through, Azure SQL, production/live-provider qualification. No claim of real-vendor exactly-once delivery or verified live pricing; M7 fixture rates are artificial.
- M01 follow-up review: both P2 findings fixed; 42 tests pass, including four real-SQL regressions for cross-provider scheduling, circuit wait/recovery, admission races and original deadlines. [Review and closure](milestones/evidence/M01-review.md).
- Next action: stabilize/diagnose shared SQL source/corpus/transaction regression, then rerun the unchanged full gate before marking M09 Offline Verified. Human native-speaker/meaning/age reviews and genuine adaptation permissions remain release gates. M10 only on a new explicit owner instruction.
- Evidence: [M09](milestones/M09.md), [M08](milestones/M08.md), [M07](milestones/M07.md), [M06](milestones/M06.md), [M05](milestones/M05.md), [M04](milestones/M04.md), [M03](milestones/M03.md), [M02](milestones/M02.md), [M01](milestones/M01.md), [M0](milestones/M00.md), [dependency register](EXTERNAL_DEPENDENCIES.md).
- M02 core: separate core schema/history, typed entities/contracts, tenant membership plus universe grants, immutable revisions, operation replay, rowversion conflicts and validated scoped imports/exports.
- M02 fixtures: Geeta/synthetic share the same operations; private tenant is isolated; 7 accepted Geeta characters plus synthetic guide seeded as Draft. Legacy alias normalization preserves ID/history/biography.
- M02 migration: 20261010125027_M2Core applied and rerun in both dedicated databases; 10 development runtime jobs preserved. API gets object-level core grants; worker gets no core permissions; credentials unchanged.
- M02 actual Windows demo: both universes retain two revisions and replay IDs; stale updates 409, isolated tenant 403; SQL readiness Ready and web shell 200/API reachable. [Sanitized demo](milestones/evidence/M02-demo.json). All owned hosts stopped.
- Final model checks: no pending model changes in CoreDbContext or RuntimeDbContext. At the M02 checkpoint no new provider, live/publishing call, M04 work, commit or remote change was present. M04 is now recorded below.

- M03: owning-tenant source registry, versioned editions/parties/permissions, checksum snapshots or evidence-only retention, admin review queue, intended-use decisions and typed content/asset provenance.
- M03 migration: 20261010134730_M3Sources applied to both existing dedicated databases; same core history; append-only API source histories; worker no source rights; credentials/data preserved.
- M03 output boundaries: export/approval/public knowledge recheck rights; attribution/citations carried through export; revision/asset ancestry preserved; no implicit modern-translation permission.
- M03 actual Windows demo: repeat import retains snapshot ID; new edition has UnknownRights; revocation blocks default core export (422) and new use (Revoked), exposes fixture-published remediation. No actual publication/provider call. [Demo](milestones/evidence/M03-demo.json).
- M03 source staff page and Blazor link served 200; actual UI clicks/F5 not repeated. Owned hosts stopped. Bounded local traversal/list/impact and human permission review documented.

## Milestone tracking

| Milestone | Implementation | Offline | Live sandbox | Production | Evidence / blocker |
|---|---|---|---|---|---|
| M0 | Offline Verified | Verified, including Windows debugger | N/A — no runtime vendor | N/A — local foundation | [M00 evidence](milestones/M00.md) and [debug evidence](milestones/evidence/M00-debug.md) |
| M1 | Offline Verified | Verified on real local SQL | N/A — deterministic adapters only | Not qualified | [M01 evidence](milestones/M01.md) |
| M2 | Offline Verified | Verified on real SQL and Windows API | N/A — no new external adapter | Not qualified | [M02 evidence](milestones/M02.md) |
| M3 | Offline Verified | Real SQL and Windows HTTP | N/A — approved-file fixtures | Not qualified | [M03 evidence](milestones/M03.md) |
| M4 | Offline Verified | Real SQL and Windows HTTP; 25/701 edition sample | N/A — local licensed snapshots | Not qualified | [M04 evidence](milestones/M04.md); 676 missing entries, review scope explicit |
| M5 | Offline Verified | 112 tests; real SQL and Windows HTTP | N/A — no new external adapter | Not qualified | [M05 evidence](milestones/M05.md); fresh interactive F5/human source review not run |
| M6 | Offline Verified | 121 tests; full-sample SQL and Windows HTTP | N/A — local adapters | Not qualified | [M06 evidence](milestones/M06.md); limited reviewed-concept semantics, fresh interactive F5 not run |
| M7 | Offline Verified | 140 tests; real SQL and Windows routing/budget/25-chunk embedding demo | Not verified — model/account/policy/pricing and real fallback qualification open | Not qualified | [M07 evidence](milestones/M07.md); fresh interactive F5 not run |
| M8 | In Progress — implementation/acceptance done | Focused workflow/restart passed; combined SQL regression Failed (5 prior query timeouts) | N/A — no new external adapter; M07 qualification open | Not qualified | [M08 evidence](milestones/M08.md); full regression gate open |
| M9 | In Progress — implementation/local acceptance done | 74 offline and focused 10/10; combined SQL 92/98 fails | N/A — local reviewed-memory adapters; human locale quality open | Not qualified | [M09 evidence](milestones/M09.md); [checks](milestones/evidence/M09-checks.md) |
| M10 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M11 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M12 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M13 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M14 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M15 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M16 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M17 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M18 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M19 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M20 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M21 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M22 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M23 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M24 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M25 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M26 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |
| M27 | Not Started | Not Run | Not Assessed | Not Assessed | [M0 audit](M0_AUDIT.md): missing implementation |

## Resume notes

Update after each substantive slice: current branch/commit, files in progress, last passing command, exact next task, blockers and affected dependent milestones, and manual checks the owner still needs to perform.

Do not mark a required live integration as verified because its fake adapter passed. For non-applicable tiers use N/A with a reason. An unresolved live gate can coexist with completed offline work; it must remain visible until the relevant feature is qualified.


## M04 completion — 10 October 2026

- Additive 20261010145059_M4Corpus applied to GeetaStudio_Dev and GeetaStudio_M1Tests; existing core/runtime histories preserved. API append-only corpus source/inventory/commands, narrowly mutable review records, worker no corpus grants.
- Manifest: baseline 700, selected Besant edition 701 including one explicit chapter 13 variant. 25 Sanskrit/English pairs imported, approved-readable 25 for IN staff Text; 676 missing. Transliteration/word meanings/approved concepts/explanations absent, every missing entry reported.
- Sample provenance: pinned Wikisource Besant 1922 Discourse 1, CC BY-SA 4.0 transcription attribution/evidence retained. Automated exact-source/hash/verse-locator review is recorded; no new human scholarly/manuscript/theological review. No canonical LLM gap filling.
- Actual Windows corpus-demo.ps1 passed twice; repeat retains manifest/snapshot IDs, Added 0/Unchanged 25; full report saved at milestones/evidence/M04-demo.json. Reader and Blazor HTTP 200, readiness Ready. Owned hosts stopped.
- Dev preservation: 10 runtime jobs, 3 universes, 8 characters; 25 corpus verses added. Core/Runtime no pending model changes. 94 tests pass with old milestone regressions.
- Source revocation impact now includes corpus text/annotation dependents; reader/report recheck rights. Exact text immutable, derived search separate; annotations separately reviewed.
- [M04 evidence](milestones/M04.md), [local run/debug](M4_LOCAL_RUN.md). Fresh interactive M04 F5/browser visual QA, human source review, hosted CI/cold cache, production/Azure SQL and rollback/load qualification remain unverified. M05 not started.

## M02–M04 review follow-up — 10 October 2026

Three findings reproduced on real SQL at the review checkpoint: P1 shared core reads bypass source revocation/rights, P1 write-only mutation responses disclose previous revisions despite denied Read, P2 corpus import accepts authentic 1.1 source as approved 2.1 because chapter/edition bindings are absent. Existing locked verification remains 94 passed; targeted regression coverage is required. No application code changed; review-only fixtures added to the test database and sharing flags restored. [Review evidence](milestones/evidence/M02-M04-review.md). These findings are now closed by the follow-up below; M05 remains Not Started.

## M02–M04 review closure — 10 October 2026

All three findings fixed: current rights on shared core reads/listing; metadata-only shared mutation/replay results; immutable admin-pinned corpus snapshot/hash/typed locator bindings. Added 20261010152946_M4SourceBindings to both dedicated databases and append-only API table grants. Four new SQL regressions; 48 offline + 50 SQL = 98 passed, zero warnings/errors, formatting clean.

The Windows version-2 sample helper ran twice with stable replay IDs and 25 approved-readable pairs/676 missing. Version-1's 25 rows remain intact but unbound text is withheld. Dev retains 10 jobs, 3 universes and 8 characters. Core model has no pending changes; owned hosts stopped and doctor ports free. No destructive reset, password change, commit, external publication or M05 work.

[Closure evidence](milestones/evidence/M02-M04-fixes.md), [bound demo](milestones/evidence/M04-bound-demo.json), [updated runbook](M4_LOCAL_RUN.md). Next action: review closure; await explicit instruction before M05. Fresh interactive F5/browser visual checks, human scholarly review and production qualification remain unverified.

## M05 completion — 10 October 2026

Claim/evidence engine, immutable versioned rubrics/spans, attributed disagreements, staff review, correction/supersession/impact and content-label gates implemented. Additive 20261010155751_M5Evidence applied to both dedicated databases. Locked verification: 53 offline + 59 real SQL = 112 passed, zero warnings/errors/skips, formatting clean. Actual Windows demo retains both viewpoints, rejects invented quotation/history with 422 and identifies two stale revisions after correction; reviewed replacement v2 retains old history. Source-use ledger, export, approval and shared reads recheck claims plus rights. Readable rubric criteria and worker/API/web startup checked. No pending Core/Runtime model changes.

Preserved 10 jobs, 3 universes, 8 characters and 50 corpus rows (unbound v1 retained, bound v2 25 pairs/676 missing). Owned hosts stopped; existing credentials/migrations unchanged. Fresh M05 interactive F5/browser QA, substantive human evidence review, hosted CI/cold cache and production qualification remain unverified. [M05 evidence](milestones/M05.md), [local run/debug](M5_LOCAL_RUN.md). M06 not started; await explicit instruction.

## M06 completion — 10 October 2026

Reviewed concepts/aliases/translations/relationships, scoped source/claim provenance, hybrid lexical/semantic projection, stable exact source chunks, versioned embedding spaces, durable asynchronous index builds and shadow-gated atomic alias activation/rollback implemented. Additive 20261010163646_M6Knowledge applied to both dedicated databases. API-hosted indexing worker reuses existing source/corpus privileges; standalone M01 Worker retains runtime-only grants. No package/credential/model download or paid provider change.

Locked verification: 55 offline + 66 SQL = 121 passed, zero skips/warnings/errors, format clean. SQL fixtures use all 25 approved English components. Owner-confirmed known 1.1/no-answer labels, controlled semantic alias, incompatible same-dimension model/route rejection, independent adapter contract, rollback, interrupted queue recovery, revocation, intended-use filtering, graph corrections and invalid adapter IDs tested. Actual Windows demo and knowledge/web/worker HTTP/startup checks passed; both EF models unchanged. Preserved 10 jobs, 3 universes, 8 characters, 50 retained corpus rows and 676 missing entries. Owned hosts stopped/ports free.

First full-sample shadow run correctly refused activation because ranking displaced target 1.1. Rarity weighting/explicit concept-supported scoring and full-sample regressions corrected it; failed index retained. Two relevance labels were explicitly confirmed by the owner, not invented human review. Semantic path is local reviewed features, not a neural/general multilingual model. [M06 evidence](milestones/M06.md), [run/debug](M6_LOCAL_RUN.md). Fresh interactive M06 F5/browser QA, hosted CI/cold cache, Azure SQL and production/model-quality/restore/security qualification unverified. M07 not started.

## M07 completion — finalized 11 October 2026

- Full locked verification: 64 offline + 76 real-SQL tests, 140 passed, no skipped tests, build warnings or errors; formatting clean. Both EF contexts reported no pending model changes.
- Additive migrations 20261010172857_M7AiProviders and 20261010172904_M7AiLedger applied to both existing dedicated databases. Development retains 10 runtime jobs, 3 universes, 8 characters and 50 corpus verse records; no reset, old migration rewrite or credential reset.
- Actual Windows demo switched A/B for new runs, retained replay snapshots, rejected unsupported tools and admitted exactly one of two concurrent requests against a shared INR reservation limit. Local controlled embedding route produced a Ready 25-chunk index without changing the active knowledge alias.
- Local benchmark reports schema/keyword quality, latency, availability and artificial ledger cost; human quality NotReviewed and no automatic route activation. API readiness/staff page and Web AI link passed HTTP checks; Worker started. No raw fixture prompt appeared in operational logs; live calls: zero.
- Evidence: [M09](milestones/M09.md), [M08](milestones/M08.md), [M07](milestones/M07.md), [demo](milestones/evidence/M07-demo.json), [benchmark](milestones/evidence/M07-benchmark.json), [hosts](milestones/evidence/M07-hosts.json). Fresh interactive M07 F5 and browser visual/accessibility checks remain unverified.
- No M08 implementation, staging, commit, remote change, publication or deployment. Real account/model/data-policy/pricing and real fallback qualification remain open.

## M08 implementation checkpoint — 11 October 2026

- Implementation and local workflow acceptance completed: immutable prompts/definitions, pinned routes, leased checkpoints, bounded owned tools, source/claim gates and mandatory staff review. M09 not started.
- Final focused checks: 20 passed, 0 skipped (6 workflow rules, 12 workflow SQL cases, existing core workflow case and historical M07 replay compatibility case). Locked build/format and all 70 offline tests passed.
- Established combined SQL regression gate remains Failed: initial run 85/87 passed with 2 old corpus timeouts; repeat 83/88 passed with 5 old core/corpus/evidence timeouts. Individual prior corpus rechecks passed; fresh-class mode still timed out in the existing public-knowledge/core evidence case. No timeout/assertion weakened, database reset or driver upgrade.
- Full milestone status stays In Progress until this regression gate is reliably green. No evidence establishes a SQL-server/client root cause; redacted wait sampling found no persistent blocker.
- Additive migration 20261010222632_M8Workflows applied to both existing databases; both EF models have no pending changes. API-only workflow object grants; standalone Worker privileges/private credentials unchanged.
- Development records preserved: 10 runtime jobs, 3 universes, 8 characters, 50 corpus verse records; core history 8, runtime history 3.
- Actual Windows API-hosted workflow-worker restart: AwaitingReview survives process change; original prompt/route stays pinned after version 2 registration; review completes with exactly 1 original AI attempt before/after. Fixture approval is not publication or human theological qualification.
- [M08 evidence](milestones/M08.md), [local run/debug guide](M8_LOCAL_RUN.md), [restart evidence](milestones/evidence/M08-demo.json). Fresh interactive M08 F5/browser visual QA, hosted CI/cold cache, live models and production qualification unverified.

## M05–M08 revalidation — 11 October 2026

[Review and reproduction evidence](milestones/evidence/M05-M08-review.md). Locked build/format and all 70 offline tests passed; selected SQL classes: M05 9/9, M06 4/7, M07 10/10, M08 12/12 (35/38 total, no skips). The three M06 failures occur in shared evidence setup, including a closed-connection cleanup failure; current full regression qualification remains open.

A new P1 M08 counterexample is confirmed: cancellation commits before tool dispatch and before any AI run exists, then the claimed worker still submits a successful fixture AI call and incurs artificial ledger cost. Completion fencing keeps the run Cancelled but does not fence new admission. This earlier window is not covered by the passing in-flight cancellation case. Couple admission to the current workflow/lease and propagate cancellation before qualifying M08.

Both EF model checks passed. No implementation fix, reset, credential change, staging, commit or live call during validation. Diagnostic reproduction used a new isolated test tenant/universe and local fixtures; no production data or source permissions changed. Local ports 5100/5200 free. Next: fix the cancellation race and shared persistence/fixture regression, then rerun unchanged checks; M09 not started.

## M08 cancellation fix — 11 October 2026

The confirmed P1 is closed. AI reservation and each provider attempt transactionally validate the workflow state and exact current lease under the cancellation lock. Claimed work observes cancellation/lease loss through a separate database context. Existing AI usage/reconciliation and completion fencing are preserved; host-owned lease proof is not bound from HTTP JSON or included in replay serialization.

Locked build/format and 71 offline tests passed; focused real-SQL M08/M07 25/25 passed, 0 skipped. The original pre-dispatch counterexample now reports no AI run/provider submission after cancellation and fixVerified=true. No migration/package/credential changes. Ports5100/5200 free. [Closure evidence](milestones/evidence/M08-cancellation-fix.md).

The shared broad SQL regression remains open and was not rerun during this focused fix. Fresh interactive F5 and live-provider settlement unverified. M08 remains In Progress; investigate the shared evidence/corpus setup failures next. M09 not started.
## M09 implementation and verification — 11 October 2026

- Typed lesson/translation ports, versioned audience/writing/locale/route rules, six-section evidence-bound treatments, reviewed translation memory, immutable linked variants and pinned cast/source lineage implemented. Children/youth/adult and 15–18/19–23 writing are editable configuration; English/Marathi/Hindi are proposed locales. Source claims/current rights/corrections and retrieved Sanskrit are preserved and rechecked. No M10/media/child personalization work.
- Additive 20261011015856_M9Lessons migrated in both dedicated databases. Existing restricted API lesson grants updated; standalone worker unchanged. Development retains 10 runtime jobs, 3 universes, 8 characters, 50 corpus verses; migration histories 9 core/3 runtime. The actual development API creates 12 Draft proposals with stable replay; no reviews or permission upgrades fabricated.
- Final locked build and 74 offline checks passed, 0 warnings/errors. Fresh final M09 focused 10/10 (7 SQL/3 offline), 0 skipped. Independent format and both EF model checks pass. JavaScript syntax/mock-DOM handlers, PowerShell/VS Code syntax and Windows doctor pass. Actual Windows fixture comparison/cast/export-denial and development staff/proposal demonstrations pass; all owned hosts stopped; ports 5100/5200 free.
- Combined SQL runs: 92/98, then 97/98, then 92/98. Final six failures include a SourceRegistry queue timeout, two M09 source-fixture review timeouts, two prior corpus setup/import timeouts and one existing M06 rollback on an already-completed transaction. Focused rechecks do not qualify this failed combined gate. SQL low-memory/SLEEP_BPOOL_STEAL observations suggest resource pressure contributes; root cause is not fully resolved. No SQL configuration, test assertion/timeout, unrelated app or source permission changed. LowMemory build/test option avoids redundant build-server resources.
- [M09 evidence](milestones/M09.md), [checks](milestones/evidence/M09-checks.md), [fixture demo](milestones/evidence/M09-demo.json), [development API](milestones/evidence/M09-development.json), [Windows run/debug guide](M9_LOCAL_RUN.md). Native-speaker/meaning/age suitability, adaptation rights, fresh interactive F5/visual/accessibility, hosted CI/cold cache and production qualification remain open. Existing M04 sample remains 25/701 per edition, 676 missing. M10 not started.