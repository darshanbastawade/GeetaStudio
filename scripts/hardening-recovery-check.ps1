$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Data
$c=[Data.SqlClient.SqlConnection]::new('Server=localhost;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True')
$target='GeetaStudio_M25Restore_'+[Guid]::NewGuid().ToString('N')
if($target -notmatch '^GeetaStudio_M25Restore_[a-f0-9]{32}$'){throw 'Unsafe recovery target'}
$timer=[Diagnostics.Stopwatch]::StartNew();$created=$false;$backup=$null;$identity=$null
function Table($sql){$q=$c.CreateCommand();$q.CommandTimeout=120;$q.CommandText=$sql;$a=[Data.SqlClient.SqlDataAdapter]::new($q);$t=[Data.DataTable]::new();[void]$a.Fill($t);return ,$t}
function Execute($sql){$q=$c.CreateCommand();$q.CommandTimeout=120;$q.CommandText=$sql;[void]$q.ExecuteNonQuery()}
function Quote($s){return ([string]$s).Replace("'","''")}
try{
$c.Open()
if((Table "SELECT name FROM sys.databases WHERE name=N'$target'").Rows.Count){throw 'Recovery target already exists; refusing overwrite'}
$paths=(Table "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) AS BackupPath,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')) AS DataPath,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath')) AS LogPath").Rows[0]
if(-not $paths.BackupPath -or -not $paths.DataPath -or -not $paths.LogPath){throw 'SQL default recovery paths unavailable'}
$backupDirectory=Join-Path $repo '.local/m25-recovery'; New-Item -ItemType Directory -Force $backupDirectory|Out-Null
$backup=Join-Path $backupDirectory ($target+'.bak')
if(Test-Path -LiteralPath $backup){throw 'Unique backup file already exists'}
$b=Quote $backup
Execute "BACKUP DATABASE [GeetaStudio_M1Tests] TO DISK=N'$b' WITH COPY_ONLY,CHECKSUM"
Execute "RESTORE VERIFYONLY FROM DISK=N'$b' WITH CHECKSUM"
$files=Table "RESTORE FILELISTONLY FROM DISK=N'$b'"
$moves=@()
foreach($file in $files.Rows){
$logical=Quote $file.LogicalName
$extension=if($file.Type -eq 'L'){'.ldf'}else{'.mdf'}
$directory=if($file.Type -eq 'L'){$paths.LogPath}else{$paths.DataPath}
$destination=Join-Path ([string]$directory) ($target+'_'+$file.FileId+$extension)
# Unique generated restore name; no REPLACE is permitted.
$moves+="MOVE N'$logical' TO N'$(Quote $destination)'"
}
Execute "RESTORE DATABASE [$target] FROM DISK=N'$b' WITH CHECKSUM,$($moves -join ',')"
$created=$true;$identity=(Table "SELECT database_id,create_date FROM sys.databases WHERE name=N'$target'").Rows[0]
$checks=@{}
foreach($table in @('reader.ReadCollection','reader.ReadPiece','reader.ReadEvidence','reader.ReadOperation','editorial.EditorialApproval','sources.SourcePermission','customers.CustomerCredit','customers.CustomerReservation','customers.CustomerOrder','customers.CustomerPaymentEvent','commerce.CommerceOrder','commerce.CommerceEffect','[create].[PrivateCreation]','[create].[CreationRevision]','sources.ProvenanceEdge')){
$counts=(Table "SELECT (SELECT COUNT_BIG(*) FROM GeetaStudio_M1Tests.$table) AS BeforeCount,(SELECT COUNT_BIG(*) FROM [$target].$table) AS RestoredCount").Rows[0]
if($counts.BeforeCount -ne $counts.RestoredCount){throw "Restore count mismatch:$table"}
$checks[$table]=[long]$counts.RestoredCount
}
# Reader immutable bindings: compare all columns and existing permissions, not only counts.
foreach($table in @('reader.ReadPiece','sources.SourcePermission','customers.CustomerCredit','customers.CustomerReservation','customers.CustomerOrder','customers.CustomerPaymentEvent','commerce.CommerceOrder','commerce.CommerceEffect','[create].[PrivateCreation]','[create].[CreationRevision]','sources.ProvenanceEdge')){
$difference=Table "SELECT TOP(1) * FROM (SELECT * FROM GeetaStudio_M1Tests.$table EXCEPT SELECT * FROM [$target].$table) d"
if($difference.Rows.Count){throw "Restore content mismatch:$table"}
$difference=Table "SELECT TOP(1) * FROM (SELECT * FROM [$target].$table EXCEPT SELECT * FROM GeetaStudio_M1Tests.$table) d"
if($difference.Rows.Count){throw "Extra restored content:$table"}
}
# Forward-compatible operational schema drill runs only in the disposable restore.
Execute "USE [$target]; CREATE TABLE dbo.M25RecoveryProbe(Id int NOT NULL PRIMARY KEY,Value int NOT NULL); INSERT dbo.M25RecoveryProbe VALUES(1,7); BEGIN TRAN; UPDATE dbo.M25RecoveryProbe SET Value=99; ROLLBACK;"
if((Table "SELECT Value FROM [$target].dbo.M25RecoveryProbe WHERE Id=1").Rows[0].Value -ne 7){throw 'Transactional rollback did not preserve acknowledged probe'}
Execute "USE [$target]; ALTER TABLE dbo.M25RecoveryProbe ADD Revision int NOT NULL CONSTRAINT DF_M25RecoveryProbe DEFAULT 1;"
if((Table "SELECT Value,Revision FROM [$target].dbo.M25RecoveryProbe").Rows[0].Revision -ne 1){throw 'Additive forward fix failed'}
Execute "USE [$target]; DROP TABLE dbo.M25RecoveryProbe"
$timer.Stop()
@{elapsedSeconds=$timer.Elapsed.TotalSeconds;offlineRpoSeconds=0;rtoTargetSeconds=300;rtoMet=($timer.Elapsed.TotalSeconds -le 300);transactionRollback=$true;additiveForwardFix=$true;oldApplicationBinaryRollbackQualified=$false;date=(Get-Date -Format 'yyyy-MM-dd');copyOnlyChecksumBackup=$true;verifyOnly=$true;actualIsolatedRestore=$true;readBindingsExact=$true;sourcePermissionsExact=$true;restoredCounts=$checks;productionAssetRecoveryQualified=$false;humanReleaseEvidenceWritten=$false}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $repo 'docs/milestones/evidence/M25-recovery.json')
}finally{
if($created){
$c.ChangeDatabase('master')
$current=(Table "SELECT database_id,create_date FROM sys.databases WHERE name=N'$target'").Rows[0]
if($target -notmatch '^GeetaStudio_M25Restore_[a-f0-9]{32}$' -or $current.database_id -ne $identity.database_id -or $current.create_date -ne $identity.create_date){throw 'Temporary restore identity changed; refusing cleanup'}
Execute "ALTER DATABASE [$target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$target]"
}
$c.Dispose()
if($backup -and (Test-Path -LiteralPath $backup)){
$resolved=[IO.Path]::GetFullPath($backup);$expected=[IO.Path]::GetFullPath((Join-Path $backupDirectory ($target+'.bak')))
if($resolved -ne $expected -or [IO.Path]::GetFileName($resolved) -ne ($target+'.bak')){throw 'Unsafe unique backup cleanup path'}
Remove-Item -LiteralPath $resolved
}
}
Write-Host 'M25 actual copy-only backup/checksum, isolated restore, transaction rollback and additive forward fix passed. Existing databases unchanged.'