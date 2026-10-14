param([switch]$DebugPause,[switch]$ReuseFixture)
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
if(Get-NetTCPConnection -State Listen -LocalPort 5100 -ErrorAction SilentlyContinue){throw 'Port 5100 is occupied; stop your API/debug host first.'}
$dir=Join-Path $repo '.local/m27-demo';New-Item -ItemType Directory -Force $dir|Out-Null
$keys=@('GEETA_REPOSITORY','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS','Runtime__Enabled','ConnectionStrings__ContentOS','Staff__Administrator__0','Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled','Optimization__MonitorEnabled')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key)};$owned=@()
Push-Location $repo
try{
$env:GEETA_REPOSITORY=$repo
if(!$ReuseFixture){
    $names=@('SourceTestFixture','EvidenceTestFixture','LessonTestFixture','RegistryTestFixture','FactoryTestFixture','EvaluationTestFixture','EditorialTestFixture','OptimizationTestFixture')
    $links=$names|ForEach-Object{ $file=Join-Path $repo "tests/ContentOS.Foundation.Tests/$_.cs";"<Compile Include=""$file"" Link=""$_.cs""/>" }
    $project="<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><RuntimeFrameworkVersion>10.0.13</RuntimeFrameworkVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include=""Microsoft.AspNetCore.App""/><ProjectReference Include=""$repo/src/ContentOS.Infrastructure/ContentOS.Infrastructure.csproj""/><ProjectReference Include=""$repo/src/ContentOS.Integrations/ContentOS.Integrations.csproj""/><Compile Include=""$repo/scripts/fixtures/OptimizationDemo.cs"" Link=""Program.cs""/>$($links -join '')</ItemGroup></Project>"
    [IO.File]::WriteAllText((Join-Path $dir 'Demo.csproj'),$project)
    & dotnet build (Join-Path $dir 'Demo.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false
    if($LASTEXITCODE -ne 0){throw 'M27 harness build failed'}
    & dotnet run --project (Join-Path $dir 'Demo.csproj') --no-build
    if($LASTEXITCODE -ne 0){throw 'M27 fixture acceptance failed'}
}
$f=Get-Content (Join-Path $dir 'fixture.json') -Raw|ConvertFrom-Json
if($f.database -ne 'GeetaStudio_M1Tests' -or !$f.highRiskApprovalDenied -or $f.rollbackState -ne 'RolledBack'){throw 'Expected private test fixture absent'}
$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:Runtime__Enabled='true';$env:ConnectionStrings__ContentOS='Server=localhost;Database=GeetaStudio_M1Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;';$env:Staff__Administrator__0=[Security.Principal.WindowsIdentity]::GetCurrent().Name;$env:ASPNETCORE_URLS='http://localhost:5100'
foreach($key in @('Workflows__WorkerEnabled','Knowledge__IndexWorkerEnabled','Factory__WorkerEnabled','Read__RetentionWorkerEnabled','Trends__RecheckWorkerEnabled','Media__WorkerEnabled','Watch__WorkerEnabled','Social__WorkerEnabled','Optimization__MonitorEnabled')){[Environment]::SetEnvironmentVariable($key,'false')}
Add-Type -AssemblyName System.Net.Http
function Request-Private([string]$path){
    $uri=[Uri]('http://localhost:5100'+$path);if($uri.Host -ne 'localhost' -or $uri.Port -ne 5100){throw 'Loopback only'}
    $handler=New-Object System.Net.Http.HttpClientHandler;$handler.UseDefaultCredentials=$true;$handler.AllowAutoRedirect=$false;$client=New-Object System.Net.Http.HttpClient($handler)
    try{$response=$client.GetAsync($uri).GetAwaiter().GetResult();$response.EnsureSuccessStatusCode()|Out-Null;$body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult();if(!$response.Headers.CacheControl.NoStore){throw 'Private no-store header missing'};return $body}finally{$client.Dispose();$handler.Dispose()}
}
function Start-Owned([int]$number){
    $p=Start-Process -FilePath (Join-Path $repo 'src/GeetaStudio.Api/bin/Debug/net10.0/GeetaStudio.Api.exe') -WorkingDirectory (Join-Path $repo 'src/GeetaStudio.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dir "api-$number.out") -RedirectStandardError (Join-Path $dir "api-$number.err");$script:owned+=$p
    for($i=0;$i -lt 60;$i++){try{Request-Private '/staff/optimization'|Out-Null;return $p}catch{if($p.HasExited){throw 'Owned API exited; inspect .local/m27-demo logs'};Start-Sleep -Milliseconds 500}}
    throw 'Owned API did not become ready'
}
$api=Start-Owned 1
if($DebugPause){Write-Host "Attach Geeta Studio: M27 attach Api to process $($api.Id). Set a breakpoint in OptimizationOperations.ReportAsync, then continue here.";Read-Host 'Press Enter when attached'|Out-Null}
$path="/api/v1/optimization/tenants/$($f.tenant)/universes/$($f.universe)"
$report=Request-Private ($path+'/report');[IO.File]::WriteAllText((Join-Path $dir 'report.json'),$report)
$experiments=Request-Private ($path+'/experiments')|ConvertFrom-Json
if(@($experiments|Where-Object{$_.id -eq $f.experiment -and $_.state -eq 'RolledBack'}).Count -ne 1){throw 'Persisted rollback not visible over Windows HTTP'}
Stop-Process -Id $api.Id;$api.WaitForExit();$api=Start-Owned 2
$again=Request-Private ($path+'/experiments')|ConvertFrom-Json
if(@($again|Where-Object{$_.id -eq $f.experiment -and $_.state -eq 'RolledBack'}).Count -ne 1){throw 'API restart lost rollback'}
$evidence=[ordered]@{verifiedUtc=([DateTimeOffset]::UtcNow).ToString('o');windowsAuthentication=$true;noStore=$true;apiRestart=$true;candidateActivated=$f.candidateAccepted;rollbackState=$f.rollbackState;highRiskApprovalDenied=$f.highRiskApprovalDenied;fixtureOnly=$true;liveCalls=0;publicPublishing=$false;interactiveDebugVerified=$false}
$evidence|ConvertTo-Json|Set-Content (Join-Path $repo 'docs/milestones/evidence/M27-demo.json')
Write-Host "Verified private M27 demo. Open http://localhost:5100/staff/optimization during -DebugPause; tenant $($f.tenant), universe $($f.universe). Owned API hosts are stopped on exit."
}finally{foreach($p in $owned){if(!$p.HasExited){Stop-Process -Id $p.Id -ErrorAction SilentlyContinue}};foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$prior[$key])};Pop-Location}
