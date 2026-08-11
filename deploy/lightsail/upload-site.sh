#!/usr/bin/env bash
# Upload deploy assets from a machine that has SSH alias `shauninfomedia`.
# Run from Windows Git Bash / WSL, or adapt paths for PowerShell scp.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
HOST="${SSH_HOST:-shauninfomedia}"

echo "Uploading company site + nginx configs to $HOST"
scp -r "$ROOT/deploy/lightsail/www/public" "$HOST:/tmp/shauninfomedia-public"
scp "$ROOT/deploy/lightsail/nginx/shauninfomedia.com.conf" "$HOST:/tmp/shauninfomedia.com.conf"
scp "$ROOT/deploy/lightsail/nginx/erp.shauninfomedia.com.conf" "$HOST:/tmp/erp.shauninfomedia.com.conf"
scp "$ROOT/deploy/lightsail/setup-server.sh" "$HOST:/tmp/setup-server.sh"

ssh "$HOST" 'sudo mkdir -p /var/www/shauninfomedia && sudo rm -rf /var/www/shauninfomedia/public && sudo mv /tmp/shauninfomedia-public /var/www/shauninfomedia/public && sudo chown -R www-data:www-data /var/www/shauninfomedia && sudo bash /tmp/setup-server.sh'

echo "Done. Next: install Docker, upload ShaunERP to /opt/shaunerp, compose up, then Cloudflare DNS + certbot."
