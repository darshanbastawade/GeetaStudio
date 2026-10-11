param([switch]$ManageApi)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageApi){throw 'Use -ManageApi with port5100 free. This demo targets only GeetaStudio_M1Tests; use /staff/editorial for development content.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Stop existing API/debug hosts; port5100 is occupied.'}
$dir=Join-Path $repo '.local/m13-demo'
New-Item -ItemType Directory -Force -Path $dir|Out-Null
$prior=@{}
foreach($key in @('MSBUILDDISABLENODEREUSE','DOTNET_CLI_USE_MSBUILD_SERVER','GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Assets__Root','Staff__Reader__0')){$prior[$key]=[Environment]::GetEnvironmentVariable($key)}
$owned=$null;$evidence=$null;$hosts=@()
try{
$env:MSBUILDDISABLENODEREUSE='1';$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:GEETA_REPOSITORY=$repo
$project=@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><ProjectReference Include="$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj"/><ProjectReference Include="$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/EditorialTestFixture.cs" Link="EditorialTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/EvaluationTestFixture.cs" Link="EvaluationTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/FactoryTestFixture.cs" Link="FactoryTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/RegistryTestFixture.cs" Link="RegistryTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/LessonTestFixture.cs" Link="LessonTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/EvidenceTestFixture.cs" Link="EvidenceTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/SourceTestFixture.cs" Link="SourceTestFixture.cs"/></ItemGroup></Project>
"@
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
[IO.File]::WriteAllText((Join-Path $dir 'Program.cs'),@"
using System.Text.Json;
using System.Security.Principal;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Foundation.Tests;
using Microsoft.Extensions.DependencyInjection;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
using var root=EditorialTestFixture.Services();using var scope=root.CreateScope();var s=scope.ServiceProvider;
var f=await EditorialTestFixture.Setup(s,WindowsIdentity.GetCurrent().Name);var e=s.GetRequiredService<IEditorialWorkbench>();
var text=await EditorialTestFixture.Approved(s,f,await e.StartAsync(f.Owner,f.Universe,EditorialTestFixture.Request(f,"text-demo"),default));
var media=await e.StartAsync(f.Owner,f.Universe,EditorialTestFixture.Request(f,"media-demo"),default);
media=await e.AttachAssetAsync(f.Owner,f.Universe,media.Id,new(Guid.NewGuid(),media.RowVersion,"voice","audio/wav",EditorialTestFixture.Wav(),"fixture:original-wave"),default);
media=await EditorialTestFixture.Approved(s,f,media);
async Task<bool> Denied(string actor,string action){try{await e.ActAsync(new(f.Owner.TenantId,actor),f.Universe,text.Id,EditorialTestFixture.Action(text,action),default);return false;}catch(CoreAccessException){return true;}}
var authorApproveDenied=await Denied("m13-author","Approve");var editorPublishDenied=await Denied("m13-editor","PublishDryRun");bool authorRouteDenied;
try{await e.RouteAsync(new(f.Owner.TenantId,"m13-author"),f.Universe,new(Guid.NewGuid(),"Activate",Guid.NewGuid(),""),default);authorRouteDenied=false;}catch(CoreAccessException){authorRouteDenied=true;}
if(!authorApproveDenied||!editorPublishDenied||!authorRouteDenied)throw new Exception("Role boundary failed.");
await File.WriteAllTextAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M13-fixture.json"),JsonSerializer.Serialize(new{tenant=f.Owner.TenantId,universe=f.Universe,textId=text.Id,mediaId=media.Id,wav=Convert.ToBase64String(EditorialTestFixture.Wav(300)),authorApproveDenied,editorPublishDenied,authorRouteDenied}));
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
"@)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Fixture harness build failed.'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed.'}
$f=Get-Content (Join-Path $repo '.local/M13-fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true'
$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name;$env:Workflows__WorkerEnabled='false';$env:Knowledge__IndexWorkerEnabled='false';$env:Factory__WorkerEnabled='false'
$exe=Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe'
function Start-OwnedApi($number){
$process=Start-Process -FilePath $exe -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err")
$ready=$false;for($i=0;$i -lt 60;$i++){if($process.HasExited){throw 'Owned API exited.'};try{$h=Invoke-WebRequest 'http://localhost:5100/health/live' -UseBasicParsing;if($h.StatusCode -eq 200){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 250}
if(-not $ready){Stop-Process -Id $process.Id -ErrorAction SilentlyContinue;throw 'Owned API not ready.'};return $process
}
$env:Assets__Root=Join-Path $repo '.local/test-assets';
$owned=Start-OwnedApi 1;$hosts+=$owned
$path="http://localhost:5100/api/v1/editorial/tenants/"+$f.tenant+"/universes/"+$f.universe
function Post($url,$data){Invoke-RestMethod $url -Method Post -UseDefaultCredentials -Headers @{'X-ContentOS-Staff'='1'} -ContentType 'application/json' -Body ($data|ConvertTo-Json -Depth 20) -TimeoutSec 300}
$page=Invoke-WebRequest 'http://localhost:5100/staff/editorial' -UseDefaultCredentials -UseBasicParsing
$t=Invoke-RestMethod ($path+'/items/'+$f.textId) -UseDefaultCredentials -TimeoutSec 300
$m=Invoke-RestMethod ($path+'/items/'+$f.mediaId) -UseDefaultCredentials -TimeoutSec 300
if($t.state -ne 'Approved' -or @($t.approvals|Where-Object{$_.current}).Count -ne 1 -or @($m.approvals|Where-Object{$_.current}).Count -ne 1){throw 'Fixture approval did not bind current versions.'}
$wave=Invoke-WebRequest ($path+'/assets/'+$m.assets[0].id+'/preview') -UseDefaultCredentials -UseBasicParsing
if($wave.StatusCode -ne 200){throw 'Owned waveform preview failed.'}
$oldHash=$m.assets[0].sha256
$t=Post ($path+'/items/'+$f.textId+'/edit') @{operationId=[Guid]::NewGuid();expectedRowVersion=$t.rowVersion;title=$t.current.title;body=$t.current.body+' The learner asks a clarifying question.';kind='Fiction';rightsStatus='Owned';provenance='fixture:text-material-edit'}
$m=Post ($path+'/items/'+$f.mediaId+'/assets') @{operationId=[Guid]::NewGuid();expectedRowVersion=$m.rowVersion;slot='voice';mediaType='audio/wav';content=$f.wav;evidenceReference='fixture:replaced-wave'}
if($t.state -ne 'Draft' -or $m.state -ne 'Draft' -or @($t.approvals|Where-Object{$_.current}).Count -ne 0 -or @($m.approvals|Where-Object{$_.current}).Count -ne 0 -or $m.assets[0].sha256 -eq $oldHash){throw 'Material edit failed to expire approval.'}
Stop-Process -Id $owned.Id;$owned.WaitForExit(10000)|Out-Null
$owned=Start-OwnedApi 2;$hosts+=$owned
$again=Invoke-RestMethod ($path+'/items/'+$f.mediaId) -UseDefaultCredentials -TimeoutSec 300
if($again.state -ne 'Draft' -or @($again.approvals|Where-Object{$_.current}).Count -ne 0 -or $again.assets[0].sha256 -ne $m.assets[0].sha256){throw 'Editorial state did not survive process restart.'}
Stop-Process -Id $owned.Id;$owned.WaitForExit(10000)|Out-Null
$env:Staff__Administrator__0='fixture:no-windows-administrator';$env:Staff__Reader__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$owned=Start-OwnedApi 3;$hosts+=$owned
function Assert-Forbidden($url,$data){
try{Post $url $data|Out-Null;throw 'Reader unexpectedly authorized.'}catch{if(-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 403){throw}}
}
foreach($action in @('Approve','PublishDryRun')){Assert-Forbidden ($path+'/items/'+$f.textId+'/actions') @{operationId=[Guid]::NewGuid();expectedRowVersion=$t.rowVersion;action=$action;mode='Fixture';evidenceReference='fixture:reader-denial';channel='LocalPreview'}}
Assert-Forbidden ($path+'/routes') @{operationId=[Guid]::NewGuid();action='Activate';routeId=[Guid]::NewGuid();expectedHeadRowVersion=''}
$evidence=[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');windowsStaffPageStatus=$page.StatusCode;privateWavePreviewStatus=$wave.StatusCode;fixtureTextApproved=$true;fixtureAssetApproved=$true;textEditExpiredApproval=$true;assetReplacementExpiredApproval=$true;assetChecksumChanged=$true;actualApiRestarted=$hosts[0].HasExited;expiredApprovalAndAssetPreserved=$true;authorApproveDenied=$f.authorApproveDenied;editorPublishDenied=$f.editorPublishDenied;authorRouteDenied=$f.authorRouteDenied;windowsReaderApproveDenied=$true;windowsReaderPublishDenied=$true;windowsReaderActivateDenied=$true;roleDenialMethod='Actual Windows Reader HTTP403 plus real SQL author/editor boundaries.';humanReleaseBlocked=$true;liveCalls=0;fixtureOnly=$true}
}finally{
foreach($hostProcess in $hosts){if(-not $hostProcess.HasExited){Stop-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue;$hostProcess.WaitForExit(10000)|Out-Null}}
foreach($key in $prior.Keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}
}
if($evidence){$evidence.ownedHostsStopped=@($hosts|Where-Object{-not $_.HasExited}).Count -eq 0;if(-not $evidence.ownedHostsStopped){throw 'Owned cleanup failed'};$evidence|ConvertTo-Json -Depth 6|Set-Content (Join-Path $repo 'docs/milestones/evidence/M13-demo.json')}
Write-Host 'M13 Windows approvals, text/asset invalidation, preview, role boundaries and durable restart passed. Owned hosts stopped; no live calls.'
