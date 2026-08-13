# Global UI / Import-Export Standards

## Import / Export

Reusable component: `Components/Shared/ImportExportPanel.razor`

Workflow: Upload → Validate/Preview → Confirm → Transactional apply → Error CSV

Permission: `settings.import_export` (or system admin)

| Entity key | Screens |
|------------|---------|
| customers | CRM → Customers |
| leads | CRM → Leads |
| contacts | CRM → Contacts |
| prospects | CRM → Prospects |
| opportunities | CRM → Opportunities |
| products | CRM → Products |
| vendors | (importer ready; no dedicated vendors list UI yet) |
| ledgers | Accounting → Chart of accounts |
| opening-balances | Accounting → Opening balances |
| invoices | Finance → Invoices |
| payments | Finance → Payments |
| sales-orders | Orders → List |
| inventory-items | (importer ready) |
| projects | Projects → List |
| project-tasks | Projects → Tasks |

Sample CSVs live under `samples/import/*-sample.csv`.

## Responsive layout

- Forms: `.form-grid` is 2-column on desktop; **forced 1-column under 900px** (global CSS at end of `app.css`).
- Tables: `.table-wrap` scrolls horizontally on narrow screens instead of crushing columns.
- Toolbars/actions: wrap; full-width buttons on phones.
- Header company name: `CompanyNameDisplay` truncates with ellipsis + `title`/`aria-label` for full name.

Breakpoints covered: ≤480, ≤700, ≤900, ≤1100, desktop.
