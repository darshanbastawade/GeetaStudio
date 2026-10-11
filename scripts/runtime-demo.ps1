param([string]$BaseAddress = 'http://localhost:5100')
$ErrorActionPreference = 'Stop'
$base = [Uri]$BaseAddress
if (-not $base.IsLoopback -or $base.Scheme -notin @('http','https') -or $base.UserInfo -or $base.AbsolutePath -ne '/') { throw 'This development demo is restricted to a loopback API root.' }
$baseText = $base.AbsoluteUri.TrimEnd('/')
$auth = @{ UseDefaultCredentials = $true; Headers = @{ 'X-ContentOS-Staff' = '1' } }
# PowerShell 7 explicitly gates integrated authentication on local HTTP; credentials are never accepted as script arguments.
if ($PSVersionTable.PSVersion.Major -ge 7 -and $base.Scheme -eq 'http') { $auth.AllowUnencryptedAuthentication = $true }
function Invoke-Staff([string]$Path, [object]$Body = $null) {
    $options = @{ Uri = "$baseText/api/v1/runtime/$Path"; TimeoutSec = 10 }
    if ($null -ne $Body) { $options.Method = 'Post'; $options.ContentType = 'application/json'; $options.Body = ($Body | ConvertTo-Json -Depth 6) }
    Invoke-RestMethod @options @auth
}
function Activate-Probe([string]$Adapter) {
    $provider = Invoke-Staff 'providers' @{ adapterKey = $Adapter; maxConcurrency = 1 }
    $revision = Invoke-Staff 'revisions' @{ profileId = $provider.profileId; maxAttempts = 3; budget = 3; deadlineSeconds = 60; timezone = 'Asia/Kolkata' }
    Invoke-Staff "revisions/$($revision.id)/activate" @{} | Out-Null
    return $revision.id
}
function Wait-State([string]$Id, [string]$State) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    do {
        $detail = Invoke-Staff "jobs/$Id"
        if ($detail.job.state -eq $State) { return $detail }
        if ($detail.job.state -eq 'DeadLetter') { throw "Job $Id dead-lettered: $($detail.job.failureReason)" }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Job $Id did not reach $State; start the SQL-enabled worker and inspect its redacted logs."
}
$routeA = Activate-Probe 'test.a'
# Delay the old job long enough to freeze and inspect its route before execution.
$old = Invoke-Staff 'jobs' @{ scenario = 'success'; scheduledUtc = [DateTimeOffset]::UtcNow.AddSeconds(5).ToString('O'); timezone = 'Asia/Kolkata' }
$routeB = Activate-Probe 'test.b'
$new = Invoke-Staff 'jobs' @{ scenario = 'success' }
$oldDetail = Wait-State $old.id 'Completed'
$newDetail = Wait-State $new.id 'Completed'
if ($oldDetail.job.snapshot.revisionId -ne $routeA -or $newDetail.job.snapshot.revisionId -ne $routeB) { throw 'Route snapshot switch failed.' }
$unknown = Invoke-Staff 'jobs' @{ scenario = 'unknown' }
$before = Wait-State $unknown.id 'Unknown'
Invoke-Staff "jobs/$($unknown.id)/reconcile" @{} | Out-Null
$after = Wait-State $unknown.id 'Completed'
if ($before.operation.id -ne $after.operation.id -or -not $after.attempts[-1].reconciliation) { throw 'Original binding reconciliation failed.' }
Write-Host "Route switch passed without rebuild: old job $($old.id) retained A; new job $($new.id) used B."
Write-Host "Unknown job $($unknown.id) reconciled the same operation; no blind resubmission."
