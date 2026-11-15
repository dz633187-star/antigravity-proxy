$ErrorActionPreference = 'Continue'
$out = Join-Path $env:LOCALAPPDATA 'XrayNL\unblock.txt'
Get-NetFirewallRule -DisplayName 'AGDNS-NoDirect-*' -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-NetFirewallRule -Name $_.Name -ErrorAction SilentlyContinue
    "removed: $($_.DisplayName)" | Add-Content $out
}
$left = @(Get-NetFirewallRule -DisplayName 'AGDNS-NoDirect-*' -ErrorAction SilentlyContinue).Count
"left: $left" | Add-Content $out
