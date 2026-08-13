# Security Guide

Security posture for ShaunERP production at Shaun Infomedia.

## Authentication

| Control | Status | Notes |
|---------|--------|-------|
| BCrypt password hashing | ✅ | Work factor default |
| Cookie HttpOnly (Web) | ✅ | ASP.NET defaults |
| JWT expiry (12h) | ✅ | |
| MFA | ❌ PLANNED | |
| Account lockout | ❌ PLANNED | |
| Password policy | ⚠️ MINIMAL | No complexity enforcement in code |

### P0: Demo admin credentials

Seeder creates `admin` with documented default password. **Must change or delete before production.**

## Authorization

| Control | Status |
|---------|--------|
| Role + permission checks in services | ✅ |
| Tenant isolation via `CompanyId` | ✅ (tests exist) |
| System admin bypass | ✅ Intentional |

Review permission assignments when onboarding staff.

## Network security

| Control | Status |
|---------|--------|
| DB not exposed publicly | ✅ prod compose |
| API/Web bind localhost only | ✅ `127.0.0.1:8080/8081` |
| TLS via nginx + Let's Encrypt | ✅ |
| Firewall 22/80/443 only | ✅ Recommended |

## API security gaps

### CORS AllowAll — **P0**

```csharp
// Program.cs — API
policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
```

**Risk:** Any browser origin can call API if user has valid JWT.

**Mitigation:** Restrict to `https://erp.shauninfomedia.com` before go-live.

### Swagger exposure — **P1**

Enabled unless `DISABLE_SWAGGER=true`. Exposes API surface publicly via nginx.

**Mitigation:** Set env var in production `.env`; optionally block `/swagger` in nginx.

### Open registration — **P1**

`POST /api/auth/register` is `[AllowAnonymous]`.

**Mitigation:** Disable in production or protect via nginx `deny all`.

## Secrets management

| Secret | Storage |
|--------|---------|
| `POSTGRES_PASSWORD` | `.env` — [private/PRODUCTION-CREDENTIALS.md](private/PRODUCTION-CREDENTIALS.md) |
| `JWT_KEY` | `.env` |
| Never commit | `.env`, `docs/private/*` in `.gitignore` |

Rotate JWT key invalidates all sessions.

## Data protection (Web)

Blazor cookie encryption keys in Docker volume `shaunerp_web_keys`. Backup volume or users re-login after loss.

## Session storage

Full JWT stored in `Sessions` table. Consider hashing at rest (PLANNED).

## Input validation

- EF Core parameterized queries (SQL injection mitigated)
- Antiforgery on Blazor forms; login endpoint explicitly disables antiforgery
- No comprehensive XSS audit documented

## Audit trail — **PARTIAL**

Insufficient for compliance-heavy scenarios. See [13-LOGGING-MONITORING.md](13-LOGGING-MONITORING.md).

## Dependency security

- .NET 8 LTS — keep runtime images updated
- `docker compose build --pull` periodically
- No automated Dependabot documented in repo

## PostgreSQL

- App user is container superuser (default image behaviour) — consider least-privilege role
- No SSL inside Docker network (acceptable on internal bridge; use TLS for external DBA access via SSH tunnel)

## Compliance notes (India)

- GST data stored in application DB — ensure backups encrypted at rest
- No DPDP-specific features (consent, erasure workflows) — **PLANNED** if required

## Security checklist before go-live

- [ ] Remove/rotate demo admin
- [ ] Strong unique `POSTGRES_PASSWORD` and `JWT_KEY`
- [ ] `DISABLE_SWAGGER=true`
- [ ] Restrict CORS origins
- [ ] Block or remove public registration
- [ ] Verify DB port not public
- [ ] Enable automated encrypted backups
- [ ] Document incident contacts in [17-DISASTER-RECOVERY.md](17-DISASTER-RECOVERY.md)

## Reporting vulnerabilities

Contact Shaun Infomedia security contact via https://shauninfomedia.com/contact.html (internal process).
