param([string]$BaseUri='http://localhost:5100',[string]$OutputPath)
$ErrorActionPreference='Stop'
$uri=[Uri]$BaseUri
if(!$uri.IsLoopback -or $uri.Scheme -ne 'http' -or $uri.Port -ne 5100){throw 'Use the local API on port 5100.'}
$staff=@{UseDefaultCredentials=$true;Headers=@{'X-ContentOS-Staff'='1'}}
if($PSVersionTable.PSVersion.Major -ge 7){$staff.AllowUnencryptedAuthentication=$true}
function Call([string]$Method,[string]$Path,$Body=$null){
 $request=@{Method=$Method;Uri="$BaseUri$Path"}
 if($null -ne $Body){$request.Body=[Text.Encoding]::UTF8.GetBytes(($Body|ConvertTo-Json -Depth 35 -Compress));$request.ContentType='application/json; charset=utf-8'}
 Invoke-RestMethod @staff @request
}
function Blocked([string]$Method,[string]$Path,$Body){
 try{$null=Call $Method $Path $Body;throw 'Incompatible embedding space unexpectedly accepted.'}
 catch{if([int]$_.Exception.Response.StatusCode -ne 422){throw};return 422}
}
$repo=Split-Path $PSScriptRoot -Parent
# Reuse preserved, independently licensed M04 evidence; its helper replays rather than replacing records.
$null=& "$PSScriptRoot/corpus-demo.ps1" -BaseUri $BaseUri -OutputPath (Join-Path $repo '.local/M06-corpus.json')
$corpus=Get-Content (Join-Path $repo '.local/M06-corpus.json') -Raw -Encoding UTF8|ConvertFrom-Json
$rows=Call GET '/api/v1/core/tenants';$tenant=@($rows)|Where-Object key -eq 'm02-local'|Select-Object -First 1
$rows=Call GET "/api/v1/core/tenants/$($tenant.id)/universes";$universe=@($rows)|Where-Object key -eq 'geeta'|Select-Object -First 1
if(!$tenant -or !$universe){throw 'Provision the current Windows account.'}
$k="/api/v1/knowledge/tenants/$($tenant.id)/universes/$($universe.id)"
$gold=Get-Content (Join-Path $repo 'docs/fixtures/M06-golden.json') -Raw -Encoding UTF8|ConvertFrom-Json
$text=$corpus.sampleVerse.texts|Where-Object kind -eq 'Translation'
if(!$text.id){throw 'Corpus text IDs are required for stable labeled evidence.'}
$run=[guid]::NewGuid().ToString('N')
$concept=Call POST "$k/concepts" @{operationId=[guid]::NewGuid();key="field-$run";snapshotId=$corpus.sourceSnapshots.translation;terms=@(@{locale='en-IN';text='field';kind='Name'},@{locale='en-IN';text='battlefield';kind='Alias'})}
$graph=Call GET "$k/graph";$c=$graph.concepts|Where-Object id -eq $concept
$null=Call POST "$k/graph/$concept/review" @{expectedRowVersion=$c.rowVersion;status='Approved';evidenceReference='fixture:M06-controlled-field-alias-projection-not-theological-review'}
function IndexRow($Id){$rows=Call GET "$k/indices";@($rows)|Where-Object id -eq $Id|Select-Object -First 1}
function Build([string]$Adapter,[string]$Model,[string]$Name){
 $request=@{operationId=[guid]::NewGuid();key="$Name-$run";manifestId=$corpus.manifestId;locale='en-IN';territory='IN';adapter=$Adapter;model=$Model}
 $id=Call POST "$k/indices" $request
 if((Call POST "$k/indices" $request) -ne $id){throw 'Queued index replay changed identity.'}
 $deadline=[DateTimeOffset]::UtcNow.AddMinutes(3)
 do{
  $index=IndexRow $id
  if($index.state -eq 'Failed'){throw "Index failed: $($index.error)"}
  if($index.state -eq 'Ready'){break}
  Start-Sleep -Milliseconds 500
 }while([DateTimeOffset]::UtcNow -lt $deadline)
 if($index.state -ne 'Ready'){throw 'Index did not become ready within three minutes; inspect the persisted queue.'}
 $questions=@()
 foreach($q in $gold.questions){$target=$null;if(!$q.noAnswer){$target=$text.id};$questions+=@{query=$q.query;expectedCorpusTextId=$target}}
 $null=Call POST "$k/indices/$id/validate" @{expectedRowVersion=$index.rowVersion;evidenceReference=$gold.reviewEvidence;questions=$questions}
 $index=IndexRow $id
 if(!$index.validationEvidence){throw 'Human-labeled shadow evaluation failed; alias unchanged.'}
 return $index
}
function Activate($Index,[string]$Version=''){
 $null=Call POST "$k/aliases" @{operationId=[guid]::NewGuid();alias="demo-$run";indexId=$Index.id;expectedRowVersion=$Version;queryEmbeddingSpaceId=$Index.embeddingSpaceId}
 $rows=Call GET "$k/aliases";@($rows)|Where-Object key -eq "demo-$run"|Select-Object -First 1
}
function Search($Index,[string]$Question){Call POST "$k/search" @{alias="demo-$run";query=$Question;locale='en-IN';territory='IN';embeddingSpaceId=$Index.embeddingSpaceId;limit=5}}
$first=Build 'sql-hybrid-v1' 'concept-projection-v1' 'primary'
$alias=Activate $first
$answer=Search $first $gold.questions[0].query
$none=Search $first $gold.questions[1].query
$semantic=Search $first 'battlefield'
if($none.evidence.Count -ne 0 -or !($answer.evidence|Where-Object corpusTextId -eq $text.id) -or !($semantic.evidence|Where-Object corpusTextId -eq $text.id)){throw 'Retrieval evidence/no-answer/semantic fixture failed.'}
$next=Build 'fixture-ranking-v1' 'concept-projection-v2' 'replacement'
if($first.embeddingSpaceId -eq $next.embeddingSpaceId -or $first.dimensions -ne $next.dimensions){throw 'Equal dimensions incorrectly identified model compatibility.'}
$mismatch=Blocked POST "$k/aliases" @{operationId=[guid]::NewGuid();alias=$alias.key;indexId=$next.id;expectedRowVersion=$alias.rowVersion;queryEmbeddingSpaceId=$first.embeddingSpaceId}
$alias=Activate $next $alias.rowVersion
$oldQuery=Blocked POST "$k/search" @{alias=$alias.key;query=$gold.questions[0].query;locale='en-IN';territory='IN';embeddingSpaceId=$first.embeddingSpaceId;limit=5}
$switched=Search $next $gold.questions[0].query
$expected=$answer.evidence|Where-Object corpusTextId -eq $text.id|Select-Object -First 1
$newExpected=$switched.evidence|Where-Object corpusTextId -eq $text.id|Select-Object -First 1
if($expected.chunkId -ne $newExpected.chunkId -or $expected.exactText -cne $newExpected.exactText){throw 'Retrieval adapter changed owned evidence identity.'}
$alias=Activate $first $alias.rowVersion
$rollback=Search $first $gold.questions[0].query
$result=[ordered]@{checkedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');first=$first;replacement=$next;known=$answer;noAnswer=$none;semantic=$semantic;incompatibleActivationStatus=$mismatch;staleQuerySpaceStatus=$oldQuery;adapterSwitch=$switched;rollback=$rollback;alias=$alias;goldenReview=$gold.reviewEvidence;paidCalls=0;externalPublications=0}
$json=$result|ConvertTo-Json -Depth 45
if($OutputPath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$json,[Text.UTF8Encoding]::new($false))}
[pscustomobject]@{firstIndex=$first.id;replacementIndex=$next.id;knownExpectedVerse=$true;noAnswer=$none.outcome;semanticExpectedVerse=$true;spaceMismatch=422;rollbackIndex=$rollback.indexId;paidCalls=0}|ConvertTo-Json
