$ErrorActionPreference='Stop';$repo=Split-Path $PSScriptRoot -Parent;Push-Location $repo
try{
$runtimes = & dotnet --list-runtimes
if (-not ($runtimes -match '^Microsoft.AspNetCore.App 10\.0\.13 ') -or -not ($runtimes -match '^Microsoft.NETCore.App 10\.0\.13 ')) { throw 'The qualified pilot requires installed shared runtimes 10.0.13; SDK pin remains 10.0.401.' }
& powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release-check.ps1
if($LASTEXITCODE -ne 0){throw 'Release checklist failed'}
$root=Join-Path $repo ('.local/releases/'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $root|Out-Null
foreach($name in @('Api','Web','Worker')){
& dotnet publish "src/GeetaStudio.$name/GeetaStudio.$name.csproj" --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false -c Release --self-contained false -o (Join-Path $root $name)
if($LASTEXITCODE -ne 0){throw "Publish failed:$name"}
}
Get-ChildItem -LiteralPath $root -File -Recurse|ForEach-Object{@{path=$_.FullName.Substring($root.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}|ConvertTo-Json|Set-Content (Join-Path $root 'manifest.json')
Write-Host "Private pilot binaries packaged:$root. No service installation or deployment performed."
}finally{Pop-Location}
