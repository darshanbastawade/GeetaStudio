param([switch]$Offline)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$toolRoot=Join-Path $root '.local/tools'
$archive=Join-Path $toolRoot 'ffmpeg-9.0.2.zip'
$expected='60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba'
New-Item -ItemType Directory -Path $toolRoot -Force|Out-Null
if(!(Test-Path -LiteralPath $archive)){if($Offline){throw 'Pinned archive missing. Run without -Offline to download it.'};Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip' -OutFile $archive -UseBasicParsing}
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){throw 'Pinned FFmpeg archive checksum mismatch; no extraction or execution performed.'}
$destination=Join-Path $toolRoot 'ffmpeg-9.0.2'
$bin=Join-Path $destination 'ffmpeg-9.0.2-essentials_build/bin'
if(!(Test-Path -LiteralPath (Join-Path $bin 'ffmpeg.exe'))){Expand-Archive -LiteralPath $archive -DestinationPath $destination}
$font=Join-Path $env:WINDIR 'Fonts/Nirmala.ttc'
if(!(Test-Path -LiteralPath $font)){throw 'Windows Nirmala font missing; install the Windows language/font feature before configuring WATCH.'}
$config=[ordered]@{Ffmpeg=(Join-Path $bin 'ffmpeg.exe');FfmpegSha256=(Get-FileHash (Join-Path $bin 'ffmpeg.exe')).Hash;Ffprobe=(Join-Path $bin 'ffprobe.exe');FfprobeSha256=(Get-FileHash (Join-Path $bin 'ffprobe.exe')).Hash;FontFile=$font;FontSha256=(Get-FileHash $font).Hash;SpoolRoot=(Join-Path $root '.local/watch-renderer')}
$config|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root '.local/M17-tools.json') -Encoding UTF8
& $config.Ffmpeg -version|Select-Object -First 1
if($LASTEXITCODE -ne 0){throw 'Pinned FFmpeg did not run.'}
Write-Host 'Configured private local WATCH tools. No system installation or public/paid provider enabled.'
