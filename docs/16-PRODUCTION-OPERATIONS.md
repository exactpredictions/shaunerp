# Production Operations

Day-2 operations for Shaun Infomedia ERP at `https://erp.shauninfomedia.com`.

## Server layout

| Path / resource | Purpose |
|-----------------|---------|
| `/opt/shaunerp` | Git clone, `.env`, compose |
| Docker volume `shaunerp_pgdata` | Database |
| Docker volume `shaunerp_web_keys` | Cookie encryption keys |
| `/var/log/nginx/erp.shauninfomedia.*.log` | Access/error logs |

## Daily checks

1. `curl -sf https://erp.shauninfomedia.com/health`
2. `docker compose -f docker-compose.prod.yml ps` — all `running`
3. Disk space: `df -h`
4. Review nginx error log for 5xx spikes

## Production initialization (milestone)

Work in progress to replace demo setup:

| Step | Action | Status |
|------|--------|--------|
| 1 | Deploy stack with Production env | ✅ |
| 2 | Remove demo admin or force password reset | ⚠️ Required |
| 3 | Delete demo-seed CRM/finance rows | ⚠️ Required |
| 4 | Create Shaun Infomedia company profile + GST | ⚠️ Required |
| 5 | Enter opening balances | ⚠️ Required |
| 6 | Import master data (customers, products, CoA) | 📋 Import/export in progress |
| 7 | Disable Swagger, tighten CORS | ⚠️ Required |

### Remove demo data (SQL examples)

**Review on staging first.**

```sql
-- Identify demo rows
SELECT COUNT(*) FROM "Customers" WHERE "Tags" = 'demo-seed';
SELECT COUNT(*) FROM "Invoices" WHERE "Notes" LIKE '%demo-seed%';

-- Delete in dependency order (example — adjust IDs)
DELETE FROM "InvoicePayments" WHERE "InvoiceId" IN (
  SELECT "Id" FROM "Invoices" WHERE "InvoiceNumber" LIKE 'DEMO-%'
);
-- Continue for lines, invoices, CRM entities tagged demo-seed
```

Prefer targeted deletes over full DB reset if real data already exists.

### Create production admin

Use Web UI `/users/new` as system admin, or API register with company name, then disable open registration.

## User administration

- Add users: `/users/new`
- Assign roles on edit screen
- Reset password: API `POST /api/users/{id}/reset-password` or UI equivalent

## Company switch

Users with multiple memberships use header account switcher.

## Deploying updates

```bash
cd /opt/shaunerp
# Backup first — see 12-BACKUP-RESTORE.md
git fetch && git checkout <tag-or-main>
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d
docker logs shaunerp-api --tail 50
```

Watch seeder logs for schema patch warnings.

## Restart services

```bash
docker compose -f docker-compose.prod.yml restart web api
docker compose -f docker-compose.prod.yml restart db   # brief outage
```

## Certificate renewal

certbot auto-renew via systemd timer. Verify:

```bash
sudo certbot renew --dry-run
```

## Environment variables (production)

See `deploy/lightsail/.env.prod.example`. Secrets: [private/PRODUCTION-CREDENTIALS.md](private/PRODUCTION-CREDENTIALS.md).

Required:

- `POSTGRES_PASSWORD`
- `JWT_KEY`
- `ASPNETCORE_ENVIRONMENT=Production`

Recommended:

- `DISABLE_SWAGGER=true`

## Financial period operations

End of month:

1. Run trial balance in accounting
2. Lock period if books closed (`/accounting/settings/period-lock`)
3. Close financial year when applicable

## Support escalation

| Issue | First action |
|-------|--------------|
| Site down | Check docker ps, nginx, health endpoint |
| Login failures | Check user `IsActive`, BCrypt password reset |
| Wrong company data | Verify user `CompanyId` context |
| Report imbalance | Review unposted vouchers, OB entries |
| DB full | Vacuum, archive audit logs, expand disk |

## Maintenance window communication

Notify users before DB restart or restore. Blazor circuits disconnect on Web restart.

## Related runbooks

- [12-BACKUP-RESTORE.md](12-BACKUP-RESTORE.md)
- [17-DISASTER-RECOVERY.md](17-DISASTER-RECOVERY.md)
- [14-SECURITY-GUIDE.md](14-SECURITY-GUIDE.md)
