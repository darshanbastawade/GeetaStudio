# Windows and VS Code development — M0 foundation through M09

For audience lessons, reviewed translation memory and casting, follow [M9_LOCAL_RUN](M9_LOCAL_RUN.md). For current workflows, review and worker restart/debugging, follow [M8_LOCAL_RUN](M8_LOCAL_RUN.md). For AI tasks, provider routes, budget accounting and debugging, follow [M7_LOCAL_RUN](M7_LOCAL_RUN.md). For knowledge retrieval/index migration/debugging, follow [M6_LOCAL_RUN](M6_LOCAL_RUN.md). For claim/evidence setup/demo/debugging, follow [M5_LOCAL_RUN](M5_LOCAL_RUN.md). For corpus setup/demo/debugging, follow [M4_LOCAL_RUN](M4_LOCAL_RUN.md); [M3_LOCAL_RUN](M3_LOCAL_RUN.md) covers sources. For core setup/demo/debugging, follow [M2_LOCAL_RUN](M2_LOCAL_RUN.md); [M1_LOCAL_RUN](M1_LOCAL_RUN.md) covers runtime recovery. The M0 instructions below describe the database-free, runtime-disabled baseline. To select it after M1 provisioning, set `$env:Runtime__Enabled = 'false'` separately in the API and worker terminals; remove that override to return to M1.

Run from `C:\dev\GeetaStudio` in PowerShell. M0 needs the stable .NET SDK selected by global.json (10.0.401 or later compatible patch), Git for review, and VS Code with the recommended C# / C# Dev Kit extensions for F5. No SQL Server, database connection, Docker, WSL, provider key, or cloud account is required. First restore requires NuGet.org or a populated cache.

## Restore, build, and verify

```powershell
Set-Location C:\dev\GeetaStudio
.\scripts\dev-doctor.ps1
.\scripts\verify.ps1 -Locked
```

The doctor checks SDK selection and loopback ports without installing software or changing settings. It exits 1 when SDK/port prerequisites fail; occupied ports are expected while hosts are already running. It reports Git/VS Code availability but does not prove extension/debugger readiness.

`verify.ps1 -Locked` executes this sequence and stops with the first nonzero exit code:

```powershell
dotnet restore .\GeetaStudio.sln --locked-mode
dotnet build .\GeetaStudio.sln --no-restore
dotnet test .\GeetaStudio.sln --no-build --no-restore
dotnet format .\GeetaStudio.sln --verify-no-changes --no-restore
```

The latest verification counts and outcomes are in [PROJECT_STATE](PROJECT_STATE.md) and [M09 evidence](milestones/M09.md). Add -Sql for the real local SQL integration suite and -LowMemory to limit build resources on a constrained workstation. No live-provider tests or calls run. Use `verify.ps1` without `-Locked` only for an intentional dependency change, and review the lock-file diff. To fix formatting intentionally: `dotnet format .\GeetaStudio.sln --no-restore`.

If the workstation's execution policy blocks local scripts, use a process-scoped invocation such as `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked`. This does not change machine/user policy. Both Windows PowerShell and PowerShell 7 can run the helpers.

## Run all three hosts

Open three PowerShell terminals at the repository root after a successful build:

```powershell
# Terminal 1: API (loopback only)
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
```

```powershell
# Terminal 2: server-interactive Blazor web
dotnet watch --project .\src\GeetaStudio.Web --launch-profile Development
```

```powershell
# Terminal 3: worker (no HTTP port)
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
```

`dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development` is the verified non-watch alternative. Profiles set DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT to Development consistently. Web calls API through `Api:BaseAddress` in appsettings.Development.json, never through direct database access.

Open [the shell](http://localhost:5200). It shows the development/fixture banner, real API reachability, a timestamp, and **Check again**. The worker logs that it is idle in M0.

```powershell
Invoke-WebRequest http://localhost:5100/health/live | Select-Object StatusCode, Content
Invoke-WebRequest http://localhost:5100/api/v1/development/status | Select-Object StatusCode, Content
```

Liveness returns the owned health response with status Healthy and UTC timestamp. The development status is an explicitly labeled deterministic fixture; the route does not exist outside Development. There are no readiness/dependency checks until M1 introduces required dependencies. PowerShell's Invoke-RestMethod may convert a parsed timestamp to local time; inspect raw Content to see the wire UTC offset.

For an outage demonstration, stop only Terminal 1 with Ctrl+C, click **Check again**, and observe the safe unavailable/timeout message. Restart the same API command and click **Check again** to recover without restarting the web app. Stop each foreground host with Ctrl+C; the worker logs clean shutdown. No helper kills unrelated dotnet processes.

## Debug in VS Code

1. Open the repository root (`code C:\dev\GeetaStudio`). If VS Code shows **Restricted Mode**, the owner must review and trust this folder before tasks/debugging can run. This is a security decision, not an application setting.
2. Ensure C# and C# Dev Kit from `.vscode/extensions.json` are installed. Stop existing hosts on 5100/5200 before F5.
3. In Run and Debug, select **Geeta Studio: all**, then press F5. The compound restores locked dependencies and builds before launches. API, Web, and Worker launch entries use built net10.0 DLLs, each host's directory, explicit Development environment, and matching loopback URLs. Individual entries also build before launch. The compound builds once, then uses hidden equivalent host entries with no extra pre-launch build tasks; these internal entries do not clutter the normal selector.
4. Put a breakpoint on the `new HealthStatus(...)` line in `src/GeetaStudio.Api/Program.cs`. Open `http://localhost:5100/health/live`, or click **Check again** in the shell. The API should pause at the breakpoint. Inspect values, then F5 to continue. A long pause may cause the web's deliberate 3-second timeout; click **Check again** after continuing.
5. Press Shift+F5 / Stop. The compound's `stopAll: true` stops every debug session it created. It does not terminate unrelated processes. Check ports with the doctor after stopping.

The Tasks menu includes restore, build, focused tests, full verification, and a foreground run task for each host. Run tasks use the same Development profile and depend on build. Terminate those tasks via their terminal's Ctrl+C or VS Code Terminate Task; debug Stop only controls debugger-launched processes.

### Configure different ports

If ports are occupied by another application, do not kill it. Change Development `applicationUrl` in each affected `Properties/launchSettings.json`; change its `ASPNETCORE_URLS` in `.vscode/launch.json`; change web `Api:BaseAddress` when the API moves; update doctor's port list, config checks, and this runbook together. All three launch paths remain consistent. For a temporary CLI-only override:

```powershell
# API terminal
dotnet run --project .\src\GeetaStudio.Api --launch-profile Development -- --urls http://localhost:5101
# Web terminal
$env:Api__BaseAddress = 'http://localhost:5101/'
dotnet run --project .\src\GeetaStudio.Web --launch-profile Development -- --urls http://localhost:5201
Remove-Item Env:Api__BaseAddress
```

Outside Development, web must have an explicitly configured HTTPS API address; fixtures are unavailable. These local HTTP profiles do not qualify production security.

## Architecture negative demonstration

After building, run:

```powershell
.\scripts\test-architecture-gate.ps1
```

The helper saves the Domain project bytes, temporarily inserts a synthetic `Vendor.DeliberateM0Probe` PackageReference, runs only the repository architecture test without restore/build, requires the expected failure, restores the original file in finally, then reruns architecture tests successfully. It does not download or call the synthetic vendor. Do not edit that project concurrently. A failed final check exits nonzero.

## M0 verified versus pending (historical foundation)

See [M0 evidence](milestones/M00.md) for actual run results, including browser outage/recovery, Ctrl+C, and the now-verified Windows compound/breakpoint/Stop checks. Hosted GitHub CI has not run; the same verification command passes locally. No SQL/provider/public-release readiness is claimed. M1 has not started.
## M1: SQL-backed runtime

M1 is implemented. Follow [the verified M1 setup/run/debug/recovery runbook](M1_LOCAL_RUN.md). It documents private staff/SQL configuration, additive migrations, real SQL acceptance, deterministic provider switching, explicit reconciliation, and process-crash recovery.

## M03 source registry

Use [M3_LOCAL_RUN.md](M3_LOCAL_RUN.md) for source registration/review, intended-use export and revocation demonstration. The existing API/Web/Worker Development profiles and **Geeta Studio: all** F5 compound are retained. Run scripts/db-update.ps1 -ProvisionRuntime, scripts/verify.ps1 -Locked -Sql, start hosts, then scripts/sources-demo.ps1. The source staff page is http://localhost:5100/staff/sources. M03 Windows HTTP passed; interactive UI/F5 was not repeated.
