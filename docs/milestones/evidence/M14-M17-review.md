# M14–M17 validation and review —13 October2026

## Conclusion

The architectural direction is sound: .NET10 modular monolith, owned contracts/typed ports, scoped/versioned records, explicit rights and human release gates, dynamic characters, evidence-backed trends, and retained media/masters with stage recovery. Existing data and deployment conventions remain preserved. The implementation is not ready for an unconditional correctness sign-off: three confirmed high-priority gaps require fixes and permanent regression tests before M18/release qualification.

Review only. No production-code fix, migration, reset, commit, remote change, paid/live call or publication was performed. Controlled reproductions created new isolated synthetic records only in GeetaStudio_M1Tests. The reproduction harness remains ignored under .local/M14-M17-review; sanitized outputs are retained here.

## Confirmed findings

1. **[P1] Check source generation/AI rights before media dispatch.** `src/ContentOS.Infrastructure/MediaGeneration.cs:28–33` validates license shape and reference reuse, but never evaluates Generate permission for the output/reference/parent source lineage. Import later checks Read permission. A new synthetic source with read/adapt permission and AI explicitly disabled returned `AiProcessingNotPermitted` from the owned rights service, yet the same source-backed reference/output completed a media-generation job and received an accepted asset. The reproduction used the shipped local fixture adapter and cost0; it does not establish that a live provider was called. Enforce operation-specific generation/AI permissions at admission and again before dispatch, including inherited sources; recheck acceptance as appropriate. If deterministic tools use a different rights operation, model that explicitly rather than treating source attestation as permission.

2. **[P1] Pin the exact approved voice provider mapping.** `src/ContentOS.Infrastructure/WatchExecution.cs:15–17` permits a generated voice when only Profile.Key and ResourceKey strings match the reviewed package. Provider adapter, account, profile version/identity and imported-recording identity are not compared. A reviewed package using an imported recording was given a new tone-generation profile named `owned-recording.v1`, with the recording's GUID as its resource and a different account. WATCH accepted the replacement voice and reached Review with a playable episode, despite retaining the old presentation/package snapshot. Pin/compare the original full mapping and require a fresh reviewed package/presentation for provider changes. An imported recording cannot acquire a generator merely through colliding names.

3. **[P1] Invalidate trend briefs when their teaching is corrected.** `src/ContentOS.Infrastructure/TrendFence.cs:75–76` checks the stored teaching row's Approved/Matched flags but does not recheck its current lesson/anchor/interpretation claims. Correcting the synthetic interpretation made the lesson OutputWithheld=true, while RecheckAsync left the existing story brief Approved, with no blocks and the old interpretation still exposed. The direct binding/export path uses this fence; M11's separate lesson recheck does not fix that boundary. Recheck the pinned lesson and current underlying evidence; hold/redact affected briefs, prevent new binding/use, and create correction/remediation work. Preserve both scripture/news source and claim lineage on direct bindings.

All three were reproduced through owned application APIs on actual SQL with declared fixtures; no business logic was bypassed by direct database tampering. See M14-M17-review-reproductions.json. These are missing cases in the existing suite, despite the related milestone tests passing.

## Validation actually run

- `scripts/verify.ps1 -Locked -Sql -LowMemory`: locked build0 warnings/errors;92/92 offline pass;157/158 real SQL pass,0 skipped. All existing M14 READ, M15 trend, M16 media and M17 WATCH tests passed. The combined command exited1 because the older M06 retrieval test `Revoked_source_filters_evidence_and_citations_and_invalidates_cutover` failed in its alias lookup at SqlKnowledgeTests.Activate line68, before revocation assertions; do not label this full run green.
- Unchanged focused recheck of that test:1/1 pass,0 skipped,28sec. Read-only count found113 aliases in the shared Test Geeta universe. KnowledgeService.AliasesAsync applies unordered Take(100); an alias created successfully can be absent from the subsequent listing. Treat this as an existing test/data/listing reliability issue; no pagination change, deleted test records or relaxed assertion/timeouts were used.
- Separate `dotnet format GeetaStudio.sln --verify-no-changes --no-restore`: exit0 (the broad command stopped before format).
- Both EF model checks with Infrastructure design-time startup: no pending Core/Runtime model changes. Development19 Core/4 Runtime migrations remain.
- `.local/M17-preservation.ps1 -After`: original8 characters/3 universes/50 corpus entries/10 jobs/5 source permissions, exact IDs and complete source permission hash preserved. No reset or license upgrade.
- Ignored reproduction harness: build0 warnings/errors; all three targeted cases completed and reproduced the gaps. Original source/API contracts and production code unchanged.
- `git diff --check`: pass. No fresh interactive F5, browser/mobile/screen-reader or real human listening/visual acceptance was run in this review. Earlier Windows HTTP/restart/render/storage/restore evidence remains historical evidence, not a newly repeated UI check.

## Coverage and direction

| Milestone | Assessment | Remaining material limits |
|---|---|---|
| M14 READ | Private/local reader, release eligibility, SSR/search/sitemap withdrawal and device-state logic are implemented and existing tests pass; no new high-priority M14 finding confirmed in this review. | Genuine M12/public-release, phone/accessibility/privacy/production acceptance remains open; successful staff/API tests do not prove it. |
| M15 trends | Canonical permitted evidence, actual match/no-match, distinct audience packages, current event correction and private operation model are on track. | Corrected-teaching invalidation gap above; real licensed live feeds/extraction and genuine human/source qualification remain open. |
| M16 media | Versioned owned files/rights/cast packages, local providers, current-use checks, zero-budget fixture gates and storage portability are on track. | Missing source-generation rights check above; no qualified paid/live visual generator, Indic speech, cloning or production art/voice acceptance. |
| M17 WATCH | Exact timelines, stage checkpoints, owned playable masters/captions, renderer receipts, original-resource recovery and private player are on track. | Exact voice mapping gap above; approved scene timing only, no ASR/word alignment/generative-video adapter, no genuine human/native-language quality or fresh interactive F5 qualification. |

Fixture adapters prove contracts and failure handling, not vendor qualification or artistic/native-language quality. The second FFmpeg contract is not an independent vendor. M12's100 genuine reviewed Development+20 genuine held-out cases remain pending. M04 remains a25-verse-per-edition sample, not complete scripture coverage. These limits are correctly kept separate from public/production readiness.

Next: fix the three confirmed gaps with permanent regressions, address the older capped alias lookup reliability, then rerun the relevant checks. M18 has not been started. Keep genuine human/source/provider and public release gates visibly open.
