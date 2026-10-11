# M14 verification — 11 October 2026

Implementation and controlled local fixtures: **Offline Verified**. Public release: **Blocked**. M15 has not started. C:\dev\GeetaStudio; main8515c3a and existing uncommitted work preserved. No stage/commit/remote changes, database reset, source-rights upgrade or live/paid call.

## Implemented

Blazor static READ home/universe/chapter/verse/concept/search/journey; current audience/language variants, Indic source text, explanation, action, reflection, attribution and previous/next; device-only bookmarks/progress; bounded consented engagement; scoped staff collection workbench; transactional current eligibility, immutable bindings/history, manual release gates and kill/rollback controls. Anonymous API, SSR, search and sitemap share current eligibility and no output cache. Private Windows-owner preview is loopback/Development only.

New staff collection page: **/staff/read-collections**. Original M04 corpus page remains **/staff/read**.

## Actual checks

| Check | Result | Evidence |
|---|---|---|
| Final complete locked gate |0 warnings/errors;85 offline +141 actual SQL tests passed;0 skipped; format clean | scripts/verify.ps1 -Locked -Sql -LowMemory; .local/M14-final-gate.log |
| Final build including latest static assets/staff link |0 warnings/errors | .local/M14-final-build.log |
| All three new SQL cases | Passed in final full141 suite | scope/authorization, replay/concurrency/history, restart/current rights, withdrawal/revocation, fixture activation denial, API no-store and rate429 |
| Both EF models |No pending model changes | .local/M14-final-models.log |
| Device JavaScript | Passed syntax and scenarios | node --check src/GeetaStudio.Web/wwwroot/read.js; node scripts/read-device-check.cjs; reload/bookmark/progress/expiry/clear/corrupt-state/unsafe links, explicit consent and private event isolation |
| Development migration/runtime grants and preservation | Passed | .local/M14-development.log; M14-preservation.json |
| Actual copy-only checksum backup/verify/isolated restore | Passed; exact immutable bindings/source permissions | .local/M14-recovery-final.log; M14-recovery.json |
| Actual Windows HTTP/restart demo | Passed | scripts/read-demo.ps1 -ManageHosts; .local/M14-final-demo.log; M14-demo.json |
| Demo specifics |Private SSR200; staff collections200;4 variants/2 verses; no Blazor boot; no-store/noindex; empty public directory/sitemap; API/Web restarted; withdrawn private verse404 and removed from search; collection disable404; all owned hosts stopped | M14-demo.json |
| PowerShell/VS Code JSON/diff syntax | Verified during implementation; final check recorded in milestone | Scripts parsed, tasks/launch JSON valid, git diff --check |
| Mobile/screen-reader/interactive F5 |Unverified | Automated browser access rejected by browser policy; native app automation unavailable. No new M14 breakpoint hits claimed. |

## Failures resolved and retained

Initial source fixture violated the existing single-edition-per-component/locale rule; the fixture now uses one edition with two snapshots per language. The rule was preserved. A nested reader anchor query was replaced by equivalent scoped joins with exact snapshot/hash/kind/locale/locator matching.

SQL timeouts occurred in reader and existing evaluation setup while SQL reported low physical memory and SLEEP_BPOOL_STEAL. User freed RAM. Focused2 retries passed, then final full141 SQL suite passed. No timeouts, assertions, server settings or unrelated processes changed. Earlier failed logs/TRX are retained.

Actual Windows private reader initially returned400/InvalidToken. Error-page re-execution was replaying NTLM401 handshake tokens. Private preview now preserves authentication challenges; owner/scope/Development/loopback guards remain. The real Windows demo passed afterward.

First broad SQL run passed140/141 and exposed a duplicate /staff/read route. M14 moved to /staff/read-collections; M04 route preserved. Final complete locked suite141/141 passes.

A single test formatting issue was corrected; final formatter gate is clean. First backup probe could not inspect protected SQL filesystem; the corrected owned-directory copy-only backup and isolated restore passed.

## Data preservation

Additive 20261011105245_M14Read: five reader-schema tables; applied Test and Development. Development retains8 characters,3 universes,50 corpus verses,10 runtime jobs and5 source permissions, exact existing ID hashes and full source-permission row hash.15 Core/3 Runtime migrations. Worker has no new reader write grants. Restore used a new identity-checked temporary database and removed only its temporary files/database; existing databases were not overwritten. No production/media-store recovery qualification.

## Release gates and local use

The pilot is an explicitly private two-verse/four-variant fixture, not25 human-reviewed release pieces. English/Marathi lesson variants and Sanskrit sources are available; no canonical Marathi translation or reviewed transliteration is invented.

PublicEnabled defaultsfalse. Current manual corpus/lesson/editorial reviews, exact audience/revision/rights and genuine M12 acceptance must hold. Practical actions/reflections must occur in the exact evaluated editorial body. Seven current fingerprint-bound Manual gate attestations (Rights, PrivacyChild, AccessibilityMobile, BackupRestore, Rollback, Monitoring, AbuseLimits) are required; fixture evidence cannot qualify a release.

Human100+20 labels, native/theological/age feedback, genuine publication/privacy/child review, hosted alerts/proxy/load and production/media recovery remain pending. No public deployment or vendor qualification was performed.

See M14_LOCAL_RUN.md for exact run, private demo, attach debugging, safe cleanup and manual mobile/accessibility checks. Existing Geeta Studio: all F5 compound remains. New attach profiles are present; interactive breakpoint verification remains pending.