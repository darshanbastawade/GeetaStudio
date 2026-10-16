# ContentOS architecture — current private implementation

Prompt 7 adds only a narrow operator helper and integrated acceptance tests over existing modules. Distinct audiences use separate reviewed audience-bound factory/workflow plans, the same event/teaching, and exact cast/presentation/media snapshots. M7 remains the monetary ledger; M8 remains the workflow runtime; M13/M16/M17/M18 retain review, rights and export ownership. The helper never creates approvals or invokes publication/payment endpoints. No new entity, migration, provider SDK or engine. Fixture/live/human qualification stays explicit. [Evidence](milestones/evidence/M25.2-prompt7.md).

Prompt 6 keeps narration planning in Application/the existing WATCH workbench; it creates no provider work. Reviewed layout and exact script determine bounded segments, then original M16 jobs and M17 stages own execution/accounting. SAPI enumeration, COM execution and request/WAV receipts stay inside Integrations through the owned speech port. No engine/ledger/schema split or language fallback; genuine art/voice qualification remains pending. [Evidence](milestones/evidence/M25.2-prompt6.md).

The .NET 10 modular monolith has independently runnable API, server-interactive Blazor web and worker hosts. Separate CoreDbContext and RuntimeDbContext own SQL schemas and migration histories. Application owns typed ports; Infrastructure implements persistence and orchestration; Integrations owns approved protocols and adapters. M26 reuses the platform for historical content, and M27 adds guarded private experiments and automation. M22 realtime voice TALK remains missing; M21 supplies text TALK.

Later modules share M7 accounting, M8 workflows, M10 cast snapshots, M13 editorial lineage, M16 assets and M17 rendering. Stateful effects retain original provider/account/resource bindings, bounded budgets, cancellation/lease fences and unknown-outcome reconciliation. Current rights, audience, review, corrections, ownership and consent gate dispatch and returned content. Stored trend retrieval now validates exact pinned evidence under serializable projection without refreshing aliases, calling providers or rewriting snapshots. Prior TALK privacy, analytics timing and commerce refund/seat fixes remain in these boundaries.

Prompt 4 bridges M11 factory work to a unique immutable M8 child workflow and the original M7 AI operation. Factory admission, cancellation and current rights/cast checks share the existing workflow/factory/AI locks. Provider calls remain outside SQL transactions; batch spend/unknown exposure is projected from M7. The immutable plan pins prompt/route/profile/evidence/cast/schema, and a new reviewed plan is required for route changes. Exact source/interpretation claims plus a pending generated-fiction claim commit with the factory artifact using shared M05 validation; existing M13 imports the private draft and carries provenance into WATCH. One additive nullable binding migration preserves local-template history. [M25.2 evidence](milestones/M25.2.md) keeps later prompts, live qualification and human acceptance open.

Prompt 5 extends the existing trend source port asynchronously. Network discovery occurs outside SQL transactions; the existing scoped command lock rechecks configuration/permission before accepting metadata. The RSS integration pins a configured URL/permission binding, validates DNS at connection time and connects to a literal public IP with ordinary TLS. Candidate identity is independent of poll ID/local poll time; existing immutable candidate/operation/audit/remediation history handles reuse, meaningful versions and durable holds at capacity. Trend AI preparation uses the same factory and shows a pending placeholder until generation. No new schema, ledger, engine or publisher license is introduced; unconfigured live intake stays disabled. [Prompt 5 evidence](milestones/evidence/M25.2-prompt5.md).

Controlled implementation and offline verification do not establish human quality, live qualification or production release. M12 human-reviewed data and feature-specific release gates remain open. M0's database-free mode remains available; durable acceptance uses real SQL Server. The milestone sections below describe their original implementation scope; consult the [matrix](MILESTONE_MATRIX.md) and [project state](PROJECT_STATE.md) for current qualification.

| Project | Responsibility and allowed project dependencies |
|---|---|
| ContentOS.Domain | Domain invariants; no project/package references. |
| ContentOS.Contracts | Owned transport and snapshot contracts; no project/package references. |
| ContentOS.Application | Use cases and typed capability ports; Domain, Contracts. |
| ContentOS.Infrastructure | SQL Server persistence, rights/review fences, files and durable jobs; Application, Domain. |
| ContentOS.Integrations | Approved capability adapters and vendor/protocol mapping; Application, Contracts. |
| GeetaStudio.Api | Composition root and HTTP endpoints; Application, Contracts, Infrastructure, Integrations. |
| GeetaStudio.Web | UI and configured server-side HTTP client; Contracts. Never direct persistence access. |
| GeetaStudio.Worker | Composition root and hosted background lifetime; Application, Infrastructure, Integrations. |
| ContentOS.Foundation.Tests | Offline rule/API/architecture/security checks and explicitly opted-in real-SQL durability/regression tests; no live-provider qualification inferred. |

Core projects use only the .NET base framework and `Microsoft.NET.Sdk`. The gate forbids PackageReference, assembly Reference, FrameworkReference, incorrect SDK, reverse/lateral project dependencies, and literal HTTP(S) endpoints/network clients in Domain/Application/Contracts. Repository Directory.Build files cannot inject references. Hosts may use ASP.NET/hosting/HTTP framework behavior. Microsoft.Extensions.Hosting 10.0.12 belongs to the worker; Microsoft.AspNetCore.Mvc.Testing 10.0.12 belongs to tests. New exceptions require an explicit decision and gate change. This is a focused source/build configuration gate, not a general security analyzer; it does not prove the absence of arbitrarily obfuscated code.

External integrations follow: use case → owned capability port → policy/route selection → approved adapter. Vendor SDK types, model/voice IDs, authentication, endpoints, and protocol mapping stay in adapters or validated configuration. M1 introduces registry/routing and deterministic adapters; M0 does not implement them. Web-to-API health is first-party HTTP, not a vendor capability.

ContentOS owns reusable product logic. Universe packs supply versioned configuration, approved source/rights records, character data, and editorial policy. No Geeta characters, canonical texts, scripture fixtures, or invented sources are seeded in M0. No empty future module forest or generic plugin loader is introduced.

Internal identifiers will be stable GUIDs independent of external provider IDs. Persist instants in UTC (DateTimeOffset with zero offset); external resource bindings retain provider/account identity. M0 health uses `DateTimeOffset.UtcNow`. The shell displays that instant in the server's local timezone/current culture and labels it as such; user-selected locale presentation belongs to its later milestone. SQL Server is the selected persistence engine from M1; no in-memory database substitutes for its acceptance.

Development configuration binds HTTP to loopback and enables the fixture route only when the API environment is Development. Web's API address is configured in appsettings.Development.json / `Api__BaseAddress`; it is required and must use HTTPS outside Development. Non-development HTTP hosts retain HTTPS redirection (web also HSTS). M0 is not an authenticated public application or production deployment baseline.

Packages are exact-version references with committed NuGet lock files. CI restores locked dependencies, builds with warnings as errors, runs focused offline tests and the secret gate, then checks formatting. SDK selection is 10.0.401, roll-forward disabled, no prereleases; executable runtime minimum is 10.0.13. Repository text checkout uses LF attributes; CI retains scoped stage logs/TRX even on failure. Secret scanning detects known key/private-key patterns and nonempty secret assignments without printing values; it is a heuristic guard, not exhaustive credential detection. Keep local secrets outside source control and use .NET user secrets from the milestone that needs them.

Official API/configuration references reviewed on 10 October 2026:

- [Blazor .NET 10 render modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0)
- [SDK selection / global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [VS Code C# debugger settings](https://code.visualstudio.com/docs/csharp/debugger-settings)
- [VS Code compound debugging](https://code.visualstudio.com/docs/debugtest/debugging#_compound-launch-configurations)

Historical M0 setup: for existing installations, audit first; add missing boundaries without renaming healthy projects or touching IDs/data; establish passing offline checks; then add host configuration and reverify before a dependency milestone. This fresh repository had no implementation to migrate. M0 additions can be reviewed/removed independently of the retained planning baseline; no claim of database rollback is applicable.

## M1 extension — current runtime

The preceding M0 section is the historical foundation. M1 now implements runtime entities in Domain, owned DTOs in Contracts, typed runtime/configuration/secrets/assets/transport/export ports and RuntimeRunner in Application, EF Core SQL persistence and local operational services in Infrastructure, and two deterministic runtime.probe adapters in Integrations. Web still references only Contracts and calls API health; no SQL credentials or direct persistence were added to it.

Provider definitions/capability profiles/revisions are immutable. A job serializes its selected connection/profile/revision, capability and budget/workflow versions at creation; activation changes only future jobs. Secret values are resolved privately, never stored in snapshots, audit or exported provider views. Supported endpoints are fixed local test endpoints; there is no dynamic plugin/code loader or real vendor integration.

The SQL transaction creating a job also reserves budget and writes its outbox event. SQL dispatch records an inbox receipt and makes queued work visible atomically. Claim locks and per-connection locks limit competing workers. Every attempt receives a fencing token, trace and audit reference; heartbeat uses a separate DbContext. A provider submission is marked before calling the adapter. The deterministic effect ledger commits separately with the stable operation idempotency key. Expired submitted work reconciles through that original connection; stale completions cannot overwrite a newer lease. Unknown outcomes require operator reconciliation and retain budget reservations. This is a tested local protocol, not a claim of universal exactly-once vendor delivery.

Staff administration uses framework Windows Negotiate plus explicit product role mappings, endpoint policies, audit and a custom action header for mutations. Runtime SQL identities have no DDL/server-admin/delete permission; workers can update only circuit counters in provider configuration. Local secrets/asset paths live outside committed configuration. Production identity, certificate, secret-vault and external-adapter deployment remain unqualified.

Budget v1 measures conservative deterministic probe attempts, not currency. Activity traces, Meter counters and structured operational logs expose identifiers/state without payloads or credentials. UTC scheduling instants and explicit timezone names persist in SQL. See M1_LOCAL_RUN.md and milestones/M01.md for actual checks and limitations.

## M02 core boundaries

CoreDbContext owns the core schema and its migration history; RuntimeDbContext owns runtime without a shared universal entity table. They share the configured SQL database/host composition root. The worker does not need core grants. Migration tooling uses explicit context selection and the Windows migration identity; API credentials only have object-level runtime/core access.

CoreScope combines authenticated actor and selected tenant. Membership is verified independently from universe selection. Universes identify topics; content belongs to the owning tenant. Grants distinguish read/write/export; public knowledge only exposes the approved current public revision and cannot authorize mutation/export. Write transactions retain membership/grant reads, rowversion checks and operation replay; revisions/events/operations commit together.

UniversePack and character/profile/alias/audience/subject data are typed relational records. Fixture/import/export JSON is a validated transport representation. The seven Geeta names appear in fixture data, not shared-service branches. Episode cast/presentation revisions freeze identity bindings; M10 supplies full registry/casting operations. Core workflow/asset records are foundations, not a new engine/media qualification.

## M03 source and rights boundary

The source registry is a module in Infrastructure with typed Application ports and framework-free Domain rules. Source tables use the sources schema in CoreDbContext so content export, rights reads and revision/asset lineage can share serializable SQL transactions with revocation. Runtime persistence/migrations are unchanged; worker gets no source grants.

Source records belong to the owning tenant and universe. Edition keys/versions, immutable evidence snapshots and permission terms retain citation identity; new editions have no inherited permission. Reviewed current permission plus intended use, expiry, snapshot review and revocation determine rights. Metadata is not permission. Intake processes only supplied bounded UTF-8 text bytes; no remote fetch, configured code or arbitrary server path.

Rights checks sit in existing core export, approval and public-read boundaries, plus the typed use/explicit export ports. Revisions inherit dependencies; attached asset evidence participates in checks and impact traversal. Original self-declared Synthetic/Fiction with Owned/Synthetic status requires truthful staff classification. Future retrieval/generation/publishing adapters must preserve edges and call the rights guard; M03 does not implement those adapters.

Append-only decision/command/audit/use/revocation/edge records retain evidence. Current review/head/revocation state and remediation resolution are narrowly mutable. Work/edition takedowns expose impacted derivatives and tasks for published ledger records; resolution is manual evidence, never an automatic external action. See M03 evidence/runbook for tested limits and bounded local traversal.

## M04 canonical corpus boundary

ICanonicalCorpus owns manifest/import/review/report/chapter and separate annotation operations; Domain rules and owned Contracts remain framework-free. CoreDbContext adds corpus-schema tables so current source rights, review, immutable input evidence and revocation share local serializable transactions. Runtime and worker persistence are untouched.

An immutable edition inventory maps stable edition locators to optional baseline references and explicit variants. Exact source strings and SHA-256 are preserved; NFC/whitespace search text is derived separately. Import requires retained exact source membership, matching language/snapshot ownership and strict locators. Reviews carry evidence/rowversions. No normalization, generation or localization rewrites quotation.

Reader and report always combine corpus review and M03 current intended-use rights. Missing components are explicit; source-restricted text is withheld while citation/reasons remain inspectable. Concept/Interpretation/GeneratedParaphrase annotations are independently approved records. Staff chapter reader is an early internal READ foundation, not public publication or the M05 claim engine.

## M02–M04 review corrections

Shared core reads validate current rights for all returned revisions within serializable transactions; access grants cannot replace source permissions. Owning staff review access is separate. Cross-tenant mutation/replay responses contain metadata, never revision bodies, preserving independent Write and Read grants.

CorpusSourceBinding is immutable selected-manifest evidence tying each component to snapshot, typed source/target coordinates, exact UTF-8 checksum and locator. Administrator registration pins one source edition per component/locale. Import, approval, reader and report enforce the binding. Legacy unbound records stay intact but are restricted; a new bound version supersedes them without overwriting citations.

## M05 evidence boundary

IEvidenceEngine coordinates owned claim/rubric/history/segment/disagreement/correction operations in evidence-schema CoreDbContext tables. Source rights, immutable selected-edition corpus bindings, current claim versions and correction state are evaluated in the same serializable transactions as output approval/export/shared reads and fixture use. Quotes/translation require exact corpus-backed evidence; historical/news use a distinct exact-assertion corroboration rubric; interpretation retains attributed competing readings. EvidenceEngine never invokes an LLM or determines unique philosophical truth.

Approved metadata remains audit history; current eligibility is rechecked. Superseding claim versions and open corrections block dependent revisions and inherited parent chains. Labels and source citations survive export; evidence-bearing exports cannot be imported by dropping their provenance. Staff review UI exposes rubric criteria and history; global role, tenant membership and object SQL permissions remain distinct boundaries. M06 retrieval/index work is not included.

## M06 concepts and retrieval

KnowledgeService owns graph review, immutable index snapshots, durable reindex, current-rights evidence filtering and atomic alias cutover through Application-owned ports. Six knowledge-schema tables share CoreDbContext with corpus/source/claims for serializable decisions. Integrations contains the local reviewed-concept embedding provider and independently implemented SQL-hybrid/fixture ranking adapters; no domain/vendor coupling or extra vector database.

Indexes pin corpus material and reviewed graph, model/version, dimensions, normalization, locale and chunking. Query embedding settings derive from the alias's immutable index; equal dimensions cannot substitute another model. Stable chunks bind exact approved selected-edition source IDs; current rights/access and claim corrections are checked before returning evidence, including source-snapshot spans and attribution. Shadow labels gate activation/rollback; later failed validation blocks use. The API-hosted background indexing processor intentionally keeps standalone runtime-worker SQL permissions unchanged. Local semantics are curated one-hop features, not neural or automated doctrinal truth.

## M07 AI routing and accounting

Application owns typed generation/streaming/embedding ports and canonical result/usage contracts. Integrations map independent local fixtures and the fixed-endpoint OpenAI text protocol; vendor abstractions stay inside integrations. Hosts compose these with the SQL-owned router.

Immutable tenant profiles and route revisions pin task, provider/model, prompt/policy/pricing versions and source IDs per run. Route heads affect new runs; shared M01 connection enabled/kill/concurrency/circuit controls still apply to pinned attempts. Current membership and source permissions are checked on generation and output inspection, with attribution retained. Generated drafts never become canonical sources or approved publications automatically.

A tenant transaction/application lock reserves the entire bounded fallback chain against per-run and tenant/capability/currency/period limits before submission. Attempts persist before provider calls; remote calls run outside transactions. Known usage settles at pinned rates including cached input and charged failures. Unknown usage retains its ceiling until authoritative reconciliation. Deadline recovery releases unsubmitted allowance and marks admitted interrupted attempts unknown without resubmitting stored prompts; raw prompts are not persisted.

Streaming deltas remain provisional until the final schema/citation validation, and fallback cannot splice providers after a delta. Every fallback satisfies the original policy/features; refusals, quota, unknown usage and cancellation stop the chain.

New controlled embedding indexes pin route/profile IDs into their immutable embedding space and account for corpus and query usage. Existing M06 spaces remain intact; provider changes require a new index, validation and explicit alias transition. No fallback mixes embedding spaces. Standalone M01 Worker permissions are unchanged; no M08 workflow runtime was added.

## M08 owned deterministic workflow runtime

PromptRevision and WorkflowDefinition are immutable tenant/universe artifacts. DurableWorkflow pins their full settings, declared canonical inputs and M07 route/profile versions; ordered WorkflowCheckpoint rows own independent operation IDs, lease tokens, accepted results and original AI bindings. WorkflowDecision is append-only. All five tables are additive in the workflow schema.

IWorkflowRuntime/IWorkflowWorker define the replaceable engine boundary. The implemented owned-sequence.v1 processor executes retrieve → plan → draft → check → review; evidence-required misses branch to a visible failure, original-fiction definitions may proceed without source evidence, and every successful draft waits for an explicit staff review decision. No model controls the step graph, route, caller identity or review transition.

IToolInvoker is a closed typed registry of evidence.retrieve.v1 and ai.generate.v1. Source ownership/current rights and bounded untrusted data are checked at the workflow boundary as well as in the deployed tools. No arbitrary HTTP endpoint, shell or MCP dispatch exists. Future protocols may transport these owned schemas without adding authority.

Serializable checkpoint claims and fencing serialize acceptance, not remote calls. Original deadlines, bounded safe-read recovery attempts and pinned token/currency/cost ceilings remain authoritative. One draft operation uses M07's full fallback reservation/usage ledger. Recovery reuses the original settled AI operation or blocks for reconciliation; it never invents zero usage or resubmits an unknown effect. Cancellation fences unfinished checkpoints while retaining admitted AI accounting.

The M05 transaction-aware claim gate and M03 current source rights guard are reused at draft/check/review and inspection. Results may be withheld after revocation. Unlike M07 operational prompt hashes, bounded canonical workflow inputs/evidence/results are persisted for recovery behind staff membership; operational logs contain no raw input or provider response.

WorkflowProcessor runs in the API host using its existing restricted content/source/AI identity, alongside M06 indexing. The separately runnable M01 Worker keeps runtime-only permissions. workflow-checkpoint.v1 can be exported only at paused/terminal boundaries; engine replacement must drain or validate/import compatible checkpoints while preserving accepted operation IDs and provider bindings. No generic importer or third-party agent runtime was added.
