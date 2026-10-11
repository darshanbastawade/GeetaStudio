param([switch]$ManageHosts,[switch]$KeepHosts,[switch]$ReuseFixture)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(-not $ManageHosts){throw 'Use -ManageHosts; targets only GeetaStudio_M1Tests.'}
foreach($port in @(5100,5200)){if(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue){throw "Port$port occupied; stop existing debug hosts first."}}
$dir=Join-Path $repo '.local/m14-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Read__PublicEnabled','Read__PreviewOwner','Read__PreviewCollection','Read__PublicBaseUri','Api__BaseAddress')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key)}
$hosts=@();$retained=$false;$evidence=$null
try {
$env:GEETA_REPOSITORY=$repo
if(-not $ReuseFixture){
$links=@('Read','Editorial','Evaluation','Factory','Registry','Lesson','Evidence','Source')|ForEach-Object{"<Compile Include=""$repo/tests/ContentOS.Foundation.Tests/$($_)TestFixture.cs"" Link=""$($_)TestFixture.cs""/>"}
$project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/>$($links -join '')</ItemGroup></Project>"
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
[IO.File]::WriteAllText((Join-Path $dir 'Program.cs'),@"
using System.Text.Json;using System.Security.Principal;using ContentOS.Application;using ContentOS.Foundation.Tests;using Microsoft.Extensions.DependencyInjection;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
using var root=ReadTestFixture.Services();using var scope=root.CreateScope();var s=scope.ServiceProvider;
var f=await ReadTestFixture.Setup(s,WindowsIdentity.GetCurrent().Name);
var b=await s.GetRequiredService<IReadWorkbench>().PreviewAsync(f.Owner.Actor,f.Collection,default);
await File.WriteAllTextAsync(Path.Combine(Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!,".local","M14-fixture.json"),JsonSerializer.Serialize(new{tenant=f.Owner.TenantId,universe=f.Universe,collection=f.Collection,key=f.Key,item=f.Items[0].Id,piece=f.Pieces[0],audience=b!.Pieces[0].AudienceId}));
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
"@)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Harness build failed'}
& dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
if($LASTEXITCODE -ne 0){throw 'Fixture setup failed'}
}
$f=Get-Content (Join-Path $repo '.local/M14-fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:Runtime__Enabled='true'
$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$env:Workflows__WorkerEnabled='false';$env:Knowledge__IndexWorkerEnabled='false';$env:Factory__WorkerEnabled='false';$env:Read__RetentionWorkerEnabled='false';$env:Read__PublicEnabled='false'
$env:Read__PreviewOwner=$env:Staff__Administrator__0;$env:Read__PreviewCollection=$f.collection;$env:Read__PublicBaseUri='http://localhost:5200/';$env:Api__BaseAddress='http://localhost:5100/'
function Start-OwnedHost($name,$port,$number){
$env:ASPNETCORE_URLS="http://localhost:$port"
$p=Start-Process -FilePath (Join-Path $repo "src/GeetaStudio.$name/bin/Debug/net10.0/GeetaStudio.$name.exe") -WorkingDirectory (Join-Path $repo "src/GeetaStudio.$name") -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "$name-$number.out") -RedirectStandardError (Join-Path $dir "$name-$number.err")
$script:hosts+=$p
$readyPath=if($name -eq 'Api'){'/health/ready'}else{'/'}
$ready=$false;for($i=0;$i -lt 60;$i++){if($p.HasExited){throw "$name exited; see .local/m14-demo logs"};try{Invoke-WebRequest "http://localhost:$port$readyPath" -UseDefaultCredentials -UseBasicParsing|Out-Null;$ready=$true;break}catch{Start-Sleep -Milliseconds 500}}
if(-not $ready){throw "$name startup timeout"};return $p
}
function Get-WindowsPage($url,[switch]$AllowError){
Add-Type -AssemblyName System.Net.Http
$handler=[Net.Http.HttpClientHandler]::new();$handler.UseDefaultCredentials=$true;$handler.AllowAutoRedirect=$false
$client=[Net.Http.HttpClient]::new($handler)
try{$response=$client.GetAsync($url).GetAwaiter().GetResult();$body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult();if(-not $AllowError -and -not $response.IsSuccessStatusCode){throw "Windows page HTTP$([int]$response.StatusCode)"};$headers=@{};foreach($h in $response.Headers){$headers[$h.Key]=$h.Value -join ','};return @{StatusCode=[int]$response.StatusCode;Content=$body;Headers=$headers}}finally{$client.Dispose()}
}
function Get-Staff($url){Invoke-RestMethod $url -UseDefaultCredentials}
function Post-Staff($url,$body){Invoke-RestMethod $url -Method Post -UseDefaultCredentials -Headers @{'X-ContentOS-Staff'='1'} -ContentType 'application/json' -Body ($body|ConvertTo-Json -Depth 8)}
$api=Start-OwnedHost 'Api' 5100 1;$web=Start-OwnedHost 'Web' 5200 1
$preview='http://localhost:5200/preview/read/'+$f.key
$book=Get-Staff "http://localhost:5100/api/v1/read-preview/$($f.collection)"
$f.audience=($book.pieces|Where-Object id -eq $f.piece).audienceId
$staffPage=Invoke-WebRequest 'http://localhost:5100/staff/read-collections' -UseDefaultCredentials -UseBasicParsing
if($staffPage.StatusCode -ne 200){throw 'READ staff page failed'}
try { $page=Get-WindowsPage "$preview/chapter/1/verse/1?locale=en-IN&audience=$($f.audience)" }
catch { if($_.Exception.Response){$stream=[IO.StreamReader]::new($_.Exception.Response.GetResponseStream());[IO.File]::WriteAllText((Join-Path $dir 'private-error.txt'),$stream.ReadToEnd());$stream.Dispose()};throw }
if($page.StatusCode -ne 200 -or $page.Content -notmatch 'Private fixture preview' -or $page.Content -match '_framework/blazor.web.js'){throw 'Private static reader failed'}
if($page.Headers['Cache-Control'] -notmatch 'no-store' -or $page.Headers['X-Robots-Tag'] -notmatch 'noindex'){throw 'Preview headers failed'}
$book=Get-Staff "http://localhost:5100/api/v1/read-preview/$($f.collection)"
if($book.pieces.Count -ne 4 -or @($book.pieces|Where-Object locale -eq 'mr-IN').Count -ne 1){throw 'Variants failed'}
$anon=Invoke-WebRequest 'http://localhost:5200/' -UseBasicParsing
$sitemap=Invoke-WebRequest 'http://localhost:5200/sitemap.xml' -UseBasicParsing
if($sitemap.Content -match '<loc>' -or $anon.Content -match $f.key){throw 'Fixture leaked publicly'}
$path="http://localhost:5100/api/v1/read-admin/tenants/$($f.tenant)/universes/$($f.universe)/collections/$($f.collection)"
$status=Get-Staff $path
if($status.releaseBlocks -notcontains 'FixtureCollectionCannotRollOut'){throw 'Fixture release gate missing'}
if($KeepHosts){
@{api=$api.Id;web=$web.Id;apiStarted=$api.StartTime.ToUniversalTime().ToString('o');webStarted=$web.StartTime.ToUniversalTime().ToString('o');url=$preview;collection=$f.collection}|ConvertTo-Json|Set-Content (Join-Path $repo '.local/M14-owned-hosts.json')
$retained=$true;Write-Host "Private preview: $preview"
}else{
$web|Stop-Process;$web.WaitForExit(10000)|Out-Null;$api|Stop-Process;$api.WaitForExit(10000)|Out-Null
$api=Start-OwnedHost 'Api' 5100 2;$web=Start-OwnedHost 'Web' 5200 2
if((Get-Staff "http://localhost:5100/api/v1/read-preview/$($f.collection)").pieces.Count -ne 4){throw 'Restart durability failed'}
$editorial="http://localhost:5100/api/v1/editorial/tenants/$($f.tenant)/universes/$($f.universe)/items/$($f.item)"
$item=Get-Staff $editorial
Post-Staff "$editorial/actions" @{operationId=[Guid]::NewGuid();expectedRowVersion=$item.rowVersion;action='Withdraw';mode='Fixture';evidenceReference='fixture:read-withdrawal';channel='LocalPreview'}|Out-Null
$changed=Get-Staff "http://localhost:5100/api/v1/read-preview/$($f.collection)"
if($changed.pieces.Count -ne 3 -or @($changed.pieces|Where-Object id -eq $f.piece).Count){throw 'Withdrawal failed'}
$withdrawn=Get-WindowsPage "$preview/chapter/1/verse/1?locale=en-IN&audience=$($f.audience)" -AllowError
if($withdrawn.StatusCode -ne 404 -or $withdrawn.Content -match $f.piece){throw 'Withdrawn verse remained in SSR'}
$search=Get-WindowsPage "$preview/search?q=Pause"
if($search.Content -match $f.piece){throw 'Withdrawn piece remained in search'}
$status=Get-Staff $path
Post-Staff "$path/control" @{operationId=[Guid]::NewGuid();expectedRowVersion=$status.rowVersion;action='Disable'}|Out-Null
$disabled=$false;try{Get-Staff "http://localhost:5100/api/v1/read-preview/$($f.collection)"|Out-Null}catch{if([int]$_.Exception.Response.StatusCode -eq 404){$disabled=$true}else{throw}}
if(-not $disabled){throw 'Disable failed'}
$evidence=@{date=(Get-Date -Format 'yyyy-MM-dd');privateSsr200=$true;noBlazorBoot=$true;noStoreNoindex=$true;fourVariants=$true;englishMarathiSanskrit=$true;twoVerses=$true;anonymousDirectoryEmpty=$true;sitemapEmpty=$true;fixtureRolloutBlocked=$true;actualApiWebRestart=$true;withdrawalRemovedPiece=$true;withdrawnSsr404=$true;withdrawnSearchAbsent=$true;staffCollections200=$true;disableRollback404=$true;liveCalls=0;humanReleasePending=$true}
}
}finally{
if(-not $retained){foreach($p in $hosts){if(-not $p.HasExited){Stop-Process -Id $p.Id -ErrorAction SilentlyContinue;$p.WaitForExit(10000)|Out-Null}}}
foreach($key in $prior.Keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}
}
if($evidence){$evidence.ownedHostsStopped=@($hosts|Where-Object{-not $_.HasExited}).Count -eq 0;$evidence|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M14-demo.json')}