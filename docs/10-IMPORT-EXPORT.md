# Import & Export

**Status:** ✅ **COMPLETE** (framework + module importers) — with documented gaps for placeholder modules.

## Current state

Reusable CSV import/export lives under:

| Layer | Path |
|-------|------|
| Models | `src/ShaunERP.Core/ImportExport/` |
| Engine | `src/ShaunERP.Infrastructure/Services/ImportExport/` |
| Importers | `.../ImportExport/Importers/` |
| UI panel | `src/ShaunERP.Web/Components/Shared/ImportExportPanel.razor` |
| Samples | `samples/import/*-sample.csv` |

### Workflow

```text
Upload CSV → Parse (UTF-8 / quoted) → Validate headers → Validate rows
→ Preview (valid/invalid) → Confirm → Transactional apply → Audit → Summary
(+ downloadable error CSV for failed rows)
```

### Implemented entity keys

| Entity key | Import | Export | Sample file |
|------------|--------|--------|-------------|
| customers | ✅ | ✅ | `customers-sample.csv` |
| leads | ✅ | ✅ | `leads-sample.csv` |
| contacts | ✅ | ✅ | `contacts-sample.csv` |
| prospects | ✅ | ✅ | `prospects-sample.csv` |
| opportunities | ✅ | ✅ | `opportunities-sample.csv` |
| products | ✅ | ✅ | `products-sample.csv` |
| vendors | ✅ | ✅ | `vendors-sample.csv` |
| ledgers | ✅ | ✅ | `chart-of-accounts-sample.csv` |
| opening-balances | ✅ (ledger OB fields) | ✅ | `opening-balances-sample.csv` |
| invoices | ✅ (header / FK) | ✅ | `invoices-sample.csv` |
| payments | ✅ | ✅ | `payments-sample.csv` |
| sales-orders | ✅ (header / FK) | ✅ | `sales-orders-sample.csv` |
| inventory-items | ✅ | ✅ | `inventory-items-sample.csv` |

Sample files for **projects / tasks / employees / purchase-orders / receipts / services / invoice-items** exist as format references; those modules are **PARTIALLY IMPLEMENTED** or **PLANNED** and do not yet have production importers.

### Permissions

- `settings.import_export` required
- Tenant/`CompanyId` scoping enforced
- Cross-company export blocked

### Company configuration import

Separate from CSV: Settings → Company Configuration supports JSON/YAML template validate / preview / import / export (no secrets).

## Remaining gaps (P2)

- Line-level invoice import
- Full voucher CSV import
- Purchase order importer (module incomplete)
- Bulk soft-delete / dry-run modes beyond preview
