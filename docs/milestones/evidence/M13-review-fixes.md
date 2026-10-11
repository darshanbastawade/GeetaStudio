# M13 review fixes — 11 October 2026

Status: Offline Verified locally. All three P1 findings are fixed; final locked verification and Windows demo pass. The three P1 findings from M09–M13 validation have dedicated real-SQL regressions. No M14 work.

## Changes

1. Evaluate and Approve both require the exact current content audience, locale and body. Bound registry/factory cast context must be present in the case and match output character/profile/role/voice/life-stage IDs.
2. Lineage resolves asset-target provenance to the owning revision, follows both directions, and checks tenant/universe ownership of targets, parents and assets. Source corrections now identify asset-derived content and its external-copy task.
3. Direct registry episodes and factory snapshots share current cast eligibility checks, including activation, review, pinned profile and presentation. Factory ancestors are shown in review context and tracked by the approval fingerprint, so retiring an ancestor cast also expires derivative approval and blocks legacy export.

Approval policy editorial.v2 invalidates approvals made under the old checks while keeping their history. Existing approved work needs reevaluation/review before renewed approval. No schema migration, database reset, source-permission or credential change.

## Regression evidence

- Initial focused13/13 pass before ancestor dependency addition.
- Final focused14/14 pass (13 SQL+1 offline),0 skipped; build0 warnings/errors.
- Four new real-SQL tests: audience mismatch at Evaluate and Approve; bidirectional asset lineage/correction plus copy and durable restart; omitted factory cast and retired-cast renewed approval denial; factory ancestor visibility and retirement-blocked derivative export.
- Existing direct-episode retirement regression now uses actual cast-bearing evaluation reports. Fixture reports retain explicit fixture status; no human qualification fabricated.
- Intermediate full run interrupted deliberately after dependency-fingerprint/context changes. Its offline83 passed; it is not reported as a completed combined gate.
- Final scripts/verify.ps1 -Locked -Sql -LowMemory exits0:83/83 offline and138/138 real-SQL tests,0 skipped,0 build warnings/errors; formatting clean. SQL duration6m55s. .local/M13-review-fixes-final-full.log.
- Focused evidence: tests/ContentOS.Foundation.Tests/TestResults/M13-review-fixes-final-focused.trx and .local/M13-review-fixes-final-focused.log.
- Historical counterexample observations remain unchanged in M09-M13-counterexamples.json.

## Run and remaining limits

Use docs/M13_LOCAL_RUN.md. Existing API/Web/Worker Development and Geeta Studio: all F5 remain the local run/debug path. Workbench:http://localhost:5100/staff/editorial. Fixture demo:scripts/editorial-demo.ps1 -ManageApi, with port5100 free.

Final Windows demo exits0: Windows staff/private preview200, text/audio invalidation, role403 boundaries and actual API restart durability pass; owned hosts stopped,live calls0. Both final EF model checks report no pending changes. Windows doctor and git diff checks pass. Evidence:.local/M13-review-fixes-windows.log and M13-demo.json. Fresh interactive F5/breakpoints and visual/mobile/accessibility remain unverified. Genuine M12 human dataset/release acceptance, non-fixture providers/media, actual publication/remediation, hosted CI and production qualification remain pending. Local publishing is dry run only.
