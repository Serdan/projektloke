#!/usr/bin/env bash
set -euo pipefail

host=${1:?Usage: deploy/provision.sh <droplet-ip-or-hostname>}
remote=${PROJEKTLOKE_SSH_USER:-root}@${host}
ssh_opts=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -o ConnectTimeout=10)
remote_dir=/root/projektloke-bootstrap

ssh "${ssh_opts[@]}" "$remote" "mkdir -p '$remote_dir'"
scp "${ssh_opts[@]}" deploy/bootstrap.sh deploy/Caddyfile deploy/projektloke.service deploy/sshd-projektloke.conf deploy/projektloke-deploy.sudoers deploy/caddy-hardening.conf "$remote:$remote_dir/"
ssh "${ssh_opts[@]}" "$remote" "cd '$remote_dir' && chmod +x bootstrap.sh && ./bootstrap.sh"
