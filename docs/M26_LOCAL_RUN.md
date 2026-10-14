# M26 — local historical universe

Run these commands in a PowerShell terminal from `C:\dev\GeetaStudio`. The SDK remains 10.0.401; executable hosts need .NET/ASP.NET Core 10.0.13 or a compatible later patch. Local SQL Server, VS Code with the existing C# debugger, an installed English Windows SAPI voice and the pinned M17 FFmpeg tool configuration are required for the complete demo.

```powershell
Set-Location C:\dev\GeetaStudio
dotnet restore .\GeetaStudio.sln --locked-mode
dotnet build .\GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
.\scripts\watch-tools.ps1 -Offline
.\scripts\db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
.\scripts\historical-demo.ps1 -ManageHosts
```

The demo creates additive, explicitly private fixtures in `GeetaStudio_M1Tests`, imports the pinned excerpts already in the repository, creates ten READ entries and two narrated WATCH samples, exercises retrieval/workflow/factory/evaluation/export, starts Windows-authenticated API and Web, checks labels and switching, restarts its own hosts and verifies master hashes. It stops only the hosts it owns. Ports 5100/5200 must be free. No paid provider, platform publishing or external download is involved when the cached tools and locked packages exist. If FFmpeg is absent, run `scripts/watch-tools.ps1` without `-Offline` to download the pinned archive and verify its checksum.

Outputs: `.local/m26-demo/GeetaStudio_M1Tests/fixture.json`, `read.json`, `coronation/{Vertical,Standard}.mp4`, `dialogue/{Vertical,Standard}.mp4`, each sample's `captions.vtt` and `manifest.json`. These local artifacts are private technical previews with original guide cards and installed English speech. Historical, pronunciation, visual and listening acceptance are pending.

For an additive ten-item private READ sample in the normal Development database:

```powershell
.\scripts\db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
.\scripts\historical-demo.ps1 -Database GeetaStudio_Dev -ImportOnly
```

This import does not run the historical AI workflow or change the Development default AI route. It adds a versioned pack, source records and private learning/CMS/READ entries; it never resets Geeta's corpus. The saved Development `fixture.json` supplies tenant, universe, collection and audience IDs for the existing staff workbenches. Reuse those records with `-ReuseFixture`; a deliberate new import creates a fresh private demonstration collection and disables the prior saved M26 demo collection with an audit record; prior source/content/history is retained.

## Run and debug

After the first successful demo:

```powershell
.\scripts\historical-demo.ps1 -ManageHosts -ReuseFixture -DebugPause
```

Open `http://localhost:5200/preview/universes` using the current Windows account. Choose the private Geeta scripture fixture or Shivaji collection. In VS Code, run **Geeta Studio: M26 attach Api**, select the printed API process, then **Geeta Studio: M26 attach Web** for the printed Web process. Useful breakpoints: `ReadService.PrivateDirectoryAsync`, `ReadHistorical.ValidateHistory`/`HistoricalPiece`, `UniversePreviews.OnInitializedAsync`, `ReadSurface.OnParametersSetAsync`; for a new generation, `MediaLibrary.GenerateAsync` and `WatchFactory.ProcessNextAsync`. The original **Geeta Studio: all** F5 compound and normal Development host profiles remain available. Interactive M26 breakpoint verification has not been claimed.

The demo deliberately pauses for inspection. Press Enter in its terminal to run restart checks and stop its hosts. The preview policy requires Development, a loopback API, and both the Web process account and authenticated reader to equal `Read:PreviewOwner`. It does not forward arbitrary user identities. READ previews use no-store/noindex; anonymous public directories do not expose the fixtures.

Private staff tools on `http://localhost:5100`: `/staff/sources`, `/staff/evidence`, `/staff/read-collections`, `/staff/characters`, `/staff/factory`, `/staff/editorial`, `/staff/media`, `/staff/watch`, `/staff/social`. Use the IDs from the selected database's fixture file. Existing authorization, reviewed cast/voice binding and mutation-header checks apply.

## Checks

```powershell
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'Category!=Sql'
$env:GEETA_SQL_TESTS='1'
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlHistoricalTests' --blame-hang-timeout 180s
.\scripts\verify.ps1 -Locked -Sql -LowMemory
```

Run SQL checks and the demo serially. The full SQL suite uses actual local SQL; timeouts are failures, not fixture substitutes. Genuine historian review, scan/OCR comparison, corroboration independence, redistribution/voice rights, M12 human labels, native-language learning review and the M25 production gates remain required before public use. Neither an `Approved` fixture record nor an export manifest is a public-release qualification.
