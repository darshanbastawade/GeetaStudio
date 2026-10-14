# M19 local analytics run and debug

Run from C:\dev\GeetaStudio using the pinned .NET10 SDK and existing local SQL Server. M19 adds operational reporting, validated evidence imports and disconnected fixtures; no analytics account, social credentials or payment account is required.

~~~powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
~~~

Use the existing VS Code **Geeta Studio: all** F5 compound and open http://localhost:5100/staff/analytics under your authorized Windows account. Select the owned tenant/universe, currency, and UTC date range. Dates in this report are UTC, including the full through date. Read membership is required for aggregate reports; Administrator membership is required for financial writes, raw ledgers and consent controls. The API keeps the existing CSRF header, authentication, no-store/noindex and exception handling. No new background processor or standalone Worker permission is needed: imports/capture/reporting are synchronous, bounded and transactional.

## Controlled Windows demonstration

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\analytics-demo.ps1 -ManageApi
~~~

The script creates a new isolated synthetic scope only in GeetaStudio_M1Tests. It uses the existing editorial technical fixtures, owned source permissions and local analytics adapters; does not create public content or a real financial transaction. It proves recognized INR1540, cost INR120, settled INR820 and estimated INR200. The USD sponsorship uses the explicitly synthetic fixed rate80, not a current market quote. It reimports identical economic events through fixtureB without changing historical fixtureA source IDs, confirms subscription revenue independently attributed to Instagram, Unknown acquisition, snapshot engagement120 instead of100+120, immutable forecast versions and API restart.

All owned hosts stop and environment overrides are restored. Port5100 must be free before starting. The approved WATCH renderer configuration from the existing M17 setup must still be present because shared test service registration includes it; this demo renders no video or speech. Outputs: .local/m19-demo/{fixture,reimport,report}.json and private API logs. Repeated demonstrations create separate clearly labeled fixture scopes, not real revenues.

For focused checks after Test migration/build:

~~~powershell
$env:GEETA_SQL_TESTS = '1'
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlAnalytics|FullyQualifiedName~AnalyticsRuleTests'
~~~

## Debug

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\analytics-demo.ps1 -ManageApi -DebugPause
~~~

Or run **Geeta Studio: M19 analytics demo**. At the pause, choose **Geeta Studio: M19 attach Api**, F5, and the printed API process. Open /staff/analytics with the printed tenant/universe. Useful breakpoints: AnalyticsWorkbench.ImportAsync/Insert for duplicate/conflict/permission/coverage, ReportAsync for independent dimensions/FX, CaptureAsync for original cost ownership, ForecastAsync/ForecastsAsync for versioned assumptions. Continue/detach and press Enter in the terminal to complete duplicate and restart checks and cleanup. The demo processes no publishing jobs. Fresh interactive F5/breakpoint/usability checks are not claimed unless separately performed.

## Evidence workflow and reporting definitions

Register a source version (analytics.import.v1 for validated manual evidence, analytics.fixture.a.v1/b.v1 for synthetic fixtures). Source system/account and previous records remain pinned; changing an adapter appends a profile. Disable the specific source version through POST /source/{id}/control with expected rowversion and evidence. No automatic vendor/network fallback exists. Source evidence is an attestation to imported evidence, not OAuth qualification.

Register metric definitions with key, version, unit, explicit description and Delta, Snapshot or Gauge aggregation. Delta windows must be disjoint. Snapshot/Gauge updates append different observation times for the same exact window. Latest observation of each window replaces earlier observations in the report, not in history. Different accounts, source systems, revision/publication/campaign series and definition versions are separate. A Gauge returns the latest available observation; Snapshot sums disjoint window snapshots and Delta sums disjoint deltas. Cross-boundary windows are marked partial and never prorated silently. Coverage Available/Partial carries a number; Unavailable carries null. Reports promise only supplied windows, not complete population or platform totals. Reimport uses original event/economic identities and cannot update values silently.

The private JSON evidence form accepts owned contracts in src/ContentOS.Contracts/AnalyticsContracts.cs, and adds a new operation ID if omitted. .local/m19-demo/reimport.json is a concrete ready-to-import example for its printed scope. Economic keys must come from stable originating sale/charge/cost/observation records, never fresh random keys on retry. Finance imports require gross, taxes, pass-through, refunds, fees, currency, earned/estimated status, provenance and explicit acquisition. RevenueStream is independent of AcquisitionChannel. No device/personality inference or guessed attribution is performed. All supported streams are listed in AnalyticsRules.RevenueStreams; costs distinguish MediaSpend, AiTool, Production, Review, Rework and OperatingOverhead.

Revenue net = gross minus taxes, pass-through, refunds and fees. Surviving fees can produce negative net receipts after a full refund. Settlements reference the original earned revenue, same currency/FX/dimensions, unique settlement reference and capped total; they reconcile net receipts and never create revenue. The selected-period recognition-minus-settlement difference is not an invoice outstanding balance. Late refunds append a positive Refund amount linked to the original earned sale, same currency/FX/stream/acquisition; recognition deducts that amount without rewriting the sale or counting a second refund. Already settled funds remain in the reconciliation difference for finance follow-up. Estimated entries never count as actual. Amount corrections with existing identities conflict; supply authoritative new economic adjustment evidence and reconcile externally. This is an operational ledger, not audited statutory accounting, a payment processor or revenue-recognition policy engine.

FX uses immutable keyed/versioned evidence records with source/target currency, effective time, positive rate and provenance. Each event pins its FX version and frozen reporting amount. New rates never revalue history automatically. A report can use original currency or that event's captured target currency; missing conversions are explicitly excluded and counted. Settlement uses the original revenue FX binding; actual bank FX differences need finance reconciliation and are not invented. Synthetic rates never qualify live FX. No automatic paid FX service.

**Capture owned production costs** reads completed M7 AI charges, finalized M16 media charges and accepted M17 renderer charges under authoritative owner keys. WATCH voice/image costs are owned by M16 and never counted a second time through stage summaries. Completed authoritative zero-price work is captured as known zero, distinct from missing or unknown charges. Unresolved usage and uncaptured charges are visible. M1 probe usage has no currency and is not presented as money. Factory budget/ceiling aggregates and staff time are not actual expenses: import authoritative allocated review/rework/overhead costs when available. Unallocated AI costs stay universe-level; no allocation is invented.

Variant snapshots bind exact revision, audience/locale, content mode/format, trend brief/cluster and cast revisions. Discovery source is explicit imported evidence. Current approval is separately labelled from historically approved outputs. Cost per approved article/episode uses a distinct approved revision denominator and directly bound actual production/review/rework costs; overhead remains separate. Episode costs bind the exact WATCH production and require its current master approval; script approval alone does not approve an episode. Fixture master approval remains technical, not public human acceptance. Selected-period directly allocated episode costs are separate from unallocated review/rework/overhead. Time-to-publish uses first recorded M18 Published history minus content revision creation, including explicit fixture history only within fixtures. Correction rate counts distinct affected revisions represented in the report. Engagement retains series/definition/unit, and feedback is observational.

Cohorts require administrator-recorded bounded AnalyticsCohorts consent, pseudonymous subject:{internal random UUID} or fixture: references and evidence. Fixture consent cannot label genuine activity. Consent must precede activity; disable is irreversible and excludes linked analytical results even for historical as-of reports. Finance totals persist for reconciliation. Cohort membership follows first acquired event in the query period and explicit acquisition channel/month; conversion is an observed later converted event; retention is an observed return at least30 days later for matured subjects. All subaggregates require five subjects. Learning uses each subject's mean0..1 feedback, not proof of learning. Session costs require matching consent/session cost and activity evidence for every observed session; otherwise they are unavailable. No personal data, credentials or real consent was fabricated; synthetic cases are marked Fixture. Raw entries are private Administrator-only; reports disclose no subject references. GET /entries defaults to100 records; use skip/take (maximum1000) and a fixed asOfUtc to page through immutable finance evidence with fingerprints.

Reports use [startsUtc, endsUtc), both occurred time and received as-of, and a10000-entry operational limit rather than silent truncation. Current consent revocation/current approval are intentionally current even in historical queries and labelled. Missing or estimated cost/FX and unallocated costs leave coverage incomplete.

Forecasts append editable key/version assumptions: audience, conversion, price, retention, periods, payout eligibility/rate, media/AI/overhead/review cost and low/high multipliers. Platform payout is zero when eligibility is false; it is separate from subscription or commerce earnings. Every assumption/evidence/version remains inspectable. Retention is a geometric retained-audience model, not an observed projection guarantee. Actual comparison filters stream plus acquisition channel and uses recognized net actuals; forecasts are gross, so deduction differences remain explicit. Cost comparison is acquisition-channel-wide without invented cross-stream allocations. Missing actuals/FX remain unavailable/partial. These scenarios make no guaranteed earnings or causal performance claims.

Live analytics accounts/OAuth/app review/vendor metric definitions and coverage, genuine finance exports/settlements/FX, consent/privacy/data retention review, genuine learning feedback and M12/public/source/human gates remain open. No payment/subscription/entitlement or M20 implementation. Browser/mobile/screen-reader and fresh interactive debugging require separate verification.
