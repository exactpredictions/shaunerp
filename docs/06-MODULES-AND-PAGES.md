# Modules and Pages

Route map for the Blazor Web application. Module metadata source: `ModuleCatalog.cs`.

**Legend:** ✅ COMPLETE · ⚠️ PARTIAL · 📋 PLANNED · 🗑️ DEPRECATED

## Home

| Route | Page | Status |
|-------|------|--------|
| `/` | Home dashboard | ✅ |

## User Management — ✅ COMPLETE

| Route | Screen | Permission |
|-------|--------|------------|
| `/users` | User list | `users.users.view` |
| `/users/new` | Create user | `users.users.create` |
| `/users/{id}` | Edit user | `users.users.update` |
| `/roles` | Roles list | `users.roles.view` |

API: `UsersController`, `RolesController`.

## CRM — ✅ COMPLETE

| Route | Screen | Permission |
|-------|--------|------------|
| `/crm` | CRM dashboard | `crm.dashboard.view` |
| `/crm/pipeline` | Pipeline board | `crm.pipeline.view` |
| `/crm/forecast` | Forecast | `crm.forecast.view` |
| `/crm/leads` | Leads list | `crm.leads.view` |
| `/crm/leads/{id}` | Lead edit | `crm.leads.update` |
| `/crm/prospects` | Prospects | `crm.prospects.view` |
| `/crm/prospects/{id}` | Prospect edit | `crm.prospects.update` |
| `/crm/opportunities` | Opportunities | `crm.opportunities.view` |
| `/crm/opportunities/{id}` | Opportunity edit | `crm.opportunities.update` |
| `/crm/quotes` | Quotes | `crm.quotes.view` |
| `/crm/quotes/{id}` | Quote edit | `crm.quotes.update` |
| `/crm/accounts` | Accounts | `crm.accounts.view` |
| `/crm/accounts/{id}` | Account edit | `crm.accounts.update` |
| `/crm/contacts` | Contacts | `crm.contacts.view` |
| `/crm/contacts/{id}` | Contact edit | `crm.contacts.update` |
| `/crm/customers` | Customers | `crm.customers.view` |
| `/crm/customers/{id}` | Customer edit | `crm.customers.update` |
| `/crm/campaigns` | Campaigns | `crm.campaigns.view` |
| `/crm/campaigns/{id}` | Campaign edit | `crm.campaigns.update` |
| `/crm/products` | Products | `crm.products.view` |
| `/crm/products/{id}` | Product edit | `crm.products.update` |
| `/crm/cases` | Cases | `crm.cases.view` |
| `/crm/cases/{id}` | Case edit | `crm.cases.update` |
| `/crm/tasks` | Tasks | `crm.tasks.view` |
| `/crm/tasks/{id}` | Task edit | `crm.tasks.update` |
| `/crm/meetings` | Meetings | `crm.meetings.view` |
| `/crm/meetings/{id}` | Meeting edit | `crm.meetings.update` |
| `/crm/calls` | Calls | `crm.calls.view` |
| `/crm/calls/{id}` | Call edit | `crm.calls.update` |
| `/crm/notes` | Notes | `crm.notes.view` |
| `/crm/notes/{id}` | Note edit | `crm.notes.update` |

API: full REST under `/api/crm/*`.

## Invoicing (Finance) — ✅ COMPLETE

| Route | Screen | Permission |
|-------|--------|------------|
| `/finance` | Finance dashboard | `finance.dashboard.view` |
| `/finance/invoices` | Invoice list | `finance.invoices.view` |
| `/finance/invoices/{id}` | Invoice edit | `finance.invoices.update` |
| `/finance/payments` | Payments | `finance.payments.record` |
| `/finance/settings` | Billing settings | `finance.settings.manage` |

API: `/api/finance/*` (invoices, payments, book).  
🗑️ Legacy `FinanceCustomer` and finance `Account` GL stubs — prefer CRM customers.

## Sales Orders — ⚠️ PARTIAL (UI ✅, API ❌)

| Route | Screen | Permission |
|-------|--------|------------|
| `/orders` | Orders dashboard | `orders.dashboard.view` |
| `/orders/list` | Sales orders | `orders.view` |
| `/orders/new` | New order | `orders.create` |
| `/orders/{id}` | Order edit | `orders.update` |

Service: `ISalesOrderService` (used by Web only). **No** `OrdersController`.

Features: confirm, fulfill, cancel, create invoice from order.

## Accounting — ⚠️ PARTIAL (UI + services ✅, API ❌)

| Route | Screen | Permission |
|-------|--------|------------|
| `/accounting` | Dashboard | `accounting.view` |
| `/accounting/vouchers/journal` | Journal voucher | `accounting.vouchers.create` |
| `/accounting/vouchers/receipt` | Receipt voucher | `accounting.vouchers.create` |
| `/accounting/vouchers/payment` | Payment voucher | `accounting.vouchers.create` |
| `/accounting/vouchers/contra` | Contra voucher | `accounting.vouchers.create` |
| `/accounting/vouchers` | All vouchers | `accounting.vouchers.view` |
| `/accounting/vouchers/{id}` | Voucher edit | `accounting.vouchers.view` |
| `/accounting/ledgers` | Chart of accounts | `accounting.ledgers.view` |
| `/accounting/groups` | Account groups | `accounting.ledgers.view` |
| `/accounting/voucher-types` | Voucher types | `accounting.settings.manage` |
| `/accounting/currencies` | Currencies | `accounting.settings.manage` |
| `/accounting/opening-balances` | Opening balances | `accounting.vouchers.create` |
| `/accounting/reports/*` | Financial reports | `accounting.reports.view` |
| `/accounting/settings/company` | Company profile | `company.settings.view` |
| `/accounting/settings/gst` | GST settings | `gst.settings.manage` |
| `/accounting/settings/financial-years` | Financial years | `accounting.financialyear.manage` |
| `/accounting/settings/period-lock` | Period lock | `accounting.periodlock.manage` |
| `/accounting/settings` | Accounting settings | `accounting.settings.manage` |

Reports routes include: `general-ledger`, `trial-balance`, `profit-loss`, `balance-sheet`, `day-book`, `cash-book`, `bank-book`, `receivables`, `payables`.

Services: `IAccountingService`, `IAccountingPostingService`, `IAccountingReportService`.

## Platform Admin — ⚠️ PARTIAL

| Route | Screen | Status |
|-------|--------|--------|
| `/platform` | Platform dashboard | Read-only tenant list |

Requires `platform.access` or system admin. **No** tenant CRUD UI; `/platform/tenants` nav item defined but not fully implemented.

## HRMS — 📋 PLANNED

Module disabled in catalogue (`IsPlaceholder = true`, `IsEnabled = false`). Entity stubs exist; no UI routes.

## Projects — 📋 PLANNED

Same as HRMS.

## Auth pages

| Route | Purpose |
|-------|---------|
| `/login` | Login form |
| `/Error` | Error boundary |

## Shell components

- `MainLayout.razor` — header, module switcher, sidebar
- `ModuleRow.razor` — module card on home
- Navigation driven by `IModuleRegistry` + user permissions

## Import / Export — 📋 PLANNED

No UI routes yet (milestone in progress). See [10-IMPORT-EXPORT.md](10-IMPORT-EXPORT.md).

## Module dependency graph

```
Home
Users (standalone)
CRM
  └── Finance (Invoicing)
        ├── Sales Orders
        └── Accounting
HRMS, Projects (future)
Platform (admin overlay)
```

See [MODULE_ROADMAP.md](MODULE_ROADMAP.md) for product sequence.
