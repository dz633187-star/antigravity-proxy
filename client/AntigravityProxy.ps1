#requires -version 5.1
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$Dir = Join-Path $env:LOCALAPPDATA 'XrayNL'
$Cfg = Join-Path $Dir 'config.json'
$Exe = Join-Path $Dir 'xray.exe'
$AppRoot = Join-Path $env:LOCALAPPDATA 'AntigravityProxy'
$BrowserProfile = Join-Path $AppRoot 'BrowserProfile'

$script:Bg = [System.Drawing.Color]::FromArgb(246, 247, 249)
$script:Card = [System.Drawing.Color]::White
$script:Ink = [System.Drawing.Color]::FromArgb(24, 32, 44)
$script:Muted = [System.Drawing.Color]::FromArgb(110, 120, 134)
$script:Accent = [System.Drawing.Color]::FromArgb(37, 99, 235)
$script:AccentHover = [System.Drawing.Color]::FromArgb(29, 78, 216)
$script:Ok = [System.Drawing.Color]::FromArgb(21, 128, 61)
$script:Warn = [System.Drawing.Color]::FromArgb(180, 83, 9)
$script:Err = [System.Drawing.Color]::FromArgb(185, 28, 28)

function Test-Port {
    try {
        $c = New-Object System.Net.Sockets.TcpClient
        $r = $c.BeginConnect('127.0.0.1', 10809, $null, $null)
        $ok = $r.AsyncWaitHandle.WaitOne(500)
        $res = $ok -and $c.Connected
        $c.Close()
        return $res
    } catch { return $false }
}

function Find-Antigravity {
    $result = @{ App = ''; Dir = ''; LanguageServer = ''; Agy = '' }
    $cands = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\antigravity\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity 2.0\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity IDE\Antigravity IDE.exe'),
        (Join-Path $env:ProgramFiles 'Antigravity\Antigravity.exe')
    )
    foreach ($c in $cands) { if (Test-Path $c) { $result.App = $c; break } }
    if (-not $result.App) {
        foreach ($key in @(
            'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*')) {
            $items = Get-ItemProperty $key -ErrorAction SilentlyContinue |
                Where-Object { $_.DisplayName -match 'Antigravity' } | Select-Object -First 1
            if ($items -and $items.InstallLocation) {
                $exe = Join-Path $items.InstallLocation 'Antigravity.exe'
                if (Test-Path $exe) { $result.App = $exe; break }
            }
        }
    }
    if ($result.App) {
        $result.Dir = Split-Path $result.App
        $ls = Get-ChildItem (Join-Path $result.Dir 'resources') -Recurse -Filter 'language_server.exe' -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ($ls) { $result.LanguageServer = $ls }
    }
    $agy = Join-Path $env:LOCALAPPDATA 'agy\bin\agy.exe'
    if (Test-Path $agy) { $result.Agy = $agy }
    return $result
}

function Find-Browser {
    foreach ($c in @(
        'C:\Program Files\Google\Chrome\Application\chrome.exe',
        'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
        (Join-Path $env:LOCALAPPDATA 'Google\Chrome\Application\chrome.exe'),
        'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
        'C:\Program Files\Microsoft\Edge\Application\msedge.exe')) {
        if (Test-Path $c) { return $c }
    }
    return ''
}

function Find-OpenCode {
    foreach ($c in @(
        (Join-Path $env:LOCALAPPDATA 'Programs\@opencode-aidesktop\OpenCode.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\OpenCode\OpenCode.exe'))) {
        if (Test-Path $c) { return $c }
    }
    return ''
}

function Test-Patched([string]$path) {
    if (-not $path -or -not (Test-Path $path)) { return $null }
    try {
        $fs = [System.IO.File]::OpenRead($path)
        $buf = New-Object byte[] (4MB)
        $carry = ''
        while (($n = $fs.Read($buf, 0, $buf.Length)) -gt 0) {
            $chunk = $carry + [System.Text.Encoding]::ASCII.GetString($buf, 0, $n)
            if ($chunk.Contains('inexigible')) { $fs.Close(); return $true }
            if ($chunk.Contains('ineligible')) { $fs.Close(); return $false }
            $carry = if ($chunk.Length -gt 32) { $chunk.Substring($chunk.Length - 32) } else { $chunk }
        }
        $fs.Close()
        return $false
    } catch { return $null }
}

function Find-Patcher {
    foreach ($d in @((Join-Path $env:USERPROFILE 'Downloads'), $PSScriptRoot)) {
        $f = Get-ChildItem $d -Filter 'Open_AG_Patcher*.exe' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($f) { return $f.FullName }
    }
    return ''
}

function Get-ProxyInfo {
    try {
        $j = Get-Content $Cfg -Raw -Encoding UTF8 | ConvertFrom-Json
        $o = $j.outbounds | Where-Object { $_.protocol -eq 'http' } | Select-Object -First 1
        if ($o) {
            $s = $o.settings.servers[0]
            $u = ''; $p = ''
            if ($s.users -and $s.users.Count -gt 0) { $u = $s.users[0].user; $p = $s.users[0].pass }
            return @{ host = [string]$s.address; port = [string]$s.port; user = [string]$u; pass = [string]$p }
        }
    } catch { }
    return $null
}

function Write-ProxyConfig([string]$h, [string]$pt, [string]$u, [string]$pw) {
    $users = @()
    if ($u) { $users = @([ordered]@{ user = $u; pass = $pw }) }
    $server = [ordered]@{ address = $h; port = [int]$pt; users = $users }
    $config = [ordered]@{
        log = @{ loglevel = 'warning' }
        inbounds = @(
            [ordered]@{ tag = 'socks'; listen = '127.0.0.1'; port = 10808; protocol = 'socks'; settings = @{ udp = $true } },
            [ordered]@{ tag = 'http'; listen = '127.0.0.1'; port = 10809; protocol = 'http' }
        )
        outbounds = @(
            [ordered]@{ tag = 'proxy'; protocol = 'http'; settings = @{ servers = @($server) } },
            @{ tag = 'direct'; protocol = 'freedom' }
        )
    }
    $json = $config | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($Cfg, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Restart-Xray {
    Get-Process xray -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
    Start-Process -FilePath $Exe -ArgumentList @('run', '-c', $Cfg) -WorkingDirectory $Dir -WindowStyle Hidden
    for ($i = 0; $i -lt 25; $i++) {
        Start-Sleep -Milliseconds 300
        if (Test-Port) { return $true }
    }
    return $false
}

function Get-BypassList {
    return ('<local>;localhost;127.0.0.1;::1;' + $env:COMPUTERNAME + ';*.' + $env:USERDNSDOMAIN)
}

function Set-SystemProxy([bool]$on) {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
    if ($on) {
        Set-ItemProperty -Path $key -Name ProxyEnable -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $key -Name ProxyServer -Value '127.0.0.1:10809' -Type String -Force
        Set-ItemProperty -Path $key -Name ProxyOverride -Value (Get-BypassList) -Type String -Force
    } else {
        Set-ItemProperty -Path $key -Name ProxyEnable -Value 0 -Type DWord -Force
    }
}

function Test-SystemProxy {
    $p = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -ErrorAction SilentlyContinue
    return ($p.ProxyEnable -eq 1 -and $p.ProxyServer -like '*10809*')
}

$ag = Find-Antigravity
$patcher = Find-Patcher
$browser = Find-Browser
$opencode = Find-OpenCode

# ---------- оформление ----------
function New-Label([string]$text, [int]$x, [int]$y, [int]$w, [int]$h = 18, [bool]$muted = $false, [bool]$bold = $false) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $text
    $l.Location = New-Object System.Drawing.Point($x, $y)
    $l.Size = New-Object System.Drawing.Size($w, $h)
    $l.ForeColor = if ($muted) { $script:Muted } else { $script:Ink }
    if ($bold) { $l.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 9.5) }
    $l.BackColor = [System.Drawing.Color]::Transparent
    return $l
}

function New-Button([string]$text, [int]$x, [int]$y, [int]$w, [int]$h, [bool]$primary = $false) {
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $text
    $b.Location = New-Object System.Drawing.Point($x, $y)
    $b.Size = New-Object System.Drawing.Size($w, $h)
    $b.FlatStyle = 'Flat'
    $b.FlatAppearance.BorderSize = if ($primary) { 0 } else { 1 }
    $b.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(214, 219, 226)
    $b.BackColor = if ($primary) { $script:Accent } else { [System.Drawing.Color]::White }
    $b.ForeColor = if ($primary) { [System.Drawing.Color]::White } else { $script:Ink }
    $b.Cursor = [System.Windows.Forms.Cursors]::Hand
    $b.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    return $b
}

function New-Input([int]$x, [int]$y, [int]$w, [string]$text) {
    $t = New-Object System.Windows.Forms.TextBox
    $t.Location = New-Object System.Drawing.Point($x, $y)
    $t.Size = New-Object System.Drawing.Size($w, 24)
    $t.Text = $text
    $t.BorderStyle = 'FixedSingle'
    $t.Font = New-Object System.Drawing.Font('Segoe UI', 9.5)
    return $t
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Antigravity Proxy'
$form.ClientSize = New-Object System.Drawing.Size(504, 572)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.BackColor = $script:Bg
$form.ShowInTaskbar = $true
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9)

# --- карточка «Прокси» ---
$card1 = New-Object System.Windows.Forms.Panel
$card1.Location = New-Object System.Drawing.Point(16, 14)
$card1.Size = New-Object System.Drawing.Size(472, 176)
$card1.BackColor = $script:Card
$form.Controls.Add($card1)

$card1.Controls.Add((New-Label 'Прокси' 16 12 200 20 $false $true))
$card1.Controls.Add((New-Label 'Сервер' 16 44 120 16 $true))
$tbHost = New-Input 16 62 260 ''
$card1.Controls.Add($tbHost)
$card1.Controls.Add((New-Label 'Порт' 292 44 80 16 $true))
$tbPort = New-Input 292 62 76 ''
$card1.Controls.Add($tbPort)
$btnApply = New-Button 'Применить' 378 60 78 28 $true
$card1.Controls.Add($btnApply)

$card1.Controls.Add((New-Label 'Логин' 16 94 120 16 $true))
$tbUser = New-Input 16 112 260 ''
$card1.Controls.Add($tbUser)
$card1.Controls.Add((New-Label 'Пароль' 292 94 80 16 $true))
$tbPass = New-Input 292 112 76 ''
$tbPass.UseSystemPasswordChar = $true
$card1.Controls.Add($tbPass)
$btnTest = New-Button 'Проверить' 378 110 78 28 $false
$card1.Controls.Add($btnTest)

$lblStatus = New-Label 'Запускаю прокси…' 16 146 440 22 $true
$card1.Controls.Add($lblStatus)

# --- Antigravity ---
$card2 = New-Object System.Windows.Forms.Panel
$card2.Location = New-Object System.Drawing.Point(16, 200)
$card2.Size = New-Object System.Drawing.Size(472, 150)
$card2.BackColor = $script:Card
$form.Controls.Add($card2)

$card2.Controls.Add((New-Label 'Antigravity' 16 12 200 20 $false $true))
$lblApp = New-Label 'поиск установки…' 16 38 440 32 $true
$card2.Controls.Add($lblApp)

$btnStart = New-Button 'Запустить Antigravity' 16 78 190 32 $true
$card2.Controls.Add($btnStart)
$btnPatch = New-Button 'Патч' 218 78 90 32 $false
$card2.Controls.Add($btnPatch)
$lblPatch = New-Label 'проверка…' 318 84 140 20 $true
$card2.Controls.Add($lblPatch)

# --- остальное ---
$card3 = New-Object System.Windows.Forms.Panel
$card3.Location = New-Object System.Drawing.Point(16, 364)
$card3.Size = New-Object System.Drawing.Size(472, 152)
$card3.BackColor = $script:Card
$form.Controls.Add($card3)

$card3.Controls.Add((New-Label 'Этот же прокси для' 16 12 260 20 $false $true))
$btnBrowser = New-Button 'Браузер: Gemini и AI Studio' 16 40 250 34 $false
$card3.Controls.Add($btnBrowser)
$btnOpenCode = New-Button 'OpenCode' 278 40 178 34 $false
$card3.Controls.Add($btnOpenCode)

$chkSystem = New-Object System.Windows.Forms.CheckBox
$chkSystem.Text = 'Системный прокси для всех программ'
$chkSystem.Location = New-Object System.Drawing.Point(16, 86)
$chkSystem.Size = New-Object System.Drawing.Size(300, 22)
$chkSystem.ForeColor = $script:Ink
$chkSystem.BackColor = [System.Drawing.Color]::Transparent
$card3.Controls.Add($chkSystem)

$lblSysHint = New-Label 'Браузер откроется в отдельном профиле — основная сессия Chrome не затрагивается.' 16 112 430 32 $true
$card3.Controls.Add($lblSysHint)

$lblFoot = New-Label 'Патчер ищется в «Загрузках». В его меню выберите PATCH → 1 (IDE) или 2 (Antigravity 2.0).' 18 526 468 34 $true
$form.Controls.Add($lblFoot)

$info = Get-ProxyInfo
if ($info) {
    $tbHost.Text = $info.host
    $tbPort.Text = $info.port
    $tbUser.Text = $info.user
    $tbPass.Text = $info.pass
}
$chkSystem.Checked = Test-SystemProxy

# --- логика ---
$script:updateStatus = {
    if (Test-Port) {
        try {
            $j = Invoke-RestMethod -Uri 'https://ipinfo.io/json' -Proxy 'http://127.0.0.1:10809' -TimeoutSec 30
            $lblStatus.Text = "Работает — выход $($j.ip), $($j.country), $($j.city)"
            $lblStatus.ForeColor = $script:Ok
        } catch {
            $lblStatus.Text = 'Прокси запущен, но проверка выхода не удалась'
            $lblStatus.ForeColor = $script:Warn
        }
    } else {
        $lblStatus.Text = 'Прокси не запущен — нажмите «Применить»'
        $lblStatus.ForeColor = $script:Err
    }
}

$script:updatePatch = {
    if ($ag.LanguageServer) {
        $r = Test-Patched $ag.LanguageServer
        if ($r -eq $true) { $lblPatch.Text = 'патч применён'; $lblPatch.ForeColor = $script:Ok }
        elseif ($r -eq $false) { $lblPatch.Text = 'патч не применён'; $lblPatch.ForeColor = $script:Err }
        else { $lblPatch.Text = 'не удалось проверить'; $lblPatch.ForeColor = $script:Muted }
    } else {
        $lblPatch.Text = 'language_server не найден'
        $lblPatch.ForeColor = $script:Muted
    }
}

if ($ag.App) {
    $lblApp.Text = $ag.App
    $lblApp.ForeColor = $script:Muted
} else {
    $lblApp.Text = 'Не найдено. Установите Antigravity.'
    $lblApp.ForeColor = $script:Err
}

$btnApply.Add_Click({
    try {
        Write-ProxyConfig $tbHost.Text.Trim() $tbPort.Text.Trim() $tbUser.Text.Trim() $tbPass.Text
        if (Restart-Xray) { & $script:updateStatus }
        else { $lblStatus.Text = 'Xray не запустился — проверьте данные'; $lblStatus.ForeColor = $script:Err }
    } catch {
        $lblStatus.Text = 'Ошибка: ' + $_.Exception.Message
        $lblStatus.ForeColor = $script:Err
    }
})

$btnTest.Add_Click({ & $script:updateStatus })

$btnStart.Add_Click({
    try {
        if (-not $ag.App) { [System.Windows.Forms.MessageBox]::Show('Antigravity не найден.'); return }
        if (-not (Test-Port)) { Restart-Xray | Out-Null }
        $env:AG_LS_PROXY = 'http://127.0.0.1:10809'
        $env:HTTP_PROXY = 'http://127.0.0.1:10809'
        $env:HTTPS_PROXY = 'http://127.0.0.1:10809'
        $env:NO_PROXY = 'localhost,127.0.0.1,::1,' + $env:COMPUTERNAME
        Get-Process Antigravity -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 700
        Start-Process -FilePath $ag.App
        $lblStatus.Text = 'Antigravity запущен через прокси'
        $lblStatus.ForeColor = $script:Ok
    } catch {
        $lblStatus.Text = 'Не удалось запустить: ' + $_.Exception.Message
        $lblStatus.ForeColor = $script:Err
    }
})

$btnPatch.Add_Click({
    if (-not $patcher) {
        [System.Windows.Forms.MessageBox]::Show('Патчер не найден. Положите Open_AG_Patcher*.exe в «Загрузки».')
        return
    }
    try {
        $patchArgs = @()
        if ($ag.Dir) { $patchArgs = @(('"{0}"' -f $ag.Dir)) }
        Start-Process -FilePath $patcher -ArgumentList $patchArgs -Verb RunAs
        $lblPatch.Text = 'патчер запущен — выберите PATCH'
        $lblPatch.ForeColor = $script:Warn
    } catch {
        $lblPatch.Text = 'ошибка запуска патчера'
        $lblPatch.ForeColor = $script:Err
    }
})

$btnBrowser.Add_Click({
    try {
        if (-not $browser) { [System.Windows.Forms.MessageBox]::Show('Chrome или Edge не найден.'); return }
        if (-not (Test-Port)) { Restart-Xray | Out-Null }
        New-Item -ItemType Directory -Force -Path $BrowserProfile | Out-Null
        Start-Process -FilePath $browser -ArgumentList @(
            '--proxy-server=http://127.0.0.1:10809',
            ('--proxy-bypass-list={0}' -f (Get-BypassList)),
            ('--user-data-dir={0}' -f $BrowserProfile),
            '--no-first-run',
            '--no-default-browser-check',
            'https://aistudio.google.com/')
        $lblStatus.Text = 'Браузер открыт через прокси'
        $lblStatus.ForeColor = $script:Ok
    } catch {
        $lblStatus.Text = 'Не удалось открыть браузер: ' + $_.Exception.Message
        $lblStatus.ForeColor = $script:Err
    }
})

$btnOpenCode.Add_Click({
    try {
        if (-not $opencode) { [System.Windows.Forms.MessageBox]::Show('OpenCode не найден.'); return }
        if (-not (Test-Port)) { Restart-Xray | Out-Null }
        $bypass = 'localhost,127.0.0.1,::1,' + $env:COMPUTERNAME
        $env:HTTP_PROXY = 'http://127.0.0.1:10809'
        $env:HTTPS_PROXY = 'http://127.0.0.1:10809'
        $env:ALL_PROXY = 'http://127.0.0.1:10809'
        $env:NO_PROXY = $bypass
        $env:no_proxy = $bypass
        # без --proxy-server: иначе он проксирует и обращение к своему локальному серверу
        Start-Process -FilePath $opencode
        $lblStatus.Text = 'OpenCode запущен через прокси'
        $lblStatus.ForeColor = $script:Ok
    } catch {
        $lblStatus.Text = 'Не удалось запустить OpenCode: ' + $_.Exception.Message
        $lblStatus.ForeColor = $script:Err
    }
})

$chkSystem.Add_Click({
    try {
        Set-SystemProxy $chkSystem.Checked
        $lblStatus.Text = if ($chkSystem.Checked) { 'Системный прокси включён' } else { 'Системный прокси выключен' }
        $lblStatus.ForeColor = if ($chkSystem.Checked) { $script:Ok } else { $script:Muted }
    } catch {
        $lblStatus.Text = 'Не удалось изменить системный прокси: ' + $_.Exception.Message
        $lblStatus.ForeColor = $script:Err
    }
})

$form.Add_Shown({
    $form.Activate(); $form.TopMost = $true; $form.TopMost = $false
    if (-not (Test-Port)) { Restart-Xray | Out-Null }
    & $script:updateStatus
    & $script:updatePatch
})
[void]$form.ShowDialog()
