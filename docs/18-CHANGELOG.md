# Changelog

Documentation and release history for ShaunERP (Shaun Infomedia production track).

Format based on [Keep a Changelog](https://keepachangelog.com/). Version tags follow git releases when published.

## [Unreleased] — production documentation milestone

### Added

- Complete `docs/` set for Shaun Infomedia production (this documentation pass)
- `docker-compose.prod.yml` with Web + API + DB on internal network
- Production nginx config for Blazor WebSocket + API split
- Accounting module: CoA, vouchers (JV/RV/PV/CV), GST settings, FY, period lock, opening balances, reports (GL/TB/P&L/BS/day/cash/bank/AR/AP)
- Sales Orders UI with invoice-from-order flow
- Platform foundation: module registry, tenant context, audit log table, user preferences
- CRM extended entities: accounts, campaigns, products, quotes, cases, calls, notes
- Invoice line items, payments, CRM customer linkage
- Tests: accounting, tenant isolation, module registry

### In progress (this milestone)

- Production init: remove demo admin and demo-seed data
- Import/export for masters and opening balances
- EF migrations pipeline (replace EnsureCreated-only upgrades)
- API controllers for Accounting and Sales Orders
- CORS hardening and Swagger disable in production

### Known issues

- `EnsureCreatedAsync` + raw SQL patches instead of formal migrations
- CORS `AllowAll` on API
- Demo credentials seeded on empty database
- Audit logging partial (auth + some accounting only)
- AP/vendor bills: entity stubs only, no UI
- No background jobs or email notifications

---

## Prior MVP phases (summary)

### Phase 1 — User Management ✅

Users, roles (Admin/Manager/User), permissions catalogue, Blazor UI, REST API.

### Phase 2 — CRM ✅

Full CRM lifecycle UI + `/api/crm/*` REST surface.

### Phase 3 — Invoicing ✅

Finance dashboard, invoices, payments, billing book, `/api/finance/*`.

### Phase 4 — Sales Orders ⚠️

UI + services complete; REST API not added.

### Phase 5 — Accounting ⚠️

UI + services + invoice posting; REST API not added.

---

## Documentation index

| File | Description |
|------|-------------|
| 00-PROJECT-OVERVIEW | Executive summary |
| 01-SYSTEM-ARCHITECTURE | Technical architecture |
| 02-SERVER-DEPLOYMENT | Deploy guide |
| 03–04 | Database docs + connection |
| 05 | Auth/authz |
| 06 | Modules and routes |
| 07–09 | Accounting, CRM, sales/invoicing |
| 10 | Import/export (planned) |
| 11 | Company configuration |
| 12–17 | Ops, backup, security, DR |
| 18 | This changelog |
| 19 | Database dictionary |
| 20 | API endpoints |
| ACCOUNTING-GAP-ANALYSIS | Accounting backlog |
| PRODUCTION-READINESS-REPORT | Go-live scorecard |

Legacy references: [LEGACY_REPO.md](LEGACY_REPO.md), [MODULE_ROADMAP.md](MODULE_ROADMAP.md).
