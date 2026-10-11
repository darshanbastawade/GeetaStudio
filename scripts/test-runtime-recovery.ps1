$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$owned = @()
$savedEnvironment = @{}
foreach ($name in @('DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
Push-Location $repo
function Start-Owned([string]$HostName, [string]$Label) {
    $target = Join-Path $repo "src/GeetaStudio.$HostName/bin/Debug/net10.0/GeetaStudio.$HostName.dll"
    if (-not (Test-Path -LiteralPath $target)) { throw 'Build the solution before recovery verification.' }
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $target -WorkingDirectory (Join-Path $repo "src/GeetaStudio.$HostName") -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $repo ".local/recovery/$Label.out.log") -RedirectStandardError (Join-Path $repo ".local/recovery/$Label.err.log")
    return @{ Id = $process.Id; Target = $target; Label = $Label }
}
function Stop-Owned([object]$Entry) {
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($Entry.Id)"
    if (-not $process) { return }
    if ($process.CommandLine -notlike "*$($Entry.Target)*") { throw 'Process identity changed; refusing to stop an unrelated process.' }
    Stop-Process -Id $Entry.Id -Force
}
$auth = @{ UseDefaultCredentials = $true; Headers = @{ 'X-ContentOS-Staff' = '1' } }
if ($PSVersionTable.PSVersion.Major -ge 7) { $auth.AllowUnencryptedAuthentication = $true }
function Staff([string]$Path, [object]$Body = $null) {
    $options = @{ Uri = "http://localhost:5100/api/v1/runtime/$Path"; TimeoutSec = 5 }
    if ($null -ne $Body) { $options.Method = 'Post'; $options.ContentType = 'application/json'; $options.Body = ($Body | ConvertTo-Json -Depth 6) }
    Invoke-RestMethod @options @auth
}
try {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'dev-doctor.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Run recovery verification with both host ports free.' }
    New-Item -ItemType Directory -Path .local/recovery -Force | Out-Null
    $env:DOTNET_ENVIRONMENT = 'Development'; $env:ASPNETCORE_ENVIRONMENT = 'Development'; $env:ASPNETCORE_URLS = 'http://localhost:5100'
    $api = Start-Owned 'Api' 'api'; $owned += $api
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        try { $response = Invoke-RestMethod http://localhost:5100/health/ready -TimeoutSec 2; if ($response.status -eq 'Ready') { $ready = $true; break } } catch { }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Runtime not ready. Run db-update.ps1 -ProvisionRuntime first.' }
    $provider = Staff 'providers' @{ adapterKey = 'test.a'; maxConcurrency = 1 }
    $revision = Staff 'revisions' @{ profileId = $provider.profileId; maxAttempts = 3; budget = 3; deadlineSeconds = 60 }
    Staff "revisions/$($revision.id)/activate" @{} | Out-Null
    Remove-Item Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
    $a = Start-Owned 'Worker' 'worker-a'; $owned += $a
    $b = Start-Owned 'Worker' 'worker-b'; $owned += $b
    $job = Staff 'jobs' @{ scenario = 'slow' }
    $committed = $false
    for ($i = 0; $i -lt 80; $i++) {
        $before = Staff "jobs/$($job.id)"
        if ($before.job.state -eq 'Running' -and $before.operation.state -eq 'Submitted') {
            $count = & sqlcmd -S localhost -d GeetaStudio_Dev -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM runtime.Effects WHERE Id='$($before.operation.id)';"
            if ($LASTEXITCODE -ne 0) { throw 'SQL effect inspection failed.' }
            if (($count -join '').Trim() -eq '1') { $committed = $true; break }
        }
        Start-Sleep -Milliseconds 100
    }
    if (-not $committed) { throw 'No submitted, committed effect observed before controlled crash.' }
    $winner = $null
    foreach ($entry in @($a,$b)) {
        $log = Get-Content -LiteralPath (Join-Path $repo ".local/recovery/$($entry.Label).out.log") -Raw
        if ($log -match "SQL runtime worker started $($before.job.leaseOwner)") { $winner = $entry }
    }
    if ($null -eq $winner -or $before.attempts.Count -ne 1) { throw 'Cannot identify the sole claiming worker.' }
    Stop-Owned $winner
    $replacement = Start-Owned 'Worker' 'worker-restarted'; $owned += $replacement
    for ($i = 0; $i -lt 120; $i++) {
        $after = Staff "jobs/$($job.id)"
        if ($after.job.state -eq 'Completed') { break }
        Start-Sleep -Milliseconds 250
    }
    if ($after.job.state -ne 'Completed' -or $after.attempts.Count -ne 2 -or -not $after.attempts[1].reconciliation -or $after.operation.id -ne $before.operation.id) { throw 'Durable recovery or original-binding reconciliation failed.' }
    $count = & sqlcmd -S localhost -d GeetaStudio_Dev -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM runtime.Effects WHERE Id='$($after.operation.id)';"
    if ($LASTEXITCODE -ne 0 -or ($count -join '').Trim() -ne '1') { throw 'Unexpected business-effect count.' }
    # Evidence contains identifiers/state only; no credentials or provider payloads.
    @{ jobId = $job.id; operationId = $after.operation.id; crashedPid = $winner.Id; competingPids = @($a.Id,$b.Id); restartedPid = $replacement.Id; attempts = $after.attempts.Count; firstState = $after.attempts[0].state; recoveredByReconciliation = $after.attempts[1].reconciliation; effects = 1; state = $after.job.state } | ConvertTo-Json -Depth 6 | Set-Content .local/recovery/evidence.json
    Write-Host "SQL recovery passed: two workers contended, owned PID $($winner.Id) crashed after commit, job $($job.id) recovered with two attempts and one effect."
} finally {
    foreach ($entry in $owned) { Stop-Owned $entry }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
    Pop-Location
}
