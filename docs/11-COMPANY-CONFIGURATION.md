# Company Configuration

Settings that define how a tenant company operates in ShaunERP.

## Tenant vs billing book vs accounting profile

| Concept | Entity | Purpose |
|---------|--------|---------|
| Tenant account | `Company` | Login boundary, module entitlements |
| Billing book | `CompanyMaster` | Invoice numbering, AR settings |
| Accounting profile | `CompanyProfile` | Statutory identity for books |
| GST | `CompanyGstSettings` | Tax registration and defaults |

Link: `CompanyMaster.TenantCompanyId` → `Company.Id`.

## Module entitlements — ✅ COMPLETE

Table: `CompanyModules`

| Field | Description |
|-------|-------------|
| `ModuleKey` | Normalized key (`crm`, `finance`, `accounting`, …) |
| `ModuleName` | Display label |
| `IsEnabled` | Toggle module for company |

Seeder ensures Orders and Accounting enabled for all companies. HRMS/Projects seeded but disabled in UI catalogue.

Manage programmatically or via future platform admin UI (**PARTIAL**).

## Company profile (accounting) — ✅ COMPLETE

Route: `/accounting/settings/company`  
Permission: `company.settings.view` / `company.settings.edit`

Fields include trade name, legal name, address, contact, PAN, CIN, logo path, base currency.

Service: `IAccountingService.UpdateCompanyProfileAsync` — **audited**.

## GST settings — ✅ COMPLETE

Route: `/accounting/settings/gst`  
Permission: `gst.settings.manage`

- GSTIN, registration type (Regular, Composition, etc.)
- Default place of supply
- Reverse charge flags
- Associated `GstTaxRates`

## Financial year — ✅ COMPLETE

Route: `/accounting/settings/financial-years`  
Permission: `accounting.financialyear.manage`

- Define FY periods (name, start, end)
- Set current year
- Close / reopen (audited)

## Accounting settings — ✅ COMPLETE

Route: `/accounting/settings`

- Voucher numbering method
- Default voucher types
- Inventory valuation method (for future inventory)
- Rounding ledger references

## Period lock — ✅ COMPLETE

Route: `/accounting/settings/period-lock`  
Blocks voucher posting on or before locked date.

## Billing settings (finance) — ✅ COMPLETE

Route: `/finance/settings`

- Invoice prefix and next number
- Order prefix and next number (`OrderNumberPrefix`, `NextOrderSequence`)
- Default payment terms and due days
- Company address on invoices

## Currency — ✅ COMPLETE

Route: `/accounting/currencies`

- `CurrencyMaster` per company
- `ExchangeRate` history (manual entry)

Multi-currency reporting is **basic** — functional currency assumed for statutory reports.

## Opening balances — ✅ COMPLETE (UI)

Route: `/accounting/opening-balances`

Journal-based OB entries into ledgers. Bulk import **PLANNED** ([10-IMPORT-EXPORT.md](10-IMPORT-EXPORT.md)).

## Chart of accounts setup — ✅ COMPLETE

Routes: `/accounting/groups`, `/accounting/ledgers`, `/accounting/voucher-types`

Initial system groups/ledgers seeded by `AccountingSchema` on first run.

## User-facing company switch

Header account switcher → `POST /account/switch` with target `companyId`. User must be member (`CompanyUsers`).

## Platform-level configuration

| Setting | Where |
|---------|-------|
| DB password | `.env` on server → [private/PRODUCTION-CREDENTIALS.md](private/PRODUCTION-CREDENTIALS.md) |
| JWT key | `.env` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DISABLE_SWAGGER` | Optional `.env` |
| nginx TLS | `/etc/nginx`, certbot |
| Blazor data protection keys | Docker volume `shaunerp_web_keys` |

## Production checklist for Shaun Infomedia

1. Create real `Company` (not Demo Company)
2. Configure `CompanyProfile` with legal entity details
3. Enter GSTIN and tax rates
4. Set financial year to active Indian FY
5. Import or enter opening balances
6. Configure invoice numbering on billing book
7. Disable demo users and demo-seed data
8. Verify module entitlements match licensed modules

See [16-PRODUCTION-OPERATIONS.md](16-PRODUCTION-OPERATIONS.md).
