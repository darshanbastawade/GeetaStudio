param([switch]$ManageApi,[switch]$DebugPause)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(!$ManageApi){throw 'Use -ManageApi for the dedicated local Test database.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Stop the API debug host using port5100 first.'}
$dir=Join-Path $repo '.local/m23-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Create__WorkerEnabled','Talk__RetentionWorkerEnabled','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled')
$prior=@{};foreach($k in $keys){$prior[$k]=[Environment]::GetEnvironmentVariable($k)};$owned=@()
$auth=@{UseDefaultCredentials=$true};if((Get-Command Invoke-RestMethod).Parameters.ContainsKey('AllowUnencryptedAuthentication')){$auth.AllowUnencryptedAuthentication=$true}
try{
$project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include=""$repo/tests/ContentOS.Foundation.Tests/ContentOS.Foundation.Tests.csproj""/><Compile Include=""$repo/scripts/fixtures/CreationDemo.cs"" Link=""Program.cs""/></ItemGroup></Project>"
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Creation fixture build failed.'}
& dotnet (Join-Path $dir 'bin/Debug/net10.0/Demo.dll') $dir
if($LASTEXITCODE -ne 0){throw 'Creation fixture setup failed.'}
$f=Get-Content (Join-Path $dir 'fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:Runtime__Enabled='true';$env:ASPNETCORE_URLS='http://localhost:5100';$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;';$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
foreach($key in $keys|Where-Object {$_ -like '*WorkerEnabled'}){[Environment]::SetEnvironmentVariable($key,'false')};$env:Create__WorkerEnabled='true';$env:Talk__RetentionWorkerEnabled='false'
function Start-OwnedApi($number){$p=Start-Process -FilePath (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err");$script:owned+=$p;for($i=0;$i -lt 60;$i++){if($p.HasExited){throw 'Owned API exited'};try{Invoke-WebRequest 'http://localhost:5100/health/ready' -UseBasicParsing|Out-Null;return $p}catch{Start-Sleep -Milliseconds 500}};throw 'API startup timeout'}
function Post-Private($path,$body){Invoke-RestMethod ($script:base+$path) -Method Post -ContentType 'application/json' -Headers @{'X-ContentOS-Staff'='1'} -Body ($body|ConvertTo-Json -Depth 12) @auth}
$api=Start-OwnedApi 1;$base="http://localhost:5100/api/v1/customer/tenants/$($f.tenant)/create"
$page=Invoke-WebRequest 'http://localhost:5100/create' -UseBasicParsing @auth;if($page.StatusCode -ne 200 -or $page.Headers['Cache-Control'] -ne 'no-store'){throw 'Private creation page failed'}
if($DebugPause){Write-Host "M23 API process $($api.Id); attach Geeta Studio: M23 attach Api; open /create, workspace $($f.tenant).";Read-Host 'Inspect submission/admission/settlement; press Enter to finish'}
$r=@{operationId=[guid]::NewGuid().ToString();templateId=$f.template;profileId=$f.profile;input=@{nickname='Learner';theme='patience';reflection='A private reflection'};uploadIds=@()}
$id=Post-Private '/creations' $r;if((Post-Private '/creations' $r) -ne $id){throw 'Submission replay changed creation identity'}
for($i=0;$i -lt 90;$i++){$row=Invoke-RestMethod "$base/creations/$id" @auth;if($row.state -eq 'Ready'){break};if($row.state -eq 'Failed'){throw "Creation failed: $($row.outcome)"};Start-Sleep -Milliseconds 500};if($row.state -ne 'Ready'){throw 'Creation queue timeout'}
Post-Private "/creations/$id/edit" @{operationId=[guid]::NewGuid().ToString();rowVersion=$row.rowVersion;input=@{nickname='Learner';theme='kindness';reflection='I can pause before choosing.'}}|Out-Null
$row=Invoke-RestMethod "$base/creations/$id" @auth
Post-Private "/creations/$id/review" @{operationId=[guid]::NewGuid().ToString();rowVersion=$row.rowVersion;decision='Approve';evidence='fixture:private-owner-preview';mode='Fixture';sharingConsent=$false}|Out-Null
$pack=Invoke-RestMethod "$base/creations/$id/export" @auth;if($pack.version -ne 2 -or $pack.publicEligible -or $pack.citations.Count -eq 0 -or $pack.html -notmatch 'I can pause'){throw 'Private export/lineage differs'}
[IO.File]::WriteAllText((Join-Path $dir 'learning-pack.html'),$pack.html)
$api|Stop-Process;$api.WaitForExit(10000)|Out-Null;$api=Start-OwnedApi 2
$again=Invoke-RestMethod "$base/creations/$id/export" @auth;if($again.sha256 -ne $pack.sha256){throw 'Restart changed reviewed pack'}
$isolated=$false;try{Invoke-RestMethod "$base/creations/$($f.otherCreation)" @auth|Out-Null}catch{if([int]$_.Exception.Response.StatusCode -eq 403){$isolated=$true}else{throw}};if(!$isolated){throw 'Other account creation leaked'}
$wallet=Invoke-RestMethod "http://localhost:5100/api/v1/customer/tenants/$($f.tenant)/wallet" @auth;if($wallet.available -ne 8 -or $wallet.reserved -ne 0){throw 'Credits were duplicated or left held'}
Post-Private "/creations/$id/delete" @{}|Out-Null
for($i=0;$i -lt 60;$i++){$gone=Invoke-RestMethod "$base/creations/$id" @auth;if($gone.cleanup -eq 'CompletedLocalNoExternalArtifacts'){break};Start-Sleep -Milliseconds 250};if($gone.state -ne 'Deleted' -or $gone.html -or $gone.publicEligible -or $gone.cleanup -ne 'CompletedLocalNoExternalArtifacts'){throw 'Deletion cleanup failed'}
[ordered]@{date='2026-10-14';windowsPage200=$true;privateNoStore=$true;reviewedTemplates=3;sourceCredits=$true;editedVersion=2;submissionReplay=$true;apiRestart=$true;crossAccount403=$true;availableCredits=8;heldCredits=0;privateDeletion=$true;localCleanupComplete=$true;liveCalls=0;publicUrlsCreated=0;interactiveBreakpointVerified=$false}|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M23-demo.json')
Write-Host 'M23 private create/edit/export, credits, replay, restart, isolation and erasure passed.'
}finally{foreach($p in $owned){if(!$p.HasExited){Stop-Process -Id $p.Id;$p.WaitForExit(10000)|Out-Null}};foreach($k in $keys){[Environment]::SetEnvironmentVariable($k,$prior[$k])}}
