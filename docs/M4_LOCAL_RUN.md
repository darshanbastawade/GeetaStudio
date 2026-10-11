# M04 local run and debug

Run in PowerShell from C:\dev\GeetaStudio. Reuse the local SQL Server and private API/worker user secrets provisioned by M1. No AI key, cloud account, Docker, WSL, Python or network source fetch is needed for the committed sample.

## Prepare and check

~~~powershell
Set-Location C:\dev\GeetaStudio
.\scripts\db-update.ps1 -ProvisionRuntime
.\scripts\verify.ps1 -Locked -Sql
~~~

ProvisionRuntime applies additive migrations to GeetaStudio_Dev, refreshes narrowly scoped object grants and seeds the existing fixture membership/locales. Existing credentials and records are preserved. Do not reset either database. The SQL acceptance database remains GeetaStudio_M1Tests. The workstation Windows identity needs the existing staff Administrator configuration and actual local tenant membership; see M1_LOCAL_RUN.md if setting up another workstation. Keep credentials in user secrets, never in source or the command output.

The locked check passed 48 offline and 50 real-SQL tests (98 total), with formatting clean and zero warnings/errors. SQL tests require the explicit -Sql profile and installed local SQL; no in-memory substitute is used.

## Run and load the sample

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

In a fourth terminal:

~~~powershell
.\scripts\corpus-demo.ps1
# Optional local acceptance report:
.\scripts\corpus-demo.ps1 -OutputPath .\.local\M04-demo.json
~~~

Windows PowerShell and PowerShell 7 are supported. If execution policy blocks scripts, use a process-scoped invocation: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\corpus-demo.ps1. Do not start terminal hosts while the F5 compound is already running.

The helper imports the committed, openly licensed Sanskrit and Annie Besant English sample, records its saved permission evidence, and submits explicit source-hash/locator review commands. It verifies exact hashes, idempotent import and the complete manifest report. Expected: baseline 700, edition inventory 701, imported 25, approved readable 25, missing 676, restricted 0 for this IN staff-text profile, AI calls 0. Rerunning keeps the manifest and source snapshot identities. It does not restore revoked permissions automatically.

Open http://localhost:5200 and choose the secured chapter reader, or open http://localhost:5100/staff/read directly. Use your Windows browser identity, select the local tenant, Geeta universe, Besant manifest, and chapter 1. Inspect Sanskrit, translation, citation/attribution/hash and the full coverage report. Chapters beyond the sample visibly lack imported text. Browser credentials are used directly; the Blazor server does not impersonate staff.

## Debug in VS Code

~~~powershell
code C:\dev\GeetaStudio
~~~

Use the already trusted folder and installed recommended C# extensions. Run and Debug → Geeta Studio: all → F5 builds once and starts API, Web and Worker. To populate the corpus during debugging, run Terminal → Run Task → Geeta Studio: M4 corpus sample, or run corpus-demo.ps1 in the integrated terminal.

Set an API breakpoint in CorpusEndpoints.MapCorpus, inside the GET chapter route; set an application breakpoint in CanonicalCorpus.ChapterAsync or ImportAsync. Reload the reader or rerun the sample to hit it. Use the call stack to follow authorization, rights and exact-text reads. Compound Stop stops all three hosts; free ports 5100/5200 before launching again.

The M0 Windows F5/breakpoint/compound Stop evidence remains valid historical evidence. M04 actual host startup, Windows-authenticated API demo and page/link HTTP checks passed. A new interactive M04 F5 session, browser clicks and visual/accessibility QA were not executed. These commands describe how to reproduce those remaining checks.

## Source and review scope

Selected material: Annie Besant's 1922 fourth edition, Wikisource Discourse 1 verses 1–25, pinned oldid 9204505. Local transcription reuse retains CC BY-SA 4.0 attribution and ShareAlike. See docs/fixtures/M04/README.md and source-evidence.json for saved source bytes, checksum and license evidence. Sanskrit and translation have separate language-specific source editions and permissions. The configured runtime profile only permits staff READ text in IN; commercial, adaptation, translation and AI-processing flags are false.

Approval here means the explicitly recorded automated transcription/hash/verse-locator review, not a new human scholarly manuscript or theological review. There are no invented verses, transliterations, word meanings or approved explanations. Footnote markers remain; full footnote presentation is outside this demonstration. The 700 baseline and chapter 13 opening-question variant are declared explicitly: edition 13.2–13.35 map to baseline 13.1–13.34. Only 25 of 701 edition locators contain text.

## Import, review and expand

Owning-tenant API root: /api/v1/corpus/tenants/{tenant}/universes/{universe}. GET / lists manifests. POST /manifests registers an immutable key/version/inventory. Under /manifests/{manifest}: POST /imports, GET /report?territory=IN, GET /chapters/{chapter}?territory=IN, POST /verses/{verse}/review, POST /verses/{verse}/annotations and POST /annotations/{annotation}/review.

POST requires X-ContentOS-Staff: 1, Windows staff role and actual tenant membership. Editors import/annotate; administrators approve/reject; readers inspect. Commands use operationId and payload replay; reviews use expectedRowVersion, reload after 409. Unknown/revoked/pending source rights hide text, while reports retain explicit restricted reasons. Pending or rejected corpus review never appears Approved.

Register independently reviewed source editions, retention/read permissions and exact UTF-8 snapshots through the M03 source registry, then import exact components referencing those snapshot IDs. Each input has Kind, Locale, ExactText, Sha256, SnapshotId and SourceLocator; canonical Sanskrit uses DirectQuotation, translation uses Translation. Optional Transliteration and WordMeaning remain distinct. Concept, Interpretation and GeneratedParaphrase use separate reviewed annotation records. Import never generates missing components.

Limits: manifest inventory at most 2,000 locators, chapters 1–18 and edition verses 1–200, batch at most 100 verses and ten components per verse, exact strings at most 16,000 characters. M03 snapshots remain at most 64 KiB. The HTTP body limit is 128 KiB: send compact JSON and smaller batches when needed. Lists/impact scans are bounded local foundations; qualify larger workloads before production. A changed canonical reading requires a new immutable manifest/source version, retaining previously cited records.

Corpus tables use the corpus schema and existing core migration history. API manifest/locator/text/command tables permit SELECT/INSERT; only verse/annotation review records additionally permit UPDATE. Neither DELETE nor DDL is granted; worker has no corpus access. Revocation impact includes corpus quotation/translation/annotation dependents, and every chapter/report rechecks current rights.

Hosted CI, cold-cache checkout, production/Azure SQL, production load, fresh interactive debugging and substantive human source review remain unverified. No external publication or new provider adapter was introduced. M05 has not started.

## M02–M04 review fixes

The review fixes add 20261010152946_M4SourceBindings. Run db-update.ps1 -ProvisionRuntime before starting the updated API. corpus-demo.ps1 now creates bound manifest **version 2**, retaining the old version 1 and its original 25 verses. Newer manifests appear first in the reader.

RegisterCorpus accepts optional Bindings; actual text imports require them. Each CorpusSourceBindingInput pins target Chapter/Verse/VariantKey, Kind/Locale, SnapshotId, SourceChapter/SourceVerse, Sha256 and SourceLocator. Source coordinates must match the declared edition coordinates; the separate BaselineVerse/variant fields retain alternate baseline numbering. A component/locale binds one source edition per manifest version. Bindings require tenant Administrator authorization, are immutable, and reference retained owned evidence. Their hashes/locators must match every imported component. Human review remains responsible for the declared source identity/authenticity; bindings are not automatic scholarly verification.

Legacy manifests without bindings remain stored and inspectable but their text is withheld as UnboundEvidence. Approval/new import is blocked. Register a new bound version from independently reviewed source data; do not infer trusted bindings from old potentially mislabeled imports or modify cited rows.

Shared core reads/listing now recheck rights for every returned revision under a serializable transaction. Owning staff can still inspect original evidence for review. Cross-tenant mutation/replay responses return metadata and an empty Revisions collection; use the normal authorized, rights-checked read endpoint for text. Read and Write grant flags remain independent.

The final regression suite includes four additional real-SQL cases covering these corrections; 98 total tests. Actual Windows version-2 sample setup passed twice with unchanged hashes, 25 readable pairs and 676 missing. Version-1 records, 10 jobs, 3 universes and 8 characters were retained. No fresh interactive F5 or human manuscript review is claimed.
