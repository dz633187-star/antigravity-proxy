#requires -version 5.1
# Запуск Antigravity через нидерландский прокси (без анлокера в цепочке).
# Antigravity IDE и его language_server уважают HTTP_PROXY/HTTPS_PROXY/ALL_PROXY.
$ErrorActionPreference = 'SilentlyContinue'

$dir  = Join-Path $env:LOCALAPPDATA 'XrayNL'
$xray = Join-Path $dir 'xray.exe'
$cfg  = Join-Path $dir 'config.json'
$app  = Join-Path $env:LOCALAPPDATA 'Programs\antigravity\Antigravity.exe'

$proxy = 'http://127.0.0.1:10809'

function Test-Proxy {
    try {
        $c = New-Object System.Net.Sockets.TcpClient
        $r = $c.BeginConnect('127.0.0.1', 10809, $null, $null)
        $ok = $r.AsyncWaitHandle.WaitOne(1500)
        $connected = $ok -and $c.Connected
        $c.Close()
        return $connected
    } catch { return $false }
}

if (-not (Test-Proxy)) {
    Start-Process -FilePath $xray -ArgumentList @('run', '-c', $cfg) -WorkingDirectory $dir -WindowStyle Hidden
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        if (Test-Proxy) { break }
    }
}

if (-not (Test-Proxy)) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show('Не удалось запустить прокси Xray. Проверьте ' + $dir, 'Antigravity NL')
    exit 1
}

# Все переменные прокси — для Electron-оболочки и Go language_server
$env:AG_LS_PROXY = $proxy
$env:HTTP_PROXY  = $proxy
$env:HTTPS_PROXY = $proxy
$env:ALL_PROXY   = $proxy
$env:http_proxy  = $proxy
$env:https_proxy = $proxy
$env:all_proxy   = $proxy
$env:NO_PROXY    = 'localhost,127.0.0.1,::1'
$env:no_proxy    = 'localhost,127.0.0.1,::1'

Start-Process -FilePath $app
