# M09–M13 validation — 11 October 2026

Current disposition: all three findings fixed and locally verified; [fix evidence](M13-review-fixes.md). Final83 offline/138 SQL and Windows demo pass. The original review and counterexample observations below are preserved as history.

Historical result: changes required fixes before M13 acceptance could be closed. Three P1 defects were reproduced with real SQL in isolated, explicitly synthetic scopes. The existing regression suite passes but does not cover these counterexamples. This validation does not alter production code.

## Confirmed findings

### 1. [P1] Bind an evaluation to the content's audience

Location: src/ContentOS.Infrastructure/EditorialWorkbench.cs:128 and135.

Evaluate compares body and locale, and Approve checks body/findings and report qualification, but neither compares the evaluated case AudienceId to the content AudienceId. The probe created a child-audience draft and an adult-audience evaluation of the same body/locale, then successfully evaluated, reviewed and approved the child draft. The report and content audience IDs differ.

Fixture approval was reproduced; no genuine human release was fabricated. The same missing audience comparison exists in the Manual path, so obtaining a genuinely qualified adult report would not establish child suitability. Require the exact current audience and appropriate cast/production context before accepting the report, and recheck the binding at approval.

### 2. [P1] Traverse asset provenance when calculating lineage/correction impact

Location: src/ContentOS.Infrastructure/EditorialPackets.cs:14–16.

The traversal requires a ContentRevisionId on every provenance edge. Valid M03 asset-target edges instead carry AssetReferenceId and a null ContentRevisionId. Such edges are dropped even when ParentRevisionId points to the corrected content.

The probe linked an Episode's WAV asset to a source-backed parent, approved both and recorded one local dry-run copy. The child correctly inherited the original source in the rights traversal, yet neither its parent appeared in editorial lineage nor its content/copy appeared in the correction queue: parent tasks1, child tasks0. Resolve each asset edge to its owning content revision and include it in both traversal directions, scoped to the tenant/universe. Otherwise editors miss affected derivatives and copies requiring manual action.

### 3. [P1] Recheck factory cast eligibility before renewed approval

Location: src/ContentOS.Infrastructure/EditorialWorkbench.cs:95–112.

The new cast guard scans only RegistryRevision Story rows linked to content. M11 factory drafts carry their cast in FactoryArtifactSnapshot; their linked content need not have a Registry Story row. Fingerprinting those characters expires the old approval, but the Rights/Approve path never validates the factory cast.

The probe generated one Story draft with an active reviewed guest, adopted and approved it in M13, then retired the guest. The old approval expired as expected. A new recorded-body evaluation with no cast context allowed the same unchanged factory draft to be approved again: factory history1, character Retired, state Approved/current approval true. Apply the current pinned profile/presentation/role/audience/eligibility checks to every production snapshot and reject evaluation reports that omit required production context.

## Executed checks

| Check | Actual result |
|---|---|
| scripts/verify.ps1 -Locked -Sql -LowMemory | Exit0; build0 warnings/errors;83/83 offline and134/134 real-SQL tests;0 skips; formatting pass. SQL duration5m57s. Log:.local/M09-M13-validation.log. |
| Separate real-SQL counterexamples | All three defects reproduced; Probe build0 warnings/errors and process exit0 means the observation harness completed, not that these invariants passed. .local/M09-M13-probes.log and [counterexample observations](M09-M13-counterexamples.json). |
| CoreDbContext/RuntimeDbContext has-pending-model-changes | Both current model checks pass; no schema mismatch. |
| Windows doctor | SDK10.0.401, git/code available; loopback5100/5200 free. Does not establish F5. |
| Five current staff-page JavaScript scripts | Extracted from LessonPage/RegistryPage/FactoryPage/EvaluationPage/EditorialPage; node --check passes. |
| Five demo PowerShell scripts and VS Code JSON | Parser/configuration checks pass. |
| git diff --check | Pass; existing Windows line-ending warnings do not indicate a diff error. |

## Milestone assessment

| Milestone | Assessment |
|---|---|
| M09 | Implemented local audience/localization system; fresh regressions pass. Genuine linguistic/theological/age suitability and pronunciation reviews remain release gates. |
| M10 | Implemented local registry/casting system; fresh regressions pass. M13's factory snapshot consumer bypass is finding3, not a failure of registry selection. |
| M11 | Implemented local planning/template batch system; fresh regressions pass. Approval of its generated cast snapshots requires M13 finding3 to be fixed. |
| M12 | Implemented fixture/evaluation system; fresh regressions pass. Genuine100 reviewed Development+20 held-out cases and release acceptance remain pending per owner instruction. M13 must consume its audience/context correctly (finding1). |
| M13 | In Progress after review: three reproduced acceptance defects remain open despite passing existing tests. Earlier passing Windows demos remain historical evidence. |

## Scope and limits

Inspected milestone prompts/evidence and current audience, registry, factory, evaluation, editorial, provenance, approval, permissions and persistence boundaries. Existing uncommitted work was preserved. No production-code fix, dependency change, database reset, credential/source-permission upgrade, live/paid call, public publication, staging, commit or remote change. Only dedicated test scopes and local preview fixtures were added by checks. Existing additive migrations were applied normally by the established verification script.

Historical Windows HTTP/restart demos are retained; they were not freshly rerun during this audit. Fresh interactive F5/breakpoints, visual/mobile/accessibility, live providers, actual publication/remediation, hosted CI and production restore/load/privacy remain unverified. M04 still has25/701 sample entries per edition; genuine source/locale/media and human review remain required. No M14 work.

To rerun the full gate:
~~~powershell
Set-Location C:\dev\GeetaStudio
.\scripts\verify.ps1 -Locked -Sql -LowMemory
~~~

The ignored local observation harness remains available:
~~~powershell
$env:GEETA_REPOSITORY = (Get-Location).Path
dotnet run --project .\.local\m09-m13-review\Probe.csproj --no-build --no-restore
~~~
It deliberately uses fixture approvals, retains separate scopes, and prints observations without source text or credentials. Fix the three findings and add negative regressions before requalifying M13. M14 should remain unstarted.
