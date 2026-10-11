# M05 local run and debug

Run in PowerShell from `C:\dev\GeetaStudio`. Uses the existing .NET 10 SDK, local SQL Server, Windows staff identity and private user secrets. No paid account or AI key is needed.

## Prepare and verify

```powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql
```

The first command adds migrations/grants and seeds the current Windows account in the local tenant. It preserves databases, existing IDs, source evidence and configured login passwords. Never use EnsureDeleted or drop/recreate these databases. Tests use GeetaStudio_M1Tests; the application uses GeetaStudio_Dev.

## Run

Use three PowerShell terminals:

```powershell
dotnet run --project .\src\GeetaStudio.Api --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --launch-profile Development
```

Run one command per terminal. Open http://localhost:5200 and follow Evidence review, or open http://localhost:5100/staff/evidence directly. Windows Integrated Authentication must use your provisioned account. M05 stores and reviews claims in the API; it adds no worker job or external publishing adapter.

With the API running, use another terminal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence-demo.ps1
```

The demo creates distinctly original fictional sources in the synthetic universe. It retains two opposing attributed viewpoints, rejects a quotation claiming canonical authority and a one-source historical assertion, exports labels/citations, copies evidence to an unchanged-body revision, requests a correction, identifies both affected revisions, blocks stale export and creates a reviewed superseding claim with retained history. New run keys deliberately preserve previous acceptance records. It neither grants rights to real scripture nor publishes externally.

## Debug in VS Code

Open C:\dev\GeetaStudio as the trusted folder. With .NET/C# debugging extensions from LOCAL_DEVELOPMENT.md installed, stop terminal hosts so ports 5100/5200 are free. Select **Geeta Studio: all** and press F5; the build task launches API, web and worker with Development profiles. Use **Geeta Studio: Api** for API-only debugging.

Set breakpoints in src/ContentOS.Infrastructure/EvidenceEngine.cs at SubmitAsync, ReviewAsync, CheckContentAsync or CorrectAsync. Run **Tasks: Run Task → Geeta Studio: M5 evidence demo** while debugging, or submit/review a claim at /staff/evidence. Inspect claim version, exact span, rubric outcome and eligibility reason. Shift+F5 stops the compound. M0's actual F5 evidence is retained; a fresh interactive M05 breakpoint session is not claimed.

## Editorial operations and payloads

Base API path: /api/v1/evidence/tenants/{tenantId}/universes/{universeId}. All routes require authenticated staff and owning tenant membership. Reads are for Reader/Editor/Administrator members; submission/link/correction needs Editor or Administrator membership plus StaffOperate; rubric registration, review and disagreement needs Administrator. Staff POST requests require X-ContentOS-Staff: 1. A global Windows role never authorizes another tenant.

- GET /rubrics: read immutable review criteria/version/independent-source minimum.
- POST /rubrics: operationId, key, version, description, minimumIndependentSources (2–10). Describe reviewed quality criteria and source independence explicitly. Same key/version cannot be rewritten.
- POST /claims: operationId, key, expectedRowVersion (empty for a new claim), type, label, locale, statement, author, tradition, uncertainty, rubricId, evidence array; optional correctionId when superseding the same claim. Use current ClaimView.rowVersion for subsequent versions.
- Evidence spans: snapshotId, start, length, exactText, sha256, locator, relation (Supporting/Contradicting), optional corpusTextId. Offsets count UTF-16 characters, SHA-256 uses UTF-8. Retained same-universe text must match precisely; offsets cannot split a Unicode surrogate pair.
- POST /revisions/{revisionId}/review: expectedRowVersion of the claim revision, status Approved/Rejected, quality DirectSource/Attributed/Corroborated/Insufficient, outcome Supported/Contested/InsufficientEvidence, evidenceReference beginning manual:, evidence: or fixture:. Pending review is frozen after approval/rejection; corrections create a new version.
- POST /links: operationId, claimRevisionId, contentRevisionId, start, length. Link the current Draft/Review body with matching locale/exact statement. Segments cannot overlap. Cover every non-whitespace body character.
- POST /disagreements: operationId, leftRevisionId, rightRevisionId, reason, evidenceReference. Both must be reviewed attributed interpretations. GET /disagreements retains their identities separately.
- POST /corrections: operationId, claimRevisionId, reason, evidenceReference. GET /revisions/{revisionId}/impact returns direct/inherited content revisions and correction history.
- POST /content/{contentRevisionId}/evaluate: purpose Read/Export/Publish, language, territory, format, commercial/adaptation/translation/aiProcessing flags. Returns eligible, reason, labels. This staff diagnostic does not authorize public publication.
- GET / lists up to 100 recent claims with immutable versions; GET /claims/{claimId} retrieves a selected history.

Labels/types: Quotation, Translation, Interpretation, HistoricalClaim, NewsClaim, ModernAnalogy, FictionalScene, Suggestion. Exact readings require SourceExact uncertainty; fiction requires Fictional. Interpretation requires author/tradition. Limited, Contested and Unverified remain explicit uncertainty values. Practical analogies/suggestions never become historical predictions merely through their label.

Quotation/translation approval requires an exact approved M04 CorpusText ID, same snapshot/locale/kind/locator and immutable selected-edition binding. Fetch text IDs through the corpus chapter API. No invented text or merely plausible URL qualifies. Historical/news approval uses a separate bounded rule: Corroborated/Supported, at least the rubric's distinct source-work count, exact matching supporting assertions and no contradicting spans. Staff must establish actual independence, timeliness and reliability; distinct IDs or repeated assertions alone do not establish truth. Interpretation approval requires supporting source evidence and an attributed Supported/Contested outcome; disagreements are retained, not settled automatically.

Core Approved transitions, shared reads/listings, core export and source-use fixture publication ledger recheck claims and current rights. Owned staff reads retain draft/history access for review. Explicit Synthetic/Fiction content without claim links remains labeled as such; claiming factual/scripture content requires evidence. Exports include versioned labels, citation and provenance; imports cannot discard an exported evidence bundle to create untraceable copies.

## Corrections and legacy content

A correction request immediately blocks eligible use of the affected claim and descendants. Creating any new claim version makes old versions stale, even while the replacement is pending. Supersession resolves the selected correction without rewriting the earlier assertion. Historical Approved metadata is retained for audit, but eligibility is rechecked and can become false; approval metadata alone does not permit export/publication.

Legacy Canonical/Translation/Interpretation/CurrentEvent content remains in the database and available to owning staff. It gains no automatic evidence approval: bind exact reviewed claims to draft revisions before approval/export/shared access. If an old approved item requires correction, revise it into Draft and supply reviewed evidence. Changed bodies require new claim links; unchanged bodies preserve pinned links and parent provenance. Old source ancestry remains conservatively restricted: to replace a stale source-derived chain, create a fresh reviewed item rather than laundering the ancestry.

Local foundation bounds: 16,000-character claims, 20 evidence spans per version, 100 recent claims in listing, 100 ancestry nodes and 10,000 impact edges. Exceeding bounds fails explicitly. No semantic inference, automated philosophical authority, LLM confidence, crawling, retrieval index or freshness monitor is supplied. Corpus coverage remains 25/701 with 676 missing; rights can restrict real M04 sample exports by format/adaptation independently of claim review.

## Verification limits

Automated SQL/API and actual Windows-authenticated host/demo results are recorded in milestones/M05.md. Fresh interactive F5, browser click-through/visual/accessibility checks, substantive human scholarly/source-independence review, hosted CI/cold-cache setup, Azure SQL and production restore/load/security qualification require separate checks. No M06 implementation is included.
