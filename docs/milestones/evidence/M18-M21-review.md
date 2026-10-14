# M18–M21 review — 14 October 2026

Verdict: changes follow the modular-monolith/provider-port direction, but correctness approval is withheld pending the findings below. Production files, packages, migrations and existing work were preserved. No M22, public publishing, customer charge, external notification, commit or reset was performed.

## Findings

1. **P1 — Recheck the current persona version at AI admission.** `AiTalkAdmission.cs:25` accepts an approved registry row and active character without the current core-version check used by `TalkWorkbench.Check`. The isolated SQL reproduction appends a newer character revision during M7's evidence retrieval, after TALK's earlier check but before AI admission. A new AI run is admitted instead of rejecting the stale reviewed persona. Apply the same current-version fence at preparation and attempt admission, and require renewed review.
2. **P1 — Withhold revoked source text in problem reports.** `TalkWorkbench.cs:60–61` returns immutable problem evidence verbatim; `ReportAsync` copies full `SourcesJson`. The SQL reproduction reports an answer, revokes its source, confirms ordinary history withholds sources, then obtains the exact revoked passage from the staff problem report. Keep immutable evidence identifiers/hashes, but recheck current source rights/corrections before exposing textual diagnostics; also guard newly submitted reports.
3. **P2 — Read the actual publication receipt event for time-to-publish.** `AnalyticsReports.cs:40` selects history kind `Published`, while `SocialExecution.Apply` records successful publication as `Receipt`. Therefore genuine successful M18 receipts cannot populate `TimeToPublishSeconds`. The initial SQL attempt timed out in `MediaRightsGuard.Revision` during package scheduling. A subsequent serial SQL retry completed the actual publication with a remote ID, then failed the report assertion because `TimeToPublishSeconds` was null (28 seconds). The defect is now reproduced end to end. Use the first authoritative successful receipt, including original-provider reconciliation/manual receipt, without counting later duplicates.
4. **P2 — Respect metric observation time in as-of reports.** `AnalyticsReports.cs:33` chooses the latest observation without filtering `ObservedUtc` to the requested as-of. Import accepts up to five minutes of future clock skew. The SQL reproduction imports a 100 observation and a 120 observation dated four minutes ahead, then asks for the report as of now: it returns 120 instead of 100. Filter observations by as-of before choosing each window's value (and handle all-future observations as unavailable).

All four findings now have completed SQL reproductions that failed their desired-behavior assertions, demonstrating defects; they are not successful acceptance tests. The initial publication attempt was an infrastructure failure; its subsequent serial retry reached the assertion. These review-only sources are stored as [text](M18-M21-review-tests.cs.txt) and [MSBuild hook](M18-M21-review.targets.txt), outside the normal test compile items, so production code and the normal suite were not altered.

## Checks actually run

- `scripts/verify.ps1 -Locked -Sql -LowMemory`: locked restore and build succeeded, zero warnings/errors; **105 offline passed, zero failed/skipped**. Test database migrations applied without reset. The full SQL suite did not complete and was stopped after severe resource waits/timeouts. It reported an editorial bounded-schedule failure, a customer refund deadlock and a core shared-read timeout; there is no final SQL pass count and no all-green claim.
- Review-only build with `CustomAfterMicrosoftCommonTargets=.local/M18-M21-review.targets` and separate `.local/review-bin/`: zero warnings/errors.
- `dotnet vstest .local/review-bin/ContentOS.Foundation.Tests.dll --TestCaseFilter:FullyQualifiedName~ReviewTests`: four failures, zero passes/skips; three desired-behavior failures above and one scheduling SQL timeout, elapsed 18m02s. See `.local/M18-M21-review-reproductions.trx` and log.
- There was a brief unintended overlap between the broad and targeted SQL runs. The refund deadlock occurred during that overlap; it is not attributed to a demonstrated payment business-rule defect. Subsequent recovery checks are serial.
- Read-only SQL diagnostics showed `RESOURCE_SEMAPHORE` waits (memory-grant pressure). No SQL configuration changes were made. Test/build/command responses were severely delayed on this workstation.
- All four M18–M21 PowerShell demo scripts parse successfully; `git diff --check` passed. Standalone format and serial recovery results are recorded below once complete.

## Repeat the review reproductions

From `C:\dev\GeetaStudio`, copy the two linked text artifacts to `.local\M18M21ReviewTests.cs` and `.local\M18-M21-review.targets`. Then:

```powershell
dotnet build tests/ContentOS.Foundation.Tests/ContentOS.Foundation.Tests.csproj --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:CustomAfterMicrosoftCommonTargets=C:\dev\GeetaStudio\.local\M18-M21-review.targets -p:OutDir=C:\dev\GeetaStudio\.local\review-bin\
$env:GEETA_SQL_TESTS = '1'
dotnet vstest .local/review-bin/ContentOS.Foundation.Tests.dll '--TestCaseFilter:FullyQualifiedName~ReviewTests'
```

Run SQL workloads serially with sufficient available memory. These tests intentionally assert the expected fixed behavior; current defects produce failures. The test source and hook are not part of the normal project. A successful prior milestone run does not override these newly reproduced issues.

## Run/debug and limits

Existing exact local instructions remain in [M18](../../M18_LOCAL_RUN.md), [M19](../../M19_LOCAL_RUN.md), [M20](../../M20_LOCAL_RUN.md), [M21](../../M21_LOCAL_RUN.md). From the project, F5 uses **Geeta Studio: all**; controlled demos are `scripts/social-demo.ps1`, `analytics-demo.ps1`, `customer-demo.ps1`, `talk-demo.ps1`, each with `-ManageApi`. API uses localhost5100, web5200; SQL and the exact SDK10.0.401 are prerequisites. Do not run demos concurrently with SQL tests.

No fresh interactive VS Code breakpoint, browser/mobile/accessibility assessment, live social/analytics/identity/payment/AI integration, real source permission, genuine reviewed M12 dataset, human safety/quality acceptance or production release was verified in this review. Prior milestone evidence remains historical. No new Development migration/grant/preservation check was needed or claimed because this review made no implementation/schema changes. Next action is repair these findings and repeat focused and full validation after memory pressure is resolved; M22 remains unstarted.

Standalone final format verification passed (exit0). The final documentation diff check is rerun after evidence updates.

## Final recovery results

- Serial targeted recovery: **6/6 passed**, zero failed/skipped, 1m37s, exit0. Includes the three broad-run failures unchanged, financial import/FX/provider history, concurrent credits/settlement, and grounded TALK/no-answer/route/replay/isolation. This is targeted recovery, not a completed full regression.
- Serial publication reproduction: one desired-behavior assertion failure (null time-to-publish after actual Published receipt), zero infrastructure failures, 28s. Together with the other three completed assertions, all four findings are reproduced.
- Standalone format exit0; Core and Runtime model checks exit0, neither has pending model changes. Four demo-script parsers and final diff check passed.
- Logs: .local/M18-M21-review-verify.log, review-build.log, review-reproductions.log/trx, review-publication.log/trx, review-serial.log/trx, review-format.log and core/runtime-model.log (each has M18-M21- prefix).
