#!/usr/bin/env bash
# Run ON the Lightsail server as ubuntu (with sudo).
# Usage: sudo bash setup-server.sh
set -euo pipefail

echo "==> Apt packages (nginx, certbot)"
apt-get update
apt-get install -y nginx certbot python3-certbot-nginx

echo "==> Directories"
mkdir -p /var/www/shauninfomedia/public
mkdir -p /opt/shaunerp

echo "==> Note: copy site files and nginx confs before enabling sites"
echo "    /var/www/shauninfomedia/public/   <- company site"
echo "    /etc/nginx/sites-available/       <- vhost confs"
echo "    /opt/shaunerp/                    <- ShaunERP + docker-compose.prod.yml"

if [[ -f /tmp/shauninfomedia.com.conf ]]; then
  cp /tmp/shauninfomedia.com.conf /etc/nginx/sites-available/shauninfomedia.com
  ln -sfn /etc/nginx/sites-available/shauninfomedia.com /etc/nginx/sites-enabled/shauninfomedia.com
fi

if [[ -f /tmp/erp.shauninfomedia.com.conf ]]; then
  cp /tmp/erp.shauninfomedia.com.conf /etc/nginx/sites-available/erp.shauninfomedia.com
  ln -sfn /etc/nginx/sites-available/erp.shauninfomedia.com /etc/nginx/sites-enabled/erp.shauninfomedia.com
fi

# Disable default site if present
rm -f /etc/nginx/sites-enabled/default

nginx -t
systemctl enable nginx
systemctl reload nginx

echo "==> HTTP vhosts ready. After Cloudflare DNS points here, run:"
echo "    sudo certbot --nginx -d shauninfomedia.com -d www.shauninfomedia.com -d erp.shauninfomedia.com"
