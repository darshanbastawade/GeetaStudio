param([switch]$ManageApi,[switch]$DebugPause)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(!$ManageApi){throw 'Use -ManageApi; this controlled demo uses only GeetaStudio_M1Tests.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Port5100 occupied; stop your existing debug hosts first.'}
$dir=Join-Path $repo '.local/m17-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Assets__Root','Assets__Stores__watch','Assets__Stores__archive','Watch__ToolsConfig')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key)};$owned=@()
try{
$env:GEETA_REPOSITORY=$repo
$links=Get-ChildItem (Join-Path $repo 'tests/ContentOS.Foundation.Tests/*TestFixture.cs')|ForEach-Object{"<Compile Include=""$($_.FullName)"" Link=""$($_.Name)""/>"}
$project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/><Compile Include=""$repo/scripts/fixtures/WatchDemo.cs"" Link=""Program.cs""/>$($links -join '')</ItemGroup></Project>"
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'WATCH harness build failed'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'WATCH spoken fixture failed'}
$f=Get-Content (Join-Path $dir 'fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true';$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;';$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
foreach($key in @('Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled')){[Environment]::SetEnvironmentVariable($key,'false')}
$env:Assets__Root=Join-Path $repo '.local/test-assets';$env:Assets__Stores__watch=Join-Path $repo '.local/watch-test-masters';$env:Assets__Stores__archive=Join-Path $repo '.local/m16-archive';$env:Watch__ToolsConfig=Join-Path $repo '.local/M17-tools.json'
function Start-OwnedApi($number){$p=Start-Process (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err");$script:owned+=$p;for($i=0;$i -lt 60;$i++){if($p.HasExited){throw 'Owned API exited'};try{Invoke-WebRequest 'http://localhost:5100/health/ready' -UseBasicParsing|Out-Null;return $p}catch{Start-Sleep -Milliseconds 500}};throw 'API startup timeout'}
$api=Start-OwnedApi 1;$path="http://localhost:5100/api/v1/watch/tenants/$($f.tenant)/universes/$($f.universe)"
$page=Invoke-WebRequest 'http://localhost:5100/staff/watch' -UseDefaultCredentials -UseBasicParsing
if($page.StatusCode -ne 200){throw 'WATCH staff player unavailable'}
if($DebugPause){Write-Host "WATCH API process $($api.Id). Attach Geeta Studio: M17 attach Api; open http://localhost:5100/staff/watch and use tenant $($f.tenant), universe $($f.universe).";Read-Host 'Inspect/play the episode; press Enter to verify restart and stop this owned host'}
$before=Invoke-RestMethod "$path/productions/$($f.production)/export" -UseDefaultCredentials
$api|Stop-Process;$api.WaitForExit(10000)|Out-Null;$api=Start-OwnedApi 2
$after=Invoke-RestMethod "$path/productions/$($f.production)/export" -UseDefaultCredentials
if($after.production.timeline.scriptSha256 -ne $before.production.timeline.scriptSha256){throw 'Restart changed pinned script'}
foreach($artifact in $f.artifacts){$file=Join-Path $dir ("http-"+$artifact.Aspect+'.mp4');Invoke-WebRequest "$path/artifacts/$($artifact.Id)/preview" -UseDefaultCredentials -UseBasicParsing -OutFile $file;if((Get-FileHash $file).Hash -ne $artifact.Sha256){throw 'Restart master checksum mismatch'}}
$captions=Invoke-WebRequest "$path/productions/$($f.production)/captions.vtt" -UseDefaultCredentials -UseBasicParsing
if($captions.Content -notmatch 'WEBVTT'){throw 'Caption track unavailable'}
[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');windowsStaff200=$true;apiRestart=$true;masterChecksumsPreserved=$true;captionTrack=$true;spokenWindowsSeconds=$f.spokenSeconds;fixtureApprovalOnly=$true;humanVisualListeningAcceptance=$false;interactiveDebugVerified=$false;paidCalls=0;publications=0;tenant=$f.tenant;universe=$f.universe;production=$f.production}|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M17-demo.json')
Write-Host "WATCH spoken masters, captions, manifest: $dir. Owned demo hosts will stop. Human quality and fresh interactive F5 remain pending."
}finally{foreach($p in $owned){if(!$p.HasExited){$p|Stop-Process;$p.WaitForExit(10000)|Out-Null}};foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}}
