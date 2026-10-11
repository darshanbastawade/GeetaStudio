param([string]$BaseUri = 'http://localhost:5100', [string]$OutputPath)
$ErrorActionPreference = 'Stop'
$uri = [Uri]$BaseUri
if (!$uri.IsLoopback -or $uri.Scheme -ne 'http' -or $uri.Port -ne 5100) { throw 'Use the local API on port 5100.' }
$staff = @{ UseDefaultCredentials=$true; Headers=@{'X-ContentOS-Staff'='1'} }
if ($PSVersionTable.PSVersion.Major -ge 7) { $staff.AllowUnencryptedAuthentication=$true }
function Invoke-Staff([string]$Method,[string]$Path,$Body=$null) {
    $request=@{Method=$Method;Uri="$BaseUri$Path"}
    if($null -ne $Body){$json=$Body | ConvertTo-Json -Depth 45 -Compress;$request.Body=[Text.Encoding]::UTF8.GetBytes($json);$request.ContentType='application/json; charset=utf-8'}
    Invoke-RestMethod @staff @request
}
function Checksum([byte[]]$Bytes) {
    $sha=[Security.Cryptography.SHA256]::Create()
    try{[BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-','')}finally{$sha.Dispose()}
}
$rows=Invoke-Staff GET '/api/v1/core/tenants'
$tenant=@($rows)|Where-Object key -eq 'm02-local'|Select-Object -First 1
if(!$tenant){throw 'Run development provisioning as your Windows account.'}
$rows=Invoke-Staff GET "/api/v1/core/tenants/$($tenant.id)/universes"
$universe=@($rows)|Where-Object key -eq 'geeta'|Select-Object -First 1
$source="/api/v1/sources/tenants/$($tenant.id)/universes/$($universe.id)"
$corpus="/api/v1/corpus/tenants/$($tenant.id)/universes/$($universe.id)"
$fixture=Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/fixtures/M04'
$sourceIds=@{}
foreach($kind in @('sanskrit','translation')){
    $locale='sa-IN';$classification='Canonical';$translator=$null
    if($kind -eq 'translation'){$locale='en-IN';$classification='Translation';$translator='Annie Besant'}
    $work=Invoke-Staff POST "$source/works" @{operationId=[guid]::NewGuid();key="m04-besant1922-$kind";title="Bhagavad-Gita 1922 fourth edition, $kind Wikisource sample";kind=$classification}
    $edition=Invoke-Staff POST "$source/works/$work/editions" @{operationId=[guid]::NewGuid();key='wikisource-9204505-v1';author='Traditional Sanskrit text';translator=$translator;publisher='G. A. Natesan & Co.; Wikisource transcription';language=$locale;locator='Discourse 1, verses 1-25; Wikisource revision 9204505';citationFormat='{author}. {title}. Translator: {translator}. {edition}. {publisher}. {locator}'}
    $detail=Invoke-Staff GET "$source/works/$work"
    $editionRow=$detail.editions|Where-Object id -eq $edition
    if(!$editionRow.permissionId){
        $permission=Invoke-Staff POST "$source/editions/$edition/permissions" @{operationId=[guid]::NewGuid();evidenceReference='evidence:M04-CC-BY-SA-4.0-source-and-license-files';languages=@($locale);territories=@('IN');formats=@('Text');commercial=$false;adaptation=$false;translation=$false;aiProcessing=$false;snapshotRetention=$true;requiredAttribution='Annie Besant, 1922; G. A. Natesan & Co.; Wikisource contributors. CC BY-SA 4.0: https://creativecommons.org/licenses/by-sa/4.0/ ; source https://en.wikisource.org/w/index.php?title=Bhagavad-Gita_(Besant_4th)/Discourse_1&oldid=9204505 . Retain attribution and ShareAlike; staff READ only.'}
        $detail=Invoke-Staff GET "$source/works/$work"
        $p=$detail.permissions|Where-Object id -eq $permission
        $null=Invoke-Staff POST "$source/permissions/$permission/review" @{expectedRowVersion=$p.rowVersion;status='Approved'}
    }
    $bytes=[IO.File]::ReadAllBytes((Join-Path $fixture "$kind-sample.txt"))
    $snapshot=Invoke-Staff POST "$source/editions/$edition/files" @{operationId=[guid]::NewGuid();fileName="$kind-sample.txt";content=[Convert]::ToBase64String($bytes);expectedSha256=(Checksum $bytes);evidenceReference='evidence:M04-source-paragraphs-revision-9204505';retainFullSnapshot=$true}
    $detail=Invoke-Staff GET "$source/works/$work";$s=$detail.snapshots|Where-Object id -eq $snapshot
    if($s.reviewStatus -eq 'Pending'){$null=Invoke-Staff POST "$source/snapshots/$snapshot/review" @{expectedRowVersion=$s.rowVersion;status='Approved'}}
    $sourceIds[$kind]=$snapshot
}
$manifest=Get-Content (Join-Path $fixture 'manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
$manifest|Add-Member operationId ([guid]::NewGuid())

$data=Get-Content (Join-Path $fixture 'sample.json') -Raw -Encoding UTF8|ConvertFrom-Json
$verses=@()
foreach($row in $data){
    # Verify source markers and exact text hashes before submitting review evidence.
    if((Checksum ([Text.Encoding]::UTF8.GetBytes($row.sanskrit))) -ne $row.sanskritHash -or (Checksum ([Text.Encoding]::UTF8.GetBytes($row.translation))) -ne $row.translationHash){throw 'Sample source hashes changed.'}
    $verses+=@{chapter=$row.chapter;verse=$row.verse;variantKey=$row.variantKey;texts=@(
        @{kind='DirectQuotation';locale='sa-IN';exactText=$row.sanskrit;sha256=$row.sanskritHash;snapshotId=$sourceIds.sanskrit;sourceLocator=$row.sourceLocator},
        @{kind='Translation';locale='en-IN';exactText=$row.translation;sha256=$row.translationHash;snapshotId=$sourceIds.translation;sourceLocator=$row.sourceLocator}
    )}
}
$manifest.version=2
$bindings=@()
foreach($verse in $verses){foreach($text in $verse.texts){$bindings+=@{chapter=$verse.chapter;verse=$verse.verse;variantKey=$verse.variantKey;kind=$text.kind;locale=$text.locale;snapshotId=$text.snapshotId;sourceChapter=$verse.chapter;sourceVerse=$verse.verse;sha256=$text.sha256;sourceLocator=$text.sourceLocator}}}
$manifest|Add-Member bindings $bindings
$id=Invoke-Staff POST "$corpus/manifests" $manifest
$import=@{operationId=[guid]::NewGuid();verses=$verses}
$first=Invoke-Staff POST "$corpus/manifests/$id/imports" $import
$second=Invoke-Staff POST "$corpus/manifests/$id/imports" $import
if($second.added -ne 0 -or $second.unchanged -ne 25){throw 'Repeated import was not idempotent.'}
$chapter=Invoke-Staff GET "$corpus/manifests/$id/chapters/1"
foreach($verse in $chapter){
    if($verse.reviewStatus -eq 'Pending'){
        $null=Invoke-Staff POST "$corpus/manifests/$id/verses/$($verse.id)/review" @{expectedRowVersion=$verse.rowVersion;status='Approved';evidenceReference='evidence:M04-25-source-hash-and-locator-checks-not-manuscript-review'}
    }
}
$report=Invoke-Staff GET "$corpus/manifests/$id/report"
$chapter=Invoke-Staff GET "$corpus/manifests/$id/chapters/1"
if($report.manifest.baselineTarget -ne 700 -or $report.manifest.expectedCount -ne 701 -or $report.imported -ne 25 -or $report.missing -ne 676 -or $report.approvedReadable -ne 25){throw 'Corpus inventory reconciliation failed.'}
$firstVerse=@($chapter)|Where-Object verse -eq 1|Select-Object -First 1
$quote=$firstVerse.texts|Where-Object kind -eq 'DirectQuotation'
$translation=$firstVerse.texts|Where-Object kind -eq 'Translation'
if($quote.sha256 -ne $data[0].sanskritHash -or $quote.exactText -cne $data[0].sanskrit -or $translation.exactText -cne $data[0].translation){throw 'Reader altered exact source text.'}
$result=[pscustomobject]@{checkedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');manifestId=$id;sourceSnapshots=$sourceIds;importReplay=$second;report=$report;sampleVerse=$firstVerse;aiProviderCalls=0;actualPublications=0;reviewScope='Automated source hash/locator verification; no new manuscript or interpretation review'}
$json=$result|ConvertTo-Json -Depth 45 -Compress
if($OutputPath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$json)}
[pscustomobject]@{manifestId=$id;baseline=700;editionInventory=701;imported=25;approvedReadable=25;missing=676;restricted=$report.restricted;reader='http://localhost:5100/staff/read';aiCalls=0}|ConvertTo-Json

