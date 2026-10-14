param([switch]$ManageApi,[switch]$DebugPause)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(!$ManageApi){throw 'Use -ManageApi; this demo uses only the dedicated Test database.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Port5100 occupied; stop debug hosts first.'}
$dir=Join-Path $repo '.local/m19-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key)};$owned=@()
try{
$env:GEETA_REPOSITORY=$repo
$links=Get-ChildItem (Join-Path $repo 'tests/ContentOS.Foundation.Tests/*TestFixture.cs')|ForEach-Object{"<Compile Include=""$($_.FullName)"" Link=""$($_.Name)""/>"}
$project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/><Compile Include=""$repo/scripts/fixtures/AnalyticsDemo.cs"" Link=""Program.cs""/>$($links -join '')</ItemGroup></Project>"
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Analytics harness build failed'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Analytics fixture failed'}
$f=Get-Content (Join-Path $dir 'fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true';$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;';$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
foreach($key in @('Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled')){[Environment]::SetEnvironmentVariable($key,'false')}
function Start-OwnedApi($number){$p=Start-Process (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err");$script:owned+=$p;for($i=0;$i -lt 60;$i++){if($p.HasExited){throw 'Owned API exited'};try{Invoke-WebRequest 'http://localhost:5100/health/ready' -UseBasicParsing|Out-Null;return $p}catch{Start-Sleep -Milliseconds 500}};throw 'API startup timeout'}
function Post-Staff($url,$body){Invoke-RestMethod $url -Method Post -ContentType 'application/json' -Headers @{'X-ContentOS-Staff'='1'} -Body ($body|ConvertTo-Json -Depth 14) -UseDefaultCredentials}
$api=Start-OwnedApi 1;$path="http://localhost:5100/api/v1/analytics/tenants/$($f.tenant)/universes/$($f.universe)"
$query="?currency=INR&startsUtc=$([Uri]::EscapeDataString($f.startsUtc))&endsUtc=$([Uri]::EscapeDataString($f.endsUtc))"
$page=Invoke-WebRequest 'http://localhost:5100/staff/analytics' -UseDefaultCredentials -UseBasicParsing;if($page.StatusCode -ne 200 -or $page.Headers['Cache-Control'] -ne 'no-store'){throw 'Private analytics page unavailable'}
$before=Invoke-RestMethod "$path/report$query" -UseDefaultCredentials
if($DebugPause){Write-Host "Analytics API process $($api.Id). Attach Geeta Studio: M19 attach Api. Open /staff/analytics; tenant $($f.tenant), universe $($f.universe).";Read-Host 'Inspect report/forecast assumptions; press Enter to verify duplicate imports and restart'}
$reimport=Get-Content (Join-Path $dir 'reimport.json') -Raw|ConvertFrom-Json
$duplicate=Post-Staff "$path/imports" $reimport;if($duplicate.inserted -ne 0 -or $duplicate.duplicates -ne 5 -or $duplicate.adapter -ne 'analytics.fixture.b.v1'){throw 'Provider replacement import duplicated finance'}
$after=Invoke-RestMethod "$path/report$query" -UseDefaultCredentials
foreach($field in @('recognizedNet','costs','settled','estimatedNet','unsettledDifference')){if($before.$field -ne $after.$field){throw "Reimport changed $field"}}
if($after.recognizedNet -ne 1540 -or $after.costs -ne 120 -or $after.settled -ne 820){throw 'Known financial total differs'}
if(-not($after.groups|Where-Object {$_.dimension -eq 'RevenueStream' -and $_.key -eq 'GeetaPlusSubscription' -and $_.recognizedNet -eq 820})){throw 'Revenue stream missing'}
if(-not($after.groups|Where-Object {$_.dimension -eq 'AcquisitionChannel' -and $_.key -eq 'Instagram' -and $_.recognizedNet -eq 820})){throw 'Acquisition blended into revenue'}
if(-not($after.groups|Where-Object {$_.dimension -eq 'AcquisitionChannel' -and $_.key -eq 'Unknown'})){throw 'Unknown attribution hidden'}
$entries=Invoke-RestMethod "$path/entries" -UseDefaultCredentials
$original=$entries|Where-Object id -eq $f.originalEntry;if($original.sourceId -ne $f.originalSource){throw 'Provider replacement rewrote the historical source'}
$forecasts=Invoke-RestMethod "$path/forecasts" -UseDefaultCredentials;if($forecasts.Count -ne 2){throw 'Forecast history missing'}
$api|Stop-Process;$api.WaitForExit(10000)|Out-Null;$api=Start-OwnedApi 2
$restart=Invoke-RestMethod "$path/report$query" -UseDefaultCredentials
if($restart.recognizedNet -ne $after.recognizedNet -or $restart.metrics[0].value -ne 120){throw 'Restart changed recorded finance/metric snapshots'}
$reimport.operationId=[guid]::NewGuid();$again=Post-Staff "$path/imports" $reimport;if($again.inserted -ne 0){throw 'Restart duplicate admission failed'}
[ordered]@{verifiedDate='2026-10-13';windowsStaff200=$true;privateNoStore=$true;knownRecognizedInr=1540;costInr=120;settledInr=820;estimatedInr=200;duplicateReimport=$true;providerSwitchKeepsHistoricalSource=$true;metricSnapshotNotSummed=$true;forecastVersions=2;apiRestart=$true;unknownAttributionVisible=$true;liveConnected=$false;freshInteractiveF5Verified=$false;humanConsentVerified=$false;paidCalls=0;publicationsToSocialPlatforms=0;tenant=$f.tenant;universe=$f.universe}|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M19-demo.json')
Write-Host "M19 private Windows report, exact finance reimport, provider history, forecasts and API restart passed. Outputs: $dir."
}finally{foreach($p in $owned){if(!$p.HasExited){$p|Stop-Process;$p.WaitForExit(10000)|Out-Null}};foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}}
