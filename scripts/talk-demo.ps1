param([switch]$ManageApi,[switch]$DebugPause)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(!$ManageApi){throw 'Use -ManageApi; this demo uses the dedicated Test database only.'}
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Port5100 occupied; stop owned debug hosts first.'}
$dir=Join-Path $repo '.local/m21-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled','Talk__RetentionWorkerEnabled')
$auth=@{UseDefaultCredentials=$true}; if((Get-Command Invoke-WebRequest).Parameters.ContainsKey('AllowUnencryptedAuthentication')){$auth.AllowUnencryptedAuthentication=$true} # Fixed loopback Windows challenge authentication only.
$prior=@{};foreach($k in $keys){$prior[$k]=[Environment]::GetEnvironmentVariable($k)};$owned=@()
try{
$project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/>"
foreach($file in @('scripts/fixtures/TalkDemo.cs','tests/ContentOS.Foundation.Tests/TalkTestFixture.cs','tests/ContentOS.Foundation.Tests/CustomerTestFixture.cs','tests/ContentOS.Foundation.Tests/TalkCorpusFixture.cs')){$project+="<Compile Include=""$repo/$file"" Link=""$([IO.Path]::GetFileName($file))""/>"};$project+='</ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
& dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'M21 fixture build failed.'}
& dotnet (Join-Path $dir 'bin/Debug/net10.0/Demo.dll') $dir
if($LASTEXITCODE -ne 0){throw 'M21 fixture setup failed.'}
$f=Get-Content (Join-Path $dir 'fixture.json') -Raw|ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:Runtime__Enabled='true';$env:ASPNETCORE_URLS='http://localhost:5100';$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;';$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name
foreach($key in $keys|Where-Object {$_ -like '*WorkerEnabled'}){[Environment]::SetEnvironmentVariable($key,'false')}
function Start-OwnedApi($number){$p=Start-Process -FilePath (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err");$script:owned+=$p;for($i=0;$i -lt 60;$i++){if($p.HasExited){throw 'Owned API exited'};try{Invoke-WebRequest 'http://localhost:5100/health/ready' -UseBasicParsing|Out-Null;return $p}catch{Start-Sleep -Milliseconds 500}};throw 'API startup timeout'}
function Post-Private($url,$body){Invoke-RestMethod $url -Method Post -ContentType 'application/json' -Headers @{'X-ContentOS-Staff'='1'} -Body ($body|ConvertTo-Json -Depth 12) @auth}
$api=Start-OwnedApi 1;$path="http://localhost:5100/api/v1/customer/tenants/$($f.tenant)/talk";$session="$path/sessions/$($f.session)"
$page=Invoke-WebRequest 'http://localhost:5100/talk' @auth -UseBasicParsing;if($page.StatusCode -ne 200 -or $page.Headers['Cache-Control'] -ne 'no-store'){throw 'Private TALK page unavailable'}
if($DebugPause){Write-Host "M21 API process $($api.Id). Attach Geeta Studio: M21 attach Api. Open /talk; workspace $($f.tenant). Session $($f.session).";Read-Host 'Inspect admission/citations/credits; press Enter to complete demo and cleanup'}
$request=@{operationId=[guid]::NewGuid().ToString();question='Where did the opposing armies gather?'};$first=Post-Private "$session/turns" $request;if($first.outcome -ne 'Supported' -or $first.credits -ne 1 -or !$first.sources.Count){throw 'Supported grounded turn failed'}
$unsupported=Post-Private "$session/turns" @{operationId=[guid]::NewGuid().ToString();question='How do I bake a pizza?'};if($unsupported.outcome -ne 'NoSupportedAnswer' -or $unsupported.aiRunId){throw 'Unsupported path generated scripture'}
$isolated=$false;try{Invoke-RestMethod "$path/sessions/$($f.otherSession)" @auth|Out-Null}catch{if([int]$_.Exception.Response.StatusCode -eq 403){$isolated=$true}else{throw}};if(!$isolated){throw 'Another account session leaked'}
Post-Private "http://localhost:5100/api/v1/ai/tenants/$($f.tenant)/routes/$($f.routeB)/activate" @{}|Out-Null
$second=Post-Private "$session/turns" @{operationId=[guid]::NewGuid().ToString();question='Where did the opposing armies gather?'};if($second.routeId -ne $f.routeB -or $first.routeId -ne $f.routeA){throw 'New turn route boundary differs'}
Post-Private "$session/turns/$($first.id)/report" @{operationId=[guid]::NewGuid().ToString();category='Citation'}|Out-Null
$stream=Invoke-WebRequest "$session/stream" -Method Post -ContentType 'application/json' -Headers @{'X-ContentOS-Staff'='1'} -Body (@{operationId=[guid]::NewGuid().ToString();question='Where did the opposing armies gather?'}|ConvertTo-Json) @auth -UseBasicParsing;$streamText=$stream.Content;if($streamText -is [byte[]]){$streamText=[Text.Encoding]::UTF8.GetString($streamText)};$events=@($streamText -split "`n"|Where-Object {$_}|ForEach-Object {$_|ConvertFrom-Json});if(($events|Where-Object kind -eq 'Delta').Count -lt 1 -or $events[-1].kind -ne 'Completed' -or $events[-1].turn.outcome -ne 'Supported'){throw 'Validated streaming failed'}
$api|Stop-Process;$api.WaitForExit(10000)|Out-Null;$api=Start-OwnedApi 2
$history=Invoke-RestMethod $session @auth;if($history.turns.Count -ne 4 -or ($history.turns|Where-Object {$_.question -or $_.answer})){throw 'Default private history retained conversation text or lost metadata'}
$replay=Post-Private "$session/turns" $request;if($replay.id -ne $first.id -or $replay.routeId -ne $f.routeA){throw 'Original turn replay changed'}
$wallet=Invoke-RestMethod "http://localhost:5100/api/v1/customer/tenants/$($f.tenant)/wallet" @auth;if($wallet.available -ne 7 -or $wallet.reserved -ne 0){throw 'Credit charging/replay differs'}
Post-Private "$session/forget" @{}|Out-Null
[ordered]@{verifiedDate=(Get-Date -Format 'yyyy-MM-dd');windowsPrivateTalk200=$true;noStore=$true;supportedCitations=$true;unsupportedNoAiRun=$true;otherSession403=$true;newTurnRouteSwitch=$true;originalRouteReplayAfterRestart=$true;validatedStreaming=$true;defaultQuestionAnswerSqlAbsent=$true;availableCredits=7;heldCredits=0;apiRestart=$true;problemReport=$true;forgottenSession=$true;paidExternalCalls=0;liveQualified=$false;freshF5Verified=$false}|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M21-demo.json')
Write-Host 'M21 Windows grounded/no-answer, isolation, route switch, validated streaming, credits, reports and API restart passed. Controlled fixtures only.'
}finally{foreach($p in $owned){if(!$p.HasExited){Stop-Process -Id $p.Id;$p.WaitForExit(10000)|Out-Null}};foreach($k in $keys){[Environment]::SetEnvironmentVariable($k,$prior[$k])}}



