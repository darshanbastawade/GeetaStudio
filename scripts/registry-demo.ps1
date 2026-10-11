param([switch]$ManageApi)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageApi){throw 'Use -ManageApi with port5100 free. This demo targets only GeetaStudio_M1Tests; use /staff/characters for development content.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Stop existing API/debug hosts; port5100 is occupied.'}
$dir=Join-Path $repo '.local/m10-demo'
New-Item -ItemType Directory -Force -Path $dir|Out-Null
$prior=@{}
foreach($key in @('MSBUILDDISABLENODEREUSE','DOTNET_CLI_USE_MSBUILD_SERVER','GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled')){$prior[$key]=[Environment]::GetEnvironmentVariable($key)}
$owned=$null;$evidence=$null
try{
$env:MSBUILDDISABLENODEREUSE='1';$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:GEETA_REPOSITORY=$repo
$project=@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><ProjectReference Include="$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj"/><ProjectReference Include="$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/RegistryTestFixture.cs" Link="RegistryTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/LessonTestFixture.cs" Link="LessonTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/EvidenceTestFixture.cs" Link="EvidenceTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/SourceTestFixture.cs" Link="SourceTestFixture.cs"/></ItemGroup></Project>
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
using var root=RegistryTestFixture.Services();using var scope=root.CreateScope();var s=scope.ServiceProvider;
var f=await RegistryTestFixture.Setup(s,WindowsIdentity.GetCurrent().Name);var r=s.GetRequiredService<IUniverseRegistry>();
await RegistryTestFixture.Character(s,f,"fixture-learner");
var missing=await r.CastAsync(f.Owner,f.Universe,RegistryTestFixture.Brief(f,["Lead","Mentor"]),default);
var coach=await RegistryTestFixture.Character(s,f,"unseen-coach","Mentor",active:false);
var pinned=RegistryTestFixture.Brief(f,["Lead","Mentor"]) with{PinnedChoices=[new(coach.CharacterId,"Mentor")]};
var immediate=await r.CastAsync(f.Owner,f.Universe,pinned,default);
await RegistryTestFixture.Review(s,f.Owner,f.Universe,coach.Profile.Id);
await r.ControlCharacterAsync(f.Owner,f.Universe,coach.CharacterId,new(Guid.NewGuid(),coach.RowVersion,"Activate","fixture:coach-activation"),default);
var story=await RegistryTestFixture.Story(s,f,RegistryTestFixture.Brief(f,["Lead","Mentor"]));
await RegistryTestFixture.Review(s,f.Owner,f.Universe,story.Id);
coach=(await r.CharactersAsync(f.Owner,f.Universe,default)).Single(c=>c.CharacterId==coach.CharacterId);
await r.ControlCharacterAsync(f.Owner,f.Universe,coach.CharacterId,new(Guid.NewGuid(),coach.RowVersion,"PromoteGuest","fixture:promotion"),default);
using var restart=root.CreateScope();var restarted=restart.ServiceProvider.GetRequiredService<IUniverseRegistry>();
var promoted=(await restarted.CharactersAsync(f.Owner,f.Universe,default)).Single(c=>c.CharacterId==coach.CharacterId);
var old=(await restarted.StoriesAsync(f.Owner,f.Universe,default)).Single(x=>x.Id==story.Id);
var blocked=false;try{await restarted.ExportAsync(f.Owner,f.Universe,story.Id,default);}catch(ContentOS.Domain.RuntimeValidationException){blocked=true;}
var evidence=new{tenant=f.Owner.TenantId,universe=f.Universe,guestProposed=missing.GuestProposals.Length==1,
immediatePinnedDraft=immediate.Selected.Any(c=>c.Character.CharacterId==coach.CharacterId),
sameIdentityPromoted=promoted.CharacterId==coach.CharacterId&&promoted.PersistenceScope=="Recurring",
oldSnapshotGuest=old.Rules.Cast.Single(c=>c.Character.CharacterId==coach.CharacterId).Character.PersistenceScope=="Guest",
oldProfileUnchanged=old.Rules.Cast.Single(c=>c.Character.CharacterId==coach.CharacterId).Character.Profile.Id==coach.Profile.Id,
fixtureExportBlocked=blocked,fixtureOnly=true,liveCalls=0};
if(!evidence.guestProposed||!evidence.immediatePinnedDraft||!evidence.sameIdentityPromoted||!evidence.oldSnapshotGuest||!evidence.oldProfileUnchanged||!blocked)throw new Exception("M10 fixture acceptance failed.");
await File.WriteAllTextAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M10-fixture.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
'@)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Fixture harness build failed.'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed.'}
$f=Get-Content (Join-Path $repo '.local/M10-fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true'
$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name;$env:Workflows__WorkerEnabled='false';$env:Knowledge__IndexWorkerEnabled='false'
$exe=Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe'
$owned=Start-Process -FilePath $exe -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir 'api.out') -RedirectStandardError (Join-Path $dir 'api.err')
$ready=$false
for($i=0;$i -lt 60;$i++){if($owned.HasExited){throw 'Owned API exited.'};try{$h=Invoke-WebRequest 'http://localhost:5100/health/live' -UseBasicParsing;if($h.StatusCode -eq 200){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 250}
if(-not $ready){throw 'Owned fixture API not ready.'}
$page=Invoke-WebRequest 'http://localhost:5100/staff/characters' -UseDefaultCredentials -UseBasicParsing
$stories=Invoke-RestMethod ("http://localhost:5100/api/v1/registry/tenants/"+$f.tenant+"/universes/"+$f.universe+"/stories") -UseDefaultCredentials
if($page.StatusCode -ne 200 -or @($stories).Count -lt 1){throw 'Windows-authenticated fixture page/stories unavailable.'}
$evidence=[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');staffPageStatus=200;windowsAuthenticatedStories=@($stories).Count;guestProposed=$f.guestProposed;immediatePinnedDraft=$f.immediatePinnedDraft;sameIdentityPromoted=$f.sameIdentityPromoted;oldSnapshotGuest=$f.oldSnapshotGuest;oldProfileUnchanged=$f.oldProfileUnchanged;fixtureExportBlocked=$f.fixtureExportBlocked;fixtureOnly=$true;liveCalls=0}
}finally{
if($owned -and -not $owned.HasExited){Stop-Process -Id $owned.Id -ErrorAction SilentlyContinue;$owned.WaitForExit(10000)|Out-Null}
foreach($key in $prior.Keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}
}
if($evidence){$evidence.ownedApiStopped=($null -ne $owned -and $owned.HasExited);if(-not $evidence.ownedApiStopped){throw 'Owned API stop unverified.'};$evidence|ConvertTo-Json -Depth 6|Set-Content (Join-Path $repo 'docs/milestones/evidence/M10-demo.json')}
Write-Host 'M10 guest draft, activation, same-identity promotion, historical snapshot and Windows staff HTTP200 passed. Owned API stopped; fixture export blocked; no live calls.'
