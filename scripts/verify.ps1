param([switch]$Locked, [switch]$Sql, [switch]$LowMemory, [string]$ResultsDirectory = '.local/verification')
$ErrorActionPreference = 'Stop'
$savedSql = $env:GEETA_SQL_TESTS
$savedNodeReuse = $env:MSBUILDDISABLENODEREUSE
$savedBuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
if ($LowMemory) { $env:MSBUILDDISABLENODEREUSE = '1'; $env:DOTNET_CLI_USE_MSBUILD_SERVER = '0' }
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (![IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory = Join-Path (Get-Location) $ResultsDirectory }
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
    function Invoke-VerificationStage([string]$Stage, [string[]]$Arguments) {
        Write-Host "Verification stage: $Stage"
        $stageLog = Join-Path $ResultsDirectory "$Stage.log"
        "Verification stage: $Stage" | Set-Content -LiteralPath $stageLog
        & dotnet @Arguments 2>&1 | Tee-Object -FilePath $stageLog -Append
        "Exit code: $LASTEXITCODE" | Add-Content -LiteralPath $stageLog
        if ($LASTEXITCODE -ne 0) { throw "Verification stage '$Stage' failed with exit code $LASTEXITCODE. See $stageLog" }
    }
    $restoreArgs = @('restore', '.\GeetaStudio.sln')
    if ($Locked) { $restoreArgs += '--locked-mode' }
    Invoke-VerificationStage 'restore' $restoreArgs
    $buildArgs = @('build', '.\GeetaStudio.sln', '--no-restore')
    if ($LowMemory) { $buildArgs += @('--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false') }
    Invoke-VerificationStage 'build' $buildArgs
    $testArgs = @('test','.\GeetaStudio.sln','--no-build','--no-restore')
    if ($LowMemory) { $testArgs += '-m:1' }
    Invoke-VerificationStage 'offline-tests' ($testArgs + @('--filter', 'Category!=Sql', '--results-directory', $ResultsDirectory, '--logger', 'trx;LogFileName=offline.trx'))
    if ($Sql) {
        # Explicit integration profile. Failure to reach real SQL Server is a failure, never an in-memory substitute.
        & powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $env:GEETA_SQL_TESTS = '1'
        Invoke-VerificationStage 'sql-tests' ($testArgs + @('--filter', 'Category=Sql', '--results-directory', $ResultsDirectory, '--logger', 'trx;LogFileName=sql.trx'))
    }
    Invoke-VerificationStage 'format' @('format', '.\GeetaStudio.sln', '--verify-no-changes', '--no-restore')
    Write-Host 'Offline verification passed. SQL integration runs only with -Sql; no live providers invoked.'
} finally { $env:GEETA_SQL_TESTS = $savedSql; $env:MSBUILDDISABLENODEREUSE = $savedNodeReuse; $env:DOTNET_CLI_USE_MSBUILD_SERVER = $savedBuildServer; Pop-Location }
