param([switch]$ManageApi,[switch]$DebugPause)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageApi){throw 'Use -ManageApi with local port5100 free.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Port5100 occupied'}
$dir=Join-Path $repo '.local/m25-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Create__WorkerEnabled','Commerce__WorkerEnabled','Talk__RetentionWorkerEnabled','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled')
$prior=@{};foreach($k in $keys){$prior[$k]=[Environment]::GetEnvironmentVariable($k)};$owned=$null
try{
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true';$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True';$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
foreach($k in $keys|Where-Object{$_ -like '*WorkerEnabled'}){[Environment]::SetEnvironmentVariable($k,'false')}
$owned=Start-Process -FilePath (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir 'api.out') -RedirectStandardError (Join-Path $dir 'api.err')
$ready=$false;for($i=0;$i -lt 60;$i++){if($owned.HasExited){throw 'Owned API exited'};try{Invoke-WebRequest 'http://localhost:5100/health/ready' -UseBasicParsing|Out-Null;$ready=$true;break}catch{Start-Sleep -Milliseconds 500}}
if(-not $ready){throw 'API startup timeout'}
if($DebugPause){Write-Host "Attach to owned API process $($owned.Id), then press Enter";Read-Host|Out-Null}
$times=@();$failures=0
for($i=0;$i -lt 20;$i++){
$timer=[Diagnostics.Stopwatch]::StartNew();$response=Invoke-WebRequest 'http://localhost:5100/api/v1/read' -UseBasicParsing;$timer.Stop();$times+=$timer.Elapsed.TotalMilliseconds
if($response.StatusCode -ne 200){$failures++}
if($response.Headers['Cache-Control'] -notmatch 'no-store' -or $response.Headers['X-Content-Type-Options'] -ne 'nosniff' -or $response.Headers['Referrer-Policy'] -ne 'no-referrer'){throw 'Required reader headers missing'}
}
$sorted=@($times|Sort-Object);$p95=$sorted[[Math]::Ceiling(.95*$sorted.Count)-1]
$denied=$false;try{Invoke-WebRequest 'http://localhost:5100/api/v1/read-admin/tenants/00000000-0000-0000-0000-000000000001/universes/00000000-0000-0000-0000-000000000001/collections/00000000-0000-0000-0000-000000000001' -UseBasicParsing|Out-Null}catch{$denied=[int]$_.Exception.Response.StatusCode -in @(401,403)}
if(-not $denied){throw 'Anonymous staff access not denied'}
@{date=(Get-Date -Format 'yyyy-MM-dd');samples=20;readerAvailability=(20-$failures)/20;p95Milliseconds=$p95;readerP95TargetMilliseconds=1000;latencyAlert=($p95 -gt 1000);availabilityAlert=($failures -gt 0);headersVerified=$true;anonymousStaffDenied=$true;loadQualification='Sequential local directory baseline only; not capacity or monthly SLO';publicRelease=$false;f5Verified=$false}|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M25-demo.json')
Write-Host "M25 local reader baseline: 20 samples, p95 $([Math]::Round($p95,2))ms; headers/access gates passed."
}finally{if($owned -and -not $owned.HasExited){Stop-Process -Id $owned.Id;$owned.WaitForExit(10000)|Out-Null};foreach($k in $keys){[Environment]::SetEnvironmentVariable($k,$prior[$k])}}
