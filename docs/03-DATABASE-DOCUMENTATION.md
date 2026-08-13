# Database Documentation

## Engine

- **PostgreSQL 16** (Alpine Docker image)
- **Database name:** `shaunerp`
- **Application user:** `shaunerp` (from `POSTGRES_USER`)
- **Password:** stored in [docs/private/PRODUCTION-CREDENTIALS.md](private/PRODUCTION-CREDENTIALS.md) — never commit to git

## Connection (inside Docker network)

```
Host=db;Port=5432;Database=shaunerp;Username=shaunerp;Password=<from private file>
```

Both `shaunerp-api` and `shaunerp-web` use the same connection string via `ConnectionStrings__DefaultConnection`.

## Schema management — **PARTIALLY IMPLEMENTED**

| Approach | Status | Notes |
|----------|--------|-------|
| EF `EnsureCreatedAsync()` | Active | Runs on every API/Web startup via `DbSeeder` |
| Raw SQL patches in `DbSeeder` | Active | CRM, finance, sales orders, accounting columns/tables |
| `AccountingSchema.EnsureAsync` | Active | Accounting tables and seed data |
| EF migration `PlatformFoundation` | **Exists, not applied** | File: `Migrations/20260810120000_PlatformFoundation.cs`; startup does **not** call `Migrate()` |

**Production risk:** No versioned, reversible migration pipeline. Upgrades depend on idempotent `IF NOT EXISTS` SQL.

## Entity Framework context

Single context: `ShaunErpDbContext` in `ShaunERP.Infrastructure/Persistence`.

### DbSet inventory

#### Identity / platform

| DbSet | Table | Purpose |
|-------|-------|---------|
| `Companies` | Companies | Tenant accounts |
| `Users` | Users | Login identities |
| `CompanyUsers` | CompanyUsers | User ↔ company membership |
| `Roles` | Roles | Admin / Manager / User |
| `Permissions` | Permissions | Fine-grained permission strings |
| `UserRoles` | UserRoles | Role assignment per membership |
| `RolePermissions` | RolePermissions | Role ↔ permission |
| `CompanyModules` | CompanyModules | Module entitlements per company |
| `Sessions` | Sessions | JWT session records |
| `UserCompanyPreferences` | UserCompanyPreferences | UI prefs (last module, favourites) |
| `AuditLogs` | AuditLogs | Audit trail |

#### CRM

Leads, Prospects, CrmAccounts, Contacts, Opportunities, Customers, CrmTasks, Meetings, CrmCalls, CrmNotes, Campaigns, CrmProducts, Quotes, QuoteLines, CrmCases

#### Finance (invoicing)

CompanyMasters, Accounts (legacy GL stub), FinanceCustomers (legacy), Invoices, InvoiceLines, InvoicePayments, Vendors (stub), Bills (stub)

#### Sales

SalesOrders, SalesOrderLines

#### Accounting

CompanyProfiles, CompanyStatutoryRegistrations, CompanyGstSettings, GstTaxRates, Currencies, ExchangeRates, FinancialYears, AccountingPeriodLocks, AccountingSettings, AccountGroups, Ledgers, VoucherTypes, AccountingVouchers, AccountingVoucherLines, AccountingAllocations, InventoryItemCategories, UnitsOfMeasure, InventoryItems

#### HRMS / Projects — **PLANNED** (schema may exist from EnsureCreated)

Departments, Employees, LeaveRequests, Projects, Issues

## Naming conventions

- PascalCase table and column names with double quotes (EF default for PostgreSQL)
- All business entities inherit `BaseEntity`: `Id`, `CreatedDate`, `UpdatedDate`, `CreatedBy`, `ModifiedBy`, `Status`
- Tenant-scoped rows use nullable `CompanyId` (int); services enforce filter via `ITenantContext`

## Indexes (selected)

| Index | Table | Columns |
|-------|-------|---------|
| Unique email | Users | Email |
| Unique username | Users | Username |
| Tenant invoice number | Invoices | CompanyId, InvoiceNumber |
| Tenant order number | SalesOrders | CompanyId, OrderNumber |
| Tenant ledger code | Ledgers | CompanyId, LedgerCode |
| Unique voucher number | AccountingVouchers | CompanyId, VoucherNumber |
| Permission name | Permissions | Name |

## Decimal precision

Global rule in `OnModelCreating`: all `decimal` properties → `numeric(18,2)`.

## Seeding behaviour

`DbSeeder.SeedAsync()` on startup:

1. Ensure database and schema patches
2. Seed roles and permissions if missing
3. If no users: create demo admin + Demo Company (**remove for production**)
4. Optionally seed rich CRM/finance demo data (`demo-seed` tags)

## Related docs

- [04-DATABASE-CONNECTION-GUIDE.md](04-DATABASE-CONNECTION-GUIDE.md) — connect and administer
- [19-DATABASE-DICTIONARY.md](19-DATABASE-DICTIONARY.md) — table reference
- [12-BACKUP-RESTORE.md](12-BACKUP-RESTORE.md) — backup procedures
