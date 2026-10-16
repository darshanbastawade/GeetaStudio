param(
    [switch]$RequireProduction,
    [switch]$SkipBuild,
    [string]$Manifest = 'ops/release-baseline.json',
    [string]$Evidence = 'ops/release-evidence.json',
    [string]$Output = 'docs/milestones/evidence/M25.3-release-decision.json'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    if (-not $SkipBuild) {
        & dotnet build src/GeetaStudio.Api/GeetaStudio.Api.csproj --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw 'Release evaluator build failed.' }
    }
    # The same evaluator runs in the host and here; check-only never starts HTTP or background workers.
    $reportPath = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $repo $Output }
    $arguments = @('run','--project','src/GeetaStudio.Api','--no-build','--no-restore','--no-launch-profile','--',
        '--Release:CheckOnly=true', "--Release:Root=$repo", "--Release:Manifest=$Manifest", "--Release:Evidence=$Evidence", "--Release:Output=$reportPath")
    if ($RequireProduction) { $arguments += '--Release:RequireProduction=true' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Release blocked or evaluator failed; inspect $Output. No deployment performed." }
    Write-Host "Current release decision written to $Output. This command grants no deployment authorization."
} finally { Pop-Location }
