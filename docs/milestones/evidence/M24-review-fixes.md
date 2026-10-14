# M24 review fixes — 14 October 2026

This follow-up closes the two M24 correctness findings in the [M23–M27 review](../M23-M27-review.md). It preserves existing identities, original provider bindings, financial history, migrations and architecture. No new milestone, public deployment, real payment or external delivery.

## R1: final delivery admission and cancellation

The worker carries its current lease as transient owned dispatch admission data. The lease is excluded from JSON and the immutable original dispatch fingerprint. Immediately before inserting a local delivery effect, the ledger uses the same scoped SQL context/serializable transaction for current lease, order, original payment, product, exact licensed creation revision, owner/guardian and source/cast rights checks. Commerce and customer locks synchronize this admission with commerce changes and signed payment/refund updates. The lease deadline is checked again after rights validation.

A bounded cancellation watcher signals claimed work when payment, order, product or lease is no longer valid; query failure cancels rather than authorizes continued work. An uncooperative fixture cannot bypass the final ledger guard. Refused effects persist no receipt; the worker records Withheld. Existing authoritative delivery effects still replay/reconcile through their original adapter/account/content binding after a later refund. Historical successful delivery is not rewritten into a nonexistent event; current download access is separately revoked.

## R2: enforce each SKU's actual seat allowance

Institutional downloads now check the requested product's exact licensed allowance against the current enabled non-owner roster under the commerce transaction. Another product with more seats cannot expand this allowance. If the shared roster exceeds a smaller SKU's capacity, that SKU is blocked for all members until an administrator reduces the enabled roster. Disabled members remain denied; an eligible remaining member can then download. The owner management seat retains its existing exemption. Current payment/license and rights checks still apply to the requested SKU, independently of other purchases.

This deliberately retains the existing shared-roster pilot. Independent per-SKU learner allocations would require an additional product feature; no silent seat assignment or schema migration is introduced here.

## Permanent regressions

`tests/ContentOS.Foundation.Tests/SqlCommerceAdmissionTests.cs` adds five real-SQL cases:

1. Five-seat plus one-seat purchases: excess downloads denied; larger SKU still usable; reducing roster restores smaller-SKU access; disabled member and refunded smaller SKU stay denied.
2. Refund after claim/reconciliation: cancellation reaches the adapter; a deliberately uncooperative adapter still cannot commit an effect.
3. Signed payment refund before commerce reconciliation: same cancellation/effect fence holds.
4. Stale/expired leases and source revocation block final effect admission.
5. A completed original delivery preserves its truthful receipt and idempotent replay after refund while download access is denied.

The old review counterexamples remain historical evidence outside the compiled acceptance tests.

## Run and debug

From `C:\dev\GeetaStudio`:

```powershell
dotnet restore GeetaStudio.sln --locked-mode
dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
$env:GEETA_SQL_TESTS = '1'
dotnet test tests/ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlCommerce'
```

Use the existing [M24 local guide](../../M24_LOCAL_RUN.md) and `scripts/commerce-demo.ps1 -ManageApi -DebugPause`, then **Geeta Studio: M24 attach Api** with the printed PID. Break in `CommerceWorkbench.ProcessNextAsync`, `SqlCommerceDeliveryLedger.CommitAsync`, and `CommerceWorkbench.InstitutionDownloadAsync`. No new startup profile or migration is needed. Run SQL tests and demos serially.

Verification results are recorded in [machine-readable fix evidence](M24-review-fixes.json). Genuine external fulfillment cancellation/reconciliation, payment/contract qualification, public commerce, human/legal acceptance, fresh interactive F5/accessibility and full-suite validation remain separate gates. These fixtures perform no external effect.

## Verification result
Locked restore and final solution build pass with0warnings/errors;111offline and33affected real-SQL cases pass0fail/skip. Final nine commerce cases rerun after the missing-lease-expiry rejection/regression pass0fail/skip, including all five new acceptance regressions. Both models have no pending changes; changed-source whitespace and diff checks pass. Actual Windows private signed payment/replay/original unknown-delivery/provider-switch/institutional isolation/refund/API restart/synthetic finance demo passes; all owned hosts stopped. This is the controlled private verification tier, with no claim of fresh F5/live/human/public qualification or a complete full-SQL rerun. Initial test-import and formatting failures were corrected before completion.
