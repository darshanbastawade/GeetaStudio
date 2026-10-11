# M13 validation and handoff

Status: Offline Verified for the local system/fixtures. Genuine human/public release remains blocked.

Implemented: editorial review workbench, immutable revision/asset/evidence/policy approvals, scoped Author/Editor/Publisher/Admin actions, lineage and correction propagation, immutable external remediation attempts, safe provider administration, M10 character workbench integration and a bounded manual trend queue. Retired direct-episode casts cannot be reapproved. A demonstrated M12 privacy false positive for declared citation GUIDs was fixed without excluding real identifiers/emails in prose.

Verified:
- Final locked restore/build:0 warnings/errors.
- Final offline tests83/83,0 skipped; formatting passes.
- Focused M13 run10/10 (9 SQL+1 offline),0 skipped.
- Actual Windows demo:200 staff/private preview, text/audio approval invalidation and checksum change, restart durability, Windows Reader403 for approve/publish/route activation. Owned hosts stopped,live calls0.
- Restricted Development and ID/source-permission preservation pass.
- Both final EF pending-model checks pass.
- PowerShell/VS Code JSON/JavaScript syntax/mock DOM/diff checks pass.

Retained failures:
- Earlier broad SQL48/133 passed,85 failed amid resource/connection cascades.
- Resumed full SQL131/134 passed:two source SQL timeouts and one M12 GUID/privacy failure.
- The privacy failure is fixed; its real-SQL recheck passed.
- Subsequent targeted current SQL9/10 passed; audio fixture setup timed out.
- Both source timeout tests and the audio test passed unchanged together3/3.
- Final combined134/134 real-SQL tests pass,0 failures/skips,8m18s. Evidence:M13-final-full.trx and .local/M13-final-full-sql.log. Earlier failures remain history.
- SQL process_physical_memory_low=true observed; no server settings, assertions or test timeouts were weakened.

Migrations:20261011061744_M13Editorial and20261011064722_M13Attempts; fourteen Core/three Runtime records. Eight characters,three universes,50 corpus verses,ten runtime jobs,five source permissions preserved. No reset, source-rights upgrade, paid/live call, publication, commit or remote action.

Run from C:\dev\GeetaStudio:
~~~powershell
.\scripts\verify.ps1 -Locked -Sql -LowMemory
.\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
~~~
Use separate terminals for the same dotnet run command with GeetaStudio.Web and GeetaStudio.Worker. API workbench:http://localhost:5100/staff/editorial; web:http://localhost:5200.

For debug, open the trusted repository in VS Code, choose Geeta Studio: all, press F5. Breakpoints:EditorialWorkbench.ActAsync/EditAsync/RouteAsync and EditorialBinding.AssertExport. Shift+F5 stops the compound. For the fixture demo, stop the API/debug host first, then run scripts/editorial-demo.ps1 -ManageApi.

Fresh M13 interactive F5/breakpoints and browser visual/mobile/accessibility remain unverified. Genuine M12 human dataset/release acceptance, theological/native-language/age feedback, non-fixture providers/media, actual publishing/remediation and hosted-CI/production recovery/load qualification remain blocked or unverified. LocalPreview copies are dry runs; public knowledge remains false. M14 not started.
