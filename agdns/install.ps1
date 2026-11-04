#requires -version 5.1
<#
    AGDNS - установка локального DNS для Antigravity.
    Запускать от имени администратора (скрипт сам запросит UAC).
    Откат: uninstall.ps1
#>
[CmdletBinding()]
param(
    [switch]$Silent,
    [switch]$WithNrpt
)

$ErrorActionPreference = 'Stop'
$TaskName = 'AGDNS'
$WatchdogName = 'AGDNS-Watchdog'
$InstallDir = Join-Path $env:ProgramData 'AGDNS'
$SourceDir = $PSScriptRoot
$LogPath = Join-Path $InstallDir 'install.log'

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Ok($text)   { Write-Host "  OK  $text" -ForegroundColor Green }
function Write-Warn2($text){ Write-Host "  !!  $text" -ForegroundColor Yellow }
function Write-Bad($text)  { Write-Host "  XX  $text" -ForegroundColor Red }

if (-not (Test-Admin)) {
    $psArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass')
    if (-not $Silent) { $psArgs += '-NoExit' }
    $psArgs += @('-File', ('"{0}"' -f $PSCommandPath))
    if ($WithNrpt) { $psArgs += '-WithNrpt' }
    if ($Silent) { $psArgs += '-Silent' }
    Write-Host 'Нужны права администратора, открываю UAC...' -ForegroundColor Yellow
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $psArgs
    exit 0
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Start-Transcript -Path $LogPath -Force | Out-Null

try {
    Write-Host ''
    Write-Host '  AGDNS - локальный DNS для Antigravity' -ForegroundColor White
    Write-Host '  ------------------------------------' -ForegroundColor DarkGray
    Write-Host ''

    # --- Python -------------------------------------------------------------
    Write-Step 'Ищу Python'
    $candidates = New-Object System.Collections.Generic.List[string]
    $pw = Get-Command pythonw.exe -ErrorAction SilentlyContinue
    if ($pw) { $candidates.Add($pw.Source) }
    foreach ($root in @("$env:LOCALAPPDATA\Programs\Python", 'C:\', "$env:ProgramFiles", "${env:ProgramFiles(x86)}")) {
        if (-not (Test-Path $root)) { continue }
        Get-ChildItem -Path $root -Filter 'Python*' -Directory -ErrorAction SilentlyContinue |
            ForEach-Object {
                $exe = Join-Path $_.FullName 'pythonw.exe'
                if (Test-Path $exe) { $candidates.Add($exe) }
            }
    }
    $pyLauncher = Get-Command py.exe -ErrorAction SilentlyContinue
    if ($pyLauncher) {
        try {
            $exe = & $pyLauncher.Source -3 -c "import sys; print(sys.executable)" 2>$null
            if ($exe) { $candidates.Add(($exe.Trim() -replace 'python\.exe$', 'pythonw.exe')) }
        } catch { }
    }
    $pythonExe = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $pythonExe) {
        Write-Bad 'Python не найден. Установите Python 3 с python.org (галочка "Add to PATH").'
        throw 'python not found'
    }
    $pythonConsole = $pythonExe -replace 'pythonw\.exe$', 'python.exe'
    if (-not (Test-Path $pythonConsole)) { $pythonConsole = $pythonExe }
    Write-Ok $pythonExe

    # --- файлы --------------------------------------------------------------
    Write-Step "Устанавливаю в $InstallDir"
    $overrides = @{}
    $oldConfig = Join-Path $InstallDir 'config.json'
    if (Test-Path $oldConfig) {
        try {
            $old = Get-Content $oldConfig -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($old.overrides) {
                $old.overrides.PSObject.Properties | ForEach-Object { $overrides[$_.Name] = @($_.Value) }
            }
        } catch { }
    }
    foreach ($f in @('agdns.py', 'config.json', 'check.ps1')) {
        $src = Join-Path $SourceDir $f
        if (Test-Path $src) { Copy-Item $src $InstallDir -Force }
    }
    $scriptPath = Join-Path $InstallDir 'agdns.py'
    if (-not (Test-Path $scriptPath)) { throw "agdns.py не найден в $SourceDir" }

    # --- локальный DNS роутера (для внутренних имён) ------------------------
    $currentDns = @(Get-DnsClientServerAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.ServerAddresses } | ForEach-Object { $_.ServerAddresses }) |
        Where-Object { $_ -and $_ -notmatch '^(127\.|8\.8\.8\.8|8\.8\.4\.4|1\.1\.1\.1|1\.0\.0\.1|9\.9\.9\.9|94\.140\.)' } |
        Select-Object -Unique
    if (-not $currentDns) { $currentDns = @() }

    $config = Get-Content (Join-Path $InstallDir 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($currentDns.Count -gt 0) { $config.local_dns = @($currentDns) }
    $config.overrides = $overrides
    $config | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $InstallDir 'config.json') -Encoding UTF8
    Write-Ok ("локальный DNS для внутренних имён: " + (($currentDns -join ', ') -replace '^$', 'нет'))

    # --- задачи -------------------------------------------------------------
    Write-Step 'Регистрирую автозапуск'
    foreach ($name in @($TaskName, $WatchdogName)) {
        try { Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue } catch { }
        try { Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue } catch { }
    }

    Get-CimInstance Win32_Process -Filter "Name='pythonw.exe' OR Name='python.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like '*agdns.py*' -and $_.ProcessId -ne $PID } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

    $action = New-ScheduledTaskAction -Execute $pythonExe -Argument ('"{0}"' -f $scriptPath) -WorkingDirectory $InstallDir
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $principal = New-ScheduledTaskPrincipal -UserId ("{0}\{1}" -f $env:USERDOMAIN, $env:USERNAME) -LogonType Interactive -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -Hidden `
        -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -StartWhenAvailable
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
        -Principal $principal -Settings $settings -Force | Out-Null
    Write-Ok "задача $TaskName (запуск при входе в систему)"

    $watchdogPath = Join-Path $InstallDir 'watchdog.ps1'
    Copy-Item (Join-Path $SourceDir 'watchdog.ps1') $watchdogPath -Force

    $wdAction = New-ScheduledTaskAction -Execute 'powershell.exe' `
        -Argument ('-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}"' -f $watchdogPath)
    $wdTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(5) `
        -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration (New-TimeSpan -Days 3650)
    $wdPrincipal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $wdSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -MultipleInstances IgnoreNew -Hidden
    try {
        Register-ScheduledTask -TaskName $WatchdogName -Action $wdAction -Trigger $wdTrigger `
            -Principal $wdPrincipal -Settings $wdSettings -Force | Out-Null
        Write-Ok "сторож $WatchdogName (проверка каждые 5 минут)"
    } catch {
        Write-Warn2 ("сторож не создан: " + $_.Exception.Message)
    }

    # --- запуск сервера ДО смены DNS ---------------------------------------
    Write-Step 'Запускаю локальный DNS'
    Start-ScheduledTask -TaskName $TaskName

    $up = $false
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 700
        $out = & $pythonConsole $scriptPath --selftest 53 2>&1
        if ($out -and -not ($out -match '^FAIL')) { $up = $true; break }
    }
    if (-not $up) {
        Write-Bad 'локальный DNS не отвечает на 127.0.0.1:53'
        Write-Bad 'DNS НЕ переключён, интернет не тронут. Журнал:'
        Write-Bad $LogPath
        throw 'server did not start'
    }
    Write-Ok 'сервер отвечает (127.0.0.1:53)'

    # --- смена DNS ----------------------------------------------------------
    Write-Step 'Переключаю DNS на 127.0.0.1'
    $adapters = @(Get-NetIPConfiguration -ErrorAction SilentlyContinue |
        Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' })
    if ($adapters.Count -eq 0) { Write-Warn2 'не нашёл активный интерфейс с шлюзом' }

    $backup = @()
    foreach ($a in $adapters) {
        $idx = $a.InterfaceIndex
        $v4 = @((Get-DnsClientServerAddress -InterfaceIndex $idx -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses)
        $v6 = @((Get-DnsClientServerAddress -InterfaceIndex $idx -AddressFamily IPv6 -ErrorAction SilentlyContinue).ServerAddresses)
        $backup += [pscustomobject]@{ index = $idx; alias = $a.InterfaceAlias; v4 = $v4; v6 = $v6 }
        Set-DnsClientServerAddress -InterfaceIndex $idx -ServerAddresses '127.0.0.1'
        if ($v6.Count -gt 0) {
            Set-DnsClientServerAddress -InterfaceIndex $idx -ServerAddresses '::1' -ErrorAction SilentlyContinue
        }
        Write-Ok ("$($a.InterfaceAlias): было [$($v4 -join ', ')] -> 127.0.0.1")
    }
    $backupPath = Join-Path $InstallDir 'dns-backup.json'
    $keepBackup = $false
    if (Test-Path $backupPath) {
        try {
            $prev = @(Get-Content $backupPath -Raw -Encoding UTF8 | ConvertFrom-Json)
            if (@($prev | Where-Object { @($_.v4) | Where-Object { $_ -and $_ -notlike '127.*' } }).Count -gt 0) {
                $keepBackup = $true
            }
        } catch { }
    }
    if ($keepBackup) {
        Write-Ok 'сохраняю прежнюю резервную копию DNS (не перезаписываю)'
    } else {
        $backup | ConvertTo-Json -Depth 4 | Set-Content $backupPath -Encoding UTF8
    }

    # NRPT: правила Antigravity (их ставит AGUnlocker) тоже переводим на наш резолвер
    Write-Step 'Правила NRPT для доменов Antigravity'
    $agRules = @(Get-DnsClientNrptRule -ErrorAction SilentlyContinue |
        Where-Object { ($_.Namespace -join ',') -match 'cloudcode-pa\.googleapis\.com' })
    if ($agRules.Count -gt 0) {
        foreach ($rule in $agRules) {
            try {
                Set-DnsClientNrptRule -Name $rule.Name -NameServers '127.0.0.1' -ErrorAction Stop
                Write-Ok ("{0} -> 127.0.0.1" -f ($rule.Namespace -join ','))
            } catch {
                Write-Warn2 ("не удалось обновить {0}: {1}" -f ($rule.Namespace -join ','), $_.Exception.Message)
            }
        }
    } else {
        Write-Ok 'правил NRPT нет, работает обычный DNS'
        if ($WithNrpt) {
            foreach ($ns in @('cloudcode-pa.googleapis.com', 'daily-cloudcode-pa.googleapis.com')) {
                Add-DnsClientNrptRule -Namespace $ns -NameServers '127.0.0.1' -DisplayName "AGDNS $ns" | Out-Null
                Write-Ok ("добавлено правило {0}" -f $ns)
            }
        }
    }

    Clear-DnsClientCache -ErrorAction SilentlyContinue
    & ipconfig.exe /flushdns *> $null

    # --- проверка -----------------------------------------------------------
    Write-Step 'Проверка'
    Start-Sleep -Seconds 1
    $bad = 0
    foreach ($name in @('cloudcode-pa.googleapis.com', 'antigravity.google', 'ya.ru')) {
        $ips = @()
        try {
            $ips = @(Resolve-DnsName $name -Type A -DnsOnly -ErrorAction Stop |
                Where-Object { $_.IPAddress } | ForEach-Object { $_.IPAddress })
        } catch { }
        $loop = @($ips | Where-Object { $_ -like '127.*' })
        if ($ips.Count -eq 0 -or $loop.Count -gt 0) {
            Write-Bad ("{0}: {1}" -f $name, (($ips -join ', ') -replace '^$', 'нет ответа'))
            $bad++
        } else {
            Write-Ok ("{0}: {1}" -f $name, ($ips -join ', '))
        }
    }

    Write-Host ''
    if ($bad -eq 0) {
        Write-Host '  ГОТОВО: локальный DNS работает, подмена обходится.' -ForegroundColor Green
        Write-Host ''
        Write-Host '  Дальше:' -ForegroundColor White
        Write-Host '   1. Закройте Antigravity полностью (включая трей) и запустите снова.' -ForegroundColor Gray
        Write-Host '   2. Если используете AGUnlocker: "Настройки для опытных" -> "Свой DNS"' -ForegroundColor Gray
        Write-Host '      -> 127.0.0.1 -> "Включить всё" заново.' -ForegroundColor Gray
        Write-Host '   3. Проверка: check.cmd' -ForegroundColor Gray
    } else {
        Write-Warn2 'часть имён не разрешилась — смотрите журнал:'
        Write-Warn2 $LogPath
    }
    Write-Host ''
    Write-Host '  Откат: uninstall.cmd' -ForegroundColor DarkGray
    Write-Host ''
}
catch {
    Write-Host ''
    Write-Bad ("Ошибка: " + $_.Exception.Message)
    Write-Bad ("Журнал: " + $LogPath)
    try {
        $bk = Join-Path $InstallDir 'dns-backup.json'
        if (Test-Path $bk) {
            foreach ($entry in @(Get-Content $bk -Raw -Encoding UTF8 | ConvertFrom-Json)) {
                if (@($entry.v4).Count -gt 0) {
                    Set-DnsClientServerAddress -InterfaceIndex $entry.index -ServerAddresses @($entry.v4)
                }
            }
            Clear-DnsClientCache -ErrorAction SilentlyContinue
            Write-Warn2 ("DNS возвращён к прежним настройкам: " + (($backup | ForEach-Object { $_.v4 -join ',' }) -join '; '))
        }
    } catch { }
}
finally {
    try { Stop-Transcript | Out-Null } catch { }
}

if (-not $Silent -and $Host.Name -eq 'ConsoleHost') {
    Read-Host 'Нажмите Enter, чтобы закрыть'
}
