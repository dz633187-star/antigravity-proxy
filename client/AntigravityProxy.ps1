#requires -version 5.1
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$Dir = Join-Path $env:LOCALAPPDATA 'XrayNL'
$Cfg = Join-Path $Dir 'config.json'
$Exe = Join-Path $Dir 'xray.exe'

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
    $result = @{ App = ''; Dir = ''; LanguageServer = ''; Agy = ''; IdeMain = '' }
    $cands = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\antigravity\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity 2.0\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity IDE\Antigravity IDE.exe'),
        (Join-Path $env:ProgramFiles 'Antigravity\Antigravity.exe')
    )
    foreach ($c in $cands) {
        if (Test-Path $c) { $result.App = $c; break }
    }
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
        $mj = Join-Path $result.Dir 'resources\app\out\main.js'
        if (Test-Path $mj) { $result.IdeMain = $mj }
    }
    $agy = Join-Path $env:LOCALAPPDATA 'agy\bin\agy.exe'
    if (Test-Path $agy) { $result.Agy = $agy }
    return $result
}

function Test-Patched([string]$path) {
    if (-not $path -or -not (Test-Path $path)) { return $null }
    try {
        $fs = [System.IO.File]::OpenRead($path)
        $buf = New-Object byte[] (4MB)
        $found = $false
        $carry = ''
        while (($n = $fs.Read($buf, 0, $buf.Length)) -gt 0) {
            $chunk = $carry + [System.Text.Encoding]::ASCII.GetString($buf, 0, $n)
            if ($chunk.Contains('inexigible')) { $found = $true; break }
            if ($chunk.Contains('ineligible')) { break }
            $carry = if ($chunk.Length -gt 32) { $chunk.Substring($chunk.Length - 32) } else { $chunk }
        }
        $fs.Close()
        return $found
    } catch { return $null }
}

function Find-Patcher {
    $dirs = @((Join-Path $env:USERPROFILE 'Downloads'), $PSScriptRoot)
    foreach ($d in $dirs) {
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

$ag = Find-Antigravity
$patcher = Find-Patcher

# ---------- форма ----------
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Antigravity Proxy'
$form.Size = New-Object System.Drawing.Size(490, 430)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.ShowInTaskbar = $true
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9)

function Add-Label($text, $x, $y, $w = 130) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $text; $l.Location = New-Object System.Drawing.Point($x, $y)
    $l.Size = New-Object System.Drawing.Size($w, 20)
    $form.Controls.Add($l)
    return $l
}
function Add-Box($x, $y, $w, $text) {
    $t = New-Object System.Windows.Forms.TextBox
    $t.Location = New-Object System.Drawing.Point($x, $y)
    $t.Size = New-Object System.Drawing.Size($w, 24)
    $t.Text = $text
    $form.Controls.Add($t)
    return $t
}

Add-Label 'Сервер (IP или домен):' 18 16 200 | Out-Null
$tbHost = Add-Box 18 38 250 ''

Add-Label 'Порт:' 290 16 60 | Out-Null
$tbPort = Add-Box 290 38 120 ''

Add-Label 'Логин:' 18 72 100 | Out-Null
$tbUser = Add-Box 18 94 250 ''

Add-Label 'Пароль:' 290 72 100 | Out-Null
$tbPass = Add-Box 290 94 120 ''
$tbPass.UseSystemPasswordChar = $true

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Location = New-Object System.Drawing.Point(18, 130)
$lblStatus.Size = New-Object System.Drawing.Size(440, 38)
$lblStatus.Text = 'Проверяю...'
$form.Controls.Add($lblStatus)

$lblApp = New-Object System.Windows.Forms.Label
$lblApp.Location = New-Object System.Drawing.Point(18, 176)
$lblApp.Size = New-Object System.Drawing.Size(440, 40)
$lblApp.ForeColor = [System.Drawing.Color]::DimGray
if ($ag.App) { $lblApp.Text = "Antigravity: $($ag.App)" } else { $lblApp.Text = 'Antigravity не найден — укажите путь вручную в настройках установки.' }
$form.Controls.Add($lblApp)

$btnApply = New-Object System.Windows.Forms.Button
$btnApply.Text = 'Применить'
$btnApply.Location = New-Object System.Drawing.Point(18, 224)
$btnApply.Size = New-Object System.Drawing.Size(110, 34)
$form.Controls.Add($btnApply)

$btnTest = New-Object System.Windows.Forms.Button
$btnTest.Text = 'Проверить выход'
$btnTest.Location = New-Object System.Drawing.Point(138, 224)
$btnTest.Size = New-Object System.Drawing.Size(130, 34)
$form.Controls.Add($btnTest)

$btnStart = New-Object System.Windows.Forms.Button
$btnStart.Text = 'Запустить Antigravity'
$btnStart.Location = New-Object System.Drawing.Point(278, 224)
$btnStart.Size = New-Object System.Drawing.Size(180, 34)
$form.Controls.Add($btnStart)

$btnPatch = New-Object System.Windows.Forms.Button
$btnPatch.Text = 'Патч'
$btnPatch.Location = New-Object System.Drawing.Point(18, 272)
$btnPatch.Size = New-Object System.Drawing.Size(110, 34)
$form.Controls.Add($btnPatch)

$lblPatch = New-Object System.Windows.Forms.Label
$lblPatch.Location = New-Object System.Drawing.Point(138, 272)
$lblPatch.Size = New-Object System.Drawing.Size(320, 50)
$lblPatch.ForeColor = [System.Drawing.Color]::DimGray
$lblPatch.Text = 'Проверка патча...'
$form.Controls.Add($lblPatch)

$lblHint = New-Object System.Windows.Forms.Label
$lblHint.Location = New-Object System.Drawing.Point(18, 330)
$lblHint.Size = New-Object System.Drawing.Size(440, 50)
$lblHint.ForeColor = [System.Drawing.Color]::Gray
$lblHint.Text = 'Патчер ищется в «Загрузках». Если патч не применён — нажмите «Патч» и в его меню выберите PATCH → 1 (IDE) или 2 (Antigravity 2.0).'
$form.Controls.Add($lblHint)

$info = Get-ProxyInfo
if ($info) {
    $tbHost.Text = $info.host
    $tbPort.Text = $info.port
    $tbUser.Text = $info.user
    $tbPass.Text = $info.pass
}

$script:updateStatus = {
    if (Test-Port) {
        try {
            $j = Invoke-RestMethod -Uri 'https://ipinfo.io/json' -Proxy 'http://127.0.0.1:10809' -TimeoutSec 30
            $lblStatus.Text = "Прокси работает. Выход: $($j.ip) — $($j.country), $($j.city)"
            $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
        } catch {
            $lblStatus.Text = 'Прокси запущен, но проверить выход не удалось: ' + $_.Exception.Message
            $lblStatus.ForeColor = [System.Drawing.Color]::DarkOrange
        }
    } else {
        $lblStatus.Text = 'Прокси не запущен. Нажмите «Применить».'
        $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick
    }
}

$script:updatePatch = {
    if ($ag.LanguageServer) {
        $r = Test-Patched $ag.LanguageServer
        if ($r -eq $true) {
            $lblPatch.Text = 'Патч: применён'
            $lblPatch.ForeColor = [System.Drawing.Color]::DarkGreen
        } elseif ($r -eq $false) {
            $lblPatch.Text = 'Патч: не применён'
            $lblPatch.ForeColor = [System.Drawing.Color]::Firebrick
        } else {
            $lblPatch.Text = 'Патч: не удалось проверить'
            $lblPatch.ForeColor = [System.Drawing.Color]::DimGray
        }
    } else {
        $lblPatch.Text = 'Патч: language_server не найден'
        $lblPatch.ForeColor = [System.Drawing.Color]::DimGray
    }
}





$btnApply.Add_Click({
    try {
        Write-ProxyConfig $tbHost.Text.Trim() $tbPort.Text.Trim() $tbUser.Text.Trim() $tbPass.Text
        if (Restart-Xray) { & $script:updateStatus }
        else { $lblStatus.Text = 'Xray не запустился. Проверьте данные прокси.'; $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick }
    } catch {
        $lblStatus.Text = 'Ошибка: ' + $_.Exception.Message
        $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick
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
        $env:NO_PROXY = 'localhost,127.0.0.1'
        Get-Process Antigravity -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 700
        Start-Process -FilePath $ag.App
        $lblStatus.Text = 'Antigravity запущен через прокси.'
        $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
    } catch {
        $lblStatus.Text = 'Не удалось запустить: ' + $_.Exception.Message
        $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick
    }
})

$btnPatch.Add_Click({
    if (-not $patcher) {
        [System.Windows.Forms.MessageBox]::Show('Патчер не найден. Положите Open_AG_Patcher*.exe в папку «Загрузки».')
        return
    }
    try {
        $patchArgs = @()
        if ($ag.Dir) { $patchArgs = @(('"{0}"' -f $ag.Dir)) }
        Start-Process -FilePath $patcher -ArgumentList $patchArgs -Verb RunAs
        $lblPatch.Text = 'Патчер запущен. В его меню: PATCH → 1 (IDE) или 2 (Antigravity 2.0).'
        $lblPatch.ForeColor = [System.Drawing.Color]::DarkOrange
    } catch {
        $lblPatch.Text = 'Не удалось запустить патчер: ' + $_.Exception.Message
        $lblPatch.ForeColor = [System.Drawing.Color]::Firebrick
    }
})

$form.Add_Shown({
    $form.Activate(); $form.TopMost = $true; $form.TopMost = $false
    $lblStatus.Text = 'Запускаю прокси...'
    if (-not (Test-Port)) { Restart-Xray | Out-Null }
    & $script:updateStatus
    & $script:updatePatch
})
[void]$form.ShowDialog()
