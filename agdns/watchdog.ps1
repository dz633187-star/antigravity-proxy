#requires -version 5.1
<#
    AGDNS - сторож. Запускается планировщиком каждые 5 минут от SYSTEM.
    1. Если AGUnlocker вернул свои NRPT-правила на домены Antigravity
       с чужим DNS - переводит их обратно на 127.0.0.1.
    2. Если локальный DNS не отвечает - поднимает задачу AGDNS.
#>
$ErrorActionPreference = 'SilentlyContinue'

foreach ($rule in @(Get-DnsClientNrptRule)) {
    $ns = ($rule.Namespace -join ',')
    if ($ns -match 'cloudcode-pa\.googleapis\.com') {
        $servers = @($rule.NameServers)
        $ours = ($servers.Count -eq 1 -and $servers[0] -eq '127.0.0.1')
        if (-not $ours) {
            Set-DnsClientNrptRule -Name $rule.Name -NameServers '127.0.0.1'
            Clear-DnsClientCache
        }
    }
}

$ok = $false
try {
    $r = Resolve-DnsName 'ya.ru' -Type A -Server 127.0.0.1 -DnsOnly -ErrorAction Stop
    if ($r) { $ok = $true }
} catch { }

if (-not $ok) {
    schtasks.exe /Run /TN AGDNS | Out-Null
}
