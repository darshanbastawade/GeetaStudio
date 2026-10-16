# Bounded loopback observation of an unqualified candidate; never a hosted release receipt.
param([ValidateRange(30, 600)][int]$ObservationSeconds = 300)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo '.local/M25-3-prompt9'
New-Item -ItemType Directory -Force $out | Out-Null
foreach ($port in @(15109, 15110)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port is occupied; refusing to replace another host." }
}
$keys = @('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','Runtime__Enabled','Release__Root','Release__Enforce','Staff__Operator__0','Api__BaseAddress','Read__PreviewOwner')
$saved = @{}; foreach ($key in $keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$api = $null; $web = $null; $result = $null
function Request([string]$path, [bool]$authenticated = $false) {
    $request = [Net.HttpWebRequest]::Create("http://127.0.0.1:15109$path")
    $request.Timeout = 5000; $request.AllowAutoRedirect = $false; $request.UseDefaultCredentials = $authenticated
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try { $response = $request.GetResponse() } catch [Net.WebException] { if (-not $_.Exception.Response) { return @{status = 0; elapsedMs = $timer.Elapsed.TotalMilliseconds} }; $response = $_.Exception.Response }
    try {
        $reader = [IO.StreamReader]::new($response.GetResponseStream())
        try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return @{status = [int]$response.StatusCode; body = $body; elapsedMs = $timer.Elapsed.TotalMilliseconds; nosniff = $response.Headers['X-Content-Type-Options']}
    } finally { $response.Dispose() }
}
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Production'; $env:DOTNET_ENVIRONMENT = 'Production'; $env:Runtime__Enabled = 'false'
    $env:Release__Root = $repo; $env:Release__Enforce = 'false'; $env:Staff__Operator__0 = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $api = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @('src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.dll','--urls','http://127.0.0.1:15109') -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'api.log') -RedirectStandardError (Join-Path $out 'api-error.log')
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) { if ((Request '/health/live').status -eq 200) { $ready = $true; break }; Start-Sleep -Milliseconds 500 }
    if (-not $ready) { throw 'Owned API did not start.' }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'; $env:DOTNET_ENVIRONMENT = 'Development'; $env:Api__BaseAddress = 'http://127.0.0.1:15109/'; $env:Read__PreviewOwner = ''
    $web = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @('bin/Debug/net10.0/GeetaStudio.Web.dll','--urls','http://127.0.0.1:15110') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Web') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'web.log') -RedirectStandardError (Join-Path $out 'web-error.log')
    $anonymous = Request '/api/v1/release/decision'
    $operator = Request '/api/v1/release/decision' $true
    if ($anonymous.status -ne 401 -or $operator.status -ne 200 -or ($operator.body | ConvertFrom-Json).ProductionReady -or $operator.nosniff -ne 'nosniff') { throw 'Authentication or enforced release boundary failed.' }
    $samples = @(); $clock = [Diagnostics.Stopwatch]::StartNew(); $observations = 0
    while ($clock.Elapsed.TotalSeconds -lt $ObservationSeconds) {
        $health = Request '/health/live'; $observations++
        if ($health.status -ne 200) { throw 'Unexpected availability failure during bounded observation.' }
        if ($samples.Count -lt 120) {
            $kind = $samples.Count % 3
            $path = @('/health/ready','/api/v1/read','/api/v1/release/decision')[$kind]
            # The runtime is deliberately disabled: readiness must report 503, not a false ready state.
            $expected = @(503,404,200)[$kind]
            $r = Request $path ($kind -eq 2)
            $samples += @{path = $path; expected = $expected; status = $r.status; elapsedMs = $r.elapsedMs}
            if ($r.status -ne $expected) { throw "Unexpected bounded workload status:$path/$($r.status)" }
        }
        Start-Sleep -Milliseconds 1000
    }
    Stop-Process -Id $api.Id -Force; $api.WaitForExit(15000) | Out-Null
    $failures = @(); for ($i = 0; $i -lt 3; $i++) { $failures += (Request '/health/live').status }
    if (@($failures | Where-Object { $_ -ne 0 }).Count) { throw 'Owned-host outage injection was not observed.' }
    $alert = @{observedUtc = [DateTimeOffset]::UtcNow.ToString('o'); destination = 'Local JSON only'; kind = 'ThreeConsecutiveHealthFailures'; statuses = $failures; notificationSent = $false; fixtureHost = $true}
    [IO.File]::WriteAllText((Join-Path $out 'local-alert.json'), ($alert | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    $sorted = @($samples.elapsedMs | Sort-Object)
    $result = [ordered]@{observedUtc = [DateTimeOffset]::UtcNow.ToString('o'); status = 'Pass'; qualification = 'Local empty-scope boundary/availability drill only'; apiEnvironment = 'Production'; webEnvironment = 'Development'; runtimeEnabled = $false; loopbackOnly = $true; tlsVerified = $false; productionSecretsOrRotationVerified = $false; anonymousDecision = $anonymous.status; operatorDecision = $operator.status; enforcementFalseCannotBypass = $true; workloadRequests = $samples.Count; workloadConcurrentClients = 1; workloadIntervalSeconds = 1; workloadErrors = 0; workloadP95Ms = $sorted[[Math]::Ceiling($sorted.Count * 0.95) - 1]; workload = $samples; healthObservationSeconds = $clock.Elapsed.TotalSeconds; healthyObservations = $observations; outageObserved = $true; localAlert = '.local/M25-3-prompt9/local-alert.json'; realAlertDeliveryVerified = $false; selectedContentLoadVerified = $false; externalEffects = 0}
} finally {
    foreach ($process in @($api, $web)) { if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit(15000) | Out-Null } }
    foreach ($key in $keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key], 'Process') }
}
foreach ($port in @(15109, 15110)) { if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw 'Owned listener remained active.' } }
if ($result) {
    $result.ownedHostsStopped = $true
    [IO.File]::WriteAllText((Join-Path $repo 'docs/milestones/evidence/M25.3-prompt9-local.json'), ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Write-Host 'Bounded local access/load/availability and outage alert drill passed. Public hosting and real content load remain unqualified.'
}
