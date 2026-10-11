# M10 checks — 11 October 2026

Executed in C:\dev\GeetaStudio on Windows, .NET10.0.401 / EF10.0.12 / local SQL Server. No live calls.

- dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false: zero warnings/errors.
- dotnet ef migrations add M10Registry --project src/ContentOS.Infrastructure --startup-project src/ContentOS.Infrastructure --context CoreDbContext --no-build: additive CoreMigrations/20261011032341_M10Registry.
- db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild; db-update.ps1 -ProvisionRuntime -NoBuild: migrated dedicated databases and existing restricted grants; no reset.
- GEETA_SQL_TESTS=1; dotnet test tests/ContentOS.Foundation.Tests --no-build --no-restore -m:1 --filter FullyQualifiedName~Registry --logger trx;LogFileName=M10-focused.trx: 13/13, 3 policy +10 SQL, zero skipped. First attempt exposed missing fixture Assets:Root; fixed fixture configuration, not assertions/timeouts.
- verify.ps1 -Locked -Sql -LowMemory: locked restore/build clean, 77/77 offline and 108/108 full SQL, no skipped; process exited1 solely for an end-of-line marker in RegistryPage.cs. Corrected dotnet format, rebuilt and independently verified formatting. Test logic unchanged.
- Both CoreDbContext and RuntimeDbContext dotnet ef migrations has-pending-model-changes --project src/ContentOS.Infrastructure --startup-project src/ContentOS.Infrastructure --no-build: no pending changes.
- dotnet format GeetaStudio.sln --verify-no-changes --no-restore; git diff --check: passed.
- node --check .local/m10-ui.js; node .local/m10-ui-smoke.cjs: syntax and mock-DOM initialization/load/profile editor/bible revision/proposals passed. Not visual QA.
- PowerShell registry-demo.ps1 parser and VS Code task/launch JSON parse: passed.
- registry-demo.ps1 -ManageApi: actual Windows Negotiate HTTP200/stored story, new coach/pinned draft/activation/same-ID promotion/historical scope/profile checks, fixture export blocked; owned host stopped.
- .local/m10-development-check.ps1: actual private restricted Development API, 7 profiles/3 Shakuni styles, Neeraja alias, catalog identity preservation and stable repeated proposals; no human/source permission fabricated; owned host stopped.
- dev-doctor.ps1: SDK/git/code available; ports5100/5200 free.
- SQL preservation counts: 8 characters,3 universes,50 corpus verses,10 runtime jobs;10 core/3 runtime migrations.

Current main baseline 8515c3a preserved. No commit/push/staging/database reset. Interactive M10 F5, browser visual/mobile/accessibility QA, genuine editorial/media/rights/language qualification, hosted CI/cold cache and production remain unverified.