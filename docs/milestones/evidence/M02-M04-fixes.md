# M02–M04 review fixes — 10 October 2026

All three confirmed review findings are fixed. M05 has not started.

1. **Shared source rights:** Core Read/List now check current intended-use rights for every returned revision within a serializable transaction. Restricted rows are omitted from lists; direct restricted reads fail. Owning staff retain evidence-review access.
2. **Write-only disclosure:** All cross-tenant create/revise/transition/import responses and operation replays return metadata with an empty Revisions collection. Read and Write flags stay independent; text requires the separate authorized, rights-checked read boundary.
3. **Canonical slot substitution:** Immutable CorpusSourceBinding records pin the selected manifest's snapshot/source edition, component/locale, exact UTF-8 hash, typed chapter/verse and source locator. Administrator registration establishes these bindings. Import, approval, reader and report enforce them. Changing target chapter, translation, snapshot or citation cannot bypass the pinned binding.

Additive migration: 20261010152946_M4SourceBindings, applied to GeetaStudio_Dev and GeetaStudio_M1Tests. No previous migration, cited source row, credential or database was reset. API receives SELECT/INSERT only on the new table; worker receives no corpus grant. A failed initial application exposed a foreign-key column-length mismatch; only the new, unapplied migration/model definition was corrected to match existing columns. A conflict-response regression was also corrected; existing immutable-data changes still return conflict.

Executed final verification:
~~~powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Locked -Sql
~~~
Result: 48 offline + 50 real SQL = **98 passed**, no skips, zero warnings/errors, locked restore and formatting clean. CoreDbContext has no pending model changes.

Four new SQL cases exercise shared allowed/unknown/revoked historical reads and list filtering; write-only create/revise/transition/import and replays; wrong chapter/translation/snapshot/source mapping; and preserved-but-restricted legacy evidence. Earlier milestone regressions also pass.

Actual Windows-authenticated corpus-demo.ps1 ran twice. Bound version 2 retains the same manifest/snapshot IDs on replay and gives 25 approved-readable Sanskrit/English pairs, with 676 missing out of 701 declared edition locators. Original version 1 and its 25 rows are retained, but unbound text is withheld as UnboundEvidence. No unsafe automatic backfill from old imports was performed. The development database still has 10 jobs, 3 universes and 8 characters; the new table contains 50 bindings. SQL readiness was Ready. Owned acceptance hosts were stopped; doctor confirms ports 5100/5200 free.

To run the updated local sample:
~~~powershell
Set-Location C:\dev\GeetaStudio
.\scripts\db-update.ps1 -ProvisionRuntime
code C:\dev\GeetaStudio
~~~
Select Geeta Studio: all in VS Code and press F5. In its terminal run .\scripts\corpus-demo.ps1, then open http://localhost:5100/staff/read and select the newer bound manifest. Full manual-host and breakpoint instructions: docs/M4_LOCAL_RUN.md.

Legacy manifests remain available as restricted evidence. For additional chapters/components, register a new manifest version with independently reviewed bindings from the source evidence; do not infer a trusted source location from already imported text. Canonical source authenticity still requires substantive review; typed bindings protect the reviewed mapping, not scholarly truth.

Not verified: fresh interactive F5/browser visual or accessibility checks, independent human manuscript/theological review, hosted CI/cold-cache checkout, Azure SQL, production workload or safe rollback/restore. No new live provider, AI operation, publication, commit or deployment was introduced. Full-source coverage remains deliberately incomplete.

Original review: docs/milestones/evidence/M02-M04-review.md. Updated bound demo: docs/milestones/evidence/M04-bound-demo.json.
