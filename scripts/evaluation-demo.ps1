param([switch]$ManageApi)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageApi){throw 'Use -ManageApi with port5100 free. This demo targets only GeetaStudio_M1Tests; use /staff/evaluation for development content.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Stop existing API/debug hosts; port5100 is occupied.'}
$dir=Join-Path $repo '.local/m12-demo'
New-Item -ItemType Directory -Force -Path $dir|Out-Null
$prior=@{}
foreach($key in @('MSBUILDDISABLENODEREUSE','DOTNET_CLI_USE_MSBUILD_SERVER','GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled')){$prior[$key]=[Environment]::GetEnvironmentVariable($key)}
$owned=$null;$evidence=$null;$hosts=@()
try{
$env:MSBUILDDISABLENODEREUSE='1';$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:GEETA_REPOSITORY=$repo
$project=@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><ProjectReference Include="$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj"/><ProjectReference Include="$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/EvaluationTestFixture.cs" Link="EvaluationTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/FactoryTestFixture.cs" Link="FactoryTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/RegistryTestFixture.cs" Link="RegistryTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/LessonTestFixture.cs" Link="LessonTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/EvidenceTestFixture.cs" Link="EvidenceTestFixture.cs"/><Compile Include="$repo/tests/ContentOS.Foundation.Tests/SourceTestFixture.cs" Link="SourceTestFixture.cs"/></ItemGroup></Project>
"@
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
[IO.File]::WriteAllText((Join-Path $dir 'Program.cs'),@"
using System.Text.Json;
using System.Security.Principal;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Foundation.Tests;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
using var root=EvaluationTestFixture.Services();using var scope=root.CreateScope();var s=scope.ServiceProvider;
var f=await FactoryTestFixture.Setup(s,WindowsIdentity.GetCurrent().Name);
var db=s.GetRequiredService<CoreDbContext>();var e=s.GetRequiredService<IQualityEvaluation>();
var text=await(from t in db.CorpusTexts join v in db.CorpusVerses on t.VerseId equals v.Id join m in db.CorpusManifests on v.ManifestId equals m.Id where m.UniverseId==f.Universe && t.Locale=="en-IN" && v.Chapter==1 && v.Verse==1 select t.Id).FirstAsync();
var dataset=await e.RegisterDatasetAsync(f.Owner,f.Universe,EvaluationFixtureCatalog.Create(f.Audience,text,FactoryTestFixture.Cast(f)),default);
foreach(var c in dataset.Dataset.Cases)await e.ReviewAsync(f.Owner,f.Universe,EvaluationTestFixture.Review(dataset.Id,c.Key),default);
var a=await e.RegisterCandidateAsync(f.Owner,f.Universe,EvaluationTestFixture.Candidate("baseline"),default);
var b=await e.RegisterCandidateAsync(f.Owner,f.Universe,EvaluationTestFixture.Candidate("candidate-b","evaluation.fixture.b.v1"),default);
var bad=await e.RegisterCandidateAsync(f.Owner,f.Universe,EvaluationTestFixture.Candidate("fabricated","evaluation.fabricated.v1"),default);
var unsafeCandidate=await e.RegisterCandidateAsync(f.Owner,f.Universe,EvaluationTestFixture.Candidate("unsafe","evaluation.unsafe.v1"),default);
var fluent=await e.RegisterCandidateAsync(f.Owner,f.Universe,EvaluationTestFixture.Candidate("fluent-failure","evaluation.fluent-failure.v1"),default);
var data=new{tenant=f.Owner.TenantId,universe=f.Universe,dataset=dataset.Id,baselineCandidate=a.Id,candidate=b.Id,fabricated=bad.Id,unsafeCandidate=unsafeCandidate.Id,fluent=fluent.Id};
var path=Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local");
await File.WriteAllTextAsync(Path.Combine(path,"M12-fixture.json"),JsonSerializer.Serialize(data));
await File.WriteAllTextAsync(Path.Combine(path,"M12-dataset.json"),JsonSerializer.Serialize(dataset.Dataset,new JsonSerializerOptions{WriteIndented=true}));
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
"@)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Fixture harness build failed.'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed.'}
$f=Get-Content (Join-Path $repo '.local/M12-fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true'
$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name;$env:Workflows__WorkerEnabled='false';$env:Knowledge__IndexWorkerEnabled='false';$env:Factory__WorkerEnabled='false'
$exe=Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe'
function Start-OwnedApi($number){
$process=Start-Process -FilePath $exe -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err")
$ready=$false;for($i=0;$i -lt 60;$i++){if($process.HasExited){throw 'Owned API exited.'};try{$h=Invoke-WebRequest 'http://localhost:5100/health/live' -UseBasicParsing;if($h.StatusCode -eq 200){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 250}
if(-not $ready){Stop-Process -Id $process.Id -ErrorAction SilentlyContinue;throw 'Owned API not ready.'};return $process
}
$owned=Start-OwnedApi 1;$hosts+=$owned
$path="http://localhost:5100/api/v1/evaluation/tenants/"+$f.tenant+"/universes/"+$f.universe
function Invoke-Evaluation($candidate,$split,$baseline){
$request=@{operationId=[Guid]::NewGuid().ToString();datasetId=$f.dataset;candidateId=$candidate;split=$split;baselineRunId=$baseline;deadlineSeconds=300}
return Invoke-RestMethod ($path+'/runs') -Method Post -UseDefaultCredentials -Headers @{'X-ContentOS-Staff'='1'} -ContentType 'application/json' -Body ($request|ConvertTo-Json) -TimeoutSec 300
}
$page=Invoke-WebRequest 'http://localhost:5100/staff/evaluation' -UseDefaultCredentials -UseBasicParsing
$baseline=Invoke-Evaluation $f.baselineCandidate 'Development' $null
$candidate=Invoke-Evaluation $f.candidate 'Development' $baseline.id
$bad=Invoke-Evaluation $f.fabricated 'Development' $baseline.id
$unsafe=Invoke-Evaluation $f.unsafeCandidate 'Development' $baseline.id
$fluent=Invoke-Evaluation $f.fluent 'Development' $baseline.id
$held=Invoke-Evaluation $f.candidate 'HeldOut' $baseline.id
if(@($baseline.results|Where-Object{-not $_.passed}).Count -ne 0 -or @($candidate.results|Where-Object{-not $_.passed}).Count -ne 0){throw 'Fixture baseline/candidate expectations failed.'}
foreach($r in @($bad,$unsafe,$fluent)){if($r.qualification.status -ne 'Blocked' -or $r.qualification.blocks -notcontains 'DeterministicFailures'){throw 'Injected fluent failure did not block release.'}}
if($baseline.results[0].output.body -eq $candidate.results[0].output.body){throw 'A/B wording did not change.'}
if(@($held.results).Count -ne 20 -or $held.qualification.humanAccepted -ne 0 -or $held.qualification.status -ne 'Blocked'){throw 'Human/held-out gate failed.'}
Stop-Process -Id $owned.Id;$owned.WaitForExit(10000)|Out-Null
$owned=Start-OwnedApi 2;$hosts+=$owned
$reports=Invoke-RestMethod ($path+'/reports') -UseDefaultCredentials -TimeoutSec 300
if(@($reports|Where-Object{$_.id -eq $held.id}).Count -ne 1){throw 'Report did not survive actual API restart.'}
$datasets=Invoke-RestMethod ($path+'/datasets') -UseDefaultCredentials
$d=$datasets|Where-Object{$_.id -eq $f.dataset}
$evidence=[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');windowsStaffPageStatus=$page.StatusCode;actualApiRestarted=$hosts[0].HasExited;fixtureReviewedCases=$d.fixtureReviewedCases;humanReviewedCases=$d.humanReviewedCases;developmentSample=@($baseline.results).Count;heldOutSample=@($held.results).Count;baselinePassed=@($baseline.results|Where-Object{$_.passed}).Count;candidatePassed=@($candidate.results|Where-Object{$_.passed}).Count;wordingChanged=$true;fabricatedCitationBlocked=$true;unsafeAudienceBlocked=$true;fluentCastClaimMediaFailureBlocked=$true;humanReleaseBlocked=$true;reportPreserved=$true;retrievalBaseline=$baseline.qualification.retrievalAccuracy;retrievalTarget=$held.qualification.retrievalTarget;baselineP95LatencyMs=$baseline.qualification.p95LatencyMs;candidateP95LatencyMs=$candidate.qualification.p95LatencyMs;latencyTargetMs=$held.qualification.latencyTargetMs;totalCost=0;liveCalls=0;fixtureOnly=$true}
}finally{
foreach($hostProcess in $hosts){if(-not $hostProcess.HasExited){Stop-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue;$hostProcess.WaitForExit(10000)|Out-Null}}
foreach($key in $prior.Keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}
}
if($evidence){$evidence.ownedHostsStopped=@($hosts|Where-Object{-not $_.HasExited}).Count -eq 0;if(-not $evidence.ownedHostsStopped){throw 'Owned cleanup failed'};$evidence|ConvertTo-Json -Depth 6|Set-Content (Join-Path $repo 'docs/milestones/evidence/M12-demo.json')}
Write-Host 'M12 Windows evaluation, A/B properties, injected failures, pending human acceptance and durable restart reports passed. Owned hosts stopped; no live calls.'



