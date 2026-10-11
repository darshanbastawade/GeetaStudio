# M0 Windows debugger evidence — 10 October 2026

The owner manually trusted `C:\dev\GeetaStudio`. The agent observed the Restricted Mode banner disappear and C# Dev Kit activate the actual solution. No trust/security settings were automated.

1. Doctor: .NET SDK 10.0.401; loopback ports 5100 and 5200 free before startup.
2. VS Code Run and Debug selector: **Geeta Studio: all**. Startup invoked locked restore and build before all three debugger sessions started.
3. Initial run exposed redundant compound/per-host preLaunchTask builds. Fixed `.vscode/launch.json`: compound retains the sole build task; hidden Api/Web/Worker `(compound)` entries retain identical DLL/cwd/environment/URLs but have no preLaunchTask. Visible individual entries retain their own build. Configuration regression assertions verify these relationships.
4. Final `scripts/verify.ps1 -Locked`: locked restore/build passed; 0 warnings/errors; **18/18 tests**; formatting clean.
5. Final F5 startup: Api (compound), Web (compound), Worker (compound) all Running in VS Code. Actual process inventory:

| Host | Process ID | Debugger parent PID | Target |
|---|---|---|---|
| API | 12576 | 10984 | src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.dll |
| Web | 17076 | 10188 | src/GeetaStudio.Web/bin/Debug/net10.0/GeetaStudio.Web.dll |
| Worker | 11196 | 1996 | src/GeetaStudio.Worker/bin/Debug/net10.0/GeetaStudio.Worker.dll |

6. Added a breakpoint on the `new HealthStatus(...)` line in API Program.cs, line 12. Requested health from a normal workstation PowerShell process:

```powershell
(Invoke-WebRequest http://localhost:5100/health/live -TimeoutSec 120).Content
```

VS Code showed **Paused on breakpoint** and stack frame `GeetaStudio.Api.dll!Program.<Main>$.AnonymousMethod__0_0()` at **Program.cs:12**. [Breakpoint screenshot](M00-debug-breakpoint.png). Continue returned:

```json
{"service":"GeetaStudio.Api","status":"Healthy","checkedAtUtc":"2026-10-10T10:15:16.6906194+00:00"}
```

7. A web request initially hit the still-enabled breakpoint again and the shell's deliberately bounded 3-second API check timed out. Removing the demonstration breakpoint and continuing the API restored the expected result. No timeout or production protection was weakened.

```powershell
$page = Invoke-WebRequest http://localhost:5200 -TimeoutSec 10
# Assert HTTP 200 and rendered "API reachable" in $page.Content.
(Invoke-WebRequest http://localhost:5100/api/v1/development/status -TimeoutSec 10).Content
```

Actual results: web HTTP **200**, rendered **API reachable**, and Development fixture JSON with `isFixture: true`. All three debugger processes belonged to this compound, with the expected Development host configuration.

8. **Shift+F5** stopped the compound. `Get-Process -Id 11196,12576,17076 -ErrorAction SilentlyContinue` returned no processes. `scripts/dev-doctor.ps1` then returned success and reported **5100/5200 available**. No unrelated dotnet process was terminated. The temporary breakpoint was removed; no debugger session remains running.

M0 Windows debugger acceptance is verified. Hosted GitHub CI is still unrun; no commits/pushes/live provider calls performed. M1 has not started.
