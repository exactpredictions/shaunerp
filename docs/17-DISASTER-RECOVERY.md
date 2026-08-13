# Disaster Recovery

Recovery objectives and procedures for ShaunERP at Shaun Infomedia.

## Recovery objectives (current — untested)

| Metric | Target | Notes |
|--------|--------|-------|
| RTO (restore service) | 4 hours | Manual restore from dump |
| RPO (data loss) | 24 hours | Daily backup assumption |
| PITR | Not available | WAL archiving not configured |

Formal DR drill **recommended before go-live**.

## Failure scenarios

### 1. Application container crash

**Symptoms:** 502 from nginx, container `Exited`

**Response:**

```bash
docker compose -f docker-compose.prod.yml up -d web api
docker logs shaunerp-web --tail 100
```

Compose `restart: unless-stopped` auto-recovers most cases.

### 2. Database corruption / data loss

**Symptoms:** API/Web errors, PostgreSQL won't start

**Response:**

1. Stop stack: `docker compose -f docker-compose.prod.yml stop api web db`
2. Restore from latest backup ([12-BACKUP-RESTORE.md](12-BACKUP-RESTORE.md))
3. If volume corrupt: recreate volume + restore

```bash
docker volume rm shaunerp_pgdata   # DESTRUCTIVE — only if volume unrecoverable
docker compose -f docker-compose.prod.yml up -d db
# restore dump
docker compose -f docker-compose.prod.yml up -d api web
```

### 3. Full server loss (Lightsail instance)

**Response:**

1. Provision new instance (same spec: 2 GB+ RAM, swap enabled)
2. Install Docker, nginx, certbot
3. Restore `/opt/shaunerp` from git + `.env` from secure backup
4. Restore `shaunerp_pgdata` from off-site dump OR volume tarball
5. Restore nginx vhosts and re-run certbot
6. Update Cloudflare DNS if IP changed
7. Verify health and login

Keep `.env` and database dumps **off-server**.

### 4. Accidental demo seed / bad migration

**Response:**

- Restore pre-upgrade dump if taken
- Or selective SQL delete (see [16-PRODUCTION-OPERATIONS.md](16-PRODUCTION-OPERATIONS.md))

Hybrid schema (`EnsureCreated` + SQL patches) increases risk of inconsistent state — prefer restore over manual repair.

### 5. JWT key compromise

**Response:**

1. Generate new `JWT_KEY` in `.env`
2. Restart api + web
3. All users re-login
4. Optional: truncate `Sessions` table

### 6. TLS certificate expiry

**Symptoms:** Browser TLS warnings

**Response:**

```bash
sudo certbot renew
sudo nginx -t && sudo systemctl reload nginx
```

### 7. Blazor keys volume lost

**Symptoms:** All users logged out; cookies invalid

**Response:** Restore `shaunerp_web_keys` volume backup or accept mass re-login

## Backup requirements for DR

| Item | Frequency | Off-site |
|------|-----------|----------|
| pg_dump custom format | Daily | Yes |
| `.env` copy | On change | Yes (encrypted) |
| `shaunerp_web_keys` tar | Weekly | Yes |
| nginx + certbot config | On change | Yes |

## Contacts (fill in operationally)

| Role | Contact |
|------|---------|
| Infrastructure owner | Shaun Infomedia ops |
| Database admin | — |
| Application owner | — |

## Post-incident

1. Document timeline and root cause
2. Update [18-CHANGELOG.md](18-CHANGELOG.md) if code fix required
3. Adjust backup frequency if RPO exceeded
4. Schedule DR retest

## Related

- [02-SERVER-DEPLOYMENT.md](02-SERVER-DEPLOYMENT.md)
- [12-BACKUP-RESTORE.md](12-BACKUP-RESTORE.md)
- [PRODUCTION-READINESS-REPORT.md](PRODUCTION-READINESS-REPORT.md)
