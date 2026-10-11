# M10 local run and debugging

Use PowerShell in C:\dev\GeetaStudio. Reuses .NET 10, local SQL Server and private restricted development credentials. No new external account or paid provider.

## Build, migrate and check

~~~powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
~~~

M10Registry is additive: existing characters, aliases, core revisions and source permissions remain. ProvisionRuntime applies required development API grants; no database reset or existing password reset. NoBuild requires a current successful build. LowMemory limits build processes, not assertions or SQL timeouts.

## Run

Open three PowerShell terminals at the repository root, one command in each:

~~~powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

Open http://localhost:5100/staff/characters, or the character link on http://localhost:5200. Windows staff authorization and actual tenant Administrator membership are required to edit/review; Readers can inspect. Browser actions supply the staff action header. The separate Worker retains the runtime role; M10 has no new background processor.

1. Select tenant/universe, **Load registry**, then **Propose universe defaults**. Seven accepted Geeta identities get Draft profiles, preserving their IDs, life stages, aliases and existing persistence scope. Neeraja remains Neerja's alias. Shakuni's Coach/Guru/Adviser are presentation drafts of one fictional identity with stable anchors.
2. Review the bible and profiles against actual editorial evidence. Use manual: references only for real human review; fixture: means test-only. Draft seed data and pronunciation guides are not completed human reviews. Review a profile before activating it.
3. Choose **New guest**, enter a unique key, canonical life stage, traits, goals, roles, audience, locale, situation/skill/objective/continuity tags, guides and identity anchors. Save. EpisodeOnly requires a previously registered episode ID. A changed life stage/identity requires **Duplicate as a distinct identity**; edits append versions.
4. Register an episode, choose an approved bible, audience/locale, roles and constraints. **Find eligible cast** explains exclusions and proposes an unregistered guest for missing roles. Deliberately pin a compatible new Draft guest to use it immediately in a draft. Retired, rejected, foreign or incompatible characters cannot be forced through.
5. Enter original fictional beats. Use profile traits and give every Lead a decision. Pin approved relationship versions. Save an episode revision. Draft-profile snapshots cannot later be approved by merely reviewing the profile: review/activate first, then save a new explicit episode revision.
6. Record story review. Satire, real-person and current-event stories require a separate escalation review. Disability ridicule, religious demeaning, real-suffering jokes and false scripture/history claims are blocked. Human reviewers must also assess undeclared risks in prose; metadata/lexical checks are not universal semantic classifiers.
7. Promote an active reviewed Guest/EpisodeOnly to Recurring, or retire it. Historical episode profiles, original scope, cast roles, presentations and relationships remain pinned. New versions create explicit reissues.
8. Presentation guides can be approved for text drafts, but release additionally needs real approved visual/voice asset references with current rights and stored bytes, manually reviewed profiles/bible/story/relationships, current bound approved content matching the beats, and any linked lesson's M09 export qualification. **Check qualified export** checks these gates; it does not publish.

Humor can be disabled per universe/audience/subject. The preferred 2–4 speakers is editable guidance; a brief has its own operational maximum up to 20. Distinct-role casting searches compatible assignments within the total cost budget; unusually complex briefs exceeding 10,000 search steps must be narrowed or explicitly pinned. Estimated costs are editor inputs, not live vendor quotes.

## Repeatable Windows demonstration

Stop API/debug hosts so port 5100 is free:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\registry-demo.ps1 -ManageApi
~~~

Or **Tasks: Run Task → Geeta Studio: M10 character fixture demo**. Creates an isolated original-fiction tenant in GeetaStudio_M1Tests, proposes/registers an unseen coach, proves immediate pinned-draft use, reviews/activates it with explicit fixtures, promotes the same ID and checks the old Guest/profile snapshot from a new service scope. Windows Negotiate checks the staff page and stored story. Starts/stops only its own hidden API, restores environment overrides and writes sanitized M10-demo.json. Fixture export must fail. It appends test records; no source permission changes or live calls.

## VS Code F5

Open C:\dev\GeetaStudio as a trusted folder. Stop CLI hosts. Select **Geeta Studio: all** and press F5; open http://localhost:5100/staff/characters. Shift+F5 stops the compound. Existing Development ports are API5100/Web5200.

Breakpoints in UniverseRegistry:
- RegisterCharacterAsync: stable core ID, alias collision, immutable life stage/anchors, new profile version.
- CastAsync: eligibility reasons, pinned overrides, distinct roles, total budget.
- ComposeAsync: frozen cast/profile/presentation/relationship snapshot and story policy.
- ReviewAsync: current review rowversion, profile qualification and escalation.
- ControlCharacterAsync: activation, retirement and same-ID promotion.
- ExportAsync / CheckStoryEvidence: manual qualification, current evidence and media/content gates.

Automated Windows host/HTTP execution is separate from interactive debugging. A fresh M10 interactive F5/breakpoint and visual/accessibility/mobile session is not verified. Existing M0 debugger evidence is historical.

## Limits

M16–M17 produce/qualify detailed media; M10 stores reviewed references and guides. No real person is authorized by a name: identity evidence and explicit likeness/voice/history permission are separately required, and fixture identity claims cannot qualify release. Existing M04 sample permissions remain read-only. Human continuity, humor, pronunciation, audience, language and rights review remains mandatory. List endpoints currently return at most 200 records; large registry pagination/operations UI is a follow-up. No M11 implementation was started.