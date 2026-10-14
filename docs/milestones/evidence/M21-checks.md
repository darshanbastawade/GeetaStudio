# M21 final checks — 14 October 2026

Controlled/private system: **Offline Verified**. Live Sandbox: **Not Verified**. Production: **Not Qualified**.

| Command actually run | Result |
|---|---|
| `scripts/verify.ps1 -Locked -Sql -LowMemory` | Exit 0; SDK 10.0.401 locked restore, build with 0 warnings/errors, 105 offline + 200 real SQL tests passed, 0 failed/skipped, final format clean. SQL duration 8m59s. |
| `dotnet test tests/ContentOS.Foundation.Tests --no-build --no-restore --filter FullyQualifiedName~Talk` with `GEETA_SQL_TESTS=1` | 16/16 passed: 5 offline and 11 SQL/HTTP. Includes unfunded-source denial, current rights, contradictory output, parent/minor limits, memory withdrawal, authentication/CSRF/isolation, original route/replay/restart, cancellation and unknown/unadmitted credit reconciliation. |
| `scripts/talk-demo.ps1 -ManageApi` | Final repeated Windows demo passed: private page200/no-store, supported citations, no-match without AI, other account403, A→B new turns, original replay after actual API restart, parsed validated streaming, report/forget, credits7/held0. Owned hosts stopped. |
| `scripts/db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild` | Additive TALK migration applied; no reset/drop. |
| `scripts/db-update.ps1 -Database GeetaStudio_Dev -ProvisionRuntime -NoBuild` | Additive migration, restricted identities and existing idempotent seed preserved; 23 Core/4 Runtime. No secret values printed. |
| `.local/M21-grants-check.ps1` | 25 narrow API permission rows; no Worker, immutable ownership/operation update or DELETE grants. |
| `.local/M21-development-check.ps1` | Actual restricted Windows `/talk` and `/staff/talk`200; scoped policy/problem reads empty; no fabricated customer account, payment fixture key, TALK approval or source permission. |
| `.local/M21-preservation.ps1 -After` | Original IDs/counts: 8 characters, 3 universes, 50 corpus entries, 10 jobs, 5 source permissions. Full permission hash unchanged; migration counts23/4. |
| `dotnet ef migrations has-pending-model-changes --project src/ContentOS.Infrastructure --context CoreDbContext --no-build` and RuntimeDbContext equivalent | Both exit0: no pending model changes. |
| PowerShell parser, VS Code JSON parser, `node --check` for both TALK pages, `git diff --check`, `scripts/dev-doctor.ps1` | Passed; exact SDK, Git/VS Code and free loopback5100/5200 confirmed. These do not prove interactive debugging. |

Final logs: `.local/M21-verify.log`, `M21-focused-final.log`, `M21-demo-final.log`, `M21-restore.log`, `M21-build-final.log`, `M21-core-model.log`, `M21-runtime-model.log`, `M21-development*.log`, `M21-grants.log`, `M21-preservation.log`, `M21-doctor.log`. Focused TRX is under `tests/ContentOS.Foundation.Tests/TestResults/M21-focused-final.trx`.

Earlier failures were real and resolved: index setup SQL timeout/orphan selection; fixture baseline reconciliation; empty concept mapping; Web/Pascal JSON route/payload mismatch; Windows helper platform guard and PowerShell challenge-auth/byte-stream decoding; SDK roll-forward NU1004. Existing package versions, assertions, SQL timeouts, corpus/rights/budget rules and original records were retained. Scoped target-index processing and exact installed SDK pin are the bounded prerequisite fixes. The one-verse fixture is explicitly incomplete, with fixture reviews; it cannot qualify genuine theological/human review. The final full run passed, but this does not establish a universal SQL reliability cure.

Source/age/audience/persona/consent/rights and credit/provider-budget boundaries are implemented now. Human M12 reviews remain0, live private text qualification/credentials, real age/privacy/legal retention, genuine safety/persona/source review, browser/mobile/accessibility and fresh F5/breakpoints remain unverified. Paid/external provider calls0, public deployments0, commits0; M22 not started.

[Milestone](../M21.md), [exact Windows run/debug](../../M21_LOCAL_RUN.md), [machine record](M21-checks.json), [Windows demo](M21-demo.json), [Development](M21-development.json), [grants](M21-grants.json), [preservation](M21-preservation.json), [static](M21-static.json).
