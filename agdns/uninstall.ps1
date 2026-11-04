#requires -version 5.1
<#
    AGDNS - удаление и откат настроек DNS.
    Запускать от имени администратора (скрипт сам запросит UAC).
#>
[CmdletBinding()]
param([switch]$KeepFiles)

$ErrorActionPreference = 'Stop'
$TaskName = 'AGDNS'
$WatchdogName = 'AGDNS-Watchdog'
$InstallDir = Join-Path $env:ProgramData 'AGDNS'

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    $psArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-NoExit', '-File', ('"{0}"' -f $PSCommandPath))
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $psArgs
    exit 0
}

Write-Host ''
Write-Host '  AGDNS - удаление' -ForegroundColor White
Write-Host ''

Write-Host '==> Останавливаю службу' -ForegroundColor Cyan
Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
Stop-ScheduledTask -TaskName $WatchdogName -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName $WatchdogName -Confirm:$false -ErrorAction SilentlyContinue

Get-CimInstance Win32_Process -Filter "Name='pythonw.exe' OR Name='python.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like '*agdns.py*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Write-Host '  OK' -ForegroundColor Green

Write-Host '==> Возвращаю прежние DNS-серверы' -ForegroundColor Cyan
$backupPath = Join-Path $InstallDir 'dns-backup.json'
if (Test-Path $backupPath) {
    $backup = Get-Content $backupPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in @($backup)) {
        $v4 = @($entry.v4)
        $hasLoopback = @($v4 | Where-Object { $_ -like '127.*' }).Count -gt 0
        if ($v4.Count -gt 0 -and -not $hasLoopback) {
            Set-DnsClientServerAddress -InterfaceIndex $entry.index -ServerAddresses $v4
        } else {
            # в копии оказался наш же 127.0.0.1 — возвращаем автоматические DNS
            Set-DnsClientServerAddress -InterfaceIndex $entry.index -ResetServerAddresses -ErrorAction SilentlyContinue
            $v4 = @('автоматически')
        }
        if ($entry.v6 -and @($entry.v6).Count -gt 0) {
            Set-DnsClientServerAddress -InterfaceIndex $entry.index -ServerAddresses @($entry.v6)
        }
        Write-Host ("  {0}: {1}" -f $entry.alias, ($v4 -join ', ')) -ForegroundColor Green
    }
} else {
    Write-Host '  резервная копия не найдена, ставлю DNS автоматически (DHCP)' -ForegroundColor Yellow
    foreach ($a in @(Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway })) {
        Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ResetServerAddresses -ErrorAction SilentlyContinue
    }
}

Write-Host '==> Убираю правила NRPT, ведущие на 127.0.0.1' -ForegroundColor Cyan
$removed = 0
foreach ($rule in @(Get-DnsClientNrptRule -ErrorAction SilentlyContinue)) {
    $ns = ($rule.Namespace -join ',')
    $servers = @($rule.NameServers)
    $ours = ($servers.Count -eq 1 -and $servers[0] -eq '127.0.0.1') -and
            ($ns -match 'googleapis\.com' -or $rule.DisplayName -like 'AGDNS*')
    if ($ours) {
        Remove-DnsClientNrptRule -Name $rule.Name -Force -ErrorAction SilentlyContinue
        Write-Host ("  удалено: {0}" -f $ns) -ForegroundColor Green
        $removed++
    }
}
if ($removed -eq 0) { Write-Host '  нечего удалять' -ForegroundColor Gray }
Write-Host '  AGUnlocker при следующем "Включить всё" поставит свои правила заново.' -ForegroundColor DarkGray

Clear-DnsClientCache -ErrorAction SilentlyContinue
Write-Host '  OK' -ForegroundColor Green

if (-not $KeepFiles -and (Test-Path $InstallDir)) {
    Write-Host '==> Удаляю файлы' -ForegroundColor Cyan
    Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host '  OK' -ForegroundColor Green
}

Write-Host ''
Write-Host '  Готово: DNS вернулся к прежним настройкам.' -ForegroundColor Green
Write-Host ''
if ($Host.Name -eq 'ConsoleHost') { Read-Host 'Нажмите Enter, чтобы закрыть' }
