$ErrorActionPreference = 'Continue'
$src = 'C:\Users\Дима\Documents\deepseek-harness\default-workspace\NL-VPS\App'
if (-not (Test-Path $src)) { $src = $PSScriptRoot }
$dst = Join-Path $env:LOCALAPPDATA 'AntigravityProxy'
$xdir = Join-Path $env:LOCALAPPDATA 'XrayNL'
$log = Join-Path $xdir 'install-app.txt'
"" | Set-Content $log

Unregister-ScheduledTask -TaskName 'XrayNL' -Confirm:$false -ErrorAction SilentlyContinue
"task removed" | Add-Content $log
Get-Process xray -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $dst | Out-Null
foreach ($f in @('AntigravityProxy.ps1', 'AntigravityProxy.ico', 'Antigravity Proxy.vbs', 'Antigravity Proxy.cmd')) {
    $p = Join-Path $src $f
    if (Test-Path $p) { Copy-Item $p $dst -Force }
}
"app copied to $dst" | Add-Content $log

& icacls.exe $xdir /grant "$($env:USERNAME):(OI)(CI)M" /T /C *> $null
"acl granted" | Add-Content $log

$ws = New-Object -ComObject WScript.Shell
$vbs = Join-Path $dst 'Antigravity Proxy.vbs'
$ico = Join-Path $dst 'AntigravityProxy.ico'
foreach ($dir in @([Environment]::GetFolderPath('Desktop'), (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'))) {
    $lnkPath = Join-Path $dir 'Antigravity Proxy.lnk'
    $lnk = $ws.CreateShortcut($lnkPath)
    $lnk.TargetPath = Join-Path $env:SystemRoot 'System32\wscript.exe'
    $lnk.Arguments = ('"{0}"' -f $vbs)
    $lnk.WorkingDirectory = $dst
    $lnk.IconLocation = $ico
    $lnk.Description = 'Antigravity Proxy'
    $lnk.Save()
    "shortcut: $lnkPath" | Add-Content $log
}
"done" | Add-Content $log
