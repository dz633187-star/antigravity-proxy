#!/bin/bash
set -e
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq curl unzip openssl ca-certificates
bash -c "$(curl -fsSL https://github.com/XTLS/Xray-install/raw/main/install-release.sh)" @ install >/dev/null
X=/usr/local/bin/xray
U=$($X uuid)
K=$($X x25519)
P=$(echo "$K"|grep -iE 'private'|awk '{print $NF}')
Q=$(echo "$K"|grep -iE 'public|password'|awk '{print $NF}')
S=$(openssl rand -hex 8)
N=www.microsoft.com
cat >/usr/local/etc/xray/config.json <<E
{"inbounds":[{"listen":"0.0.0.0","port":443,"protocol":"vless","settings":{"clients":[{"id":"$U","flow":"xtls-rprx-vision"}],"decryption":"none"},"streamSettings":{"network":"tcp","security":"reality","realitySettings":{"dest":"$N:443","serverNames":["$N"],"privateKey":"$P","shortIds":["$S"]}}}],"outbounds":[{"protocol":"freedom"}]}
E
systemctl enable xray >/dev/null 2>&1
systemctl restart xray
sleep 2
I=$(curl -4 -fsS --max-time 10 https://api.ipify.org || hostname -I | awk '{print $1}')
echo "vless://$U@$I:443?encryption=none&security=reality&sni=$N&fp=chrome&pbk=$Q&sid=$S&type=tcp&flow=xtls-rprx-vision#NL"
