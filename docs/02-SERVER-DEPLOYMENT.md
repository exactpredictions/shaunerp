# Server Deployment

Production deployment for Shaun Infomedia on Linux (Lightsail or similar).

## Target environment

| Item | Value |
|------|-------|
| Deploy path | `/opt/shaunerp` |
| ERP domain | `erp.shauninfomedia.com` |
| Company site | `shauninfomedia.com` (static site at `/var/www/shauninfomedia/public`) |
| Compose file | `docker-compose.prod.yml` |
| Environment | `ASPNETCORE_ENVIRONMENT=Production` |

## Container layout

```yaml
# docker-compose.prod.yml (summary)
services:
  db:    # shaunerp-db — no public ports
  api:   # shaunerp-api — 127.0.0.1:8080
  web:   # shaunerp-web — 127.0.0.1:8081
volumes:
  shaunerp_pgdata      # PostgreSQL data
  shaunerp_web_keys    # Data Protection keys for Blazor
networks:
  shaunerp_internal    # bridge; DB not exposed
```

## Prerequisites

- Ubuntu/Debian Linux with Docker Engine and Compose plugin
- nginx installed
- Ports **22**, **80**, **443** open (not 5432, 8080, 8081)
- DNS A records for `@`, `www`, `erp` → server IP (Cloudflare per [deploy/lightsail/CLOUDFLARE_DNS.md](../deploy/lightsail/CLOUDFLARE_DNS.md))

## Install steps

### 1. Clone and configure

```bash
sudo mkdir -p /opt/shaunerp
sudo chown $USER:$USER /opt/shaunerp
git clone <repo-url> /opt/shaunerp
cd /opt/shaunerp
cp deploy/lightsail/.env.prod.example .env
# Edit .env — use docs/private/PRODUCTION-CREDENTIALS.md for secrets
```

Required `.env` variables:

- `POSTGRES_PASSWORD` (strong)
- `JWT_KEY` (32+ random characters)
- `ASPNETCORE_ENVIRONMENT=Production`
- Optional: `DISABLE_SWAGGER=true` for production

### 2. Start stack

```bash
cd /opt/shaunerp
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml ps
curl -s http://127.0.0.1:8080/health
curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8081/
```

### 3. nginx

Copy vhost from `deploy/lightsail/nginx/erp.shauninfomedia.com.conf` to `/etc/nginx/sites-available/`, enable, test, reload:

```bash
sudo ln -sf /etc/nginx/sites-available/erp.shauninfomedia.com.conf /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

Routing:

| Path | Backend |
|------|---------|
| `/api/*` | `127.0.0.1:8080` |
| `/swagger`, `/health` | `127.0.0.1:8080` |
| `/` (all other) | `127.0.0.1:8081` (WebSocket upgrade for Blazor) |

### 4. TLS (Let's Encrypt)

```bash
sudo certbot --nginx -d shauninfomedia.com -d www.shauninfomedia.com -d erp.shauninfomedia.com
```

Cloudflare SSL mode: **Full (strict)** after certbot succeeds.

### 5. Production hardening (required before go-live)

- [ ] Change or remove demo `admin` user (see [16-PRODUCTION-OPERATIONS.md](16-PRODUCTION-OPERATIONS.md))
- [ ] Set `DISABLE_SWAGGER=true` in `.env`
- [ ] Restrict CORS on API (see [14-SECURITY-GUIDE.md](14-SECURITY-GUIDE.md))
- [ ] Verify DB not reachable from internet
- [ ] Configure automated backups (see [12-BACKUP-RESTORE.md](12-BACKUP-RESTORE.md))

## Upgrade procedure

```bash
cd /opt/shaunerp
git pull
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d
```

**Caution:** Schema changes rely on `DbSeeder` raw SQL patches, not formal migrations. Review release notes and test on staging before production pull.

## Rollback

```bash
git checkout <previous-tag-or-commit>
docker compose -f docker-compose.prod.yml up -d --build
```

Restore database from backup if schema/data incompatible (see [17-DISASTER-RECOVERY.md](17-DISASTER-RECOVERY.md)).

## Local development

```bash
cp .env.example .env
docker compose up -d db          # or full stack
dotnet run --project src/ShaunERP.Web
dotnet run --project src/ShaunERP.Api
```

Dev compose (`docker-compose.yml`) may expose Postgres on port 5432 — do not use that layout in production.

## Logs

```bash
docker logs -f shaunerp-web
docker logs -f shaunerp-api
docker logs -f shaunerp-db
```

## Reference scripts

| Path | Purpose |
|------|---------|
| `deploy/lightsail/setup-server.sh` | Initial server bootstrap |
| `deploy/lightsail/upload-site.sh` | Company static site upload |
| `deploy/lightsail/README.md` | Lightsail-specific layout |
