$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Data
$c=[Data.SqlClient.SqlConnection]::new('Server=localhost;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True')
$target='GeetaStudio_M14Restore_'+[Guid]::NewGuid().ToString('N')
if($target -notmatch '^GeetaStudio_M14Restore_[a-f0-9]{32}$'){throw 'Unsafe recovery target'}
$created=$false;$backup=$null;$identity=$null
function Table($sql){$q=$c.CreateCommand();$q.CommandTimeout=120;$q.CommandText=$sql;$a=[Data.SqlClient.SqlDataAdapter]::new($q);$t=[Data.DataTable]::new();[void]$a.Fill($t);return ,$t}
function Execute($sql){$q=$c.CreateCommand();$q.CommandTimeout=120;$q.CommandText=$sql;[void]$q.ExecuteNonQuery()}
function Quote($s){return ([string]$s).Replace("'","''")}
try{
$c.Open()
if((Table "SELECT name FROM sys.databases WHERE name=N'$target'").Rows.Count){throw 'Recovery target already exists; refusing overwrite'}
$paths=(Table "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) AS BackupPath,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')) AS DataPath,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath')) AS LogPath").Rows[0]
if(-not $paths.BackupPath -or -not $paths.DataPath -or -not $paths.LogPath){throw 'SQL default recovery paths unavailable'}
$backupDirectory=Join-Path $repo '.local/m14-recovery'; New-Item -ItemType Directory -Force $backupDirectory|Out-Null
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
foreach($table in @('reader.ReadCollection','reader.ReadPiece','reader.ReadEvidence','reader.ReadOperation','editorial.EditorialApproval','sources.SourcePermission')){
$counts=(Table "SELECT (SELECT COUNT_BIG(*) FROM GeetaStudio_M1Tests.$table) AS BeforeCount,(SELECT COUNT_BIG(*) FROM [$target].$table) AS RestoredCount").Rows[0]
if($counts.BeforeCount -ne $counts.RestoredCount){throw "Restore count mismatch:$table"}
$checks[$table]=[long]$counts.RestoredCount
}
# Reader immutable bindings: compare all columns and existing permissions, not only counts.
foreach($table in @('reader.ReadPiece','sources.SourcePermission')){
$difference=Table "SELECT TOP(1) * FROM (SELECT * FROM GeetaStudio_M1Tests.$table EXCEPT SELECT * FROM [$target].$table) d"
if($difference.Rows.Count){throw "Restore content mismatch:$table"}
}
@{date=(Get-Date -Format 'yyyy-MM-dd');copyOnlyChecksumBackup=$true;verifyOnly=$true;actualIsolatedRestore=$true;readBindingsExact=$true;sourcePermissionsExact=$true;restoredCounts=$checks;productionAssetRecoveryQualified=$false;humanReleaseEvidenceWritten=$false}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $repo 'docs/milestones/evidence/M14-recovery.json')
}finally{
if($created){
$current=(Table "SELECT database_id,create_date FROM sys.databases WHERE name=N'$target'").Rows[0]
if($target -notmatch '^GeetaStudio_M14Restore_[a-f0-9]{32}$' -or $current.database_id -ne $identity.database_id -or $current.create_date -ne $identity.create_date){throw 'Temporary restore identity changed; refusing cleanup'}
Execute "ALTER DATABASE [$target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$target]"
}
$c.Dispose()
if($backup -and (Test-Path -LiteralPath $backup)){
$resolved=[IO.Path]::GetFullPath($backup);$expected=[IO.Path]::GetFullPath((Join-Path $backupDirectory ($target+'.bak')))
if($resolved -ne $expected -or [IO.Path]::GetFileName($resolved) -ne ($target+'.bak')){throw 'Unsafe unique backup cleanup path'}
Remove-Item -LiteralPath $resolved
}
}
Write-Host 'Actual copy-only backup, checksum verification and isolated restore passed. Existing databases unchanged.'