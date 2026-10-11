# M14 — local reader run and debug

Run from C:\dev\GeetaStudio in Windows PowerShell. Existing M0–M13 work, identities and permissions are preserved. Public rollout defaults to disabled. The two-verse/four-variant pilot is a private fixture, not a human-reviewed publication.

## Build and ordinary Development

~~~powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
~~~

Open this folder in VS Code. Select **Geeta Studio: all** and press F5. Existing API localhost:5100, Web localhost:5200 and Worker profiles remain. The web home is the reader; the engineering health screen moved to http://localhost:5200/development (Development only). Reader administration is http://localhost:5100/staff/read-collections and editorial administration is /staff/editorial. Windows staff authorization and actual tenant membership both apply; no credential is placed in this runbook.

Alternatively run each host in its own PowerShell terminal:

~~~powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

An empty anonymous library is expected until genuine release inputs exist. /sitemap.xml is empty and unqualified /read/{key} URLs return404. No fixture becomes public by changing Read:PublicEnabled alone.

## Private pilot and repeatable acceptance demo

Stop F5/other hosts so ports5100 and5200 are free. These commands use only GeetaStudio_M1Tests, existing Windows integrated identity and deterministic fixtures. They do not add pilot content to Development or call paid providers.

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\read-demo.ps1 -ManageHosts
~~~

The VS Code task **Geeta Studio: M14 reader fixture demo** runs that command. It builds an owned fixture harness, starts hidden owned hosts, checks Windows-authenticated static reading, current English/Marathi/audience variants, anonymous exclusion and empty sitemap, restarts both hosts, withdraws one editorial approval, and disables the collection. It stops its own hosts afterward. Failure remains a failure; no retry budget, assertion or server setting is weakened.

For interactive inspection and debugging:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\read-demo.ps1 -ManageHosts -KeepHosts
~~~

When successful, open its printed http://localhost:5200/preview/read/{key} URL in a browser that supports Windows integrated authentication. The preview is restricted to the current Windows owner, loopback and Development; it has no-store/noindex and never sends engagement events. The API, configured collection and tenant must belong to this same identity. A remote phone cannot access this staff preview. Use browser device emulation for the private phone-size check.

The saved .local/M14-fixture.json contains only identifiers. To resume a successful fixture without generating a duplicate, add -ReuseFixture. A withdrawn/disabled fixture needs a new setup. KeepHosts leaves the private hosts running deliberately; stop them with:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\read-demo-stop.ps1
~~~

Cleanup verifies each PID's exact executable path and start time before stopping it. It does not stop unrelated API/debug hosts.

Select **Geeta Studio: M14 attach Api** or **Geeta Studio: M14 attach Web**, press F5 and select the matching GeetaStudio.Api.exe/Web.exe from .local/M14-owned-hosts.json. Put API breakpoints in ReadService.PreviewAsync/Piece/ControlAsync and a Web breakpoint in ReadClient.Book or ReadSurface.OnParametersSetAsync. Open the private verse page, follow a variant, search, or withdraw an approval. Attach avoids a second process competing for the ports. New M14 interactive breakpoint hits are not yet verified. The actual Windows HTTP/SSR/restart/withdrawal/disable demo passed.

## Reader routes and coverage

Public routes: /, /read/{key}, /read/{key}/universe, /chapter/{chapter}, /chapter/{chapter}/verse/{verse}, /concept/{concept}, /search?q={text}, /journey/chronological and /journey/{journey}, all underneath /read/{key}. The private equivalents start /preview/read/{key}. Verse variants use locale and audience query parameters. Their availability is derived from current reviewed bindings, not invented.

The pilot contains Sanskrit and English source text for chapter1 verses1–2, child English variants for both, a youth English variant for1.1 and a Marathi child variant for1.1. Marathi teaching is fixture translation memory; no unlicensed canonical Marathi translation is invented. No reviewed transliteration exists in this fixture: the reader says so. Text, interpretation, practical action, reflection, attribution, related concepts and previous/next stay distinct. Search is bounded literal text over eligible DTOs; it does not claim semantic M06 vector ranking. The M04 corpus, M05 source lineage, M09 lessons and M12/M13 reviews provide provenance.

This is two covered verses/four variants, not the proposed25-piece reviewed release sample and not full-corpus coverage. M14's implementation admits up to50 immutable pieces per version and20 public active collection keys.

## Checks and recovery

~~~powershell
$env:GEETA_SQL_TESTS = '1'
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlReadTests|FullyQualifiedName~ReadRuleTests'
Remove-Item Env:\GEETA_SQL_TESTS
node .\scripts\read-device-check.cjs
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\read-recovery-check.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
~~~

The recovery check creates a COPY_ONLY/CHECKSUM backup of Test, verifies it, restores into a unique GeetaStudio_M14Restore_{GUID} database, compares immutable reader bindings and source permissions, then removes only its identity-checked temporary database and project-owned backup. It never restores over Test/Development. It requires local SQL migration/backup authority. It qualifies this local SQL exercise, not production asset storage or a hosted restore strategy.

M14 adds 20261011105245_M14Read and five reader-schema tables. Forward migration is additive; do not run a schema downgrade as rollout rollback. Disable a collection through /staff/read-collections with its current rowversion or disable Read:PublicEnabled and restart the API. Activate/Rollback recheck all release gates; historical versions and approval/operation history remain. The fixture cannot pass activation or rollback-to-public.

## Accessibility/mobile manual checklist (pending)

At390px and desktop, verify no horizontal overflow, readable Indic text,44px controls, visible focus and source attribution. Tab from the skip link through navigation/search/variants/source details/device actions. Use a screen reader to verify headings, language and status announcements. Select English/youth/Marathi, search a known word and an unsupported word, follow chronological/previous-next, bookmark, mark read, reload and clear. With JavaScript disabled, text/navigation/search/source details still work; optional device actions require JavaScript. No generated media, external fonts or Blazor circuit is required to read.

Browser policy blocked the automated local visual check. Neither mobile emulation nor real-phone/screen-reader qualification is claimed. Final locked gate:85 offline/141 SQL,0 skipped; actual Windows demo and local restore/preservation pass.

## Release baseline and privacy

Public eligibility is rechecked on every API/UI/search/sitemap request; no output/index cache is introduced. Current rights, exact revision/audience, source and corpus reviews, approved M09 variant, current M13 approval and genuine M12 tested-condition acceptance must hold. Withdrawal, rights revocation, changed evaluation/cast or expiry withholds affected pieces; a public collection with any ineligible piece disappears entirely.

The practical action and reflection must also occur in the exact editorial body accepted by M12/M13 before public eligibility. Collection-bound, current-fingerprint, expiring **Manual** evidence is required for Rights, PrivacyChild, AccessibilityMobile, BackupRestore, Rollback, Monitoring and AbuseLimits. Maximum evidence validity30days. Fixture references cannot stand in for manual evidence. Read:PublicEnabled is a separate deployment kill switch, defaultsfalse and cannot replace those gates. Publication/admission is transactional with rowversion and immutable audit/operation replay.

Device bookmarks/progress/consent are optional localStorage only, version1, expiry90days after a save, bounded100 bookmarks/200 progress entries, clearable and not account-linked. Stored paths/IDs omit text bodies. Optional API counts require version1, explicit consent and an eligible public piece. They aggregate by piece/name/UTC day, capped10000 per bucket and retain seven UTC-day buckets with hourly pruning. No account/IP/search text is persisted in those events; rate limiting transiently partitions by visible client IP. Reverse-proxy trust/partition behavior requires deployment qualification.

API public reads/events:60 requests per minute per visible client IP, no queue; Web120. Private admin/preview retains Windows role/scope gates. API has existing redacted error503/readiness/audit and ContentOS.Read request metrics; hosted alert delivery and load qualification remain pending. Logs must not be configured to capture request query/body or identity during public deployment.

Genuine100+20 M12 human cases, native/theological/age feedback, release content/source rights and privacy/child policy remain pending under the owner's fixture-only agreement. No public release, real vendor qualification, production restore, visual accessibility/mobile or new interactive F5 verification is claimed. M15 has not begun.