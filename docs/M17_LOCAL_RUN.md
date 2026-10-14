# M17 local WATCH run and debug

Run from C:\dev\GeetaStudio in PowerShell. Prerequisites: pinned .NET 10 SDK, local SQL Server and existing dedicated Development/Test databases, VS Code C# extensions. WATCH needs the pinned local FFmpeg tools and Windows Nirmala font. The spoken demo additionally needs an installed English Windows SAPI voice. No paid account/provider is enabled.

```powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\watch-tools.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
```

The tool script verifies the exact FFmpeg9.0.2 archive checksum before extraction, pins executable/font hashes in ignored .local/M17-tools.json, and installs nothing system-wide. Use -Offline when the verified archive is already present. FFmpeg binaries and Windows fonts remain outside Git. If the font changes, deliberately rerun configuration; existing production renderer versions remain pinned and cannot silently submit under the new version. Existing owned masters remain portable.

For normal Development, use the existing **Geeta Studio: all** F5 compound (API5100, web5200, standalone Worker). WATCH processing runs inside the API when Runtime:Enabled and Watch:WorkerEnabled are true. Open http://localhost:5100/staff/watch with your authorized Windows account. Use owned tenant/universe codes from the staff registry. Existing API credentials are reused with only narrow WATCH table/column grants; standalone Worker gains no core permissions.

Approve an M13 script with its reviewed M10/M16 production cast and packages. Create/review a versioned layout, then register a zero-ceiling local renderer profile. Choose the exact approved script, character/presentation, image and spoken voice versions, and requested shapes. The simple form handles one scene up to160 characters. Advanced owned CreateWatch JSON supports1–24 scenes covering the complete script exactly once; each scene has an explicit imported voice transcript attestation or generated voice with exact approved text. Image/voice generation uses the existing M16 profile/review gates. Generated draft assets pause Assets/Voice until reviewed. Music is an optional exact approved WAV binding, mixed quietly. Currency and aggregate media reservations are checked before admission.

Storyboard → Assets → Voice → Timing → requested renders → QA → final review have separate durable checkpoints. Review the preview, captions, manifest, citations, warnings, cost and actual review effort. Fixture approvals are explicitly technical and cannot authorize publication. To correct a finished episode, append its next version with ParentProductionId and a correction reason; original timeline and masters remain immutable. Known failed stages use Resume. Unknown work uses Reconcile at its original renderer/account/operation/resource before any retry. Disabled/revoked/expired dependencies block current use. Changing or retiring a character never substitutes a new cast into an existing master.

## Repeatable spoken demonstration

Stop debug hosts so port5100 is free:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\watch-demo.ps1 -ManageApi
```

This creates isolated fixture data only in GeetaStudio_M1Tests, a new fictional guest with a controlled placeholder character card, an actual English SAPI voice, M13 fixture evaluation/approval, vertical360x640 and standard640x360 H264/AAC masters, VTT/SRT captions and a complete manifest. It checks Windows staff HTTP, API restart, exact script/master checksums and caption serving, then stops only its owned hosts and restores environment variables. The additional Indic shaping sample is a renderer component fixture with tone audio: it is not spoken Hindi or human editorial acceptance. Outputs live in .local/m17-demo. Technical fixture review minutes are fixture data, not genuine human review.

## Debug the concrete fixture

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\watch-demo.ps1 -ManageApi -DebugPause
```

Or run VS Code task **Geeta Studio: M17 spoken video demo**. At the pause, note the printed API process ID, tenant and universe. Choose **Geeta Studio: M17 attach Api**, F5, and that exact GeetaStudio.Api process. Open /staff/watch, enter the printed codes and load the episode. Useful breakpoints: WatchFactory.InspectAsync, PreviewAsync, CreateAsync, ControlAsync, ProcessNextAsync and FfmpegWatchRenderer.SubmitAsync. Inspect/preview requests hit read breakpoints; deliberately submitting a new episode and enabling Watch:WorkerEnabled is needed for processing breakpoints (the reproducible demo disables processors). Continue execution, detach, then press Enter in the demo terminal to complete restart/checksum checks and clean up. Do not stop unrelated processes.

VS Code tasks also provide **M17 configure renderer** and **M17 focused checks**. The focused task requires a prior successful build/Test migration; full verify above is the reproducible baseline.

## Limits and release gates

Actual Windows HTTP/restart, rendering/decode/peak measurements and extracted-frame inspection are separate from interactive F5, browser playback/listening and genuine human approval. Fresh M17 F5/breakpoint, browser/mobile/screen-reader checks, human visual/listening/native-language quality and M12's100+20 genuine reviews remain pending. Scene captions are approved scene-level timing, not ASR or word alignment. Indic shaping does not qualify an Indic speech provider. Placeholder character cards and tone fixtures do not establish production art/voice quality. Renderer B exercises the replaceable contract using the same FFmpeg engine; it is not an independent vendor qualification. Optional generative video, paid/cloud providers and public distribution remain unimplemented/unqualified. Existing WATCH generation rights checks remain strict, including AI permission even for the shipped deterministic renderer; sources without it are blocked. Genuine source/video/publication permissions remain required. M18 is not started.
