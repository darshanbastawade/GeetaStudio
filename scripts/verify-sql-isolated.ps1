param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$savedSql=$env:GEETA_SQL_TESTS
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
 if(!$SkipBuild){
  & powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -Locked
  if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
 }
 & powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\db-update.ps1 -Database GeetaStudio_M1Tests
 if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
 $env:GEETA_SQL_TESTS='1'
 $listing=@(& dotnet test .\GeetaStudio.sln --no-build --no-restore --filter 'Category=Sql' --list-tests)
 if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
 $classes=@{}
 foreach($line in $listing){
  if($line -match '^\s*(ContentOS\.Foundation\.Tests\.([A-Za-z0-9_]+))\.[A-Za-z0-9_]+'){
   $class=$Matches[1];if(!$classes.ContainsKey($class)){$classes[$class]=0};$classes[$class]++
  }
 }
 if($classes.Count -eq 0){throw 'No SQL test classes discovered; refusing a vacuous pass.'}
 $results=Join-Path (Get-Location) '.local\verification\sql-isolated'
 New-Item -ItemType Directory -Path $results -Force|Out-Null
 $total=0;$expected=0;$evidence=@()
 foreach($class in @($classes.Keys|Sort-Object)){
  $expected += $classes[$class];$name=($class -split '\.')[-1]+'.trx'
  & dotnet test .\GeetaStudio.sln --no-build --no-restore --filter "Category=Sql&FullyQualifiedName~$class." --results-directory $results --logger "trx;LogFileName=$name"
  if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
  [xml]$trx=Get-Content -LiteralPath (Join-Path $results $name) -Raw
  $count=$trx.TestRun.ResultSummary.Counters
  if([int]$count.total -ne $classes[$class] -or [int]$count.passed -ne [int]$count.total -or [int]$count.failed -ne 0 -or [int]$count.notExecuted -ne 0){throw "Missing, failed or skipped SQL tests in $class"}
  $total += [int]$count.passed;$evidence += @{class=$class;discovered=$classes[$class];passed=[int]$count.passed;failed=[int]$count.failed;skipped=[int]$count.notExecuted}
 }
 if($total -ne $expected){throw 'SQL test discovery/execution totals differ.'}
 $summary=@{verifiedUtc=[DateTimeOffset]::UtcNow.ToString('o');mode='fresh-test-process-per-class';database='GeetaStudio_M1Tests';discovered=$expected;passed=$total;failed=0;skipped=0;classes=$evidence;assertionsAndTimeoutsUnchanged=$true;liveCalls=0}
 $summary|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 docs\milestones\evidence\M08-sql-isolated.json
 Write-Host "All $total discovered SQL tests passed in $($classes.Count) fresh class processes. This does not qualify the combined-process SQL run."
} finally { $env:GEETA_SQL_TESTS=$savedSql;Pop-Location }
