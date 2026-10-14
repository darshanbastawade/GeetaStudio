# M20 local accounts, households, Geeta+ and credits

Run from C:\dev\GeetaStudio with the existing Windows/.NET10/local SQL setup. M20 extends the modular monolith; customer authentication does not grant staff roles or tenant administration. Persistent child personalization, real payment entitlements and public commercial launch remain gated. Local adult responsibility is an explicit self-attestation, not verified age or regional legal qualification.

## Build, migrate and demonstrate

~~~powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\customer-demo.ps1 -ManageApi
~~~

The demonstration creates a separate synthetic scope only in GeetaStudio_M1Tests. It generates a private signing key in the process environment without printing/storing the key, admits only one of two competing80-credit reservations against100 credits, releases failed work exactly once, links two verified fixture identities without replacing the internal user, and prepares signed test payment evidence. Windows-authenticated API requests replay purchases, refund a credit pack through its original provider, apply an older Paid event without resurrecting access, cancel a subscription, capture finance twice, restart the API and recheck balances, household isolation and consented progress. All prices/credits/provider receipts are fixtures; no payment gateway, email/SMS provider or external identity account is contacted.

Final demo expects100 available customer credits,100 reversed credits,0 held credits, cancelled subscription features empty and INR10 recognized synthetic revenue (two INR10 purchases minus one INR10 refund). Credits are integer customer units, never INR or the M7 provider-spend budget. There is no settlement/bank deposit inferred from a Paid event. Test subscriptions end immediately on canonical cancellation; actual provider mapping/proration/paid-through policy requires live qualification.

Port5100 must be free. Owned API processes are hidden and stopped in finally; existing hosts are never killed. Overrides, including the signing key, are restored. Fixtures/logs: .local/m20-demo. Repeating the demo creates a new isolated fixture scope. No fixtures are imported into Development.

Focused checks after building and migrating the Test database:

~~~powershell
$env:GEETA_SQL_TESTS = '1'
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlCustomer|FullyQualifiedName~CustomerRuleTests'
~~~

## Run and debug

Open C:\dev\GeetaStudio in VS Code. Use the preserved **Geeta Studio: all** F5 compound (API5100, Web5200, Worker); open http://localhost:5100/account under the authorized Windows account. /staff/customers is Administrator-only. Enter the existing/printed workspace identifier. Register the verified local account and explicitly attest adult responsibility before creating a household. Invite other verified adult account IDs; the recipient must accept. An owner can transfer ownership. Adult profiles stay private to their account; household parents can manage child/teen profiles. Advertising profiling is always false.

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\customer-demo.ps1 -ManageApi -DebugPause
~~~

At the pause choose **Geeta Studio: M20 attach Api**, F5, and the printed API process. Open /account with the printed workspace. Breakpoints: CustomerWorkbench.Self/Proof/RecoverAsync; Member/ProfileAccess/Consented; ReserveAsync/Finish/SettleAsync; SubmitAsync/WebhookAsync/Apply; AnalyticsWorkbench.CaptureCustomerPaymentsAsync. Continue/detach and press Enter to complete replay/restart/cleanup. Tasks: **M20 focused checks** and **M20 account demo**. Configuration and actual HTTP checks are separate from fresh interactive breakpoint and visual/accessibility verification; the latter are not implied.

Normal Development has only the trusted host identity and private inbox adapters. Payment/alternate identity fixtures are registered only in Development with an explicit Customers:FixtureSigningKey of at least32 characters; no key is generated silently by the application. The demo supplies a fresh32-byte random key through Customers__FixtureSigningKey. Do not persist a test key in repository configuration. For a deliberate repeated private test host, keep its key in private environment/user secrets. Signing helpers live in Integrations and test scripts; no public signing endpoint exists. Changing a fixture key invalidates old signatures, not existing accepted SQL evidence. Live identity/payment credentials must use a qualified adapter and secret-reference integration; a test key is not vendor qualification.

## Operators and evidence

Customer endpoints: /api/v1/customer/tenants/{tenant}. Server derives ownership from the authenticated principal; requests never supply an authoritative internal user ID. Register/me, single-use identity challenges/link/recover, households/invite/accept/transfer, profiles/edit/delete, consent/progress, data export/delete, products/checkout/orders/submit/reconcile/change, entitlements/wallet/reservations/cancel and private notifications are supported. Ambient Windows mutations require X-ContentOS-Staff:1. Responses are private/no-store; no browser-provided payment success or running-generation settlement endpoint exists.

Administrator endpoints: /api/v1/customer-admin/tenants/{tenant}. Every catalog/route/grant/work command also checks actual tenant Administrator membership. /staff/customers accepts bounded owned evidence files for product versions, route versions, credit grants and signed local payment events. Contracts are in src/ContentOS.Contracts/CustomerContracts.cs; scripts/fixtures/CustomerDemo.cs supplies working examples. Money prices are integer minor units for the implemented INR/USD/EUR catalog; immutable product snapshots carry feature keys, expiry days and an optional exact owned universe for finance allocation. Unknown acquisition stays Unknown; products without a universe remain tenant-level/unallocated and are not duplicated into each universe report.

Register route versions, then POST /routes/{id}/control with operationId/enabled/active. Activation routes new checkouts only; existing orders/customer/subscription references retain their original provider/account/Test environment and product snapshot. Disabled routes block new dispatch; reconciliation continues against original resources. Cancel/refund requests are persisted before original-provider submission. An uncertain change is reconciled before another request; no gateway fallback, automatic mandate/token migration or blind retry occurs.

Payment fixtures normalize signed canonical evidence. /payment-events exists only in Development and is StaffAdmin-only. The signature covers the raw payload and fresh Unix delivery timestamp (five-minute window); event/resource sequence, environment, customer, order, amount, currency, resource and original subscription period are checked. Unique event IDs and provider sequence prevent replay/contradictory histories. Submit/Pending/client claims never grant access; only verified Paid/current-period evidence grants fixture feature keys. Delayed older states are retained without overwriting newer cancellation/refund. Refund totals are cumulative and bounded by original price; reversals cannot mint revenue/credits. Production payment integration, token refresh, actual webhook verification, commercial/refund/tax policies and sandbox testing remain unqualified.

Reserve credits per profile and stable usage key; tenant serialization and SQL uniqueness prevent double spending. Unused grants/purchases expire by lot; queued reservations expire after30 minutes. Cancellation releases only undispatched work. A trusted service Administrator records /reservations/{id}/dispatch with exact provider/account/resource before generation. /settle requires that original binding and a recorded Completed/Failed/Unknown outcome; actual consumed credits are bounded by the reservation. Unknown keeps credits held; Completed consumes actual units and releases excess; Failed releases once. Replays cannot change final consumption. The customer cannot settle running work from a claimed result. Future TALK/CREATE milestones must connect their real receipts to this port; M20's controlled receipt demonstrates accounting and does not implement those products. Refund of already consumed purchased credits creates visible debt and blocks new reservations. Expired credit adjustments avoid charging debt for unused expired credits; history is never rewritten.

Profile consent is purpose-specific, expiring, parent-authorized for minors and rechecked with current household membership/active issuer. Progress updates require rowversion and consent; withdrawing Progress erases stored progress. Child personalization remains disabled even with a parent consent record until reviewed launch requirements are implemented. Adult personalization needs current explicit consent; no advertising-oriented profile exists. Export excludes other adults' private profiles/finance. Deletion erases display/profile/household labels and progress, disables sign-in/membership, and cancels undispatched reservations. Shared ownership must be transferred and running/unknown work, active subscriptions and payment actions reconciled first. Minimal pseudonymous identity tombstones, consent/audit and financial/credit evidence remain; actual regional retention/erasure/provider cleanup rules require launch review. No claim of legal compliance is made by fixtures.

Payment-failure/refund/cancel notices are essential private in-app records with stable internal recipient IDs and deduplicated inbox receipts. No marketing, email or SMS is sent. The Notifications consent purpose is reserved for optional delivery; adding external delivery requires recipient/consent/secret/provider qualification.

M19 /staff/analytics adds **Capture test customer payments**, scoped to immutable product universe allocation. It imports the original gross price exactly once and append-only cumulative refund deltas under reserved owned keys; older provider observations cannot revise captured entries. Sources are labelled fixture and acquisition remains independent of subscription/TALK/CREATE revenue stream. Missing FX stays explicit; no new FX quote, tax/fee evidence, settlement or bank balance is fabricated. Real finance reconciliation requires qualified records.

## Remaining gates

See docs/milestones/M20.md for commands/results actually run. Offline fixtures do not qualify live identity/recovery/age assurance, gateway charges/mandates/refunds, genuine consent/retention, external notifications, M12 human/public rights, accessibility or production deployment. Full SQL reliability from earlier milestones is tracked separately; targeted reruns do not imply an all-green combined run. M21 is not started.
