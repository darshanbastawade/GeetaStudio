$ErrorActionPreference = 'Stop'
$doctor = Join-Path $PSScriptRoot 'dev-doctor.ps1'
& powershell -NoProfile -ExecutionPolicy Bypass -File $doctor
if ($LASTEXITCODE -ne 0) { throw 'Begin this check with both documented host ports free.' }
$addresses = @([Net.IPAddress]::Loopback)
if ([Net.Sockets.Socket]::OSSupportsIPv6) { $addresses += [Net.IPAddress]::IPv6Loopback }
foreach ($port in @(5100, 5200)) {
    foreach ($address in $addresses) {
        $listener = [Net.Sockets.TcpListener]::new($address, $port)
        try {
            $listener.Start()
            & powershell -NoProfile -ExecutionPolicy Bypass -File $doctor
            if ($LASTEXITCODE -ne 1) { throw "Doctor missed ${address}:$port conflict." }
        } finally { $listener.Stop() }
    }
}
& powershell -NoProfile -ExecutionPolicy Bypass -File $doctor
if ($LASTEXITCODE -ne 0) { throw 'Doctor failed after releasing probe listeners.' }
Write-Host 'IPv4/IPv6 port conflicts return 1; free ports return 0; all probe listeners released.'
