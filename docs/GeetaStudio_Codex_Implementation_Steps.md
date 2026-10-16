# GeetaStudio — step-by-step Codex implementation prompts

Prepared for Darshan Bastawade · 15 October 2026

These prompts close the audit's M25.1–M25.3 milestones in the existing repository. They are implementation instructions, not evidence that changes or tests have already run. The audit baseline was commit `82cc55d431367b822300c1d64bbb00986d38ccdd`; Codex must inspect your current revision before changing anything.

## How to use this pack

1. Open your existing GeetaStudio repository folder in VS Code.
2. Save this file as `docs/GeetaStudio_Codex_Implementation_Steps.md` in that repository. Optionally save the earlier audit beside it.
3. Use one Codex conversation for each milestone. Start with the common instruction below, then give one numbered prompt at a time. For a new conversation, repeat the common instruction and name the next prompt.
4. Let Codex implement and verify the selected step. Read its result before starting the next one. A plan, fixture demonstration or skipped integration test is not the required acceptance result.
5. At each milestone boundary, review the diff, commit the verified changes, and push through your normal workflow. Check CI on that pushed revision. The prompts do not authorize publishing content, charging customers or deploying publicly.

| Order | Prompt | Milestone | Output |
|---|---|---|---|
| 1 | Establish current baseline and fix CI | M25.1 | Known failure cause and reproducible build/offline checks |
| 2 | Fix current rights checks for stored retrieval | M25.1 | Regression-proven fix for returned evidence |
| 3 | Verify and close M25.1 | M25.1 | Full SQL result and accurate project status |
| 4 | Connect the factory to real AI workflows | M25.2 | Durable, budgeted original-script generation |
| 5 | Add one permitted live trend source | M25.2 | Bounded discovery and idempotent observations |
| 6 | Complete one usable media/language path | M25.2 | Approved artwork and actual narration |
| 7 | Demonstrate and close the integrated pipeline | M25.2 | Two audience variants and playable captioned exports |
| 8 | Implement selected-feature release checks | M25.3 | Evidence-backed release decisions |
| 9 | Execute release acceptance | M25.3 | Passed or explicitly blocked release report |

M22 voice TALK remains an existing unfinished milestone. M26/M27 already have private implementation at the audit baseline; preserve them. Do not add them again or make them prerequisites for the first reviewed-video pilot.

## Common instruction — paste at the start of each milestone conversation

```text
Work in the existing GeetaStudio repository. Read AGENTS.md, the current
PROJECT_STATE, accepted ROADMAP v2.1, milestone reports, relevant source and
tests, and docs/GeetaStudio_Codex_Implementation_Steps.md.

Implement only the numbered prompt I select, plus the smallest demonstrated
prerequisite fixes. Make actual code changes and run applicable verification;
do not stop after a plan. Revalidate audit observations against current code.
If already fixed, retain the fix and prove it instead of implementing it again.

Preserve the .NET modular monolith, project boundaries, public contracts,
stable IDs, database contents, migrations, source permissions, review history,
and existing M26/M27 work. Do not reset databases or broaden source licenses.
Use additive migrations only where necessary. Preserve unrelated local edits.

Inspect branch/status first. If no suitable working branch exists, create an
isolated feature branch from the current working baseline without discarding
changes. Do not automatically pull, reset, stash, commit, push, merge or deploy.

Reuse existing typed ports, M7 routing/accounting, M8 workflows, M10 casting,
M13 approval, M16 media and M17 rendering. Do not create a second workflow
engine, AI ledger, source registry or character system. Do not add microservices.

Use local deterministic fixtures for regression tests. Live qualification
requires a deliberately enabled profile, available credentials and an explicit
owner-approved spend limit. Never print secrets. Do not manufacture live IDs,
rights, human review, provider qualification or production-readiness evidence.

Keep meaningful regressions for rights, isolation, leases, cancellation,
idempotency and costs. Run focused tests after changes and the full established
suite at milestone closure. Do not weaken tests, silently skip SQL cases or
suppress security checks to obtain a green result.

When blocked by external prerequisites, complete all independent implementation
and offline verification, then record precisely what remains unverified.
Do not mark the milestone complete or proceed into a dependent live release.

Maintain docs/milestones/M25.1.md, M25.2.md or M25.3.md as applicable, plus a
small resume checkpoint with completed step, changed files, test results and
next step. Finish with: behavior changed; files/migrations; commands actually
run and results; remaining blockers; exact local demo commands. Stop after the
selected prompt; do not expand into the next step automatically.
```

## Prompt 1 — current baseline and CI diagnosis

```text
Execute Prompt 1 / M25.1: establish the current baseline and fix the actual CI
failure. Follow the common instruction in this prompt pack.

Inspect global.json, Directory.Build files, project and lock files,
.github/workflows, scripts/verify.ps1, scripts/verify-sql-isolated.ps1,
scripts/dev-doctor.ps1 and the latest milestone evidence.

The audit observed failing GitHub run 38604099399 on commit 82cc55d. Its checkout
and setup-dotnet steps passed; the combined restore/build/test/format step
failed. The audit could not retrieve the log. This does not establish the
failure's cause. Fetch the actual current failed-run log through available
GitHub tooling and compare it with the exact revision being tested.

1. Record branch, HEAD, dirty files, installed SDK/runtime/PowerShell and
   available dedicated SQL test environment. Do not expose connection secrets.
2. Reproduce the failing stage from a fresh-checkout-equivalent environment.
   Inspect the log before assuming a package, SDK, formatting or test defect.
3. Make the smallest justified correction. Preserve version locks and required
   security/runtime patches; do not downgrade merely to use a cached runtime.
4. Keep the normal Windows/VS Code run path working. Improve failure logs or
   publish test-result artifacts in CI if necessary to identify the failure.
5. Run locked restore, build, offline tests and formatting with established
   scripts. Do not run production SQL or paid providers.
6. Open M25.1's evidence report and record the cause, fix and exact commands.

Acceptance: local fresh-checkout checks pass, or an exact environment blocker
is documented. Hosted CI is only reported green after observing a successful
run for the pushed revision; a local result must not impersonate hosted CI.
If GitHub logs are inaccessible, continue local diagnosis and leave the remote
cause explicitly unresolved. Do not guess it.
```

**Checkpoint:** review the failure diagnosis and changed files. Continue with Prompt 2 when the build is usable. Hosted CI sign-off may follow your normal commit/push at milestone closure.

## Prompt 2 — stored retrieval rights regression and fix

```text
Execute Prompt 2 / M25.1: reproduce and fix current eligibility of stored trend
retrieval evidence. Follow the common instruction.

Inspect:
- ContentOS.Infrastructure/TrendWorkbench.cs: LinkAsync and TeachingView
- TrendStories.cs: PrepareAsync and StoryView
- TrendFence.cs: AssertTeaching and AssertBrief
- KnowledgeService.cs: Allowed/Search/RetrieveAsync
- SourceRightsGuard.cs, EvidenceEngine.cs and LessonEngine.cs
- ContentOS.Contracts/KnowledgeContracts.cs and TrendContracts.cs
- Existing SqlTrendTests, TrendTestFixture and source/corpus fixtures.
Paths above are under src unless they name tests.

Audit hypothesis: LinkAsync stores a full KnowledgeResult; TeachingView returns
it based on the selected lesson's withholding flag without checking every saved
hit. A stored hit can contain ExactText and citations from another source.
Story snapshots also embed retrieval and need the same complete check.

1. First write a meaningful real-SQL regression through normal application
   operations: valid selected lesson A; retrieval from independently controlled
   source/index B; save a no-match teaching lookup containing B; revoke or
   withdraw B while A stays valid; read the lookup again. Assert B's text is
   absent from every serialized response field. Prove failure before fixing.
2. Also cover matched lookups/additional hits and story snapshots where the
   actual corpus model supports those cases. Do not fabricate an impossible
   fixture or bypass normal ownership/binding rules to force a test.
3. Reuse current source/corpus/evidence eligibility to sanitize or withhold
   saved retrieval on reads and relevant reuse/review boundaries. Include
   current corpus review, source binding and applicable correction state.
4. Preserve immutable snapshot/audit identity. Returning a withheld result
   must not erase the historical record or silently choose a different source.
5. Do not re-run a paid retrieval/model operation just to authorize a saved
   result. Revalidate the exact stored IDs and version bindings instead.
6. Use existing transaction conventions so revocation and output projection
   have a defined consistent boundary. Keep restricted text out of diagnostic
   fields, exception messages and logs as well as the main result.
7. Test eligible results still work, no-match lookups remain honest, foreign
   tenant access fails, and source withdrawal/correction cannot restore text.

Acceptance: the failing regression now passes; affected source, retrieval,
trend, lesson and editorial regressions pass. Document any audit hypothesis
that was disproved. Do not change unrelated generators or provider adapters.
```

## Prompt 3 — close M25.1 on one revision

```text
Execute Prompt 3 / M25.1: complete verification and reconcile current status.
Follow the common instruction. Do not introduce a new feature.

1. Review the combined diff from Prompts 1–2. Preserve existing regression
   fixes for TALK persona admission/report privacy, analytics as-of time,
   publication receipts, commerce refund admission and institutional seats.
2. Run scripts/verify.ps1 -Locked -Sql -LowMemory against the dedicated real
   test database, or the documented isolated equivalent when required by the
   environment. Inspect those scripts before invoking them. Run serially;
   do not run overlapping suites/demos against the same SQL database.
3. Verify both EF models against migrations using the existing design-time
   startup conventions. Do not rewrite old migrations or reset databases.
4. Fix concrete failures. Record failed attempts and any subsequent passing
   rerun honestly. Run the full suite on the final source state if fixes changed
   it; do not combine unrelated historical passes into a fictitious final run.
5. Refresh the primary MILESTONE_MATRIX rows, PROJECT_STATE and stale README/
   ARCHITECTURE summaries. Distinguish implementation, offline verification,
   live qualification and production release. M22 stays missing unless current
   code proves otherwise; retain existing M26/M27 private implementation.
6. Record current source identity using HEAD plus the reviewed diff/hash when
   uncommitted. Include exact commands, TRX/log references and totals/skips.
7. Provide the minimal owner handoff for commit/push and checking hosted CI.
   Do not claim green hosted CI without observing the matching run.

Acceptance: exact candidate source has complete passing offline and real-SQL
verification, current documentation, and a matching successful hosted offline
CI run after push. Keep any unavailable gate marked pending. Do not close
M25.1 solely on focused regressions or older saved test totals.
```

**Milestone checkpoint:** review and commit/push M25.1 through your normal workflow. Confirm CI passes before starting the AI integration work.

## Prompt 4 — integrate original AI scripts with the existing factory

```text
Execute Prompt 4 / M25.2: connect the existing content factory to budgeted,
durable AI generation. Follow the common instruction. M25.1 must be verified.

Inspect DraftFactory.cs, FactoryExecution.cs, FactoryPorts/Contracts/Model,
LocalDraftTemplates.cs, WorkflowRuntime/Execution/Tools, AiService/Execution,
EditorialWorkbench/Binding, TrendStories and relevant tests/composition roots.

Current gap: the factory's registered IFactoryTemplate implementations render
local lesson fields. The real text-provider adapter exists elsewhere. Preserve
the local renderer as a clearly identified fixture/deterministic mode.

1. Trace the current M8 AI workflow output and existing editorial import/binding
   operations. Choose the smallest bridge that lets a factory work item own or
   reference one existing workflow/AI operation. Do not build another runtime.
2. Pin prompt, route/profile, evidence, audience/locale, format, current reviewed
   cast/relationship/presentation snapshots and output-schema version. Generate
   useful original script/story sections while preserving exact canonical text
   and separate interpretation/fiction labels. Treat source data as untrusted.
3. Make generated output enter existing M12/M13 review and lineage, not an
   untracked string field. Preserve source/cast provenance into WATCH.
4. Adapt persistence only as necessary to reference durable child operations.
   Use additive migrations and retain old local-template records/behavior.
5. External calls must happen outside SQL transactions. A factory lease/retry
   must resume or reconcile the original workflow/AI operation, not create a
   fresh paid operation. Propagate cancellation and final admission checks.
6. Enforce aggregate batch cost by reserving/admitting against the bounded M7
   operation budgets; do not introduce a competing monetary ledger. Preserve
   unknown costs and all fallback attempts. Do not label real generation free.
7. Add scoped API/UI controls for selecting an already-qualified generation
   route and showing estimated/actual usage, review state and blockers.
8. Test restart, duplicate request, stale lease, cancellation before dispatch,
   unknown outcome, exhausted budget, source/cast withdrawal, invalid schema
   and fabricated citation. Prove route changes affect new runs only.

Acceptance: a factory request uses the existing AI workflow, creates a linked
reviewable original script and preserves lineage/costs/recovery. Offline tests
use controlled providers; live creative quality remains pending until Prompt 7.
Do not add a new vendor SDK unless the selected deployed adapter is insufficient.
```

## Prompt 5 — one real trend intake adapter

```text
Execute Prompt 5 / M25.2: implement one permitted live trend/news intake path
using the existing trend source boundary. Follow the common instruction.

Inspect TrendPorts/Contracts, TrendAdapters.cs, TrendWorkbench.cs, TrendFence.cs,
TrendModel.cs, source rights registration and the current staff trend UI.

1. Prefer an existing approved configured RSS/news source. Verify the provider's
   current official access documentation and permissions before implementing
   its contract. If no source is approved yet, implement bounded configured RSS
   intake and leave it disabled/unqualified until a permitted URL is supplied.
2. Retain uploaded RSS/manual intake. Distinguish discovered metadata from
   retrieved permitted substance and independently verified factual claims.
   A headline/link is not a transcript, source license or verified event.
3. Fetch only configured allowlisted destinations. Bound time, response bytes,
   redirects, content type and attempts; block loopback/private/link-local and
   metadata-service destinations, including redirects/DNS resolution changes.
   Disable XML external entities/DTDs. Do not execute source instructions.
4. Make repeated polls idempotent at source identity plus content/observation
   version, independently of the poll command's operation ID. Do not fill a
   cluster with unchanged duplicate candidates or invalidate approvals merely
   because the same observation was polled again. Retain changed observations
   intentionally, including meaningful metric/time changes, with bounded history.
5. Preserve source time, retrieval time, canonical URL/ID, coverage and rights.
   Keep existing verification, stale-event, no-match, sensitive-topic and human
   review rules. Do not auto-approve a teaching connection from popularity.
6. Route approved trend briefs through the same factory path from Prompt 4.
   Do not maintain a separate AI story engine for trends.
7. Add controlled HTTP contract tests for repeated/changed feeds, malformed XML,
   timeouts, oversized content, disallowed redirects and unavailable substance.
   Add SQL tests for stable candidate identity and correction propagation.

Acceptance: one configured permitted source can produce timestamped candidate
metadata without duplicates; evidence/rights are explicit; an approved candidate
can reach the shared factory. Missing source access leaves live qualification
blocked, not replaced by an invented feed or fabricated transcript.
```

## Prompt 6 — launch language, artwork and narration

```text
Execute Prompt 6 / M25.2: complete one usable, qualified media path through the
existing M16/M17 modules. Follow the common instruction.

Inspect AiService profile validation, LessonEngine/translation adapters,
MediaProviders, WindowsSpeechProvider, MediaGeneration/CharacterMedia,
WatchFactory/Execution, FfmpegWatchRenderer, UI and relevant contracts/tests.

1. Reuse an explicit existing launch locale. If none is recorded, use en-IN for
   the first technical integration because current code supports it, and record
   this as a temporary technical default, not approval of the public language.
   If the owner selects mr-IN or hi-IN, implement validated capability support
   throughout generation/translation/voice/rendering before claiming support.
2. Replace placeholder media for the pilot with a genuinely approved imported
   character art pack and usable narration, or selected qualified generation
   adapters where these are needed. Imported licensed art is sufficient for
   this step; do not require a new image vendor merely to complete a video.
3. Preserve stable character IDs, visual/voice profiles, pronunciation rules,
   rights, consent where applicable and immutable episode cast/package bindings.
   Add assets for one data-defined guest without hardcoding that character.
4. Use an actual speech provider supporting the selected voice/language. Existing
   SAPI may be used only where its capabilities and intended-use permissions
   are adequate. Tone fixtures must remain clearly non-narration fixtures.
5. Keep FFmpeg composition and approved-script captioning. Bound/split longer
   narration at supported stage limits if necessary, preserving ordering,
   timing, per-operation costs and replay identities. Add no generative-video
   dependency for this pilot.
6. Keep provider-bound resource IDs and qualification: voice fallback cannot
   silently change character identity or language. Unsupported configurations
   must fail visibly. Do not broaden source licenses to make a render pass.
7. Test actual adapter contracts, rights/voice-binding rejection, restart,
   unknown media outcome and accepted-stage reuse. Produce a playable sample
   when configured tools/assets permit it and inspect decode/captions/audio.

Acceptance: actual approved artwork and spoken narration can traverse M16/M17
in the selected supported locale. Human visual/listening/native-language review
is recorded only when genuinely supplied. Missing assets/voice permission are
explicit prerequisites, not synthetic approvals.
```

## Prompt 7 — integrated trend-to-video acceptance

```text
Execute Prompt 7 / M25.2: prove the complete integrated vertical slice from
Prompts 4–6. Follow the common instruction.

1. Select one real permitted current event with reviewed evidence and a
   defensible Geeta lesson connection. Preserve factual context separately
   from fictional treatment. If no suitable connection exists, reject it and
   choose another eligible candidate; do not force the match.
2. Create two genuinely different audience variants using existing reviewed
   profiles. Reuse the character registry and include one approved guest where
   the brief needs that role. Freeze each cast and presentation snapshot.
3. Run original-script generation through M7/M8 and the factory bridge; complete
   actual editorial review before producing public-eligible material. Export
   private review outputs when genuine release review is not yet available.
4. Produce captioned WATCH masters and M18 packages for manual export. Include
   traceable source IDs, teaching rationale, script/cast/media versions,
   rendered-master checksums, actual provider usage and ledger-derived costs.
5. Exercise restart after an accepted generation/media stage, identical request
   replay, no suitable teaching match, source withdrawal and stale event checks.
   Prove no accepted paid stage repeats and ineligible content cannot advance.
6. Provide one repeatable Windows/VS Code demo command using existing scripts
   or a narrow orchestration helper. It must support an explicit fixture mode
   and a separately enabled live mode with a real bounded budget. Never default
   to paid calls, public posting or real financial transactions.
7. Run focused integration tests, then the established full offline/SQL checks
   on the final code state. Update M25.2 evidence and current project status.

Acceptance: two useful original audience variants, real playable captioned
masters, sources/cast lineage and real cost evidence exist, and recovery/rights
checks pass. Distinguish technical playback from genuine human quality approval.
If live credentials, licensed material or real review are missing, retain
completed code and report M25.2 as partly verified, not fully operational.
```

**Milestone checkpoint:** inspect the actual videos and their evidence/cost report. Review, commit and push the code; confirm matching CI. Do not accept a screenshot of a fixture dashboard as evidence of real video generation.

## Prompt 8 — evidence-backed release tooling

```text
Execute Prompt 8 / M25.3: implement release decisions for the selected pilot.
Follow the common instruction. This is not authorization to deploy.

Inspect ops/release-baseline.json, scripts/release-check.ps1, deployment and
recovery scripts, ReadRules/ReadService, QualityEvaluation, existing eligibility
gates and the current security/privacy/operations runbooks.

1. Define the release scope from the owner's existing choice. In its absence,
   prepare a proposed READ/WATCH with manual-export pilot and keep TALK/voice,
   customer payments, B2B and automatic social publishing disabled. Do not claim
   the proposal is owner-approved production scope.
2. Map each enabled feature to its actual required evidence: exact code/content/
   policy/provider versions, source rights, M12 qualification, human review,
   usability, hosting, monitoring and recovery as applicable.
3. Evolve the current private-only checklist into a deterministic decision that
   evaluates current applicable evidence. Reuse the existing evidence stores;
   add only the minimal operational evidence representation that is missing.
4. Missing, expired, mismatched, revoked, fixture-only or superseded required
   evidence must block the affected feature. Fixtures cannot qualify human or
   live requirements. Do not replace hardcoded false with a manual ready flag.
5. Preserve dependency rules: disabled payments need no live payment approval,
   but an enabled WATCH feature still needs its source/media/quality evidence.
   Material changes must invalidate the relevant decision.
6. Align effective route/feature exposure with the release manifest. Confirm
   unqualified routes stay unavailable rather than merely absent from menus.
7. Add meaningful gate tests for missing/expired evidence, fixture substitution,
   changed code/policy/provider binding, source withdrawal and disabled optional
   features. A synthetic passing record is test data, not real approval.

Acceptance: the decision lists precise blockers and evidence references for the
selected scope, fails closed and cannot qualify a live release using fixtures.
No public deployment, account messaging, platform publication or charge.
```

## Prompt 9 — execute the selected release acceptance

```text
Execute Prompt 9 / M25.3: collect and evaluate genuine release evidence for the
selected pilot, using the checks from Prompt 8. Follow the common instruction.

1. Verify the current revision and explicit selected release scope. Run only
   the relevant remaining acceptance checks, reusing still-valid evidence.
2. Inventory the actual launch corpus subset. Preserve exact scripture,
   translation/interpretation labels, rights and visible coverage limitations.
   Do not expand to all 701 edition entries as an unrelated prerequisite.
3. Provide a concrete reviewer packet for the existing M12 development/held-out
   requirements and chosen audiences/language/media. Import genuine reviews
   when available. Do not impersonate reviewers or relabel fixtures as human.
4. Verify the actual selected UI journeys on phone-size and desktop screens,
   keyboard navigation and relevant screen-reader behavior. Test content
   withdrawal across the actual reader/search/export surfaces.
5. On an authorized staging/local environment representative of the deployment,
   verify selected authentication/TLS/secret configuration, bounded realistic
   load, observable failures, alert test evidence and SQL-plus-asset restore.
   State what a local drill does and does not prove for the intended hosting.
6. Check preserved source/cast IDs, accepted-operation bindings and any relevant
   balances during recovery. Respect irreversible privacy redactions; do not
   restore old sensitive text merely to demonstrate rollback.
7. Fix only demonstrated blockers. Re-run affected tests and the required final
   gates if code changes. Produce a revision-bound release report with Pass,
   Fail or NotRun for each requirement, evidence references, limitations and
   owner actions needed for anything external.

Acceptance: production-ready may be stated only when all applicable real gates
pass. Otherwise provide a precise blocked decision and a short remaining list.
The engineering portion can be complete while human/provider/deployment
qualification remains pending. Do not start M22, add new product scope or
publish/deploy as part of this demonstration.
```

## If a Codex session is interrupted

```text
Resume the selected prompt from docs/GeetaStudio_Codex_Implementation_Steps.md.
Read AGENTS.md, git status/diff and the current milestone resume checkpoint.
Preserve completed code and user changes. Verify the last known passing point,
then finish the remaining authorized work. Do not restart the milestone or
mark external/human gates complete without evidence. Run only checks needed
for the remaining changes and milestone acceptance. Stop after this prompt.
```

## What to check before accepting a milestone

Ask Codex to report these five things together: exact source revision/diff; implemented behavior; test results including failures/skips; working local demonstration command; remaining live/human prerequisites. At M25.2, additionally inspect the actual generated scripts, videos, lineage and costs. At M25.3, inspect the feature-specific release decision rather than relying on a general statement that everything is complete.

No additional milestone is proposed for cosmetic refactoring, more characters, more universes, every provider/platform, a full-corpus launch, or replacing the modular monolith.
