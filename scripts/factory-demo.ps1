param([switch]$ManageApi)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageApi){throw 'Use -ManageApi with port5100 free. This demo targets only GeetaStudio_M1Tests; use /staff/factory for development content.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Stop existing API/debug hosts; port5100 is occupied.'}
$dir=Join-Path $repo '.local/m11-demo'
New-Item -ItemType Directory -Force -Path $dir|Out-Null
$prior=@{}
foreach($key in @('MSBUILDDISABLENODEREUSE','DOTNET_CLI_USE_MSBUILD_SERVER','GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled')){$prior[$key]=[Environment]::GetEnvironmentVariable($key)}
$owned=$null;$evidence=$null;$hosts=@()
try{
$env:MSBUILDDISABLENODEREUSE='1';$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:GEETA_REPOSITORY=$repo
$project=@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><ProjectReference Include="$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj"/><ProjectReference Include="$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/FactoryTestFixture.cs" Link="FactoryTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/RegistryTestFixture.cs" Link="RegistryTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/LessonTestFixture.cs" Link="LessonTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/EvidenceTestFixture.cs" Link="EvidenceTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/SourceTestFixture.cs" Link="SourceTestFixture.cs"/></ItemGroup></Project>
"@
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
[IO.File]::WriteAllText((Join-Path $dir 'Program.cs'),@'
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Foundation.Tests;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Principal;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
using var root=FactoryTestFixture.Services();using var scope=root.CreateScope();var s=scope.ServiceProvider;
var f=await FactoryTestFixture.Setup(s,WindowsIdentity.GetCurrent().Name);var factory=s.GetRequiredService<IDraftFactory>();
var pilots=await factory.ProposePilotsAsync(f.Owner,f.Universe,new(Guid.NewGuid(),"visible-pilot",[f.Lesson,f.SecondLesson],FactoryTestFixture.Cast(f),[]),default);
foreach(var p in pilots)await factory.ReviewPlanAsync(f.Owner,f.Universe,p.Id,new(Guid.NewGuid(),p.RowVersion,true,"fixture:pilot-reviewed-input-behavior-not-human-quality"),default);
var plan=(await factory.PlansAsync(f.Owner,f.Universe,default)).Single(p=>p.Snapshot!.Rules.JourneyKind=="Chronological");
var request=FactoryTestFixture.Start(plan,"visible-business-request");var batch=await factory.StartAsync(f.Owner,f.Universe,request,default);
await factory.ProcessNextAsync(default);batch=(await factory.BatchesAsync(f.Owner,f.Universe,default)).Single(b=>b.Id==batch.Id);
var first=batch.Work.Single(w=>w.State=="Generated").ArtifactId;
await factory.ControlAsync(f.Owner,f.Universe,batch.Id,new(Guid.NewGuid(),batch.RowVersion,"Pause"),default);
var evidence=new{tenant=f.Owner.TenantId,universe=f.Universe,batch=batch.Id,plan=plan.Id,firstArtifact=first,pilotPlans=pilots.Count,fixtureOnly=true,liveCalls=0};
await File.WriteAllTextAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M11-fixture.json"),JsonSerializer.Serialize(evidence));
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
'@)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Fixture harness build failed.'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed.'}
$f=Get-Content (Join-Path $repo '.local/M11-fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true'
$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name;$env:Workflows__WorkerEnabled='false';$env:Knowledge__IndexWorkerEnabled='false';$env:Factory__WorkerEnabled='true'
$exe=Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe'
function Start-OwnedApi($number){
$process=Start-Process -FilePath $exe -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err")
$ready=$false;for($i=0;$i -lt 60;$i++){if($process.HasExited){throw 'Owned API exited.'};try{$h=Invoke-WebRequest 'http://localhost:5100/health/live' -UseBasicParsing;if($h.StatusCode -eq 200){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 250}
if(-not $ready){Stop-Process -Id $process.Id -ErrorAction SilentlyContinue;throw 'Owned API not ready.'};return $process
}
$owned=Start-OwnedApi 1;$hosts+=$owned
$path="http://localhost:5100/api/v1/factory/tenants/"+$f.tenant+"/universes/"+$f.universe
$before=Invoke-RestMethod ($path+'/batches') -UseDefaultCredentials
$paused=$before|Where-Object{$_.id -eq $f.batch};if($paused.state -ne 'Paused'){throw 'Checkpoint not paused'}
Stop-Process -Id $owned.Id;$owned.WaitForExit(10000)|Out-Null
$owned=Start-OwnedApi 2;$hosts+=$owned
$body=@{operationId=[Guid]::NewGuid().ToString();expectedRowVersion=$paused.rowVersion;action='Resume'}|ConvertTo-Json
Invoke-RestMethod ($path+'/batches/'+$f.batch+'/control') -Method Post -UseDefaultCredentials -Headers @{'X-ContentOS-Staff'='1'} -ContentType 'application/json' -Body $body|Out-Null
$finished=$null
for($i=0;$i -lt 240;$i++){
$rows=Invoke-RestMethod ($path+'/batches') -UseDefaultCredentials;$current=$rows|Where-Object{$_.id -eq $f.batch}
if($current.state -in @('Completed','CompletedWithIssues','Failed')){$finished=$current;break}
Start-Sleep -Milliseconds 500
}
if(-not $finished -or $finished.state -ne 'Completed'){throw 'Resumed batch did not complete; inspect owned API logs and work outcomes.'}
$page=Invoke-WebRequest 'http://localhost:5100/staff/factory' -UseDefaultCredentials -UseBasicParsing
$artifacts=Invoke-RestMethod ($path+'/artifacts') -UseDefaultCredentials
$coverage=Invoke-RestMethod ($path+'/coverage') -UseDefaultCredentials
$replayBody=@{operationId=[Guid]::NewGuid().ToString();businessKey=$finished.businessKey;planId=$finished.planId;budget=$finished.budget}|ConvertTo-Json -Depth 6
$replayed=Invoke-RestMethod ($path+'/batches') -Method Post -UseDefaultCredentials -Headers @{'X-ContentOS-Staff'='1'} -ContentType 'application/json' -Body $replayBody
$unchanged=@($artifacts|Where-Object{$_.id -eq $f.firstArtifact}).Count -eq 1
if(@($artifacts).Count -ne 12 -or -not $unchanged -or $replayed.id -ne $f.batch){throw 'Lineage/history/replay check failed'}
$generated=($coverage|Measure-Object generated -Sum).Sum;$reviewed=($coverage|Measure-Object reviewed -Sum).Sum;$published=($coverage|Measure-Object published -Sum).Sum;$usable=($coverage|Measure-Object publiclyUsable -Sum).Sum
if($generated -ne 12 -or $reviewed -ne 0 -or $published -ne 0 -or $usable -ne 0){throw 'Truthful coverage gate failed'}
$evidence=[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');windowsStaffPageStatus=$page.StatusCode;actualApiWorkerRestarted=$hosts[0].HasExited;approvedFixturePilotPlans=$f.pilotPlans;approvedFixtureVerseInputs=2;generatedDrafts=@($artifacts).Count;firstAcceptedArtifactPreserved=$unchanged;businessReplayStable=($replayed.id -eq $f.batch);generationCost=$finished.generationCost;generatedCoverage=$generated;reviewedCoverage=$reviewed;publishedCoverage=$published;publiclyUsableCoverage=$usable;fixtureOnly=$true;humanQualityVerified=$false;liveCalls=0}
}finally{
foreach($hostProcess in $hosts){if(-not $hostProcess.HasExited){Stop-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue;$hostProcess.WaitForExit(10000)|Out-Null}}
foreach($key in $prior.Keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}
}
if($evidence){$evidence.ownedHostsStopped=@($hosts|Where-Object{-not $_.HasExited}).Count -eq 0;if(-not $evidence.ownedHostsStopped){throw 'Owned cleanup failed'};$evidence|ConvertTo-Json -Depth 6|Set-Content (Join-Path $repo 'docs/milestones/evidence/M11-demo.json')}
Write-Host 'M11 two-verse/12-draft pilot, actual API worker restart, checkpoint resume, unchanged accepted output, business replay and truthful Windows staff coverage passed. Owned hosts stopped; no live calls or publication.'