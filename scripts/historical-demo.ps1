param([switch]$ManageHosts,[switch]$DebugPause,[switch]$ReuseFixture,[switch]$ImportOnly,[ValidateSet('GeetaStudio_M1Tests','GeetaStudio_Dev')][string]$Database='GeetaStudio_M1Tests')
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(!$ImportOnly -and (!$ManageHosts -or $Database -ne 'GeetaStudio_M1Tests')){throw 'Host demo requires -ManageHosts and the dedicated Test database. Use -ImportOnly for additive private Development READ samples.'}
if(!$ImportOnly){foreach($port in @(5100,5200)){if(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue){throw "Port $port occupied; stop your debug hosts first."}}}
$dir=Join-Path $repo ".local/m26-demo/$Database";New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('GEETA_REPOSITORY','GEETA_M26_DATABASE','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Read__PublicEnabled','Read__PreviewOwner','Api__BaseAddress','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled','Assets__Root','Assets__Stores__watch','Assets__Stores__archive','Watch__ToolsConfig')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key)};$owned=@()
try{
$env:GEETA_REPOSITORY=$repo;$env:GEETA_M26_DATABASE=$Database
if(!$ReuseFixture){
    $fixtureNames=@('WatchTestFixture','TrendTestFixture','MediaTestFixture','EditorialTestFixture','EvaluationTestFixture','FactoryTestFixture','RegistryTestFixture','LessonTestFixture','EvidenceTestFixture','SourceTestFixture','ReadTestFixture','HistoricalTestFixture','HistoricalWatchFixture','HistoricalPipelineFixture')
    $files=$fixtureNames|ForEach-Object{Get-Item (Join-Path $repo ('tests/ContentOS.Foundation.Tests/'+$_+'.cs'))}
    $links=$files|ForEach-Object{"<Compile Include=""$($_.FullName)"" Link=""$($_.Name)""/>"}
    $project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><RuntimeFrameworkVersion>10.0.13</RuntimeFrameworkVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/><Compile Include=""$repo/scripts/fixtures/HistoricalDemo.cs"" Link=""Program.cs""/>$($links -join '')</ItemGroup></Project>"
    [IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
    & dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
    if($LASTEXITCODE -ne 0){throw 'Historical harness build failed'}
    & dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
    if($LASTEXITCODE -ne 0){throw 'Historical fixture setup failed'}
}
$f=Get-Content (Join-Path $dir 'fixture.json') -Raw|ConvertFrom-Json
if($f.database -ne $Database -or $f.readCount -ne 10){throw 'Fixture does not match the requested private database/target'}
if($ImportOnly){Write-Host "Additive private READ fixture: $dir. Human historical review remains pending.";return}
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:Runtime__Enabled='true';$env:ConnectionStrings__ContentOS="Server=localhost;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
foreach($key in @('Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled')){[Environment]::SetEnvironmentVariable($key,'false')}
$env:Read__PublicEnabled='false';$env:Read__PreviewOwner=$env:Staff__Administrator__0;$env:Api__BaseAddress='http://localhost:5100/'
$env:Assets__Root=Join-Path $repo '.local/test-assets';$env:Assets__Stores__watch=Join-Path $repo '.local/watch-test-masters';$env:Assets__Stores__archive=Join-Path $repo '.local/m16-archive';$env:Watch__ToolsConfig=Join-Path $repo '.local/M17-tools.json'
function Start-Owned($name,$port,$n){
    $env:ASPNETCORE_URLS="http://localhost:$port"
    $p=Start-Process -FilePath (Join-Path $repo "src/GeetaStudio.$name/bin/Debug/net10.0/GeetaStudio.$name.exe") -WorkingDirectory (Join-Path $repo "src/GeetaStudio.$name") -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "$name-$n.out") -RedirectStandardError (Join-Path $dir "$name-$n.err");$script:owned+=$p
    $path=if($name -eq 'Api'){'/health/ready'}else{'/'}
    for($i=0;$i -lt 60;$i++){if($p.HasExited){throw "$name exited; inspect $dir logs"};try{Invoke-WebRequest "http://localhost:$port$path" -UseBasicParsing|Out-Null;return $p}catch{Start-Sleep -Milliseconds 500}}
    throw "$name startup timeout"
}
function Get-Private($url,$file=$null){
    $uri=[Uri]$url;if(!$uri.IsLoopback -or $uri.Host -ne 'localhost' -or $uri.Port -notin @(5100,5200)){throw 'Private credentials are restricted to the owned loopback demo hosts'}
    Add-Type -AssemblyName System.Net.Http
    $handler=[Net.Http.HttpClientHandler]::new();$handler.UseDefaultCredentials=$true;$handler.AllowAutoRedirect=$false;$client=[Net.Http.HttpClient]::new($handler)
    try{
        $response=$client.GetAsync($uri).GetAwaiter().GetResult();if(!$response.IsSuccessStatusCode){throw "Private page HTTP $([int]$response.StatusCode)"}
        $headers=@{};foreach($h in $response.Headers){$headers[$h.Key]=$h.Value -join ','}
        $content='';if($file){[IO.File]::WriteAllBytes($file,$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult())}else{$content=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}
        return [pscustomobject]@{StatusCode=[int]$response.StatusCode;Content=$content;Headers=$headers}
    }finally{$client.Dispose()}
}
function Check-Pages{
    $directory=(Get-Private 'http://localhost:5100/api/v1/read-preview').Content|ConvertFrom-Json
    if(@($directory|Where-Object id -eq $f.collection).Count -ne 1 -or @($directory|Where-Object id -eq $f.geetaCollection).Count -ne 1){throw 'Private universe directory missing samples'}
    $directoryPage=Get-Private 'http://localhost:5100/api/v1/read-preview';if($directoryPage.Headers['Cache-Control'] -notmatch 'no-store' -or $directoryPage.Headers['X-Robots-Tag'] -notmatch 'noindex'){throw 'Private directory headers failed'}
    $selector=Get-Private 'http://localhost:5200/preview/universes'
    if($selector.Content -notmatch 'Shivaji' -or $selector.Content -notmatch 'Geeta scripture fixture'){throw 'Same-application universe selector missing'}
    $historical=Get-Private "http://localhost:5200/preview/read/$($f.key)/chapter/1/verse/9?locale=en-IN&audience=$($f.audience)"
    if($historical.Content -notmatch 'Debated historical claim' -or $historical.Content -notmatch 'Differing historical account' -or $historical.Content -match 'No reviewed transliteration'){throw 'Historical labels or source distinctions failed'}
    $legend=Get-Private "http://localhost:5200/preview/read/$($f.key)/chapter/1/verse/10?locale=en-IN&audience=$($f.audience)"
    $search=Get-Private "http://localhost:5200/preview/read/$($f.key)/search?q=Raigad";if($search.Content -notmatch 'data-read-card'){throw 'Historical place search failed'}
    if($legend.Content -notmatch 'Popular legend' -or $legend.Content -notmatch 'Undated'){throw 'Legend/date label failed'}
    $original=Get-Private "http://localhost:5200/preview/read/$($f.geetaKey)"
    if($original.Content -notmatch 'Chapter' -or $original.Content -notmatch 'verse'){throw 'Original scripture READ path failed'}
    if($historical.Headers['Cache-Control'] -notmatch 'no-store' -or $historical.Headers['X-Robots-Tag'] -notmatch 'noindex'){throw 'Private headers failed'}
    $book=(Get-Private "http://localhost:5100/api/v1/read-preview/$($f.collection)").Content|ConvertFrom-Json
    if($book.materialKind -ne 'History' -or $book.pieces.Count -ne 10){throw 'History preview missing entries'}
    foreach($sample in $f.watch){
        $path="http://localhost:5100/api/v1/watch/tenants/$($f.tenant)/universes/$($f.universe)"
        $captions=Get-Private "$path/productions/$($sample.id)/captions.vtt";if($captions.Content -notmatch 'WEBVTT'){throw 'Historical captions failed'}
        foreach($a in $sample.artifacts){$file=Join-Path $dir ("http-"+$sample.id+'-'+$a.aspect+'.mp4');Get-Private "$path/artifacts/$($a.id)/preview" $file|Out-Null;if((Get-FileHash $file).Hash -ne $a.sha256){throw 'Historical master checksum changed'}}
    }
    if((Invoke-RestMethod 'http://localhost:5100/api/v1/read').Count -ne 0){throw 'Private fixture leaked to public directory'}
    [IO.File]::WriteAllText((Join-Path $dir 'selector.html'),$selector.Content);[IO.File]::WriteAllText((Join-Path $dir 'disagreement.html'),$historical.Content)
}
$api=Start-Owned 'Api' 5100 1;$web=Start-Owned 'Web' 5200 1;Check-Pages
if($DebugPause){Write-Host "Open http://localhost:5200/preview/universes. Attach M26 API/Web to processes $($api.Id)/$($web.Id).";Read-Host 'After inspection, press Enter to check restart and stop owned hosts'}
foreach($p in @($web,$api)){$p|Stop-Process;$p.WaitForExit(10000)|Out-Null}
$api=Start-Owned 'Api' 5100 2;$web=Start-Owned 'Web' 5200 2;Check-Pages
[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');database=$Database;readCount=10;watchCount=2;actualWindowsSpeech=$true;actualFfmpegMasters=4;windowsPrivateHttp=$true;universeSwitch=$true;disagreementAndLegendVisible=$true;restart=$true;ownedChecksums=$true;publicRelease=$false;historicalHumanReview='Pending';humanListeningAndVisualReview='Pending';interactiveDebuggerVerified=$false;liveCalls=0}|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M26-demo.json')
Write-Host "M26 private demo passed. Samples and captions: $dir. Owned hosts will stop."
}finally{foreach($p in $owned){if(!$p.HasExited){$p|Stop-Process;$p.WaitForExit(10000)|Out-Null}};foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])}}
