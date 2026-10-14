# M23–M27 review — 14 October 2026

The modular-monolith architecture remains on track, but M24 has two reproduced correctness defects. Passing the existing tests does not establish complete acceptance. This review does not fix application code or qualify production release.

## Follow-up resolution
R1/R2 are resolved by the authorized M24 fixes. Five permanent real-SQL acceptance regressions,33affectedSQL/final9commerce rerun and the Windows demo passed. See [fix report](evidence/M24-review-fixes.md). The findings below record the original review and counterexamples; they are no longer open. Production gates remain pending.

## Findings

### R1 — P1: fence fulfillment against committed refunds/revocation
`src/ContentOS.Infrastructure/CommerceExecution.cs`, lines 29–32.

The worker checks the paid license while claiming a delivery, then submits outside the commerce transaction. `SqlCommerceDeliveryLedger.CommitAsync` only verifies the original dispatch fingerprint and deduplication; it does not recheck current payment/order/license or the claimed lease. Its effect lock is different from the tenant commerce lock. `Apply` also accepts a Delivered receipt without rechecking entitlement.

Controlled real-SQL reproduction pauses the fixture adapter after claim, sends an authentic fixture refund webhook, reconciles the order to Revoked, then resumes the original submission. One new delivery effect commits and DeliveryState becomes Delivered despite the revoked order. Download correctly remains denied, which does not prevent the fulfillment effect. No external email/printing or real payment occurred.

Required correction: synchronize final side-effect admission with refund/revocation and current lease, recheck current license/rights at the effect boundary, and propagate cancellation to claimed work where supported. Preserve original receipts and reconcile uncertain external effects rather than resubmitting them. Add an acceptance regression for refund between claim and effect.

### R2 — P1: enforce the purchased product's seat allowance
`src/ContentOS.Infrastructure/CommerceInstitutions.cs`, line 55; related Capacity/SeatAsync at lines 15–20 and 34–36.

Organization capacity is the maximum seat count across all active products. Institutional download only checks organization membership and an active order for the requested product. It does not bind a learner seat to that product/license or enforce that product's seat count.

Controlled real-SQL reproduction purchases both a five-seat institutional product and a distinct one-seat institutional product, registers and seats two adult members, then downloads the one-seat product successfully as both members. The larger unrelated purchase grants excess access to the smaller SKU.

Required correction: model/enforce product-specific licensed seat assignment or an explicitly purchased shared institutional entitlement; recheck the requested license's capacity and current assignments on download and after refund/expiry/capacity reduction. Add mixed-SKU and reduced-capacity acceptance regressions.

## Architecture assessment

| Milestone | Assessment |
|---|---|
| M23 CREATE | Uses owned contracts/rules and renderer ports, immutable revisions, original credit receipts, owner/guardian controls and current source/cast rights. Six focused SQL cases pass. Controlled local composition; genuine human/public qualification pending. |
| M24 Commerce | Reuses M20 original payment bindings and M19 finance, versioned owned catalog/packages and portable delivery ledger. Four existing SQL cases pass, but R1/R2 remain open. Commerce acceptance must not be described as complete. |
| M25 Hardening | Core dependency isolation, private HTTP/privacy controls, current source/persona and analytics fences retained. Four hardening plus one persona race SQL case pass. Historical restore/provider drills are evidence, not fresh review executions; production gates remain blocked. |
| M26 Shivaji | Uses shared corpus/evidence/retrieval/workflow/evaluation/CMS/READ/WATCH infrastructure, with narrow historical metadata/claim distinctions rather than a second platform. Four SQL cases pass, including the local media/pipeline proofs. Historian/source/rights and human release acceptance remain pending. |
| M27 Optimization | Versioned proposals, exact route/template admission, bounded reservations, immutable audits, scoped replay, candidate cancellation and human approval separation remain consistent with the agreed architecture. Nine SQL cases pass. Automatic execution is limited to qualified private generation; other experiment dimensions have versioned configuration/evaluation evidence. |

The repository architecture gate passed: Domain/Contracts have no infrastructure/vendor dependencies; Application references only Domain/Contracts and owns typed ports/rules; Infrastructure owns SQL/history; Integrations owns adapters; API/Worker compose the system, Web references Contracts. No extra host, vendor-owned workflow history or duplicated universe platform was introduced. The two findings are business-boundary enforcement gaps within this architecture, not a reason to replace it.

## Checks actually run

- Locked solution restore: pass.
- Solution build before and after review probes: pass, 0 warnings/errors.
- Full current offline suite: 111 passed, 0 failed/skipped; includes positive and deliberate-negative architecture tests.
- Focused current M23–M27 SQL suite: 27 passed, 0 failed/skipped (6 CREATE, 4 commerce, 4 hardening, 4 historical, 9 optimization).
- Additional shared AI/workflow/current-persona SQL boundary suite: 26 passed, 0 failed/skipped. Total existing SQL cases rerun: 53.
- Two review-only SQL counterexample probes: both reproduced the defects. These assert observed defective behavior, not desired acceptance. Source is retained as [review probes](evidence/M23-M27-review-probes.cs.txt) outside the compiled acceptance suite.
- Both EF models match existing migrations; source formatting and diff whitespace checks pass. No migration applied or database reset by this review.

[Machine-readable check evidence](evidence/M23-M27-review.json). Raw build/test logs and TRX results are in `.local/M23-M27-review-*` and `.local/M23-M27-review-results/`.

## Scope and limits

Existing uncommitted implementation preserved. Review adds documentation and counterexample source only; production application code is unchanged. Tests use the dedicated local SQL test database and controlled fixtures, with isolated identities. No real charge, external fulfillment, public publish/deploy, commit or new milestone.

The complete SQL suite was not rerun; 53 existing relevant SQL cases were rerun. Earlier full-suite checkpoints remain historical. This review did not repeat interactive VS Code F5/breakpoint/browser/accessibility, Windows demo/restart, Development identity/hash preservation, offsite/log/asset recovery, old-binary rollback, sustained SLO/alert delivery, current online dependency advisory review, live providers, or genuine human/editorial/historical/legal acceptance. These must not be inferred from fixture test success. R1/R2 prevent a clean correctness sign-off for M24 and its release-hardening claims.
