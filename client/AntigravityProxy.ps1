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

$script:NeedScale = $false
$script:K = 1.0
try {
    $dpiApi = Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);' -Name 'DpiApi' -Namespace 'Win' -PassThru
    $unaware = [Win.DpiApi]::SetProcessDpiAwarenessContext([IntPtr](-1))
    if (-not $unaware) { $script:NeedScale = $true }
} catch {
    $script:NeedScale = $true
}
if ($script:NeedScale) {
    try {
        $gfx = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero)
        $script:K = [double]$gfx.DpiX / 96.0
        $gfx.Dispose()
    } catch { $script:K = 1.0 }
    if ($script:K -lt 1.0) { $script:K = 1.0 }
}

function Scale-Fonts([object]$ctrl) {
    foreach ($c in $ctrl.Controls) {
        try {
            if ($c.Font) {
                $c.Font = New-Object System.Drawing.Font($c.Font.FontFamily, [float]($c.Font.Size * $script:K), $c.Font.Style)
            }
        } catch { }
        Scale-Fonts $c
    }
}

function Scale-Form([object]$f) {
    if (-not $script:NeedScale) { return }
    if ([Math]::Abs($script:K - 1.0) -lt 0.01) { return }
    $f.Scale((New-Object System.Drawing.SizeF([float]$script:K, [float]$script:K)))
    # шрифты уже масштабируются системой (процесс DPI-aware), размеры масштабируем сами
}
# ---------- скруглённые кнопки ----------
try {
    Add-Type -ReferencedAssemblies System.Drawing, System.Windows.Forms -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

public class UiButton : Button
{
    public int Radius { get; set; }

    private Color _fill = Color.FromArgb(37, 99, 235);
    public Color Fill
    {
        get { return _fill; }
        set { _fill = value; BackColor = value; Invalidate(); }
    }

    private Color _hover = Color.FromArgb(59, 130, 246);
    public Color Hover
    {
        get { return _hover; }
        set { _hover = value; Invalidate(); }
    }

    private Color _pressed = Color.FromArgb(29, 78, 216);
    public Color Pressed
    {
        get { return _pressed; }
        set { _pressed = value; Invalidate(); }
    }

    private Color _borderColor = Color.Empty;
    public Color BorderColor
    {
        get { return _borderColor; }
        set { _borderColor = value; Invalidate(); }
    }

    private Color _hoverBorderColor = Color.Empty;
    public Color HoverBorderColor
    {
        get { return _hoverBorderColor; }
        set { _hoverBorderColor = value; Invalidate(); }
    }

    private int _borderWidth = 0;
    public int BorderWidth
    {
        get { return _borderWidth; }
        set { _borderWidth = value; Invalidate(); }
    }

    private bool _isHovered = false;
    private bool _isPressed = false;

    public UiButton()
    {
        Radius = 7;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _isPressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _isPressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Не используем Region = new Region(path) — это предотвращает зубчатые не-антиалиасенные края GDI!
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Заливаем углы цветом родителя для идеального бесшовного скругления
        Color parentBg = Parent != null ? Parent.BackColor : BackColor;
        using (var pb = new SolidBrush(parentBg))
        {
            g.FillRectangle(pb, ClientRectangle);
        }

        if (Width <= 3 || Height <= 3) return;

        Color currentFill = Fill;
        if (!Enabled)
        {
            currentFill = Color.FromArgb(70, Fill);
        }
        else if (_isPressed && Pressed != Color.Empty)
        {
            currentFill = Pressed;
        }
        else if (_isHovered && Hover != Color.Empty)
        {
            currentFill = Hover;
        }

        Color currentBorder = BorderColor;
        if (_isHovered && HoverBorderColor != Color.Empty)
        {
            currentBorder = HoverBorderColor;
        }

        int r = Radius;
        if (r * 2 > Height) r = (int)(Height / 2);
        if (r * 2 > Width) r = (int)(Width / 2);

        // Плавная заливка скругленного прямоугольника
        using (var pathFill = GetRoundPath(new RectangleF(0f, 0f, Width, Height), r))
        using (var b = new SolidBrush(currentFill))
        {
            g.FillPath(b, pathFill);
        }

        // Четкая 1px обводка с половинным сдвигом
        if (BorderWidth > 0 && currentBorder != Color.Empty && currentBorder.A > 0)
        {
            using (var pathBorder = GetRoundPath(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), r))
            using (var pen = new Pen(currentBorder, 1f))
            {
                g.DrawPath(pen, pathBorder);
            }
        }

        // Текст с микро-сдвигом при нажатии для тактильного отклика
        var textRect = new Rectangle(0, 0, Width, Height);
        if (_isPressed) { textRect.Offset(0, 1); }

        Color textColor = Enabled ? ForeColor : Color.FromArgb(120, ForeColor);
        TextRenderer.DrawText(g, Text, Font, textRect, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    public static GraphicsPath GetRoundPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0) { path.AddRectangle(rect); return path; }
        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public class UiComboBox : ComboBox
{
    public Color ItemBackColor { get; set; }
    public Color ItemForeColor { get; set; }
    public Color SelectedItemBackColor { get; set; }
    public Color SelectedItemForeColor { get; set; }

    public UiComboBox()
    {
        this.DropDownStyle = ComboBoxStyle.DropDownList;
        this.DrawMode = DrawMode.OwnerDrawFixed;
        this.FlatStyle = FlatStyle.Flat;
        this.ItemHeight = 22;
        this.ItemBackColor = Color.FromArgb(33, 38, 47);
        this.ItemForeColor = Color.FromArgb(240, 243, 246);
        this.SelectedItemBackColor = Color.FromArgb(37, 99, 235);
        this.SelectedItemForeColor = Color.White;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= this.Items.Count) return;

        bool isSelected = (e.State & DrawItemState.Selected) != 0;
        Color bg = isSelected ? SelectedItemBackColor : ItemBackColor;
        Color fg = isSelected ? SelectedItemForeColor : ItemForeColor;

        using (SolidBrush b = new SolidBrush(bg))
        {
            e.Graphics.FillRectangle(b, e.Bounds);
        }

        string text = this.Items[e.Index] != null ? this.Items[e.Index].ToString() : string.Empty;
        using (SolidBrush bText = new SolidBrush(fg))
        using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
        {
            float x = (float)(e.Bounds.X + 6);
            float y = (float)e.Bounds.Y;
            float w = (float)Math.Max(0, e.Bounds.Width - 6);
            float h = (float)e.Bounds.Height;
            RectangleF rect = new RectangleF(x, y, w, h);
            e.Graphics.DrawString(text, this.Font, bText, rect, sf);
        }
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
$IconPath = if ($PSScriptRoot) { Join-Path $PSScriptRoot 'AntigravityProxy.ico' } else { 'AntigravityProxy.ico' }

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
            card = [System.Drawing.Color]::FromArgb(28, 32, 40)
            ink = [System.Drawing.Color]::FromArgb(240, 243, 246)
            muted = [System.Drawing.Color]::FromArgb(140, 149, 159)
            border = [System.Drawing.Color]::FromArgb(48, 54, 61)
            accent = [System.Drawing.Color]::FromArgb(37, 99, 235)
            accentHover = [System.Drawing.Color]::FromArgb(59, 130, 246)
            accentPressed = [System.Drawing.Color]::FromArgb(29, 78, 216)
            ok = [System.Drawing.Color]::FromArgb(46, 160, 67)
            warn = [System.Drawing.Color]::FromArgb(210, 153, 34)
            err = [System.Drawing.Color]::FromArgb(248, 81, 73)
            secondaryFill = [System.Drawing.Color]::FromArgb(33, 38, 47)
            secondaryHover = [System.Drawing.Color]::FromArgb(46, 53, 65)
            secondaryPressed = [System.Drawing.Color]::FromArgb(26, 30, 38)
            secondaryBorder = [System.Drawing.Color]::FromArgb(56, 63, 76)
            secondaryHoverBorder = [System.Drawing.Color]::FromArgb(99, 110, 130)
            secondaryText = [System.Drawing.Color]::FromArgb(226, 232, 240)
        }
    }
    return @{
        bg = [System.Drawing.Color]::FromArgb(246, 248, 250)
        card = [System.Drawing.Color]::White
        ink = [System.Drawing.Color]::FromArgb(24, 32, 44)
        muted = [System.Drawing.Color]::FromArgb(100, 116, 139)
        border = [System.Drawing.Color]::FromArgb(226, 232, 240)
        accent = [System.Drawing.Color]::FromArgb(37, 99, 235)
        accentHover = [System.Drawing.Color]::FromArgb(29, 78, 216)
        accentPressed = [System.Drawing.Color]::FromArgb(30, 64, 175)
        ok = [System.Drawing.Color]::FromArgb(16, 185, 129)
        warn = [System.Drawing.Color]::FromArgb(217, 119, 6)
        err = [System.Drawing.Color]::FromArgb(239, 68, 68)
        secondaryFill = [System.Drawing.Color]::FromArgb(248, 250, 252)
        secondaryHover = [System.Drawing.Color]::FromArgb(241, 245, 249)
        secondaryPressed = [System.Drawing.Color]::FromArgb(226, 232, 240)
        secondaryBorder = [System.Drawing.Color]::FromArgb(203, 213, 225)
        secondaryHoverBorder = [System.Drawing.Color]::FromArgb(148, 163, 184)
        secondaryText = [System.Drawing.Color]::FromArgb(30, 41, 59)
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

# ---------- мульти-прокси ----------
function Ensure-ProxySettings {
    $existing = @()
    if ($script:Settings -and $script:Settings.proxies) {
        $existing = @($script:Settings.proxies)
    }
    if ($existing.Count -eq 0) {
        $cur = Get-ProxyInfo
        if ($cur -and $cur.host) {
            $existing = @([PSCustomObject]@{
                name = 'Основной'
                host = [string]$cur.host
                port = [string]$cur.port
                user = [string]$cur.user
                pass = [string]$cur.pass
            })
        } else {
            $existing = @([PSCustomObject]@{
                name = 'Основной'
                host = ''
                port = '8000'
                user = ''
                pass = ''
            })
        }
    }
    $idx = 0
    if ($script:Settings -and ($null -ne $script:Settings.activeProxyIndex)) {
        try {
            $idx = [int]$script:Settings.activeProxyIndex
            if ($idx -lt 0 -or $idx -ge $existing.Count) { $idx = 0 }
        } catch { $idx = 0 }
    }
    $script:Settings = [PSCustomObject]@{
        theme = if ($script:Settings -and $script:Settings.theme) { $script:Settings.theme } else { 'dark' }
        activeProxyIndex = $idx
        proxies = $existing
    }
    Save-Settings
}

function Get-ActiveProxy {
    $arr = @($script:Settings.proxies)
    if ($arr.Count -eq 0) { return $null }
    $idx = 0
    if ($script:Settings -and ($null -ne $script:Settings.activeProxyIndex)) {
        try {
            $idx = [int]$script:Settings.activeProxyIndex
            if ($idx -lt 0 -or $idx -ge $arr.Count) { $idx = 0 }
        } catch { $idx = 0 }
    }
    return $arr[$idx]
}

function Switch-ActiveProxy([int]$newIndex) {
    $arr = @($script:Settings.proxies)
    if ($newIndex -lt 0 -or $newIndex -ge $arr.Count) { return }
    $script:Settings.activeProxyIndex = $newIndex
    Save-Settings
    $p = $arr[$newIndex]
    if ($p -and $p.host) {
        $lblStatus.Text = 'Переключение прокси…'
        $lblStatus.ForeColor = $script:P.warn
        $form.Update()
        try {
            Write-ProxyConfig $p.host $p.port $p.user $p.pass
            Restart-Xray | Out-Null
        } catch { }
        Update-Status
    }
}

function Refresh-ProxyComboBox {
    if (-not $cbActiveProxy) { return }
    $script:updatingProxyCb = $true
    try {
        $cbActiveProxy.Items.Clear()
        $arr = @($script:Settings.proxies)
        for ($i = 0; $i -lt $arr.Count; $i++) {
            $px = $arr[$i]
            $n = if ($px.name) { $px.name } else { "Прокси $($i + 1)" }
            $h = if ($px.host) { "$($px.host):$($px.port)" } else { "не настроен" }
            $cbActiveProxy.Items.Add("$n ($h)") | Out-Null
        }
        $idx = 0
        if ($script:Settings -and ($null -ne $script:Settings.activeProxyIndex)) {
            $idx = [int]$script:Settings.activeProxyIndex
            if ($idx -lt 0 -or $idx -ge $arr.Count) { $idx = 0 }
        }
        if ($arr.Count -gt 0) {
            $cbActiveProxy.SelectedIndex = $idx
        }
    } finally {
        $script:updatingProxyCb = $false
    }
}

function Test-DirectProxyInfo([string]$h, [string]$pt, [string]$u, [string]$pw) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $proxy = New-Object System.Net.WebProxy(('http://{0}:{1}' -f $h, $pt))
        if ($u) {
            $proxy.Credentials = New-Object System.Net.NetworkCredential($u, $pw)
        }
        $req = [System.Net.HttpWebRequest]::Create('https://ipinfo.io/json')
        $req.Proxy = $proxy
        $req.Timeout = 7000
        $req.ReadWriteTimeout = 7000
        $resp = $req.GetResponse()
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $text = $reader.ReadToEnd()
        $reader.Close()
        $resp.Close()
        $sw.Stop()
        $j = $text | ConvertFrom-Json
        return @{ ok = $true; ip = $j.ip; country = $j.country; city = $j.city; ms = [Math]::Round($sw.Elapsed.TotalMilliseconds) }
    } catch {
        $sw.Stop()
        $errMsg = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message }
        return @{ ok = $false; error = $errMsg; ms = [Math]::Round($sw.Elapsed.TotalMilliseconds) }
    }
}

function Measure-ProxySpeed([string]$h = '', [string]$pt = '', [string]$u = '', [string]$pw = '', [int]$bytes = 524288) {
    try {
        $proxy = $null
        if ($h -and $pt) {
            $proxy = New-Object System.Net.WebProxy(('http://{0}:{1}' -f $h, $pt))
            if ($u) {
                $proxy.Credentials = New-Object System.Net.NetworkCredential($u, $pw)
            }
        } else {
            $proxy = New-Object System.Net.WebProxy('http://127.0.0.1:10809')
        }

        $nocache = [Guid]::NewGuid().ToString('N')
        $url = ('https://speed.cloudflare.com/__down?bytes={0}&nocache={1}' -f $bytes, $nocache)
        $req = [System.Net.HttpWebRequest]::Create($url)
        $req.Proxy = $proxy
        $req.KeepAlive = $false
        $req.Headers.Add('Cache-Control', 'no-cache, no-store')
        $req.Headers.Add('Pragma', 'no-cache')
        $req.Timeout = 10000
        $req.ReadWriteTimeout = 10000

        # 1. Замер задержки ответа (TTFB / пинг)
        $swPing = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = $req.GetResponse()
        $swPing.Stop()
        $pingMs = [Math]::Round($swPing.Elapsed.TotalMilliseconds)

        # 2. Замер чистой скорости загрузки потока данных (Throughput)
        $swDl = [System.Diagnostics.Stopwatch]::StartNew()
        $stream = $resp.GetResponseStream()
        $buf = New-Object byte[] 32768
        $total = 0
        while (($read = $stream.Read($buf, 0, $buf.Length)) -gt 0) {
            $total += $read
        }
        $stream.Close()
        $resp.Close()
        $swDl.Stop()

        $sec = $swDl.Elapsed.TotalSeconds
        if ($sec -le 0.005) { $sec = 0.005 }
        $mbps = [Math]::Round((($total * 8) / 1000000) / $sec, 2)
        return @{ ok = $true; mbps = $mbps; ping = $pingMs; bytes = $total; sec = [Math]::Round($sec, 2) }
    } catch {
        $errMsg = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message }
        return @{ ok = $false; error = $errMsg }
    }
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
$form.ClientSize = New-Object System.Drawing.Size(520, 526)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.ShowInTaskbar = $true
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$form.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::None
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
    $l.BackColor = [System.Drawing.Color]::Transparent
    $fname = if ($bold) { 'Segoe UI Semibold' } else { 'Segoe UI' }
    $l.Font = New-Object System.Drawing.Font($fname, $size)
    $l.ForeColor = if ($role -eq 'muted') { $script:P.muted } else { $script:P.ink }
    return $l
}

# шапка
$lblTitle = New-Lbl 'Antigravity Proxy' 12 10 200 26 'ink' 12 $true
$form.Controls.Add($lblTitle)
$lblSub = New-Lbl 'прокси, патч и запуск' 14 36 200 18 'muted' 8.5
$form.Controls.Add($lblSub)

$btnSettings = New-Object UiButton
$btnSettings.Text = 'Настройки'
$btnSettings.Location = New-Object System.Drawing.Point(284, 12)
$btnSettings.Size = New-Object System.Drawing.Size(108, 32)
$btnSettings.Radius = 7
$form.Controls.Add($btnSettings)
$btnSettings.BringToFront()

$btnTheme = New-Object UiButton
$btnTheme.Text = ''
$btnTheme.Location = New-Object System.Drawing.Point(398, 12)
$btnTheme.Size = New-Object System.Drawing.Size(106, 32)
$btnTheme.Radius = 7
$form.Controls.Add($btnTheme)
$btnTheme.BringToFront()

# карточка статуса
$card1 = New-Object System.Windows.Forms.Panel
$card1.Location = New-Object System.Drawing.Point(16, 64)
$card1.Size = New-Object System.Drawing.Size(488, 122)
$card1.Tag = 'card'
$form.Controls.Add($card1)

$lblStatus = New-Lbl 'Запускаю прокси…' 16 14 450 24 'ink' 10.5 $true
$card1.Controls.Add($lblStatus)
$lblDetail = New-Lbl '' 16 40 450 34 'muted' 8.5
$card1.Controls.Add($lblDetail)

$btnTest = New-Object UiButton
$btnTest.Text = '↻ Статус'
$btnTest.Location = New-Object System.Drawing.Point(16, 80)
$btnTest.Size = New-Object System.Drawing.Size(96, 28)
$btnTest.Radius = 6
$card1.Controls.Add($btnTest)

$btnSpeed = New-Object UiButton
$btnSpeed.Text = '⚡ Скорость'
$btnSpeed.Location = New-Object System.Drawing.Point(118, 80)
$btnSpeed.Size = New-Object System.Drawing.Size(104, 28)
$btnSpeed.Radius = 6
$card1.Controls.Add($btnSpeed)

$cbActiveProxy = New-Object UiComboBox
$cbActiveProxy.Location = New-Object System.Drawing.Point(228, 80)
$cbActiveProxy.Size = New-Object System.Drawing.Size(244, 28)
$cbActiveProxy.DropDownWidth = 280
$cbActiveProxy.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$cbActiveProxy.Tag = 'combobox'
$card1.Controls.Add($cbActiveProxy)

$script:updatingProxyCb = $false
$cbActiveProxy.Add_SelectedIndexChanged({
    if (-not $script:updatingProxyCb -and $cbActiveProxy.SelectedIndex -ge 0) {
        if ($cbActiveProxy.SelectedIndex -ne [int]$script:Settings.activeProxyIndex) {
            Switch-ActiveProxy $cbActiveProxy.SelectedIndex
        }
    }
})

$btnSpeed.Add_Click({
    try {
        if (-not (Test-Port)) {
            $lblStatus.Text = 'Прокси не запущен'
            $lblStatus.ForeColor = $script:P.err
            return
        }
        $btnSpeed.Text = '⚡ Замер…'
        $lblStatus.Text = 'Замер скорости…'
        $lblStatus.ForeColor = $script:P.warn
        $form.Update()
        $res = Measure-ProxySpeed
        if ($res.ok) {
            $lblStatus.Text = 'Прокси работает'
            $lblStatus.ForeColor = $script:P.ok
            $active = Get-ActiveProxy
            $pName = if ($active -and $active.name) { "$($active.name)" } else { "Прокси" }
            $lblDetail.Text = "${pName}: $($res.mbps) Мбит/с · задержка $($res.ping) мс`r`nТест Cloudflare CDN (512 КБ)"
        } else {
            $lblStatus.Text = 'Ошибка замера скорости'
            $lblStatus.ForeColor = $script:P.warn
            $lblDetail.Text = 'Не удалось загрузить данные: ' + $res.error
        }
    } catch {
        Write-Log ('BTN_SPEED: ' + $_.Exception.ToString())
        $lblStatus.Text = 'Ошибка замера'
        $lblStatus.ForeColor = $script:P.err
    } finally {
        $btnSpeed.Text = '⚡ Скорость'
    }
})

# действия
$btnStart = New-Object UiButton
$btnStart.Text = '▶ Запустить Antigravity'
$btnStart.Location = New-Object System.Drawing.Point(16, 196)
$btnStart.Size = New-Object System.Drawing.Size(488, 42)
$btnStart.Radius = 8
$btnStart.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 10)
$form.Controls.Add($btnStart)

$btnBrowser = New-Object UiButton
$btnBrowser.Text = '🌐 AI Studio (Web)'
$btnBrowser.Location = New-Object System.Drawing.Point(16, 246)
$btnBrowser.Size = New-Object System.Drawing.Size(240, 36)
$btnBrowser.Radius = 7
$btnBrowser.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$form.Controls.Add($btnBrowser)

$btnTelegram = New-Object UiButton
$btnTelegram.Text = '✈ Telegram (Прокси)'
$btnTelegram.Location = New-Object System.Drawing.Point(264, 246)
$btnTelegram.Size = New-Object System.Drawing.Size(240, 36)
$btnTelegram.Radius = 7
$btnTelegram.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$form.Controls.Add($btnTelegram)

$btnOcGui = New-Object UiButton
$btnOcGui.Text = '⚡ OpenCode GUI'
$btnOcGui.Location = New-Object System.Drawing.Point(16, 288)
$btnOcGui.Size = New-Object System.Drawing.Size(240, 34)
$btnOcGui.Radius = 7
$btnOcGui.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$form.Controls.Add($btnOcGui)

$btnOcCli = New-Object UiButton
$btnOcCli.Text = '>_ OpenCode CLI'
$btnOcCli.Location = New-Object System.Drawing.Point(264, 288)
$btnOcCli.Size = New-Object System.Drawing.Size(240, 34)
$btnOcCli.Radius = 7
$btnOcCli.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$form.Controls.Add($btnOcCli)

# карточка состояния
$card2 = New-Object System.Windows.Forms.Panel
$card2.Location = New-Object System.Drawing.Point(16, 332)
$card2.Size = New-Object System.Drawing.Size(488, 150)
$card2.Tag = 'card'
$form.Controls.Add($card2)

$lblPatch = New-Lbl 'Патч: проверка…' 16 14 300 20 'muted' 9
$card2.Controls.Add($lblPatch)

$btnPatch = New-Object UiButton
$btnPatch.Text = 'Патч'
$btnPatch.Location = New-Object System.Drawing.Point(380, 10)
$btnPatch.Size = New-Object System.Drawing.Size(92, 28)
$btnPatch.Radius = 6
$card2.Controls.Add($btnPatch)

$lblApp = New-Lbl '' 16 44 450 44 'muted' 8
$card2.Controls.Add($lblApp)

$btnSysProxy = New-Object UiButton
$btnSysProxy.Text = 'Системный прокси: выкл'
$btnSysProxy.Location = New-Object System.Drawing.Point(16, 106)
$btnSysProxy.Size = New-Object System.Drawing.Size(250, 32)
$btnSysProxy.Radius = 7
$card2.Controls.Add($btnSysProxy)

$lblFoot = New-Lbl 'Прокси-настройки, тема и пути — в «Настройках».' 18 494 480 18 'muted' 8.5
$form.Controls.Add($lblFoot)

function Update-SysProxyButton {
    $on = Test-SystemProxy
    $btnSysProxy.Text = if ($on) { '✓ Системный прокси: вкл' } else { '✕ Системный прокси: выкл' }
    if ($on) {
        $btnSysProxy.Fill = $script:P.accent
        $btnSysProxy.Hover = $script:P.accentHover
        $btnSysProxy.Pressed = $script:P.accentPressed
        $btnSysProxy.BorderWidth = 0
        $btnSysProxy.ForeColor = [System.Drawing.Color]::White
    } else {
        $btnSysProxy.Fill = $script:P.secondaryFill
        $btnSysProxy.Hover = $script:P.secondaryHover
        $btnSysProxy.Pressed = $script:P.secondaryPressed
        $btnSysProxy.BorderColor = $script:P.secondaryBorder
        $btnSysProxy.HoverBorderColor = $script:P.secondaryHoverBorder
        $btnSysProxy.BorderWidth = 1
        $btnSysProxy.ForeColor = $script:P.secondaryText
    }
    $btnSysProxy.Invalidate()
}
# ---------- тема ----------
function Apply-Theme {
    $p = $script:P
    $form.BackColor = $p.bg
    foreach ($c in @($lblTitle, $lblSub, $lblStatus, $lblDetail, $lblPatch, $lblApp, $lblFoot)) {
        if (-not $c) { continue }
        $c.BackColor = [System.Drawing.Color]::Transparent
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
    foreach ($b in @($btnTest, $btnSpeed, $btnBrowser, $btnTelegram, $btnOcGui, $btnOcCli, $btnPatch, $btnTheme, $btnSettings)) {
        if (-not $b) { continue }
        $b.Fill = $p.secondaryFill
        $b.Hover = $p.secondaryHover
        $b.Pressed = $p.secondaryPressed
        $b.BorderColor = $p.secondaryBorder
        $b.HoverBorderColor = $p.secondaryHoverBorder
        $b.BorderWidth = 1
        $b.ForeColor = $p.secondaryText
    }
    $btnStart.Fill = $p.accent
    $btnStart.Hover = $p.accentHover
    $btnStart.Pressed = $p.accentPressed
    $btnStart.BorderWidth = 0
    $btnStart.ForeColor = [System.Drawing.Color]::White
    $btnTheme.Text = if ($script:Settings.theme -eq 'dark') { '☀ Светлая' } else { '🌙 Тёмная' }
    $btnTheme.ForeColor = $p.ink
    if ($cbActiveProxy) {
        $cbActiveProxy.BackColor = $p.secondaryFill
        $cbActiveProxy.ForeColor = $p.ink
        $cbActiveProxy.ItemBackColor = $p.secondaryFill
        $cbActiveProxy.ItemForeColor = $p.ink
        $cbActiveProxy.SelectedItemBackColor = $p.accent
        $cbActiveProxy.SelectedItemForeColor = [System.Drawing.Color]::White
        $cbActiveProxy.Invalidate()
    }
    Update-SysProxyButton
    $form.Invalidate($true)
}

function Update-Status {
    if (Test-Port) {
        $active = Get-ActiveProxy
        $proxyName = if ($active -and $active.name) { "$($active.name) · " } else { '' }
        $where = if ($active -and $active.host) { "$($active.host):$($active.port)" } else {
            $info = Get-ProxyInfo
            if ($info) { "$($info.host):$($info.port)" } else { '—' }
        }
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $j = Invoke-RestMethod -Uri 'https://ipinfo.io/json' -Proxy 'http://127.0.0.1:10809' -TimeoutSec 15
            $sw.Stop()
            $ping = [Math]::Round($sw.Elapsed.TotalMilliseconds)
            $lblStatus.Text = 'Прокси работает'
            $lblStatus.ForeColor = $script:P.ok
            $lblDetail.Text = "Выход: $($j.ip) · $($j.country), $($j.city) · пинг $ping мс`r`nПрокси: $proxyName$where"
        } catch {
            $lblStatus.Text = 'Прокси работает, проверка не удалась'
            $lblStatus.ForeColor = $script:P.warn
            $lblDetail.Text = "Прокси: $proxyName$where"
        }
    } else {
        $lblStatus.Text = 'Прокси не запущен'
        $lblStatus.ForeColor = $script:P.err
        $lblDetail.Text = 'Откройте «Настройки», проверьте адрес прокси и нажмите «Сохранить».'
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
    $sf.ClientSize = New-Object System.Drawing.Size(460, 500)
    $sf.StartPosition = 'CenterParent'
    $sf.FormBorderStyle = 'FixedDialog'
    $sf.MaximizeBox = $false
    $sf.MinimizeBox = $false
    $sf.BackColor = $p.bg
    $sf.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $sf.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::None
    if ($form.Icon) { $sf.Icon = $form.Icon }

    $localProxies = [System.Collections.ArrayList]@()
    foreach ($px in @($script:Settings.proxies)) {
        $localProxies.Add([PSCustomObject]@{
            name = [string]$px.name
            host = [string]$px.host
            port = [string]$px.port
            user = [string]$px.user
            pass = [string]$px.pass
        }) | Out-Null
    }
    if ($localProxies.Count -eq 0) {
        $localProxies.Add([PSCustomObject]@{
            name = 'Основной'
            host = ''
            port = '8000'
            user = ''
            pass = ''
        }) | Out-Null
    }
    $script:localActiveIdx = [int]$script:Settings.activeProxyIndex
    if ($script:localActiveIdx -lt 0 -or $script:localActiveIdx -ge $localProxies.Count) { $script:localActiveIdx = 0 }
    $script:currentIdx = $script:localActiveIdx
    $script:updatingSettingsCb = $false

    $sf.Controls.Add((New-Lbl 'Настройки прокси' 18 14 200 22 'ink' 10.5 $true))

    $cbList = New-Object UiComboBox
    $cbList.Location = New-Object System.Drawing.Point(18, 42)
    $cbList.Size = New-Object System.Drawing.Size(236, 28)
    $cbList.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $cbList.BackColor = $p.secondaryFill
    $cbList.ForeColor = $p.ink
    $cbList.ItemBackColor = $p.secondaryFill
    $cbList.ItemForeColor = $p.ink
    $cbList.SelectedItemBackColor = $p.accent
    $cbList.SelectedItemForeColor = [System.Drawing.Color]::White
    $sf.Controls.Add($cbList)

    $btnAdd = New-Object UiButton
    $btnAdd.Text = '+ Добавить'
    $btnAdd.Location = New-Object System.Drawing.Point(262, 41)
    $btnAdd.Size = New-Object System.Drawing.Size(96, 28)
    $btnAdd.Radius = 6
    $btnAdd.Fill = $p.secondaryFill; $btnAdd.Hover = $p.secondaryHover; $btnAdd.Pressed = $p.secondaryPressed
    $btnAdd.BorderColor = $p.secondaryBorder; $btnAdd.HoverBorderColor = $p.accent; $btnAdd.BorderWidth = 1
    $btnAdd.ForeColor = $p.secondaryText
    $sf.Controls.Add($btnAdd)

    $btnDel = New-Object UiButton
    $btnDel.Text = 'Удалить'
    $btnDel.Location = New-Object System.Drawing.Point(366, 41)
    $btnDel.Size = New-Object System.Drawing.Size(76, 28)
    $btnDel.Radius = 6
    $btnDel.Fill = $p.secondaryFill; $btnDel.Hover = $p.secondaryHover; $btnDel.Pressed = $p.secondaryPressed
    $btnDel.BorderColor = $p.secondaryBorder; $btnDel.HoverBorderColor = $p.err; $btnDel.BorderWidth = 1
    $btnDel.ForeColor = $p.secondaryText
    $sf.Controls.Add($btnDel)

    $sf.Controls.Add((New-Lbl 'Имя профиля:' 18 76 200 16 'muted' 8.5))
    $tbName = New-Txt 18 94 424; $tbName.BackColor = $p.card; $tbName.ForeColor = $p.ink; $sf.Controls.Add($tbName)

    $sf.Controls.Add((New-Lbl 'Сервер / Хост:' 18 124 120 16 'muted' 8.5))
    $tbH = New-Txt 18 142 260; $tbH.BackColor = $p.card; $tbH.ForeColor = $p.ink; $sf.Controls.Add($tbH)

    $sf.Controls.Add((New-Lbl 'Порт:' 290 124 60 16 'muted' 8.5))
    $tbPt = New-Txt 290 142 152; $tbPt.BackColor = $p.card; $tbPt.ForeColor = $p.ink; $sf.Controls.Add($tbPt)

    $sf.Controls.Add((New-Lbl 'Логин:' 18 172 120 16 'muted' 8.5))
    $tbU = New-Txt 18 190 260; $tbU.BackColor = $p.card; $tbU.ForeColor = $p.ink; $sf.Controls.Add($tbU)

    $sf.Controls.Add((New-Lbl 'Пароль:' 290 172 60 16 'muted' 8.5))
    $tbPw = New-Txt 290 190 152; $tbPw.UseSystemPasswordChar = $true; $tbPw.BackColor = $p.card; $tbPw.ForeColor = $p.ink; $sf.Controls.Add($tbPw)

    $btnActive = New-Object UiButton
    $btnActive.Text = 'Сделать активным'
    $btnActive.Location = New-Object System.Drawing.Point(18, 226)
    $btnActive.Size = New-Object System.Drawing.Size(200, 32)
    $btnActive.Radius = 6
    $sf.Controls.Add($btnActive)

    $btnTestSettings = New-Object UiButton
    $btnTestSettings.Text = '↻ Проверить этот прокси'
    $btnTestSettings.Location = New-Object System.Drawing.Point(226, 226)
    $btnTestSettings.Size = New-Object System.Drawing.Size(216, 32)
    $btnTestSettings.Radius = 6
    $btnTestSettings.Fill = $p.secondaryFill; $btnTestSettings.Hover = $p.secondaryHover; $btnTestSettings.Pressed = $p.secondaryPressed
    $btnTestSettings.BorderColor = $p.secondaryBorder; $btnTestSettings.HoverBorderColor = $p.secondaryHoverBorder; $btnTestSettings.BorderWidth = 1
    $btnTestSettings.ForeColor = $p.secondaryText
    $sf.Controls.Add($btnTestSettings)

    $sf.Controls.Add((New-Lbl 'Тема оформления' 18 270 200 20 'ink' 10 $true))
    $rbDark = New-Object System.Windows.Forms.RadioButton
    $rbDark.Text = 'Тёмная'; $rbDark.Location = New-Object System.Drawing.Point(20, 296); $rbDark.Size = New-Object System.Drawing.Size(100, 22)
    $rbDark.ForeColor = $p.ink
    $rbLight = New-Object System.Windows.Forms.RadioButton
    $rbLight.Text = 'Светлая'; $rbLight.Location = New-Object System.Drawing.Point(130, 296); $rbLight.Size = New-Object System.Drawing.Size(100, 22)
    $rbLight.ForeColor = $p.ink
    if ($script:Settings.theme -eq 'dark') { $rbDark.Checked = $true } else { $rbLight.Checked = $true }
    $sf.Controls.Add($rbDark); $sf.Controls.Add($rbLight)

    $lblNote = New-Lbl 'Выберите нужный прокси, отредактируйте параметры и нажмите «Сохранить».' 18 330 424 44 'muted' 8.5
    $sf.Controls.Add($lblNote)

    $btnSave = New-Object UiButton
    $btnSave.Text = 'Сохранить'
    $btnSave.Location = New-Object System.Drawing.Point(18, 444)
    $btnSave.Size = New-Object System.Drawing.Size(130, 38)
    $btnSave.Radius = 7
    $btnSave.Fill = $p.accent; $btnSave.Hover = $p.accentHover; $btnSave.Pressed = $p.accentPressed
    $btnSave.BorderWidth = 0
    $btnSave.ForeColor = [System.Drawing.Color]::White
    $sf.Controls.Add($btnSave)

    $btnCancel = New-Object UiButton
    $btnCancel.Text = 'Отмена'
    $btnCancel.Location = New-Object System.Drawing.Point(158, 444)
    $btnCancel.Size = New-Object System.Drawing.Size(110, 38)
    $btnCancel.Radius = 7
    $btnCancel.Fill = $p.secondaryFill; $btnCancel.Hover = $p.secondaryHover; $btnCancel.Pressed = $p.secondaryPressed
    $btnCancel.BorderColor = $p.secondaryBorder; $btnCancel.HoverBorderColor = $p.secondaryHoverBorder; $btnCancel.BorderWidth = 1
    $btnCancel.ForeColor = $p.secondaryText
    $sf.Controls.Add($btnCancel)

    $saveCurrentFields = {
        if ($script:currentIdx -ge 0 -and $script:currentIdx -lt $localProxies.Count) {
            $cur = $localProxies[$script:currentIdx]
            $cur.name = $tbName.Text.Trim()
            $cur.host = $tbH.Text.Trim()
            $cur.port = $tbPt.Text.Trim()
            $cur.user = $tbU.Text.Trim()
            $cur.pass = $tbPw.Text
        }
    }

    $updateActiveButton = {
        param([int]$idx)
        if ($idx -eq $script:localActiveIdx) {
            $btnActive.Text = '✓ Текущий активный'
            $btnActive.Fill = $p.accent
            $btnActive.Hover = $p.accentHover
            $btnActive.Pressed = $p.accentPressed
            $btnActive.BorderWidth = 0
            $btnActive.ForeColor = [System.Drawing.Color]::White
        } else {
            $btnActive.Text = 'Сделать активным'
            $btnActive.Fill = $p.secondaryFill
            $btnActive.Hover = $p.secondaryHover
            $btnActive.Pressed = $p.secondaryPressed
            $btnActive.BorderColor = $p.secondaryBorder
            $btnActive.HoverBorderColor = $p.secondaryHoverBorder
            $btnActive.BorderWidth = 1
            $btnActive.ForeColor = $p.secondaryText
        }
        $btnActive.Invalidate()
    }

    $loadCurrentFields = {
        param([int]$idx)
        if ($idx -ge 0 -and $idx -lt $localProxies.Count) {
            $cur = $localProxies[$idx]
            $tbName.Text = [string]$cur.name
            $tbH.Text = [string]$cur.host
            $tbPt.Text = [string]$cur.port
            $tbU.Text = [string]$cur.user
            $tbPw.Text = [string]$cur.pass
            & $updateActiveButton $idx
        }
    }

    $refreshSettingsList = {
        param([int]$selIdx)
        $script:updatingSettingsCb = $true
        try {
            $cbList.Items.Clear()
            for ($i = 0; $i -lt $localProxies.Count; $i++) {
                $item = $localProxies[$i]
                $star = if ($i -eq $script:localActiveIdx) { '★ ' } else { '' }
                $n = if ($item.name) { $item.name } else { "Прокси $($i + 1)" }
                $h = if ($item.host) { "$($item.host):$($item.port)" } else { "пусто" }
                $cbList.Items.Add("$star$n ($h)") | Out-Null
            }
            if ($selIdx -ge 0 -and $selIdx -lt $localProxies.Count) {
                $cbList.SelectedIndex = $selIdx
            } elseif ($localProxies.Count -gt 0) {
                $cbList.SelectedIndex = 0
            }
        } finally {
            $script:updatingSettingsCb = $false
        }
    }

    & $refreshSettingsList $script:currentIdx
    & $loadCurrentFields $script:currentIdx

    $cbList.Add_SelectedIndexChanged({
        if (-not $script:updatingSettingsCb -and $cbList.SelectedIndex -ge 0) {
            & $saveCurrentFields
            $script:currentIdx = $cbList.SelectedIndex
            & $loadCurrentFields $script:currentIdx
        }
    })

    $btnAdd.Add_Click({
        & $saveCurrentFields
        $newNum = $localProxies.Count + 1
        $localProxies.Add([PSCustomObject]@{
            name = "Прокси $newNum"
            host = ""
            port = "8000"
            user = ""
            pass = ""
        }) | Out-Null
        $script:currentIdx = $localProxies.Count - 1
        & $refreshSettingsList $script:currentIdx
        & $loadCurrentFields $script:currentIdx
        $tbName.Focus()
        $tbName.SelectAll()
        $lblNote.Text = 'Добавлен новый профиль прокси. Заполните данные и нажмите «Сохранить».'
        $lblNote.ForeColor = $p.muted
    })

    $btnDel.Add_Click({
        if ($localProxies.Count -le 1) {
            [System.Windows.Forms.MessageBox]::Show('Нельзя удалить единственный прокси.', 'Antigravity Proxy', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            return
        }
        $toRemove = $script:currentIdx
        $localProxies.RemoveAt($toRemove)
        if ($script:localActiveIdx -eq $toRemove) {
            $script:localActiveIdx = 0
        } elseif ($script:localActiveIdx -gt $toRemove) {
            $script:localActiveIdx--
        }
        if ($script:currentIdx -ge $localProxies.Count) {
            $script:currentIdx = $localProxies.Count - 1
        }
        & $refreshSettingsList $script:currentIdx
        & $loadCurrentFields $script:currentIdx
        $lblNote.Text = 'Профиль удален.'
        $lblNote.ForeColor = $p.muted
    })

    $btnActive.Add_Click({
        & $saveCurrentFields
        $script:localActiveIdx = $script:currentIdx
        & $refreshSettingsList $script:currentIdx
        & $updateActiveButton $script:currentIdx
        $nameCur = $localProxies[$script:currentIdx].name
        $lblNote.Text = "Прокси «$nameCur» выбран активным (нажмите «Сохранить» для применения)."
        $lblNote.ForeColor = $p.accent
    })

    $btnTestSettings.Add_Click({
        & $saveCurrentFields
        $cur = $localProxies[$script:currentIdx]
        if (-not $cur.host) {
            $lblNote.Text = 'Укажите адрес сервера перед проверкой.'
            $lblNote.ForeColor = $p.err
            return
        }
        $lblNote.Text = "Проверяю $($cur.name) ($($cur.host):$($cur.port))..."
        $lblNote.ForeColor = $p.warn
        $sf.Update()
        try {
            $info = Test-DirectProxyInfo $cur.host $cur.port $cur.user $cur.pass
            if ($info.ok) {
                $lblNote.Text = "✓ $($info.ip) ($($info.country), $($info.city)) · пинг $($info.ms) мс · замер скорости..."
                $lblNote.ForeColor = $p.ok
                $sf.Update()

                $sp = Measure-ProxySpeed $cur.host $cur.port $cur.user $cur.pass
                $spStr = if ($sp.ok) { " · $($sp.mbps) Мбит/с" } else { "" }
                $lblNote.Text = "✓ $($info.ip) ($($info.country), $($info.city)) · пинг $($info.ms) мс$spStr"
                $lblNote.ForeColor = $p.ok
            } else {
                $lblNote.Text = "Ошибка подключения ($($info.ms) мс): " + $info.error
                $lblNote.ForeColor = $p.err
            }
        } catch {
            $lblNote.Text = 'Ошибка проверки: ' + $_.Exception.Message
            $lblNote.ForeColor = $p.err
        }
    })

    $btnSave.Add_Click({
        & $saveCurrentFields
        $hasValid = $false
        foreach ($px in $localProxies) {
            if ($px.host -and $px.host.Trim().Length -gt 0) { $hasValid = $true; break }
        }
        if (-not $hasValid) {
            $lblNote.Text = 'Укажите хост/сервер хотя бы для одного прокси.'
            $lblNote.ForeColor = $p.err
            return
        }
        $script:Settings.proxies = @($localProxies)
        $script:Settings.activeProxyIndex = $script:localActiveIdx
        $script:Settings.theme = if ($rbDark.Checked) { 'dark' } else { 'light' }
        Save-Settings
        $sf.Close()

        $script:P = Get-Palette $script:Settings.theme
        Apply-Theme
        Refresh-ProxyComboBox

        $active = Get-ActiveProxy
        if ($active -and $active.host) {
            try {
                Write-ProxyConfig $active.host $active.port $active.user $active.pass
                Restart-Xray | Out-Null
            } catch { }
        }
        Update-Status
        Update-Patch
    })

    $btnCancel.Add_Click({
        $sf.Close()
    })

    Scale-Form $sf
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

$btnTelegram.Add_Click({
    try {
        if (-not (Test-Port 10809) -or -not (Test-Port 10808)) { Restart-Xray | Out-Null }
        Set-ProxyEnv
        $tgUrl = 'tg://socks?server=127.0.0.1&port=10808'
        $tgCmd = (Get-ItemProperty 'HKCU:\Software\Classes\tg\shell\open\command' -ErrorAction SilentlyContinue).'(Default)'
        $tgExe = $null
        if ($tgCmd -and ($tgCmd -match '"([^"]+Telegram\.exe)"')) {
            $tgExe = $matches[1]
        }
        if (-not $tgExe) {
            $candidates = @(
                (Join-Path $env:APPDATA 'Telegram Desktop\Telegram.exe'),
                (Join-Path $env:LOCALAPPDATA 'Telegram Desktop\Telegram.exe'),
                'C:\Program Files\Telegram Desktop\Telegram.exe',
                'C:\Program Files (x86)\Telegram Desktop\Telegram.exe'
            )
            foreach ($c in $candidates) { if (Test-Path $c) { $tgExe = $c; break } }
        }
        if ($tgExe -and (Test-Path $tgExe)) {
            Start-Process -FilePath $tgExe -ArgumentList @('--', ('"' + $tgUrl + '"'))
        } else {
            Start-Process -FilePath $tgUrl
        }
    } catch { [System.Windows.Forms.MessageBox]::Show('Не удалось запустить Telegram: ' + $_.Exception.Message) }
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
Ensure-ProxySettings
Scale-Form $form
Apply-Theme
Refresh-ProxyComboBox
$form.Add_Shown({
    $form.Activate(); $form.TopMost = $true; $form.TopMost = $false
    $active = Get-ActiveProxy
    $cur = Get-ProxyInfo
    if ($active -and $active.host -and (-not $cur -or $cur.host -ne $active.host -or $cur.port -ne $active.port)) {
        try { Write-ProxyConfig $active.host $active.port $active.user $active.pass } catch { }
    }
    if (-not (Test-Port)) { Restart-Xray | Out-Null }
    Update-Status
    Update-Patch
    if ($env:ANTIGRAVITY_PROXY_OPEN_SETTINGS -eq '1') { Show-Settings }
})
[System.Windows.Forms.Application]::Run($form)
