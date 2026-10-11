# M02 local run, demonstration and debugging

Run from C:\dev\GeetaStudio in PowerShell. Reuse the existing local SQL Server/default instance and private M1 API/worker user secrets. No provider key, paid service, Docker or WSL is required.

## Setup and checks

~~~powershell
Set-Location C:\dev\GeetaStudio
.\scripts\db-update.ps1 -ProvisionRuntime
.\scripts\db-update.ps1 -SeedCore
.\scripts\verify.ps1 -Locked -Sql
~~~

ProvisionRuntime applies runtime and core migrations, preserves existing credentials, grants the API object-level SELECT/INSERT/UPDATE on core tables and SELECT on core migration history, and seeds fixtures. The worker gets no core permissions. SeedCore applies migrations and reruns seeds using the Windows migration identity without provisioning credentials. Both commands preserve existing records; no reset/drop/delete is used.

The current Windows account receives Administrator membership in the local fixture tenant. The isolated fixture tenant receives no such membership. Global staff Administrator does not grant access to every tenant. Existing staff mappings and database credentials stay in API user secrets; do not export those secrets.

Verification runs 31 offline and 29 actual SQL tests (60 total), locked restore/build and formatting. SQL tests use the existing dedicated GeetaStudio_M1Tests, which now contains both schemas; its name is retained from M1. Records/history are preserved.

## Run and demonstrate

Open three PowerShell terminals at the root:

~~~powershell
# API, port 5100
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
~~~

~~~powershell
# Web, port 5200
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
~~~

~~~powershell
# Worker, no HTTP port
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

Open http://localhost:5200. The shell shows API reachability and a content-foundation link. It opens the secured API in the browser using your own Windows identity; the web server does not forward a service identity as a tenant administrator.

In a fourth terminal:

~~~powershell
.\scripts\core-demo.ps1
~~~

The same create/revise/export operation runs against Geeta and the synthetic fixture. Command replay keeps the content ID; the old revision remains; stale updates return 409; a request to the isolated tenant returns 403. The helper retains demonstration records and prints IDs/outcomes only. Both Windows PowerShell and PowerShell 7 work; the helper explicitly permits integrated credentials over loopback HTTP in PowerShell 7.

~~~powershell
$staff = @{ UseDefaultCredentials = $true; Headers = @{ 'X-ContentOS-Staff' = '1' } }
if ($PSVersionTable.PSVersion.Major -ge 7) { $staff.AllowUnencryptedAuthentication = $true }
Invoke-RestMethod http://localhost:5100/api/v1/core/tenants @staff
Invoke-RestMethod http://localhost:5100/health/ready
~~~

Tenant/universe IDs come from secured metadata; membership is enforced by the server. All commands require operationId. Repeat the exact request with the same ID for an idempotent retry; a different payload with that ID is rejected. Revision/editorial commands also require expectedRowVersion. A 409 ConcurrencyConflict means reload, review the latest revision, then send a new operation ID.

Routes under /api/v1/core: /tenants; /tenants/{tenant}/universes; /tenants/{tenant}/universes/{universe}/catalog and /content. Content subroutes are /{id}, /{id}/revisions, /{id}/editorial-state and /{id}/export. Import uses /universes/{universe}/import, grants use /universes/{universe}/grants. POSTs require X-ContentOS-Staff: 1, the appropriate staff policy and tenant/universe permission.

Draft → Review → Approved requires recorded rights; Approved → Archived is supported. Revising content returns it to Draft. Import validates schema/tenant/universe/references/revision order, preserves supplied stable IDs and rejects existing ID collisions without rewriting history. Public knowledge access shares only the explicitly public approved current canonical/translation revision; it grants neither write nor export and exposes no prior private revisions. Full read/write/export grants are separate.

Fixtures contain original synthetic text and character profile data, not verified scripture, licenses or approved media. Registering metadata does not qualify those claims. Source verification is M03; full character administration/casting is M10.

## Debug

1. Open C:\dev\GeetaStudio in the already trusted VS Code folder with C# extensions.
2. Stop CLI hosts, select **Geeta Studio: all**, press F5.
3. Set a breakpoint in ContentCoreStore.CreateAsync, ReviseAsync, AccessAsync or CoreDbContext.SaveChangesAsync.
4. Run scripts/core-demo.ps1 in another terminal, inspect scope/operation/revision checks and continue.
5. Shift+F5 stops compound sessions.

Tasks: **Geeta Studio: seed core fixtures**, **Geeta Studio: M2 core demo**, existing **Geeta Studio: verify SQL**. Ports, launch profiles and compound build/stop conventions are retained.

Actual M02 CLI/API/SQL/web checks ran. A separate interactive M02 F5/breakpoint session was not performed; prior M0 F5 evidence remains. Hosted CI, cold checkout/cache, Azure SQL and production/live-provider qualification were not verified. M03 has not started.

M03 compatibility note: source-derived export, approval and public reads now enforce reviewed source bindings through M03. The current run/debug guide is M3_LOCAL_RUN.md; M02's 60-test count and next-milestone notes above record its historical checkpoint.
