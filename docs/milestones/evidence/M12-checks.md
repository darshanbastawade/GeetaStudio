# M12 executed checks — 11 October2026

Windows, C:\dev\GeetaStudio, SDK10.0.401/EF10.0.12/local SQL. No new dependencies/live calls.

- dotnet ef migrations add M12Evaluation --project src/ContentOS.Infrastructure --startup-project src/ContentOS.Infrastructure --context CoreDbContext --output-dir CoreMigrations: additive20261011044921_M12Evaluation. Initial API startup-project attempt required Design; corrected to the established infrastructure design factory, no package added.
- dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false:0 warnings/errors.
- scripts/db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild and scripts/db-update.ps1 -ProvisionRuntime -NoBuild: both dedicated migrations applied; restricted SELECT/INSERT evaluation grants, no reset/password/source-permission change.
- GEETA_SQL_TESTS=1; dotnet test tests/ContentOS.Foundation.Tests --no-build --no-restore -m:1 --filter FullyQualifiedName~Evaluation --logger trx;LogFileName=M12-focused.trx: final9/9 (7 SQL/2 policy),0 skipped.
- scripts/verify.ps1 -Locked -Sql -LowMemory: final exit0,locked restore/build0 warnings/errors,81/81 offline/125/125 combined SQL,0 skipped,format verification clean.
- Both CoreDbContext/RuntimeDbContext dotnet ef migrations has-pending-model-changes --project src/ContentOS.Infrastructure --startup-project src/ContentOS.Infrastructure --no-build: current.
- scripts/evaluation-demo.ps1 -ManageApi: actual Windows staff page200,120 fixture labels,100-case A/B property checks with distinct wording,20 once-scored held-out cases, critical fluent failure rejection,actual API restart/report persistence; human0/blocked,cost0; all owned hosts stopped.
- .local/m12-development-check.ps1: actual private restricted Development API,staff page200,scoped datasets/candidates/reports reads,empty genuine inventory; owned API stopped.
- .local/m12-preservation.ps1: actual Dev8 characters/3 universes/50 corpus verses/10 runtime jobs,12 core/3 runtime migrations,evaluation dataset/run0. Initial metadata query used an incorrect runtime history schema; corrected to the existing dbo table.
- node --check .local/m12-ui.js; node .local/m12-ui-smoke.cjs: syntax/loading/run/structured rubric-review handlers pass; textContent only for dynamic data. Not visual/accessibility qualification.
- PowerShell parser and tasks/launch/API launch-settings/catalog JSON parse: pass.
- git diff --check: final clean after extra EOF whitespace removed; LF/CRLF warnings only. Windows doctor: SDK/git/code available,5100/5200 free on supported address families.
- Fresh M12 interactive F5/breakpoints/browser visual/mobile/accessibility, live model/native-language/human-age-feedback, hosted CI/cold cache/production/restore not run.

Meaningful failures fixed during implementation: M07 camelCase JSON decoding, persisted recorded artifact ID/current withholding, M06 transaction composition, owned retrieval fixture lacking semantic aliases (use an explicitly source-specific reviewed fixture query), and Windows platform guard in the demonstration. No assertion/timeout/rights rule weakened. One focused run hit an unchanged earlier M09 SQL profile setup timeout; unchanged no-build focused recheck9/9 and final broad125/125 passed. No SQL infrastructure cure claimed.

Owner explicitly selected fixtures and pending human review.120 correlated variants are not120 human-approved independent questions; no automatic release/provider activation. M13 untouched.
Final housekeeping: extra EOF whitespace cleanup briefly introduced CRLF at C# EOF; restored required LF endings. These were whitespace-only changes; final format verification rerun.
