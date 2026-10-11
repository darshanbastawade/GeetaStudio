# Architecture and implementation decisions

Record evidence-backed implementation decisions here. The entries below are accepted planning direction, not proof of working code.

| ID | Decision | Reason / implementation consequence |
|---|---|---|
| D001 | .NET 10 modular monolith | Keep local development and transactions manageable; API, web, and worker remain separately runnable hosts |
| D002 | Windows + PowerShell + VS Code first | A direct local workflow for the owner; use Linux tools only when an integration requires them |
| D003 | SQL Server persistence from M1 | Durable jobs and outbox require real database behavior; M2 expands domain schema |
| D004 | Provider ports and versioned routes | Change qualified adapters without editing business use cases; preserve stateful resource bindings |
| D005 | Characters are versioned data | Add recurring or situational guests while retaining audience, relationship, and asset consistency |
| D006 | Evidence before generation | Source rights and provenance must survive into scripts, media, review, and corrections |
| D007 | Sequential milestones, staged releases | Start with a runnable shell, then an internal reader, READ, WATCH, and later products |
| D008 | Three verification tiers | Offline tests, real-provider sandbox qualification, and production readiness are different claims |

For each new decision, record date, scope, alternatives actually considered, chosen behavior, affected milestones, migration implications, and evidence. Do not rename or replace healthy existing architecture solely to match suggested filenames.

## M0 implementation decisions — 10 October 2026

D001–D004 now have M0 structure/run evidence in [M00](milestones/M00.md). SQL Server, provider registry, and routing remain future M1 implementation, not completed infrastructure. D005–D008 retain their accepted planning meaning.

| ID | Scope / considered choices / chosen behavior | Consequence / evidence |
|---|---|---|
| D009 | ContentOS vs universe packs: reusable core libraries with future versioned universe data, rather than Geeta-specific domain switches | No cast/scripture seeds in M0; [architecture](ARCHITECTURE.md); M2+ introduce data. |
| D010 | Stable internal GUIDs rather than vendor IDs; UTC persisted instants with explicit locale/timezone presentation | No persistence yet. Health DTO uses DateTimeOffset.UtcNow; M1+ must preserve IDs and provider bindings. |
| D011 | Database-free M0 rather than simulated durable jobs | Empty Infrastructure boundary and idle worker; SQL Server is M1's prerequisite; no migrations. |
| D012 | Focused allowlist gate rather than speculative SDK wrappers | Core package/assembly/framework references and network endpoints rejected; actual injected Domain vendor reference triggers failure. |
| D013 | SDK 10.0.401/latestPatch and exact package versions with lock files | Uses installed stable SDK; only patches in the same feature band may roll forward; locked CI restore. |
| D014 | Coreclr direct-DLL debug launches with explicit Development environment and loopback URLs | Matches Development launch profiles; compound builds once using hidden host entries, while individual entries build themselves; stopAll verified with all three owned PIDs exiting; Windows breakpoint evidence recorded. |

These are fresh additive changes with no data migration or existing application compatibility change. Alternatives above address the actual empty repository; no legacy application is rewritten.

## M1 implementation decisions — 10 October 2026

| ID | Decision and actual alternative | Consequence / evidence |
|---|---|---|
| D015 | One local SQL-backed job transport, instead of an in-memory queue or new distributed engine | EF migrations, atomic outbox/inbox, fenced leases, contention/crash tests; M01. Future engine migration must reconcile checkpoints. |
| D016 | Framework Windows Negotiate for local staff plus explicit configured roles, rather than a development impersonation/header identity endpoint | Real integrated authentication ran; test identity handler is test-only; product roles cannot be inherited from Windows groups; mutation action header blocks cross-site forms. Production identity deployment remains a separate qualification. |
| D017 | Separate restricted SQL logins from the Windows migration identity | Private host user secrets; object/column grants; no runtime DDL/admin/delete. Script refuses to reset existing credentials or change server authentication mode. |
| D018 | Immutable route/profile revisions and operation-bound reconciliation, rather than automatic rerouting/retrying uncertain submissions | A→B changes new jobs; old snapshots/bindings retained; kill stops further calls; Unknown preserves reserved estimates until explicit reconcile. |
| D019 | Conservative probe-attempt budget units and metadata-only local telemetry, rather than guessed monetary prices or a new telemetry vendor | Atomic quota/reservation tests; no real pricing/provider usage qualification before M7; Activity/Meter/log boundaries remain portable. |
| D020 | Stable asset GUID plus persisted checksum/owner and local configured root, rather than storage URLs as identity | Owner-isolated reload/checksum tests; moving roots requires controlled byte/checksum reconciliation. No automatic cloud migration. |

M1's two additive migrations preserve the initial migration/history/jobs. No data-safe downgrade was demonstrated. Prior D014 debug conventions remain; the new SQL/demo tasks retain those launch paths.

## M02 decisions — 10 October 2026

| ID | Decision | Consequence/evidence |
|---|---|---|
| D021 | Separate core schema/context/history in existing SQL database | Module-owned typed relationships and additive migration; unchanged runtime migrations. Two contexts need explicit tool selection. |
| D022 | Database membership plus explicit universe grants; global staff role is not tenant authority | Query/command/export isolation tested; public access only current approved public revision; no automatic cross-tenant export. |
| D023 | SQL rowversion and immutable revision/event/operation records | Explicit 409 concurrency outcome; duplicate command IDs replay safely, different payloads conflict. |
| D024 | Data-driven v1 manifests/character foundations; non-destructive seed/import | Geeta and synthetic operations identical; aliases preserve legacy IDs/biography/history. No full M10 casting, source qualification or commercial entities yet. |

No new package/version change or live provider selected. Core/Runtime pending-model checks passed. See M02 evidence and Windows guide.

## M03 decisions — 10 October 2026

| ID | Decision | Consequence / evidence |
|---|---|---|
| D025 | Source schema within CoreDbContext rather than a separate distributed/context transaction | Serializable rights/export/revocation and revision lineage; additive M3Sources migration; runtime untouched. |
| D026 | Immutable edition/snapshot/permission terms, reviewed current permission and explicit use dimensions | Unknown/pending/expired/revoked restricted; every new edition starts unknown; attribution/checksum/evidence survive export. |
| D027 | Approved bounded text-file intake instead of a crawler/path importer | UTF-8, 64 KiB, SHA-256 validation; full retention grant or evidence-only record; no URL establishes permission. |
| D028 | Transitive typed content/asset lineage and manually evidenced remediation | New affected uses blocked; fixture/reported-existing publication ledger, no actual social publication/takedown; bounded impact and source transfer limits documented. |
| D029 | Narrow source SQL grants including SELECT/INSERT-only histories | Initial broad UPDATE proposal rejected before execution; final API histories cannot be updated/deleted, worker has no sources grants. |

M03 uses existing package versions; no external adapter was added. Source UI foundation and Windows HTTP passed; interactive M03 F5/click-through and production qualification remain unverified.

## M04 decisions — 10 October 2026

| ID | Decision | Consequence / evidence |
|---|---|---|
| D030 | Immutable versioned corpus inventory and exact source-backed components in CoreDbContext | Stable citation identity, transactional review/rights/replay; additive corpus schema, no worker permission. |
| D031 | 700 baseline with explicit 701-entry Besant edition variant | Chapter 13 opening question recorded separately; missing entries never manufactured or approved. |
| D032 | CC BY-SA Wikisource sample and explicit automated transcription review scope | 25 unchanged Sanskrit/English pairs, saved permission evidence and separate source editions; no claim of human manuscript review. |
| D033 | Exact UTF-8 hashes plus separately derived search normalization | No LLM gap filling/localized rewriting; changed readings need new immutable edition/manifest version. |
| D034 | Rights-aware staff reader and separately reviewed annotation kinds | Current revocation/language/territory checks; Concept/Interpretation/GeneratedParaphrase cannot overwrite quotation. |

M04 introduces no package or runtime vendor adapter; current sample permission only covers staff READ text in IN. Full inventory, human review and production qualification remain distinct from the accepted early reader/import foundation.

## Review correction decisions — 10 October 2026

| ID | Decision | Consequence |
|---|---|---|
| D035 | Recheck rights for cross-tenant core reads of all returned revisions | Explicit sharing is access authority, not a source-license override; owning staff retain evidence review access. |
| D036 | Metadata-only cross-tenant mutation/replay responses | Independent Write does not disclose history when Read is missing; authorized normal reads remain the text boundary. |
| D037 | Administrator-pinned immutable corpus bindings in a new additive table | Snapshot/edition, typed source coordinates and exact hashes prevent slot substitution; legacy unbound records withheld, retained and superseded by bound version 2. |

## M05 decisions — 10 October 2026

| ID | Decision | Consequence |
|---|---|---|
| D038 | Immutable claim versions/spans and versioned staff-review rubrics | Precise source/effective-version history; no LLM confidence or automatic philosophical authority. |
| D039 | Exact approved selected-edition corpus for quotation/translation; separate historical/news corroboration | Plausible citation is insufficient; staff must assess actual independence/reliability. |
| D040 | Explicit attributed disagreements and typed content segments | Conflicting readings retained; unlabeled assertions and type laundering fail output gates. |
| D041 | Conservative correction/supersession propagation through source ancestry | Old approvals remain historical, while current eligible use becomes false; no rewriting original evidence. |

## M06 decisions — 10 October 2026

| ID | Decision | Consequence |
|---|---|---|
| D042 | Reuse SQL for one production-intended bounded hybrid adapter and a separate fixture adapter | No second vector store/model download; stable owned source responses and explicit quality limits. |
| D043 | Local reviewed-concept projection with immutable corpus/graph/model/settings space IDs | Semantic aliases use approved data only; model changes create new indexes even at identical dimensions. |
| D044 | API-hosted durable asynchronous SQL index worker | Atomic builds/restart recovery without widening standalone M01 worker source permissions. |
| D045 | Human-labeled shadow evaluation plus serializable rowversion alias cutover/rollback | Failed labels, current rights or incompatible spaces cannot activate; failed revalidation blocks active use. |
| D046 | Full 25-verse regression after first real-sample relevance failure | Rarity-weighted lexical ranking and concept-supported semantics replace collision-driven/common-word ranking; expected labels unchanged. |

## M07 decisions — finalized 11 October 2026

| ID | Decision | Consequence |
|---|---|---|
| D047 | Reuse M01 provider controls with owned immutable AI profiles/task routes | No duplicate connection registry; new runs switch configuration while old snapshots remain pinned. |
| D048 | Reserve the full bounded fallback chain atomically in explicit USD/INR currency periods | Attempt costs and cached usage settle at pinned rates; no implicit currency conversion or reset of existing reservations. |
| D049 | Hold uncertain usage for authoritative reconciliation; hash rather than persist raw prompts | Cancellation/crash cannot silently charge zero or automatically resubmit; recovery is explicit and bounded by the original deadline. |
| D050 | Pin embedding route/profile before index space creation | Existing vectors remain compatible; a changed model needs a new index and controlled validation/cutover, never cross-space fallback. |
| D051 | Fixed-endpoint real text protocol plus independent offline fixtures; benchmark never changes routing | Offline results qualify control behavior only. Account/model policy, live pricing, quality and any real fallback require separate qualification. |

## M08 decisions — 11 October 2026

| ID | Decision | Consequence |
|---|---|---|
| D052 | Version explicit prompt/definition IDs and pin full input/route/profile snapshots | A new definition selects a new prompt for new runs; existing runs retain inspectable prior revisions and bindings. |
| D053 | Implement one closed deterministic five-step workflow with mandatory review | Model plans cannot loop, add tools or waive editorial review; no multi-agent engine dependency. |
| D054 | Store ordered leased checkpoints and fence completion/cancellation | Accepted effects are not repeated after restart; unknown AI usage stays bound to the original ledger operation. |
| D055 | Reuse API-hosted restricted background processing and transaction-aware M05 gates | Standalone M01 Worker privileges remain unchanged; nested transactions cannot detach workflow review state. |
| D056 | Restrict workflow retrieval to historical local embedding spaces and reserve one full AI fallback chain | Total cost/token limits remain enforceable; remote retrieval needs an explicit reviewed accounting extension. |
| D057 | Export owned checkpoints at safe boundaries; require drain or validated import for another engine | No automatic conversion of in-flight states or unsafe replay of accepted provider work. |
