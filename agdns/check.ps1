#requires -version 5.1
<#
    AGDNS - быстрая проверка состояния (права администратора не нужны).
#>
$ErrorActionPreference = 'SilentlyContinue'
$InstallDir = Join-Path $env:ProgramData 'AGDNS'

function Line($text, $color = 'Gray') { Write-Host $text -ForegroundColor $color }

Write-Host ''
Line '  AGDNS - проверка' White
Line '  ----------------' DarkGray

$python = (Get-Command python.exe -ErrorAction SilentlyContinue).Source
if (-not $python) {
    $python = Get-ChildItem "$env:LOCALAPPDATA\Programs\Python" -Filter python.exe -Recurse -Depth 2 -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
$script = Join-Path $InstallDir 'agdns.py'

$up = $false
$output = @()
if ($python -and (Test-Path $script)) {
    $output = & $python $script --selftest 53 2>&1
    $up = ($output.Count -gt 0) -and -not ($output -match '^FAIL')
}

if ($up) {
    Line '  Локальный DNS: отвечает на 127.0.0.1:53' Green
} else {
    Line '  Локальный DNS: НЕ отвечает (запустите install.cmd)' Red
}

$dnsText = (netsh interface ipv4 show dnsservers 2>&1 | Out-String)
if ($dnsText -match '127\.0\.0\.1') {
    Line '  DNS интерфейсов: 127.0.0.1' Green
} else {
    Line '  DNS интерфейсов: 127.0.0.1 не настроен (запустите install.cmd)' Yellow
}

$nrpt = @(Get-DnsClientNrptRule -ErrorAction SilentlyContinue |
    Where-Object { ($_.Namespace -join ',') -match 'googleapis' })
if ($nrpt.Count -gt 0) {
    foreach ($rule in $nrpt) {
        Line ("  NRPT: {0} -> {1}" -f ($rule.Namespace -join ','), ($rule.NameServers -join ', '))
    }
} elseif ($dnsText -match '127\.0\.0\.1') {
    Line '  NRPT: правил нет (работает обычный DNS)' DarkGray
}

Write-Host ''
Line '  Разрешение имён:' White
if ($output.Count -gt 0) { $output | ForEach-Object { Write-Host "  $_" } }
else { Line '  не удалось запустить проверку' Yellow }

Write-Host ''
Line '  Журнал: C:\ProgramData\AGDNS\agdns.log' DarkGray
Write-Host ''
if ($Host.Name -eq 'ConsoleHost') { Read-Host 'Нажмите Enter, чтобы закрыть' }
