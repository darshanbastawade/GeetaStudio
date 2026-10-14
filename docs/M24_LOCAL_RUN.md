# M24 local run and debugging

Run from `C:\dev\GeetaStudio` using pinned SDK10.0.401 and the existing dedicated local SQL Server. M24 starts with one private-test comic/digital offer and one institutional pilot. No actual card/customer charge, email, print fulfillment, public sale or commercial qualification is enabled.

```powershell
Set-Location C:\dev\GeetaStudio
dotnet restore GeetaStudio.sln --locked-mode
dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
$env:GEETA_SQL_TESTS = '1'
dotnet test tests/ContentOS.Foundation.Tests/ContentOS.Foundation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Commerce'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/commerce-demo.ps1 -ManageApi
```

Run SQL tests and demos serially. The demo prepares owned M23 technical fixtures and local signed M20 test payment events, then verifies the actual Windows private `/shop` API, duplicate payment receipt, original unknown delivery reconciliation after a route switch, licensed download, institution isolation, restart, refund revocation and M19 synthetic net50INR. Signing material is generated locally, stays outside source control, is not printed and is restored after the demo. It downloads `.local/m24-demo/printable-comic.html` and institution-pack.html before refund, then stops only its API hosts. These samples are explicitly controlled fiction/translation evidence, not commercially accepted products.

Apply Development schema/restricted permissions without recreating existing data:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
```

F5 **Geeta Studio: all** retains API5100, web5200 and the separate Worker. Open `http://localhost:5100/shop`, select your verified-account workspace, then an available private offer and personal or institutional license. Default Development deliberately has no fabricated paid order, provider signing key or qualified offer. Administrator-owned reviewed M23 packages register at `/api/v1/customer/tenants/{tenant}/shop/products` with an explicit private-sharing consent/license, pinned version/hash, price, days/seats, contract and correction/update policy. Only PrivateTest licenses are currently accepted. Books/comics/learning packs use M20 Digital product semantics; institutional licenses use Institutional semantics. Neither mints customer credits.

Configure original payment routes through existing M20 administration and original delivery routes at `/api/v1/customer-admin/tenants/{tenant}/shop/routes`. Deployed controlled delivery adapters: delivery.fixture.a.v1, delivery.fixture.b.v1, delivery.unknown.v1, email.fixture.inbox.v1 and print.fixture.v1. All are disconnected zero-cost receipt fixtures; email/print names are contract demonstrations, not actual external services. Receipt IDs use fixture prefixes and are not public download URLs. New routes affect new orders; unknown original orders reconcile their original adapter/account/content hash and never blindly repost.

For concrete debugging:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/commerce-demo.ps1 -ManageApi -DebugPause
```

Choose **Geeta Studio: M24 attach Api**, select the printed API process, and break in CommerceWorkbench.PurchaseAsync/ReconcileAsync/DownloadAsync, ProcessNextAsync and CustomerWorkbench.WebhookAsync. Open the printed `/shop?workspace=...` link. Continue/detach and press Enter to complete checks and cleanup. Focused checks and demo tasks are provided. No fresh interactive breakpoint is claimed without an actual interactive check.

Browser/submit success cannot grant access. The authoritative signed payment must match the original product/price/user/provider/account/environment/resource and bounded period. Only then does reconciliation activate the exact license and enqueue its original fulfillment. Refunds go to the original provider; access changes on authoritative reconciliation. Download also checks canonical payment state directly, so a refund/expiry denies access even before local status refresh. Renewals use new stable orders for new paid periods; past prices/content/history are not overwritten. No bank settlement, tax invoice compliance or guaranteed income is fabricated.

Institution owners manage audited adult account seats/cohorts. Learner and administrator seats consume the licensed allowance; overlapping renewal periods do not multiply capacity. Children stay guardian-controlled profiles. Completion records require current own/guardian progress consent. Administrator summaries suppress fewer than five current consented subjects and disclose no profile text/identifiers or causal learning claim. Other institutions and individual orders remain isolated. This is a licensing pilot, not a school administration system.

M19 captures original paid products/refunds via capture-customer-payments; reserved fulfillment capture is `/api/v1/customer-admin/tenants/{tenant}/shop/capture-fulfillment?universe={universe}`. Economic identities deduplicate repeated capture. Acquisition stays separate from BooksComicsPacks/InstitutionalContract revenue; delivered zero-cost fixture receipts are known zero costs. Institution contract references accompany their signed paid orders. Sponsorship, affiliate, merchandise, content licensing and ContentOS licensing register only disclosed prerequisite-gated offers, with no purchasing or automatic revenue.

Current source/character/owner/guardian rights and retention still govern licensed copies. Ordinary private edits do not rewrite a sold version. Source corrections/withdrawal can withhold access under the recorded policy; deletion erases the original and reachable stored licensed bundle content, disables access and retains minimal financial/effect/audit receipts. Owner-downloaded files cannot be remotely recalled and require owner cleanup. External provider cleanup, actual commercial distribution, legal/tax/market/payment configuration, real fulfillment/email/printing, source/media licenses, genuine human M12/quality/privacy/age review and browser/mobile/accessibility/F5 qualification remain release gates.

M23 and M24 have separate milestone reports/evidence. Prior M18–M21 review findings remain open. M25 is not started.

## M24 review corrections
The shared institutional roster is checked against each requested product's own seat allowance when downloading. A five-seat SKU does not extend a one-seat SKU. If the enabled learner/administrator roster exceeds the requested SKU's capacity, reduce that roster before using the smaller license; the immutable owner management seat remains exempt. Independent rosters per SKU are not part of this pilot.

Final delivery effects require the current worker lease and current original payment/license/content rights. A committed refund cancels claimed work and prevents a new fixture delivery effect, even before commerce reconciliation. Already completed effects retain their original truthful receipts. See [fixes and regression commands](milestones/evidence/M24-review-fixes.md).
