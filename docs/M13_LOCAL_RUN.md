# M13 local run and debug
Run in PowerShell from C:\dev\GeetaStudio. Existing .NET10, local SQL Server and Windows staff setup apply. No new provider key or paid service is needed.

~~~powershell
Set-Location C:\dev\GeetaStudio
.\scripts\verify.ps1 -Locked -Sql -LowMemory
.\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
~~~

The additive M13Editorial and M13Attempts migrations add ten editorial tables without resetting existing data. ProvisionRuntime refreshes restricted API grants; the standalone Worker gets no editorial grants.

Start the hosts in separate terminals:
~~~powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
~~~
~~~powershell
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
~~~
~~~powershell
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

Open http://localhost:5100/staff/editorial using your configured Windows staff account, or the editorial link at http://localhost:5200. Select your owned tenant and universe. Create an original draft from a brief or adopt an existing owned content ID. Review current/previous text, sources, competing claims, variants, models/routes, costs, evaluation findings and lineage. Source-bearing text is withheld if current rights block reading.

Evaluation binds a real M12 report ID and case key whose output matches the exact current body, locale and audience. For registry/factory production drafts, the case must carry cast context and its output must match the pinned character/profile/role/voice/life-stage snapshot. Current eligibility is rechecked before approval. Move Draft → Evaluated → Review → Approved. Fixture approvals use fixture: evidence and are limited to local preview. Approval policy editorial.v2 expires approvals made under the older checks without deleting them; reevaluate/review before renewed approval. Manual release approval requires the existing genuinely accepted M12 evaluation gate plus manual: evidence; existing fixtures cannot satisfy it. There is no automated human approval.

Attach private WAV/PNG/MP4/plain-text preview samples up to64KiB; file signatures are checked, and private preview verifies stored checksums. This does not qualify audio pronunciation, likeness consent, codec quality or production media. Replacing a slot or editing text expires approval. Older attachments remain immutable history and conservative rights dependencies; current previews show the latest slot. A text revision needs freshly attached/reviewed assets.

Schedule a current approval within30days, then use PublishDryRun when due. Only LocalPreview is supported. Published describes the editorial workflow with an explicitly DryRun copy, never actual public distribution; public knowledge remains false. No scheduler or platform delivery worker is added. Real publication and external takedown/edit delivery belong to M18.

Submit a correction for one owned claim revision or source snapshot. It propagates through same-content history and both content-target and asset-target provenance families across articles/translations/episodes/exports/posts. Asset edges resolve to their owning content revision with tenant/universe checks. Internal tasks require a new verified revision or withdrawal. When old immutable ancestry contains withdrawn evidence, create an independent reviewed replacement and enter its content ID before Resolve; the old content is archived, replacement IDs stay in immutable history, and old source text never regains export permission. A corrected immutable source or claim requires replacement evidence, not a relabeled old source. External tasks record Attempted/Unknown/Unsupported/Confirmed outcomes; confirmation needs real manual evidence. Every attempt is retained in immutable scoped editorial history, including external tasks for legacy content. Unsupported/unknown platform outcomes remain visible.

Author creates/edits only their own work. Editor reviews/approves/corrects. Publisher schedules/records preview copies and external remediation outcomes. Administrator also owns provider configuration. Actual owning tenant membership is checked in addition to HTTP role. Stale rowversions return conflicts; stable operation IDs replay only the identical actor/request.

The provider section lists safe profiles, capability/policy and qualification evidence, route versions and connection enabled/killed/credential-availability booleans. No secret reference/value is returned. Save validates a typed route; activation/rollback uses the current route-head rowversion. Emergency disable blocks the original owned provider. Reuse /staff/characters for guest creation, profiles/aliases, assets, casting overrides, promotion/retirement and usage/continuity.

The manual trend queue accepts bounded event/source IDs, freshness dates, proposed teaching variant, cast brief, transcript reference, script preview and estimated cost. Missing verified event evidence, stale time, no approved matching variant, missing transcript, rights or cast problems block approval. Fluent prose never substitutes for evidence. Automatic trend ingestion/clustering belongs to M15.

For the actual Windows fixture demo, stop your API/debug host first:
~~~powershell
.\scripts\editorial-demo.ps1 -ManageApi
~~~

This creates an isolated GeetaStudio_M1Tests scope, actual M12 recorded-body fixture reports, original fictional text and tiny synthetic WAV samples. It approves both drafts, edits text/replaces audio over Windows-authenticated HTTP, checks approval expiration and checksum change, then restarts its owned API and checks durability. The demo also restarts its own host with the current Windows account configured as Reader and verifies real HTTP403 for approval, publication and route activation. Real SQL services separately check author/editor denials; focused HTTP tests verify isolation and the staff-action-header boundary. It stops only its own hidden hosts. IDs: .local/M13-fixture.json; evidence: docs/milestones/evidence/M13-demo.json.

Debug:
1. Open trusted C:\dev\GeetaStudio in VS Code. Stop foreground API/Web hosts.
2. Select existing **Geeta Studio: all**, press F5.
3. Place breakpoints in EditorialWorkbench.ActAsync, EditorialWorkbench.EditAsync, EditorialBinding.AssertExport and EditorialWorkbench.RouteAsync.
4. Load /staff/editorial; evaluate/review/approve a fixture, edit it, and inspect approval fingerprint/current status. Resume with F5, stop with Shift+F5.
5. The **Geeta Studio: M13 editorial fixture demo** task runs the isolated demonstration; port5100 must be free.

Fresh interactive M13 breakpoint and browser visual/mobile/accessibility checks are unverified unless separately recorded. Human/theological/native-language/age feedback, live providers, production media, platform publication/remediation, hosted CI/cold cache and production recovery/load checks remain open. M14 is not started.
