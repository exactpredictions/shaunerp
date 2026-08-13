# Authentication & Authorization

## Overview

ShaunERP uses **custom authentication** — not ASP.NET Identity. Passwords are hashed with **BCrypt**. Authorization combines:

1. **System admin flag** (`User.IsSystemAdmin`)
2. **Module entitlements** (per company, `CompanyModules`)
3. **Role-based permissions** (Admin / Manager / User → `Permissions.*` strings)

## Authentication mechanisms

### Blazor Web — cookie auth (**COMPLETE**)

| Item | Detail |
|------|--------|
| Scheme | `CookieAuthenticationDefaults.AuthenticationScheme` |
| Login | `POST /account/login` (form post, antiforgery disabled on endpoint) |
| Logout | `GET /account/logout` |
| Session | 12 hours, sliding expiration |
| Account switch | `POST /account/switch` with `companyId` |
| Validation | `AuthSessionService` → BCrypt verify → claims principal |

Data Protection keys: filesystem `/app/keys` (Docker volume in production).

### API — JWT Bearer (**COMPLETE**)

| Item | Detail |
|------|--------|
| Login | `POST /api/auth/login` |
| Register | `POST /api/auth/register` (creates user; optional company) |
| Profile | `GET /api/auth/me` |
| Token lifetime | 12 hours |
| Config | `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` (env / appsettings) |
| Session storage | Row in `Sessions` table with token and expiry |

Swagger documents Bearer auth when enabled.

## Roles — system-wide definitions

Roles are stored with `CompanyId = null` (global templates):

| Role | Description | Default permission set |
|------|-------------|------------------------|
| **Admin** | Full access | `Permissions.AdminDefaults` (all permissions) |
| **Manager** | CRM + invoicing + accounting ops | `Permissions.ManagerDefaults` |
| **User** | Standard operational access | `Permissions.UserDefaults` |

Assignment: `UserRoles` links a `CompanyUser` to a `Role`.

## Permissions

Defined in `PermissionCatalog` (`Permissions` static class). Examples:

- `crm.leads.create`, `finance.invoices.view`, `accounting.vouchers.post`
- `platform.tenants.manage`, `users.users.delete`

Full list: see `src/ShaunERP.Core/Platform/PermissionCatalog.cs`.

UI and services call `ITenantContext.Permissions` and `EnsureAccess()` patterns in services.

## Module entitlements

Separate from permissions. A user may have `crm.access` permission but the company must have CRM enabled in `CompanyModules`.

`ModuleCatalog` defines required entitlement and permission per module.

System admins receive all entitlements in memory when building context.

## Tenant context

`ITenantContext` (scoped per request/circuit):

| Property | Purpose |
|----------|---------|
| `UserId` | Current user |
| `CompanyId` | Active tenant |
| `CompanyName` | Display |
| `IsSystemAdmin` | Bypass entitlement checks |
| `Permissions` | Effective permission set |
| `Entitlements` | Enabled module keys |

Hydrated in middleware (API and Web) from authenticated principal, refreshed from DB via `AuthorizationContextService`.

## Multi-company users

A user may belong to multiple companies (`CompanyUsers`). Login selects:

- Preferred `CompanyId` in login request (API), or
- Default/first membership (Web)

Switch account without re-entering password via `/account/switch`.

## Authorization in code

```csharp
// Typical service pattern
_modules.EnsureEntitlement(ModuleKeys.Accounting);
EnsureAccess(Permissions.AccountingVouchersPost);
var companyId = _tenant.CompanyId ?? throw new ForbiddenException(...);
// Query filtered by companyId
```

API returns 401/403; Web shows "not authorized" in `AuthorizeRouteView`.

## Audit on auth events — **PARTIAL**

| Event | Logged |
|-------|--------|
| `auth.login` | Yes (API) |
| `auth.logout` | Yes (Web) |
| `auth.login` | Web login via `AuthSessionService` |
| Password change | **Not audited** |
| Permission change | **Not audited** |
| Failed login attempts | **Not logged** |

## Production security notes

| Issue | Severity | Mitigation |
|-------|----------|------------|
| Demo admin `admin` / known password | **P0** | Remove or rotate before go-live |
| Open registration API | High | Disable or restrict in production nginx |
| JWT in `Sessions` table (full token) | Medium | Consider hashing token at rest |
| No MFA | Medium | Planned future enhancement |
| No account lockout | Medium | Planned |

See [14-SECURITY-GUIDE.md](14-SECURITY-GUIDE.md).

## Related endpoints

Documented in [20-API-ENDPOINTS.md](20-API-ENDPOINTS.md) under Auth, Users, Roles, Companies.
