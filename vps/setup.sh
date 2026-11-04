#!/usr/bin/env bash
# ============================================================================
#  Antigravity NL proxy - установка Xray (VLESS + REALITY) на чистый сервер.
#  Проверено на Ubuntu 22.04 / 24.04 и Debian 12. Запускать от root.
#
#  Использование:
#     bash setup.sh
#
#  В конце скрипт печатает готовый клиентский конфиг - его нужно сохранить.
# ============================================================================
set -euo pipefail

SNI="www.microsoft.com"      # маскировка: снаружи выглядит как HTTPS к Microsoft
PORT=443

echo "==> Проверяю систему"
if [ "$(id -u)" -ne 0 ]; then
  echo "Запустите от root (sudo -i)"; exit 1
fi
. /etc/os-release 2>/dev/null || true
echo "    ${PRETTY_NAME:-unknown} $(uname -m)"

echo "==> Ставлю зависимости"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq curl unzip openssl ca-certificates >/dev/null

echo "==> Ставлю Xray"
bash -c "$(curl -fsSL https://github.com/XTLS/Xray-install/raw/main/install-release.sh)" @ install >/dev/null
XRAY_BIN=/usr/local/bin/xray
"$XRAY_BIN" version | head -n 1

echo "==> Генерирую ключи"
UUID=$("$XRAY_BIN" uuid)
KEYS=$("$XRAY_BIN" x25519)
PRIVATE_KEY=$(echo "$KEYS" | awk -F': *' '/[Pp]rivate/{print $NF}' | tr -d '[:space:]')
PUBLIC_KEY=$(echo "$KEYS"  | awk -F': *' '/[Pp]ublic/{print $NF}'  | tr -d '[:space:]')
SHORT_ID=$(openssl rand -hex 8)
if [ -z "$PRIVATE_KEY" ] || [ -z "$PUBLIC_KEY" ]; then
  echo "Не удалось разобрать ключи:"; echo "$KEYS"; exit 1
fi

echo "==> Пишу конфиг сервера"
mkdir -p /usr/local/etc/xray
cat > /usr/local/etc/xray/config.json <<EOF
{
  "log": { "loglevel": "warning" },
  "inbounds": [
    {
      "listen": "0.0.0.0",
      "port": $PORT,
      "protocol": "vless",
      "settings": {
        "clients": [
          { "id": "$UUID", "flow": "xtls-rprx-vision" }
        ],
        "decryption": "none"
      },
      "streamSettings": {
        "network": "tcp",
        "security": "reality",
        "realitySettings": {
          "show": false,
          "dest": "$SNI:443",
          "xver": 0,
          "serverNames": [ "$SNI" ],
          "privateKey": "$PRIVATE_KEY",
          "shortIds": [ "$SHORT_ID" ]
        }
      },
      "sniffing": { "enabled": true, "destOverride": [ "http", "tls", "quic" ] }
    }
  ],
  "outbounds": [
    { "protocol": "freedom", "tag": "direct" },
    { "protocol": "blackhole", "tag": "block" }
  ],
  "routing": {
    "rules": [
      { "type": "field", "ip": [ "geoip:private" ], "outboundTag": "block" }
    ]
  }
}
EOF

echo "==> Включаю BBR (ускорение TCP)"
grep -q 'tcp_congestion_control' /etc/sysctl.conf 2>/dev/null || {
  echo 'net.core.default_qdisc=fq' >> /etc/sysctl.conf
  echo 'net.ipv4.tcp_congestion_control=bbr' >> /etc/sysctl.conf
}
sysctl -p >/dev/null 2>&1 || true

echo "==> Открываю порт $PORT"
if command -v ufw >/dev/null 2>&1 && ufw status | grep -q active; then
  ufw allow $PORT/tcp >/dev/null
fi
iptables -I INPUT -p tcp --dport $PORT -j ACCEPT 2>/dev/null || true

echo "==> Запускаю сервис"
systemctl enable xray >/dev/null 2>&1
systemctl restart xray
sleep 2
systemctl is-active --quiet xray && echo "    xray: active" || { echo "    xray НЕ запустился"; journalctl -u xray -n 20 --no-pager; exit 1; }

SERVER_IP=$(curl -4 -fsS --max-time 10 https://api.ipify.org || curl -4 -fsS --max-time 10 https://ifconfig.me || true)
[ -z "$SERVER_IP" ] && SERVER_IP=$(hostname -I | awk '{print $1}')

CLIENT_JSON=$(cat <<EOF
{
  "log": { "loglevel": "warning" },
  "inbounds": [
    { "tag": "socks", "listen": "127.0.0.1", "port": 10808, "protocol": "socks",
      "settings": { "udp": true } },
    { "tag": "http", "listen": "127.0.0.1", "port": 10809, "protocol": "http" }
  ],
  "outbounds": [
    {
      "tag": "nl",
      "protocol": "vless",
      "settings": {
        "vnext": [
          { "address": "$SERVER_IP", "port": $PORT,
            "users": [ { "id": "$UUID", "encryption": "none", "flow": "xtls-rprx-vision" } ] }
        ]
      },
      "streamSettings": {
        "network": "tcp",
        "security": "reality",
        "realitySettings": {
          "serverName": "$SNI",
          "fingerprint": "chrome",
          "publicKey": "$PUBLIC_KEY",
          "shortId": "$SHORT_ID",
          "spiderX": "/"
        }
      }
    },
    { "tag": "direct", "protocol": "freedom" }
  ]
}
EOF
)

echo "$CLIENT_JSON" > /root/xray-client.json

VLESS_URL="vless://${UUID}@${SERVER_IP}:${PORT}?encryption=none&security=reality&sni=${SNI}&fp=chrome&pbk=${PUBLIC_KEY}&sid=${SHORT_ID}&type=tcp&flow=xtls-rprx-vision#NL-Antigravity"
echo "$VLESS_URL" > /root/vless-url.txt

cat <<EOF

============================================================
 ГОТОВО. Сервер: $SERVER_IP:$PORT (VLESS + REALITY)

 СКОПИРУЙТЕ ОДНУ СТРОКУ НИЖЕ И ПРИШЛИТЕ В ЧАТ:

$VLESS_URL

 (файлы на сервере: /root/vless-url.txt, /root/xray-client.json)

 Проверка:  systemctl status xray
 Логи:      journalctl -u xray -n 50 --no-pager
============================================================
EOF
