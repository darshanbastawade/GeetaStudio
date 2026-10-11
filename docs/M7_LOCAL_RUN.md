# M07 local run, AI routing, budgets and debugging

From PowerShell in C:\dev\GeetaStudio:

```powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql
```

Setup applies the additive M7AiProviders/M7AiLedger migrations to the existing dedicated local database, retains prior records and credentials, and grants only the API the needed ai-schema object permissions. The separate M01 Worker still has runtime-only privileges.

Use three terminals, one command per terminal:

```powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
```

Open http://localhost:5200 and follow AI tasks and budgets, or open http://localhost:5100/staff/ai with your provisioned Windows account. The API hosts M06 asynchronous indexing; the separately runnable Worker processes M01 runtime probes.

With the API running:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ai-demo.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ai-benchmark.ps1
```

The demo changes the local draft route A → B for new runs, proves old-operation replay retains its snapshot, rejects tools, submits two concurrent evaluate requests against a small shared INR fixture budget and admits exactly one, then rolls the draft route back to A. It also configures the local embedding route, indexes all 25 approved English components through the usage ledger and leaves the new index Ready for separately reviewed shadow validation/activation. It never changes an active knowledge alias.

Fixture USD/INR rates are explicitly artificial configuration used to test the monetary ledger; they are not current vendor pricing, payment or a real charge. Existing periods/reservations are preserved, not reset by repeated demonstrations. An exhausted demo budget requires a later reviewed period. The benchmark measures latency, availability, structured/keyword quality and cost/reservations for original public prompts; it reports human quality as NotReviewed and never changes routing. Both helpers reject remote API addresses.

## Debug

Open C:\dev\GeetaStudio as the trusted VS Code folder. Stop existing hosts. Select **Geeta Studio: all**, press F5, then **Tasks: Run Task → Geeta Studio: M7 AI demo** or **M7 AI benchmark**. Shift+F5 stops the compound. Useful breakpoints:

- AiService.Prepare: policy selection and full-chain reservation.
- AiService.Start / Generate / FinishAttempt / Finish: admission, provider result and settlement.
- AiService.ReconcileAsync / RecoverAsync: unknown usage and expired interruption recovery.
- RoutedEmbeddingProvider.PinAsync / EmbedAsync: immutable index model binding.
- FixtureTextA, FixtureTextB and OpenAiTextProvider: protocol/fixture behavior.

Existing M0 interactive debugger evidence is preserved. M07's actual verification tier is recorded in milestones/M07.md; a fresh interactive F5/breakpoint or visual/accessibility session must not be inferred from HTTP tests.

## Task profiles and API

Base: /api/v1/ai/tenants/{tenantId}. All operations require Windows staff authentication and actual owning-tenant membership. Reader can inspect profiles/routes/budgets/runs; Administrator registers configuration, changes controls, generates, reconciles and recovers. Every POST requires X-ContentOS-Staff: 1.

- GET/POST /profiles: immutable settings with adapter/model/capability/locale, explicit structured/streaming/tools flags and context limits, versioned pricing and data policy/qualification evidence. SecretRef is a separate registration field and is never returned in run/profile output.
- POST /profiles/{id}/switch: enabled/killed, using the original M01 runtime connection controls.
- GET/POST /routes: task/version, ordered profile IDs (one primary, at most two fallbacks), run limit, currency, deadline, required features and data/prompt policies. Tasks: draft, summarize, translate, evaluate, answer-with-evidence and embedding.
- POST /routes/{id}/activate: activate or roll back; new runs use the new route, old snapshots remain immutable.
- GET/POST /budgets: capability, currency, explicit UTC starts/ends and limit. Overlapping tenant/capability/currency periods are rejected. Expiry does not erase pending reservations.
- POST /universes/{universeId}/generate: operationId/task/prompt and matching locale/classification/region/retention/training/feature requirements. Optional sourceSnapshotIds must belong to that universe and permit the requested generation/translation/AI use.
- POST /universes/{universeId}/stream: same request; newline-delimited owned Delta/Completed events, backpressure and cancellation. Deltas are provisional provider data; only the final validated result is an accepted draft. No fallback splices providers after a delta.
- GET /runs and /runs/{id}: task/profile/provider/model/route/prompt/policy/pricing versions, attempts, exact token usage/cache usage, actual costs, held reservation, normalized outcome, generated draft and current source-citation attribution.
- POST /runs/{id}/recover: only after the original deadline, recover an interrupted Reserved/Running record. Release unsubmitted allowance; submitted Started attempts become UsageUnknown without resubmission.
- POST /runs/{id}/attempts/{attemptId}/reconcile: usage {inputTokens,cachedInputTokens,outputTokens} and explicit evidenceReference. Idempotent for identical authoritative evidence/usage; conflicting or repeated known settlement is rejected. Reconciliation never retries a generation.

Generated outputs use the owned generated-text.v1 shape:

```json
{"text":"Generated draft requiring review.","citations":[]}
```

The canonical validator rejects malformed/extra fields, invalid/foreign citations and missing citations when reviewed source IDs were supplied. Output labels are GeneratedDraft or GeneratedTranslationDraft; generating does not approve scripture, claims or publication.

answer-with-evidence requires evidenceQuery (the M06 KnowledgeQuery). The owned retriever rechecks rights for Generate/adaptation/AI processing and returns NoSupportedMatch if there is no supported evidence. Current Besant sample is READ-only; it does not silently authorize AI generation. Revoked source-backed outputs are withheld on subsequent inspection.

## Budget and fallback behavior

Before submission, a SQL transaction with a tenant application lock reserves the sum of all potential attempt ceilings against the run limit and current tenant/capability/currency period. The ceiling conservatively counts UTF-8 input bytes plus a bounded protocol allowance as tokens, with the configured maximum completion tokens. Provider concurrency and shared enabled/kill/circuit controls are checked before each attempt. At most one attempt per configured profile is made; there are no adapter/SDK retries.

Known charged failures and successful fallback usage all settle against the pinned pricing version. Cached input tokens use the separate cached rate. Only known bounded transient/schema failures may fallback; policy refusals, quota/configuration failures, unknown usage and cancellations do not. Every fallback must satisfy the original classification, region, retention, training and policy version before any request is sent.

Unknown/missing/invalid usage, disconnected streams, cancellation after admission and process interruption retain that attempt's ceiling and expose UsageUnresolved. Unused fallback allowance is released. Never enter fabricated zero usage to clear a hold. Administrative reconciliation requires authoritative provider/account evidence. An actual report over its reservation ceiling is retained as an overage and blocks further admission when the budget is exhausted; no external provider's billing can be made transactional with local SQL.

No raw prompt/provider error/credential appears in operational logs or snapshots. The database retains bounded generated outputs behind staff membership; source rights are checked again when reading them. Prompts are hashed, not persisted for automatic retry. Crash recovery therefore never reconstructs or blindly resubmits a prompt.

## Embedding compatibility

M06 historical concept-projection-v1/v2 spaces remain available as their immutable legacy local profiles. For new fully controlled indexes use model **route:embedding** in QueueKnowledgeIndex. Queueing pins the active route/profile IDs to ai-embedding:ROUTE_ID:PROFILE_ID before computing EmbeddingSpaceId. Query and corpus calls use this same pinned profile and report usage to the M07 ledger even after the default route changes. A new model/profile needs a new index, shadow validation, alias activation and rollback through M06; embedding fallback across spaces is prohibited.

The implemented embedding adapter is the reviewed local 128-dimensional L2 concept projection, not a qualified neural provider. It performs no remote processing. A new remote embedding provider needs a deployed/qualified adapter, source AI permissions, verified data policy and explicit new space/index lifecycle.

## Real text adapter and live gate

openai.text.v1 implements the fixed official /v1/chat/completions protocol: strict owned JSON schema, max_completion_tokens, store=false, optional SSE/include_usage, normalized refusals/errors and cancellation. Redirects/SDK retries/tools are disabled. Microsoft.Extensions.AI.Abstractions 10.9.0 supplies internal chat/usage DTOs; vendor types do not enter Domain/Application contracts.

Model IDs and currency-aware rates are maintained profile configuration, not task constants. Configure credentials privately through the existing config:ProviderSecrets:NAME user-secret reference or env:GEETA_PROVIDER_NAME environment reference. Do not put a key in chat, reports, configuration files or benchmark fixtures; Codex sign-in is unrelated.

Real calls require all of: an available private credential reference, an explicitly evidence-qualified profile (LiveSandboxVerified, never fixture evidence), compatible policy/features, active bounded route/budget and private Ai:LiveEnabled=true. Unqualified registrations cannot activate. The current real adapter is limited to reviewed Public data and the declared US/default-retention profile (at least 30 days, no training); account-specific residency/ZDR and other sensitive classifications are not qualified. Qualification evidence must establish account/model schema, streaming/usage, policy, pricing and budget compatibility; setting a label alone is not evidence.

No live provider was called for M07. Exact current model access/pricing/account policy, quality and a second real fallback remain live prerequisites. Fixture A/B fallback proves the owned routing/ledger, not real vendor equivalence. Benchmark -Live is an explicit billable profile and still obeys its worst-case MaxEstimatedCost; first review the credential/profile/budget and qualification evidence.

## Limits and next milestone

Bounds: three attempts/profiles, 32 KiB configured input allowance (16,000 prompt characters, plus evidence/protocol limit), 4,096 output tokens, 5–300-second original deadline, up to four calls per connection, 20 explicit source IDs, USD/INR separately accounted without FX conversion. The benchmark's two original prompts provide only an initial transparent quality proxy. No distributed M08 workflow engine, tools, automatic route optimization, customer billing, auto-translation approval or publication.

Fresh interactive F5/browser QA, hosted CI/cold cache, Azure SQL, full operational/load/security/restore testing and live model/fallback/data-policy qualification remain open. M08 is not started.
