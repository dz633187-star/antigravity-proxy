$ErrorActionPreference = 'Continue'
$dir = Join-Path $env:LOCALAPPDATA 'XrayNL'
$cfg = Join-Path $dir 'config.json'
$out = Join-Path $dir 'log-on.txt'

try {
    $j = Get-Content $cfg -Raw -Encoding UTF8 | ConvertFrom-Json
    $j.log | Add-Member -NotePropertyName access -NotePropertyValue (Join-Path $dir 'access.log') -Force
    $j.log.loglevel = 'info'
    $json = $j | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($cfg, $json, (New-Object System.Text.UTF8Encoding($false)))
    'config updated' | Set-Content $out
} catch {
    "config FAIL: $($_.Exception.Message)" | Set-Content $out
}

Get-Process xray -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-ScheduledTask -TaskName 'XrayNL'
Start-Sleep -Seconds 5
"listen: $([bool](Get-NetTCPConnection -State Listen -LocalPort 10809 -ErrorAction SilentlyContinue))" | Add-Content $out
