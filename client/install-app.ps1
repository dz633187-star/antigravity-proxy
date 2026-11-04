$ErrorActionPreference = 'Continue'
$src  = 'C:\Users\Дима\Documents\deepseek-harness\default-workspace\NL-VPS\App'
$dst  = Join-Path $env:LOCALAPPDATA 'AntigravityProxy'
$xdir = Join-Path $env:LOCALAPPDATA 'XrayNL'
$log  = Join-Path $xdir 'install-app.txt'
"" | Set-Content $log

# 1. убираем системную задачу, чтобы Xray можно было перезапускать из приложения
Unregister-ScheduledTask -TaskName 'XrayNL' -Confirm:$false -ErrorAction SilentlyContinue
"task removed" | Add-Content $log
Get-Process xray -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
"xray stopped" | Add-Content $log

# 2. копируем приложение
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item (Join-Path $src 'AntigravityProxy.ps1') $dst -Force
Copy-Item (Join-Path $src 'Antigravity Proxy.cmd') $dst -Force
"app copied to $dst" | Add-Content $log

# 3. права пользователю на папку XrayNL (чтобы приложение могло писать конфиг)
& icacls.exe $xdir /grant "$($env:USERNAME):(OI)(CI)M" /T /C *> $null
"acl granted" | Add-Content $log

# 4. ярлык на рабочем столе
$desktop = [Environment]::GetFolderPath('Desktop')
$lnkPath = Join-Path $desktop 'Antigravity Proxy.lnk'
$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($lnkPath)
$lnk.TargetPath = Join-Path $dst 'Antigravity Proxy.cmd'
$lnk.WorkingDirectory = $dst
$lnk.IconLocation = (Join-Path $xdir 'xray.exe')
$lnk.Description = 'Запуск Antigravity через прокси'
$lnk.Save()
"shortcut: $lnkPath" | Add-Content $log

# 5. копия в меню Пуск
$startDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$lnk2 = $ws.CreateShortcut((Join-Path $startDir 'Antigravity Proxy.lnk'))
$lnk2.TargetPath = Join-Path $dst 'Antigravity Proxy.cmd'
$lnk2.WorkingDirectory = $dst
$lnk2.IconLocation = (Join-Path $xdir 'xray.exe')
$lnk2.Save()
"start menu ok" | Add-Content $log

"done" | Add-Content $log
