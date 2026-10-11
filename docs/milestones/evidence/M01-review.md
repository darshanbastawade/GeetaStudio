# M01 review and validation — 10 October 2026

Reviewed runtime persistence, runner, authorization/endpoints, secrets, local provisioning and milestone requirements. Application source was not changed.

## Findings

1. **[P2] Skip jobs on saturated connections when selecting runnable work** — C:\dev\GeetaStudio\src\ContentOS.Infrastructure\RuntimeStore.cs:188–191. ClaimAsync selects the earliest job before checking its connection's concurrency. If A is occupied and the earliest queued job is another A job, it returns null without considering later jobs on available B. With two workers, the idle worker repeatedly chooses that blocked job; B can wait unnecessarily and miss its deadline. Select an eligible connection or continue to another candidate while retaining atomic concurrency checks. Add a cross-provider scheduling regression.

   Actual SQL reproduction: held A's only slot, queued another A job before a B job, then called ClaimAsync in a separate scope. Result: `claim=null, runnable B=Queued, A capacity occupied=true`.

2. **[P2] Treat an open circuit as temporary unavailability** — C:\dev\GeetaStudio\src\ContentOS.Infrastructure\RuntimeStore.cs:240; C:\dev\GeetaStudio\src\ContentOS.Application\RuntimeRunner.cs:24; RuntimeStore.cs:290. CanCallAsync returns the same false result for a temporary open circuit and permanent policy/kill rejection. RuntimeRunner converts it to PolicyRejected, so unsubmitted work becomes DeadLetter immediately, despite a valid deadline and unused retry allowance. Defer until circuit expiry or return a typed unavailable result governed by the original bounded retry/deadline policy. Keep kill/disable rejection distinct.

   Actual SQL reproduction: a review-owned B connection had a circuit open for 30 seconds, with a new job whose deadline remained valid. The actual admission/completion path produced `allowed=False, job=DeadLetter, deadline still valid=True`.

## Validation

- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Locked -Sql` passed again: locked restore/build, **0 warnings/errors**, **25 offline + 13 SQL = 38 tests**, clean formatting.
- Two targeted probes used real GeetaStudio_M1Tests through the public runtime store/configuration ports. No in-memory durability substitute or application test/source edits.
- Review-created jobs were cancelled/terminated through store operations; circuit state was cleared; the prior active test route was restored. Historical records were retained; no database reset/drop.
- No hosts, debugger sessions, live providers, commits or pushes were started by this review.

Passing current tests does not cover these two scheduling cases. Resolve both findings and add the regressions before proceeding to M2. Existing limits remain: hosted CI, cold-cache checkout, a separate M1 interactive F5 session, and production/live-provider qualification were not verified here.

## Resolution — both findings closed, 10 October 2026

The findings and 38-test count above record the initial review. The authorized fixes now skip capacity-blocked connections at selection, keep the transactional concurrency recheck, and distinguish CircuitOpen from policy rejection. Circuit waits preserve the original operation, submission allowance, budget and deadline; submitted/unknown operations keep their reconciliation binding.

Actual validation: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Locked -Sql. Result: 25 offline + 17 actual SQL = 42 passing tests, no skips/failures, 0 build warnings/errors, formatting clean. Four new SQL regressions cover cross-provider scheduling/capacity, circuit wait/recovery, post-claim admission race with one original effect, and deadline termination/budget settlement. Existing recovery and security regressions remain passing.

No owner setup, new migration, secret change or database reset is needed. Restart already-running hosts after rebuilding. M2 has not started. Interactive F5 and the separate real-process crash helper were not rerun for this change; hosted CI, cold-cache checkout and live/production qualification remain unverified.