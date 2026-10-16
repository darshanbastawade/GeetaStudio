# Dedicated test-environment drill, not a production recovery qualification.
param([switch]$Quiesced)
$ErrorActionPreference = 'Stop'
if (-not $Quiesced) { throw 'Stop test writers and owned hosts, then explicitly supply -Quiesced.' }
$repo = Split-Path $PSScriptRoot -Parent
$source = 'GeetaStudio_M1Tests'
$target = 'GeetaStudio_P9Restore_' + [Guid]::NewGuid().ToString('N')
$root = [IO.Path]::GetFullPath((Join-Path $repo '.local/M25-3-prompt9/recovery'))
$owned = Join-Path $root $target
New-Item -ItemType Directory -Path $owned -ErrorAction Stop | Out-Null
$backup = Join-Path $owned ($target + '.bak')
$c = [Data.SqlClient.SqlConnection]::new('Server=localhost;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True')
$created = $false; $identity = $null; $result = $null
$timer = [Diagnostics.Stopwatch]::StartNew()
function Quote([string]$s) { return $s.Replace("'", "''") }
function Table([string]$sql) {
    $q = $c.CreateCommand(); $q.CommandTimeout = 120; $q.CommandText = $sql
    $a = [Data.SqlClient.SqlDataAdapter]::new($q); $t = [Data.DataTable]::new()
    try { [void]$a.Fill($t); return ,$t } finally { $a.Dispose(); $q.Dispose() }
}
function Execute([string]$sql) {
    $q = $c.CreateCommand(); $q.CommandTimeout = 120; $q.CommandText = $sql
    try { [void]$q.ExecuteNonQuery() } finally { $q.Dispose() }
}
try {
    $c.Open()
    if ((Table "SELECT name FROM sys.databases WHERE name=N'$target'").Rows.Count) { throw 'Restore name collision; refusing overwrite.' }
    $paths = (Table "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')) DataPath,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath')) LogPath").Rows[0]
    if (-not $paths.DataPath -or -not $paths.LogPath) { throw 'SQL default restore paths unavailable.' }
    # Only already-owned, explicitly identified Prompt7 fixture masters. Never revive old private text.
    $assets = @()
    foreach ($audience in @('child', 'youth')) {
        $sample = Get-Content (Join-Path $repo ".local/M25-2-prompt7/sample/$audience.json") -Raw | ConvertFrom-Json
        foreach ($artifact in $sample.watch.Production.Artifacts) {
            $id = [Guid]$artifact.StorageAssetId
            $row = (Table "SELECT a.Id,a.Length,a.Sha256,COALESCE(l.StoreKey,'primary') StoreKey FROM [$source].runtime.Assets a LEFT JOIN [$source].runtime.AssetLocations l ON l.AssetId=a.Id WHERE a.Id='$id'").Rows
            if ($row.Count -ne 1 -or $row[0].Sha256 -ne $artifact.Sha256 -or $row[0].Length -ne $artifact.Length) { throw "Fixture master metadata mismatch:$id" }
            $store = switch ($row[0].StoreKey) { 'primary' { '.local/test-assets' } 'watch' { '.local/watch-test-masters' } 'archive' { '.local/m16-archive' } default { throw 'Unconfigured original asset store; refusing guessed mapping.' } }
            $original = Join-Path (Join-Path $repo $store) ($id.ToString('N') + '.bin')
            if ((Get-Item -LiteralPath $original).Length -ne $artifact.Length -or (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne $artifact.Sha256) { throw "Original fixture master unavailable:$id" }
            $restored = Join-Path $owned ($id.ToString('N') + '.bin')
            Copy-Item -LiteralPath $original -Destination $restored -ErrorAction Stop
            $assets += [ordered]@{ id = $id.ToString(); length = [long]$artifact.Length; sha256 = [string]$artifact.Sha256; storeKey = [string]$row[0].StoreKey; original = $original; restored = $restored }
        }
    }
    if ($assets.Count -lt 2) { throw 'Representative media assets are required.' }
    Execute "BACKUP DATABASE [$source] TO DISK=N'$(Quote $backup)' WITH COPY_ONLY,CHECKSUM"
    Execute "RESTORE VERIFYONLY FROM DISK=N'$(Quote $backup)' WITH CHECKSUM"
    $files = Table "RESTORE FILELISTONLY FROM DISK=N'$(Quote $backup)'"
    $moves = @()
    foreach ($file in $files.Rows) {
        $directory = if ($file.Type -eq 'L') { [string]$paths.LogPath } else { [string]$paths.DataPath }
        $extension = if ($file.Type -eq 'L') { '.ldf' } else { '.mdf' }
        $destination = Join-Path $directory ($target + '_' + $file.FileId + $extension)
        $moves += "MOVE N'$(Quote $file.LogicalName)' TO N'$(Quote $destination)'"
    }
    Execute "RESTORE DATABASE [$target] FROM DISK=N'$(Quote $backup)' WITH CHECKSUM,$($moves -join ',')"
    $created = $true
    $identity = (Table "SELECT database_id,create_date FROM sys.databases WHERE name=N'$target'").Rows[0]
    $tables = @('core.Characters','core.CharacterRevisions','core.Cast','sources.SourceWork','sources.SourceEdition','sources.SourceSnapshot','sources.SourcePermission','sources.ProvenanceEdge','reader.ReadOperation','editorial.EditorialApproval','runtime.Operations','runtime.Reservations','customers.CustomerCredit','customers.CustomerReservation','customers.CustomerOrder','customers.CustomerPaymentEvent','commerce.CommerceOrder','commerce.CommerceEffect','media.MediaVersion','media.MediaGeneration','watch.WatchArtifact')
    $checks = [ordered]@{}
    foreach ($table in $tables) {
        # EXCEPT in both directions checks identities, provider bindings, amounts and redacted state, not only counts.
        $difference = Table "SELECT TOP(1) * FROM (SELECT * FROM [$source].$table EXCEPT SELECT * FROM [$target].$table) d"
        if ($difference.Rows.Count) { throw "Restored rows differ:$table" }
        $difference = Table "SELECT TOP(1) * FROM (SELECT * FROM [$target].$table EXCEPT SELECT * FROM [$source].$table) d"
        if ($difference.Rows.Count) { throw "Extra restored rows:$table" }
        $checks[$table] = [long](Table "SELECT COUNT_BIG(*) Records FROM [$target].$table").Rows[0].Records
    }
    foreach ($asset in $assets) {
        $row = (Table "SELECT Length,Sha256 FROM [$target].runtime.Assets WHERE Id='$($asset.id)'").Rows[0]
        if ((Get-Item -LiteralPath $asset.restored).Length -ne $row.Length -or (Get-FileHash -LiteralPath $asset.restored -Algorithm SHA256).Hash -ne $row.Sha256) { throw 'Restored asset fails actual restored SQL binding.' }
    }
    # Demonstrate corruption detection and recovery only on the disposable asset copy.
    $probe = $assets[0]
    [IO.File]::WriteAllBytes($probe.restored, [byte[]]@(0, 1, 2))
    if ((Get-FileHash -LiteralPath $probe.restored -Algorithm SHA256).Hash -eq $probe.sha256) { throw 'Corruption probe was not detected.' }
    Copy-Item -LiteralPath $probe.original -Destination $probe.restored -Force
    if ((Get-FileHash -LiteralPath $probe.restored -Algorithm SHA256).Hash -ne $probe.sha256) { throw 'Asset recovery failed.' }
    Execute "USE [$target]; CREATE TABLE dbo.P9RecoveryProbe(Id int NOT NULL PRIMARY KEY,Value int NOT NULL); INSERT dbo.P9RecoveryProbe VALUES(1,7); BEGIN TRAN; UPDATE dbo.P9RecoveryProbe SET Value=99; ROLLBACK;"
    if ((Table "SELECT Value FROM [$target].dbo.P9RecoveryProbe").Rows[0].Value -ne 7) { throw 'Transaction rollback failed.' }
    Execute "USE [$target]; ALTER TABLE dbo.P9RecoveryProbe ADD Revision int NOT NULL CONSTRAINT DF_P9RecoveryProbe DEFAULT 1;"
    if ((Table "SELECT Revision FROM [$target].dbo.P9RecoveryProbe").Rows[0].Revision -ne 1) { throw 'Forward fix failed.' }
    $timer.Stop()
    $result = [ordered]@{ observedUtc = [DateTimeOffset]::UtcNow.ToString('o'); status = 'Pass'; qualification = 'Local fixture SQL plus two-audience asset restore only'; sourceDatabase = $source; isolatedRestore = $target; copyOnlyChecksumBackup = $true; restoredCounts = $checks; exactRowsBothDirections = $true; restoredAssets = @($assets | ForEach-Object { @{ id = $_.id; length = $_.length; sha256 = $_.sha256; originalStore = $_.storeKey } }); corruptAssetDetectedAndRecovered = $true; transactionRollback = $true; additiveForwardFix = $true; elapsedSeconds = $timer.Elapsed.TotalSeconds; quiescedCopyRpoSeconds = 0; oldBinaryRollbackQualified = $false; productionOffsiteOrLogRecoveryQualified = $false; genuineReviewOrOperationalReceiptWritten = $false; sensitiveTextRestoredFromHistoricalBackup = $false; developmentDatabaseModified = $false }
} finally {
    if ($created) {
        # USE in the rollback/forward-fix probe changes this connection's current database.
        # Leave the disposable database before dropping it; a session cannot drop its own database.
        $c.ChangeDatabase('master')
        $current = (Table "SELECT database_id,create_date FROM sys.databases WHERE name=N'$target'").Rows[0]
        if ($target -notmatch '^GeetaStudio_P9Restore_[a-f0-9]{32}$' -or $current.database_id -ne $identity.database_id -or $current.create_date -ne $identity.create_date) { throw 'Restore identity changed; refusing cleanup.' }
        Execute "ALTER DATABASE [$target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$target]"
    }
    $c.Dispose()
    $resolved = [IO.Path]::GetFullPath($owned)
    if ($resolved -ne (Join-Path $root $target) -or -not $resolved.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $target -notmatch '^GeetaStudio_P9Restore_[a-f0-9]{32}$') { throw 'Unsafe disposable directory; refusing cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
if ($result) {
    $result.ownedRestoreAndAssetCopiesRemoved = $true
    [IO.File]::WriteAllText((Join-Path $repo 'docs/milestones/evidence/M25.3-prompt9-recovery.json'), ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Write-Host 'Local SQL-plus-asset restore and exact business binding checks passed; owned disposable resources removed.'
}
