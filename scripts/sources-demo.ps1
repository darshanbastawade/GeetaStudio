param([string]$BaseUri = 'http://localhost:5100', [string]$OutputPath)
$ErrorActionPreference = 'Stop'
$uri = [Uri]$BaseUri
if (!$uri.IsLoopback -or $uri.Scheme -ne 'http' -or $uri.Port -ne 5100) { throw 'Use the local API on port 5100.' }
$staff = @{ UseDefaultCredentials = $true; Headers = @{ 'X-ContentOS-Staff' = '1' } }
if ($PSVersionTable.PSVersion.Major -ge 7) { $staff.AllowUnencryptedAuthentication = $true }
function Invoke-Staff([string]$Method, [string]$Path, $Body = $null) {
    $request = @{ Method = $Method; Uri = "$BaseUri$Path" }
    if ($null -ne $Body) { $request.Body = $Body | ConvertTo-Json -Depth 40; $request.ContentType = 'application/json' }
    Invoke-RestMethod @staff @request
}
$tenantRows = Invoke-Staff GET '/api/v1/core/tenants'
$tenant = @($tenantRows) | Where-Object key -eq 'm02-local' | Select-Object -First 1
if (!$tenant) { throw 'Provision the development database as your Windows account first.' }
$universeRows = Invoke-Staff GET "/api/v1/core/tenants/$($tenant.id)/universes"
$universe = @($universeRows) | Where-Object key -eq 'synthetic' | Select-Object -First 1
$core = "/api/v1/core/tenants/$($tenant.id)/universes/$($universe.id)"
$base = "/api/v1/sources/tenants/$($tenant.id)/universes/$($universe.id)"
$work = Invoke-Staff POST "$base/works" @{ operationId = [guid]::NewGuid(); key = 'm03-demo-' + [guid]::NewGuid().ToString('N'); title = 'Original M03 local fixture'; kind = 'Synthetic' }
$edition = Invoke-Staff POST "$base/works/$work/editions" @{ operationId = [guid]::NewGuid(); key = 'original-v1'; author = 'Geeta Studio original fixture'; translator = $null; publisher = 'Local demo'; language = 'en-IN'; locator = 'docs/fixtures/M03-original.txt, lines 1-3' }
$permission = Invoke-Staff POST "$base/editions/$edition/permissions" @{
    operationId = [guid]::NewGuid(); evidenceReference = 'fixture:owned-original-M03-demonstration-only'
    languages = @('en-IN'); territories = @('IN'); formats = @('Text','Json')
    commercial = $true; adaptation = $true; translation = $true; aiProcessing = $true; snapshotRetention = $true
    requiredAttribution = 'Credit: Geeta Studio original M03 demonstration fixture. No third-party reuse permission is implied.'
}
$detail = Invoke-Staff GET "$base/works/$work"
$permissionRow = $detail.permissions | Where-Object id -eq $permission
$null = Invoke-Staff POST "$base/permissions/$permission/review" @{ expectedRowVersion = $permissionRow.rowVersion; status = 'Approved' }
$file = Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/fixtures/M03-original.txt'
$bytes = [IO.File]::ReadAllBytes($file)
$sha = [Security.Cryptography.SHA256]::Create()
try { $checksum = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '') } finally { $sha.Dispose() }
$import = @{ operationId = [guid]::NewGuid(); fileName = 'M03-original.txt'; content = [Convert]::ToBase64String($bytes); expectedSha256 = $checksum
    evidenceReference = 'fixture:owned-original-M03-file'; retainFullSnapshot = $true; retrievedUtc = [DateTimeOffset]::UtcNow.ToString('o') }
$snapshot = Invoke-Staff POST "$base/editions/$edition/files" $import
$replay = Invoke-Staff POST "$base/editions/$edition/files" $import
$import.operationId = [guid]::NewGuid()
$dedupe = Invoke-Staff POST "$base/editions/$edition/files" $import
if ($snapshot -ne $replay -or $snapshot -ne $dedupe) { throw 'Re-import changed the cited snapshot identity.' }
$detail = Invoke-Staff GET "$base/works/$work"
$snapshotRow = $detail.snapshots | Where-Object id -eq $snapshot
$null = Invoke-Staff POST "$base/snapshots/$snapshot/review" @{ expectedRowVersion = $snapshotRow.rowVersion; status = 'Approved' }
# A modern edition/translation starts with unknown rights, regardless of the original's permission.
$nextEdition = Invoke-Staff POST "$base/works/$work/editions" @{ operationId = [guid]::NewGuid(); key = 'translation-v2'; author = 'Geeta Studio original fixture'; translator = 'Synthetic demo translator'; publisher = 'Local demo'; language = 'hi-IN'; locator = 'Demo metadata only; no translated scripture' }
$import.operationId = [guid]::NewGuid(); $import.retainFullSnapshot = $false
$unknownSnapshot = Invoke-Staff POST "$base/editions/$nextEdition/files" $import
$unknown = Invoke-Staff POST "$base/snapshots/$unknownSnapshot/evaluate" @{ purpose = 'Publish'; language = 'hi-IN'; territory = 'IN'; format = 'Text'; commercial = $true }
if ($unknown.allowed -or $unknown.reason -ne 'UnknownRights') { throw 'Unknown edition rights unexpectedly allowed publication.' }
$catalog = Invoke-Staff GET "$core/catalog"
$content = Invoke-Staff POST "$core/content" @{ operationId = [guid]::NewGuid(); subjectId = $catalog.subjects[0].id; audienceId = $catalog.manifest.audiences[0].id
    localeId = 'en-IN'; title = 'M03 original derivative'; body = 'An original fixture story: a traveller shares tea with a neighbour.'
    kind = 'Synthetic'; rightsStatus = 'Synthetic'; provenance = 'M03 local source fixture'; publicKnowledge = $false }
$null = Invoke-Staff POST "$base/provenance" @{ operationId = [guid]::NewGuid(); snapshotId = $snapshot; parentRevisionId = $null; contentRevisionId = $content.revisions[0].id; assetReferenceId = $null; relation = 'based-on' }
$export = Invoke-Staff POST "$base/content/$($content.id)/export" @{ purpose = 'Export'; language = 'en-IN'; territory = 'IN'; format = 'Text'; commercial = $true }
if ($export.content.citations[0].snapshotId -ne $snapshot -or !$export.content.citations[0].requiredAttribution) { throw 'Attribution/evidence was lost during export.' }
$use = Invoke-Staff POST "$base/uses" @{ operationId = [guid]::NewGuid(); contentRevisionId = $content.revisions[0].id
    use = @{ purpose = 'Publish'; language = 'en-IN'; territory = 'IN'; format = 'Text'; commercial = $true }
    published = $true; externalReference = 'fixture:local-published-ledger-only'; mode = 'Fixture' }
$detail = Invoke-Staff GET "$base/works/$work"
$impact = Invoke-Staff POST "$base/works/$work/revoke" @{ operationId = [guid]::NewGuid(); editionId = $null; reason = 'M03 fixture takedown demonstration'; expectedRowVersion = $detail.work.rowVersion }
if (!($impact.derivatives | Where-Object contentRevisionId -eq $content.revisions[0].id) -or !($impact.remediation | Where-Object sourceUseId -eq $use)) { throw 'Revocation lost an impacted derivative or published remediation task.' }
try { $null = Invoke-Staff GET "$core/content/$($content.id)/export"; throw 'Revoked work unexpectedly exported.' }
catch { if ([int]$_.Exception.Response.StatusCode -ne 422) { throw } }
$denied = Invoke-Staff POST "$base/snapshots/$snapshot/evaluate" @{ purpose = 'Generate'; language = 'en-IN'; territory = 'IN'; format = 'Text'; aiProcessing = $true }
if ($denied.allowed -or $denied.reason -ne 'Revoked') { throw 'Revocation did not block new uses.' }
$result = [pscustomobject]@{ schemaVersion = 1; checkedAtUtc = [DateTimeOffset]::UtcNow.ToString('o'); tenantId = $tenant.id; universeId = $universe.id
    sourceWorkId = $work; snapshotId = $snapshot; reimportPreservedId = $true; editionCount = 2; unknownCommercialUse = $unknown.reason
    sampleExportBeforeRevocation = $export; revocation = $impact; defaultCoreExportAfterRevocation = 422; newUseAfterRevocation = $denied.reason
    liveProviderCalls = 0; actualPublications = 0; publicationMode = 'Fixture ledger only' }
$json = $result | ConvertTo-Json -Depth 40
if ($OutputPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json) }
$json
