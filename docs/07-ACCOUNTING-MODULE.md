# Accounting Module

**Status:** ⚠️ **PARTIALLY IMPLEMENTED** — full UI and server services; no REST API controller; AP/vendor workflows incomplete.

## Scope

Double-entry accounting for Indian GST-oriented SMEs:

- Chart of accounts (groups + ledgers)
- Voucher types: Journal (JV), Receipt (RV), Payment (PV), Contra (CV)
- GST configuration and tax rates
- Financial years and period locking
- Opening balances
- Financial reports
- Automatic posting from AR invoices

## Architecture

| Component | Location |
|-----------|----------|
| Entities | `ShaunERP.Core/Accounting/AccountingEntities.cs` |
| Schema bootstrap | `AccountingSchema.EnsureAsync` |
| Business logic | `AccountingService.cs` |
| Posting engine | `AccountingPostingService.cs` |
| Reports | `AccountingReportService.cs` |
| UI | `src/ShaunERP.Web/Components/Pages/Accounting/**` |

Web app calls services directly (same as CRM pattern before API parity).

## Chart of accounts — ✅ COMPLETE

- **Account groups** — hierarchical (`AccountGroups`), system codes in `SystemGroupCodes`
- **Ledgers** — leaf accounts (`Ledgers`), unique `LedgerCode` per company
- **System ledgers** — seeded codes: `CASH`, `BANK`, `AR`, `AP`, `SALES`, GST input/output, etc.

Nature: Asset, Liability, Equity, Income, Expense. Balance side: Debit / Credit.

## Vouchers — ✅ COMPLETE (UI + services)

| Type | Code | Purpose |
|------|------|---------|
| Journal | JV | General adjustments |
| Receipt | RV | Money in |
| Payment | PV | Money out |
| Contra | CV | Cash ↔ bank transfers |

Lifecycle: Draft → Posted → (optional) Reversed.

Rules enforced in `AccountingPostingService`:

- Minimum two lines
- Debits = credits
- Period lock respected
- Idempotent posting by source reference (`SourceModule`, `SourceEntityType`, `SourceEntityId`)

## Company configuration — ✅ COMPLETE

- `CompanyProfile` — legal/trade name, address, PAN, etc.
- `CompanyGstSettings` — GSTIN, registration type, default tax behaviour
- `CompanyStatutoryRegistrations`
- `AccountingSettings` — numbering, valuation method, defaults
- `FinancialYears` — create, set current, close, reopen
- `AccountingPeriodLocks` — block posting before date

## GST — ✅ COMPLETE (settings); 📋 PLANNED (returns filing)

- `GstTaxRates` per company (CGST, SGST, IGST, UTGST, Cess)
- GST split on invoice posting to output tax ledgers
- **No** GSTR-1/3B export or government portal integration

## Reports — ✅ COMPLETE

| Report | Service method |
|--------|----------------|
| General Ledger | `GetGeneralLedgerAsync` |
| Trial Balance | `GetTrialBalanceAsync` |
| Profit & Loss | `GetProfitAndLossAsync` |
| Balance Sheet | `GetBalanceSheetAsync` |
| Day Book | `GetDayBookAsync` |
| Cash Book | `GetCashBookAsync` |
| Bank Book | `GetBankBookAsync` |
| Receivables (AR) | `GetReceivablesAsync` |
| Payables (AP) | `GetPayablesAsync` |

AR report pulls from posted vouchers and invoice balances. **AP report is limited** — vendor/bill module not built (see gap analysis).

## Invoice integration — ✅ COMPLETE

When an invoice is sent/posted to accounting:

- Sales credit, AR debit, GST output lines
- Linked via `Invoice.AccountingVoucherId`
- Re-posting prevented by source idempotency key

Payment receipts can post RV vouchers (via finance/accounting integration in services).

## Inventory masters — ⚠️ PARTIAL

Entities: `InventoryItem`, `InventoryItemCategory`, `UnitOfMeasure`.  
Schema exists; **no** stock transactions, GRN, or COGS posting workflow.

## Permissions

Key permissions: `accounting.*`, `company.settings.*`, `gst.settings.*` — see [05-AUTHENTICATION-AUTHORIZATION.md](05-AUTHENTICATION-AUTHORIZATION.md).

## Audit coverage — ⚠️ PARTIAL

Audited: company profile, GST settings, FY open/close, period lock, ledger create/update, voucher post/reverse.

**Not audited:** most voucher draft edits, report exports, settings reads.

## Known gaps

See [ACCOUNTING-GAP-ANALYSIS.md](ACCOUNTING-GAP-ANALYSIS.md) for prioritized list (AP, bank reconciliation, migrations, API, etc.).

## Testing

`tests/ShaunERP.Tests/AccountingTests.cs` — posting balance, FY rules (not exhaustive).
