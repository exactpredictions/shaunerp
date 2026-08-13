# ShaunERP

Open-source, self-hosted ERP MVP for SMEs. Modular **CRM**, **Finance**, **HRMS**, and **Projects** on a stack that runs cleanly on **Linux**.

| Layer | Choice |
|-------|--------|
| Runtime | .NET 8 (cross-platform: Linux / Windows / macOS) |
| Database | PostgreSQL 16 |
| Deploy | Docker Compose |

Domain models are carried forward from the original [HYPanchal/ShaunERP](https://github.com/HYPanchal/ShaunERP) prototype (SQL Server era), cleaned for public OSS: no secrets, no theme zips, PostgreSQL instead of SQL Server.

## Linux: yes

.NET 8 and PostgreSQL are first-class on Linux. Recommended production path:

```bash
# Ubuntu example — install Docker, then:
git clone https://github.com/exactpredictions/shaunerp.git
cd shaunerp
cp .env.example .env
# edit JWT_KEY and POSTGRES_PASSWORD
docker compose up -d --build
curl http://localhost:8080/health
```

Swagger (Development, or set `ENABLE_SWAGGER=true`): `http://localhost:8080/swagger`

### Without Docker (Linux packages)

```bash
# Install .NET 8 SDK + PostgreSQL 16, create DB/user, then:
dotnet restore ShaunERP.sln
dotnet run --project src/ShaunERP.Api
```

Set `ConnectionStrings__DefaultConnection` and `Jwt__Key` via environment variables or `appsettings.json` (do not commit real secrets).

## Demo login (Development only)

Demo admin (`admin` / `Admin@12345`) is seeded **only in Development** when `SEED_DEMO_DATA` is not `false`.  
In **Production**, the seeder does **not** create `Admin@12345`. Use:

```bash
export PROD_ADMIN_PASSWORD='...'
export PROD_ACCOUNTANT_PASSWORD='...'
./scripts/production-bootstrap-users.sh
```

Or: `dotnet run --project src/ShaunERP.Tools -- bootstrap-users --admin-password ... --accountant-password ...`

Change passwords immediately on any shared environment. Users created by bootstrap have `MustChangePassword=true`.

## MVP API surface

| Area | Endpoints |
|------|-----------|
| Health | `GET /health` |
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Companies | `GET /api/companies` |
| CRM | `/api/crm/dashboard`, `leads`, `prospects`, `contacts`, `opportunities`, `customers`, `tasks`, `meetings` |
| Finance | `/api/finance/books`, `accounts`, `customers`, `invoices`, `vendors`, `bills` |

## Project layout

```
src/
  ShaunERP.Api/             # HTTP API host (JWT + Swagger)
  ShaunERP.Core/            # Domain entities + platform module catalogue
  ShaunERP.Infrastructure/  # EF Core + PostgreSQL + auth/authz/seed
  ShaunERP.Web/             # Blazor Server application shell
  ShaunERP.Tools/           # Ops console (production user bootstrap)
tests/
  ShaunERP.Tests/           # Unit/integration tests
config/templates/           # India company configuration templates
samples/import/             # Sample CSV files for import importers
scripts/                    # Postgres backup/restore + production bootstrap
```

> Schema upgrades currently use `EnsureCreated` plus additive `IF NOT EXISTS` SQL in `DbSeeder`.  
> Do **not** enable `Database.Migrate()` until an EF migration snapshot is committed.

## Platform foundation

- Module registry (`ModuleCatalog` / `IModuleRegistry`) drives navigation and module switching
- Tenant context separates account entitlement from user permissions
- Shared application shell: header, module switcher, dynamic sidebar, main dashboard
- Audit log + user/account preferences for last module and favourites

## Local Windows (dev)

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Docker Desktop (or local Postgres).
2. `docker compose up -d db` (or full stack with `docker compose up -d --build`).
3. `dotnet run --project src/ShaunERP.Api`

## Roadmap (short)

1. EF migrations (replace `EnsureCreated` for production upgrades)
2. Multi-tenant company scoping on every query
3. Simple OSS-safe web UI (no commercial admin themes)
4. HRMS + Projects REST modules
5. Push cleaned tree to GitHub (replace bloated prototype or new `shaunerp` org repo)

## License

MIT — see [LICENSE](LICENSE).
