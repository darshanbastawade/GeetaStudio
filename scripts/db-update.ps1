param(
    [ValidateSet('GeetaStudio_Dev','GeetaStudio_M1Tests')][string]$Database = 'GeetaStudio_Dev',
    [string]$Server = 'localhost',
    [switch]$ProvisionRuntime,
    [switch]$SeedCore,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
if ($Server -notin @('localhost', '.', [Environment]::MachineName)) { throw 'Only an explicit local SQL Server target is allowed.' }
if ($ProvisionRuntime -and $Database -ne 'GeetaStudio_Dev') { throw 'Provision runtime identities only for the development database.' }
$saved = @{}
foreach ($name in @('DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','GEETA_MIGRATION_DATABASE','GEETA_MIGRATION_SERVER')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $env:DOTNET_ENVIRONMENT = 'Development'; $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:GEETA_MIGRATION_DATABASE = $Database; $env:GEETA_MIGRATION_SERVER = $Server
    $operation = '--migrate-development'; if ($SeedCore) { $operation = '--seed-development' }; if ($ProvisionRuntime) { $operation = '--setup-development' }
    $runArgs = @('run','--project','src/GeetaStudio.Api','--no-launch-profile')
    if ($NoBuild) { $runArgs += '--no-build' }
    & dotnet @runArgs -- $operation
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
}
