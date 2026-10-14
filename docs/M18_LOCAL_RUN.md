# M18 local publishing run and debug

Run from C:\dev\GeetaStudio in PowerShell. Prerequisites are the pinned .NET 10 SDK, the existing local SQL Server/Test/Development databases, and VS Code C# extensions. M18 reuses the owned editorial, source, media and WATCH gates. No social credential or paid account is required for the shipped export/fixture paths.

~~~powershell
Set-Location C:\dev\GeetaStudio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked -Sql -LowMemory
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild
~~~

Use the existing **Geeta Studio: all** F5 compound. Open http://localhost:5100/staff/social with your authorized Windows account and owned tenant/universe codes. The API runs the publishing processor when Runtime:Enabled and Social:WorkerEnabled are true; the standalone Worker keeps its existing runtime-only permissions.

Register a destination account version for YouTube, Instagram, Facebook or X. **ExportOnly** uses manual.export.v1 and manual: binding evidence; it is not connected or qualified. **Fixture** uses an explicit fixture: account reference and a local simulation adapter. Declared fixture scopes never establish real platform authorization. Disable/revoke blocks new submission; renewal or vendor changes append an account version for the same platform/account/key. Existing packages and posts keep their old binding.

Choose approved editorial content, an account, destination format, campaign and caption/title/link. Video/short packages require an approved WATCH master of that exact content revision. Approved PNG images support image posts, carousel slides and thumbnails. Carousel text preserves graphemes; articles retain the exact canonical body. Explicit destination metadata is reviewed separately. Conservative versioned local profiles reject excessive text/media and unsupported formats; they do not assert every live account's current API entitlements.

Preparation creates an immutable package and a Draft publication. Review the exact payload hash, metadata, fiction/claim/citation labels and media hashes. Technical **Fixture** reviews cannot authorize public release. Scheduling requires Publisher/Editor/Administrator membership and current package/core/media/source approval; UTC instants retain the selected timezone. Export-only schedules require manual action and never enter automatic dispatch.

Unknown or expired submitting work requires **Reconcile** at the original adapter/platform/account. A vendor change affects newly prepared packages; no fallback swaps the destination. Only confirmed-not-submitted rate limits defer safely within the original deadline/attempt limits. Uncertain work is never blindly reposted. Withdrawal retains remote history and requires actual manual takedown evidence for an external post. In-app notifications track review/recovery; no email or external messages are sent.

Download the private reviewed manifest, then retrieve its exact media through the authenticated M16 /versions/{id}/preview or M17 /artifacts/{id}/preview paths. Article/caption/slide text and original hashes are in the manifest. Current rights/review changes can withhold the manifest/media. Manual upload/recording requires genuine publication rights, current manual content/package/WATCH review and actual platform/account authorization. A manual receipt's external URL/ID is an attested record, not independently verified provider qualification.

## Repeatable controlled demonstration

Stop existing debug hosts so port 5100 is free:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\social-demo.ps1 -ManageApi
~~~

This uses only GeetaStudio_M1Tests. It reuses the private M17 English spoken WATCH fixture, creating it with the M17 demo if absent (pinned FFmpeg and an installed English Windows SAPI voice are required for that prerequisite). It prepares four distinct reviewed exports from the same original content, simulates an unknown result, appends vendor B for the same X account, reconciles at original vendor A, checks duplicate receipt handling, restarts the API, checks Windows staff HTTP and preserves core text/post bindings. No social platform is contacted. Outputs are .local/m18-demo; only owned demo hosts are stopped and environment overrides are restored.

For focused checks after a successful build/Test migration:

~~~powershell
$env:GEETA_SQL_TESTS = '1'
dotnet test .\tests\ContentOS.Foundation.Tests --no-build --no-restore --filter 'FullyQualifiedName~SqlSocial|FullyQualifiedName~SocialRuleTests'
~~~

## Debug the concrete fixture

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\social-demo.ps1 -ManageApi -DebugPause
~~~

Or use VS Code task **Geeta Studio: M18 publishing demo**. At the pause, choose **Geeta Studio: M18 attach Api**, F5, and the printed API process. Open /staff/social using the printed tenant/universe. Useful breakpoints: SocialPublishing.PrepareAsync, ReviewAsync, ScheduleAsync, Guard, ControlAsync and ReceiptAsync; SqlSocialEffectLedger.FindAsync for reconciliation. Processing breakpoints are in ProcessNextAsync/AdmissionForClaim and SocialFixturePublisher.SubmitAsync. The reproducible demo disables processors; a separate newly reviewed fixture schedule with Social:WorkerEnabled=true is needed for processing breakpoints. Continue/detach, then press Enter in the demo terminal to finish duplicate/restart checks and cleanup.

Fresh interactive F5, browser/mobile/screen-reader usability, actual live OAuth/token refresh/webhook-signature handling, per-account publish/retrieve verification, real provider budgets/rate limits/monetization eligibility, M12 genuine100+20 acceptance and actual source/media/publication permissions remain release gates. Shipped providers are local fixtures, not qualified external adapters. Real accounts/permissions are unavailable; export/manual workflows remain available with release guards. Meta official publishing pages could not be retrieved during this implementation (unavailable/429), so current Meta API/version/permission details require fresh official verification before a live adapter is enabled. M19 is not started.
