# Database Connection Guide

Credentials for production are **not** stored in this document. See [docs/private/PRODUCTION-CREDENTIALS.md](private/PRODUCTION-CREDENTIALS.md).

## Connection parameters

| Parameter | Production value |
|-----------|------------------|
| Host (from host OS) | Not exposed — use `docker exec` |
| Host (from app containers) | `db` |
| Port | `5432` |
| Database | `shaunerp` |
| Username | `shaunerp` |
| Password | See private credentials file |

## PostgreSQL superuser note

The official `postgres:16-alpine` image creates the user named in `POSTGRES_USER` as a **superuser** inside the container. In production, `POSTGRES_USER=shaunerp` means `shaunerp` is typically both the application user and the container superuser.

For hardened deployments, consider:

1. Creating a non-superuser application role with least privilege
2. Retaining superuser access only for DBA tasks via `docker exec`

This is **not** yet automated in ShaunERP compose files.

## Docker exec — interactive psql

```bash
# From production server
docker exec -it shaunerp-db psql -U shaunerp -d shaunerp
```

Useful commands inside `psql`:

```sql
\dt                          -- list tables
\d "Invoices"                -- describe table
SELECT COUNT(*) FROM "Users";
SELECT "Email", "IsActive" FROM "Users" LIMIT 10;
\q                           -- quit
```

## One-shot SQL from shell

```bash
docker exec shaunerp-db psql -U shaunerp -d shaunerp -c "SELECT version();"
docker exec shaunerp-db psql -U shaunerp -d shaunerp -c "SELECT COUNT(*) FROM \"Companies\";"
```

## Connection from application containers

```bash
docker exec -it shaunerp-api bash
# Inside container — env already has ConnectionStrings__DefaultConnection
dotnet --info   # verify runtime if debugging
```

Apps resolve hostname `db` on the `shaunerp_internal` Docker network.

## GUI clients (pgAdmin, DBeaver, DataGrip)

PostgreSQL is **not** published to the internet in production. Options:

### A. SSH tunnel (recommended)

```bash
# On your workstation
ssh -L 15432:127.0.0.1:5432 user@your-server-ip
```

Then connect GUI to:

| Field | Value |
|-------|-------|
| Host | `localhost` |
| Port | `15432` |
| Database | `shaunerp` |
| User | `shaunerp` |
| Password | From private credentials file |
| SSL | Prefer; tunnel carries encryption |

### B. Temporary port publish (dev only)

Dev `docker-compose.yml` exposes `${POSTGRES_PORT:-5432}:5432`. **Do not** use on production.

### pgAdmin

1. Register → Server → Connection tab with tunnel settings above
2. Maintenance → Backup/Restore uses same connection

### DBeaver

1. New connection → PostgreSQL
2. Test connection after SSH tunnel active
3. Enable "Show all databases" if needed

### DataGrip

1. Data Source → PostgreSQL
2. SSH/SSL tab → configure SSH tunnel to production host
3. Use schema `public`; tables are quoted PascalCase names

## Connection string formats

**Npgsql (.NET):**

```
Host=db;Port=5432;Database=shaunerp;Username=shaunerp;Password=<see private file>
```

**URI:**

```
postgresql://shaunerp:<password>@db:5432/shaunerp
```

## Backup via docker exec

```bash
docker exec shaunerp-db pg_dump -U shaunerp -d shaunerp -Fc -f /tmp/shaunerp.dump
docker cp shaunerp-db:/tmp/shaunerp.dump ./shaunerp-$(date +%Y%m%d).dump
```

Plain SQL:

```bash
docker exec shaunerp-db pg_dump -U shaunerp -d shaunerp > shaunerp-$(date +%Y%m%d).sql
```

## Restore via docker exec

Custom format:

```bash
docker cp ./shaunerp-backup.dump shaunerp-db:/tmp/restore.dump
docker exec shaunerp-db pg_restore -U shaunerp -d shaunerp --clean --if-exists /tmp/restore.dump
```

Plain SQL (drops may be embedded in dump):

```bash
docker exec -i shaunerp-db psql -U shaunerp -d shaunerp < shaunerp-backup.sql
```

**Always** stop API/Web during full restore to avoid concurrent writes:

```bash
docker compose -f docker-compose.prod.yml stop api web
# ... restore ...
docker compose -f docker-compose.prod.yml start api web
```

## Health check

```bash
docker exec shaunerp-db pg_isready -U shaunerp -d shaunerp
```

Compose healthcheck uses the same command.

## Troubleshooting

| Symptom | Check |
|---------|-------|
| `connection refused` from host to 5432 | Expected in prod — use docker exec or tunnel |
| `password authentication failed` | Verify password in private file matches `.env` |
| `database "shaunerp" does not exist` | Volume may be fresh; restart stack to run seeder |
| Too many connections | Restart `shaunerp-db` or tune `max_connections` |

See also [12-BACKUP-RESTORE.md](12-BACKUP-RESTORE.md) and [16-PRODUCTION-OPERATIONS.md](16-PRODUCTION-OPERATIONS.md).
