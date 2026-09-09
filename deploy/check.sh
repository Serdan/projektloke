#!/usr/bin/env bash
set -euo pipefail

host=${1:?Usage: deploy/check.sh <droplet-ip-or-hostname>}
remote=${PROJEKTLOKE_SSH_USER:-root}@${host}
ssh_opts=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -o ConnectTimeout=10)

service=$(ssh "${ssh_opts[@]}" "$remote" "systemctl is-active projektloke.service")
status=$(ssh "${ssh_opts[@]}" "$remote" "curl -sS -o /dev/null -w '%{http_code}' http://127.0.0.1:5080/")

if [[ "$service" != "active" || "$status" != "200" ]]; then
  echo "Health check failed: service=$service http=$status" >&2
  exit 1
fi

echo "Health check passed: service=$service http=$status"
