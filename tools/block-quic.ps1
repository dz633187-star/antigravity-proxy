$ErrorActionPreference = 'Continue'
$out = Join-Path $env:LOCALAPPDATA 'XrayNL\quic.txt'
"" | Set-Content $out

$paths = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\antigravity\Antigravity.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\antigravity\resources\bin\language_server.exe')
)
foreach ($p in $paths) {
    if (-not (Test-Path $p)) { continue }
    $name = 'AGDNS-NoQUIC-' + (Split-Path $p -Leaf)
    Remove-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName $name -Direction Outbound -Program $p `
        -Protocol UDP -RemotePort 443 -RemoteAddress Any -Action Block -Profile Any | Out-Null
    "udp443 blocked: $p" | Add-Content $out
}

Get-Process Antigravity -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$env:AG_LS_PROXY = 'http://127.0.0.1:10809'
Start-Process -FilePath (Join-Path $env:LOCALAPPDATA 'Programs\antigravity\Antigravity.exe')
"launched" | Add-Content $out


