param([switch]$Locked, [switch]$Sql, [switch]$LowMemory)
$ErrorActionPreference = 'Stop'
$savedSql = $env:GEETA_SQL_TESTS
$savedNodeReuse = $env:MSBUILDDISABLENODEREUSE
$savedBuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
if ($LowMemory) { $env:MSBUILDDISABLENODEREUSE = '1'; $env:DOTNET_CLI_USE_MSBUILD_SERVER = '0' }
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if ($Locked) { & dotnet restore .\GeetaStudio.sln --locked-mode }
    else { & dotnet restore .\GeetaStudio.sln }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($LowMemory) { & dotnet build .\GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false }
    else { & dotnet build .\GeetaStudio.sln --no-restore }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $testArgs = @('test','.\GeetaStudio.sln','--no-build','--no-restore')
    if ($LowMemory) { $testArgs += '-m:1' }
    & dotnet @testArgs --filter 'Category!=Sql'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($Sql) {
        # Explicit integration profile. Failure to reach real SQL Server is a failure, never an in-memory substitute.
        & powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $env:GEETA_SQL_TESTS = '1'
        & dotnet @testArgs --filter 'Category=Sql'
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    & dotnet format .\GeetaStudio.sln --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host 'Offline verification passed. SQL integration runs only with -Sql; no live providers invoked.'
} finally { $env:GEETA_SQL_TESTS = $savedSql; $env:MSBUILDDISABLENODEREUSE = $savedNodeReuse; $env:DOTNET_CLI_USE_MSBUILD_SERVER = $savedBuildServer; Pop-Location }
