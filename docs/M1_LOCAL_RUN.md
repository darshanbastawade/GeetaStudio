# M1 local setup, run, debug and recovery

M2 now extends this runtime with core persistence and fixture setup; see [M02 local guide](M2_LOCAL_RUN.md).

Run from `C:\dev\GeetaStudio`. M1 uses installed SQL Server's default local instance, Windows authentication for the migration identity, and a dedicated `GeetaStudio_Dev` database. No Docker, WSL or paid provider credentials are needed. SQL Server must permit the setup identity to create this database and two application logins. Local restricted SQL logins need mixed authentication; the script does not change server authentication mode. A differently managed installation must supply approved runtime identities privately.

## Setup and checks

Stop existing hosts before building or migrating. On this workstation setup is already complete; repeating it preserves migrations, database data, existing login passwords, asset-root settings and staff mappings.

```powershell
Set-Location C:\dev\GeetaStudio
.\scripts\dev-doctor.ps1
.\scripts\db-update.ps1 -ProvisionRuntime
.\scripts\verify.ps1 -Locked -Sql
.\scripts\test-doctor.ps1
.\scripts\test-architecture-gate.ps1
```

`db-update.ps1` defaults to localhost / GeetaStudio_Dev. It applies additive EF migrations using your Windows identity, never resets/drops a database, and refuses remote/unapproved targets. `-ProvisionRuntime` creates separate `GeetaStudioApi_Dev` and `GeetaStudioWorker_Dev` SQL logins and narrowly scoped grants. It generates passwords privately and saves each connection only in that host's .NET user secrets. Existing logins without matching local secrets fail clearly rather than having their passwords reset. The host identities have no schema-change/server-admin/delete permissions. The worker can read provider configuration and update only circuit counters, alongside job/attempt/outbox/budget/audit state.

The setup enables `Runtime:Enabled` in host user secrets, configures `Assets:Root` beneath the repository's ignored `.local`, and initially maps the setup Windows account to staff Administrator. Edit private configuration to add staff accounts under `Staff:Administrator`, `Staff:Operator`, or `Staff:Reader`; role mappings are explicit and do not inherit Windows group roles. Never paste credential values into source, commands in this document, screenshots or chat. Do not run application hosts with the migration identity's integrated SQL connection.

`verify.ps1 -Locked` runs build, 31 offline tests, secret/architecture/configuration guards and formatting without SQL. `-Sql` additionally migrates the separate `GeetaStudio_M1Tests` database and runs 29 real SQL tests (M1 plus M2). Its runtime-permission checks require development provisioning first. It preserves existing test records and uses cancellation/reconciliation to clean up prior unfinished test jobs. SQL unavailability fails this profile; there is no in-memory substitute. Hosted CI invokes only the offline profile; SQL acceptance is explicit and local.

If execution policy blocks local scripts, invoke them through `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql`; this is process-scoped and does not change machine policy.

## Run

Open three terminals at the root after verification:

```powershell
# API
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
```

```powershell
# Blazor web
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
# For web development, dotnet watch --project .\src\GeetaStudio.Web --launch-profile Development
```

```powershell
# SQL worker, no HTTP port
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
```

Open `http://localhost:5200`. The shell checks real API liveness. `http://localhost:5100/health/live` proves the API process is alive; `/health/ready` additionally requires SQL access and applied migrations. Runtime-disabled readiness returns 503 intentionally. Staff operations use Windows Negotiate authentication on the loopback API.

Run the secured configuration/reconciliation demonstration while API and worker are running:

```powershell
.\scripts\runtime-demo.ps1
```

It registers the two deployed deterministic probe adapters, creates immutable revisions, activates A, freezes a scheduled job under A, activates B without rebuilding, and verifies new work uses B while the old job retains A. It then submits an `unknown` probe, explicitly requests reconciliation, and verifies the original operation binding is retained. It leaves B active and preserves all records. No text generation or real external submission occurs.

For direct staff inspection:

```powershell
$staff = @{ UseDefaultCredentials = $true; Headers = @{ 'X-ContentOS-Staff' = '1' } }
# PowerShell 7 requires explicit consent for integrated authentication over local HTTP.
if ($PSVersionTable.PSVersion.Major -ge 7) { $staff.AllowUnencryptedAuthentication = $true }
Invoke-RestMethod http://localhost:5100/api/v1/runtime/adapters @staff
Invoke-RestMethod http://localhost:5100/api/v1/runtime/providers @staff
Invoke-RestMethod http://localhost:5100/api/v1/runtime/active-route @staff
Invoke-RestMethod http://localhost:5100/api/v1/runtime/jobs @staff
Invoke-RestMethod http://localhost:5100/api/v1/runtime/budgets @staff
Invoke-RestMethod http://localhost:5100/api/v1/runtime/audit @staff
```

Do not carry that HTTP consent to a remote server; the demo helpers reject non-loopback API roots. Mutating requests must include `X-ContentOS-Staff: 1`, preventing ambient Windows credentials from authorizing a cross-site form submission. No permissive CORS configuration is enabled.

Administrative POST endpoints under `/api/v1/runtime`: `providers`, `providers/{id}/test`, `/enable`, `/disable`, `/kill`; `revisions` and `revisions/{id}/activate` or `/rollback`. Operations POST endpoints: `jobs`, `jobs/{id}/cancel`, `/reconcile`, `/retry`; `assets` accepts 1–1048576 raw bytes with Content-Length. GET `jobs/{id}` returns the immutable snapshot, lease/heartbeat, attempt trace/audit references and original operation binding. GET `assets/{id}` is restricted to its owner and verifies integrity. Reader can inspect, Operator can create/cancel/reconcile jobs and own assets, Administrator can additionally change provider/routes. Unauthorized users receive 401/403; denied operations are audited.

Provider enable clears a kill switch but does not erase history or reset an open circuit. Kill/disable blocks subsequent calls, including reconciliation through that connection. An already submitted operation remains bound to its original provider and cannot silently switch to B. Enable it deliberately to reconcile when safe. `retry` creates an audited new job from a terminal dead letter using the currently active route; it never resubmits Unknown work. Cancellation of already submitted work retains uncertainty until reconciliation establishes its outcome.

Only `local:test`, `runtime.probe`, contract v1, `en-IN`, and deployed test.a/test.b adapters are accepted. Secret references may name `env:GEETA_PROVIDER_<NAME>` or `config:ProviderSecrets:<name>`; unavailable or disallowed references fail without exposing values. Local adapters require no secret. There is no real-provider/live profile to accidentally enable in M1.

## Recovery and shutdown

Ctrl+C stops each normal foreground host. Unfinished work keeps its SQL lease and operation state; a worker restart recovers expired leases. Worker submissions use a stable operation idempotency key. The deterministic ledger models a remote side effect in a separate SQL transaction; this demonstrates the recovery protocol, not universal exactly-once behavior for future vendors.

For the repeatable real-process crash demonstration, stop all existing hosts first:

```powershell
.\scripts\test-runtime-recovery.ps1
.\scripts\dev-doctor.ps1
```

The helper starts its own API and two worker processes, waits for a single submitted job with a committed effect, verifies the owning PID's command line, force-stops only that owned worker, starts a replacement, and asserts the same operation completed through reconciliation with two attempts and exactly one effect. Its finally block stops only its verified owned hosts. Evidence and redacted logs are under ignored `.local/recovery/`. It requires `sqlcmd` and the local setup identity for independent effect inspection. It deliberately leaves the persisted test job/audit history in GeetaStudio_Dev and its test A revision active; prior route revisions are retained for explicit rollback.

Budget policy v1 uses conservative probe-attempt units, not currency. A job reserves its bounded route budget atomically before its outbox record is committed. Each submission consumes a unit; unresolved estimates stay reserved until reconciliation. Reconciliation does not resubmit or consume an additional submission attempt. Terminal resolution releases the unused reservation; future M7 must add real usage/pricing qualification. Safe transient failures have bounded jittered retries and persisted scheduled time; invalid input/configuration/policy failures do not automatically retry. Circuit state and per-connection concurrency limits are SQL-backed. Schedules store UTC instants plus an explicit display/schedule timezone (default Asia/Kolkata); use ISO timestamps with offsets. PowerShell may display parsed timestamps in local time even though raw JSON/database offsets are UTC.

## Debug

Open `code C:\dev\GeetaStudio`, ensure the already trusted folder and C# / C# Dev Kit extensions, stop CLI hosts, select **Geeta Studio: all**, and press F5. The compound performs one locked restore/build, then starts API/Web/Worker with explicit Development environment and their own directories. User secrets load for API/worker; web does not receive SQL credentials.

Place breakpoints in `RuntimeStore.ClaimAsync`, `CanCallAsync`, `CompleteAsync`, or `TestOperationProvider.ReconcileAsync`; invoke `runtime-demo.ps1` from a separate terminal to trigger them. A paused worker can lose its lease. For long interactive inspections, pause every worker, then resume/reconcile rather than assuming a paused lease remains owned. A paused API can trigger the shell's deliberate 3-second timeout. Shift+F5 stops all compound sessions; the doctor confirms both ports are free. Individual host launch entries retain their own build tasks. Tasks include SQL verification, migration and runtime demo.

The unchanged compound/debug configuration is covered by configuration checks and the prior actual M0 Windows breakpoint/stop demonstration. A separate interactive M1 F5/breakpoint session has **not** been repeated; actual M1 CLI hosts, Windows authentication and process-crash recovery have run.

## Verification limits

M1 is a locally verified foundation using deterministic adapters. Hosted GitHub CI, a cold package-cache checkout, a new M1 interactive debugger session, production identity/secret storage, production certificate/network deployment, and any live vendor remain unverified. No database reset, public deployment, paid call, commit or push was performed. M2 has not started.

## Scheduling fixes

Workers skip full connections so other providers can process ready jobs. Open circuits wait without consuming submission attempts or budget. A circuit opening after claim records a CircuitOpen attempt, retains the operation binding and returns the job to the queue; its original deadline still applies. No additional setup or migration is required. Restart any already-running API/worker after rebuilding to load the updated assemblies. Revalidate with scripts/verify.ps1 -Locked -Sql.