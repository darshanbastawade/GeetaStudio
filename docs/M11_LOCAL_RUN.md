# M11 local run and debugging

Use PowerShell in C:\dev\GeetaStudio. Reuses .NET10, local SQL Server and private restricted development credentials. Local templates do not call an AI provider or produce media.

## Build, migrate and check

~~~powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
~~~

The additive M11Factory migration preserves prior data and adds five factory tables plus scoped reference keys. NoBuild requires a successful current build; omit it before building. No database reset/password reset. LowMemory changes build orchestration, not assertions/timeouts.

## Run

One command in each of three repository-root terminals:

~~~powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

Open http://localhost:5100/staff/factory or the factory link on http://localhost:5200. Windows staff access and actual tenant Administrator membership guard mutations; Readers can inspect. The Development API launch profile and existing VS Code API profiles enable Factory__WorkerEnabled=true. The API-hosted processor uses its restricted content identity; the separate Worker remains the runtime worker and gets no new content grants. For manual API-executable startup, set Factory__WorkerEnabled=true explicitly to enable batch processing.

1. Create reviewed source lessons in /staff/lessons and compatible reviewed/active characters, bible, episodes, optional presentations and relationships in /staff/characters.
2. Select one to three reviewed lessons for the same audience/locale. Enter cast situation/skills/roles/continuity, optional pins and approved relationships. **Check cast** explains missing/incompatible roles; register a suitable guest through M10 and repeat. Leads must fit the writing profile's canonical life stages; compatible older mentors can support them. Production/media-required casts need reviewed stored assets.
3. **Propose chronological and practical journeys** creates two Draft plans from actual approved corpus locators. No generated scripture or invented verse IDs. Inspect source/interpretation/cast and record actual editorial review with a manual: evidence reference. fixture: is explicitly test-only. Proposals do not fabricate human approval.
4. Edit a plan as its next version. Change individual format directions/limits, priority, journey position, media/review estimates or deployed local template version. Old plans and cast/source snapshots stay pinned. Directions and generated treatments remain subject to M10 fiction/attribution rules.
5. **Show effort and limits** displays six-format item counts, worst-case generated characters, zero local generation cost and editor-estimated media/review effort. Media estimates are informational; no media spend is authorized or charged here.
6. Give a stable business request key, original deadline, item and character budgets, then **Queue review drafts**. Refresh inventory to watch checkpoints. Same key with exact plan/budget reuses the same batch; changed inputs require an explicit new key/version.
7. Pause/resume preserves the original deadline and accepted outputs. Cancel prevents new admission after cancellation commits. Expired claims recover with a fenced lease and at most three attempts; local generation plus artifact/core revision/provenance/checkpoint commit atomically. There are no remote side effects or model charges to reconcile in this milestone.
8. Inspect six separately labeled explanation/story/comic-script/short-video-script/carousel/reflection drafts. Their canonical evidence bundle, lesson, template, cast, relationship and core revision references stay traceable. Exact unchanged derivatives reuse IDs, including previously reviewed material; changed derivatives become deliberate versions. Changed source/audience/locale creates a new core content family while retaining artifact revision lineage.
9. Record artifact review separately. Coverage distinguishes planned, generated, reviewed, recorded-published, currently publicly usable, reused, duplicate-rejected and rights-withheld inventory. Review approval is not publication. Actual publishing is not implemented here.
10. Topic fatigue counts repeated learning treatments rather than six derivative formats; suggested priority combines editorial priority with a fatigue penalty. Work execution follows the declared editorial priority and journey position. Token-set overlap/body hashes are conservative duplicate proxies, not neural semantic or demand validation. Rejected duplicates remain visible; deliberate edits to an existing plan family are explicit reissues.

Original core content is recorded as labeled mixed fictional treatment with attributed source evidence. Factory read/export-readiness rechecks current lesson/evidence/rights; revoked output is withheld. Owning staff's existing core evidence-inspection access remains available for review, while public/shared/export gates retain their existing checks.

## Visible Windows restart demonstration

Stop existing API/debug hosts so port5100 is free:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\factory-demo.ps1 -ManageApi
~~~

Or **Tasks: Run Task → Geeta Studio: M11 draft factory fixture demo**. Uses only GeetaStudio_M1Tests and an isolated original fixture tenant with two exact stored Besant/Sanskrit verse inputs. Its source permissions and editorial references are explicitly fixture-only, not changes to development permissions or genuine human theological reviews.

The harness prepares two reviewed fixture pilot plans, generates one accepted draft and pauses. It starts an actual Windows API worker host, verifies the persisted pause, stops that owned host, starts another, resumes and waits for all12 linked drafts. It proves the first artifact survives, replay returns the same batch and coverage reports12 generated/0 reviewed/0 published/0 publiclyusable. Windows Negotiate checks the staff page and APIs. Both owned hidden hosts stop, environment overrides restore, sanitized evidence is saved to docs/milestones/evidence/M11-demo.json. Repeating appends isolated fixture records, not public content.

## VS Code

Open trusted C:\dev\GeetaStudio; stop CLI hosts; select **Geeta Studio: all**, press F5 and open http://localhost:5100/staff/factory. Shift+F5 stops the compound. Do not run the fixture task while F5 owns5100.

Useful breakpoints:
- DraftFactory.Resolve / RegisterPlanAsync: approved lesson, exact locator, life stage and M10 cast/relationship snapshot.
- ReviewPlanAsync / EstimateAsync / StartAsync: human/fixture references, scoped replay and bounded original request.
- Claim / ProcessNextAsync: leases, current state/cancellation fence, pinned renderer, duplicate/reuse and atomic completion.
- FactoryProcessor.ExecuteAsync: API-hosted processing/restart.
- ReviewArtifactAsync / CoverageAsync / PubliclyUsable: current evidence, truthful inventory and independent release gates.

Actual Windows host/restart/HTTP acceptance is distinct from interactive debugger or browser visual QA. Fresh M11 F5/breakpoints, visual/mobile/accessibility and hosted CI/cold-cache checks are not claimed unless separately recorded.

## Limits and release gates

local-template.a.v1 / b.v1 are deployed local layout versions over reviewed authored sections, not machine-generation vendors. Template version switches pin old artifacts and need no rebuild between deployed versions. Source/cast/profile changes require explicit reviewed plans. A remote generator must use M07's owned admission, pricing, AI/data permissions and qualification; it cannot be smuggled through the zero-cost synchronous local template contract.

Generation cost is genuinely zero for these templates; character/item/deadline quotas still bound resource use. Headers are staff-format labels, not a claim of multilingual product quality. Media is not rendered. Published counts are recorded source-use declarations; this milestone does not independently confirm platform delivery. Public usability additionally requires manual reviews/current core approval/current lesson and source rights plus qualified cast media. Human source/meaning/age/humor/continuity/pronunciation/language and actual adaptation/publication permissions remain open. Development M04 source permissions remain read-only.

Capacity is intentionally small:10 briefs/6 formats/60 items,200 plans/500 artifacts/5000 work outcomes per scoped coverage query. At the coverage cap, totals fail explicitly rather than silently truncating. List pagination/archival, calibrated semantic/evaluation quality, detailed media and real publishing belong to later selected milestones. M12 has not started.