$ErrorActionPreference = 'Stop'
$failed = $false
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $version = & dotnet --version
        if ($LASTEXITCODE -ne 0 -or $version -notmatch '^10\.0\.4\d\d$') {
            Write-Host 'Install the stable .NET SDK compatible with global.json (exactly10.0.401; SDK roll-forward is disabled for reproducible package locks).'; $failed = $true
        } else { Write-Host ".NET SDK: $version" }
    } else { Write-Host '.NET SDK missing.'; $failed = $true }
    foreach ($tool in @('git', 'code')) {
        if (Get-Command $tool -ErrorAction SilentlyContinue) { Write-Host "$tool available." }
        else { Write-Host "$tool not on PATH (needed for source review / VS Code debugging)." }
    }
    $addresses = @([Net.IPAddress]::Loopback)
    if ([Net.Sockets.Socket]::OSSupportsIPv6) { $addresses += [Net.IPAddress]::IPv6Loopback }
    foreach ($port in @(5100, 5200)) {
        $available = $true
        foreach ($address in $addresses) {
            $listener = [Net.Sockets.TcpListener]::new($address, $port)
            try { $listener.Start() }
            catch { Write-Host "Loopback address ${address}:$port unavailable. Stop its owner or configure another port."; $available = $false; $failed = $true }
            finally { $listener.Stop() }
        }
        if ($available) { Write-Host "Loopback port $port available on all supported address families." }
    }
    Write-Host 'Runtime disabled needs no database. M1 SQL runtime requires scripts/db-update.ps1 setup and host secrets.'
    Write-Host 'The doctor does not exercise SQL access or F5; consult milestone evidence.'
} finally { Pop-Location }
if ($failed) { exit 1 }
exit 0
