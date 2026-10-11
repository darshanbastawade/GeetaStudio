# M02–M04 review — 10 October 2026

Three actionable findings were reproduced on real SQL. Application code was not changed. The existing locked suite still passes; its current coverage does not catch these cases.

## [P1] M03: shared reads bypass current source rights

Location: C:\dev\GeetaStudio\src\ContentOS.Infrastructure\ContentCoreStore.cs, lines 88–94 and 102–107.

AccessAsync sets FullRead for an explicit cross-tenant Read grant. ListAsync and ReadAsync only invoke SourceRightsGuard when FullRead is false, so a shared tenant receives all revision bodies without any current source-rights decision. This permits reading revoked, expired or unknown-rights source material through the ordinary core endpoint even though export/public-only reads are guarded.

Executed reproduction: create a source-backed Translation, grant another tenant Read, revoke the translation source, then invoke ReadAsync as that tenant. Actual: TextReturned=true, Revisions=1. The text remains readable after revocation.

Fix direction: distinguish owning staff review access from cross-tenant distribution. Apply current intended-use rights to every revision returned to a shared tenant, including list responses, within a consistent transaction. Do not let an access grant substitute for the source license. Add revoked/expired/unknown source regressions for cross-tenant Read and List.

## [P1] M02: write-only responses disclose old content

Location: C:\dev\GeetaStudio\src\ContentOS.Infrastructure\ContentCoreStore.cs, lines 190–218; also mutation/replay ViewAsync responses.

Read, Write and Export are deliberately independent grant flags. ReviseAsync checks Write, then returns ViewAsync containing the complete revision history. A write-only tenant can receive prior bodies that ReadAsync explicitly denies. This also applies to replay responses; transition/create/import response boundaries need the same review.

Executed reproduction: the second tenant retained a previously obtained rowversion, its Read flag was removed while Write remained enabled, and ReadAsync denied access. ReviseAsync succeeded and returned two revisions, including the original text. Actual: ReadDenied=true, ReturnedOldText=true, Revisions=2.

Fix direction: honor separate Read permission in mutation responses and operation replay, or explicitly prohibit unsupported write-without-read grants. Prefer a mutation result containing only permitted identifiers/version metadata when Read is absent. Test grant removal and replay, not only the all-permissions case.

## [P2] M04: source chapter/edition is not bound to the manifest locator

Location: C:\dev\GeetaStudio\src\ContentOS.Infrastructure\CanonicalCorpus.cs, lines 110–117.

The importer verifies a declared target locator, a quotation's ending verse number, locale/hash and substring membership in a retained snapshot. It does not bind that snapshot's source chapter or edition to the manifest locator. SourceLocator is arbitrary nonempty text. Changing Chapter from 1 to 2 while keeping the authentic 1.1 quotation/translation passes import and rights-based review.

Executed reproduction: take the committed 1.1 pair with unchanged source text, hashes and source locator; import it at target 2.1 in a new test manifest; approve it. Actual: ManifestChapter=2, SourceChapter=1, ReviewStatus=Approved; displayed source locator still identifies Discourse 1, verse 1. This creates wrongly assigned canonical text despite valid bytes/rights. The real development sample itself was not modified.

Fix direction: add immutable typed source-edition/locator bindings to the manifest/import evidence, including explicit variant mappings. Validate target chapter/verse against that binding for both quotation and translation; retain manual scholarly review for semantic authenticity. Do not rely on parsing arbitrary display citation strings as authoritative evidence.

## Executed validation

- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Locked -Sql: 48 offline + 46 real SQL = 94 passed, no skipped tests, zero warnings/errors, locked restore and formatting clean.
- A temporary .NET console probe used the existing typed ports and unique fixtures in GeetaStudio_M1Tests. It reused the committed M04 source material; no fabricated scripture was created. Sharing flags were restored in finally.
- Probe outputs: WrongChapter approved; RevokedSharedRead TextReturned=true; WriteWithoutRead ReadDenied=true/ReturnedOldText=true.
- No production/development application records or source code were changed. The probe left uniquely identified review-only records in the dedicated test database.
- Reviewed core access/import/export/revision paths, source registry/rights/revocation, canonical import/review/reader/report, relevant tests and milestone specifications. No new live integration, F5 or browser session was performed.
- Review limitations: no claim of an exhaustive production security audit, hosted CI, production load or independent scholarly source review. Existing M04 missing coverage is documented scope, not a newly discovered bug.

Suggested next action: resolve these three findings and add focused SQL regressions before beginning M05.

## Closure

All three findings were fixed and covered by real-SQL regressions. Final locked verification: 98 passed. See [fix evidence](M02-M04-fixes.md) and [bound sample demo](M04-bound-demo.json). Historical reproduction details above are retained.
