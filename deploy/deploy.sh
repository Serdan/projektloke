#!/usr/bin/env bash
set -euo pipefail

host=${1:?Usage: deploy/deploy.sh <droplet-ip-or-hostname>}
remote=${PROJEKTLOKE_SSH_USER:-projektloke-deploy}@${host}
ssh_opts=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -o ConnectTimeout=10)
artifact=$(mktemp /tmp/projektloke.XXXXXX.tar.gz)
trap 'rm -f "$artifact"' EXIT

(
  cd site
  rm -rf publish
  dotnet run build.cs
  dotnet publish app.cs -c Release -r linux-x64 --self-contained true -o publish
  tar -czf "$artifact" -C publish app app.staticwebassets.endpoints.json wwwroot
)

scp "${ssh_opts[@]}" "$artifact" "$remote:/tmp/projektloke.tar.gz"
ssh "${ssh_opts[@]}" "$remote" '
  set -euo pipefail
  umask 027
  sudo -n systemctl stop projektloke.service 2>/dev/null || true
  find /srv/projektloke -mindepth 1 -maxdepth 1 -exec rm -rf {} +
  tar -xzf /tmp/projektloke.tar.gz -C /srv/projektloke
  chmod 0755 /srv/projektloke/app
  rm -f /tmp/projektloke.tar.gz
  sudo -n systemctl restart projektloke.service
  systemctl --no-pager --full status projektloke.service
'
