#!/usr/bin/env bash
set -euo pipefail

host=${1:?Usage: deploy/provision.sh <droplet-ip-or-hostname>}
remote=${PROJEKTLOKE_SSH_USER:-root}@${host}
remote_dir=/root/projektloke-bootstrap

ssh "$remote" "mkdir -p '$remote_dir'"
scp deploy/bootstrap.sh deploy/Caddyfile deploy/projektloke.service "$remote:$remote_dir/"
ssh "$remote" "cd '$remote_dir' && chmod +x bootstrap.sh && ./bootstrap.sh"
