param([string]$BaseUri='http://localhost:5100',[string]$OutputPath)
$ErrorActionPreference='Stop'
$uri=[Uri]$BaseUri
if(!$uri.IsLoopback -or $uri.Scheme -ne 'http' -or $uri.Port -ne 5100){throw 'Use the local API on port 5100.'}
$staff=@{UseDefaultCredentials=$true;Headers=@{'X-ContentOS-Staff'='1'}}
if($PSVersionTable.PSVersion.Major -ge 7){$staff.AllowUnencryptedAuthentication=$true}
function Call([string]$Method,[string]$Path,$Body=$null){
    $request=@{Method=$Method;Uri="$BaseUri$Path"}
    if($null -ne $Body){$request.Body=[Text.Encoding]::UTF8.GetBytes(($Body|ConvertTo-Json -Depth 30 -Compress));$request.ContentType='application/json; charset=utf-8'}
    Invoke-RestMethod @staff @request
}
function Checksum([string]$Text){
    $sha=[Security.Cryptography.SHA256]::Create()
    try{[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))).Replace('-','')}finally{$sha.Dispose()}
}
function ExpectBlocked([string]$Method,[string]$Path,$Body=$null){
    try{$null=Call $Method $Path $Body;throw 'Evidence gate unexpectedly allowed the operation.'}
    catch{if([int]$_.Exception.Response.StatusCode -ne 422){throw};return 422}
}
$rows=Call GET '/api/v1/core/tenants'
$tenant=@($rows)|Where-Object key -eq 'm02-local'|Select-Object -First 1
$rows=Call GET "/api/v1/core/tenants/$($tenant.id)/universes"
$universe=@($rows)|Where-Object key -eq 'synthetic'|Select-Object -First 1
if(!$tenant -or !$universe){throw 'Run development provisioning as your Windows account.'}
$scope="tenants/$($tenant.id)/universes/$($universe.id)"
$source="/api/v1/sources/$scope";$e="/api/v1/evidence/$scope";$core="/api/v1/core/$scope"
$catalog=Call GET "$core/catalog";$run=[guid]::NewGuid().ToString('N')
$text=[IO.File]::ReadAllText((Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/fixtures/M05-original.txt'))
$work=Call POST "$source/works" @{operationId=[guid]::NewGuid();key="m05-original-$run";title='M05 original fictional evidence fixture';kind='Synthetic'}
$edition=Call POST "$source/works/$work/editions" @{operationId=[guid]::NewGuid();key='v1';author='ContentOS fixture author';publisher='ContentOS local demo';language='en-IN';locator='Original fixture lines; not scripture or historical reporting'}
$permission=Call POST "$source/editions/$edition/permissions" @{operationId=[guid]::NewGuid();evidenceReference='fixture:M05-owned-original-material-only';languages=@('en-IN');territories=@('IN');formats=@('Text','Json');commercial=$false;adaptation=$true;translation=$false;aiProcessing=$false;snapshotRetention=$true;requiredAttribution='ContentOS original fictional M05 fixture; no third-party text'}
$detail=Call GET "$source/works/$work";$p=$detail.permissions|Where-Object id -eq $permission
$null=Call POST "$source/permissions/$permission/review" @{expectedRowVersion=$p.rowVersion;status='Approved'}
$snapshot=Call POST "$source/editions/$edition/files" @{operationId=[guid]::NewGuid();fileName='M05-original.txt';content=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($text));expectedSha256=(Checksum $text);evidenceReference='fixture:M05-original-file';retainFullSnapshot=$true}
$detail=Call GET "$source/works/$work";$s=$detail.snapshots|Where-Object id -eq $snapshot
$null=Call POST "$source/snapshots/$snapshot/review" @{expectedRowVersion=$s.rowVersion;status='Approved'}
$rubric=Call POST "$e/rubrics" @{operationId=[guid]::NewGuid();key='m05-original-demo';version=1;description='Exact bound corpus for quotation. Attributed fictional-fixture viewpoints retain disagreement. History/news require independent exact evidence and manual assessment; model consensus is not verification.';minimumIndependentSources=2}
function Submit([string]$Key,[string]$Statement,[string]$Author,[string]$Type='Interpretation',[string]$RowVersion='',$Correction=$null){
    $uncertainty='Contested';if($Type -eq 'Quotation'){$uncertainty='SourceExact'}
    $body=@{operationId=[guid]::NewGuid();key=$Key;expectedRowVersion=$RowVersion;type=$Type;label=$Type;locale='en-IN';statement=$Statement;author=$Author;tradition='Original fictional fixture viewpoints';uncertainty=$uncertainty;rubricId=$rubric;evidence=@(@{snapshotId=$snapshot;start=$text.IndexOf($Statement,[StringComparison]::Ordinal);length=$Statement.Length;exactText=$Statement;sha256=(Checksum $Statement);locator='Original fixture line';relation='Supporting'})}
    if($Correction){$body.correctionId=$Correction}
    Call POST "$e/claims" $body
}
function ClaimRow($Revision){$rows=Call GET "$e/";@($rows)|Where-Object {$_.revisions.id -contains $Revision}|Select-Object -First 1}
function Review($Revision,[string]$Quality='Attributed',[string]$Outcome='Contested'){
    $head=ClaimRow $Revision;$r=$head.revisions|Where-Object id -eq $Revision
    Call POST "$e/revisions/$Revision/review" @{expectedRowVersion=$r.rowVersion;status='Approved';quality=$Quality;outcome=$Outcome;evidenceReference='fixture:M05-controlled-original-review'}
}
$a='Original viewpoint A: the traveller should share tea.'
$b='Original viewpoint B: the traveller should keep tea for later.'
$a2='Corrected original viewpoint A: sharing tea is an optional suggestion.'
$left=Submit "m05-A-$run" $a 'Original fixture critic A';$null=Review $left
$right=Submit "m05-B-$run" $b 'Original fixture critic B';$null=Review $right
$disagreement=Call POST "$e/disagreements" @{operationId=[guid]::NewGuid();leftRevisionId=$left;rightRevisionId=$right;reason='Both attributed original viewpoints retained; neither is canonical doctrine';evidenceReference='fixture:M05-opposing-viewpoints'}
# Authentic original evidence plus a plausible locator still cannot become a canonical quotation.
$invented=Submit "m05-invented-$run" $a 'Original fixture author' 'Quotation'
$r=(ClaimRow $invented).revisions|Where-Object id -eq $invented
$quoteStatus=ExpectBlocked POST "$e/revisions/$invented/review" @{expectedRowVersion=$r.rowVersion;status='Approved';quality='DirectSource';outcome='Supported';evidenceReference='fixture:M05-plausible-citation-is-not-scripture'}
$historical=Submit "m05-history-$run" $a 'Original fixture author' 'HistoricalClaim'
$r=(ClaimRow $historical).revisions|Where-Object id -eq $historical
$historyStatus=ExpectBlocked POST "$e/revisions/$historical/review" @{expectedRowVersion=$r.rowVersion;status='Approved';quality='Corroborated';outcome='Supported';evidenceReference='fixture:M05-one-source-is-insufficient'}
$contents=@()
foreach($pair in @(@{revision=$left;body=$a},@{revision=$right;body=$b})){
    $item=Call POST "$core/content" @{operationId=[guid]::NewGuid();subjectId=$catalog.subjects[0].id;audienceId=$catalog.manifest.audiences[0].id;localeId='en-IN';title='M05 original attributed interpretation';body=$pair.body;kind='Interpretation';rightsStatus='Licensed';provenance='Original fictional M05 fixture';publicKnowledge=$false}
    $null=Call POST "$e/links" @{operationId=[guid]::NewGuid();claimRevisionId=$pair.revision;contentRevisionId=$item.revisions[0].id;start=0;length=$pair.body.Length}
    $export=Call GET "$core/content/$($item.id)/export"
    if($export.evidenceLabels[0].label -ne 'Interpretation'){throw 'Export lost the interpretation label.'}
    $contents+=@{item=$item;export=$export}
}
$item=$contents[0].item
$updated=Call POST "$core/content/$($item.id)/revisions" @{operationId=[guid]::NewGuid();expectedRowVersion=$item.rowVersion;title='M05 title update, evidence retained';body=$a;kind='Interpretation';rightsStatus='Licensed';provenance='Original fixture'}
$before=Call POST "$e/content/$($updated.revisions[-1].id)/evaluate" @{purpose='Read';language='en-IN';territory='IN';format='Text'}
if(!$before.eligible){throw 'Reviewed attributed content was not eligible.'}
$correction=Call POST "$e/corrections" @{operationId=[guid]::NewGuid();claimRevisionId=$left;reason='Replace overly prescriptive original viewpoint with an optional suggestion';evidenceReference='fixture:M05-correction'}
$impact=Call GET "$e/revisions/$left/impact"
$blocked=Call POST "$e/content/$($updated.revisions[-1].id)/evaluate" @{purpose='Read';language='en-IN';territory='IN';format='Text'}
$exportStatus=ExpectBlocked GET "$core/content/$($item.id)/export"
$head=ClaimRow $left
$replacement=Submit $head.key $a2 'Original fixture critic A, corrected' 'Interpretation' $head.rowVersion $correction
$null=Review $replacement
$after=Call GET "$e/revisions/$left/impact";$history=ClaimRow $replacement
if($blocked.eligible -or $impact.contentRevisionIds.Count -ne 2 -or $history.currentVersion -ne 2 -or $after.corrections[0].status -ne 'Superseded'){throw 'Correction propagation failed.'}
$result=[ordered]@{checkedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');tenantId=$tenant.id;universeId=$universe.id;left=$left;right=$right;disagreement=$disagreement;inventedQuoteStatus=$quoteStatus;unsupportedHistoricalStatus=$historyStatus;before=$before;stale=$blocked;impact=$after;supersededHistory=$history;staleExportStatus=$exportStatus;rightExport=$contents[1].export;liveProviderCalls=0;externalPublications=0}
$json=$result|ConvertTo-Json -Depth 40
if($OutputPath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$json,[Text.UTF8Encoding]::new($false))}
$json
