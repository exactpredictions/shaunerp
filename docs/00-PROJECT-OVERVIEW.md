# ShaunERP — Project Overview

**Owner:** Shaun Infomedia OPC Private Limited  
**Production URL:** https://erp.shauninfomedia.com  
**Company site:** https://shauninfomedia.com  
**Repository path (production server):** `/opt/shaunerp`

## Purpose

ShaunERP is a self-hosted, modular ERP for SMEs. It provides CRM, invoicing, sales orders, and double-entry accounting on a .NET 8 + PostgreSQL stack, deployed via Docker Compose behind nginx with Let's Encrypt TLS.

## Technology stack

| Layer | Technology | Version / notes |
|-------|------------|-----------------|
| Runtime | .NET | 8 |
| Web UI | Blazor Server | Interactive Server components |
| API | ASP.NET Core Web API | JWT authentication |
| Database | PostgreSQL | 16 (Alpine image) |
| ORM | Entity Framework Core | Npgsql provider |
| Password hashing | BCrypt | Not ASP.NET Identity |
| Deployment | Docker Compose | `docker-compose.prod.yml` on production |
| Reverse proxy | nginx | TLS termination, WebSocket for Blazor |

## Solution structure

```
ShaunERP.sln
src/
  ShaunERP.Core/            Domain entities, ModuleCatalog, PermissionCatalog
  ShaunERP.Infrastructure/  EF Core, services, auth, seeding
  ShaunERP.Api/             REST API (JWT)
  ShaunERP.Web/             Blazor Server UI (cookie auth)
tests/
  ShaunERP.Tests/           Unit/integration tests
deploy/lightsail/           nginx vhosts, server setup scripts
docs/                       This documentation set
```

## Module status summary

| Module | Status | API | UI |
|--------|--------|-----|-----|
| User Management | **COMPLETE** | Yes | Yes |
| CRM | **COMPLETE** | Yes | Yes |
| Invoicing (Finance) | **COMPLETE** | Yes | Yes |
| Sales Orders | **PARTIALLY IMPLEMENTED** | No REST controller | Yes |
| Accounting | **PARTIALLY IMPLEMENTED** | No REST controller | Yes (services) |
| Platform Admin | **PARTIALLY IMPLEMENTED** | Limited | Read-only dashboard |
| HRMS | **PLANNED** | Entity stubs only | Placeholder |
| Projects | **PLANNED** | Entity stubs only | Placeholder |
| Import / Export | **PLANNED** (milestone in progress) | Not yet | Not yet |

## Multi-tenancy model

- **Company** = tenant account (`Companies` table).
- Users join companies via `CompanyUsers`; roles and permissions are scoped per company membership.
- **Module entitlements** (`CompanyModules`) control which modules a company may use.
- **Tenant scoping** at runtime via `ITenantContext.CompanyId`; services filter data by company.

## Authentication (high level)

| Host | Scheme | Session |
|------|--------|---------|
| Blazor Web (`shaunerp-web`) | Cookie authentication | 12 h sliding |
| API (`shaunerp-api`) | JWT Bearer | 12 h; stored in `Sessions` |

Custom auth — not ASP.NET Identity. Roles: **Admin**, **Manager**, **User** (system-wide role definitions; assigned per company user).

## Production containers

| Container | Bind address | Purpose |
|-----------|--------------|---------|
| `shaunerp-web` | `127.0.0.1:8081` | Blazor UI |
| `shaunerp-api` | `127.0.0.1:8080` | REST API |
| `shaunerp-db` | Internal only | PostgreSQL 16, volume `shaunerp_pgdata` |

## Demo vs production

On first startup, `DbSeeder` may create:

- User `admin` / password documented in README (must be removed or changed for production).
- **Demo Company** with rich CRM/finance sample data tagged `demo-seed`.

**Production init work (in progress this milestone):** disable demo seed, provision real company and admin, remove default credentials.

## Related documents

| Doc | Topic |
|-----|-------|
| [01-SYSTEM-ARCHITECTURE.md](01-SYSTEM-ARCHITECTURE.md) | Components and data flow |
| [02-SERVER-DEPLOYMENT.md](02-SERVER-DEPLOYMENT.md) | Deploy procedure |
| [06-MODULES-AND-PAGES.md](06-MODULES-AND-PAGES.md) | Routes and screens |
| [PRODUCTION-READINESS-REPORT.md](PRODUCTION-READINESS-REPORT.md) | Go-live assessment |
| [MODULE_ROADMAP.md](MODULE_ROADMAP.md) | Product build order |
| [LEGACY_REPO.md](LEGACY_REPO.md) | Archive references |

## License

MIT — see repository [LICENSE](../LICENSE).
