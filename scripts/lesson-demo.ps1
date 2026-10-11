param([switch]$ManageApi)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageApi){throw 'Use -ManageApi with port5100 free. This demo targets only GeetaStudio_M1Tests; use /staff/lessons for development content.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Stop existing API/debug hosts; port5100 is occupied.'}
$dir=Join-Path $repo '.local/m09-demo'
New-Item -ItemType Directory -Force -Path $dir|Out-Null
$prior=@{}
foreach($key in @('MSBUILDDISABLENODEREUSE','DOTNET_CLI_USE_MSBUILD_SERVER','GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled')){$prior[$key]=[Environment]::GetEnvironmentVariable($key)}
$owned=$null;$evidence=$null
try{
$env:MSBUILDDISABLENODEREUSE='1';$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:GEETA_REPOSITORY=$repo
$project=@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><ProjectReference Include="$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj"/><ProjectReference Include="$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/LessonTestFixture.cs" Link="LessonTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/EvidenceTestFixture.cs" Link="EvidenceTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/SourceTestFixture.cs" Link="SourceTestFixture.cs"/></ItemGroup></Project>
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
using var root=LessonTestFixture.Services();
using var scope=root.CreateScope();
var f=await LessonTestFixture.Setup(scope.ServiceProvider,WindowsIdentity.GetCurrent().Name);
var e=scope.ServiceProvider.GetRequiredService<ILessonEngine>();
var child=await e.AdaptAsync(f.Owner,f.Universe,LessonTestFixture.Request(f),default);
var youth=await e.AdaptAsync(f.Owner,f.Universe,LessonTestFixture.Request(f,"youth","writing-15-18"),default);
var guest=await e.RegisterCharacterAsync(f.Owner,f.Universe,new(Guid.NewGuid(),"fixture-peer","Fixture peer","Teen","Fixture guest retains canonical Teen life stage.","fixture:identity-suitability-only"),default);
var cast=await e.AdaptAsync(f.Owner,f.Universe,LessonTestFixture.Request(f,"youth","writing-15-18"),default);
var exportBlocked=false;try{await e.ExportAsync(f.Owner,f.Universe,child.Id,default);}catch{exportBlocked=true;}
var evidence=new{tenant=f.Owner.TenantId,universe=f.Universe,child=child.Id,youth=youth.Id,
differentExamples=child.Sections!.Example!=youth.Sections!.Example,sameSourceAnchor=child.Sections.SourceAnchor==youth.Sections.SourceAnchor,
sameInterpretation=child.Sections.Interpretation==youth.Sections.Interpretation,sameSanskrit=child.Snapshot.Bundle.Sanskrit==youth.Snapshot.Bundle.Sanskrit,
newGuestAvailable=cast.Snapshot.Cast.Any(x=>x.CharacterId==guest),canonicalLifeStagePreserved=cast.Snapshot.Cast.All(x=>x.LifeStage=="Teen"),
fixtureExportBlocked=exportBlocked,fixtureOnly=true,humanMultilingualQualityVerified=false,liveCalls=0};
if(!evidence.differentExamples||!evidence.sameSourceAnchor||!evidence.sameInterpretation||!evidence.sameSanskrit||!evidence.newGuestAvailable||!evidence.fixtureExportBlocked)throw new Exception("Fixture acceptance failed.");
await File.WriteAllTextAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M09-fixture.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
'@)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Fixture harness build failed.'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed.'}
$f=Get-Content (Join-Path $repo '.local/M09-fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true'
$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name;$env:Workflows__WorkerEnabled='false';$env:Knowledge__IndexWorkerEnabled='false'
$exe=Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe'
$owned=Start-Process -FilePath $exe -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir 'api.out') -RedirectStandardError (Join-Path $dir 'api.err')
$ready=$false
for($i=0;$i -lt 60;$i++){if($owned.HasExited){throw 'Owned API exited.'};try{$h=Invoke-WebRequest 'http://localhost:5100/health/live' -UseBasicParsing;if($h.StatusCode -eq 200){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 250}
if(-not $ready){throw 'Owned fixture API not ready.'}
$page=Invoke-WebRequest 'http://localhost:5100/staff/lessons' -UseDefaultCredentials -UseBasicParsing
$variants=Invoke-RestMethod ("http://localhost:5100/api/v1/lessons/tenants/"+$f.tenant+"/universes/"+$f.universe+"/variants") -UseDefaultCredentials
if($page.StatusCode -ne 200 -or @($variants).Count -lt 3){throw 'Windows-authenticated fixture page/variants unavailable.'}
$evidence=[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');staffPageStatus=200;windowsAuthenticatedVariants=@($variants).Count;distinctExamples=$f.differentExamples;sameAnchor=$f.sameSourceAnchor;sameInterpretation=$f.sameInterpretation;sameSanskrit=$f.sameSanskrit;newGuestAvailable=$f.newGuestAvailable;canonicalLifeStagePreserved=$f.canonicalLifeStagePreserved;fixtureExportBlocked=$f.fixtureExportBlocked;fixtureOnly=$true;humanMultilingualQualityVerified=$false;liveCalls=0}
}finally{
if($owned -and -not $owned.HasExited){Stop-Process -Id $owned.Id -ErrorAction SilentlyContinue;$owned.WaitForExit(10000)|Out-Null}
foreach($key in $prior.Keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}
}
if($evidence){$evidence.ownedApiStopped=($null -ne $owned -and $owned.HasExited);if(-not $evidence.ownedApiStopped){throw 'Owned API stop unverified.'};$evidence|ConvertTo-Json -Depth 6|Set-Content (Join-Path $repo 'docs/milestones/evidence/M09-demo.json')}
Write-Host 'M09 fixture comparison, unchanged source/Sanskrit, eligible new guest and Windows staff HTTP200 passed. Owned API stopped. Fixture export blocked; no live calls or multilingual quality qualification.'
