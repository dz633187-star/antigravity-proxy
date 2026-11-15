$ErrorActionPreference = 'Continue'
$src = $PSScriptRoot
$dst = Join-Path $env:LOCALAPPDATA 'AntigravityProxy'
$xdir = Join-Path $env:LOCALAPPDATA 'XrayNL'
$log = Join-Path $xdir 'install-app.txt'
if (-not (Test-Path $xdir)) { New-Item -ItemType Directory -Force -Path $xdir | Out-Null }
"" | Set-Content $log

# 1. Убираем автозапуск старой задачи XrayNL (приложение само управляет Xray)
Unregister-ScheduledTask -TaskName 'XrayNL' -Confirm:$false -ErrorAction SilentlyContinue
"task removed" | Add-Content $log
# Xray НЕ останавливаем, чтобы не прерывать текущую сессию и интернет

# 2. Если exe еще не скомпилирован - компилируем через build.cmd
$exePath = Join-Path $src 'AntigravityProxy.exe'
$unPath = Join-Path $src 'Uninstall.exe'
$setupPath = Join-Path $src 'AntigravityProxySetup.exe'
$buildCmd = Join-Path $src 'build.cmd'

if ((-not (Test-Path $exePath)) -or (-not (Test-Path $unPath)) -or (-not (Test-Path $setupPath))) {
    & cmd.exe /c "`"$buildCmd`""
    "compiled all targets" | Add-Content $log
}

# 3. Копируем файлы приложения
New-Item -ItemType Directory -Force -Path $dst | Out-Null
foreach ($f in @('AntigravityProxy.exe', 'AntigravityProxy.cs', 'AntigravityProxy.ico', 'Uninstall.exe', 'AntigravityProxySetup.exe')) {
    $p = Join-Path $src $f
    if (Test-Path $p) { Copy-Item $p $dst -Force }
}
"app copied to $dst" | Add-Content $log

# 4. Языконезависимые права Все пользователи (*S-1-1-0) на папки XrayNL и AntigravityProxy
& icacls.exe $xdir /grant "*S-1-1-0:(OI)(CI)F" /T /C *> $null
& icacls.exe $dst /grant "*S-1-1-0:(OI)(CI)F" /T /C *> $null
"acl granted" | Add-Content $log

# 5. Ярлыки
$ws = New-Object -ComObject WScript.Shell
$targetExe = Join-Path $dst 'AntigravityProxy.exe'
$ico = Join-Path $dst 'AntigravityProxy.ico'

# Рабочий стол и Меню «Пуск»
foreach ($dir in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
    if (Test-Path $dir) {
        $lnkPath = Join-Path $dir 'Antigravity Proxy.lnk'
        $lnk = $ws.CreateShortcut($lnkPath)
        $lnk.TargetPath = $targetExe
        $lnk.Arguments = ''
        $lnk.WorkingDirectory = $dst
        $lnk.IconLocation = "$ico,0"
        $lnk.Description = 'Antigravity Proxy'
        $lnk.Save()
        "shortcut: $lnkPath" | Add-Content $log
    }
}

# Панель задач (User Pinned)
$taskbarDir = Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'
if (-not (Test-Path $taskbarDir)) { New-Item -ItemType Directory -Force -Path $taskbarDir | Out-Null }
$taskbarLnk = Join-Path $taskbarDir 'Antigravity Proxy.lnk'
$lnk = $ws.CreateShortcut($taskbarLnk)
$lnk.TargetPath = $targetExe
$lnk.WorkingDirectory = $dst
$lnk.IconLocation = "$ico,0"
$lnk.Description = 'Antigravity Proxy'
$lnk.Save()

# 6. Регистрация в "Установка и удаление программ"
try {
    $regKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AntigravityProxy'
    if (-not (Test-Path $regKey)) { New-Item -Path $regKey -Force | Out-Null }
    Set-ItemProperty -Path $regKey -Name 'DisplayName' -Value 'Antigravity Proxy'
    Set-ItemProperty -Path $regKey -Name 'DisplayVersion' -Value '1.2.0'
    Set-ItemProperty -Path $regKey -Name 'Publisher' -Value 'Antigravity'
    Set-ItemProperty -Path $regKey -Name 'DisplayIcon' -Value $ico
    Set-ItemProperty -Path $regKey -Name 'InstallLocation' -Value $dst
    Set-ItemProperty -Path $regKey -Name 'UninstallString' -Value "`"$dst\Uninstall.exe`""
    Set-ItemProperty -Path $regKey -Name 'NoModify' -Value 1 -Type DWord
    Set-ItemProperty -Path $regKey -Name 'NoRepair' -Value 1 -Type DWord
} catch {}

"done" | Add-Content $log
