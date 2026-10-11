param([string]$BaseUri = 'http://localhost:5100')
$ErrorActionPreference = 'Stop'
$uri = [Uri]$BaseUri
if (!$uri.IsLoopback -or $uri.Scheme -ne 'http' -or $uri.Port -ne 5100) { throw 'Use the local API on port 5100.' }
$staff = @{ UseDefaultCredentials = $true; Headers = @{ 'X-ContentOS-Staff' = '1' } }
if ($PSVersionTable.PSVersion.Major -ge 7) { $staff.AllowUnencryptedAuthentication = $true }
function Invoke-Core([string]$Method, [string]$Path, $Body = $null) {
    $request = @{ Method = $Method; Uri = "$BaseUri/api/v1/core/$Path" }
    if ($null -ne $Body) { $request.Body = $Body | ConvertTo-Json -Depth 30; $request.ContentType = 'application/json' }
    Invoke-RestMethod @staff @request
}
$tenantResponse = Invoke-Core GET 'tenants'
$tenant = @($tenantResponse) | Where-Object key -eq 'm02-local' | Select-Object -First 1
if (!$tenant) { throw 'Run development provisioning/seeding as your Windows account first.' }
$universes = Invoke-Core GET "tenants/$($tenant.id)/universes"
$results = @()
foreach ($key in @('geeta','synthetic')) {
    $universe = $universes | Where-Object key -eq $key | Select-Object -First 1
    if (!$universe) { throw 'Expected seeded universe is unavailable.' }
    $prefix = "tenants/$($tenant.id)/universes/$($universe.id)"
    $catalog = Invoke-Core GET "$prefix/catalog"
    $request = @{ operationId = [guid]::NewGuid(); subjectId = $catalog.subjects[0].id; audienceId = $catalog.manifest.audiences[0].id
        localeId = 'en-IN'; title = 'M02 local demonstration'; body = 'Original synthetic example; no scripture or factual claim.'
        kind = 'Synthetic'; rightsStatus = 'Synthetic'; provenance = 'Local deterministic demonstration'; publicKnowledge = $false }
    $created = Invoke-Core POST "$prefix/content" $request
    $replayed = Invoke-Core POST "$prefix/content" $request
    if ($created.id -ne $replayed.id) { throw 'Operation replay changed the content identity.' }
    $revision = @{ operationId = [guid]::NewGuid(); expectedRowVersion = $created.rowVersion; title = 'M02 revised demonstration'
        body = 'An updated original synthetic example.'; kind = 'Synthetic'; rightsStatus = 'Synthetic'; provenance = 'Local fixture revision' }
    $updated = Invoke-Core POST "$prefix/content/$($created.id)/revisions" $revision
    if ($updated.currentVersion -ne 2 -or $updated.revisions[0].id -ne $created.revisions[0].id) { throw 'Revision history was not preserved.' }
    $revision.operationId = [guid]::NewGuid()
    try { $null = Invoke-Core POST "$prefix/content/$($created.id)/revisions" $revision; throw 'Stale revision unexpectedly succeeded.' }
    catch { if ([int]$_.Exception.Response.StatusCode -ne 409) { throw } }
    $export = Invoke-Core GET "$prefix/content/$($created.id)/export"
    if ($export.schemaVersion -ne 1 -or $export.content.revisions.Count -ne 2) { throw 'Export is inconsistent.' }
    $results += [pscustomobject]@{ universe = $key; contentId = $created.id; revisionCount = 2; replayPreservedId = $true; staleUpdate = 409 }
}
$hash = [Security.Cryptography.SHA256]::Create()
try { $bytes = $hash.ComputeHash([Text.Encoding]::UTF8.GetBytes('ContentOS.M02.fixture.v1:tenant:isolated')); $isolated = [guid]::new([byte[]]$bytes[0..15]) }
finally { $hash.Dispose() }
try { $null = Invoke-Core GET "tenants/$isolated/universes"; throw 'Cross-tenant read unexpectedly succeeded.' }
catch { if ([int]$_.Exception.Response.StatusCode -ne 403) { throw } }
[pscustomobject]@{ schemaVersion = 1; checkedAtUtc = [DateTimeOffset]::UtcNow.ToString('o'); universes = $results; crossTenantRead = 403; liveProviderCalls = 0 } | ConvertTo-Json -Depth 10
