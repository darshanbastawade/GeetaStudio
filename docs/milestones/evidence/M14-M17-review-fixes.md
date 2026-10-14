# M14–M17 review fixes — 13 October 2026

The three reproduced P1 review findings are fixed. No schema change, permission upgrade, database reset, paid/live provider call, public publication, commit or M18 work is included. Existing M15–M17 work is preserved.

## Behavior

- Media admission, provider dispatch and generated-asset acceptance now require Generate/AI permission for direct output sources and inherited reference/parent media and content provenance. A denial occurs before dispatch. Already-submitted unknown outcomes retain their original provider/account/resource and can reconcile the original cost/result; revoked rights still prevent accepting that result as an asset.
- WATCH compares the original generated voice's exact profile identity and immutable provider/account/version/resource configuration. Imported recordings cannot gain a generation mapping through matching strings. An approved alternative requires a new reviewed package/presentation. Admission, scene execution, current timeline inspection and export enforce the binding; original media/timeline history is retained.
- Trend use rechecks the pinned lesson's current anchor, interpretation, corpus review and source permissions. Invalid teaching is immediately withheld from story views and blocked at use/review boundaries; periodic recheck holds affected briefs and creates idempotent editorial remediation for the actual teaching claim. Correction traversal includes both direct brief bindings and factory artifacts, then uses existing descendant/copy task propagation.

## Permanent regressions

`tests/ContentOS.Foundation.Tests/SqlMilestoneReviewFixTests.cs` covers direct/inherited AI denial, rights changes between admission/dispatch/acceptance, preservation of original unknown outcomes, colliding imported-voice names, changed generated-voice profile versions/accounts, legitimate original profile admission, and corrected teaching redaction/hold/direct binding/correction targeting/idempotency. These use owned synthetic fixture data in the dedicated real SQL Test database, never fabricated human approvals or upgraded existing source permissions.

## Run and debug

From C:\dev\GeetaStudio in PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
$env:GEETA_SQL_TESTS = '1'
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlMilestoneReviewFixTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\watch-demo.ps1 -ManageApi
```

Stop existing debug hosts before the demo (port 5100 must be free). Normal Development uses the existing VS Code **Geeta Studio: all** F5 compound. For the concrete WATCH fixture, run the demo with `-DebugPause`, choose **Geeta Studio: M17 attach Api**, F5, and attach to the printed API process. Use the printed tenant/universe at `/staff/watch`. Breakpoints: `MediaLibrary.GenerateAsync`, `ProviderRequest`, `WatchFactory.CreateAsync`/`SceneIdentity`, `TrendFence.AssertBrief` and `TrendWorkbench.RecheckAsync`. Detach and press Enter in the demo terminal to finish restart checks and stop owned hosts. See `docs/M17_LOCAL_RUN.md` for prerequisites and full debugging details.

## Verification and limits

Genuine human M12 acceptance, editorial/source/publication rights, native-language/art/listening quality, paid/live providers, browser/mobile/screen-reader acceptance and a fresh interactive F5 session remain pending. Local fixtures and actual English Windows speech do not qualify public release.

### Results actually run

- Full `scripts/verify.ps1 -Locked -Sql -LowMemory`: locked restore/build pass, zero warnings/errors; 92 offline pass; 152/162 SQL pass, 10 failed, 0 skipped, exit 1. First failure was an older workflow cancellation test with a MultipleActiveResultSets/disposal error; six subsequent workflow tests hit the workflow lock. WATCH recovery, M04 corpus and M02 scoped mutation tests hit SQL query timeouts. This is retained as a failed full run, not a successful combined verification.
- Final build after tightening trend preparation/redaction: zero warnings/errors. Final 21/21 real SQL focused tests pass, 0 skipped, across SqlMilestoneReviewFixTests, SqlMediaTests, SqlWatchTests and SqlTrendTests (including the previously failed WATCH recovery). Separate unchanged retry of the other nine failed tests: 9/9 pass, 0 skipped. All ten full-run failures therefore passed targeted rechecks without changing timeouts/assertions/data or business logic; full-suite reliability remains a caveat and no new all-green combined run is claimed.
- Original persisted reproduction records: mismatched WATCH timeline now withheld with the imported-recording mapping blocker; corrected trend brief redacted and held; history retained. See `M14-M17-fixes-current-use.json`.
- Actual Windows spoken demo repeats successfully: 3.49 seconds of English SAPI speech, vertical/standard masters, Windows staff HTTP 200, caption serving, API restart and exact script/master checksums. Demo hosts stopped; no live/paid calls or public publication. See `M17-demo.json`; `.local/m17-demo` retains local outputs.
- Both EF models report no pending model changes. No new migrations. Development remains 19 Core/4 Runtime migrations; original 8 characters, 3 universes, 50 corpus entries, 10 jobs and 5 source permissions preserve exact IDs and full permission hashes. Preservation checker passed; see `M17-preservation.json`.
- `git diff --check` passes after removing new EOF blank lines. Initial final format check found three EOF CRLF/LF markers, corrected; final independent format verification exits0.

Logs and TRX files remain ignored under `.local/M14-M17-fixes-*` and `tests/ContentOS.Foundation.Tests/TestResults`. Broader SQL resource/test-cleanup stability, genuine human/source/live/quality/public qualifications and fresh interactive F5 remain open; M18 is not started.
