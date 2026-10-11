# M09 local run and debugging

Use PowerShell in C:\dev\GeetaStudio. The existing .NET 10 SDK, local SQL Server, privately provisioned restricted SQL identities and Windows staff membership are reused. No paid provider, translation service or cloud account is needed.

## Build, migrate and verify

~~~powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
~~~

The additive M9Lessons migration preserves existing data and adds five lessons tables. ProvisionRuntime updates only the dedicated development database's existing API grants and seed configuration; it does not reset database data or passwords. NoBuild requires a successful current build. Omit NoBuild when running the updater before building.

LowMemory uses one build node and disables persistent build servers/shared compilation for this invocation. It restores environment overrides afterward. Tests, SQL command timeouts and acceptance assertions are unchanged. Use this option on an 8 GB workstation; it cannot guarantee enough memory if other applications occupy the machine.

## Run locally

Open three terminals at the repository root, one command in each:

~~~powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

Open http://localhost:5200 and choose **Compare audience lessons and review translations**, or open http://localhost:5100/staff/lessons with the provisioned Windows account. Read access requires the configured staff role and actual tenant/universe membership; mutations also require Administrator membership. M09 uses the API and its restricted content SQL identity. The standalone Worker remains the M01 runtime worker; existing M06/M08 API-hosted processors are preserved.

1. Select tenant/universe and load rules. **Propose initial rules** creates 12 Draft profiles: three audience versions, four writing treatments, three locale proposals and two local translation routes. Repeat is idempotent.
2. Read each profile. Edit rules as a new version using the form. Review age suitability, examples, sensitive topics, learning goals, glossary/pronunciation and route bounds. Record real approval only with a traceable manual: review reference. fixture: references are visibly test-only and cannot qualify export.
3. Select an approved M05 exact English quotation/translation and a separately attributed approved interpretation supporting the same imported verse/snapshot. Optional Sanskrit must be a retrieved canonical text of that same verse. Register the shared bundle.
4. Select approved audience, its bound writing treatment, approved locale and route. Create children/youth/adult review drafts and compare the six sections, source attribution, interpretation, goals and cast. A new profile version does not change old variants.
5. Foreign-language variants require exact approved translation memory for this bundle, locale profile and full English treatment. Choose an English variant, supply the five adapted sections, preserve identifiers/source anchor, add meaning-review notes and review the memory. Changing a source treatment requires a new exact memory entry. No English fallback is silently relabeled Marathi or Hindi.
6. Add a reviewed eligible fictional guest, retaining canonical life stage. Show eligible characters; new drafts can select this guest without rebuilding. Earlier cast snapshots remain unchanged. M09 does not create visual/voice assets or re-age existing identities.
7. Every variant starts Draft and needs its own review. **Check reviewed export** rechecks current evidence and permissions and rejects fixture-only qualifications. This does not publish.

Existing development M04 sample permissions are read-only and do not authorize adaptation. The system will block adaptation until independently reviewed permission evidence actually permits the proposed use. Do not edit those permissions merely to make the demo pass.

## Repeatable Windows fixture demonstration

Stop all debug/CLI API hosts so port 5100 is free, then run:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\lesson-demo.ps1 -ManageApi
~~~

Or **Tasks: Run Task → Geeta Studio: M9 lesson fixture demo**. This mode uses only GeetaStudio_M1Tests and creates an isolated fixture tenant/universe. It copies actual stored M04 sample text into distinctly labeled test sources with fixture-only permission evidence; it does not change development source permissions. It compares children/youth examples with unchanged anchor, interpretation and Sanskrit, activates an eligible Teen guest, confirms fixture export is blocked, then checks the staff page and variants using Windows Negotiate. It starts and stops only its own hidden API process and writes sanitized evidence to docs/milestones/evidence/M09-demo.json. Fixture preparation compiles a small ignored harness under .local; no live provider calls or translation quality claims.

Do not run this task while F5 owns port 5100. Repeated demonstrations append isolated test records, not production content.

## VS Code debugging

Open C:\dev\GeetaStudio as a trusted folder. Stop CLI hosts. Select **Geeta Studio: all**, press F5 and open http://localhost:5100/staff/lessons. Use the Development profiles and existing private connection setup. Shift+F5 stops the compound.

Useful breakpoints:

- LessonEngine.RegisterProfileAsync / ReviewProfileAsync: scoped rules, versioning, explicit review and rowversion.
- LessonEngine.RegisterBundleAsync / Recheck: approved M05 claims, corpus identity and current rights.
- LessonEngine.AdaptAsync: pinned rules, exact translation memory, policy gate and cast snapshot.
- ReviewedTranslationProvider.TranslateAsync: bounded typed port and explicitly selected adapter.
- LessonEngine.CandidatesAsync: data-driven life-stage/character eligibility.
- LessonEngine.ReviewVariantAsync / ExportAsync: mandatory review, source revocation and human qualification.

The automated Windows process/HTTP demo and test results are recorded in M09 evidence. A fresh interactive M09 F5/breakpoint session and browser visual/accessibility/mobile checks are not verified.

## Gates and limits

The two deployed adapters are local exact-reviewed-memory implementations, not machine translators. English preserves the owned treatment; Marathi/Hindi require reviewed stored target text. Routes A/B demonstrate switching while old variants retain the original route, memory, profile and cast. A new external translation vendor needs an adapter, account/data/rights policy, bounded qualification and explicit live authorization.

Human theological/meaning, audience suitability and native-speaker glossary/pronunciation/translation reviews remain required before release. Canonical Sanskrit is never regenerated. Minimal child data, no persistent conversation, behavioral advertising or personalization remain enforced pending M20. Lexical sensitive-topic checks are conservative configuration gates, not a universal safety classifier. No public distribution, production backup/restore or new external provider is qualified by this milestone.