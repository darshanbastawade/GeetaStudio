# M06 local run, retrieval and index migration

From PowerShell in C:\dev\GeetaStudio:

```powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql
```

Uses existing .NET10, Windows authentication, local SQL and private user secrets. Adds M6Knowledge; retains data, migrations and login passwords. Tests use GeetaStudio_M1Tests; application uses GeetaStudio_Dev.

## Run and demonstrate

Use three terminals, one command per terminal:

```powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
```

Open http://localhost:5200 and follow Knowledge and evidence search, or http://localhost:5100/staff/knowledge. The page uses your provisioned Windows account. Select the local tenant and Geeta universe, refresh, choose an active alias and search. Source strings and citations are returned as evidence; no generated answer or canonical gap filling.

With the API running:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\knowledge-demo.ps1
```

The helper replays the preserved bound M04 source sample, creates a controlled reviewed field/battlefield alias, queues asynchronous SQL indexing, runs the two owner-confirmed labels, activates an index, demonstrates semantic alias matching and no supported pizza answer, rejects a same-dimension model mismatch, switches to the independent fixture adapter/model v2, then rolls back. It preserves old indexes/aliases and uses new run keys. No paid/network model calls or publication.

The initial human-labeled fixture is docs/fixtures/M06-golden.json. The project owner confirmed the two relevance/no-answer labels in chat on 10 October 2026. This confirms those labels only; no additional scholarly review or broad model qualification is claimed.

## Debug

Open C:\dev\GeetaStudio as the trusted VS Code folder. Stop existing hosts. Select **Geeta Studio: all**, F5, then **Tasks: Run Task → Geeta Studio: M6 knowledge demo**. Breakpoints: KnowledgeService.QueueAsync, ProcessNextAsync, Search, ValidateAsync or ActivateAsync; embedding/ranking code in ContentOS.Integrations/KnowledgeAdapters.cs. Shift+F5 stops the compound. Existing M0 debugger evidence is preserved; a fresh interactive M06 breakpoint check was not run.

Reindex work is a durable SQL queue processed asynchronously by an API-hosted background worker, KnowledgeIndexProcessor. This deliberately reuses the API's existing source/corpus read identity. The separately runnable M01 Worker retains runtime-only SQL privileges and gets no knowledge/source privileges. API restart resumes Queued work. Set Knowledge:IndexWorkerEnabled=false to stop only this processor; queues remain durable. Builds commit all chunks/Ready atomically; interruption rolls back and remains Queued. Other failures become Failed with a redacted reason; queue a fresh index with a new key after fixing the cause. No remote side effect is retried.

## Staff API

Base: /api/v1/knowledge/tenants/{tenantId}/universes/{universeId}. Staff authentication and owning tenant membership required. Reader can inspect/search; Administrator controls concepts, review, indexes, validation and aliases. Every POST requires X-ContentOS-Staff: 1.

- GET /graph: concepts, terms (Name/Alias/Translation), relationships and review status/provenance. Owning staff can inspect pending metadata.
- POST /concepts: operationId, key, snapshotId, terms [{locale,text,kind}], optional claimRevisionId. Same-universe source required. Pending by default.
- POST /relations: operationId, fromId, toId, kind Related/Broader/Contrasts, snapshotId, optional claimRevisionId. Both nodes must belong to the universe. Pending by default.
- POST /graph/{id}/review: expectedRowVersion, status Approved/Rejected, evidenceReference with manual:/fixture:/evidence: prefix. Source review/current rights and current claim eligibility checked. Approved/rejected review is frozen; corrections use new records.
- GET/POST /indices: POST operationId, key, manifestId, locale, territory, adapter, model. Deployed profiles only: sql-hybrid-v1 (production-intended local adapter), fixture-ranking-v1 (independent test adapter); concept-projection-v1/v2, 128 dimensions, L2 normalization. Never use the fixture adapter as evidence of production relevance.
- POST /indices/{id}/validate: expectedRowVersion, evidenceReference, questions [{query,expectedCorpusTextId}]. Include known-positive and no-answer cases (null target). Shadow evaluation searches the candidate index without changing the alias; a failed evaluation cannot authorize activation.
- GET/POST /aliases: POST operationId, alias, indexId, expectedRowVersion (empty for a new alias), queryEmbeddingSpaceId. Only Ready, validated, currently eligible indexes can activate. Rowversion/serializable cutover atomically changes active and previous IDs. Rollback is the same guarded operation targeting the retained prior index; no vectors are copied/mixed.
- POST /search: alias, query, locale, territory, embeddingSpaceId, limit 1–20. Optional use is the existing IntendedUse record (purpose/language/territory/format and commercial/adaptation/translation/aiProcessing flags). Omitted use is staff READ/Text. Requested-use rights apply before ranking/evidence return; READ permission never authorizes generation.
- GET /indices reports pinned model/version, dimensions, normalization, chunking, space ID, state, validation result/evidence and rowversion. GET /aliases reports active/previous IDs and rowversion.

Example search body:

```json
{"alias":"your-active-alias","query":"Where did the opposing armies gather?","locale":"en-IN","territory":"IN","embeddingSpaceId":"space-ID-from-active-index","limit":5}
```

Responses contain NoSupportedMatch or SupportedEvidence; index/space/adapter/model metadata, stable chunk/corpus/snapshot IDs, exact text, edition/verse/version, source-snapshot UTF-16 span, SHA-256, source locator, current permission/citation and attribution. Scores rank evidence; they are not confidence that a claim is true.

## Compatibility and rights

One exact verse component per immutable chunk (verse-component-v1). Chunk IDs derive from corpus text ID and policy, so retrieval/index switches preserve evidence identity. Index rows pin the reviewed material ID list and the reviewed concept graph. EmbeddingSpaceId hashes manifest ID/version, material, graph, locale, model/version, dimensions, normalization and chunking policy. Different models require new spaces even with identical dimensions. Query vectors use the pinned profile; callers cannot override it or silently select an incompatible space. Persisted and returned vectors/contracts are validated; adapter results cannot invent or bypass filtered chunk IDs.

Lexical matching uses local normalized tokens, limited English inflection handling and inverse document-frequency weighting. Semantic projection uses reviewed concept aliases and one-hop Related/Broader edges into a 128-dimensional normalized feature vector. Contrasts remains a reviewed relationship without implying equivalence. No neural model, inferred relationship, automatic translation, philosophical authority or broad multilingual semantic quality is claimed. A new pretrained/remote embedding provider needs its own approved adapter/profile/license, explicit permissions and new validated index; M07 handles full provider routing.

Corpus components must remain approved, exact and selected-edition-bound. Source permissions/revocation/retention and intended use are rechecked before ranking. Restricted components never appear in evidence/citations. Superseded/open-correction claim provenance or restricted concept/relationship provenance blocks the pinned graph; all referenced claim evidence sources are rechecked. For claim-backed corrections, register replacement graph records referencing the current claim version and build/evaluate/cut over a new index; old stale nodes are excluded. Generic graph editorial withdrawal/deletion is not supplied. Existing chunks are retained for audit; revocation is enforced on every retrieval without waiting for a purge/reindex.

This is internal owning-tenant retrieval, not public/shared access. Bounds: 100 concepts, 20 terms per concept, 300 relationships, 1,000 components per index, 100 recent indexes/aliases, 1,000-character queries, 20 results and 2–20 labeled evaluation cases. SQL exact scan is appropriate to the initial sample; large-scale vector acceleration, caching, production throughput and neural model quality are not qualified. Corpus still has 25/701 pairs and 676 missing entries.

## Verification limits

See milestones/M06.md for actual checks and saved evidence. Fresh interactive F5/browser visual/accessibility QA, hosted CI/cold cache, Azure SQL, substantive scholarly/model-quality/license review, production scale/restore/security and any real external embedding service remain unverified. M07 is not started.
