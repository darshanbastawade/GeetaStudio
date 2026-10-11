param()
$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent
$file=Join-Path $repo '.local/M14-owned-hosts.json'
if(-not (Test-Path -LiteralPath $file)){Write-Host 'No retained M14 hosts';return}
$owned=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
foreach($name in @('api','web')){
$p=Get-Process -Id $owned.$name -ErrorAction SilentlyContinue;if(-not $p){continue}
$expected=[IO.Path]::GetFullPath((Join-Path $repo "src/GeetaStudio.$name/bin/Debug/net10.0/GeetaStudio.$name.exe"))
$stamp=$owned.($name+'Started')
if($p.Path -ne $expected -or $p.StartTime.ToUniversalTime().Ticks -ne [DateTimeOffset]::Parse($stamp).UtcDateTime.Ticks){throw 'Retained host identity changed; refusing to stop another process.'}
Stop-Process -Id $p.Id;$p.WaitForExit(10000)|Out-Null
}
Remove-Item -LiteralPath $file
Write-Host 'Only retained M14 hosts stopped.'