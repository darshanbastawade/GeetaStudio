# M03 local run and debug

Run in PowerShell from C:\dev\GeetaStudio. Reuse the dedicated local SQL instance and private API/worker user secrets. No provider key, cloud account, Docker or WSL is needed.

## Setup and checks

~~~powershell
Set-Location C:\dev\GeetaStudio
.\scripts\db-update.ps1 -ProvisionRuntime
.\scripts\verify.ps1 -Locked -Sql
~~~

ProvisionRuntime applies additive runtime/core/source migrations, preserves existing credentials and seeds the existing core fixtures. Source tables use the sources schema and the existing core migration history. The API can update only source work/edition/review/remediation records; source party, permission dimensions, decisions, provenance, use, revocation, command and audit histories have SELECT/INSERT only. Neither host receives source DELETE/DDL privileges; the worker receives no source grants.

The current Windows account has staff Administrator and local fixture tenant membership. Global staff role does not grant every tenant. Keep credentials in user secrets. The SQL test database remains GeetaStudio_M1Tests; do not reset it.

## Run

Start each host in its own terminal:

~~~powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
~~~

~~~powershell
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
~~~

~~~powershell
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

Open http://localhost:5200 and choose **Open the secured source registry**, or open http://localhost:5100/staff/sources directly. The browser uses your Windows identity; the web server does not impersonate staff. Select your owning tenant/universe, register a work and edition, record an actual permission evidence reference, approve the permission, import a UTF-8 text file, then review its snapshot. Only tenant administrators can approve, revoke or resolve remediation; editors can register/import/link/use; readers can inspect metadata. These are staff UI foundations, not a public source reader.

The UI supports registration, editions, permission submission/review, file import/review, work revocation, and remediation resolution. JSON details are diagnostic metadata. Permission claims require independent human verification; clicking Approve does not establish a legal license. A URL alone is rejected as a permission evidence reference. No endpoint fetches URLs or arbitrary server paths.

In a fourth terminal run:

~~~powershell
.\scripts\sources-demo.ps1
# Optional saved local evidence:
.\scripts\sources-demo.ps1 -OutputPath .\M03-demo.json
~~~

The demo uses only docs/fixtures/M03-original.txt, an original synthetic example. It records explicitly labeled fixture permission, imports/reimports the same bytes, verifies attribution in export, creates a separate edition with unknown rights, records a fixture publication ledger entry, revokes the work, and verifies HTTP 422 plus an open remediation task. It makes zero actual publications and zero provider calls. Records remain for inspection; each run creates its own work. VS Code task **Geeta Studio: M3 sources demo** runs the same helper.

## Rights and endpoints

Sources are owning-tenant/universe scoped under /api/v1/sources/tenants/{tenant}/universes/{universe}. GET / lists works; /works/{work}, /review-queue and /remediation inspect state. POST routes: /works, /works/{work}/editions, /editions/{edition}/permissions, /permissions/{permission}/review, /editions/{edition}/files, /snapshots/{snapshot}/review, /snapshots/{snapshot}/evaluate, /provenance, /uses, /works/{work}/revoke, /remediation/{id}/resolve, /content/{content}/export.

All POSTs require X-ContentOS-Staff: 1 and actual membership. Business commands use a stable operationId: repeat identical requests with the same ID; different payloads conflict. Reviews/revocation/remediation require expectedRowVersion. Reload after 409. Rights denials return 422 with a bounded outcome reason; malformed input returns 400; unauthorized scope returns 403.

Imported .txt files are strict UTF-8, at most 64 KiB, with caller-supplied SHA-256 verified against bytes. JSON transports bytes as base64. Full retention requires an approved, unexpired retention grant. Otherwise only checksum, timestamps, length and evidence reference persist. Re-import dedupes by edition/checksum without changing retained evidence. Changed edition metadata requires a new key/version. No permission is inherited by a modern translation or new edition.

IntendedUse explicitly selects purpose, actual content language, territory, format, commercial/adaptation/translation/AI-processing flags. Checks combine current permission, source/snapshot review, expiry, work/edition revocation and all source ancestors. Generated/interpretive content and revision derivations require adaptation permission even if the caller omits it; language changes require translation permission. Unknown or pending rights are restricted.

The existing core GET export also enforces rights (default Export, actual locale, IN, Text, noncommercial). Source POST export accepts an explicit Export use; it returns content, citations, attribution, checksum/evidence references and provenance. Every historical revision in the export is checked. Linked source lineage follows revisions and attached assets. Canonical/translation/interpretation/current-event content without reviewed evidence cannot export or approve. Explicit public reads recheck current rights; staff inspection remains available for review. Synthetic/Fiction with Owned/Synthetic rights can export without third-party evidence, subject to truthful human classification. Rights checks cannot prove that a submitted ownership claim is true.

A source-derived export is portable evidence, not import authority: core import rejects citation/provenance-bearing envelopes rather than stripping dependencies. Register/review/rebind source evidence explicitly; no automated cross-tenant source transfer exists.

Publication records are Fixture or ReportedExisting ledger entries, not social publishing. Revocation blocks new rights-checked uses and identifies impacted content/assets transitively; published ledger entries receive remediation tasks. Resolution records evidence of manual action; it does not revoke the source's restriction or perform an external takedown. Actual generation/retrieval/publication adapters in later milestones must use the typed rights guard and preserve lineage.

Traversal is bounded to 100 ancestor revisions / 100 edges per node; local revocation impact is bounded to 10,000 edges. Queue/list/detail views are capped at 100. These are explicit local foundations; production-scale pagination, retention/takedown policy and load qualification remain open.

## Debug

Stop existing CLI hosts first. Open C:\dev\GeetaStudio in VS Code, choose **Geeta Studio: all** in Run and Debug, then F5. Existing compound builds once and starts API5100/Web5200/Worker. Put a breakpoint in SourceRegistry.ImportAsync, SourceRightsGuard.DecideAsync or ContentCoreStore.ExportForUseAsync; run sources-demo.ps1 in another terminal; inspect scope/permission IDs and outcome, continue with F5, then Shift+F5 to stop all hosts.

Windows integrated HTTP and native API/web startup were verified for M03. Source UI served successfully and linked from Blazor. Interactive M03 UI clicks and a fresh M03 F5/breakpoint session were not executed; earlier M0 debugger evidence remains valid for the retained configuration. Hosted CI, cold-cache checkout, Azure SQL, real third-party permissions and production deployment were not verified. M04 has not started.
