param([string]$BaseUri = 'http://localhost:5100', [switch]$ManageApi)
$ErrorActionPreference='Stop'
$uri=[Uri]$BaseUri
if (!$uri.IsLoopback -or $uri.Scheme -ne 'http' -or $uri.Port -ne 5100) { throw 'Use the local API on port 5100.' }
$repo=Split-Path $PSScriptRoot -Parent
$ownedApi=$null; $previousRoute=$null; $aiBase=$null
$staff=@{UseDefaultCredentials=$true;Headers=@{'X-ContentOS-Staff'='1'}}
if($PSVersionTable.PSVersion.Major -ge 7){$staff.AllowUnencryptedAuthentication=$true}
function Call([string]$method,[string]$path,$body=$null){
 $request=@{Method=$method;Uri="$BaseUri/$path";ContentType='application/json'}
 if($null -ne $body){$request.Body=[Text.Encoding]::UTF8.GetBytes(($body|ConvertTo-Json -Depth 40 -Compress))}
 $response=Invoke-RestMethod @request @staff; $response
}
function Start-OwnedApi {
 $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,5100)
 try{$listener.Start()}catch{throw 'Port 5100 must be free before the owned restart demonstration.'}finally{$listener.Stop()}
 $local=Join-Path $repo '.local';New-Item -ItemType Directory -Path $local -Force|Out-Null
 $process=Start-Process dotnet -ArgumentList @('run','--project','src/GeetaStudio.Api','--no-build','--launch-profile','Development') -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $local 'm08-api.log') -RedirectStandardError (Join-Path $local 'm08-api.err')
 try {
  for($i=0;$i -lt 45;$i++){
   if($process.HasExited){throw 'Owned API exited; inspect redacted local logs.'}
   try{if((Invoke-RestMethod "$BaseUri/health/ready").status -eq 'Ready'){return $process}}catch{}
   Start-Sleep -Seconds 1
  }
  throw 'Owned API readiness timeout.'
 } catch { Stop-OwnedApi $process; throw }
}
function Stop-OwnedApi($process){
 if(!$process){return}
 $root=Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)"
 if(!$root){return}
 if($root.CommandLine -notmatch 'dotnet.*run.*src/GeetaStudio.Api.*--launch-profile.*Development'){throw 'Owned API identity no longer matches; refusing to stop it.'}
 foreach($child in Get-CimInstance Win32_Process -Filter "ParentProcessId=$($process.Id)"){
  if($child.ExecutablePath -like "$repo\src\GeetaStudio.Api\bin\*"){Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue}
 }
 Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
 Start-Sleep -Seconds 1
}
function Wait-Review([string]$path){
 for($i=0;$i -lt 45;$i++){
  $run=Call GET $path
  if($run.state -eq 'AwaitingReview'){return $run}
  if($run.state -in @('Failed','Blocked','Cancelled','Rejected')){throw "Workflow ended: $($run.state)/$($run.outcome)"}
  Start-Sleep -Seconds 1
 }
 throw 'Workflow review timeout.'
}
try{
 if($ManageApi){$ownedApi=Start-OwnedApi}
 $tenant=@(Call GET 'api/v1/core/tenants')|Where-Object key -eq 'm02-local'|Select-Object -First 1
 if(!$tenant){throw 'Provision the development database and Windows staff account.'}
 $universe=@(Call GET "api/v1/core/tenants/$($tenant.id)/universes")|Where-Object key -eq 'geeta'|Select-Object -First 1
 $catalog=Call GET "api/v1/core/tenants/$($tenant.id)/universes/$($universe.id)/catalog"
 $audience=@($catalog.manifest.audiences)|Select-Object -First 1
 if(!$audience){throw 'Existing universe audience missing.'}
 $base="api/v1/workflows/tenants/$($tenant.id)"
 $uBase="$base/universes/$($universe.id)"
 $aiBase="api/v1/ai/tenants/$($tenant.id)"
 $previousRoute=@(Call GET "$aiBase/routes")|Where-Object {$_.active -and $_.settings.task -eq 'draft'}|Select-Object -First 1
 $now=[DateTimeOffset]::UtcNow
 $budget=@(Call GET "$aiBase/budgets")|Where-Object {$_.currency -eq 'USD' -and $_.capability -eq 'ai.text' -and [DateTimeOffset]$_.startsUtc -le $now -and [DateTimeOffset]$_.endsUtc -gt $now}|Select-Object -First 1
 if(!$budget){$null=Call POST "$aiBase/budgets" @{capability='ai.text';currency='USD';startsUtc=$now.AddMinutes(-1).ToString('o');endsUtc=$now.AddHours(24).ToString('o');limit=.1}}
 $key='m08-'+[guid]::NewGuid().ToString('N')
 $steps=@(
  @{kind='retrieve';tool='evidence.retrieve.v1';maxAttempts=1},
  @{kind='plan';tool=$null;maxAttempts=1},
  @{kind='draft';tool='ai.generate.v1';maxAttempts=1},
  @{kind='check';tool=$null;maxAttempts=1},
  @{kind='review';tool=$null;maxAttempts=1})
 function Register([int]$version,[string]$adapter){
  $prompt=Call POST "$uBase/prompts" @{key=$key;version=$version;template="Original public fiction v$version about {{situation}}.";inputs=@(@{name='situation';maxLength=500;required=$true});locale='en-IN';audienceId=$audience.id;outputSchema='generated-text.v1';requireEvidence=$false}
  $model='fixture-a.v1';if($adapter -eq 'fixture.text.b'){$model='fixture-b.v1'}
  $profile=Call POST "$aiBase/profiles" @{settings=@{adapter=$adapter;model=$model;capability='ai.text';locale='en-IN';structured=$true;streaming=$false;tools=$false;maxInputBytes=32768;maxOutputTokens=128;pricing=@{version='fixture-zero.v1';currency='USD';inputPerMillion=0;cachedInputPerMillion=0;outputPerMillion=0};policy=@{classifications=@('Public');region='local';retentionDays=0;training=$false;policyVersion='owned-policy.v1'};qualification='OfflineVerified';evidenceReference='fixture:M08-original-fiction';scenario='success'};maxConcurrency=2}
  $route=Call POST "$aiBase/routes" @{task='draft';version="$key.v$version";profiles=@($profile);runLimit=.01;currency='USD';deadlineSeconds=20;structured=$true;streaming=$false;tools=$false;classification='Public';region='local';maxRetentionDays=0;allowTraining=$false;policyVersion='owned-policy.v1';promptVersion="$key.v$version"}
  $null=Call POST "$aiBase/routes/$route/activate" @{}
  $definition=Call POST "$uBase/definitions" @{key=$key;version=$version;promptId=$prompt;routeId=$route;steps=$steps;maxCost=.01;currency='USD';maxTokens=40000;deadlineSeconds=600;leaseSeconds=90;humanReview=$true}
  @{prompt=$prompt;route=$route;definition=$definition}
 }
 $first=Register 1 'fixture.text.a'
 $request=@{operationId=[guid]::NewGuid();definitionId=$first.definition;inputs=@{situation='a calm original conversation'}}
 $run=Call POST "$uBase/runs" $request
 $path="$base/runs/$($run.id)"
 $review=Wait-Review $path
 $aiId=$review.steps[2].aiRunId
 $before=Call GET "$aiBase/runs/$aiId"
 $second=Register 2 'fixture.text.b'
 $beforePid=$null;$afterPid=$null
 if($ManageApi){
  $beforePid=$ownedApi.Id;Stop-OwnedApi $ownedApi;$ownedApi=$null
  $ownedApi=Start-OwnedApi;$afterPid=$ownedApi.Id
 }
 $afterRestart=Call GET $path
 if($afterRestart.state -ne 'AwaitingReview' -or $afterRestart.snapshot.prompt.settings.version -ne 1 -or $afterRestart.snapshot.route.id -ne $first.route){throw 'Old checkpoint/prompt/route changed.'}
 $checkpoint=Call GET "$path/checkpoint"
 $completed=Call POST "$path/review" @{operationId=[guid]::NewGuid();expectedRowVersion=$afterRestart.rowVersion;evidenceReference='fixture:M08-owner-demonstration-review';approve=$true}
 $after=Call GET "$aiBase/runs/$aiId"
 if($completed.state -ne 'Completed' -or @($after.attempts).Count -ne @($before.attempts).Count -or @($after.attempts).Count -ne 1){throw 'Review repeated or lost the accepted AI effect.'}
 $next=Call POST "$uBase/runs" @{operationId=[guid]::NewGuid();definitionId=$second.definition;inputs=@{situation='a second original conversation'}}
 if($next.snapshot.prompt.settings.version -ne 2 -or $next.snapshot.route.id -ne $second.route){throw 'New run did not select updated definition.'}
 $next=Call GET "$base/runs/$($next.id)"
 $null=Call POST "$base/runs/$($next.id)/cancel" @{operationId=[guid]::NewGuid();expectedRowVersion=$next.rowVersion;evidenceReference='fixture:M08-demo-cleanup'}
 $staffResponse=Invoke-WebRequest "$BaseUri/staff/workflows" -UseDefaultCredentials -UseBasicParsing
 $evidence=@{milestone='M08';verifiedUtc=[DateTimeOffset]::UtcNow.ToString('o');run=$run.id;aiRun=$aiId;reviewState=$afterRestart.state;completedState=$completed.state;oldPromptVersion=1;newPromptVersion=2;oldRoute=$first.route;newRoute=$second.route;attemptsBefore=@($before.attempts).Count;attemptsAfter=@($after.attempts).Count;workerRestartVerified=[bool]$ManageApi;apiPidBefore=$beforePid;apiPidAfter=$afterPid;checkpointSchema=$checkpoint.snapshot.schema;staffHttp=$staffResponse.StatusCode;liveCalls=0;fixtureReviewNotPublication=$true}
 $file=Join-Path $repo 'docs\milestones\evidence\M08-demo.json'
 $evidence|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 $file
 $evidence|ConvertTo-Json -Depth 10
}finally{
 if($previousRoute -and $aiBase){try{$null=Call POST "$aiBase/routes/$($previousRoute.id)/activate" @{}}catch{Write-Warning 'Restore the prior draft route manually.'}}
 if($ownedApi){Stop-OwnedApi $ownedApi}
}
