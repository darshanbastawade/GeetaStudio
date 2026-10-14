param([switch]$ManageApi,[switch]$DebugPause)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(!$ManageApi){throw 'Use -ManageApi; this controlled demo uses only the dedicated Test database.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Port5100 occupied; stop debug hosts first.'}
if(!(Test-Path (Join-Path $repo '.local/m17-demo/fixture.json'))){& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts/watch-demo.ps1') -ManageApi;if($LASTEXITCODE -ne 0){throw 'The spoken WATCH prerequisite failed.'}}
$dir=Join-Path $repo '.local/m18-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled','Assets__Root','Assets__Stores__watch','Assets__Stores__archive','Watch__ToolsConfig')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key)};$owned=@()
try{
$env:GEETA_REPOSITORY=$repo
$links=Get-ChildItem (Join-Path $repo 'tests/ContentOS.Foundation.Tests/*TestFixture.cs')|ForEach-Object{"<Compile Include=""$($_.FullName)"" Link=""$($_.Name)""/>"}
$project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/><Compile Include=""$repo/scripts/fixtures/SocialDemo.cs"" Link=""Program.cs""/>$($links -join '')</ItemGroup></Project>"
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Publishing harness build failed'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Publishing package fixture failed'}
$f=Get-Content (Join-Path $dir 'fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true';$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;';$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
foreach($key in @('Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled')){[Environment]::SetEnvironmentVariable($key,'false')}
$env:Assets__Root=Join-Path $repo '.local/test-assets';$env:Assets__Stores__watch=Join-Path $repo '.local/watch-test-masters';$env:Assets__Stores__archive=Join-Path $repo '.local/m16-archive';$env:Watch__ToolsConfig=Join-Path $repo '.local/M17-tools.json'
function Start-OwnedApi($number){$p=Start-Process (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err");$script:owned+=$p;for($i=0;$i -lt 60;$i++){if($p.HasExited){throw 'Owned API exited'};try{Invoke-WebRequest 'http://localhost:5100/health/ready' -UseBasicParsing|Out-Null;return $p}catch{Start-Sleep -Milliseconds 500}};throw 'API startup timeout'}
function Post-Staff($url,$body){Invoke-RestMethod $url -Method Post -ContentType 'application/json' -Headers @{'X-ContentOS-Staff'='1'} -Body ($body|ConvertTo-Json -Depth 12) -UseDefaultCredentials}
$api=Start-OwnedApi 1;$path="http://localhost:5100/api/v1/social/tenants/$($f.tenant)/universes/$($f.universe)"
$page=Invoke-WebRequest 'http://localhost:5100/staff/social' -UseDefaultCredentials -UseBasicParsing;if($page.StatusCode -ne 200){throw 'Publishing staff page unavailable'}
foreach($package in $f.packages){$export=Invoke-RestMethod "$path/packages/$($package.Id)/export" -UseDefaultCredentials;if($export.package.payloadHash -ne $package.PayloadHash){throw 'Reviewed package hash changed'}}
$before=Invoke-RestMethod "$path/publications" -UseDefaultCredentials;$unknown=$before|Where-Object Id -eq $f.publication;if($unknown.State -ne 'Unknown'){throw 'Original timeout lost its unknown state'}
if($DebugPause){Write-Host "Publishing API process $($api.Id). Attach Geeta Studio: M18 attach Api. Open /staff/social; tenant $($f.tenant), universe $($f.universe).";Read-Host 'Inspect the packages/unknown outcome; press Enter to verify reconciliation, duplicates and restart'}
Post-Staff "$path/publications/$($f.publication)/control" @{operationId=[guid]::NewGuid();expectedRowVersion=$unknown.rowVersion;action='Reconcile';evidenceReference='fixture:original-publishing-ledger'}|Out-Null
$published=(Invoke-RestMethod "$path/publications" -UseDefaultCredentials)|Where-Object Id -eq $f.publication
if($published.State -ne 'Published' -or $published.Adapter -ne 'social.unknown.v1' -or $published.AccountReference -ne $f.accountReference){throw 'Provider switch rebound the original publication'}
$receipt=@{operationId=[guid]::NewGuid();eventId=('fixture-demo-'+[guid]::NewGuid());platform=$published.platform;accountReference=$published.accountReference;adapter=$published.adapter;payloadHash=$f.payloadHash;remoteId=$published.remoteId;remoteUrl=$published.remoteUrl;evidenceReference='fixture:authenticated-original-ledger'}
Post-Staff "$path/publications/$($f.publication)/receipt" $receipt|Out-Null;$receipt.operationId=[guid]::NewGuid();Post-Staff "$path/publications/$($f.publication)/receipt" $receipt|Out-Null
$history=(Invoke-RestMethod "$path/publications" -UseDefaultCredentials)|Where-Object Id -eq $f.publication
$api|Stop-Process;$api.WaitForExit(10000)|Out-Null;$api=Start-OwnedApi 2
$after=(Invoke-RestMethod "$path/publications" -UseDefaultCredentials)|Where-Object Id -eq $f.publication
if($after.remoteId -ne $published.remoteId -or $after.adapter -ne $published.adapter -or $after.history.Count -ne $history.history.Count){throw 'Restart/callback changed original post/history'}
$accounts=Invoke-RestMethod "$path/accounts" -UseDefaultCredentials;if($accounts|Where-Object Qualified){throw 'Fixture incorrectly qualified as live'}
[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');platforms=@('YouTube','Instagram','Facebook','X');windowsStaff200=$true;reviewedExports=$true;coreBodyPreserved=$f.coreBodyPreserved;unknownReconciled=$true;duplicateReceiptPreservedHistory=$true;apiRestart=$true;originalProviderAccountPreserved=$true;liveConnected=$false;humanApproval=$false;freshInteractiveF5Verified=$false;paidCalls=0;publicationsToSocialPlatforms=0;tenant=$f.tenant;universe=$f.universe;publication=$f.publication}|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M18-demo.json')
Write-Host "Four private reviewed exports, original unknown reconciliation, duplicate receipt and API restart passed. Outputs: $dir. Owned API hosts will stop."
}finally{foreach($p in $owned){if(!$p.HasExited){$p|Stop-Process;$p.WaitForExit(10000)|Out-Null}};foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}}
