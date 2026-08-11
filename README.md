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

Swagger (when `ASPNETCORE_ENVIRONMENT=Development`): `http://localhost:8080/swagger`

### Without Docker (Linux packages)

```bash
# Install .NET 8 SDK + PostgreSQL 16, create DB/user, then:
dotnet restore ShaunERP.sln
dotnet run --project src/ShaunERP.Api
```

Set `ConnectionStrings__DefaultConnection` and `Jwt__Key` via environment variables or `appsettings.json` (do not commit real secrets).

## Demo login (seeded on first start)

- Username: `admin`
- Password: `Admin@12345`

Change this immediately on any shared environment.

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
tests/
  ShaunERP.Tests/           # Unit/integration tests
```

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
