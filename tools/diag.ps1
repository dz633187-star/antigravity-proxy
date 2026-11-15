# Проверка цепочки Antigravity: прокси жив, выход NL, Google отвечает.
$ErrorActionPreference = 'SilentlyContinue'
$proxy = 'http://127.0.0.1:10809'

"=== 1. Порт 10809 ==="
$l = Get-NetTCPConnection -LocalPort 10809 -ErrorAction SilentlyContinue |
     Where-Object { $_.OwningProcess -eq (Get-Process xray -ErrorAction SilentlyContinue).Id }
if ($l) { " слушает xray (PID $($l[0].OwningProcess))" } else {
    " НЕТ слушателя — запустите 'Antigravity NL.cmd' или xray вручную"
}

if (-not $l) { exit 1 }

"=== 2. Выход через прокси ==="
$ip = & curl.exe -s --max-time 15 --proxy $proxy https://ipwho.is/ | ConvertFrom-Json
" IP: $($ip.ip)  страна: $($ip.country_code)  город: $($ip.city)"

"=== 3. Google API через прокси ==="
$code = & curl.exe -s -o NUL -w "%{http_code}" --max-time 15 --proxy $proxy "https://daily-cloudcode-pa.googleapis.com/"
" daily-cloudcode-pa -> HTTP $code (401/404 = доступ есть, отказ будет только по аккаунту)"

"=== 4. Анлокер выключен? ==="
$agdns = Get-ScheduledTask -TaskName 'AG Unlocker DNS'
$wd    = Get-ScheduledTask -TaskName 'AG Unlocker Watchdog'
" AG Unlocker DNS:      $($agdns.State)"
" AG Unlocker Watchdog: $($wd.State)"
if (Get-Process ag_dns -ErrorAction SilentlyContinue) { " ag_dns.exe: ЗАПУЩЕН (плохо)" } else { " ag_dns.exe: не запущен (хорошо)" }

"=== 5. Antigravity через прокси? ==="
$ls = Get-Process language_server -ErrorAction SilentlyContinue
if ($ls) {
    $conns = $ls | ForEach-Object { Get-NetTCPConnection -OwningProcess $_.Id -State Established } |
             Where-Object { $_.RemotePort -eq 10809 }
    if ($conns) { " language_server -> 10809: подключений $($conns.Count) (хорошо)" }
    else        { " language_server работает, но к 10809 ещё не подключился (появится после первого запроса)" }
} else { " language_server не запущен — откройте Antigravity из лончера" }
