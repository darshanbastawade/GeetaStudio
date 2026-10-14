# M15 local run and debug

Verified12 October2026: clean build,88 offline/145 real SQL tests, full formatting, Windows restart/retraction demo and Development data preservation. Live sources and genuine human/public release remain pending.

Run from C:\dev\GeetaStudio with the pinned .NET 10 SDK, local SQL Server and the existing Windows staff setup. No provider key or paid call is needed for controlled fixtures. Preserve the existing Development database and source permissions.

## Verify and provision

```powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
```

The first command uses GeetaStudio_M1Tests for real SQL tests, then checks formatting. The second applies the additive M15 migration and existing restricted API grants to GeetaStudio_Dev; it does not reset data or upgrade source rights. Existing secret references stay outside source control.

## Controlled Windows demonstration

Stop existing debug hosts so port 5100 is free:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\trend-demo.ps1 -ManageApi
```

Equivalent VS Code task: **Geeta Studio: M15 trend fixture demo**. This creates explicitly synthetic, tenant-scoped fixtures only in GeetaStudio_M1Tests, uses actual M6 retrieval and M11 planning, registers two approved dynamic guests without rebuilding, reviews the script, produces two local text artifacts, performs an actual M12 fixture evaluation and M13 schedule, restarts its own API, and retracts the synthetic source. It checks that scheduled content becomes Corrected, remediation appears, and PublishDryRun returns 409 from the corrected state. No external post or media is produced. Owned API processes stop and environment overrides restore in finally. Results: docs/milestones/evidence/M15-demo.json. Earlier fixture records remain auditable; reruns create a fresh scope.

## Normal run and debug

Open the trusted folder C:\dev\GeetaStudio in VS Code. Select **Geeta Studio: all** and press F5. Existing API/Web/Worker profiles run on 5100/5200 with Development settings. API workbench: http://localhost:5100/staff/trends; the editorial page links to it. Use the configured Windows staff account. Read membership and actual tenant/universe grants are required even when a Windows role is assigned. Development contains no automatic trend fixture seed.

Separate API terminal:

```powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
```

Set breakpoints in TrendWorkbench.PollAsync/ReviewClusterAsync/LinkAsync, TrendStories.PrepareAsync/ReviewStoryAsync/ChangeAsync, FactoryExecution.ProcessNextAsync and EditorialWorkbench.ActAsync. Use **Geeta Studio: M15 attach Api** to attach to an already-running owned API; debugger attachment does not configure permissions or seed fixtures. New M15 interactive breakpoint hits and visual/browser usability remain unverified. Existing M0 debugging evidence does not qualify these new paths.

In /staff/trends, enter owned tenant and universe IDs, load current connectors/clusters/teaching links/stories/remediation, and use the bounded operation path with the owned Contracts DTO shape. Mutations require the staff header, correct role, operation ID and current row version. Reader can inspect but cannot write. It is a technical staff form, not a public reader screen.

## Source setup and review

Register a connector version with deployed source/extractor keys. A and B are explicitly controlled fixtures. trend.uploaded-rss.v1 accepts an already approved, permitted M3 feed snapshot, configured locale/region and Manual attestation; it never fetches its URL. RSS supplies metadata only. Article substance and authorized transcripts must be separately registered and rights-reviewed in M3. A manual URL/topic enters the same verification path, and URLs are identifiers, not instructions to fetch.

A cluster requires current approved M5 NewsClaim evidence, central-claim spans tied to its substance, primary material or independent reports, original-report attribution, current rights and an expiry no more than seven days ahead. Titles, missing metrics, undated RSS, copied reports and inaccessible captions cannot satisfy that gate. M6 retrieval plus an approved M5 interpretation/M9 variant yields a reviewed defensible connection or explicit NoSuitableGeetaConnection. No-match cannot be approved into a story.

Prepare a versioned brief using the shared M11 plan and current M10 casts. Explicit M15 script approval and M11 plan approval are both required before generation. Non-fixture events require Manual review; fixture review cannot qualify publication. Real-person/violence/tragedy/political/religious/child concerns require human editorial review. Sensitive child candidates are conservatively held in ranking. Factual context, uncertainty, analogy, interpretation and fictional humor remain separate snapshot fields.

One-minute API rechecks hold changed/expired clusters and deliver idempotent M13 correction tasks. Admission, leased generation, export and publication recheck current evidence synchronously, without waiting for the timer. Corrections retain old lineage and approvals as audit history. A Held/Corrected/Withdrawn story needs replacement evidence and a new reviewed brief/content version; clicking approval on the unchanged old script is insufficient. Scheduled timestamps do not auto-publish anything.

Live news/video/chart/calendar/RSS polling and live extraction are not qualified. The shipped source capability covers controlled fixtures and permitted offline uploads/manual submissions only. A new live source needs an approved adapter, permissions, licensing, data policy and sandbox evidence. No universal social feed, caption downloader or automatic M16–M18 media/publication is included. M12 genuine human release and M14 public rights/accessibility gates remain pending.
