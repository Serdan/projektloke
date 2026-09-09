#!/usr/bin/env bash
set -euo pipefail

if [[ ${EUID:-$(id -u)} -ne 0 ]]; then
  echo "Run as root." >&2
  exit 1
fi

apt-get update
apt-get install -y ca-certificates curl debian-keyring debian-archive-keyring apt-transport-https gnupg ufw
rm -f /usr/share/keyrings/caddy-stable-archive-keyring.gpg

curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' > /etc/apt/sources.list.d/caddy-stable.list
apt-get update
apt-get install -y caddy

if ! id projektloke >/dev/null 2>&1; then
  useradd --system --home /srv/projektloke --shell /usr/sbin/nologin projektloke
fi

install -d -o projektloke -g projektloke -m 0755 /srv/projektloke
install -m 0644 ./projektloke.service /etc/systemd/system/projektloke.service
install -m 0644 ./Caddyfile /etc/caddy/Caddyfile

ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

systemctl daemon-reload
systemctl enable projektloke.service
systemctl enable --now caddy
systemctl reload caddy

echo "Bootstrap complete. Deploy the app into /srv/projektloke, then start projektloke.service."
