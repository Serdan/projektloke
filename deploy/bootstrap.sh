#!/usr/bin/env bash
set -euo pipefail

if [[ ${EUID:-$(id -u)} -ne 0 ]]; then
  echo "Run as root." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y ca-certificates curl debian-keyring debian-archive-keyring apt-transport-https gnupg ufw sudo unattended-upgrades
apt-get upgrade -y
rm -f /usr/share/keyrings/caddy-stable-archive-keyring.gpg

curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' > /etc/apt/sources.list.d/caddy-stable.list
apt-get update
apt-get install -y caddy

if ! id projektloke > /dev/null 2>&1; then
  useradd --system --home /srv/projektloke --shell /usr/sbin/nologin projektloke
fi

if ! id projektloke-deploy > /dev/null 2>&1; then
  useradd --create-home --gid projektloke --shell /bin/bash projektloke-deploy
fi

install -d -o projektloke-deploy -g projektloke -m 0750 /srv/projektloke
install -d -o projektloke-deploy -g projektloke -m 0700 /home/projektloke-deploy/.ssh
if [[ -s /root/.ssh/authorized_keys ]]; then
  install -o projektloke-deploy -g projektloke -m 0600 /root/.ssh/authorized_keys /home/projektloke-deploy/.ssh/authorized_keys
fi

install -m 0440 ./projektloke-deploy.sudoers /etc/sudoers.d/projektloke-deploy
visudo -cf /etc/sudoers.d/projektloke-deploy
install -m 0644 ./sshd-projektloke.conf /etc/ssh/sshd_config.d/90-projektloke.conf
sshd -t

install -m 0644 ./projektloke.service /etc/systemd/system/projektloke.service
install -m 0644 ./Caddyfile /etc/caddy/Caddyfile
install -d -m 0755 /etc/systemd/system/caddy.service.d
install -m 0644 ./caddy-hardening.conf /etc/systemd/system/caddy.service.d/hardening.conf

if ! swapon --show=NAME --noheadings | grep -qx '/swapfile'; then
  if [[ ! -f /swapfile ]]; then
    fallocate -l 1G /swapfile
    chmod 0600 /swapfile
    mkswap /swapfile
  fi
  swapon /swapfile
fi
grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab

ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

systemctl daemon-reload
systemctl enable projektloke.service
systemctl enable --now unattended-upgrades.service
systemctl enable --now caddy
caddy validate --config /etc/caddy/Caddyfile
systemctl restart caddy
systemctl reload ssh

echo "Bootstrap complete. Future deployments use projektloke-deploy over SSH."
