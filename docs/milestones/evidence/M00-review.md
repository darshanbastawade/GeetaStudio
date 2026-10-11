# M0 review — 10 October 2026

Reviewed C:\dev\GeetaStudio against AGENTS.md, docs/prompts/M00.md, milestone evidence, architecture decisions, dependency register, host implementation, tests and Windows run/debug configuration. M1 remains untouched. No application source was edited.

## Finding

**[P2] Check IPv6 localhost conflicts in the development doctor** — scripts/dev-doctor.ps1:18–21.

The doctor attempts to bind only IPAddress.Loopback (127.0.0.1), while the documented host URLs use localhost. An IPv6-only listener on ::1:5100 leaves the IPv4 probe free. The doctor reports success even though the documented API launch fails. The same check is used for web port 5200.

Actual reproduction on this workstation:

1. Started a temporary TcpListener on IPAddress.IPv6Loopback, port 5100.
2. Ran the doctor in Windows PowerShell: it reported both ports available and exited 0.
3. Repeated the temporary conflict and ran the documented API command. Startup failed with `Failed to bind to address http://[::1]:5100: address already in use`, exit -532462766.
4. Released every temporary listener in finally. No unrelated process was stopped.

Recommended fix: probe both supported loopback address families, report the conflicting address and port, and return exit 1 if either required address is unavailable. Add a focused IPv6-only conflict regression check. Preserve the existing localhost profiles and free-port behavior.

## Checks run in this review

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked`: restore/build passed, 0 warnings/errors, 18 passed/0 failed/0 skipped, formatting clean.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-architecture-gate.ps1`: injected Domain vendor dependency failed as expected; project restored byte-for-byte; all 7 architecture cases then passed.
- Reviewed Development-only fixture exclusion, real health client and safe errors, reference boundaries, known-secret guard, pinned dependencies, CI configuration, and compound debugger task ordering.
- Actual Windows breakpoint/compound-stop checks are documented prior evidence; they were not repeated during this review.

## Remaining limits and next step

Resolve the doctor finding within M0 before considering M1. Hosted GitHub CI and a fully cold NuGet download remain unverified. The repository still has no commit, so prior clean-source verification used a source snapshot rather than a git clone. No commit, staging, push, deployment or paid provider operation was performed.

Local run/debug commands remain in M0-local-run-debug.md beside this report and in the repository's docs/LOCAL_DEVELOPMENT.md. Use VS Code's Geeta Studio: all and F5; use Shift+F5 to stop all debugger-owned hosts. Until the doctor is corrected, its successful port result alone does not prove IPv6 localhost availability.

Resolution during authorized M1: scripts/dev-doctor.ps1 now checks both supported loopback address families. scripts/test-doctor.ps1 verifies IPv4-only and IPv6-only conflicts on both ports and free-port success. The P2 finding is closed.

