# Geeta Studio — revised M0–M27 execution roadmap

Version: 2.1 · Prepared for Darshan Bastawade · Updated 10 October 2026

Geeta Studio is the first product built on a reusable ContentOS. Preserve WATCH, READ, TALK and CREATE; trustworthy scripture and interpretation; age-appropriate learning; trend-to-wisdom content; a dynamic character system initially containing Dhairy, Neerja, Dadaji, Shakuni, Tara, Kabir and Meera; multilingual delivery; social distribution; and revenue intelligence. Future universes include Shivaji Maharaj, Sambhaji Maharaj and Jain teachings.

Revision 2.1 incorporates the user's acceptance of the proposed cast and requirement to introduce new characters whenever a situation needs them. These seven are initial records, not a fixed maximum or a requirement that all appear at launch. Use Neerja as the current display name and preserve Neeraja as an alias for the same stable character identity. This revision also specifies the full trending-video/news/event-to-original-video workflow. It changes the roadmap, not the implementation status of the project.

Review basis: the milestone mapping and requirements recovered from prior conversations, especially the October 3 M0–M27 version. The linked conversation could not be fetched directly. This revision is a proposed implementation specification, not a verbatim copy of the original prompts or a verification of the current repository. No implementation-completion claims are made.

## 1. What changes in this revision

| Area | Improvement | Where it is implemented |
|---|---|---|
| External dependencies | A provider registry, typed capability contracts, versioned routes, tests and migration rules cover all external APIs and tools, including self-hosted engines. | M0–M1 foundation; M6–M8 AI/retrieval; every integration milestone |
| Security sequencing | Staff authorization, secrets, audit, data isolation and endpoint restrictions start immediately. Public, child and commercial features have their own release gates. | M0–M1 and each release; M25 verifies and hardens |
| Cost discipline | Reserve budget before work; account for every attempt; reconcile estimates against actual costs. | M1, M7; reporting in M19 |
| Reliability | Durable execution, leases, outbox/inbox, reconciliation and explicit unknown outcomes prevent duplicate posts and charges. | M1, M8, M18, M20 |
| Knowledge integrity | Scripture, translations, scholarly interpretations, historical claims, current news and generated fiction retain separate identities and evidence. | M3–M6 |
| Product validation | A thin, internal READ demonstration appears by M4. M14 is the first public product gate; reaching M27 is not required to validate demand. | M4, M14, release gates |
| Evaluation | Model, prompt, voice and provider changes require capability-specific evidence. Tests with fakes and live-provider qualification have separate status. | M7, M12, M25, M27 |
| Reuse | One approved source bundle can create several audience, language and format variants; shared facts remain linked and corrections propagate. | M9–M17 |
| Revenue | Keep revenue source separate from acquisition channel; distinguish estimates, recognized amounts and cash receipts. | M19–M24 |
| Launch scope | Keep all 28 milestones but stage the releases. Add integrations when used, with one live adapter and an independent test adapter first. | All milestones |
| Existing work | Audit and extend completed work with evidence. Preserve valid migrations, content IDs and public URLs. | M0 and every prompt |
| Dynamic characters | Store characters, roles, relationships, audience eligibility, visual/voice profiles and episode casting as versioned data. Add recurring or situation-specific guests without business-code changes. | M2, M9–M13, M16–M17 |
| Trend-to-video workflow | Discover videos/news/events, validate evidence, find a defensible teaching connection, select a cast and generate an original reviewed episode with correction lineage. | M15 using M3–M13, M16–M19 |

The earlier plan already included provenance, provider abstractions, evaluation and reusable universes. This revision makes their implementation and completion criteria more explicit. It does not assume those features were absent from the prior discussion or the code.

## 2. Architecture and provider-switching contract

Retain the agreed .NET 10/C#, ASP.NET Core, EF Core, SQL Server/Azure SQL, Blazor and Worker Service direction. Start as a modular monolith with separately runnable API/web and worker hosts. Use Docker for local supporting tools where needed. Keep Azure deployment as an implementation option behind appropriate infrastructure boundaries.

Suggested logical structure; adapt it to the existing repository instead of renaming healthy code:

| Project or module | Responsibility |
|---|---|
| `ContentOS.Domain` | Universes, sources, claims, content, editorial rules, product and entitlement invariants. No vendor SDKs. |
| `ContentOS.Contracts` | Versioned application DTOs, events and external-operation envelopes. |
| `ContentOS.Application` | Use cases, typed provider ports, routing requirements, workflow definitions and authorization policies. |
| `ContentOS.Infrastructure` | EF Core persistence, durable jobs, outbox/inbox, local files, audit and operational services. |
| `ContentOS.Integrations` | Provider adapters, SDK mappings, approved tool runners and contract validation. Organize by capability; split projects only when useful. |
| `GeetaStudio.Api`, `GeetaStudio.Web`, `GeetaStudio.Worker` | Composition roots and thin delivery layers. |
| `UniversePacks` | Versioned Geeta/Shivaji configuration, knowledge schemas, terminology, character and editorial rules. |
| `tests` | Domain, integration, adapter contract, workflow, evaluation and relevant UI tests. |

A module may be a folder initially. Do not create a microservice, repository wrapper or plugin framework for every class.

### 2.1 The rule for every external API or tool

Application use case → typed capability port → policy/routing service → approved adapter → external service or tool.

Business code asks for a capability such as text generation or video rendering. It never constructs a vendor client, uses a vendor model name, hardcodes an external URL, or accepts a vendor response object. The adapter owns authentication, SDK translation, API-version details and provider error mapping. Business rules remain in ContentOS.

The provider system has distinct concepts:

1. **Adapter definition:** deployed and reviewed code, contract version and supported operations.
2. **Provider connection:** a configured account/endpoint/region plus a secret reference. This is scoped to environment and, where relevant, tenant.
3. **Capability profile:** model, voice, renderer or service settings; supported languages, limits, modalities and verified features.
4. **Route revision:** the primary connection/profile and explicitly permitted fallbacks for a named task.
5. **Execution snapshot:** immutable route, prompt, workflow and relevant policy versions selected for a run.
6. **External resource binding:** the originating provider/account and external ID for an existing subscription, post, media job, conversation session or stored object.

Changing a default route affects newly created runs. Existing runs keep their snapshots. A security kill switch can stop a pinned provider immediately; it must not silently reroute already submitted side effects. Rebinding unstarted work is an explicit, audited operation.

### 2.2 Coverage of provider ports

Implement a port when its milestone first needs it. M1 defines the registry conventions; it does not build every future adapter.

| Capability | Example owned contract | First implementation | Special requirement |
|---|---|---|---|
| Text generation | `ITextGenerationProvider` | M7 | Structured output, streaming, input limits and tool-call support are capability flags. |
| Embeddings | `IEmbeddingProvider` | M6, fully routed in M7 | Version the embedding space; corpus and query embeddings must match. |
| Retrieval/search/reranking | `IKnowledgeRetriever`, `IRerankingProvider` | M6 | Return internal source/chunk IDs and evidence, not only opaque scores. |
| Translation | `ITranslationProvider` | M9 | Preserve names, terminology and citations; Sanskrit source text is immutable. |
| Content assessment | `IContentAssessmentProvider` | M12 | An external classifier supplements owned deterministic and editorial policies. |
| Workflow runtime | `IWorkflowRuntime` | M8 | Own run/step records and exportable checkpoints. Engine migration is controlled. |
| External agent tools | `IToolInvoker` plus typed operation schemas | M8 | Allowlisted actions and least-privilege credentials; no arbitrary tool execution. |
| Trends, RSS, news and discovery | `ITrendSource` | M15 | Track source time, provenance, usage rights and source-specific coverage. |
| Web extraction | `IWebContentExtractor` | M15 | Use permitted sources, restrict destinations and preserve evidence snapshots where allowed. |
| Image generation | `IImageGenerationProvider` | M16 | Style/character references, usage rights and unsupported-feature detection. |
| Speech synthesis | `ISpeechSynthesisProvider` | M16 | Voice IDs and languages are provider-bound; fallback needs voice qualification. |
| Transcription | `ITranscriptionProvider` | M17/M22 | Word timestamps, language and confidence are explicit capabilities. |
| Video generation/rendering | `IVideoGenerationProvider`, `IVideoRenderer` | M17 | Generative video and deterministic rendering are distinct capabilities. |
| Subtitles/alignment | `ISubtitleProvider` | M17 | Timing, script, font and line-break contracts. |
| Music/licensed media | `IMediaSourceProvider` | M16/M17 | No asset is usable without the recorded license and allowed uses. |
| Storage/delivery | `IAssetStore`, `IAssetDeliveryProvider` | M1/M16 | Stable internal asset IDs; locations and delivery URLs are replaceable mappings. |
| Jobs/messaging | `IJobTransport` | M1 | Durable local state stays authoritative; use leases and outbox/inbox. |
| Secrets/config | `ISecretResolver`, `IProviderConfigurationStore` | M1 | Resolve secrets just in time; never expose them in exported configuration. |
| Social publishing | `ISocialPublisher` | M18 | Route is platform + account + operation; platforms are destinations, not interchangeable fallbacks. |
| Channel analytics | `IChannelAnalyticsProvider` | M19 | Version metric definitions and preserve source coverage/missingness. |
| Notifications | `INotificationProvider` | M18/M20 | Consent, destination identity, deduplication and delivery receipts. |
| Identity | `IIdentityProvider` with standards-based host integration | M1/M20 | Internal user IDs remain stable; linking external subjects requires proof. |
| Payments | `IPaymentProvider` | M20 | Existing subscriptions, charges and refunds remain bound to their originating provider. |
| Commerce/fulfillment | `ICommerceProvider`, `IFulfillmentProvider` | M24 | Reconcile external order IDs; never resubmit an uncertain order blindly. |
| Telemetry/export | `ITelemetryExporter` or standard OpenTelemetry exporters | M1/M19 | Keep operational event schemas independent of the analytics vendor. |

`Microsoft.Extensions.AI` supplies shared chat and embedding abstractions. They can be used inside the AI adapter layer where they fit; ContentOS still owns task routing, policy, cost and product contracts. Do not build wrappers that merely duplicate stable framework functionality. See sources S1–S2.

### 2.3 What “change provider at any time” actually means

| Change | Supported behavior |
|---|---|
| Switch between deployed, qualified text/translation/speech adapters | Activate a validated route revision; new runs use it without business-code edits. |
| Add a previously unsupported vendor or external tool | Implement its adapter and contract tests, deploy it, register credentials, qualify it, then activate a route. |
| Switch embedding model | Build a new versioned index with matching query embedding settings, validate it, switch an index alias atomically, keep the prior index for rollback. |
| Move asset storage | Copy, checksum, reconcile references, then change location mappings; retain the old store until migration is verified. |
| Switch queue/workflow engine | Drain or reconcile in-flight work and migrate compatible checkpoints; preserve business operation IDs. |
| Change payment gateway | Route new purchases to the new gateway; service old subscriptions/refunds through the original binding until a supported migration is completed. |
| Change identity provider | Link or migrate identities with verified ownership; a setting change alone must not join or overwrite accounts. |
| Change social-publishing vendor | Preserve platform/account destination and existing publication bindings; validate that the new adapter supports that operation. |
| Change a live voice-session provider | End/reconnect at a defined boundary; retain only consented context. Do not splice incompatible sessions silently. |
| Change relational database engine | Perform a schema/data/behavior migration. EF Core does not make engines interchangeable by a configuration flag. |

Capability equivalence, licenses, language quality, data handling and provider availability must be tested. Portability is an architectural property; it is not a promise that providers produce identical outputs.

### 2.4 Illustrative route configuration

This is a proposed ContentOS configuration shape, not a configuration accepted by any vendor SDK. `text-a`, `text-b`, model references and secret references below are illustrative placeholders. Production validation rejects unresolved placeholders and unqualified profiles.

```json
{
  "schemaVersion": 1,
  "revision": "routes-0007",
  "providers": {
    "text-a": {
      "adapterKey": "installed.text.adapter-a",
      "connectionRef": "connections/text-a",
      "secretRef": "secrets/text-a",
      "profileRef": "profiles/draft-model-a"
    },
    "text-b": {
      "adapterKey": "installed.text.adapter-b",
      "connectionRef": "connections/text-b",
      "secretRef": "secrets/text-b",
      "profileRef": "profiles/draft-model-b"
    }
  },
  "routes": {
    "content.draft": {
      "capability": "text.generate",
      "primary": "text-a",
      "fallbacks": ["text-b"],
      "requiredFeatures": ["structured-output"],
      "allowedLocales": ["mr-IN", "hi-IN", "en-IN"],
      "contentPolicyRef": "policies/editorial-standard",
      "dataPolicyRef": "policies/public-source-content",
      "budgetPolicyRef": "budgets/draft-content",
      "timeoutPolicyRef": "timeouts/interactive-draft",
      "maxTotalAttempts": 2,
      "fallbackOn": ["unavailable", "rate-limited"],
      "unknownOutcomeAction": "reconcile",
      "snapshotScope": "workflow-run"
    }
  }
}
```

A route selects a connection/profile only after checking operation, locale, consent/data policy, region, remaining deadline, budget and required features. Fallback must satisfy the same checks. An unavailable child-safe route cannot fall back to a route whose data policy permits different handling of child data. Cache keys include relevant provider/profile, prompt, corpus/index, policy, locale and scope versions.

Administrative operations: list providers and capabilities; validate a connection; view credential status without revealing secrets; test a route; compare evaluation results and costs; activate or roll back a revision; disable a provider; inspect failures and original resource bindings. Start with secured API/CLI operations in M1 and expose them in the editorial administration UI at M13.

### 2.5 Failure, budget and audit rules

- Normalize errors into authentication/configuration, invalid input, unsupported capability, rate limit, unavailable, policy rejection, quota exhausted, timeout/unknown outcome and terminal provider failure.
- Do not retry authentication errors, invalid requests or policy rejections automatically. Use bounded, jittered retries for safe transient operations and respect provider backoff. Count attempts across SDK, transport and workflow layers; avoid retry multiplication.
- A timeout after submitting a post, payment, generation job or order can mean the provider accepted it. Record `Unknown` and reconcile through the original binding before another submission. Outbox/inbox and idempotency help; they do not create universal exactly-once delivery.
- Persist business operation ID, attempt ID, provider connection/profile, external request ID, route revision, start/end time, normalized outcome and usage. Redact credentials and personal content.
- Reserve budget atomically before work. Each fallback attempt consumes the same overall operation budget. Finalize or release reservations from confirmed usage; retain unresolved estimates for reconciliation instead of marking them zero.
- Cost estimates have units, currency, pricing-version/effective-date and exchange-rate provenance. Cloud expenses, AI/media usage and customer credits are separate ledgers.
- Use provider-specific deadlines, circuit breakers, concurrency limits and job priorities. Cancel remote jobs where supported and account for charges that still occur.
- Treat source pages and tool responses as untrusted data. They cannot change system instructions, obtain secrets, select arbitrary endpoints or authorize publishing/payments.

Microsoft documents retry controls for unsafe HTTP methods; this design therefore uses operation-aware retry policy instead of a blanket retry setting. See S3.

## 3. Common Codex instruction — prepend to every milestone

```text
You are implementing Geeta Studio on reusable ContentOS. Read AGENTS.md,
the current solution, migrations, tests, docs and milestone evidence first.
Use this revised roadmap as the target. Implement ONLY the selected milestone
and the smallest prerequisite gaps necessary for it. Preserve valid existing
work; do not recreate the repository, reset data or renumber completed work.

Keep the agreed .NET 10 modular-monolith direction, adapting names to the repo.
Use canonical owned DTOs and typed ports for every external API/tool. Vendor
SDKs, model IDs, endpoints and provider-specific options belong in adapters or
validated configuration. Do not expose secrets, execute arbitrary configured
code or assume an unavailable credential exists.

Implement the milestone's application behavior, persistence/migrations,
relevant API/UI, permissions, audit, telemetry, meaningful tests, deterministic
fixtures and operator documentation. Test authorization and data isolation for
new boundaries, idempotency for side effects, and the relevant provider-switch
behavior. CI uses controlled fixtures; live calls use explicit integration
profiles, configured credentials and budget limits.

Keep canonical sources, interpretations and generated content distinct. Enforce
rights and lineage at generation/export/publish boundaries. Never fabricate
source text, evidence, completed tests, credentials, external IDs or live status.
Use configurable languages, age profiles, universes and publishing channels.

For each adapter, record official documentation URL, verification date, API/SDK
version, permissions, supported capabilities, limits, licensing and data policy.
Use pinned compatible dependencies. External open-source tools are replaceable
adapters, not owners of our domain, workflow history, content or entitlements.

Update docs/milestones/Mxx.md and the dependency/coverage matrix. Report:
implemented behavior; files and migrations; commands actually run and results;
provider-switch evidence; local demo; unresolved live prerequisites; and known
limits. Separate Offline Verified, Live Sandbox Verified and Production Ready.
Do not claim a milestone is production-ready using mocks alone.

Do not deploy publicly, charge customers or publish to social accounts merely
to prove implementation. Provide dry-run/sandbox behavior and identify release
prerequisites. Work already explicitly authorized by the project owner may
proceed within that authorization.
```

### Definition of done applying to all milestones

The stated user workflow works; relevant acceptance criteria have reproducible evidence; data migrations preserve IDs and constraints; external calls pass through the configured adapter boundary; failures and cancellations have visible outcomes; the appropriate rights, authorization and budget rules are enforced; and the runbook explains setup and recovery. Mark blocked live verification clearly without discarding completed offline work.

Do not impose unrelated test suites on small documentation/configuration edits. Use risk-focused tests at the boundaries described below.

## 4. Milestone execution specifications

Execute M0 through M27 in order by default. Add a small prerequisite where the current code proves it is missing; record the reason. Independent feature releases are defined after M27. The completion gate for each feature is evidence-based, not merely the presence of files or interfaces.

### M0 — Engineering Foundation and Existing-Work Audit

**Depends on:** none.

**Codex task**

```text
Implement M0. Establish or audit the .NET 10 modular monolith, preserve existing
work, and document the module and external-integration boundaries. Create the
smallest repeatable local/CI development workflow and milestone evidence map.
```

**Deliverables**

- Inspect the existing solution and map each old milestone to implemented, partial, missing or unverified evidence. Record the commit reviewed and avoid declaring completeness from README statements.
- Establish API, Blazor web, worker, domain/application, persistence and integration boundaries; dependency management, formatting/analyzers, API contracts, test projects and local run instructions for VS Code.
- Record architecture decisions for modular monolith, ContentOS versus universe packs, SQL Server, adapter boundaries, identifiers, UTC persistence and locale-aware presentation.
- Create an external-dependency register listing capability, current provider/tool, authentication, account ownership, data sent, costs, license, supported operations and migration difficulty.
- Configure development-only fixtures, secret exclusion/scanning, CI build and focused tests. Keep paid/external integration tests opt-in with budgeted profiles.
- Add architectural checks that domain/application product logic cannot reference vendor SDKs or hardcoded provider endpoints. Document permitted framework dependencies.

**Acceptance**

- A fresh checkout starts the documented hosts and passes the initial build/test pipeline using fixtures without paid credentials.
- A deliberately introduced vendor dependency in the domain layer is detected by the architecture gate.
- The milestone map distinguishes code evidence from planned functionality and includes a reversible implementation order for existing installations.

**Scope boundary:** no mass repository rewrite, new distributed services, cloud rollout or speculative implementation of all adapters.

### M1 — Runtime, Provider Configuration and Observability

**Depends on:** M0.

**Codex task**

```text
Implement M1. Build durable job execution, secured configuration, provider
registration/routing and operational telemetry. Demonstrate a configuration
switch between two test adapters while preserving in-flight execution history.
```

**Deliverables**

- Add provider definitions, connections, capability profiles, route revisions, validation, activation/rollback and immutable execution snapshots. Resolve secrets from environment/local development secrets initially through `ISecretResolver`.
- Provide authorized admin APIs/CLI for test, enable, disable, activate and inspect operations. Introduce staff roles, authorization policies, audit, least-privilege runtime identities and restricted provider endpoints now.
- Persist `Job`, `JobAttempt`, lease/heartbeat, scheduled time, cancellation and failure reason; support safe recovery, bounded retries, dead-letter handling and manual reconciliation. Add transactional outbox/inbox support with local SQL-backed dispatch.
- Introduce `ExternalOperation` and attempt/resource bindings, normalized errors, request deadlines, concurrency limits, circuit-breaker state and kill switches. Implement initial budget policy/reservation plumbing used by M7.
- Add `IAssetStore` with local development storage, job transport and telemetry exporter boundaries; stable asset IDs must not embed storage URLs.
- Use structured logs, traces, metrics, health/readiness and redaction. Persist UTC and an explicit timezone for schedules, including Asia/Kolkata as a configurable display/scheduling choice.

**Acceptance**

- Route A → B changes the next run without rebuilding; a run already created retains its snapshot; a kill switch stops further prohibited calls.
- Two workers contend for one job, one worker crashes and execution resumes without a duplicated business effect. An uncertain external result is reconciled rather than blindly resubmitted.
- Invalid configuration, missing capabilities, unavailable secrets and unauthorized admin actions fail clearly without secret leakage. Every attempt has a trace and audit reference.

**Scope boundary:** one working durable transport initially; runtime engine and queue migrations remain controlled operational procedures.

### M2 — Universal Core Domain and Database

**Depends on:** M0–M1.

**Codex task**

```text
Implement M2. Model reusable ContentOS entities and module-owned persistence.
Keep tenant security scope separate from content universe and introduce stable
IDs, versioning, concurrency and exportable domain contracts.
```

**Deliverables**

- Define organization/tenant, universe, locale, audience profile, subject/concept, content item/revision, asset reference, workflow run and editorial-state contracts. Add later commercial entities only when their milestones need them.
- Establish module-owned schemas/migrations, uniqueness constraints, row-version concurrency, timestamps and business operation IDs. Avoid a universal untyped JSON table for core relationships.
- Model a `UniversePack` manifest with schema version, terminology, subject types, source rules, audience/character references and editorial policies. Seed Geeta and a minimal synthetic second-universe fixture.
- Establish stable `CharacterId`, character revision/alias, relationship, presentation-profile and episode-cast contracts for the registry implemented in M10. Character names and cast lists are data, never enums or branches in shared services; retain prior IDs when normalizing Neeraja to the display name Neerja.
- Keep ownership and permissions explicit: a universe is a topic boundary, not a tenant/security boundary. Deliberately shared public knowledge requires an explicit access model.
- Version external API DTOs and domain events; validate imports/exports and support non-destructive seed reruns. Preserve legacy IDs when extending existing data.

**Acceptance**

- Two universe fixtures use the same ContentOS operations without Geeta-specific branching in shared services.
- Cross-tenant reads/writes and exports are rejected unless explicitly authorized; filtering is enforced in queries and commands, not only in the UI.
- Migration/seed reruns preserve IDs and records; conflicting updates produce an explicit concurrency outcome.

**Scope boundary:** keep SQL Server/Azure SQL persistence. Database-engine portability needs a tested migration, not an unverified connection-string toggle.

### M3 — Source Registry, Rights and Provenance

**Depends on:** M2.

**Codex task**

```text
Implement M3. Establish the source-of-truth registry and machine-readable rights
checks that all later retrieval, generation, export and publishing must use.
```

**Deliverables**

- Model source work, edition, author/translator, publisher, locator, imported snapshot, checksum, retrieval/import time, citation format and review status. Store a permitted evidence reference when retaining a full snapshot is disallowed.
- Model licenses/permissions with allowed languages, territories, formats, commercial use, adaptation/translation, AI-processing use, attribution, expiry and revocation. Unknown rights remain an explicit restricted state.
- Provide approved-file ingestion and source-registration endpoints/UI foundations, deduplication and a review queue. A URL alone does not establish permission to reuse its text.
- Add `RightsDecision` and provenance edges from evidence to generated content/assets. Evaluate rights for the intended use, rather than a single global `IsLicensed` flag.
- Track takedown/revocation requests and impacted derivatives. Export citations and provenance with content where appropriate.

**Acceptance**

- An unknown-rights source cannot be commercially published; an attribution-required source carries the required attribution through a sample export.
- Re-importing the same source is idempotent; changing the edition creates a new version rather than replacing cited evidence invisibly.
- Revoking a source identifies dependent content and blocks new affected uses; existing published items become visible remediation tasks.

**Scope boundary:** no unrestricted crawler and no assumption that a public-domain original makes a modern translation, narration or image freely reusable.

### M4 — Geeta Canonical Knowledge Corpus

**Depends on:** M3.

**Codex task**

```text
Implement M4. Import a reviewed Geeta corpus with stable verse locators, edition
provenance and licensed translations. Build a thin internal READ demonstration
using real approved material to validate the source-to-reader path early.
```

**Deliverables**

- Define the selected edition manifest, chapter/verse locators, Sanskrit, transliteration, translations, word meanings and citation references. Treat 700 verses as the baseline corpus target; explicitly represent edition variants instead of manufacturing missing records.
- Store exact source text and source hashes separately from search normalization and display formatting. Never use an LLM to fill canonical text gaps.
- Add idempotent import validation for duplicates, missing locators, chapter boundaries, Unicode, source attribution and translation availability/rights.
- Attach concepts and approved explanations as separate records. Distinguish direct quotation, translation, interpretation and generated paraphrase in the API and presentation.
- Deliver a simple staff-only chapter/verse page with source details for an initial reviewed sample of about 20–30 verses; expand to the declared corpus inventory through the same importer.

**Acceptance**

- A corpus report reconciles the manifest's expected records with imported records and lists every missing/variant/restricted entry; no placeholder verse is marked verified.
- A reviewer can trace displayed Sanskrit and translation to the correct edition and locator; source text is unchanged by normalization or localization.
- The internal reader shows meaningful approved material and can run without an AI provider being available.

**Scope boundary:** full generated commentary and full multilingual coverage are later content work; visibly report their actual coverage.

### M5 — Claim, Evidence and Interpretation Engine

**Depends on:** M3–M4.

**Codex task**

```text
Implement M5. Separate verifiable source statements, translations, doctrinal
interpretations, historical/news claims, fictional narratives and practical
applications. Make uncertainty, disagreement and source changes reviewable.
```

**Deliverables**

- Model claim type, evidence span/locator, supporting and contradicting sources, interpretation tradition/author, uncertainty, reviewer and effective version.
- Define content labels for quotation, translation, interpretation, modern analogy, fictional scene and suggestion. A practical lesson is not a statement that the scripture predicted a modern event.
- Record evidence quality and provenance through reviewable rubrics; do not treat LLM-generated confidence or model consensus as truth.
- Support disagreement between interpretations and bounded “insufficient evidence” responses. Historical universes use event/claim schemas without inheriting scripture-specific certainty rules.
- Introduce correction requests, supersession and impact lookup so source/claim updates can invalidate downstream approval eligibility.

**Acceptance**

- A fixture with conflicting interpretations retains both attributed viewpoints and does not silently merge them into a canonical fact.
- An invented quotation or unsupported historical assertion cannot pass the evidence gate merely because a model supplies a plausible citation.
- Updating an evidence record identifies affected content revisions and makes stale evidence status visible.

**Scope boundary:** this is an evidence and editorial system, not an automated authority that determines which philosophical interpretation is uniquely correct.

### M6 — Concept Graph and Portable Knowledge Retrieval

**Depends on:** M4–M5; M1 integration conventions.

**Codex task**

```text
Implement M6. Build concept linking and hybrid retrieval behind owned contracts.
Return verifiable evidence and make index/model changes safe through versioned
embedding spaces, compatibility validation and controlled index cutover.
```

**Deliverables**

- Model concepts, aliases, translations and reviewed relationships with provenance; avoid automatically making every generated relationship an approved edge.
- Implement lexical search and a configured semantic retrieval path behind `IKnowledgeRetriever`; return stable source/chunk IDs, evidence spans, edition, rights/access filters and retrieval metadata.
- Isolate embedding and optional reranking providers. Chunk deterministically and record corpus version, chunking policy, embedding model/version, dimensions, normalization and index version.
- Define an `EmbeddingSpaceId`; enforce corpus/query compatibility. Changing to a different embedding model creates a new index even when vector dimensions happen to match.
- Add asynchronous reindex, shadow evaluation, atomic index-alias activation and rollback. Remove or filter revoked/deleted content promptly; enforce scope/rights filters before returning evidence.
- Introduce a small, human-labeled retrieval fixture with known relevant verses and no-answer questions. M12 expands the evaluation set.

**Acceptance**

- Known questions retrieve expected evidence; a no-match question returns no supported match rather than invented verse IDs.
- A route to an incompatible embedding space is rejected. A new index can be built, validated, activated and rolled back without mixing spaces.
- Swapping a retrieval adapter preserves the owned response contract; cross-tenant/restricted-source evidence is absent from results and citations.

**Scope boundary:** one production-intended retrieval adapter plus an independent deterministic test adapter initially. Do not operate several vector databases merely to demonstrate architectural flexibility. See S4 for the corpus/query embedding requirement.

### M7 — AI Providers, Task Router and Cost Ledger

**Depends on:** M1, M3, M6.

**Codex task**

```text
Implement M7. Connect AI generation and embedding routes to the provider control
plane, capability checks, budget enforcement and usage accounting. Keep task
profiles independent of vendor model names and prove configuration-based routing.
```

**Deliverables**

- Define task profiles such as draft, summarize, translate, evaluate and answer-with-evidence; resolve their model/version/options from qualified provider profiles. Keep model-specific tuning inside validated adapter settings.
- Implement structured generation, streaming where required, cancellation and usage reporting through typed contracts; reuse suitable stable `Microsoft.Extensions.AI` abstractions internally.
- Provide one real configured text adapter and a behaviorally independent test adapter; prepare a second real adapter when needed for live fallback. Record capability qualification instead of assuming all compatible endpoints support identical features.
- Complete budget reservation/settlement, limits per run/tenant/capability/period, currency-aware cost estimates, pricing versions, actual usage, cache-hit accounting and unresolved-usage reconciliation.
- Enforce data classification, region, retention/processing restrictions and policy compatibility across the whole fallback chain. Never retry a policy refusal through a more permissive provider.
- Add a benchmark command that reports quality, latency, availability and cost for an approved fixture without automatically changing production routing.

**Acceptance**

- Configuring a different qualified adapter changes the chosen provider without application changes; schema failures, quota exhaustion and unsupported features produce normalized outcomes.
- Concurrent runs cannot overspend the configured reservation limit; retry/fallback costs are included; unknown usage is visible rather than silently zero.
- Every completed output records task/profile/provider/model and route versions. No secret or raw sensitive prompt appears in operational telemetry.

**Scope boundary:** model prices and names are maintained configuration. Mock success proves routing behavior; only live qualification supports a claim that a particular real fallback works.

### M8 — Prompt Registry and Durable Workflow Runtime

**Depends on:** M1, M5–M7.

**Codex task**

```text
Implement M8. Store prompts and workflow definitions as versioned, testable
artifacts, and execute bounded workflows with owned state and controlled tools.
Allow orchestration-engine replacement without putting business rules in it.
```

**Deliverables**

- Model prompt versions, inputs, output schemas, locale/audience bindings, evidence requirements, workflow/step definitions, retry policy, approvals and run snapshots.
- Implement a deterministic sequence/branch runtime first: retrieve → plan → draft → check → review. Bound steps, tokens, time and total budget; keep AI planning subordinate to the workflow contract.
- Add durable checkpoints, step leases, pause/resume, cancellation, recovery and idempotent step completion. Persist canonical inputs/results and external-operation bindings outside any third-party agent engine.
- Provide `IWorkflowRuntime` and a capability-limited tool registry. HTTP, MCP or other tool protocols are transport adapters; tool schemas, caller permissions and allowed side effects remain application-owned.
- Validate untrusted retrieval/tool output as data. Prevent instructions in a source page from changing routes, invoking tools or bypassing editorial review.
- Document how an alternative workflow engine could import compatible checkpoints or drain existing runs. A third-party runtime may be added after its value is established.

**Acceptance**

- A run pauses for review, survives a worker restart and resumes from the correct step without repeating an accepted external effect.
- Invalid model JSON, looping plans and instruction-bearing source text are rejected or bounded with visible failure states.
- A prompt update affects new runs only; the prior prompt, evidence and route versions remain inspectable for an old run.

**Scope boundary:** do not introduce multi-agent coordination for tasks solved by one deterministic workflow and bounded model calls.

### M9 — Audience, Pedagogy and Localization Engine

**Depends on:** M4–M8.

**Codex task**

```text
Implement M9. Adapt a shared verified evidence bundle into configurable audience
and language variants while preserving meaning, citations and learning goals.
Keep child-facing data and content policies explicit.
```

**Deliverables**

- Version audience profiles for the previously discussed 8–14 and 15–23 examples and an adult profile. Store reading level, vocabulary, length, examples, tone, sensitive topics, interaction limits and learning goals as editable rules.
- Add editable writing subprofiles for 15–18 and 19–23, with configurable eligible characters, preferred lead/supporting roles, setting, conflict complexity and humor. Audience writing profiles are separate from legal/consent age rules. M9 defines the selection requirements; M10 resolves actual registry characters.
- Keep fictional character identity and canonical life stage consistent across audience variants. Select another suitable protagonist when needed; an explicit alternate timeline/version is required to depict the same character at a substantially different age.
- Define a lesson structure: situation, source anchor, interpretation, relatable example, practical action and reflection/check-for-understanding. Preserve distinctions between story and source.
- Add locale profiles, terminology glossary, transliteration/pronunciation rules and translation memory through `ITranslationProvider`. Suggested initial delivery locales are Marathi, Hindi and English; these are proposed seeds, not a recovered fixed launch-language decision.
- Store localized/adapted variants as linked revisions with their own review status. Canonical Sanskrit is retrieved, not regenerated or translated over itself.
- Apply conservative child defaults: minimal collected data, no unnecessary persistent conversational profile, no advertising-oriented behavioral profiling and age-appropriate output constraints. Household consent flows arrive before child personalization in M20.

**Acceptance**

- One approved verse/evidence bundle produces visibly different 8–14 and 15–23 variants while preserving its cited meaning and attribution.
- Changing age bands or locales changes configuration rather than conditional business code; an unsupported locale cannot be silently labeled as supported.
- A newly activated eligible character becomes available to audience casting without rebuilding the application. No profile requires Dadaji, Shakuni or any other named character to appear in every episode.
- Reviewed sample translations preserve names, verse locators and key concepts; unsuitable age examples fail the policy gate.

**Scope boundary:** initially publish only locales and age profiles that have passed human review. Do not claim complete multilingual quality from translation-provider availability alone.

### M10 — Universe Bible, Characters and Humor Governance

**Depends on:** M5, M8–M9.

**Codex task**

```text
Implement M10. Build a dynamic character registry and situation-aware casting
service. Seed Dhairy, Neerja, Dadaji, Shakuni, Tara, Kabir and Meera; allow editors
to introduce any suitable recurring or episode-only character through data and
configuration. Version the universe bible, relationships, continuity and humor
rules without hardcoding names or requiring a code deployment for a new cast.
```

**Deliverables**

- Seed the seven accepted characters with the personality direction in section 10. Keep Dhairy/Neerja as child leads, Tara as the initial teen lead, Kabir as the initial young-adult lead, Meera as an adult lead, Dadaji as a mentor and Shakuni as a fictional trickster. These are editable defaults, not exclusive role assignments.
- Implement character/alias/revision, relationship, arc state, approved presentation profile, audience eligibility, situation/skill tags and lifecycle status. Support core, recurring, guest and episode-only characters; scope episode-only records to their episode rather than automatically making them recurring canon.
- Provide register, edit, validate, activate, retire, duplicate-as-distinct-character and promote-guest operations. A new editor-created character can be used in a draft immediately; publishing still requires the same profile, asset and content checks as other cast members.
- Implement a casting service that accepts the situation, audience, locale, learning objective, roles needed, continuity constraints and budget; returns eligible candidates and reasons; supports editor overrides/pinned choices; and proposes a draft guest when no existing character fits. Store every selected character as a stable registry reference before production.
- Snapshot character revisions, cast roles, presentation variants and relationships in each episode. Later registry edits do not rewrite completed episodes. Retiring a character prevents new default selection while preserving historical references; explicit reissues create a new episode revision.
- Separate fictional character profiles from real people. A familiar name alone does not authorize use of a real person's likeness, voice or private history.
- Define humor styles and forbidden treatments relevant to the project: ridicule of disability, demeaning religious communities, jokes about real suffering and presenting fictional scenes as scripture/history.
- Treat “Shakuni-style” temptation or flawed reasoning as an attributed fictional storytelling device where appropriate; distinguish characters from Geeta canonical speakers and source claims.
- Support approved coach/guru/adviser presentations of the same fictional Shakuni through presentation profiles, with stable identity and voice/visual anchors. Give every protagonist agency; Shakuni's presence does not remove responsibility for their choices.
- Store episode continuity, reviewed story beats, visual references and character/pronunciation guides for M16–M17.
- Add audience-sensitive satire labels and editorial escalation for real people/current events. Humor can be toned down or disabled per universe, audience and subject.

**Acceptance**

- Sample stories preserve approved character traits and clearly label fictional dialogue; no generated quote is attributed to scripture.
- The same content engine renders a different universe bible from configuration and reviewed assets.
- An editor introduces an unseen guest (for example a coach), gives the profile audience/situation tags and activates it; the next suitable brief can cast that guest without application changes. The editor can also deliberately select an eligible new character without waiting for an automated suggestion.
- A guest can be promoted to recurring without losing identity; a retired or incompatible profile is not silently selected; past episode manifests retain their original character revisions.
- High-risk humor examples are blocked or routed to human review; a model cannot override those decisions with its own confidence score.

**Scope boundary:** detailed visual production starts in M16. Adding ordinary characters must be data/configuration work. New animation mechanisms or unsupported media capabilities can still require implementation. Prefer two to four speaking characters in a short as an editable production guideline, not a hard platform limit.

### M11 — Evergreen Content Planning and Draft Factory

**Depends on:** M4–M10.

**Codex task**

```text
Implement M11. Turn approved evidence into a traceable content plan and draft
packages across READ, WATCH and learning formats. Add coverage and duplication
controls before expanding output volume.
```

**Deliverables**

- Model content briefs, curriculum/journey position, source anchors, concepts, learning goal, audience, locale, format, estimated cost and editorial priority.
- Resolve the lead, supporting characters, optional mentor/trickster and required guest roles through M10; store selection reasons and an approved cast snapshot. Use this same brief/casting service for M15 trend-driven content to avoid maintaining a second story engine.
- Create a coverage matrix for chapter/verse/concept/audience/locale/format; distinguish planned, generated, reviewed and published coverage.
- Produce a canonical evidence bundle and linked drafts for explanation, story, comic script, short-video script, carousel and reflection exercise. Regenerate only changed derivatives when appropriate.
- Add semantic/template duplication checks, topic fatigue indicators and scheduling priorities; reuse approved material without repeatedly charging to rediscover it.
- Show expected generation/media/review effort and budget before batch execution. Batch generation is cancellable, resumable and bounded.
- Include one chronological Geeta learning journey and a small practical-theme journey as reviewed pilot content.

**Acceptance**

- A batch for a small set of approved verses creates drafts with complete source and variant lineage, obeys the budget and resumes after interruption.
- Repeating the same approved business request reuses or versions its result intentionally; it does not create duplicate content records invisibly.
- A coverage dashboard accurately distinguishes draft inventory from publicly usable content and reports rejected duplicates.
- A new registry guest can fill a situation-specific role in a brief, while an existing cast is reused when it already fits; invalid relationships, life stages and unapproved visual substitutions are flagged.

**Scope boundary:** optimize a useful pilot before generating hundreds of episodes. Do not equate generated inventory with product demand.

### M12 — Evaluation System and Golden Dataset

**Depends on:** M5–M11.

**Codex task**

```text
Implement M12. Build a versioned quality evaluation system covering grounding,
learning value, language, safety, character continuity and provider compatibility.
Separate human judgments, deterministic checks and model-assisted evaluations.
```

**Deliverables**

- Start with at least 100 reviewed evaluation cases plus at least 20 held-out cases. Cover real/missing citations, conflicting interpretations, translation, age adaptation, humor, adversarial source text, no-answer queries and minority/edge topics. Numbers are proposed initial gates and can be revised with rationale.
- Track expected evidence, acceptable answer properties, review rubric, severity, locale, audience and dataset version. Keep evaluation cases separate from prompt-tuning examples and refresh them over time.
- Implement deterministic citation/rights/schema checks plus human rubrics for faithful meaning, usefulness, readability and age suitability. LLM-as-judge output is advisory and versioned, not the sole authority.
- Compare candidate prompt/provider/retrieval configurations on quality, failure rates, latency and cost. Identify which capabilities and languages each adapter has actually passed.
- Add cast and trend fixtures covering an unseen guest, guest promotion, Neerja/Neeraja alias resolution, retired characters, wrong voice/life stage, cross-episode continuity, no suitable verse match, an unavailable video transcript, and multiple outlets repeating one unverified claim. Assess character appeal and understanding with age-appropriate human feedback.
- Define hard gates: zero fabricated verse IDs in the tested set, no critical rights/privacy/child-safety failure, all required citations resolvable, and human acceptance for the release sample. Choose numerical retrieval and latency targets from the measured baseline and record them.

**Acceptance**

- A deliberately injected fabricated citation, rights violation or high-risk audience failure blocks release of the candidate configuration.
- Reports separate deterministic, human and model-based results, show sample sizes and retain the baseline/candidate versions.
- Provider-switch tests validate schema/meaning expectations without expecting identical wording; held-out cases are not used to optimize the same release repeatedly.
- A context-inappropriate cast, fabricated event claim, forced scripture connection or missing required media evidence cannot pass simply because the generated story is fluent or entertaining.

**Scope boundary:** passing a finite dataset supports the tested release conditions; it does not establish universal factual correctness or safety.

### M13 — Content Lineage, Versioning and Editorial CMS

**Depends on:** M3–M12.

**Codex task**

```text
Implement M13. Give editors a usable review workbench with complete lineage,
version-bound approvals, rights/cost evidence and correction workflows. Expose
the provider administration features established in M1 without leaking secrets.
```

**Deliverables**

- Implement brief → draft → evaluated → review → approved → scheduled → published → corrected/withdrawn states, with explicit rejection and failure paths.
- Show evidence, competing interpretations, generated differences, audience/locale variants, model/prompt/route versions, assets, costs and evaluation findings together in the editor workbench.
- Bind approval to the exact content revision, asset manifest and policy/evidence versions. Material edits invalidate prior approval; publication rechecks current rights and access.
- Provide author/editor/publisher/admin permissions, optimistic concurrency and audited actions. High-risk content retains human approval even if low-risk automation is enabled later.
- Add lineage navigation and correction propagation across articles, translations, episodes, exports and posts. Record external takedown/edit attempts and unresolved platform limitations.
- Add route configuration UI for validation, qualification evidence, activation/rollback and emergency disable; mask secrets and restrict administrative scope.
- Add the character-registry and casting workbench: create a guest from a brief, edit profiles and aliases, review preview assets, override casting, promote/retire characters, and inspect usage/continuity. Include a trend-candidate review queue with evidence, teaching match, freshness, estimated cost and a previewable cast/script.

**Acceptance**

- Editing approved text or swapping an approved audio/video asset invalidates its approval before publishing.
- A source correction identifies all affected derivatives; an editor can resolve or withdraw them and see which external copies still need action.
- Unauthorized staff cannot approve/publish or alter provider routes. Every public-intended artifact can be traced to source and production history.

**Scope boundary:** prioritize a usable editorial workflow over a general-purpose no-code CMS or workflow designer.

### M14 — READ Factory and Public Web MVP

**Depends on:** M4, M6, M9, M11–M13; public-release baseline below.

**Codex task**

```text
Implement M14. Deliver the first public Geeta Studio experience using approved
content: browse, search, read and follow a learning journey. Complete the public
release controls now rather than deferring them until M25.
```

**Deliverables**

- Build responsive Blazor pages for home, universe, chapter, verse, concept, search and learning journey, with stable URLs, server-delivered indexable content where appropriate, metadata and sitemap.
- Display licensed Sanskrit/transliteration/translation, clearly labeled explanation, age variant, practical takeaway, citations, related items and previous/next navigation. Show language coverage and missing reviewed content honestly.
- Support a chronological journey over the selected corpus. Provide local-device bookmarks/progress initially; add server-synced household progress in M20.
- Include keyboard access, legible Indic scripts, screen-reader semantics, contrast, mobile layouts and a low-bandwidth reading path. Avoid requiring generated media to read a verse.
- Instrument minimal, versioned content/engagement events with documented consent/retention choices; reserve account-level tracking for authorized flows.
- Establish release procedures: staff authentication, no public access to drafts/admin APIs, rights review, required privacy/child policy review, backups with restore evidence, error monitoring, abuse limits and rollback.

**Acceptance**

- A visitor can find a verse, choose an available audience/language variant, inspect its sources and continue a learning journey on a phone.
- Unapproved content is absent from public APIs, sitemaps, caches and search; removing approval removes public eligibility.
- The release sample passes M12, an accessibility/mobile check and a restore/rollback exercise. Missing required gate evidence blocks public rollout, not internal testing.

**Scope boundary:** ship a small reviewed collection first, for example 25 READ pieces and one journey. These are proposed pilot targets, not a requirement to generate the full corpus before launch.

### M15 — Trend-to-Wisdom Engine

**Depends on:** M3, M5–M13; M14 for public distribution.

**Codex task**

```text
Implement M15. Turn permitted trending-video, news and event signals into
verified trend briefs, defensible teaching connections and original audience-
specific story packages. Use the dynamic cast from M10 and shared planning from
M11. Keep discovery, factual evidence, interpretation and fiction distinct; allow
no-match/reject outcomes and expose the process in the editorial workbench.
```

**Deliverables**

- Integrate trend/news/RSS/discovery and extraction through separate provider ports; deduplicate related stories and distinguish original reporting from repeated copies of the same report.
- Add permitted video metadata/search/chart feeds, licensed news feeds, publisher RSS, approved event calendars and manual URL/topic submissions through configured sources. Record what each connector can actually access; there is no assumed universal Instagram/Facebook/X trending feed or universal transcript downloader.
- Normalize `TrendCandidate` and `TrendCluster` records with provider/source identifiers, category, region/language, event/publication/observation times, metric snapshots, evidence-access status and rights. Compute momentum only from available permitted observations; missing metrics remain missing. Manual submissions enter the same verification process.
- Capture event time, publication/update time, retrieval time, source attribution, evidence permissions, geographic context and expiry/recheck policy.
- Fetch article/video substance only through permitted access, licensed media, owned content or authorized uploads/transcripts. A title, thumbnail or popularity signal does not establish what happened. The official YouTube captions download endpoint requires permission to edit that video (S6); unsupported access is reported, never bypassed.
- Verify the central claim against primary material where available and appropriate independent reporting; record uncertainty and copied-source relationships. Popularity and a high model score cannot turn an allegation or edited clip into a verified fact.
- Rank eligible candidates by configurable freshness/momentum, audience/locale fit, evidence quality, teaching relevance, novelty and cost. Rights, safety, evidence and sensitivity rules are hard eligibility gates rather than weights that virality can outweigh.
- Build candidate concept links with evidence, a plain-language rationale, alternatives, risk level and an explicit “No suitable Geeta connection” result.
- Select an explicit format: sourced event explainer, clearly labeled original fictional story inspired by a verified theme, or calendar/event-themed lesson. Use M6 retrieval and M5 interpretation records; preserve both the news-evidence and scripture-evidence chains. Do not recycle an unsupported allegation as recognizable fiction.
- Produce a versioned `TrendStoryBrief` with evidence bundle, checked-at/expiry time, learning goal, source/verse references, adaptation rationale, audience variants, selected cast/guest proposals, original hook/script, channel targets and estimated production cost. Review the brief/script before paid production by default; M16–M17 produce media and M13/M18 handle final approval/publication.
- Keep factual summary, uncertain/alleged claims, analogy, scripture interpretation and fictional humor in separate fields. Avoid presenting a verse as endorsing a person or predicting an event.
- Route sensitive real-person, violence, tragedy, political, religious-conflict and child-facing material for human review. Use age/humor rules from M9–M10.
- Add correction, stale-story and withdrawal handling; source retractions trigger affected-content review. Track acceptance/rejection rates and costs.
- Recheck time-sensitive claims before the scheduled publish operation. Persist lineage from trend cluster and source versions through teaching, character/cast revisions, script, assets, final episode and external posts; feed discovery-to-publication time, production cost and audience outcomes to M19.

**Acceptance**

- Fixtures include a useful mapping, forced/unrelated mapping, stale story and contradicted report; only suitable reviewed items proceed.
- Replacing a trend or extractor adapter preserves the canonical story/evidence contract while reporting different coverage.
- A retracted or corrected story invalidates dependent publication eligibility and creates a visible remediation task.
- One verified trend produces at least two original age-appropriate story packages with distinct suitable casts and consistent evidence; a necessary new guest can be registered and selected without rebuilding the application.
- An inaccessible transcript is marked unavailable; the system uses other legitimate evidence or holds/rejects the candidate. It does not invent what the video says. A manual submission passes the same factual/rights checks as automatically discovered trends.
- A stale or corrected claim blocks an already scheduled episode until reviewed; multiple syndicated copies do not falsely count as independent corroboration.

**Scope boundary:** no scraping of inaccessible sources and no automated publication of high-risk trend content. These are product/editorial requirements carried forward from the original plan.

### M16 — Media Asset Library and Character Visual Consistency

**Depends on:** M3, M7, M9–M10, M13.

**Codex task**

```text
Implement M16. Build a portable asset library and approved character references
for images, voices, music and other media. Keep asset identity, licensing and
provenance independent of the generating vendor or storage location.
```

**Deliverables**

- Model asset versions, source/generation lineage, license/consent, media type, dimensions/duration, checksum, accessibility description, character reference and review status.
- Create approved character sheets, style/palette rules, poses, scene references and voice/pronunciation profiles. Record generated deviations for editorial review.
- Bind approved visual/voice assets to character and presentation-profile revisions. Provide an on-demand guest onboarding path—profile, reference sheet, voice mapping, pronunciation and preview—using the same replaceable media providers. Reuse approved assets and retain scope/rights when an episode-only guest becomes recurring.
- Add image, speech-synthesis and media-source adapters as needed; expose required capabilities such as reference-image support, pronunciation control and language.
- Use asynchronous external-operation tracking for paid generation. Persist accepted outputs in owned storage with rights metadata; do not depend on expiring provider URLs as canonical assets.
- Implement content-addressed or equivalently reliable deduplication, safe uploads, bounded file/media processing and storage-location mappings.
- Support asset export and a small verified storage-migration exercise. Do not assume one provider's voice ID or latent character representation can be transferred to another.

**Acceptance**

- A character asset can be generated/imported, reviewed and reused without losing its rights, generation history or stable ID.
- A newly registered character receives a usable, reviewed media package without changes to production code. Switching a media provider or Shakuni presentation variant preserves identity requirements and triggers the relevant quality check.
- Changing image/speech providers either passes the capability/quality gate or reports an unsupported requirement; it never silently discards a character reference.
- Moving an approved sample asset to a different store preserves checksum and content references; duplicate external callbacks do not create duplicate assets.

**Scope boundary:** voice/likeness cloning requires appropriate permission recorded in the asset record. Provider switching needs fresh voice/character quality approval when output identity changes.

### M17 — WATCH Production Factory

**Depends on:** M8–M13, M16; M15 for trend episodes.

**Codex task**

```text
Implement M17. Convert an approved content revision into a reproducible episode
package using a portable scene/timeline model, replaceable media tools and
stage-level recovery. Deliver an internal WATCH player and exportable masters.
```

**Deliverables**

- Define an owned episode/scene/timeline schema referencing exact script, image, voice, music, caption, source and rights versions. Rendering templates are versioned assets.
- Include the immutable episode-cast snapshot: character/revision IDs, scene roles, wardrobe/presentation, voice/visual asset references, continuity and optional trend-brief/evidence versions. Rendering must resolve these records, not switch on named characters.
- Build storyboard → asset selection/generation → voice → timing/captions → render → QA → editorial approval stages with independent checkpoints and budget accounting.
- Separate deterministic composition/rendering from optional generative video. Integrate renderers, speech, transcription/alignment and subtitle tools through the relevant ports.
- Produce requested aspect ratios and caption formats, including a vertical short and a standard episode. Validate Indic text shaping, safe margins, subtitle synchronization, audio levels and readable citations/end credits.
- Add render manifests, asset checksums, warnings and a playable preview with a correction loop. Show creation cost and review effort per completed episode.
- Retain original masters/assets and lineage so a renderer can be replaced without losing scripts or the content model.

**Acceptance**

- One approved story creates a playable episode and captions; each factual claim can be traced through the script to evidence.
- A failure in one render stage resumes without redoing already accepted paid generation; uncertain provider jobs are reconciled.
- A sample timeline works with a second test renderer contract, while real output quality is verified visually and audibly on the configured production-intended renderer.
- A trend-inspired episode containing a newly added guest renders through the same pipeline. The published item has the correct event/fiction labeling, source references and pinned cast; later character edits do not silently change the finished episode.

**Scope boundary:** make a few good episodes before bulk automation. Do not promise bit-identical output when a third-party generative model is changed.

### M18 — Omnichannel Growth and Publishing Engine

**Depends on:** M13–M17; M1 job/operation infrastructure.

**Codex task**

```text
Implement M18. Treat YouTube, Instagram, Facebook and X as first-class publishing
destinations. Add per-platform/account capability checks, reviewable packages,
scheduling, idempotency and reconciliation behind replaceable publisher adapters.
```

**Deliverables**

- Model channel/platform, account binding, authorized scopes, content requirements, destination-specific metadata, approved payload hash, schedule/timezone and publication state.
- Produce articles, shorts, carousels, captions, thumbnails, links and campaign identifiers suited to the selected destination; apply length/media constraints from versioned capability profiles.
- Implement dry-run/export paths for all four requested destinations and real adapters as accounts/permissions allow. Direct APIs and an external publishing manager can both implement `ISocialPublisher`.
- Track `Draft`, `Scheduled`, `Submitting`, `Unknown`, `Published`, `Failed` and `Withdrawn` outcomes; persist remote IDs, reconciliation history and callback deduplication.
- Add notifications, account-token lifecycle, rate-limit handling and retry rules tied to actual provider idempotency support. An uncertain publish attempt must not post a second copy.
- Keep a manual export/publish workflow for unsupported operations and unavailable integrations, with recorded external URLs/IDs. Display connected, qualified and export-only states distinctly.

**Acceptance**

- The same approved content creates valid destination-specific packages for YouTube, Instagram, Facebook and X without changing core content records.
- Restart/retry/callback duplication does not create a second intended publication; an unknown outcome requires reconciliation before resubmission.
- Switching a publishing vendor preserves the destination platform/account and existing post bindings. Each enabled live destination passes its own authorization and publish/retrieve verification.

**Scope boundary:** platform API access, permitted formats and monetization eligibility must be checked against current official documentation. Never mark an export-only or mocked destination as live-connected; never silently send an Instagram post to X as fallback.

### M19 — Analytics, Attribution and Revenue Intelligence

**Depends on:** M1 event/usage records, M7, M14, M17–M18.

**Codex task**

```text
Implement M19. Connect content, acquisition, customer activity, production costs
and revenue using reconciled ledgers and explicit metric definitions. Preserve
revenue source independently from the channel that acquired the customer.
```

**Deliverables**

- Own the event schema and track content revision, universe, audience/locale where appropriate, campaign, publication, source system and consented identity. Ingest platform metrics through replaceable adapters or validated imports.
- Include trend-cluster/brief IDs, factual-versus-fictional content mode, cast/character revisions and discovery source in content-level analytics. Measure time to publish, correction rate, production cost, engagement and learning feedback by variant using appropriate aggregation; keep character/story performance separate from claims about individual viewers.
- Model revenue streams for platform payouts, sponsorship, Geeta+ subscriptions, TALK/CREATE credit sales, books/comics/digital packs, school/institutional contracts, content/ContentOS licensing, merchandise and affiliate/commerce income.
- Store gross amounts, taxes/pass-through amounts where applicable, refunds, platform/payment fees, net receipts, earned/estimated status and settlement references without counting settlements twice. Keep the operational ledger reconcilable to finance records.
- Separate acquisition attribution from revenue recognition. Example: an Instagram referral buying Geeta+ creates subscription revenue attributed to Instagram acquisition; it is not also Instagram platform-payout revenue.
- Report cost per approved article/episode/session, production contribution margin, review/rework cost, funnel conversion, retention and consented cohort outcomes. Distinguish media spend, AI/tool cost and operating overhead.
- Add scenario forecasts with editable audience, conversion, price, retention, platform-payout eligibility and cost assumptions; preserve forecast versions and show forecast-versus-actual. Use INR reporting if selected, with explicit FX provenance.

**Acceptance**

- Duplicate imports, delayed events and currency conversion do not double-count revenue or costs. Fixtures reconcile to a known financial total.
- Reports can group by revenue stream and acquisition channel independently; unknown attribution and unavailable metrics remain visible.
- Provider/metric-definition changes do not rewrite historical results silently. Forecast assumptions and uncertainty are inspectable; projections are never displayed as guaranteed earnings.

**Scope boundary:** start with SQL-backed operational reporting. A warehouse or full attribution platform is added only when volume or analysis justifies it.

### M20 — Identity, Households, Geeta+ and Credit Ledger

**Depends on:** M1 security, M2, M14, M19.

**Codex task**

```text
Implement M20. Add verified user/household identity, subscription entitlements,
privacy controls and a consistent credit wallet. Isolate identity/payment
providers and preserve existing customer-resource bindings during a switch.
```

**Deliverables**

- Model internal users, external identity bindings, household roles, consent records, profiles and access policies. Provide appropriate account recovery, profile deletion/export and progress synchronization.
- Introduce household/parent-controlled child profiles and age-appropriate defaults before enabling persistent child personalization. Apply reviewed regional requirements at the actual launch gate.
- Model plans, products, prices, feature entitlements, subscriptions and provider customer/subscription bindings. Do not spread provider-specific subscription states into business authorization.
- Implement payment/order webhooks with signature validation, replay protection, unique event IDs, reconciliation and recorded test/live environments. Client-reported payment success cannot grant access.
- Build an append-only credit ledger with grants, purchases, reservations, consumption, releases, expiry and reversals. Separate customer credits from M7 provider-spend budgets.
- Add notifications and payment-failure/refund/cancellation handling. Route new purchases to the active provider while old subscription changes and refunds use their original binding.

**Acceptance**

- Two concurrent requests cannot spend the same credits; a failed generation releases or settles the correct reservation exactly once.
- Duplicate/out-of-order payment events reconcile to consistent entitlements; a refund/reversal cannot be counted as fresh revenue.
- Provider changes retain internal user IDs, balances and existing payment bindings. Identity linking needs verified ownership; tenant/household data cannot leak across accounts.

**Scope boundary:** production charges require a qualified payment integration and completed commercial/privacy release gates. A gateway configuration switch does not migrate existing mandates or payment tokens.

### M21 — TALK: Dadaji Text

**Depends on:** M5–M10, M12–M14, M20.

**Codex task**

```text
Implement M21. Build a grounded Dadaji text experience using the shared evidence,
audience and entitlement systems. Make citations, uncertainty, memory scope and
cost controls visible while preserving the approved fictional persona.
```

**Deliverables**

- Implement retrieve → validate evidence/rights → answer → validate citations, using qualified task routes and an explicit no-supported-answer path.
- Support age/locale profiles, conversational context, reflections and suggested reading. Display Dadaji as an AI character, with attributed interpretations rather than claims of divine or human authority.
- Separate scripture learning and philosophical reflection from factual news, medical/legal/financial advice and crisis handling. Use the appropriate safe response/escalation behavior for sensitive questions.
- Define session lifetime, retention, consent, deletion and user-controlled memory. Disable unnecessary persistent child memory and keep each household/tenant context isolated.
- Reserve credits and provider budget per turn/session, enforce usage limits, support cancellation/streaming and record only consented content in diagnostics.
- Show cited verse/source links and collect report-a-problem feedback linked to the answer/evidence revision.

**Acceptance**

- Supported questions show valid citations; ungrounded or contradictory cases acknowledge uncertainty instead of fabricating scripture.
- Cross-user/household context, prompt-injection attempts and requests to alter entitlements cannot access data or tools outside the session's authority.
- Changing the qualified text route changes new turns/runs at the documented boundary; prior source/usage records remain auditable, and credit charging reconciles.

**Scope boundary:** launch with a reviewed set of capabilities and languages. Keep learning support separate from individualized professional treatment or advice.

### M22 — TALK: Realtime Voice

**Depends on:** M16, M20–M21.

**Codex task**

```text
Implement M22. Add consented, bounded voice conversations using replaceable
transcription, speech and optional realtime-session providers. Preserve the
same grounding, persona, entitlement and safety rules as text TALK.
```

**Deliverables**

- Implement a staged transcription → grounded text response → speech path first, with an optional native realtime adapter when its capabilities meet the same contract.
- Own session/turn IDs, turn state, citation events, interruption/cancellation handling, reconnection and allowed context transfer. Keep realtime credentials short-lived and narrowly scoped.
- Version voice profiles, pronunciation dictionaries, supported languages and audio formats. Dadaji voice changes require listening review and user-visible continuity where needed.
- Add microphone consent, recording/transcript retention controls, clear session status, text fallback and accessible transcript/citation presentation.
- Cap duration, concurrent sessions and credits; reconcile usage reported after disconnect. Measure first useful audio latency, transcription accuracy, interruption recovery and real cost.
- Pin a provider per active session; switch at new-session/reconnect boundaries. Do not send unfinished audio or historical context to a fallback without the relevant data policy/consent.

**Acceptance**

- A spoken question receives a grounded answer with corresponding on-screen sources; an interrupted answer stops playback/work as supported.
- Network loss, expired credentials and provider failure recover visibly without duplicate charges or silent cross-provider audio forwarding.
- Qualified locales pass recorded pronunciation/listening tests. An unsupported voice/language follows an explicit text or alternate-voice path rather than pretending parity.

**Scope boundary:** do not make native realtime support a prerequisite for useful voice TALK. Only enable always-on or recorded modes through explicit product and consent design.

### M23 — CREATE: Personalized Content Studio

**Depends on:** M9–M13, M16–M17, M20; M21–M22 where conversational input is used.

**Codex task**

```text
Implement M23. Let authorized users request personalized stories, learning
cards, comics and approved media from bounded templates while retaining shared
evidence, privacy, budget, rights and editorial controls.
```

**Deliverables**

- Define allowed personalization inputs, templates, output formats, audience/locale selection, preview/edit/review flow, ownership and private/public visibility.
- Reuse canonical evidence and characters; clearly label personalized fictional scenes. Collect the minimum data needed and provide parent/guardian control for child profiles.
- Validate uploaded text/images, file types, rights/consent, size and untrusted instructions; do not treat a file's embedded text as permission to invoke tools.
- Show estimated credits, actual production status and cancellation/refund rules. Add idempotent submission, queue position, progress and notification behavior.
- Export usable packages with rights/attribution and lineage. Public sharing/publishing requires the appropriate review and consent; private generation must not silently create public URLs.
- Isolate user media and drafts by tenant/household and implement deletion across assets, caches, indexes and reachable provider artifacts.

**Acceptance**

- A user creates and edits one personalized story/comic/learning pack through a configured template with correct source attribution and budget settlement.
- Disallowed personalization, unauthorized likeness use and cross-account access are rejected by server-side rules.
- A failed/repeated request does not double-spend credits or duplicate accepted generation; deleting a private creation removes public eligibility and starts any required provider cleanup.

**Scope boundary:** start with two or three reviewed templates. A general-purpose design/video editor and unrestricted user-defined tool workflows are separate future scope.

### M24 — Books, Digital Products and Institutional B2B

**Depends on:** M13, M19–M20, M23; M17 where video products are sold.

**Codex task**

```text
Implement M24. Package approved content into sellable products and institutional
offers, with portable commerce/fulfillment integrations, clear licenses and
reconciled orders. Preserve the wider revenue model without launching every
product type simultaneously.
```

**Deliverables**

- Model product/SKU, versioned content bundle, price, license, order, entitlement, delivery and refund. Support books, comics and digital learning/activity packs first.
- Generate print/digital packages with source credits, asset licenses, accessibility and visual layout checks. Record the exact content version sold and its update/correction policy.
- Provide institutional organizations, seats/cohorts, licensed content access and minimal appropriate learning reports. Keep institutional boundaries isolated; separate this from an entire school administration system.
- Integrate commerce, email delivery, fulfillment and optional print services through ports. Reconcile uncertain orders and preserve provider/order bindings across route changes.
- Add sponsorship/affiliate disclosures and contract references where relevant. Register merchandise, content licensing and ContentOS licensing as offers with capability/rights prerequisites, not automatic revenue.
- Feed verified orders, renewals, refunds, fulfillment costs and institutional invoices into M19. Attribute acquisition without duplicating revenue.

**Acceptance**

- A test digital order grants the correct version/license once; a refund/revocation updates entitlements and financial records consistently.
- An institution can access only its licensed content and authorized cohort data; seat changes are audited.
- A sample printable/comic output passes layout and rights checks; changing a fulfillment adapter preserves canonical order identity and history.

**Scope boundary:** launch one digital offer and one institutional pilot before expanding. Commercial eligibility, tax/payment configuration and contractual requirements are release prerequisites to be verified for the actual market.

### M25 — Security, Privacy, Reliability and Production Hardening

**Depends on:** all features selected for the next production release; earlier security gates remain mandatory.

**Codex task**

```text
Implement M25. Audit and harden the implemented product, exercise provider
switching and stateful migrations, and verify production operations with measured
evidence. Do not treat this milestone as the first implementation of security.
```

**Deliverables**

- Review threat boundaries: staff/user roles, tenants/households, uploads, web extraction, tool execution, secrets, webhooks, browser output, storage links and provider callbacks. Fix demonstrated issues.
- Enforce reviewed data retention/export/deletion policies across database, assets, search, backups, telemetry and external providers. Keep necessary financial/audit records minimized under the applicable policy; do not expose deleted conversational data through logs or caches.
- Run release-relevant dependency/license checks, access tests, recovery/failure injection, backup/restore, migration rollback/forward-fix and secret-rotation exercises.
- Validate provider switches for critical enabled routes using actual qualified providers where configured; run embedding reindex, storage relocation and publishing/payment reconciliation exercises. Offline-only integrations remain labeled as such.
- Set measured SLOs and alerts for reader availability/latency, queue delay, generation failure, cost variance, voice latency and publication backlog. Document restore-point/recovery-time objectives and actual exercise results.
- Produce deployment/IaC, environment separation, network/secret configuration, incident/runbooks, rollback procedures and kill-switch operation. Start with modest hosting; choose autoscaling/AKS only if workload evidence justifies it.

**Acceptance**

- A release checklist references evidence for every enabled feature; unresolved high-severity issues or required live-integration gaps prevent that feature from being declared production-ready.
- Restore and rollback recover a representative release without losing source lineage, customer balances or acknowledged business operations beyond the documented recovery objective.
- Required provider-switch/migration drills preserve contracts and resource bindings, and fault tests demonstrate bounded cost with no known duplicate business effect.

**Scope boundary:** harden only enabled/near-release capabilities. Do not hold a small READ-only release hostage to unimplemented voice or commerce features.

### M26 — Second Content Universe: Shivaji Maharaj

**Depends on:** M2–M13 and the reused READ/WATCH capabilities; M25 baseline for its release.

**Codex task**

```text
Implement M26. Prove ContentOS reuse by delivering a small, historically grounded
Shivaji Maharaj universe through the existing pipeline. Identify real Geeta
coupling and remove it with narrow, tested changes.
```

**Deliverables**

- Create a versioned universe pack with terminology, source/rights rules, historical events/people/places, audience learning goals, timeline semantics and editorial policies.
- Import a small reviewed collection of reliable, permitted sources. Distinguish documented facts, debated claims, popular legends and dramatized dialogue.
- Reuse source registry, claims, retrieval, audience variants, workflows, evaluation, CMS, media and publishing. Extend schemas only when historical material demonstrates a concrete need.
- Create approximately 10 READ items and two small WATCH samples as proposed proof targets, with citations and historical editorial review. Limit formats to already enabled product capabilities.
- Measure reuse versus required changes. Demonstrate that Geeta routes/data remain intact and make a short plan for Sambhaji Maharaj and Jain teachings as future packs.

**Acceptance**

- A reader can switch universes and use the same application workflows while receiving correct universe-specific labels, sources and learning content.
- Historical disagreement and dramatization labels remain visible; no scripture-only source assumption leaks into historical claims.
- Core changes are justified by cross-universe needs and tested against Geeta regressions. No copy-pasted second platform is needed.

**Scope boundary:** use one second universe to validate architecture. Do not launch all future universes or remove meaningful differences between scripture, history and philosophy.

### M27 — Learning Loop, Optimization and Selective Automation

**Depends on:** M12–M13, M19, M25; M26 for cross-universe experiments.

**Codex task**

```text
Implement M27. Improve quality, learning usefulness, reliability and economics
through reviewed experiments and bounded automation. Make every automatic change
auditable and reversible, retaining human judgment for high-risk publishing.
```

**Deliverables**

- Collect correction rates, editorial rework, learning feedback, retention, audience satisfaction, conversion, cost and operational failures. Do not optimize only for clicks/watch time or treat a quiz score as proven learning effectiveness.
- Version experiments across prompts, providers, retrieval, narratives, formats and schedules. Define hypotheses, guardrail metrics, minimum evaluation evidence, holdout use and stop rules before promotion.
- Add shadow/canary routing for qualified read/generation operations; keep stateful payments/publishing out of automatic speculative duplication.
- Promote candidate configurations only after evaluation and authorized policy checks. Keep automatic rollback and a clear history of the decision/evidence.
- Automate low-risk jobs such as scheduling already approved content, cost reports, expired-rights alerts and failed-job triage. Retain human approval for high-risk trends, sensitive history/religion, child-impacting policy changes and materially changed content.
- Add provider-health/cost review, stale dependency/API monitoring and periodic source/rights checks. Recommendations to change provider remain constrained by qualification and migration requirements.

**Acceptance**

- A candidate route/prompt passes a recorded experiment, activates for an allowed cohort and rolls back cleanly if a guardrail fails.
- A high-risk item cannot gain publish approval from optimization metrics or an agent's self-assessment.
- Reports distinguish measured results from hypotheses and preserve held-out evaluation evidence; automated jobs remain within credit, cost and permission limits.

**Scope boundary:** no self-modifying production code, uncontrolled autonomous publishing or unattended provider migration for existing stateful resources.

## 5. Release sequencing and scope control

Keep the original numbering for tracking. The milestones describe the full product; the following gates control when a useful subset can ship.

| Gate | Milestone scope | Concrete proof | Required operational baseline |
|---|---|---|---|
| Internal source-to-reader proof | Thin M0–M4 slice | Reviewed source → canonical verse → internal reader | Staff-only access, source rights and repeatable import |
| Public READ pilot | M0–M14 | Small reviewed collection and one learning journey | M14 security/privacy/rights, monitoring, backup/restore and rollback gates |
| WATCH and distribution pilot | M15–M18 as applicable | A few approved episodes and qualified selected channels | Asset rights, visual/audio QA, version-bound approval, publish reconciliation |
| Commercial and text TALK pilot | M19–M21 | Reconciled subscription/credits and cited text sessions | Payment/identity qualification, household/privacy controls and cost limits |
| Voice/CREATE and first product offers | M22–M24 | Measured voice quality, private creation and a test purchase path | Consent, media handling, wallet reconciliation and selected commerce controls |
| Wider production and second universe | M25–M26 | Restore/failure/provider drills and a reusable Shivaji pilot | Release evidence for each enabled capability |
| Measured optimization | M27 | A reversible experiment with quality and cost guardrails | Baseline evaluation, sufficient consented data and bounded automation |

M25 is an explicit hardening milestone and a reusable checklist. Its relevant release requirements must already be met before each earlier public/commercial feature goes live; implementation of every future feature is not required.

Proposed initial product focus: one primary delivery language, a small number of reviewed age profiles, a READ journey, a few WATCH episodes and selected qualified channels. Marathi is a reasonable proposed first delivery language given the user's preference, while Hindi and English remain configured expansion locales. This launch-language choice is a recommendation, not a recovered agreement. Keep the original 8–14 and 15–23 profiles available without forcing both into the first public launch.

Do not generate the whole content catalogue, connect every tool, add Kubernetes or launch all monetization streams before validating the first product experience. Keep the architecture capable of expansion and the initial operational footprint small.

## 6. Provider portability verification matrix

| Verification | Expected evidence | Milestones |
|---|---|---|
| Contract isolation | Vendor references appear only in approved integration/composition locations | M0, all integration work |
| Dynamic activation | Same application binary uses route A then B for new runs; prior runs retain their route snapshot | M1, M7 |
| Capability enforcement | Unsupported schema, locale, modality, context or policy is rejected before billable execution | M7, M9, M16, M22 |
| Safe fallback | Allowed transient failure uses a qualified fallback within the same deadline/budget/data constraints | M7, M25 |
| Side-effect uncertainty | Timeout after submission becomes Unknown; external status or manual evidence resolves it before retry | M1, M17–M18, M20, M24 |
| Provider-bound resources | Old posts, subscriptions, identities, objects and jobs resolve through original provider/account bindings | M18, M20, M24–M25 |
| Embedding migration | New embedding space/index passes evaluation before alias cutover; rollback remains available | M6, M25 |
| Owned assets | Masters, text, captions, timeline and source manifests export independently of generating tools | M16–M17 |
| Budget correctness | Every paid attempt, including fallback and failed charged work, is represented and reconciled | M7, M19–M20 |
| Policy-preserving routing | No fallback bypasses rights, child-data, region, consent or content policy | M7, M21–M23 |
| Tool/engine migration | Owned run state and IDs survive supported checkpoint migration or controlled draining | M8, M25 |
| Real versus simulated status | Adapter report separately records offline contracts, live sandbox qualification and production readiness | M7, M12, M25 |

Every external integration must also have a maintained record of official documentation, supported API/tool version, date checked, permissions, known limitations, pricing source, data sent/retained, secrets owner, contract tests and the steps needed to retire it.

## 7. Previously discussed external tools

The recovered discussion mentioned Microsoft Agent Framework, RAGFlow, Crawl4AI/RSSHub, MoneyPrinterTurbo, Remotion, VideoLingo and Postiz as possible reusable components. Their present features, licensing, maintenance and deployment compatibility have not been audited in this roadmap review. Treat these as candidates from the prior discussion, not mandatory dependencies or a claim that each is suitable today.

| Candidate role | Required integration boundary | ContentOS must retain |
|---|---|---|
| Agent/workflow framework | `IWorkflowRuntime`, typed tool operations | Workflow definitions, approvals, authoritative run/step state and audit |
| Retrieval platform | `IKnowledgeRetriever`, index lifecycle operations | Canonical sources, chunk/source IDs, evidence and rights policy |
| News/extraction tools | `ITrendSource`, `IWebContentExtractor` | Event/evidence records, source permissions and editorial decisions |
| Video/story tools | Generation ports and `IVideoRenderer` | Scripts, scene/timeline model, character bible, master assets and manifests |
| Caption/translation tools | `ISubtitleProvider`, `ITranslationProvider` | Approved terminology, source meaning, captions and revision history |
| Social publishing manager | `ISocialPublisher`, analytics adapters | Platform/account destination, approved payload, post bindings and reconciliation |

A maintained local tool, a subprocess/container, a hosted API and a SaaS product may implement the same narrow capability when their semantics fit. Keep commands/containers allowlisted and version-pinned. Do not allow arbitrary executable paths, dynamic assembly loading or unrestricted endpoints merely to call the provider system “dynamic.”

## 8. How to execute this roadmap with Codex

1. Put this specification in the repository's documentation and identify it as the active roadmap version.
2. Run M0 first to map existing work. Preserve the original milestone numbers and attach evidence to completed work rather than rebuilding it.
3. For each milestone, supply the common Codex instruction, that milestone's complete specification and the relevant provider rules. Keep one milestone as the implementation objective at a time.
4. Review its actual demo, tests, migrations, cost behavior and unresolved integration status before proceeding. Do not infer live readiness from generated code.
5. Deliver the first product release at the relevant gate; continue later milestones according to measured use and operating capacity.

Recommended first implementation action: audit the existing solution through M0, then implement any missing M1 provider/configuration/runtime foundation before adding more external integrations. If those already exist, prove their behavior with the portability checks and extend them narrowly.

## 9. Technical references consulted

These primary sources support the adapter, .NET AI, retry and embedding principles. The milestone order, contracts, acceptance gates and release targets above are design recommendations for this project.

- **S1 — Microsoft.Extensions.AI:** shared `IChatClient` and `IEmbeddingGenerator` abstractions. [Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- **S2 — Anti-Corruption Layer pattern:** keep external subsystem semantics behind an adapter boundary. [Microsoft Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer)
- **S3 — HTTP resilience:** retries require care for unsafe/side-effecting operations; the framework supports disabling retries by method. [Microsoft .NET documentation](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- **S4 — RAG embeddings:** use a compatible shared embedding space for document chunks and queries; Microsoft describes using the same model. [Microsoft Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/rag/rag-generate-embeddings)
- **S5 — YouTube video discovery:** the videos list API supports a most-popular chart for a specified content region/category. Treat it as one source of discovery signals, not complete coverage of every trend. [YouTube Data API](https://developers.google.com/youtube/v3/docs/videos/list)
- **S6 — YouTube caption access:** the official caption-download method requires permission to edit the video. [YouTube Data API](https://developers.google.com/youtube/v3/docs/captions/download)
- **S7 — Example news-provider capability:** News API documents headline discovery by country, category or source. It is an adapter candidate; this roadmap does not require selecting it or assume its language/rights coverage fits every use. [Provider documentation](https://newsapi.org/docs/endpoints/top-headlines)

Sources consulted on 10 October 2026. Recheck provider-specific documentation when implementing or activating each integration.

## 10. Dynamic characters and situational casting — accepted direction

The user accepted retaining the proposed cast while permitting any new suitable character to be introduced when a situation needs one. Characters are versioned content records. There is no fixed cast-size limit and no need to alter prompts or application code by hand for each new name; prompt construction reads the selected profiles.

### Initial character records

| Character | Initial role and personality | Story opportunity | Initial introduction |
|---|---|---|---|
| Dhairy | Curious, energetic child and experimenter; generous and ambitious, sometimes impatient or attached to recognition | Games, friendship, effort, losing, helping others and learning from action | Core child series |
| Neerja | Inventive, observant child and problem-solver; has her own goals, sometimes overplans or struggles to admit a mistake | Experiments, fairness, teamwork, uncertainty and letting others contribute | Core child series; retain Neeraja as an alias |
| Dadaji | Warm, playful mentor with hobbies, memories and mistakes; asks useful questions and keeps learning | Intergenerational reflection and life experience; participation is optional | Recurring across suitable stories |
| Shakuni | Fictional, witty trickster/imaginary commentator who sells persuasive shortcuts | The inner argument around ego, envy, impatience, approval and easy answers | Optional; approved coach/guru/adviser presentations |
| Tara | Initial teen profile, approximately 17; creative, outspoken, capable and seeking independence while valuing approval | School pressure, friendship, family expectations, belonging and online identity | Teen series |
| Kabir | Initial young-adult profile, approximately 22; resourceful, humorous and ambitious, with uncertainty about his direction | College-to-work transition, rejection, comparison, relationships and ethical choices | Young-adult series |
| Meera | Initial adult profile, approximately 36; capable professional/business owner, generous but sometimes overcommitted | Responsibilities, boundaries, work pressure, family, fairness and disappointment | Adult series when introduced |

These descriptions guide initial profile creation. The registry stores authoritative detail and explicit revisions; scripts cannot invent permanent changes to biography or family relationships. Age-profile selection changes the story's lead and treatment, not the protagonist's canonical age. Both children can be right or wrong, Dadaji need not solve every conflict, and Shakuni is not required in every episode.

### Registry and casting behavior

1. **Create a profile whenever needed.** An editor can introduce a coach, friend, teacher, colleague, musician, shopkeeper, neighbor, fictional animal or other supported character type with a name, purpose and situation tags. A workflow may suggest one and create a draft through its permitted tool contract. Unsupported animation mechanics are handled as capability gaps, not falsely promised as configuration-only work.
2. **Store reusable identity.** Keep character ID, universe scope, names/aliases, fictional/historical status, life stage, traits, goals, flaws, speech style, relationships, permitted situations, audience suitability, continuity, media rights, visual/voice profile references and version. Exact fields can be extended through validated schema evolution.
3. **Choose persistence scope.** Core and recurring characters belong to the universe; a guest can recur later; episode-only extras remain scoped to their original production until intentionally promoted. An episode-only character still has a stored identity and consistent assets across its scenes.
4. **Select a cast for a story.** Match audience, situation, role, learning objective, continuity, locale, media readiness and budget. Return a short explanation, such as “Tara fits the peer-pressure conflict; this new coach provides the sports authority role.” Editors can choose and override eligible candidates directly.
5. **Prepare only necessary assets.** Reuse existing references; generate/import new approved visuals and voice mappings through M16 when required. A text draft can use a registered new character before its final media package is ready; a publishable video cannot pretend missing assets have been qualified.
6. **Freeze the episode's version.** Store the selected profile and presentation revisions in the episode manifest. Future edits affect new productions; revisions to an existing episode follow the normal content approval/version process.

Example: a cricket story needs a coach. The editor adds a guest profile, assigns a coach role and compatible audience tags, creates the reference/voice package, and includes the guest in this episode. No named-character enum, switch statement, database deployment or new provider-specific script is required. The same guest can later be promoted to a recurring character while retaining its identity.

Normal content/asset checks apply to new cast members. Registering a real person, using their likeness or representing a historical figure follows the project's existing evidence and rights rules; the registry itself does not establish those permissions.

## 11. How trends become original Geeta Studio videos

This is the planned application workflow. Updating this roadmap does not establish that the connectors or production pipeline are already running.

### Inputs and their limits

| Input | What it contributes | What it does not establish |
|---|---|---|
| Permitted video charts/search/metadata | Topic discovery, source links and available popularity signals | Full video understanding, source reliability or permission to reuse footage |
| News APIs and publisher feeds | Headline discovery, publication times, source attribution and permitted article access | Automatic verification or unlimited rights to the article body |
| Licensed/owned video or authorized transcript/upload | Actual material that can be examined within its allowed uses | That an edited clip presents the complete context |
| Reviewed event calendar | Planned occasions, dates, context and opportunities for timely lessons | That every event needs a Geeta connection or identical treatment for every audience |
| Editor-pasted URL, topic or event | Direct entry for something the automatic feeds missed | Exemption from factual, relevance, rights or audience checks |

Connector polling, categories, languages, regions, priority rules and production limits are configurable. A connector must report its actual coverage. A publisher adapter capable of posting to a platform does not automatically provide broad discovery access to that platform.

### End-to-end workflow and ownership

| Step | Operation | Saved result and owner |
|---|---|---|
| 1. Discover | Collect candidate videos, articles and events through enabled providers or manual input | `TrendCandidate`, observations and connector provenance — M15 |
| 2. Cluster and verify | Group reports about the same event; inspect permitted evidence, timing, context and independent support; reject/hold unsupported claims | `TrendCluster`, evidence bundle, verification status and expiry — M3/M5/M15 |
| 3. Choose useful topics | Apply eligibility gates, then rank relevance, freshness, teaching potential, novelty and expected production effort | Reviewable shortlist with reasons and rejected/held outcomes — M15 |
| 4. Find a teaching connection | Identify the human dilemma; retrieve candidate verses and reviewed interpretations; explain fit or record no suitable connection | Cited teaching/evidence bundle — M5/M6/M15 |
| 5. Plan audience variants and cast | Choose event-explainer versus fictional-story mode; adapt the conflict; select existing characters or register suitable guests | `TrendStoryBrief`, audience profile and versioned cast — M9/M10/M11/M15 |
| 6. Write and review | Create an original hook, story/script, practical takeaway, citations and storyboard; assess facts, meaning, humor, age suitability, originality and budget | Reviewed content revision and production authorization — M11/M12/M13 |
| 7. Produce | Reuse/create character assets, speech, captions, scenes and rendered channel variants | Episode manifest, masters, caption files, costs and QA evidence — M16/M17 |
| 8. Approve and publish | Review the final video, recheck time-sensitive facts and rights, then schedule to qualified chosen destinations | Approved revision and external publication bindings — M13/M18 |
| 9. Learn and correct | Measure performance/cost/learning feedback; handle source corrections and outdated claims across derivatives | Analytics and correction tasks — M19/M27 |

At launch, the proposed default is automatic discovery and draft preparation within configured budgets, editor selection/script review before expensive production, and final editorial approval before publishing. Later low-risk automation follows M27 policy. High-risk publishing retains human review. This is one bounded workflow with replaceable providers; it does not require an independent autonomous agent for every step.

### Hypothetical example: an argument during a cricket match becomes popular

Assume a clip shows a player arguing after a decision. First establish what happened and the relevant context through permitted, adequate evidence. If the central factual claim cannot be established, hold/reject that event as a source; do not manufacture a story about the identifiable person. The table below illustrates an original fictional adaptation after the learning theme is selected, not a report about an actual current event.

Learning theme: responding to disappointment or anger before acting. Retrieve and review the appropriate Geeta passage; do not select or quote a verse merely because a keyword matches.

| Audience treatment | Original fictional story and cast |
|---|---|
| Children | Dhairy challenges a school-match decision; Neerja and a newly introduced coach help the story explore what his next action changes. |
| Teens | Tara feels unfairly treated during a competition and wants to post an angry message; a friend/organizer can be added for the situation. |
| Young adults | Kabir receives difficult feedback and is tempted to send an impulsive reply; a colleague can enter as a guest. |
| Adults | Meera faces a tense customer disagreement and must choose a response while protecting legitimate boundaries. |

The published fictional versions identify their fictional nature. Where a real event is mentioned, cite the verified factual context separately from the Geeta interpretation. The animation, dialogue and character actions are original; external footage, music or dialogue enters production only when its rights record permits that use.

An event should generate only the variants with a useful audience fit. Reject forced analogies, skip unsuitable subjects, and avoid spending on four versions simply because four audience profiles exist.

