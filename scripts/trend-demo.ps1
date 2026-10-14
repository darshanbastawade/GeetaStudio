param([switch]$ManageApi)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageApi){throw 'Use -ManageApi with port5100 free; this demo targets only GeetaStudio_M1Tests.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Port5100 occupied; stop existing debug hosts first.'}
$dir=Join-Path $repo '.local/m15-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key)}
$hosts=@();$evidence=$null
try{
$env:GEETA_REPOSITORY=$repo
$links=@('Trend','Read','Editorial','Evaluation','Factory','Registry','Lesson','Evidence','Source')|ForEach-Object{"<Compile Include=""$repo/tests/ContentOS.Foundation.Tests/$($_)TestFixture.cs"" Link=""$($_)TestFixture.cs""/>"}
$project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/>$($links -join '')</ItemGroup></Project>"
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
[IO.File]::WriteAllText((Join-Path $dir 'Program.cs'),@"
using System.Text.Json;using System.Security.Principal;using ContentOS.Application;using ContentOS.Contracts;using ContentOS.Foundation.Tests;using Microsoft.Extensions.DependencyInjection;
using var root=TrendTestFixture.Services();using var scope=root.CreateScope();var s=scope.ServiceProvider;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This demonstration requires Windows authentication.");
using var identity=WindowsIdentity.GetCurrent();
var f=await TrendTestFixture.Setup(s,identity.Name);var w=s.GetRequiredService<ITrendWorkbench>();var factory=s.GetRequiredService<IDraftFactory>();
var match=await TrendTestFixture.Match(s,f);var noMatch=await TrendTestFixture.Match(s,f,"How do I bake a pizza?");
if(match.Outcome!="Matched"||noMatch.Outcome!="NoSuitableGeetaConnection")throw new Exception("Teaching/no-match failure");
var brief=await TrendTestFixture.Package(s,f,match);if(brief.Snapshot!.Packages.Length!=2||brief.Snapshot.Packages[0].Cast.Cast[0].Character.CharacterId==brief.Snapshot.Packages[1].Cast.Cast[0].Character.CharacterId)throw new Exception("Distinct audience/cast failure");
var plan=(await factory.PlansAsync(f.Owner,f.Universe,default)).Single(x=>x.Id==brief.FactoryPlanId);
await factory.ReviewPlanAsync(f.Owner,f.Universe,plan.Id,new(Guid.NewGuid(),plan.RowVersion,true,"fixture:M15-plan-review"),default);
await w.ReviewStoryAsync(f.Owner,f.Universe,brief.Id,new(Guid.NewGuid(),brief.RowVersion,"Approve","Fixture","fixture:M15-script-review-not-public"),default);
await factory.StartAsync(f.Owner,f.Universe,new(Guid.NewGuid(),"m15-windows",plan.Id,new(0,"INR",24000,300,2)),default);await FactoryTestFixture.Drain(s);
var artifacts=(await factory.ArtifactsAsync(f.Owner,f.Universe,default)).Where(x=>x.Snapshot?.PlanId==plan.Id).ToArray();if(artifacts.Length!=2)throw new Exception("Two artifacts missing");
var packet=await TrendTestFixture.Schedule(s,f,artifacts[0]);
await File.WriteAllTextAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M15-fixture.json"),JsonSerializer.Serialize(new{tenant=f.Owner.TenantId,universe=f.Universe,cluster=f.Cluster,brief=brief.Id,item=packet.Id,twoPackages=true,distinctGuests=true,noMatch=true,scheduled=true}));
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
"@)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Demo harness build failed'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Controlled trend fixture failed'}
$f=Get-Content (Join-Path $repo '.local/M15-fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true'
$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$env:Workflows__WorkerEnabled='false';$env:Knowledge__IndexWorkerEnabled='false';$env:Factory__WorkerEnabled='false';$env:Read__RetentionWorkerEnabled='false';$env:Trends__RecheckWorkerEnabled='false'
function Start-OwnedApi($number){
$p=Start-Process -FilePath (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err")
$script:hosts+=$p;$ready=$false
for($i=0;$i -lt 60;$i++){if($p.HasExited){throw 'Owned API exited'};try{Invoke-WebRequest 'http://localhost:5100/health/ready' -UseBasicParsing|Out-Null;$ready=$true;break}catch{Start-Sleep -Milliseconds 500}}
if(-not $ready){throw 'API startup timeout'};return $p
}
function Get-Staff($url){$response=Invoke-WebRequest $url -UseDefaultCredentials -UseBasicParsing; $body=$response.Content|ConvertFrom-Json; Write-Output $body}
function Post-Staff($url,$data){Invoke-RestMethod $url -Method Post -UseDefaultCredentials -Headers @{'X-ContentOS-Staff'='1'} -ContentType 'application/json' -Body ($data|ConvertTo-Json -Depth 15)}
$api=Start-OwnedApi 1
$page=Invoke-WebRequest 'http://localhost:5100/staff/trends' -UseDefaultCredentials -UseBasicParsing
$path="http://localhost:5100/api/v1/trends/tenants/$($f.tenant)/universes/$($f.universe)"
$stories=Get-Staff "$path/stories";if(@($stories|Where-Object id -eq $f.brief).Count -ne 1){throw 'Stored story unavailable'}
$api|Stop-Process;$api.WaitForExit(10000)|Out-Null;$api=Start-OwnedApi 2
$stories=Get-Staff "$path/stories";if(@($stories|Where-Object id -eq $f.brief).Count -ne 1){throw 'Restart lost the brief'}
$clusters=Get-Staff "$path/clusters"
$clusters|ConvertTo-Json -Depth 25|Set-Content (Join-Path $dir "clusters-response.json")
$cluster=$clusters|Where-Object { $_.id -eq $f.cluster }
if(@($cluster).Count -ne 1 -or -not $cluster.rowVersion){throw "Exact cluster and current rowversion required for the retraction demo."}
Write-Host "M15 restart passed; retracting the scoped synthetic cluster."
Post-Staff "$path/clusters/$($f.cluster)/change" @{operationId=[Guid]::NewGuid();expectedRowVersion=$cluster.rowVersion;action='Withdraw';reason='Controlled synthetic source retraction';evidenceReference='fixture:M15-retraction'}|Out-Null
$tasks=Get-Staff "$path/remediations";if(@($tasks).Count -eq 0){throw 'No remediation after retraction'}
$editorial="http://localhost:5100/api/v1/editorial/tenants/$($f.tenant)/universes/$($f.universe)/items/$($f.item)"
$item=Get-Staff $editorial;if($item.state -ne 'Corrected'){throw 'Scheduled item not invalidated'}
Write-Host "M15 scheduled item is Corrected; checking the exact denied publication response."
$blocked=$false;try{Post-Staff "$editorial/actions" @{operationId=[Guid]::NewGuid();expectedRowVersion=$item.rowVersion;action='PublishDryRun';mode='Fixture';evidenceReference='fixture:retracted-publication-denied';channel='LocalPreview'}|Out-Null}catch{if([int]$_.Exception.Response.StatusCode -eq 409){$blocked=$true}else{throw}}
if(-not $blocked){throw 'Retracted story could publish'}
$evidence=@{date=(Get-Date -Format 'yyyy-MM-dd');windowsStaffPage200=($page.StatusCode -eq 200);twoAudiencePackages=$true;distinctNewGuests=$true;actualM06MatchAndNoMatch=$true;sharedM11GeneratedScripts=$true;actualM12FixtureEvaluationAndM13Schedule=$true;actualApiRestart=$true;retractionInvalidatedSchedule=$true;visibleRemediation=$true;publication409=$true;liveCalls=0;actualMediaProduced=$false;humanPublicReleasePending=$true}
}finally{
foreach($p in $hosts){if(-not $p.HasExited){Stop-Process -Id $p.Id -ErrorAction SilentlyContinue;$p.WaitForExit(10000)|Out-Null}}
foreach($key in $prior.Keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}
}
if($evidence){$evidence.ownedHostsStopped=@($hosts|Where-Object{-not $_.HasExited}).Count -eq 0;$evidence|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M15-demo.json')}

Write-Host 'M15 Windows demo passed: two audience scripts, distinct dynamic guests, teaching/no-match, schedule, restart, retraction and publication denial. Owned hosts stopped; no live calls or human release approval.'
