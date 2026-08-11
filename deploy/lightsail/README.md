# Lightsail layout — shauninfomedia.com

## Goal

| Host | Purpose |
|------|---------|
| `shauninfomedia.com` / `www` | Company website |
| `erp.shauninfomedia.com` | ShaunERP public demo (nginx → Docker API) |

DNS is managed in **Cloudflare**. TLS via **Let's Encrypt (certbot)** after DNS points here.

## Server roles

```
Internet (Cloudflare)
        │
        ▼
   Lightsail :80 / :443
        │
     nginx
        ├── shauninfomedia.com  → /var/www/shauninfomedia/public
        └── erp.shauninfomedia.com → http://127.0.0.1:8080  (Docker)
                                      ├── shaunerp-api
                                      └── shaunerp-db (internal only)
```

- Postgres is **not** published to the internet.
- API listens on **localhost:8080** only; nginx terminates TLS.

## Cloudflare DNS (you do this)

Add these records (replace `YOUR_LIGHTSAIL_PUBLIC_IP`):

| Type | Name | Content | Proxy |
|------|------|---------|-------|
| A | `@` | YOUR_LIGHTSAIL_PUBLIC_IP | Proxied (orange) or DNS-only for first cert |
| A | `www` | YOUR_LIGHTSAIL_PUBLIC_IP | same |
| A | `erp` | YOUR_LIGHTSAIL_PUBLIC_IP | same |

**SSL/TLS mode in Cloudflare:** Full (strict) after certbot succeeds.

Tip for first certificate: set records to **DNS only** (grey cloud), run certbot, then turn proxy back on.

## Lightsail firewall

Open: **22**, **80**, **443**. Do **not** open 5432 or 8080 publicly.

## Install order

1. Docker + swap (2 GB RAM)
2. nginx + site files + vhosts (HTTP first)
3. Upload / clone ShaunERP → `docker compose -f docker-compose.prod.yml up -d --build`
4. You add Cloudflare DNS
5. `sudo certbot --nginx -d shauninfomedia.com -d www.shauninfomedia.com -d erp.shauninfomedia.com`

## Paths on server

```
/var/www/shauninfomedia/public/     # company site
/opt/shaunerp/                      # ERP compose project
/etc/nginx/sites-available/...
```
