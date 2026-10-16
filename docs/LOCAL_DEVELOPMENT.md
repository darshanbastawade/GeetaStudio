Prompt8: [current release-decision command, exact evidence schema, Production route boundary and debugging](M25.3_LOCAL_RUN.md). Prompt9 now has a blocked acceptance report and scoped local drills; genuine live/human/hosting approval remains open. Development private workbench remains available by default.

Prompt 7: [one-command two-audience private fixture, bounded opt-in live staging and debugging](M25.2_PROMPT7_LOCAL_RUN.md). Genuine event/art/voice/human qualification remains pending; no new migration.

Prompt 6: [narration planning, exact voice catalog, controlled demo and debugging](M25.2_PROMPT6_LOCAL_RUN.md). Approved art/voice and human qualification remain pending; no migration.

For M27 experiments, rollback, bounded automation and debugging, see [M27_LOCAL_RUN](M27_LOCAL_RUN.md).

Prompt 5 configured RSS: [local checks, permitted-source configuration and debugging](M25.2_PROMPT5_LOCAL_RUN.md). Default intake is disabled/unqualified; uploaded/manual paths remain. No new migration is required, and fixtures do not supply publisher permission. No actual live fetch has been performed.

Prompt 4 AI factory: see [M25.2 local run/debug](M25.2_PROMPT4_LOCAL_RUN.md) for the additive migration, controlled SQL demo, compatible prompt/definition setup, usage/recovery behavior and debugger breakpoints. Enable factory/workflow processing only for deliberately configured permitted work. M25.2 and matching hosted CI remain open.

For M25 security/privacy, release gates, isolated recovery, private packaging and debugging, see [M25_LOCAL_RUN](M25_LOCAL_RUN.md).

For M24 private digital/institutional offers and original-payment/delivery debugging, see [M24_LOCAL_RUN](M24_LOCAL_RUN.md).

For the private M23 creation studio and queue/credit debugging, see [M23_LOCAL_RUN](M23_LOCAL_RUN.md).

For private M21 TALK, grounded questions, memory, credits and debug setup, see [M21_LOCAL_RUN](M21_LOCAL_RUN.md).

For M20 accounts, households, Geeta+ and credits, see [M20_LOCAL_RUN](M20_LOCAL_RUN.md).

For M19 analytics, reconciliation, controlled imports and debugging, see [M19_LOCAL_RUN](M19_LOCAL_RUN.md).

For M18 destination packages, controlled publishing recovery and debugging, see [M18_LOCAL_RUN](M18_LOCAL_RUN.md).

For M17 WATCH, pinned renderer setup, spoken fixtures and debugging, see [M17_LOCAL_RUN](M17_LOCAL_RUN.md).

For M15 trends, source review, controlled fixtures and debugging, see [M15_LOCAL_RUN](M15_LOCAL_RUN.md).

For the M14 reader, private fixture preview and attach debugging, see [M14_LOCAL_RUN](M14_LOCAL_RUN.md).

# Windows and VS Code development — current M0–M27 private implementation

For audience lessons, reviewed translation memory and casting, follow [M9_LOCAL_RUN](M9_LOCAL_RUN.md). For current workflows, review and worker restart/debugging, follow [M8_LOCAL_RUN](M8_LOCAL_RUN.md). For AI tasks, provider routes, budget accounting and debugging, follow [M7_LOCAL_RUN](M7_LOCAL_RUN.md). For knowledge retrieval/index migration/debugging, follow [M6_LOCAL_RUN](M6_LOCAL_RUN.md). For claim/evidence setup/demo/debugging, follow [M5_LOCAL_RUN](M5_LOCAL_RUN.md). For corpus setup/demo/debugging, follow [M4_LOCAL_RUN](M4_LOCAL_RUN.md); [M3_LOCAL_RUN](M3_LOCAL_RUN.md) covers sources. For core setup/demo/debugging, follow [M2_LOCAL_RUN](M2_LOCAL_RUN.md); [M1_LOCAL_RUN](M1_LOCAL_RUN.md) covers runtime recovery. The M0 instructions below describe the database-free, runtime-disabled baseline. To select it after M1 provisioning, set `$env:Runtime__Enabled = 'false'` separately in the API and worker terminals; remove that override to return to M1.

Run from `C:\dev\GeetaStudio` in PowerShell. M0 needs the stable .NET SDK selected by global.json (exactly10.0.401; roll-forward is disabled to keep SDK-provided Blazor assets consistent with the existing package locks), Git for review, and VS Code with the recommended C# / C# Dev Kit extensions for F5. No SQL Server, database connection, Docker, WSL, provider key, or cloud account is required. First restore requires NuGet.org or a populated cache.

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
dotnet test .\GeetaStudio.sln --no-build --no-restore --filter 'Category!=Sql'
dotnet format .\GeetaStudio.sln --verify-no-changes --no-restore
```

The latest verification counts and outcomes are in [PROJECT_STATE](PROJECT_STATE.md) and [M25.1 evidence](milestones/M25.1.md). Add -Sql for the real local SQL integration suite and -LowMemory to limit build resources on a constrained workstation. No live-provider tests or calls run. Use `verify.ps1` without `-Locked` only for an intentional dependency change, and review the lock-file diff. To fix formatting intentionally: `dotnet format .\GeetaStudio.sln --no-restore`.

Verification now saves separate `restore.log`, `build.log`, `offline-tests.log`, `format.log` and `offline.trx` under `.local/verification`; an explicit `-Sql` run also saves SQL test output and `sql.trx`. Use `-ResultsDirectory <path>` to retain a separate run. A failed stage stops verification and names its log. CI preserves only this generated verification directory as a 14-day artifact, including on failure; it does not upload the entire `.local` directory. Hosted success must be checked on the exact pushed revision.

On a memory-constrained workstation, the documented complete SQL equivalent runs each discovered SQL class in a fresh process. Run it serially after a successful offline build:

```powershell
pwsh -NoProfile -File scripts/verify.ps1 -Locked -LowMemory -ResultsDirectory .local/verification/offline
pwsh -NoProfile -File scripts/verify-sql-isolated.ps1 -SkipBuild -LowMemory -ResultsDirectory .local/verification/sql-isolated -SummaryPath .local/verification/sql-isolated-summary.json
```

`-LowMemory` disables build/node reuse and limits MSBuild to one node; migration tooling reuses the preceding build. Every discovered SQL test must pass with zero skips. A failed class stops the run; no empty discovery or missing-count pass is accepted. `-ResultsDirectory` and `-SummaryPath` preserve separate attempts instead of replacing older evidence. This qualifies the isolated mode only, not a failed combined-process run. See M25.1 for actual attempt results.

The repository's `.gitattributes` enforces LF for text checkouts to match `.editorconfig`, including Windows with `core.autocrlf=true`; automatic text detection preserves binary files. It does not rewrite existing local edits. A fresh checkout needs no global Git configuration change. See [M25.1 Prompt 1 diagnosis](milestones/M25.1.md) for the reproduced baseline failure and remaining hosted-CI limits.

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

## M10 characters and casting

See [M10 local run/debug guide](M10_LOCAL_RUN.md). Staff page: http://localhost:5100/staff/characters. The existing compound remains; the M10 fixture task requires port5100 free. Fresh interactive M10 F5 and visual QA are not verified.

## M11 evergreen draft factory
See [M11 run/debug guide](M11_LOCAL_RUN.md). Staff page: http://localhost:5100/staff/factory. Existing API Development/VS Code profiles enable the local processor. The dedicated fixture demo task requires5100 free and restarts only its own hosts. Genuine editorial/source/media and fresh interactive-debug/visual qualification remain separate.

## M12 quality evaluation
See [M12 local run/debug guide](M12_LOCAL_RUN.md). Staff page http://localhost:5100/staff/evaluation. Existing F5 compound retained; the M12 evaluation fixture task requires5100 free. Human labels/release acceptance and live/native-language/age-feedback/production qualification remain open.

## M13 editorial workbench
See [M13 local run/debug guide](M13_LOCAL_RUN.md). Staff page http://localhost:5100/staff/editorial. Existing F5 compound retained; the M13 editorial fixture demo task requires5100 free. Real human qualification and actual platform publication/remediation remain blocked. Fresh interactive M13 debug/visual checks are unverified.

## M16 media library
See [M16 local run/debug guide](M16_LOCAL_RUN.md). Private staff library: http://localhost:5100/staff/media. Existing F5 compound retained. M16 media fixture demo task requires5100 free; installed English SAPI voice needed for the spoken preview. New debugger/visual/listening/public quality checks remain separate.

## M26 historical universe
See [M26 local run/debug](M26_LOCAL_RUN.md). Private universe selector: http://localhost:5200/preview/universes. Use scripts/historical-demo.ps1 -ManageHosts, or -ReuseFixture -DebugPause with M26 attach Api/Web. Development import is additive and private. Historian/source/rights/listening/public qualification and fresh interactive debugging remain pending.
