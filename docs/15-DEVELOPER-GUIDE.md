# Developer Guide

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker Desktop (or PostgreSQL 16 locally)
- IDE: VS 2022, Rider, or VS Code + C# Dev Kit

## Clone and restore

```bash
git clone <repo-url> ShaunERP
cd ShaunERP
dotnet restore ShaunERP.sln
cp .env.example .env
```

## Run locally

### Option A: Docker DB + local apps

```bash
docker compose up -d db
dotnet run --project src/ShaunERP.Web    # https://localhost:5xxx or http
dotnet run --project src/ShaunERP.Api    # swagger in Development
```

### Option B: Full Docker stack

```bash
docker compose up -d --build
```

Note: root `docker-compose.yml` includes API + DB only; add Web to compose or run Web locally.

## Configuration

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` | API JWT |
| `ASPNETCORE_ENVIRONMENT` | Development / Production |
| `DISABLE_SWAGGER` | Hide Swagger when `true` |

Use user secrets or `.env` — never commit passwords.

## Project conventions

### Layering

- **Core:** entities, enums, catalogues — no EF attributes required but allowed sparingly
- **Infrastructure:** DbContext, services, platform
- **Web / Api:** hosts only; business logic in Infrastructure

### Tenant scoping

Every query on tenant data must filter by `_tenant.CompanyId`. Pattern:

```csharp
await _db.Leads.Where(x => x.CompanyId == companyId).ToListAsync(ct);
```

### Permissions

Check entitlement + permission before mutations:

```csharp
_modules.EnsureEntitlement(ModuleKeys.Crm);
EnsureAccess(Permissions.CrmLeadsCreate);
```

### DbContext lifetime

Blazor Server uses `IDbContextFactory<ShaunErpDbContext>` to avoid concurrent use. Scoped `ShaunErpDbContext` resolved per operation in services.

## Adding a module feature

1. Entity in `ShaunERP.Core`
2. `DbSet` + `OnModelCreating` in `ShaunErpDbContext`
3. Schema patch in `DbSeeder` or new EF migration (prefer migration long-term)
4. Service interface + implementation in Infrastructure
5. Register in `DependencyInjection.cs`
6. Blazor pages under `Components/Pages/<Module>/`
7. Optional API controller
8. Permissions in `PermissionCatalog` + role defaults
9. Nav items in `ModuleCatalog`
10. Tests in `ShaunERP.Tests`

## Database schema changes

**Current (hybrid):** Add idempotent SQL to `DbSeeder` or `AccountingSchema`.

**Target:** EF Core migrations with `dotnet ef migrations add` and `Database.Migrate()` at startup.

Migration file exists: `20260810120000_PlatformFoundation.cs` — not wired to startup.

## Testing

```bash
dotnet test tests/ShaunERP.Tests/ShaunERP.Tests.csproj
```

Existing tests: accounting, tenant isolation, module registry.

## Code style

- Match surrounding file conventions
- Minimal comments; explain business rules only
- No over-abstraction for one-off helpers

## API development

Controllers in `ShaunERP.Api/Controllers`. JWT required except `[AllowAnonymous]`.

Run Swagger: `ASPNETCORE_ENVIRONMENT=Development` and open `/swagger`.

## Blazor development

- Interactive Server render mode
- Auth via cookie + `CookieAuthStateProvider`
- Shell state: `ShellStateService`, `IModuleRegistry`

## Docker build

```bash
docker build -f src/ShaunERP.Api/Dockerfile -t shaunerp-api .
docker build -f src/ShaunERP.Web/Dockerfile -t shaunerp-web .
```

Production: `docker compose -f docker-compose.prod.yml build`

## Useful paths

| Path | Content |
|------|---------|
| `ModuleCatalog.cs` | Module/nav definitions |
| `PermissionCatalog.cs` | All permission strings |
| `DbSeeder.cs` | Seed + schema patches |
| `deploy/lightsail/` | Production nginx |

## Legacy reference

See [LEGACY_REPO.md](LEGACY_REPO.md) — do not merge secrets or theme assets from archive.

## Documentation

When changing behaviour, update relevant `docs/*.md` and [18-CHANGELOG.md](18-CHANGELOG.md).
