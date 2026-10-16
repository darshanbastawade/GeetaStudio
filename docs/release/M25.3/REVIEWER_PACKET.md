# M25.3 release reviewer packet — pending

Prepared for Prompt 9 on 16 October 2026. The owner explicitly requested that scope approval and human acceptance remain pending. This packet is ready for coordination; it grants no permission, review or publication approval. No genuine reviews were supplied or imported.

## Candidate and material to inspect

The proposal is READ / WATCH / manual M18 export, with no selected exact resources. All other pilot capabilities remain disabled. The base commit is `ee777f65e2b472e72f244964d426509ec4e990fc`, with the preserved Prompt 8 working changes. Use the [current decision](../../milestones/evidence/M25.3-prompt9-decision.json), [source baseline](../../milestones/evidence/M25.3-prompt9-baseline.json) and final Prompt 9 acceptance manifest to identify the candidate. Approval must bind the final compiled code, configuration, policy, provider, content revision and audience; the base commit alone is insufficient.

The [actual local corpus inventory](../../milestones/evidence/M25.3-prompt9-inventory.json) preserves all 128 exact stored text records, IDs, SHA256 values, kinds, locators and recorded permissions. Its 78 entry rows comprise two versions of the same 25-verse Sanskrit/English Besant sample and two 14-entry private history indexes. They are not 78 distinct Geeta verses or a complete edition. The Besant sample is Discourse 1, verses 1–25; the edition inventory and 700-baseline alignment remain separate. No requirement to import all 701 edition entries is introduced. No stored corpus annotations were found. Interpretations used in the video examples remain separately labeled fixture teaching; they are not scripture quotations or scholarly consensus.

Besant attribution: Annie Besant, *Bhagavad-Gita*, fourth edition, G. A. Natesan & Co., 1922; Wikisource transcription. Retain the original [source and license evidence](../../fixtures/M04/source-evidence.json), permanent revision reference, exact Sanskrit/translation separation and CC BY-SA 4.0 attribution/share-alike record. Source/transcription checks are not manuscript or theological approval. Historical excerpts retain their original author/account/bias labels and recorded private-sample territorial limits; they are not part of an approved Geeta launch. Recorded `Approved` source permissions do not approve an entire derivative release. Do not reuse revoked sources, fixture-only grants, or broaden AI/adaptation/commercial/territory permissions.

Historical technical examples, both **withdrawn fixtures**, are indexed with exact IDs/checksums/costs in [historical-samples.json](historical-samples.json). The [readable scripts](historical-scripts.md) copy the actual stored child/youth bodies without generating new text:

| Audience | Actual script, cast, citations and costs | Playable captioned master | Limits |
|---|---|---|---|
| Child | [child.json](../../../.local/M25-2-prompt7/sample/child.json) | [child MP4](../../../.local/M25-2-prompt7/sample/child-CONTROLLED-TONE-NOT-NARRATION.mp4), [VTT](../../../.local/M25-2-prompt7/sample/child.vtt) | 87.02 seconds; placeholder art and tone audio |
| Youth | [youth.json](../../../.local/M25-2-prompt7/sample/youth.json) | [youth MP4](../../../.local/M25-2-prompt7/sample/youth-CONTROLLED-TONE-NOT-NARRATION.mp4), [VTT](../../../.local/M25-2-prompt7/sample/youth.vtt) | 88.77 seconds; placeholder art and tone audio |

The scripts contain a fictional gathering event, a labeled interpretation of Besant 1.1, and original audience-specific stories. The source event was intentionally revoked during the previous fixture drill. These files are historical inspection material, not current export permission. Technical default `en-IN` is not owner-approved launch language or native-language acceptance. The two synthetic AI ledger amounts total USD 0.00216; actual external provider charges were zero. No invoices or live voice/AI qualification exist. New approved event/art/voice and current evidence must replace these examples before genuine media acceptance.

## Review work and separation

Use the existing M12 system at `/staff/evaluation`; do not create a second evaluation store. [Development reference cases](development-reference-cases.csv) contain the actual 100 old fixture inputs across 20 families, five development contexts each. Their IDs belong to an old test scope. They illustrate the schema and failure coverage only. They are not 100 independent genuine reviewed questions; never import their fixture attestations as Human. The old 20 held-out fixture cases were already consumed and are not a new sealed set. No held-out inputs or answer keys are included in this packet.

The coordinator must identify real reviewers and approve a fresh scoped dataset: at least 100 genuine reviewed Development labels and 20 genuine reviewed HeldOut labels, covering the existing 20 categories in [M12 catalog](../../fixtures/M12-catalog.json). Use five Development cases and one HeldOut case per category as a starting coverage allocation, with independently authored contexts and meaningful child/youth representation. The blank [worklist](review-worklist.csv) allocates these slots without fabricating questions, answers or scores. Replace provisional audiences and locale only after scope approval; use real owned IDs. Do not treat repeated wording or reskinned versions of a tuning question as independent held-out material.

The evaluation coordinator holds fresh HeldOut inputs and expected judgments outside the tuning team's packet, using restricted local storage and existing role controls. Register them through the authorized M12 dataset boundary when ready. Do not expose answers in tuning files or rescore consumed held-out identities. Pin the Development baseline, candidate adapter/route, prompt, retrieval space/index and release configuration. Evaluate each split through M12; HeldOut needs its Development baseline and remains once scored. A failed held-out run needs genuinely new questions rather than copied dataset versions.

For every selected release output, reviewers inspect the exact body, quotations/locators, interpretation labels, practical action and reflection, audience, current source rights, cast profile/presentation versions, transcript, visible artwork, playback, captions and current manual package. A body-only review cannot qualify unseen media, a different audience, a replaced voice/provider/account/resource, or another language. Native-language listening, caption timing/readability, child safety, character appeal and understanding need real relevant reviewers. Record aggregate age-appropriate feedback without children's names or personal data.

The existing four rubric scores are FaithfulMeaning, Usefulness, Readability and AgeSuitability, each 1–5. Acceptance needs every score at least 4; cast result reviews also need CharacterAppeal and Understanding at least 4. Reject unsupported doctrinal certainty, forced source connections, fabricated citations or events, omitted permissions, harmful audience instructions, missing transcript/voice evidence and continuity/identity changes. Human/advisory scores cannot override deterministic failures, current rights or M12 qualification. Actual retrieval baseline/latency/cost evidence remains required; an unbound recorded output cannot qualify a live provider.

## Genuine review import and audit

The [response template](review-response-template.json) has null scores/identities and `templateOnly: true`; it is deliberately not an import request. After a real review, fill the existing `EvaluationHumanReview` request in its `request` object with the actual immutable dataset ID, case key, optional exact run ID, new stable operation ID, scores, decision, `rubric.v1`, substantive notes and `manual:` evidence reference. Retain a genuine signed/identified reviewer record under restricted operator storage; never use a `fixture:` reference, invented reviewer identity, automatic maximum scores or auto-acceptance.

Submit **only that completed request object**, using the authorized Windows staff account and existing protected endpoint:

```text
POST /api/v1/evaluation/tenants/{tenant}/universes/{universe}/reviews
X-ContentOS-Staff: 1
Content-Type: application/json
```

`RunId = null` records a dataset label; the actual `RunId` records release-result acceptance. Both are necessary. The authenticated actor is recorded by the server; request JSON does not impersonate one. Stable operation replay must retain the same request. Reviews are immutable; reassessment needs the existing fresh dataset/run process, not editing history. Inspect the resulting report and its current qualification blocks after import. Re-run the release decision to obtain the exact new subject fingerprint before seeking operational receipts. This prompt performed **zero review imports**.

## User and operations sessions still required

With actual approved targets, walk through directory → chapter/verse → audience/language → source/interpretation → search → practical action/reflection/bookmark and WATCH → captions → manual export at phone and desktop sizes. Use keyboard and the intended screen reader, record comprehension/task outcomes, and test source withdrawal on those exact reader/search/export resources. The Prompt 9 fallback UI check is not this session. No genuine participants or screen-reader session have been recorded.

The deployment operator must additionally qualify intended TLS/authentication, isolation, secrets/rotation, retention, realistic selected-content load, real alert delivery, offsite SQL/log-plus-asset recovery and compatible rollback on the final artifact. The local fixture drills do not qualify these hosting requirements. Matching hosted CI and actual enabled-provider qualification remain pending. Keep operational evidence outside the release index until it is genuine, current, applicable and bound to the actual selected candidate. Do not deploy or publish to obtain a review.
