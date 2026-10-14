# M16 local run and debug

Run from C:\dev\GeetaStudio in Windows PowerShell. Requires pinned .NET10, existing dedicated local SQL Server, VS Code C# debugger, and an installed English Windows SAPI voice for the spoken demo. No paid key, Docker, WSL or network media service is required.

## Build and check
```powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
```
The second command uses the established local setup to apply additive migrations and configure the existing restricted Development identities privately. Do not reset either database or upgrade source permissions. Private Windows staff allowlisting and tenant membership remain necessary; no secret should be pasted into a request or report.

## Normal Development use
Open this folder in trusted VS Code; choose **Geeta Studio: all**, then F5. API http://localhost:5100, web http://localhost:5200, existing Worker host. M16 generation processor runs inside the API when Runtime:Enabled and Media:WorkerEnabled are true; the standalone Worker retains its existing responsibilities. Existing compound setup is preserved.

Open http://localhost:5100/staff/media using your authorized Windows account. Enter an owned workspace/tenant and universe code from the core/character staff pages. Import a plain RGB/RGBA8 noninterlaced PNG or PCM16 WAV, select the exact character profile/audience/locale, describe it accessibly and record real permission/source evidence. Controlled samples use Fixture and fixture: evidence and cannot authorize public release. Append the next version for an existing asset name.

Review image and voice with explicit evidence and no unresolved deviations. Create the package using the form: exact character, approved image/voice, palette/style/poses/scenes/pronunciation. Approve the package, bind it, then review the new presentation on /staff/characters. The package review and presentation review are separate. A provider/style change creates a new draft package/presentation and needs fresh quality review. Episode-only promotion retains character/media identity; retirement, stale profile, revoked sources/assets or package withdrawal blocks current reuse/casting/export while preserving approval history.

For installed SAPI, the profile, character and output locale must explicitly be en-US. If it is not already in the catalog, register an en-US Locale profile through the existing /staff/lessons administration flow before character registration; that existing data operation adds a proposed locale without claiming human language qualification. The controlled demo adds this catalog entry only to Test. en-IN/hi-IN/mr-IN cannot silently substitute an English Windows voice.

Provider/generation administration is available in the expandable administration panel using the owned MediaContracts DTOs. Shipped profiles require local: account references and zero call ceilings; no paid/live adapter is qualified. An installed SAPI voice token is a provider-specific resource, never a portable character identity. Unknown/Submitted generation uses Reconcile against the original profile/account/resource; do not create another submission to resolve ambiguity. Callback acceptance requires authenticated staff plus the original recorded resource/checksum/cost and a stable operation ID.

## Repeatable controlled Windows demonstration
Stop your debug hosts first so port5100 is free:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\media-demo.ps1 -ManageApi
```
Or run VS Code task **Geeta Studio: M16 media fixture demo**. This creates isolated fictional fixtures only in GeetaStudio_M1Tests, generates a controlled reference card and actual spoken en-US WAV through the installed Windows voice, reviews a guest package, binds/casts/promotes the guest, rejects an unsupported reference fallback, checks callback replay and original-resource reconciliation, moves the PNG from primary to the configured local archive, restarts its API and verifies bytes/SHA/stable references via Windows HTTP. It restores environment variables and stops only its own hosts.

Outputs: .local/M16-preview.png, .local/M16-preview.wav, .local/M16-fixture.json (workspace/universe codes), docs/milestones/evidence/M16-demo.json. These previews remain explicitly controlled fixtures; no human pronunciation/identity or public redistribution approval is implied. If no English SAPI voice is installed the spoken demo fails explicitly; the offline/SQL suite uses declared tone fixtures, not narration.

## Inspect and debug the controlled fixture
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\media-demo.ps1 -ManageApi -DebugPause
```
The script prints the owned API process ID and workspace/universe codes and pauses before shutdown. In VS Code choose **Geeta Studio: M16 attach Api**, F5 and select that exact GeetaStudio.Api process. Set breakpoints in MediaLibrary.ImportAsync, MediaGeneration.AcceptAsync/ProcessNextAsync, CharacterMedia.BindPackageAsync or MediaStorageMover.MoveAsync. Open /staff/media, load the printed scope, import/review or submit a move request to hit the relevant breakpoint. Enable Media:WorkerEnabled only for a deliberately configured generation test; the demo disables processors for reproducibility. Continue execution, stop the debugger and press Enter in the demo terminal to clean up. Never stop an unrelated SQL Server or application process.

Actual new M16 interactive F5/breakpoint/UI clicks, browser/phone/screen-reader usability and listening/visual quality review are unverified. Windows authenticated HTTP and process restart are separate checks and do not establish debugger or human quality acceptance.

## Storage and processing limits
Primary files: configured Assets:Root. Archive default: Assets:Root + .archive; an explicit Assets:Stores:archive overrides it. Demo uses .local/test-assets and .local/m16-archive consistently for both fixture and host. Move takes an approved store key, copies/verifies owned bytes, then changes the runtime mapping; it does not accept arbitrary client paths and retains the old file. Both stores must remain private to the configured host identity. This verifies two owned local stores, not cloud migration or a data-center failover.

1MiB per upload, 1024x1024/one million pixels, PNG CRC/chunk/expanded-size validation, WAV PCM16 mono/stereo8–48kHz up to30sec, no active/ancillary metadata. JPEG/MP3/video require future qualified processors. Only one image reference is supported; unsupported locale/reference/pronunciation/cloning is blocked explicitly. Character/reference adapters preserve the exact supplied image or generate procedural placeholders; they are not live AI character generators. Tone fixture is not spoken narration. Windows SAPI is local English preview with bounded plain text and speak-as substitution; human pronunciation and redistribution/license review remain pending. M12 genuine100+20 reviews/public release gates remain open. No M17 video or platform publishing is implemented.