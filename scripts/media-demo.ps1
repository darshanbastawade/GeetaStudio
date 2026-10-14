param([switch]$ManageApi,[switch]$DebugPause)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageApi){throw 'Use -ManageApi with port5100 free; this demo targets only GeetaStudio_M1Tests.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Port5100 occupied; stop your existing debug hosts first.'}
$dir=Join-Path $repo '.local/m16-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Assets__Root','Assets__Stores__archive','Media__ProviderSpool')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key)}
$hosts=@()
try{
$env:GEETA_REPOSITORY=$repo
$links=@('Media','Registry','Lesson','Evidence','Source')|ForEach-Object{"<Compile Include=""$repo/tests/ContentOS.Foundation.Tests/$($_)TestFixture.cs"" Link=""$($_)TestFixture.cs""/>"}
$project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/>$($links -join '')</ItemGroup></Project>"
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
[IO.File]::WriteAllText((Join-Path $dir 'Program.cs'),@"
using System.Text.Json;using System.Security.Principal;using ContentOS.Application;using ContentOS.Contracts;using ContentOS.Domain;using ContentOS.Infrastructure;using ContentOS.Integrations;using ContentOS.Foundation.Tests;using Microsoft.Extensions.DependencyInjection;using Microsoft.EntityFrameworkCore;
using var root=MediaTestFixture.Services();using var scope=root.CreateScope();var s=scope.ServiceProvider;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Windows authentication and installed SAPI required.");
using var identity=WindowsIdentity.GetCurrent();var f=await RegistryTestFixture.Setup(s,identity.Name);var db=s.GetRequiredService<CoreDbContext>();
if(!await db.Locales.AnyAsync(x=>x.Id=="en-US")){db.Locales.Add(new(){Id="en-US",Name="US English"});await db.SaveChangesAsync();}
var registry=s.GetRequiredService<IUniverseRegistry>();var library=s.GetRequiredService<IMediaLibrary>();
var c=await registry.RegisterCharacterAsync(f.Owner,f.Universe,new(Guid.NewGuid(),"m16-mira","EpisodeOnly",RegistryTestFixture.Rules(f,"Mira","Coach","Adult") with{Locales=["en-US"],EpisodeId=f.Episode,PronunciationGuide="Mira = Mee rah; human pronunciation review pending"}),default);
await RegistryTestFixture.Review(s,f.Owner,f.Universe,c.Profile.Id);await registry.ControlCharacterAsync(f.Owner,f.Universe,c.CharacterId,new(Guid.NewGuid(),c.RowVersion,"Activate","fixture:M16-original-fiction"),default);c=(await registry.CharactersAsync(f.Owner,f.Universe,default)).Single(x=>x.CharacterId==c.CharacterId);
var imageProfile=await MediaTestFixture.Profile(s,f,locale:"en-US");var voiceToken=WindowsSpeechProvider.AvailableEnglishVoices().FirstOrDefault()??throw new Exception("Install an English Windows SAPI voice before this demo.");
var voiceProfile=await MediaTestFixture.Profile(s,f,"media.speech.windows-sapi.v1","Speech","en-US",voiceToken);
var output=MediaTestFixture.Upload(f,c,"mira-sheet",[]) with{ExpectedSha256=""};
var imageRun=await library.GenerateAsync(f.Owner,f.Universe,new(Guid.NewGuid(),imageProfile,output,"Controlled abstract front and side reference card for original fictional Mira.",[],[],0),default);
var voiceRun=await library.GenerateAsync(f.Owner,f.Universe,new(Guid.NewGuid(),voiceProfile,MediaTestFixture.Upload(f,c,"mira-voice",[],"Voice") with{ExpectedSha256=""},"Mira pauses before choosing her next action.",[],[new("Mira","Mee rah")],0),default);
await MediaTestFixture.Drain(s);
var runs=await library.GenerationsAsync(f.Owner,f.Universe,default);imageRun=runs.Single(x=>x.Id==imageRun.Id);voiceRun=runs.Single(x=>x.Id==voiceRun.Id);
if(imageRun.State!="Completed"||voiceRun.State!="Completed")throw new Exception("Owned image/speech generation not completed: "+voiceRun.Outcome);
var versions=(await library.AssetsAsync(f.Owner,f.Universe,default)).SelectMany(x=>x.Versions).ToArray();var image=versions.Single(x=>x.Id==imageRun.AcceptedVersionId);var voice=versions.Single(x=>x.Id==voiceRun.AcceptedVersionId);
await MediaTestFixture.Review(s,f,image);await MediaTestFixture.Review(s,f,voice);
var vp=(await library.ProfilesAsync(f.Owner,f.Universe,default)).Single(x=>x.Id==voiceProfile);
var package=await MediaTestFixture.Package(s,f,c,image,voice,voiceKey:vp.Configuration.Key,voiceResource:vp.Configuration.ResourceKey);var binding=await library.BindPackageAsync(f.Owner,f.Universe,package.Id,Guid.NewGuid(),default);await RegistryTestFixture.Review(s,f.Owner,f.Universe,binding);
var brief=RegistryTestFixture.Brief(f,["Coach"]) with{Locale="en-US",Mode="Production",Budget=new(2,0,"INR",true),PinnedChoices=[new(c.CharacterId,"Coach",binding)]};
if((await registry.CastAsync(f.Owner,f.Universe,brief,default)).Selected.Single().Character.CharacterId!=c.CharacterId)throw new Exception("Guest media casting failed");
await registry.ControlCharacterAsync(f.Owner,f.Universe,c.CharacterId,new(Guid.NewGuid(),c.RowVersion,"PromoteGuest","fixture:M16-recurring-original-identity"),default);
if((await registry.CastAsync(f.Owner,f.Universe,brief,default)).Selected.Single().Character.CharacterId!=c.CharacterId)throw new Exception("Promotion lost media identity");
var exported=await library.ExportAsync(f.Owner,f.Universe,image.Id,"Reuse",default);var audio=await library.ExportAsync(f.Owner,f.Universe,voice.Id,"Reuse",default);
if(audio.Version.Info.DurationSeconds is not >0)throw new Exception("Actual spoken WAV missing duration");
var b=await MediaTestFixture.Profile(s,f,"media.image.basic.v1",locale:"en-US");var unsupported=false;
try{await library.GenerateAsync(f.Owner,f.Universe,new(Guid.NewGuid(),b,output with{Key="unsupported-reference"},"Preserve Mira",[image.Id],[],0),default);}catch(EvidenceBlockedException){unsupported=true;}
if(!unsupported)throw new Exception("Reference silently discarded");
var copied=await library.GenerateAsync(f.Owner,f.Universe,new(Guid.NewGuid(),imageProfile,output with{Key="reference-reuse"},"Preserve exact approved reference",[image.Id],[],0),default);await MediaTestFixture.Drain(s);copied=(await library.GenerationsAsync(f.Owner,f.Universe,default)).Single(x=>x.Id==copied.Id);
var copy=await library.ExportAsync(f.Owner,f.Universe,copied.AcceptedVersionId!.Value,"Read",default);if(!copy.Bytes.AsSpan().SequenceEqual(exported.Bytes))throw new Exception("Reference changed");
var callback=new AcceptMediaResult(Guid.NewGuid(),imageRun.Id,imageRun.ExternalResourceId!,exported.Bytes,MediaRules.Hash(exported.Bytes),0);
if((await library.AcceptAsync(f.Owner,f.Universe,callback,default)).Id!=image.Id||(await library.AcceptAsync(f.Owner,f.Universe,callback,default)).Id!=image.Id)throw new Exception("Duplicate callback created a new asset");
var unknown=await MediaTestFixture.Profile(s,f,"media.image.unknown-fixture.v1",locale:"en-US");var pending=await library.GenerateAsync(f.Owner,f.Universe,new(Guid.NewGuid(),unknown,output with{Key="unknown-reconcile"},"Original fixture",[],[],0),default);await MediaTestFixture.Drain(s);
if((await library.GenerationsAsync(f.Owner,f.Universe,default)).Single(x=>x.Id==pending.Id).State!="Unknown")throw new Exception("Ambiguous submission not retained");await library.ReconcileAsync(f.Owner,f.Universe,new(Guid.NewGuid(),pending.Id),default);
if((await library.GenerationsAsync(f.Owner,f.Universe,default)).Single(x=>x.Id==pending.Id).State!="Completed")throw new Exception("Original resource reconciliation failed");
await File.WriteAllBytesAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M16-preview.png"),exported.Bytes);await File.WriteAllBytesAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M16-preview.wav"),audio.Bytes);
await File.WriteAllTextAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M16-fixture.json"),JsonSerializer.Serialize(new{tenant=f.Owner.TenantId,universe=f.Universe,character=c.CharacterId,package=package.Id,presentation=binding,image=image.Id,voice=voice.Id,asset=image.AssetId,assetReference=image.AssetReferenceId,sha256=image.Info.Sha256,duration=audio.Version.Info.DurationSeconds,guestPromotion=true,referencePreserved=true,unsupportedReferenceBlocked=true,duplicateCallback=true,originalReconciliation=true,spokenWindowsPreview=true,humanQualityVerified=false,paidCalls=0}));
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
"@)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'M16 harness build failed'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'M16 guest/media fixture failed'}
$f=Get-Content (Join-Path $repo '.local/M16-fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://localhost:5100';$env:Runtime__Enabled='true'
$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$env:Workflows__WorkerEnabled='false';$env:Knowledge__IndexWorkerEnabled='false';$env:Factory__WorkerEnabled='false';$env:Read__RetentionWorkerEnabled='false';$env:Trends__RecheckWorkerEnabled='false';$env:Media__WorkerEnabled='false'
$env:Assets__Root=Join-Path $repo '.local/test-assets';$env:Assets__Stores__archive=Join-Path $repo '.local/m16-archive';$env:Media__ProviderSpool=Join-Path $repo '.local/m16-provider-spool'
function Start-OwnedApi($number){
$p=Start-Process -FilePath (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err")
$script:hosts+=$p;$ready=$false
for($i=0;$i -lt 60;$i++){if($p.HasExited){throw 'Owned API exited'};try{Invoke-WebRequest 'http://localhost:5100/health/ready' -UseBasicParsing|Out-Null;$ready=$true;break}catch{Start-Sleep -Milliseconds 500}}
if(-not $ready){throw 'API startup timeout'};return $p
}
function Get-Staff($url){Invoke-RestMethod $url -UseDefaultCredentials}
$api=Start-OwnedApi 1;$path="http://localhost:5100/api/v1/media/tenants/$($f.tenant)/universes/$($f.universe)"
$page=Invoke-WebRequest 'http://localhost:5100/staff/media' -UseDefaultCredentials -UseBasicParsing
if($page.StatusCode -ne 200){throw 'Staff library unavailable'}
$move=Invoke-RestMethod "$path/move" -Method Post -UseDefaultCredentials -Headers @{'X-ContentOS-Staff'='1'} -ContentType 'application/json' -Body (@{operationId=[Guid]::NewGuid();versionId=$f.image;storeKey='archive'}|ConvertTo-Json)
if($move.sha256 -ne $f.sha256){throw 'Storage move changed checksum'}
$api|Stop-Process;$api.WaitForExit(10000)|Out-Null;$api=Start-OwnedApi 2
$export=Get-Staff "$path/versions/$($f.image)/export"
if($export.version.assetId -ne $f.asset -or $export.version.assetReferenceId -ne $f.assetReference -or $export.version.info.sha256 -ne $f.sha256 -or $export.storeKey -ne 'archive'){throw 'Restart/move lost stable metadata'}
$preview=Join-Path $dir 'http-preview.png';Invoke-WebRequest "$path/versions/$($f.image)/preview" -UseDefaultCredentials -UseBasicParsing -OutFile $preview
if((Get-FileHash $preview -Algorithm SHA256).Hash -ne $f.sha256){throw 'Actual preview checksum mismatch'}
Invoke-WebRequest "$path/versions/$($f.voice)/preview" -UseDefaultCredentials -UseBasicParsing -OutFile (Join-Path $dir 'http-voice.wav')
$evidence=[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');windowsStaff200=$true;apiRestart=$true;guestPromotion=$true;referencePreserved=$true;unsupportedReferenceBlocked=$true;duplicateCallback=$true;originalResourceReconciliation=$true;spokenWindowsPreview=$true;durationSeconds=$f.duration;storageMove='primary-to-configured-local-archive';checksumAndReferencesPreserved=$true;humanQualityVerified=$false;paidCalls=0;publicPublication=$false;newInteractiveF5Verified=$false}
$evidence|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M16-demo.json')
Write-Host "M16 passed: guest package, original Windows speech, provider gates, callback replay, restart and verified local storage move."
Write-Host "Inspect /staff/media with workspace $($f.tenant) and universe $($f.universe). Human quality/public rights remain pending."
if($DebugPause){Write-Host "Attach VS Code 'Geeta Studio: M16 attach Api' to process $($api.Id), open http://localhost:5100/staff/media and inspect the fixture. Press Enter to stop owned hosts.";Read-Host|Out-Null}
}finally{foreach($p in $hosts){if(-not $p.HasExited){Stop-Process -Id $p.Id -ErrorAction SilentlyContinue;$p.WaitForExit(10000)|Out-Null}};foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}}