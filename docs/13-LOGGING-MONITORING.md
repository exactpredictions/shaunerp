# Logging & Monitoring

## Current state — ⚠️ PARTIAL

ShaunERP does **not** ship a centralized observability stack (no Prometheus, Grafana, ELK, or Application Insights integration).

## Application logging

### ASP.NET Core default

- Console logging to Docker stdout/stderr
- Log level from `appsettings.json` / environment

View logs:

```bash
docker logs -f shaunerp-web --tail 200
docker logs -f shaunerp-api --tail 200
docker logs -f shaunerp-db --tail 100
```

### Seeder logs

`DbSeeder` logs information/warning on:

- Demo user creation
- Schema patch skips/failures
- Rich demo seed progress/errors

Review after deploy for unwanted demo seed messages.

## Health checks

| Endpoint | Host | Status |
|----------|------|--------|
| `GET /health` | API only (`8080`) | ✅ Returns JSON `{ status, product, time }` |
| Web UI | No dedicated health route | ⚠️ Use HTTP 200 on `/` via nginx |

Production nginx proxies `/health` to API (see `deploy/lightsail/nginx/erp.shauninfomedia.com.conf`).

External monitor example:

```bash
curl -sf https://erp.shauninfomedia.com/health | jq .
```

**No** Web health endpoint for Blazor circuit status.

## Docker healthchecks

`shaunerp-db` uses `pg_isready` in compose file. API/Web rely on restart policy only.

## nginx logs

```bash
sudo tail -f /var/log/nginx/erp.shauninfomedia.access.log
sudo tail -f /var/log/nginx/erp.shauninfomedia.error.log
```

## Audit log (application-level)

Table: `AuditLogs` — **PARTIAL** coverage.

| Action examples | Source |
|-----------------|--------|
| `auth.login`, `auth.logout` | Auth services |
| `accounting.voucher.posted` | Posting service |
| `company.profile.updated` | Accounting service |

**Gap:** CRM/finance CRUD largely **not** audited. No UI to browse audit log.

Query example:

```sql
SELECT "OccurredAt", "Action", "UserId", "EntityType", "EntityId"
FROM "AuditLogs"
ORDER BY "OccurredAt" DESC
LIMIT 50;
```

## Background services

**None** — no job queue metrics or worker health.

## Recommended production monitoring (PLANNED)

| Layer | Tool suggestion |
|-------|-----------------|
| Uptime | UptimeRobot, Pingdom on `/health` |
| Logs | Loki + Promtail or CloudWatch |
| Metrics | Prometheus node_exporter + cAdvisor |
| Alerts | Disk >80%, container restart loop, health fail |
| DB | pg_stat_activity, connection count cron |

## Performance considerations

- Blazor Server: long-lived WebSocket — monitor nginx `proxy_read_timeout` (3600s configured)
- EF Core: `AddDbContextFactory` avoids concurrent context issues on Blazor
- No query performance logging enabled by default

## Security logging gaps

- Failed login attempts not logged
- No SIEM integration
- Swagger access not separately logged

See [14-SECURITY-GUIDE.md](14-SECURITY-GUIDE.md) and [PRODUCTION-READINESS-REPORT.md](PRODUCTION-READINESS-REPORT.md).
