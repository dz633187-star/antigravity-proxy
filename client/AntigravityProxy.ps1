#requires -version 5.1
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# ---------- чистое окружение ----------
foreach ($n in @('http_proxy', 'https_proxy', 'all_proxy', 'no_proxy')) {
    try { [System.Environment]::SetEnvironmentVariable($n, $null) } catch { }
}

# ---------- иконка в панели задач ----------
try {
    $shellApi = Add-Type -MemberDefinition @'
[DllImport("shell32.dll", SetLastError = true)]
public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);
'@ -Name 'ShellApi' -Namespace 'Win' -PassThru
    [void][Win.ShellApi]::SetCurrentProcessExplicitAppUserModelID('AntigravityProxy.Desktop')
} catch { }

try {
    $dpiApi = Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);' -Name 'DpiApi' -Namespace 'Win' -PassThru
    [void][Win.DpiApi]::SetProcessDpiAwarenessContext([IntPtr](-1))
} catch { }
# ---------- скруглённые кнопки ----------
try {
    Add-Type -ReferencedAssemblies System.Drawing, System.Windows.Forms -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class UiButton : Button
{
    public int Radius = 9;
    public Color Fill = Color.FromArgb(37, 99, 235);
    public Color Hover = Color.FromArgb(29, 78, 216);
    public Color Border = Color.Transparent;
    public Color TextColor = Color.White;
    private bool _hover;

    public UiButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bg = Parent != null ? Parent.BackColor : BackColor;
        using (var bgBrush = new SolidBrush(bg)) g.FillRectangle(bgBrush, ClientRectangle);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Round(rect, Radius))
        {
            using (var b = new SolidBrush(_hover ? Hover : Fill)) g.FillPath(b, path);
            if (Border != Color.Transparent)
                using (var p = new Pen(Border)) g.DrawPath(p, path);
        }
        TextRenderer.DrawText(g, Text, Font, rect, TextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        if (d <= 0 || d > r.Height) d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
'@
} catch { }

$ErrorActionPreference = 'Stop'
$script:LogFile = Join-Path $env:TEMP 'antigravity-proxy.log'
function Write-Log([string]$m) {
    try { "$(Get-Date -Format 'HH:mm:ss') $m" | Out-File $script:LogFile -Append -Encoding UTF8 } catch { }
}
[System.Windows.Forms.Application]::add_ThreadException({ param($s, $e) Write-Log ('THREAD: ' + $e.Exception.ToString()) })
[System.AppDomain]::CurrentDomain.add_UnhandledException({ param($s, $e) Write-Log ('UNHANDLED: ' + $e.ExceptionObject.ToString()) })

$Dir = Join-Path $env:LOCALAPPDATA 'XrayNL'
$Cfg = Join-Path $Dir 'config.json'
$Exe = Join-Path $Dir 'xray.exe'
$AppRoot = Join-Path $env:LOCALAPPDATA 'AntigravityProxy'
$BrowserProfile = Join-Path $AppRoot 'BrowserProfile'
$SettingsPath = Join-Path $AppRoot 'settings.json'
$IconPath = Join-Path $PSScriptRoot 'AntigravityProxy.ico'

# ---------- настройки ----------
$script:Settings = @{ theme = 'dark' }
if (Test-Path $SettingsPath) {
    try { $script:Settings = (Get-Content $SettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json) } catch { }
}
if (-not $script:Settings.theme) { $script:Settings.theme = 'dark' }

function Save-Settings {
    ($script:Settings | ConvertTo-Json -Depth 5) | Set-Content $SettingsPath -Encoding UTF8
}

# ---------- палитры ----------
function Get-Palette([string]$theme) {
    if ($theme -eq 'dark') {
        return @{
            bg = [System.Drawing.Color]::FromArgb(20, 22, 27)
            card = [System.Drawing.Color]::FromArgb(30, 33, 40)
            ink = [System.Drawing.Color]::FromArgb(235, 238, 243)
            muted = [System.Drawing.Color]::FromArgb(148, 157, 171)
            border = [System.Drawing.Color]::FromArgb(48, 53, 63)
            accent = [System.Drawing.Color]::FromArgb(59, 130, 246)
            accentHover = [System.Drawing.Color]::FromArgb(37, 99, 235)
            ok = [System.Drawing.Color]::FromArgb(74, 222, 128)
            warn = [System.Drawing.Color]::FromArgb(251, 191, 36)
            err = [System.Drawing.Color]::FromArgb(248, 113, 113)
            secondaryFill = [System.Drawing.Color]::FromArgb(38, 42, 51)
            secondaryText = [System.Drawing.Color]::FromArgb(226, 230, 236)
        }
    }
    return @{
        bg = [System.Drawing.Color]::FromArgb(246, 247, 249)
        card = [System.Drawing.Color]::White
        ink = [System.Drawing.Color]::FromArgb(24, 32, 44)
        muted = [System.Drawing.Color]::FromArgb(110, 120, 134)
        border = [System.Drawing.Color]::FromArgb(227, 231, 237)
        accent = [System.Drawing.Color]::FromArgb(37, 99, 235)
        accentHover = [System.Drawing.Color]::FromArgb(29, 78, 216)
        ok = [System.Drawing.Color]::FromArgb(21, 128, 61)
        warn = [System.Drawing.Color]::FromArgb(180, 83, 9)
        err = [System.Drawing.Color]::FromArgb(185, 28, 28)
        secondaryFill = [System.Drawing.Color]::White
        secondaryText = [System.Drawing.Color]::FromArgb(24, 32, 44)
    }
}

# ---------- утилиты ----------
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
    $result = @{ App = ''; Dir = ''; LanguageServer = '' }
    foreach ($c in @(
        (Join-Path $env:LOCALAPPDATA 'Programs\antigravity\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity 2.0\Antigravity.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Antigravity IDE\Antigravity IDE.exe'))) {
        if (Test-Path $c) { $result.App = $c; break }
    }
    if ($result.App) {
        $result.Dir = Split-Path $result.App
        $ls = Get-ChildItem (Join-Path $result.Dir 'resources') -Recurse -Filter 'language_server.exe' -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ($ls) { $result.LanguageServer = $ls }
    }
    return $result
}

function Find-Browser {
    foreach ($c in @(
        'C:\Program Files\Google\Chrome\Application\chrome.exe',
        'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
        (Join-Path $env:LOCALAPPDATA 'Google\Chrome\Application\chrome.exe'),
        'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe')) {
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

function Find-Patcher {
    foreach ($d in @((Join-Path $env:USERPROFILE 'Downloads'), $PSScriptRoot)) {
        $f = Get-ChildItem $d -Filter 'Open_AG_Patcher*.exe' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($f) { return $f.FullName }
    }
    return ''
}

function Find-OpenCodeCli {
    $cmd = Get-Command opencode.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($c in @(
        (Join-Path $env:LOCALAPPDATA 'Programs\@opencode-aidesktop\resources\opencode-cli.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\@opencode-aidesktop\resources\opencode.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\opencode\opencode.exe'))) {
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

function Get-BypassList { return ('<local>;localhost;127.0.0.1;::1;' + $env:COMPUTERNAME) }

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
$ocCli = Find-OpenCodeCli

$script:P = Get-Palette $script:Settings.theme
$script:ThemeTag = @()

# ---------- главное окно ----------
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Antigravity Proxy'
$form.ClientSize = New-Object System.Drawing.Size(520, 512)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.ShowInTaskbar = $true
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9)
if (Test-Path $IconPath) { try { $form.Icon = New-Object System.Drawing.Icon($IconPath) } catch { } }

function Add-Ui([object]$ctrl, [string]$role) {
    $ctrl.Tag = $role
    $form.Controls.Add($ctrl)
    return $ctrl
}

function New-Txt([int]$x, [int]$y, [int]$w) {
    $t = New-Object System.Windows.Forms.TextBox
    $t.Location = New-Object System.Drawing.Point($x, $y)
    $t.Size = New-Object System.Drawing.Size($w, 24)
    $t.BorderStyle = 'FixedSingle'
    return $t
}

function New-Lbl([string]$text, [int]$x, [int]$y, [int]$w, [int]$h, [string]$role = 'ink', [double]$size = 9, [bool]$bold = $false) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $text
    $l.Location = New-Object System.Drawing.Point($x, $y)
    $l.Size = New-Object System.Drawing.Size($w, $h)
    $l.Tag = $role
    $fname = if ($bold) { 'Segoe UI Semibold' } else { 'Segoe UI' }
    $l.Font = New-Object System.Drawing.Font($fname, $size)
    $l.ForeColor = if ($role -eq 'muted') { $script:P.muted } else { $script:P.ink }
    return $l
}

# шапка
$lblTitle = New-Lbl 'Antigravity Proxy' 12 10 300 26 'ink' 12 $true
$form.Controls.Add($lblTitle)
$lblSub = New-Lbl 'прокси, патч и запуск' 14 36 300 18 'muted' 8.5
$form.Controls.Add($lblSub)

$btnTheme = New-Object UiButton
$btnTheme.Text = ''
$btnTheme.Location = New-Object System.Drawing.Point(406, 12)
$btnTheme.Size = New-Object System.Drawing.Size(40, 32)
$btnTheme.Radius = 8
$form.Controls.Add($btnTheme)

$btnSettings = New-Object UiButton
$btnSettings.Text = 'Настройки'
$btnSettings.Location = New-Object System.Drawing.Point(296, 12)
$btnSettings.Size = New-Object System.Drawing.Size(100, 32)
$btnSettings.Font = New-Object System.Drawing.Font('Segoe UI', 8)
$btnSettings.Radius = 8
$form.Controls.Add($btnSettings)

# карточка статуса
$card1 = New-Object System.Windows.Forms.Panel
$card1.Location = New-Object System.Drawing.Point(16, 68)
$card1.Size = New-Object System.Drawing.Size(488, 120)
$card1.Tag = 'card'
$form.Controls.Add($card1)

$lblStatus = New-Lbl 'Запускаю прокси…' 16 16 380 26 'ink' 10.5 $true
$card1.Controls.Add($lblStatus)
$lblDetail = New-Lbl '' 16 46 450 34 'muted' 8.5
$card1.Controls.Add($lblDetail)
$btnTest = New-Object UiButton
$btnTest.Text = 'Проверить'
$btnTest.Location = New-Object System.Drawing.Point(16, 80)
$btnTest.Size = New-Object System.Drawing.Size(120, 30)
$btnTest.Radius = 8
$card1.Controls.Add($btnTest)

# действия
$btnStart = New-Object UiButton
$btnStart.Text = 'Запустить Antigravity'
$btnStart.Location = New-Object System.Drawing.Point(16, 200)
$btnStart.Size = New-Object System.Drawing.Size(488, 44)
$btnStart.Radius = 10
$btnStart.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 9.5)
$form.Controls.Add($btnStart)

$btnBrowser = New-Object UiButton
$btnBrowser.Text = 'Браузер: Gemini'
$btnBrowser.Location = New-Object System.Drawing.Point(16, 254)
$btnBrowser.Size = New-Object System.Drawing.Size(200, 38)
$btnBrowser.Radius = 9
$form.Controls.Add($btnBrowser)

$btnOcGui = New-Object UiButton
$btnOcGui.Text = 'OpenCode GUI'
$btnOcGui.Location = New-Object System.Drawing.Point(224, 254)
$btnOcGui.Size = New-Object System.Drawing.Size(130, 38)
$btnOcGui.Radius = 9
$form.Controls.Add($btnOcGui)

$btnOcCli = New-Object UiButton
$btnOcCli.Text = 'OpenCode CLI'
$btnOcCli.Location = New-Object System.Drawing.Point(362, 254)
$btnOcCli.Size = New-Object System.Drawing.Size(142, 38)
$btnOcCli.Radius = 9
$form.Controls.Add($btnOcCli)

# карточка состояния
$card2 = New-Object System.Windows.Forms.Panel
$card2.Location = New-Object System.Drawing.Point(16, 306)
$card2.Size = New-Object System.Drawing.Size(488, 150)
$card2.Tag = 'card'
$form.Controls.Add($card2)

$lblPatch = New-Lbl 'Патч: проверка…' 16 14 300 20 'muted' 9
$card2.Controls.Add($lblPatch)

$btnPatch = New-Object UiButton
$btnPatch.Text = 'Патч'
$btnPatch.Location = New-Object System.Drawing.Point(376, 10)
$btnPatch.Size = New-Object System.Drawing.Size(96, 28)
$btnPatch.Radius = 8
$card2.Controls.Add($btnPatch)

$lblApp = New-Lbl '' 16 44 450 44 'muted' 8
$card2.Controls.Add($lblApp)

$btnSysProxy = New-Object UiButton
$btnSysProxy.Text = 'Системный прокси: выкл'
$btnSysProxy.Location = New-Object System.Drawing.Point(16, 100)
$btnSysProxy.Size = New-Object System.Drawing.Size(250, 30)
$btnSysProxy.Radius = 8
$card2.Controls.Add($btnSysProxy)

$lblFoot = New-Lbl 'Прокси-настройки, тема и пути — в «Настройках».' 18 474 480 18 'muted' 8.5
$form.Controls.Add($lblFoot)

function Update-SysProxyButton {
    $on = Test-SystemProxy
    $btnSysProxy.Text = if ($on) { 'Системный прокси: вкл' } else { 'Системный прокси: выкл' }
    if ($on) {
        $btnSysProxy.Fill = $script:P.accent
        $btnSysProxy.Hover = $script:P.accentHover
        $btnSysProxy.Border = [System.Drawing.Color]::Transparent
        $btnSysProxy.TextColor = [System.Drawing.Color]::White
    } else {
        $btnSysProxy.Fill = $script:P.secondaryFill
        $btnSysProxy.Hover = $script:P.border
        $btnSysProxy.Border = $script:P.border
        $btnSysProxy.TextColor = $script:P.secondaryText
    }
    $btnSysProxy.Invalidate()
}
# ---------- тема ----------
function Apply-Theme {
    $p = $script:P
    $form.BackColor = $p.bg
    foreach ($c in @($lblTitle, $lblSub, $lblStatus, $lblDetail, $lblPatch, $lblApp, $lblFoot)) {
        if (-not $c) { continue }
        switch ($c.Tag) {
            'card' { }
            'muted' { $c.ForeColor = $p.muted }
            'ink' { $c.ForeColor = $p.ink }
            default { $c.ForeColor = $p.ink }
        }
    }
    foreach ($c in @($card1, $card2)) {
        $c.BackColor = $p.card
    }
    foreach ($b in @($btnTest, $btnBrowser, $btnOcGui, $btnOcCli, $btnPatch, $btnTheme, $btnSettings)) {
        $b.Fill = $p.secondaryFill
        $b.Hover = $p.border
        $b.Border = $p.border
        $b.TextColor = $p.secondaryText
    }
    $btnStart.Fill = $p.accent
    $btnStart.Hover = $p.accentHover
    $btnStart.Border = [System.Drawing.Color]::Transparent
    $btnStart.TextColor = [System.Drawing.Color]::White
    $btnTheme.Text = if ($script:Settings.theme -eq 'dark') { '☀' } else { '☾' }
    $btnTheme.Font = New-Object System.Drawing.Font('Segoe UI Symbol', 11)
    $btnTheme.TextColor = $p.ink
    Update-SysProxyButton
    $form.Invalidate($true)
}

function Update-Status {
    if (Test-Port) {
        $info = Get-ProxyInfo
        $where = if ($info) { "$($info.host):$($info.port)" } else { '—' }
        try {
            $j = Invoke-RestMethod -Uri 'https://ipinfo.io/json' -Proxy 'http://127.0.0.1:10809' -TimeoutSec 30
            $lblStatus.Text = 'Прокси работает'
            $lblStatus.ForeColor = $script:P.ok
            $lblDetail.Text = "Выход: $($j.ip) · $($j.country), $($j.city)`r`nПрокси: $where"
        } catch {
            $lblStatus.Text = 'Прокси работает, проверка не удалась'
            $lblStatus.ForeColor = $script:P.warn
            $lblDetail.Text = "Прокси: $where"
        }
    } else {
        $lblStatus.Text = 'Прокси не запущен'
        $lblStatus.ForeColor = $script:P.err
        $lblDetail.Text = 'Откройте «Настройки», проверьте адрес прокси и нажмите «Применить».'
    }
}

function Update-Patch {
    if ($ag.LanguageServer) {
        $r = Test-Patched $ag.LanguageServer
        if ($r -eq $true) { $lblPatch.Text = 'Патч применён'; $lblPatch.ForeColor = $script:P.ok }
        elseif ($r -eq $false) { $lblPatch.Text = 'Патч не применён'; $lblPatch.ForeColor = $script:P.err }
        else { $lblPatch.Text = 'Патч: не удалось проверить'; $lblPatch.ForeColor = $script:P.muted }
    } else {
        $lblPatch.Text = 'language_server не найден'
        $lblPatch.ForeColor = $script:P.muted
    }
    if ($ag.App) { $lblApp.Text = "Antigravity:`r`n$($ag.App)"; $lblApp.ForeColor = $script:P.muted }
    else { $lblApp.Text = 'Antigravity не найден'; $lblApp.ForeColor = $script:P.err }
}

# ---------- окно настроек ----------
function Show-Settings {
    $p = $script:P
    $sf = New-Object System.Windows.Forms.Form
    $sf.Text = 'Настройки'
    $sf.ClientSize = New-Object System.Drawing.Size(430, 430)
    $sf.StartPosition = 'CenterParent'
    $sf.FormBorderStyle = 'FixedDialog'
    $sf.MaximizeBox = $false
    $sf.MinimizeBox = $false
    $sf.BackColor = $p.bg
    $sf.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    if ($form.Icon) { $sf.Icon = $form.Icon }

    $info = Get-ProxyInfo
    $h = if ($info) { $info.host } else { '' }
    $pt = if ($info) { $info.port } else { '' }
    $u = if ($info) { $info.user } else { '' }
    $pw = if ($info) { $info.pass } else { '' }

    $sf.Controls.Add((New-Lbl 'Прокси' 18 14 200 22 'ink' 10.5 $true))
    $sf.Controls.Add((New-Lbl 'Сервер' 18 46 120 16 'muted' 8.5))
    $tbH = New-Txt 18 64 260; $tbH.Text = $h; $tbH.BackColor = $p.card; $tbH.ForeColor = $p.ink; $sf.Controls.Add($tbH)
    $sf.Controls.Add((New-Lbl 'Порт' 292 46 60 16 'muted' 8.5))
    $tbPt = New-Txt 292 64 118; $tbPt.Text = $pt; $tbPt.BackColor = $p.card; $tbPt.ForeColor = $p.ink; $sf.Controls.Add($tbPt)
    $sf.Controls.Add((New-Lbl 'Логин' 18 98 120 16 'muted' 8.5))
    $tbU = New-Txt 18 116 260; $tbU.Text = $u; $tbU.BackColor = $p.card; $tbU.ForeColor = $p.ink; $sf.Controls.Add($tbU)
    $sf.Controls.Add((New-Lbl 'Пароль' 292 98 60 16 'muted' 8.5))
    $tbPw = New-Txt 292 116 118; $tbPw.Text = $pw; $tbPw.UseSystemPasswordChar = $true; $tbPw.BackColor = $p.card; $tbPw.ForeColor = $p.ink; $sf.Controls.Add($tbPw)

    $btnApplyProxy = New-Object UiButton
    $btnApplyProxy.Text = 'Применить прокси'
    $btnApplyProxy.Location = New-Object System.Drawing.Point(18, 154)
    $btnApplyProxy.Size = New-Object System.Drawing.Size(180, 34)
    $btnApplyProxy.Radius = 8
    $btnApplyProxy.Fill = $p.accent; $btnApplyProxy.Hover = $p.accentHover
    $btnApplyProxy.Border = [System.Drawing.Color]::Transparent; $btnApplyProxy.TextColor = [System.Drawing.Color]::White
    $sf.Controls.Add($btnApplyProxy)

    $sf.Controls.Add((New-Lbl 'Тема' 18 206 200 22 'ink' 10.5 $true))
    $rbLight = New-Object System.Windows.Forms.RadioButton
    $rbLight.Text = 'Светлая'; $rbLight.Location = New-Object System.Drawing.Point(20, 234); $rbLight.Size = New-Object System.Drawing.Size(110, 22)
    $rbLight.ForeColor = $p.ink
    $rbDark = New-Object System.Windows.Forms.RadioButton
    $rbDark.Text = 'Тёмная'; $rbDark.Location = New-Object System.Drawing.Point(140, 234); $rbDark.Size = New-Object System.Drawing.Size(110, 22)
    $rbDark.ForeColor = $p.ink
    if ($script:Settings.theme -eq 'dark') { $rbDark.Checked = $true } else { $rbLight.Checked = $true }
    $sf.Controls.Add($rbLight); $sf.Controls.Add($rbDark)

    $lblNote = New-Lbl 'Тема применится сразу после сохранения.' 18 268 390 34 'muted' 8.5
    $sf.Controls.Add($lblNote)

    $btnSave = New-Object UiButton
    $btnSave.Text = 'Сохранить'
    $btnSave.Location = New-Object System.Drawing.Point(18, 372)
    $btnSave.Size = New-Object System.Drawing.Size(140, 36)
    $btnSave.Radius = 9
    $btnSave.Fill = $p.accent; $btnSave.Hover = $p.accentHover
    $btnSave.Border = [System.Drawing.Color]::Transparent; $btnSave.TextColor = [System.Drawing.Color]::White
    $sf.Controls.Add($btnSave)

    $btnCancel = New-Object UiButton
    $btnCancel.Text = 'Отмена'
    $btnCancel.Location = New-Object System.Drawing.Point(170, 372)
    $btnCancel.Size = New-Object System.Drawing.Size(120, 36)
    $btnCancel.Radius = 9
    $btnCancel.Fill = $p.secondaryFill; $btnCancel.Hover = $p.border
    $btnCancel.Border = $p.border; $btnCancel.TextColor = $p.secondaryText
    $sf.Controls.Add($btnCancel)

    $btnApplyProxy.Add_Click({
        try {
            Write-ProxyConfig $tbH.Text.Trim() $tbPt.Text.Trim() $tbU.Text.Trim() $tbPw.Text
            $ok = Restart-Xray
            if ($ok) { Update-Status } else { $lblNote.Text = 'Xray не запустился — проверьте данные.' }
        } catch { $lblNote.Text = 'Ошибка: ' + $_.Exception.Message }
    })

    $btnSave.Add_Click({
        $script:Settings.theme = if ($rbDark.Checked) { 'dark' } else { 'light' }
        Save-Settings
        $sf.Close()
        $script:P = Get-Palette $script:Settings.theme
        Apply-Theme
        Update-Status
        Update-Patch
    })

    $btnCancel.Add_Click({ $sf.Close() })
    [void]$sf.ShowDialog($form)
}

# ---------- трей ----------
$script:exiting = $false
$appIcon = if ($form.Icon) { $form.Icon } else { [System.Drawing.SystemIcons]::Application }
$notify = New-Object System.Windows.Forms.NotifyIcon
$notify.Icon = $appIcon
$notify.Text = 'Antigravity Proxy'
$notify.Visible = $true
$trayMenu = New-Object System.Windows.Forms.ContextMenuStrip
$miOpen = $trayMenu.Items.Add('Открыть')
$miSettings = $trayMenu.Items.Add('Настройки')
$trayMenu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
$miExit = $trayMenu.Items.Add('Выход')
$notify.ContextMenuStrip = $trayMenu

$showForm = {
    $form.Show()
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.Activate()
}
$notify.Add_DoubleClick($showForm)
$miOpen.Add_Click($showForm)
$miSettings.Add_Click({ $showForm.Invoke(); Show-Settings })
$miExit.Add_Click({
    $script:exiting = $true
    $notify.Visible = $false
    $notify.Dispose()
    $form.Close()
})

$script:traymsg = $false
$form.Add_Resize({
    try {
        if ($form.WindowState -eq [System.Windows.Forms.FormWindowState]::Minimized) {
            $form.Hide()
            $notify.Visible = $true
            if (-not $script:traymsg) {
                $script:traymsg = $true
                $notify.ShowBalloonTip(1500, 'Antigravity Proxy', 'Свернуто в трей. Двойной клик — открыть.', [System.Windows.Forms.ToolTipIcon]::None)
            }
        }
    } catch {
        Write-Log ('RESIZE: ' + $_.Exception.ToString())
    }
})

$form.Add_FormClosing({
    if (-not $script:exiting) {
        $notify.Visible = $false
        $notify.Dispose()
    }
})

# ---------- события ----------
$btnTheme.Add_Click({
    $script:Settings.theme = if ($script:Settings.theme -eq 'dark') { 'light' } else { 'dark' }
    Save-Settings
    $script:P = Get-Palette $script:Settings.theme
    Apply-Theme
    Update-Status
    Update-Patch
})

$btnSettings.Add_Click({ Show-Settings })
$btnTest.Add_Click({ Update-Status })

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
        Update-Status
    } catch {
        $lblStatus.Text = 'Не удалось запустить: ' + $_.Exception.Message
        $lblStatus.ForeColor = $script:P.err
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
            '--no-first-run', '--no-default-browser-check',
            'https://aistudio.google.com/')
    } catch { [System.Windows.Forms.MessageBox]::Show('Не удалось открыть браузер: ' + $_.Exception.Message) }
})

function Set-ProxyEnv {
    $bypass = 'localhost,127.0.0.1,::1,' + $env:COMPUTERNAME
    $env:HTTP_PROXY = 'http://127.0.0.1:10809'
    $env:HTTPS_PROXY = 'http://127.0.0.1:10809'
    $env:ALL_PROXY = 'http://127.0.0.1:10809'
    $env:NO_PROXY = $bypass
    $env:no_proxy = $bypass
}

$btnOcGui.Add_Click({
    try {
        if (-not $opencode) { [System.Windows.Forms.MessageBox]::Show('OpenCode GUI не найден.'); return }
        if (-not (Test-Port)) { Restart-Xray | Out-Null }
        Set-ProxyEnv
        Start-Process -FilePath $opencode
    } catch { [System.Windows.Forms.MessageBox]::Show('Не удалось запустить OpenCode GUI: ' + $_.Exception.Message) }
})

$btnOcCli.Add_Click({
    try {
        if (-not $ocCli) { [System.Windows.Forms.MessageBox]::Show('OpenCode CLI не найден.'); return }
        if (-not (Test-Port)) { Restart-Xray | Out-Null }
        Set-ProxyEnv
        Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoExit', '-NoProfile', '-Command', ('& "' + $ocCli + '"'))
    } catch { [System.Windows.Forms.MessageBox]::Show('Не удалось запустить OpenCode CLI: ' + $_.Exception.Message) }
})

$btnPatch.Add_Click({
    if (-not $patcher) { [System.Windows.Forms.MessageBox]::Show('Патчер не найден в «Загрузках».'); return }
    try {
        $pa = @()
        if ($ag.Dir) { $pa = @(('"{0}"' -f $ag.Dir)) }
        Start-Process -FilePath $patcher -ArgumentList $pa -Verb RunAs
        $lblPatch.Text = 'Патчер запущен — выберите PATCH'
        $lblPatch.ForeColor = $script:P.warn
    } catch { $lblPatch.Text = 'Не удалось запустить патчер'; $lblPatch.ForeColor = $script:P.err }
})

$btnSysProxy.Add_Click({
    try {
        Set-SystemProxy (-not (Test-SystemProxy))
        Update-SysProxyButton
    } catch { }
})

# ---------- старт ----------
Apply-Theme
$form.Add_Shown({
    $form.Activate(); $form.TopMost = $true; $form.TopMost = $false
    if (-not (Test-Port)) { Restart-Xray | Out-Null }
    Update-Status
    Update-Patch
    if ($env:ANTIGRAVITY_PROXY_OPEN_SETTINGS -eq '1') { Show-Settings }
})
[System.Windows.Forms.Application]::Run($form)
