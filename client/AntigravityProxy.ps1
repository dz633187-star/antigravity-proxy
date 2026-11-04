#requires -version 5.1
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$Dir   = Join-Path $env:LOCALAPPDATA 'XrayNL'
$Cfg   = Join-Path $Dir 'config.json'
$Exe   = Join-Path $Dir 'xray.exe'
$App   = 'C:\Users\Дима\AppData\Local\Programs\antigravity\Antigravity.exe'

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

function Get-ProxyInfo {
    try {
        $j = Get-Content $Cfg -Raw -Encoding UTF8 | ConvertFrom-Json
        $o = $j.outbounds | Where-Object { $_.protocol -eq 'http' } | Select-Object -First 1
        if ($o) {
            $s = $o.settings.servers[0]
            $u = ''
            $p = ''
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

# ---------- форма ----------
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Antigravity Proxy'
$form.Size = New-Object System.Drawing.Size(470, 340)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9)

function Add-Label($text, $x, $y, $w = 110) {
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

Add-Label 'Сервер (IP или домен):' 18 20 180 | Out-Null
$tbHost = Add-Box 18 42 250 ''

Add-Label 'Порт:' 290 20 60 | Out-Null
$tbPort = Add-Box 290 42 120 ''

Add-Label 'Логин:' 18 78 100 | Out-Null
$tbUser = Add-Box 18 100 250 ''

Add-Label 'Пароль:' 290 78 100 | Out-Null
$tbPass = Add-Box 290 100 120 ''
$tbPass.UseSystemPasswordChar = $true

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Location = New-Object System.Drawing.Point(18, 140)
$lblStatus.Size = New-Object System.Drawing.Size(420, 40)
$lblStatus.Text = 'Проверяю...'
$form.Controls.Add($lblStatus)

$btnApply = New-Object System.Windows.Forms.Button
$btnApply.Text = 'Применить'
$btnApply.Location = New-Object System.Drawing.Point(18, 190)
$btnApply.Size = New-Object System.Drawing.Size(120, 34)
$form.Controls.Add($btnApply)

$btnTest = New-Object System.Windows.Forms.Button
$btnTest.Text = 'Проверить выход'
$btnTest.Location = New-Object System.Drawing.Point(150, 190)
$btnTest.Size = New-Object System.Drawing.Size(130, 34)
$form.Controls.Add($btnTest)

$btnStart = New-Object System.Windows.Forms.Button
$btnStart.Text = 'Запустить Antigravity'
$btnStart.Location = New-Object System.Drawing.Point(292, 190)
$btnStart.Size = New-Object System.Drawing.Size(150, 34)
$form.Controls.Add($btnStart)


$lblHint = New-Object System.Windows.Forms.Label
$lblHint.Location = New-Object System.Drawing.Point(18, 240)
$lblHint.Size = New-Object System.Drawing.Size(240, 46)
$lblHint.ForeColor = [System.Drawing.Color]::Gray
$lblHint.Text = 'Если прокси перестал работать — впишите новый и нажмите «Применить».'
$form.Controls.Add($lblHint)

# заполняем текущими значениями
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
if (-not (Test-Port)) { Restart-Xray | Out-Null }
& $script:updateStatus

$btnApply.Add_Click({
    try {
        Write-ProxyConfig $tbHost.Text.Trim() $tbPort.Text.Trim() $tbUser.Text.Trim() $tbPass.Text
        if (Restart-Xray) { if (-not (Test-Port)) { Restart-Xray | Out-Null }
& $script:updateStatus }
        else { $lblStatus.Text = 'Xray не запустился. Проверьте данные прокси.'; $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick }
    } catch {
        $lblStatus.Text = 'Ошибка: ' + $_.Exception.Message
        $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick
    }
})

$btnTest.Add_Click({ if (-not (Test-Port)) { Restart-Xray | Out-Null }
& $script:updateStatus })

$btnStart.Add_Click({
    try {
        if (-not (Test-Port)) { Restart-Xray | Out-Null }
        $env:AG_LS_PROXY = 'http://127.0.0.1:10809'
        $env:HTTP_PROXY = 'http://127.0.0.1:10809'
        $env:HTTPS_PROXY = 'http://127.0.0.1:10809'
        $env:NO_PROXY = 'localhost,127.0.0.1'
        Get-Process Antigravity -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 700
        Start-Process -FilePath $App
        $lblStatus.Text = 'Antigravity запущен через прокси.'
        $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
    } catch {
        $lblStatus.Text = 'Не удалось запустить: ' + $_.Exception.Message
        $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick
    }
})

[void]$form.ShowDialog()
