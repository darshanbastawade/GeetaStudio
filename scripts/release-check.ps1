param([switch]$RequireProduction)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
$r=Get-Content (Join-Path $repo 'ops/release-baseline.json') -Raw|ConvertFrom-Json
foreach($feature in $r.features){if(-not(Test-Path -LiteralPath (Join-Path $repo $feature.evidence))){throw "Missing feature evidence:$($feature.key)"}}
foreach($reference in $r.platformEvidence){if(-not(Test-Path -LiteralPath (Join-Path $repo $reference))){throw "Missing platform evidence:$reference"}}
$missing=@('Human M12 and source/media review','Production TLS/network/secret/retention acceptance','Offsite encrypted SQL/log/asset restore and old binary rollback','Required real provider qualifications')
$result=@{date=(Get-Date -Format 'yyyy-MM-dd');features=$r.features;productionReady=$false;blockers=$missing;scope=$r.release}
$result|ConvertTo-Json -Depth 8|Set-Content (Join-Path $repo 'docs/milestones/evidence/M25-release-decision.json')
if($RequireProduction){throw 'Production release blocked; see M25 release decision. Manual booleans cannot qualify required evidence.'}
Write-Host 'Private pilot checklist references present; production release remains blocked.'
