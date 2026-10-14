# M23 local run and debugging

Run from `C:\dev\GeetaStudio` with the exact pinned SDK10.0.401 and the existing dedicated local SQL Server. M23 uses local zero-cost template renderers; no cloud credential, AI call, live payment or public sharing is enabled.

```powershell
Set-Location C:\dev\GeetaStudio
dotnet restore GeetaStudio.sln --locked-mode
dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
$env:GEETA_SQL_TESTS = '1'
dotnet test tests/ContentOS.Foundation.Tests/ContentOS.Foundation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Creation'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/creation-demo.ps1 -ManageApi
```

Run SQL tests and demos serially. The demo creates explicitly controlled Test-only source/account/template fixtures, starts its own Windows-authenticated API on5100, verifies private create/edit/review/export, stable submission, exact credit settlement, restart, other-account403 and deletion, then stops only its hosts and restores environment overrides. Its exported printable fixture is `.local/m23-demo/learning-pack.html`. It is technical demonstration content, not human-qualified commercial material.

For the normal Development identity/schema/grants:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
```

F5 **Geeta Studio: all** preserves API5100, web5200 and the separate Worker. Open `http://localhost:5100/create`. The studio lists workspaces bound to your verified local account, then managed profiles and configured templates. Create a local account/household in `/account` if required. Development deliberately has no fabricated approved template/source license. An authorized administrator registers templates at `/api/v1/customer-admin/tenants/{tenant}/create/templates`, selecting current approved M13 source and eligible M10 fictional-character profile, audience bands/themes, optional approved M16 illustrations/M17 master and explicit review evidence. Template changes append versions; enable/disable uses the scoped `/templates/{id}/enable?enabled=false` action. Local adapters are `create.local.a.v1`/`create.local.b.v1`; new requests pin their template version, previous outputs retain theirs.

For a concrete fixture debugging session, stop any API using5100 and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/creation-demo.ps1 -ManageApi -DebugPause
```

Choose **Geeta Studio: M23 attach Api**, select the process printed by the demo, and set breakpoints in `CreationStudio.SubmitAsync`, `ProcessNextAsync`, `EditAsync`, `ExportAsync` and `DeleteAsync`. Open `/create` and select the controlled workspace. Continue/detach, then press Enter in the demo terminal to finish its checks and cleanup. **Geeta Studio: M23 focused checks** and **M23 creation demo** tasks are provided. This runbook does not claim a fresh interactive breakpoint or visual/accessibility acceptance was performed.

Only short fictional nicknames, reviewed theme keys and bounded reflections are accepted. Uploaded material must be owned, explicitly consented, a plain PNG up to1MiB or UTF-8 text up to4000bytes, with a description; real-person likeness processing is blocked pending qualification. Material binds one creation. Delete unused material with `/uploads/{id}/delete`; delete its creation first when bound. Parent/owner authority is rechecked for managed child profiles; no advertising or persistent conversational child memory is introduced.

Credits are reserved before queue admission and consumed once after a durable accepted output. Queued cancellation releases undispatched credits. Local editing is included; deleting accepted work does not undo its recorded credit consumption. Expired local claims can reconstruct deterministic output; settlement recovery uses the original reservation. The renderer has no remote side effects, provider spend, or provider-retained artifacts; paid/AI renderers are rejected rather than bypassing M7 budget controls.

Preview is escaped and sandboxed. Reviewed export downloads a private ZIP containing printable HTML, attribution/lineage and optional exact separately reviewed video/captions. Source rights, editorial approval, character version and material eligibility are rechecked at generation/export; invalid evidence is withheld in history. Editing invalidates output review/public eligibility. Public eligibility needs current genuine review/rights and consent and still creates no public URL or publishing action.

Deletion erases input text, uploaded bytes/descriptions and every creation revision's HTML, and queues local cleanup. Profile/account privacy deletion propagates erasure. Minimal hashes/IDs/credit and event receipts remain for reconciliation. There is no creation cache/search index or external retained output to purge. Copies explicitly downloaded by the owner are outside server cleanup and must be removed by that owner.

Live providers, genuine source/public/commercial permissions, human M12/quality/safety/age/privacy review, real-person consent processing, browser/mobile/accessibility and fresh interactive F5 qualification remain release gates. Prior M18–M21 review findings remain separately open. M24 begins only after this milestone's controlled implementation and checks are complete.
