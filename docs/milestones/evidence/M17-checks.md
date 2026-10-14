# M17 verification —13 October2026

Commands actually run from C:\dev\GeetaStudio:

- scripts/watch-tools.ps1 -Offline: pinned archive/executables/font config and actualFFmpeg9.0.2 pass.
- scripts/verify.ps1 -Locked -Sql -LowMemory: locked restore/build0 warnings/errors;92 offline+158 real SQL pass,0 skipped; overall exit2 only on final DevelopmentSetup.cs EOF formatting. Fixed the line ending, without semantic or schema change.
- dotnet format GeetaStudio.sln --verify-no-changes --no-restore: final independent exit0.
- dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false: latest code0 warnings/errors.
- GEETA_SQL_TESTS=1; dotnet test tests/ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlWatchTests|FullyQualifiedName~WatchRuleTests|FullyQualifiedName~SqlMediaTests' --logger 'trx;LogFileName=M17-final-focused.trx': final15/15,0 skipped,2m6sec. Broad250 tests preceded the final small original-reconciliation replay/receipt-regression/audio-version changes; final15 rerun covers all WATCH/M16 focused checks on latest code; final92 offline tests also rerun/pass after restoring composition version4 headroom. No combined verify exit0 claim.
- dotnet ef migrations has-pending-model-changes --project src/ContentOS.Infrastructure --startup-project src/ContentOS.Infrastructure --context CoreDbContext --no-build; same RuntimeDbContext: both no pending model changes. API is not the design-time startup; its initial design-package error was corrected by using the established Infrastructure startup, no new dependency.
- scripts/db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild, .local/M17-preservation.ps1 -After: additive19Core/4Runtime, original IDs/counts/full source permissions unchanged. No reset/credential rotation/source upgrade; existing narrow API/watch grants catalog and restricted Windows staff/scoped reads pass.
- PowerShell parser, VS Code JSON and Node --check extracted WATCH script: pass. Git diff --check: pass after removing trailing blank lines.
- scripts/watch-demo.ps1 -ManageApi: Windows spoken SAPI, vertical+standard masters, captions, private HTTP, API restart and checksums. Renderer-only Indic sample uses tone audio, not Hindi narration. See M17-demo.json and M17_LOCAL_RUN.md. Extracted English/credit/Indic frames inspected; spoken peak−4.0dB/mean−27.5dB. No human listening/visual/native-language acceptance fabricated.

Earlier local failures were fixed without weakening tests, timeouts or SQL settings: concat UTF8 BOM, Windows demo platform guard/DTO argument order, actual AAC peak headroom, fixture-source rights. Auto-review rejected a proposed Generate→Adapt change; it never executed. Production generation/AI rights stay strict; only freshly created synthetic Test sources declare their explicit fixture Video/AI permissions. An overlapping formatter overwrote the early audio fix; restored headroom and pinned composition version4, then reran real Windows demo/focused checks sequentially. Actual real source/public licenses remain unchanged and unqualified. M12 genuine100+20, fresh M17F5/browser/mobile/accessibility and human art/listening/native-language acceptance remain pending. No live/paid/public operation; M18 not started.


