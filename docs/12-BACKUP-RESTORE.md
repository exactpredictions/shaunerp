# Backup & Restore

## What to back up

| Asset | Location | Criticality |
|-------|----------|-------------|
| PostgreSQL data | Docker volume `shaunerp_pgdata` | **Critical** |
| Environment secrets | `/opt/shaunerp/.env` | **Critical** (store separately) |
| Blazor data protection keys | Volume `shaunerp_web_keys` | High (invalidates cookies if lost) |
| Application code | Git remote | Medium (reclone) |
| nginx / TLS certs | `/etc/letsencrypt` | Medium (reissue via certbot) |

## PostgreSQL backup

### Manual dump (recommended format: custom)

```bash
docker exec shaunerp-db pg_dump -U shaunerp -d shaunerp -Fc \
  -f /tmp/shaunerp-$(date +%Y%m%d-%H%M).dump

docker cp shaunerp-db:/tmp/shaunerp-$(date +%Y%m%d-%H%M).dump \
  /backups/shaunerp/
```

### Plain SQL

```bash
docker exec shaunerp-db pg_dump -U shaunerp -d shaunerp \
  > /backups/shaunerp/shaunerp-$(date +%Y%m%d).sql
```

### Verify dump

```bash
docker exec shaunerp-db pg_restore --list /tmp/shaunerp-*.dump | head
```

## Automated backup (recommended)

Example cron on host (`/etc/cron.d/shaunerp-backup`):

```cron
0 2 * * * root docker exec shaunerp-db pg_dump -U shaunerp -d shaunerp -Fc -f /tmp/nightly.dump && docker cp shaunerp-db:/tmp/nightly.dump /backups/shaunerp/nightly-$(date +\%Y\%m\%d).dump
```

Retention policy suggestion:

- Daily: 7 days
- Weekly: 4 weeks
- Monthly: 12 months

Copy off-server to S3, Backblaze, or second region.

## Restore procedure

### 1. Stop writers

```bash
cd /opt/shaunerp
docker compose -f docker-compose.prod.yml stop api web
```

### 2. Restore database

**Custom format:**

```bash
docker cp /backups/shaunerp/shaunerp-backup.dump shaunerp-db:/tmp/restore.dump
docker exec shaunerp-db pg_restore -U shaunerp -d shaunerp \
  --clean --if-exists /tmp/restore.dump
```

**Plain SQL:**

```bash
docker exec -i shaunerp-db psql -U shaunerp -d shaunerp < /backups/shaunerp/backup.sql
```

### 3. Restart stack

```bash
docker compose -f docker-compose.prod.yml start api web
curl -s http://127.0.0.1:8080/health
```

### 4. Smoke test

- Login to Web UI
- Open CRM dashboard and one invoice
- Trial balance report for current FY

## Volume-level backup

Alternative: snapshot `shaunerp_pgdata` volume while DB stopped:

```bash
docker compose -f docker-compose.prod.yml stop db api web
docker run --rm -v shaunerp_pgdata:/data -v /backups:/backup alpine \
  tar czf /backup/pgdata-$(date +%Y%m%d).tar.gz -C /data .
docker compose -f docker-compose.prod.yml start db api web
```

## Point-in-time recovery

**Not configured.** WAL archiving and PITR are **PLANNED** for hardened production. Current backups are snapshot-only.

## Pre-upgrade backup

Always dump before `git pull` + compose rebuild:

```bash
docker exec shaunerp-db pg_dump -U shaunerp -d shaunerp -Fc -f /tmp/pre-upgrade.dump
docker cp shaunerp-db:/tmp/pre-upgrade.dump ./pre-upgrade.dump
```

## Credentials

Database password required for restore — see [docs/private/PRODUCTION-CREDENTIALS.md](private/PRODUCTION-CREDENTIALS.md).

See also [17-DISASTER-RECOVERY.md](17-DISASTER-RECOVERY.md).
