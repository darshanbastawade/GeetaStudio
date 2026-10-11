$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $target = Join-Path (Get-Location) 'src/ContentOS.Domain/ContentOS.Domain.csproj'
    $original = [IO.File]::ReadAllBytes($target)
    try {
        [xml]$xml = Get-Content -LiteralPath $target -Raw
        $group = $xml.CreateElement('ItemGroup')
        $reference = $xml.CreateElement('PackageReference')
        $reference.SetAttribute('Include', 'Vendor.DeliberateM0Probe')
        $reference.SetAttribute('Version', '1.0.0')
        $group.AppendChild($reference) | Out-Null
        $xml.Project.AppendChild($group) | Out-Null
        $xml.Save($target)
        # Windows PowerShell 5 treats redirected native stderr as ErrorRecords.
        # Capture the deliberate failure, then restore normal terminating-error behavior.
        $savedPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $probeOutput = & dotnet test GeetaStudio.sln --no-build --no-restore --filter FullyQualifiedName~Repository_obeys_dependency_and_endpoint_boundaries 2>&1
            $probeExit = $LASTEXITCODE
        }
        finally { $ErrorActionPreference = $savedPreference }
        $probeOutput | ForEach-Object { Write-Host $_ }
        if ($probeExit -eq 0 -or ($probeOutput -join "`n") -notmatch 'forbidden dependency.*Vendor.DeliberateM0Probe') {
            throw 'Architecture gate did not report the expected deliberate vendor-dependency failure.'
        }
        Write-Host 'Expected failure observed: real Domain project vendor dependency detected without restoring/calling that vendor.'
    }
    finally { [IO.File]::WriteAllBytes($target, $original) }
    & dotnet test GeetaStudio.sln --no-build --no-restore --filter FullyQualifiedName~ArchitectureTests
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host 'Original Domain project restored byte-for-byte; architecture gate passes.'

} finally { Pop-Location }
