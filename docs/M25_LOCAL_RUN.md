# M25 hardening and operations

Scope: the existing controlled private product. This is an Offline Verified baseline, not a production authorization. `ops/release-baseline.json` lists each enabled private feature, its evidence and individual production gates. `scripts/release-check.ps1 -RequireProduction` deliberately fails while required qualifications are absent. A small future READ release need only qualify its selected features; do not enable unrelated paid/voice/commerce capabilities.

## Local checks and debugger

```powershell
Set-Location C:\dev\GeetaStudio
dotnet restore GeetaStudio.sln --locked-mode
dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
$env:GEETA_SQL_TESTS='1'
dotnet test tests/ContentOS.Foundation.Tests/ContentOS.Foundation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Hardening'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/hardening-recovery-check.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/hardening-demo.ps1 -ManageApi
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release-check.ps1
```

Run SQL checks/drills serially with API5100 free. Dedicated Test database only; no reset. Recovery makes a copy-only checksum backup and a uniquely named disposable restored database, compares source permissions/lineage, reader bindings, credit ledger/reservations, payment/commerce orders and acknowledged delivery effects; tests transaction rollback and additive forward fix only in that restored database, then removes its exact owned restore/backup. It does not claim a production offsite recovery or old application binary compatibility. No `REPLACE` and no existing database drop. RPO is zero only for the quiesced test snapshot; compare before/after while other writers are stopped. Production proposed RPO15min/RTO5min require actual log and asset backup drills.

`-DebugPause` on hardening-demo starts its owned API and prints PID. Use existing **Geeta Studio: M25 attach Api**, set breakpoints in OperationalBoundary/TalkAdmission/ProblemsAsync/AnalyticsWorkbench.ReportAsync; continue and Enter to finish. F5 **Geeta Studio: all** retains API5100/web5200/Worker. New interactive breakpoint execution is not claimed. Additive privacy migration redacts legacy copied problem text; it cannot restore deleted passages. Apply to Development with `scripts/db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild`.

## Environment and packaging

Use exact SDK10.0.401 with installed patched shared runtimes10.0.13. API/Web Negotiate10.0.13 and transitive Pkcs9.0.21 are narrowly pinned for 13 October advisories. SDK selection remains unchanged. `scripts/deployment-package.ps1` produces distinct Release API/Web/Worker directories and SHA256 manifest under an unused `.local/releases/<uuid>`; no install, service change or deployment. Keep the previous artifact directory for rollback. Runtime defaults false; separate Development/Test/Production databases, asset roots, OS service identities and secret stores. No fixtures/signing keys in Production. Do not copy Development user-secrets or Test signing material to a release.

Modest intended hosting: one Windows/IIS host behind qualified HTTPS, separate API/Web app pools and Worker identity; SQL only reachable on private network. Provision schema using a separate migration identity, then remove its access. API restricted column grants; Worker runtime-only grants; no dbo/DELETE grants. Expose HTTPS ingress only, allowlist hostnames, configure a real certificate/HSTS and Negotiate Kerberos SPN/channel behavior before any public or cross-machine use. Do not accept arbitrary forwarded headers/proxies or open SQL/asset directories publicly. No evidence justifies AKS/autoscaling here. Configuration is an environment contract; actual host/network/IIS deployment remains unverified.

## Retention and deletion boundaries

- TALK keeps adult opted-in history within bounded policy hours; minors/no-memory text is transient. Account/profile/forget/expiry clear stored question/answer and memory; problem diagnostics now store identifiers only and legacy copied sources are redacted. Source rights are rechecked when returning conversation evidence. Financial/run hashes survive for reconciliation, not conversational display.
- CREATE deletion clears input, revisions and upload bytes/alt, invalidates licensed reachable commerce copies, queues explicit local cleanup. Downloaded owner files are outside server recall; no provider training permission exists. Existing media/source/retrieval correction gates and indexed source filtering remain mandatory.
- READ anonymous engagement is minimized and swept; customer progress/learning uses current consent and minimum-five cohort suppression. Audit logs must contain bounded status/type/IDs only; new HTTP metrics contain only surface and status class, never URLs/query/body/identity.
- SQL backups contain data prior to deletion. Do not serve a restore until migrations/privacy sweeps and deletion reconciliation run. Quarantine restored copies, restrict/encrypt backups, maintain a separately backed-up deletion ledger or bounded expiration, and qualify jurisdiction-specific financial/audit/legal retention before production. Offline recovery temporarily retains a checksum backup only for its drill and removes it. Production backups/assets/external provider cleanup/search/log sinks need reviewed owner policy; no legal durations or completed external erasure are invented.

## Incidents, recovery, switches and alerts

Freeze admission first: disable affected route/profile/collection/provider through existing authorized control endpoints; stop only the affected generation/publishing worker if necessary. Preserve original dispatch IDs/snapshots and unknown state. Never reroute/resubmit an ambiguous payment/publication/delivery: reconcile original provider/account/resource and verified receipts; restore access only after scoped rights/approval checks. Cancel queued private jobs using existing owner controls; reservation recovery releases only unused credits, accepted work settles once. A deployment rollback must stop writers, retain acknowledgments, restore compatible code/data/assets to an isolated candidate and compare lineage, balances and effects before cutover. Never apply destructive Down to an active commerce table. Prefer additive forward fix; M25 privacy redaction is irreversible. Old binary deployment rollback and active production log replay remain gates.

Rotate secrets through secret references, never logs/chat/config tracked files. Disable affected route, preserve original account and replay receipts, replace key with overlapping vendor verification if supported, verify new-key acceptance/old-key rejection and reconcile pinned work before enabling admission. Fixture HMAC rotation is tested; real OAuth/token/Kerberos/service-secret rotations require their own qualification. Embedding shadow validation/cutover/rollback and asset relocation keep IDs/hashes; publishing/payment reconciliation retain original route after a switch. Existing focused SQL drills exercise these contracts with local/fixture adapters; no real vendor switching is claimed.

`ContentOS.Operations` exposes http.duration(ms) and http.responses with bounded surface/statusClass tags. The Windows probe records20 anonymous SQL-backed directory samples, p95, availability and security headers. It is a local baseline, not a monthly/load claim. Proposed targets/alerts in `ops/release-baseline.json`: reader99.9%/30days,p95<1s, runnable queue<120s, terminal generation failures<5% of20, cost variance<10%, publication backlog<300s; voice N/A for selected release. Missing observations are qualification gaps, not zero failures. Route budget fences stop exhausted spending. A production collector/notification sink and time-window queue/generation/cost dashboards must be installed/tested before release; this task sends no notifications. Treat unknown effect/SQL unavailable/redaction failure as incident requiring immediate admission hold. Escalate genuine access/privacy/financial anomalies to the project owner with redacted identifiers, not copied source or user text.

## Release decision

Check every selected feature against its milestone, threat tests, provider rights/qualification, human M12/safety, dependency/license report, retention policy and current recovery evidence. High-severity defects/required live gaps prevent its production readiness. Current result: private controlled baseline only; human, external/live, public, hosting/offsite, accessibility and sustained SLO/alert qualification remain open. Earlier four M18-M21 findings are fixed with regressions in M25; historical review reports remain unchanged.
