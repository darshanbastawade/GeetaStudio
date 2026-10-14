# M21 private TALK — Windows run and debug

Run from `C:\dev\GeetaStudio`. Requires the exactly pinned .NET10.0.401 SDK, native local SQL Server, existing private Development credentials, Windows integrated authentication and an available loopback port5100. No paid provider or customer charge is required. Preserve existing databases; never drop/reset to run the fixture.

```powershell
Set-Location C:\dev\GeetaStudio
dotnet restore .\GeetaStudio.sln --locked-mode
dotnet build .\GeetaStudio.sln --no-restore -m:1 /p:UseSharedCompilation=false
.\scripts\db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
$env:GEETA_SQL_TESTS = '1'
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~Talk'
.\scripts\talk-demo.ps1 -ManageApi
```

The demo creates isolated owned Test records, imports the first licensed Besant sample verse through the real source/corpus/index pipeline, seeds explicitly labeled fixture persona/audience/locale reviews and10 synthetic credits, and starts an owned hidden API. It checks a supported question, an unsupported pizza question, another account's403, source citations, A→B new-turn routing, original replay, validated streaming, reports, credits7/held0 and actual API restart. It stops its own hosts and restores environment settings. It never upgrades Development sources or qualifies human review. The sample is intentionally narrow; it does not establish general philosophical competence.

For interactive inspection:

```powershell
.\scripts\talk-demo.ps1 -ManageApi -DebugPause
```

While paused, open `http://localhost:5100/talk`, enter the workspace printed in the terminal, load your profiles/capabilities, and start a new session. Ask **Where did the opposing armies gather?** and inspect the quoted passage and source link. Ask **How do I bake a pizza?** for the no-supported-answer path. Additional manual turns consume the demo's finite credits; run the unattended demo separately for exact automated balance assertions. The pause creates no public or paid access.

In VS Code, open this trusted folder, run **Geeta Studio: M21 TALK demo**, then select **Geeta Studio: M21 attach Api** and choose the printed API process. Set breakpoints in `TalkExecution.cs` at turn admission/credit reservation, `AiTalkAdmission.cs` at admission, and `CustomerCredits.cs` at settlement. Trigger a question from `/talk`; cancellation uses a second scoped request and a provider cancellation monitor. Press Enter in the demo terminal to finish cleanup. Fresh interactive F5/breakpoints are not verified by a build or attach configuration. Existing **Geeta Studio: all** remains the three-host Development entry.

For the restricted Development host:

```powershell
.\scripts\db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
```

Open `http://localhost:5100/talk` and `/staff/talk`. A Windows login still needs its own registered M20 customer account/profile; only explicitly mapped administrators can review/configure capabilities. Empty capability lists in Development are expected: this milestone does not fabricate approvals or upgrade existing source permissions. Use `/staff/ai` to configure a qualified local `answer-with-evidence` route and monetary budget. TALK accepts only `Internal`, local, zero-retention, no-training/no-tools policies at launch; current live adapters cannot satisfy this private policy and remain blocked. Configure a new TALK policy via `/staff/talk` using `TalkPolicyRequest`: actual universe, approved current fictional mentor registry revision, reviewed audience/locale lesson profiles, exact age band, active M6 alias/embedding space and explicit private fixture/evidence reference. The configuring administrator remains the service actor and must retain tenant administration. No character name is a business enum.

Credit admission precedes source retrieval; unfunded turns receive no source text. Credits are the M20 customer entitlement for this pay-per-supported-turn capability, separate from the M7 monetary budget. Only an accepted exact supported answer consumes the configured fixed customer units. Rejected/no-match/failed known work releases credits; known provider costs remain booked even if the customer answer is rejected. Unknown/interrupted submissions hold both systems and block another turn in that session. Never resubmit an unknown turn. Staff must inspect/recover/reconcile the original M7 run/attempt with authoritative usage evidence, then the owner uses **Check unresolved usage**. Recover unadmitted interrupted work only after cancelling/expiring its session. Route changes affect new turns; prior route/attempt/source and accounting IDs remain pinned.

Streaming buffers and validates the provider response before sending answer chunks; unvalidated vendor deltas never reach the customer. Cancel stops the whole session. Session30min/default12turns, with minors additionally bounded by the reviewed audience interaction cap, at most3 active sessions/account; configuration permits5–60min/1–24turns. Three previous supported turns form process-memory context scoped to the exact owner/session, lost on restart and cleared on cancellation/deletion. Default SQL never retains question/answer content. Adult explicit current personalization consent plus the opt-in checkbox permits this session's SQL memory for at most24h. Child memory is never persistent and minor access is parent-mediated; no advertising. Withdrawal/profile deletion erase stored text in their customer transaction. Expiry and current consent/membership are checked on reads and a five-minute bounded retention sweep. Accounting, operation hashes, canonical evidence and problem category metadata remain; legal retention and production privacy qualification are open gates. No cross-session personalized memory is implemented.

Only scripture learning and clearly labeled reflection are supported. Professional/current-news/crisis/authority requests return controlled limitations or escalation text without calling AI. No account tools, arbitrary URLs, secrets or other users' context are available. The conservative English safety rules and fictional persona require real human/safety/usability/accessibility review before release. Genuine M12's100+20 reviews remain pending. M22 voice is not implemented.
