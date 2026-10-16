# Read-only local inventory. This produces observations, never approval receipts.
param([string]$Output = 'docs/milestones/evidence/M25.3-prompt9-inventory.json')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$c = [Data.SqlClient.SqlConnection]::new('Server=localhost;Database=GeetaStudio_Dev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True')
function Rows([string]$sql) {
    $q = $c.CreateCommand(); $q.CommandTimeout = 60; $q.CommandText = $sql
    $reader = $q.ExecuteReader(); $rows = @()
    try {
        while ($reader.Read()) {
            $row = [ordered]@{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $value = $reader.GetValue($i)
                if ($value -is [DBNull]) { $value = $null }
                if ($value -is [byte[]]) { $value = [Convert]::ToBase64String($value) }
                $row[$reader.GetName($i)] = $value
            }
            $rows += $row
        }
    } finally { $reader.Dispose(); $q.Dispose() }
    return $rows
}
try {
    $c.Open()
    $result = [ordered]@{
        observedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        database = 'GeetaStudio_Dev'
        purpose = 'Existing local inventory; no owner-approved launch subset'
        launchTargets = @()
        manifests = @(Rows 'SELECT m.Id,m.TenantId,m.UniverseId,m.[Key],m.Version,m.Edition,m.MaterialKind,m.BaselineTarget,m.Fingerprint,COUNT(v.Id) ImportedVerses FROM corpus.CorpusManifest m LEFT JOIN corpus.CorpusVerse v ON v.ManifestId=m.Id GROUP BY m.Id,m.TenantId,m.UniverseId,m.[Key],m.Version,m.Edition,m.MaterialKind,m.BaselineTarget,m.Fingerprint ORDER BY m.Id')
        texts = @(Rows 'SELECT m.Id ManifestId,v.Id VerseId,v.Chapter,v.Verse,v.VariantKey,v.ReviewStatus,v.ReviewEvidence,t.Id TextId,t.Kind,t.Locale,t.ExactText,t.Sha256,t.SnapshotId,t.SourceLocator FROM corpus.CorpusManifest m JOIN corpus.CorpusVerse v ON v.ManifestId=m.Id JOIN corpus.CorpusText t ON t.VerseId=v.Id ORDER BY m.Id,v.Chapter,v.Verse,t.Kind,t.Locale')
        annotations = @(Rows 'SELECT a.Id,a.VerseId,a.Kind,a.Locale,a.Text,a.SnapshotId,a.ReviewStatus,a.ReviewEvidence FROM corpus.CorpusAnnotation a ORDER BY a.Id')
        rights = @(Rows 'SELECT w.Id WorkId,w.[Key],w.Title,w.Kind,w.Revoked WorkRevoked,e.Id EditionId,e.Language,e.Locator,e.CitationFormat,e.Revoked EditionRevoked,p.Id PermissionId,p.Version,p.Status,p.EvidenceReference,p.Commercial,p.Adaptation,p.Translation,p.AiProcessing,p.SnapshotRetention,p.RequiredAttribution,p.ExpiresUtc,p.ReviewedUtc FROM sources.SourceWork w JOIN sources.SourceEdition e ON e.WorkId=w.Id LEFT JOIN sources.SourcePermission p ON p.Id=e.PermissionId ORDER BY w.Id,e.Id')
        collections = @(Rows 'SELECT Id,TenantId,UniverseId,ManifestId,[Key],Version,Title,Mode,State,ExpectedPieces FROM reader.ReadCollection ORDER BY Id')
        evaluationDatasets = @(Rows "SELECT d.Id,d.TenantId,d.UniverseId,d.[Key],d.Version,JSON_VALUE(j.value,'$.Split') Split,JSON_VALUE(j.value,'$.Locale') Locale,JSON_VALUE(j.value,'$.AudienceId') AudienceId,COUNT(*) Cases FROM evaluation.EvaluationDataset d CROSS APPLY OPENJSON(d.SnapshotJson,'$.Cases') j GROUP BY d.Id,d.TenantId,d.UniverseId,d.[Key],d.Version,JSON_VALUE(j.value,'$.Split'),JSON_VALUE(j.value,'$.Locale'),JSON_VALUE(j.value,'$.AudienceId') ORDER BY d.Id,Split")
        evaluationReviewModes = @(Rows 'SELECT Mode,COUNT(*) Reviews FROM evaluation.EvaluationReview GROUP BY Mode')
        storedAssets = @(Rows 'SELECT COUNT_BIG(*) Assets,COALESCE(SUM(Length),0) Bytes FROM runtime.Assets')
        rightsQualification = 'Recorded permission data only; not a new legal, human or launch approval'
        genuineReviewsImported = 0
        writesToDatabase = 0
    }
    $path = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $repo $Output }
    [IO.File]::WriteAllText($path, ($result | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    Write-Host "Local inventory saved: $path; no launch approval granted."
} finally { $c.Dispose() }
