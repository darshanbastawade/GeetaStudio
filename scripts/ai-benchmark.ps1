param([string]$BaseUri='http://localhost:5100', [switch]$Live, [decimal]$MaxEstimatedCost=.25, [string]$Output='docs/milestones/evidence/M07-benchmark.json')
$ErrorActionPreference='Stop'
$uri=[Uri]$BaseUri
if (!$uri.IsLoopback -or $uri.Scheme -ne 'http' -or $uri.Port -ne 5100) {throw 'Use the local staff API.'}
$staff=@{UseDefaultCredentials=$true;Headers=@{'X-ContentOS-Staff'='1'}}
if($PSVersionTable.PSVersion.Major -ge 7){$staff.AllowUnencryptedAuthentication=$true}
function Call([string]$method,[string]$path,$body=$null){
 $request=@{Method=$method;Uri="$BaseUri/$path";ContentType='application/json'}
 if($null -ne $body){$request.Body=[Text.Encoding]::UTF8.GetBytes(($body|ConvertTo-Json -Depth 25 -Compress))}
 $response=Invoke-RestMethod @request @staff
 $response
}
$root=Split-Path $PSScriptRoot -Parent
$fixture=Get-Content (Join-Path $root 'docs/fixtures/M07-benchmark.json') -Raw|ConvertFrom-Json
$tenant=@(Call GET 'api/v1/core/tenants')|Where-Object key -eq 'm02-local'|Select-Object -First 1
if(!$tenant){throw 'Provision the local fixture tenant.'}
$universe=@(Call GET "api/v1/core/tenants/$($tenant.id)/universes")|Where-Object key -eq 'geeta'|Select-Object -First 1
$base="api/v1/ai/tenants/$($tenant.id)"
$routes=@(Call GET "$base/routes");$profiles=@(Call GET "$base/profiles");$estimate=0
foreach($case in $fixture.cases){
 $route=$routes|Where-Object {$_.active -and $_.settings.task -eq $case.task}|Select-Object -First 1
 if(!$route){throw 'Run the M07 demo/configure this task route first.'}
 $estimate += [decimal]$route.settings.runLimit
 foreach($id in $route.settings.profiles){
  $profile=$profiles|Where-Object id -eq $id|Select-Object -First 1
  if($profile.settings.adapter -eq 'openai.text.v1' -and !$Live){throw 'Real benchmark calls need -Live, an enabled approved live profile and an explicit budget ceiling.'}
 }
}
if($estimate -gt $MaxEstimatedCost){throw 'Worst-case route allowances exceed the benchmark ceiling.'}
$rows=@()
foreach($case in $fixture.cases){
 $route=$routes|Where-Object {$_.active -and $_.settings.task -eq $case.task}|Select-Object -First 1
 $watch=[Diagnostics.Stopwatch]::StartNew()
 $run=Call POST "$base/universes/$($universe.id)/generate" @{operationId=[guid]::NewGuid().ToString();task=$case.task;prompt=$case.prompt;classification=$fixture.classification;region=$route.settings.region;maxRetentionDays=$route.settings.maxRetentionDays;allowTraining=$route.settings.allowTraining;structured=$route.settings.structured}
 $watch.Stop()
 $keyword=$false
 if($run.output){foreach($term in $case.expectedAny){if($run.output.text.IndexOf($term,[StringComparison]::OrdinalIgnoreCase) -ge 0){$keyword=$true}}}
 $rows += [ordered]@{task=$case.task;runId=$run.id;routeId=$run.snapshot.routeId;routeVersion=$run.snapshot.route.version;outcome=$run.outcome;available=($run.outcome -eq 'Success');elapsedMs=$watch.ElapsedMilliseconds;structuredQuality=($null -ne $run.output);keywordQuality=$keyword;humanQuality='NotReviewed';cost=$run.spent;unresolvedReservation=$run.reserved;currency=$run.snapshot.route.currency;attempts=@($run.attempts|ForEach-Object{@{profileId=$_.profileId;usage=$_.usage;cost=$_.cost;outcome=$_.outcome}})}
}
$report=[ordered]@{fixture=$fixture.version;qualityScope=$fixture.qualityScope;verifiedUtc=[DateTimeOffset]::UtcNow.ToString('o');routingChanged=$false;liveEnabled=[bool]$Live;results=$rows}
$destination=Join-Path $root $Output
$report|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $destination -Encoding UTF8
$report|ConvertTo-Json -Depth 30
