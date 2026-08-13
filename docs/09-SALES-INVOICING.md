# Sales & Invoicing

Covers **Invoicing (Finance)** and **Sales Orders** modules.

## Invoicing (Finance) — ✅ COMPLETE

### Purpose

Accounts receivable: billing book, customer invoices, line items, payments, dashboard metrics.

### Billing book

Entity: `CompanyMaster` (one per tenant, linked via `TenantCompanyId`).

Settings: legal name, tax ID, currency, invoice prefix/sequence, payment terms, billing address.

Routes: `/finance/settings`, API `GET/PUT /api/finance/book`.

### Invoices

Entity: `Invoice` + `InvoiceLine` + `InvoicePayment`.

| Status | Value | Description |
|--------|-------|-------------|
| Draft | 0 | Editable |
| Sent | 1 | Issued to customer |
| PartiallyPaid | 2 | Part payment received |
| Paid | 3 | Settled |
| Void | 4 | Cancelled |

Features:

- CRM customer link (`CustomerId`) — preferred over legacy `FinanceCustomer`
- Product lines from `CrmProduct`
- Tax per line (`TaxRate`, `TaxAmount`)
- Document discount
- PO number, bill-to snapshot, salesperson
- Link to sales order (`SalesOrderId`)

### Payments

Recorded against invoices; updates `AmountPaid` and status.  
Routes: `/finance/payments`, API `POST /api/finance/invoices/{id}/payments`.

### Accounting bridge — ✅

Posted invoices create accounting vouchers (sales + AR + GST). See [07-ACCOUNTING-MODULE.md](07-ACCOUNTING-MODULE.md).

### Legacy / deprecated

| Item | Status |
|------|--------|
| `FinanceCustomer` | 🗑️ DEPRECATED — use CRM `Customer` |
| Finance `Account` GL stubs | 🗑️ DEPRECATED — use Accounting ledgers |
| `Vendor` / `Bill` entities | 📋 PLANNED — AP not implemented |

README historically listed `/api/finance/vendors` and `/api/finance/bills` — **endpoints do not exist** in current `FinanceController`.

## Sales Orders — ⚠️ PARTIAL

### Status

| Layer | Status |
|-------|--------|
| UI | ✅ COMPLETE |
| Service (`ISalesOrderService`) | ✅ COMPLETE |
| REST API | ❌ No controller |
| Accounting posting from order | ⚠️ Via invoice only |

### Order lifecycle

| Status | Description |
|--------|-------------|
| Draft | Editable |
| Confirmed | Committed |
| PartiallyFulfilled | Shipment partial |
| Fulfilled | Complete |
| Cancelled | Void |

### Features (UI)

- Create from customer / quote
- Line items with products, tax, discount
- Confirm, fulfill, cancel actions
- **Create invoice from order** — copies lines to new invoice
- Tracks `AmountInvoiced` vs `GrandTotal`

### Routes

`/orders`, `/orders/list`, `/orders/new`, `/orders/{id}`

### Permissions

`orders.view`, `orders.create`, `orders.confirm`, `orders.fulfill`, `orders.create_invoice`, etc.

## Order-to-cash flow (as implemented)

1. **CRM:** Lead → … → Customer
2. **Optional:** Quote accepted
3. **Sales Order:** Create, confirm, fulfill
4. **Invoice:** Create from order or standalone
5. **Payment:** Record on invoice
6. **Accounting:** Auto-post invoice (and payment voucher when configured)

Gaps: no shipment/inventory deduction; no credit note document type.

## Finance dashboard

Metrics: outstanding AR, overdue count, recent invoices — `IFinanceService.GetDashboardAsync`.

## API summary

Finance endpoints only (no sales order API). See [20-API-ENDPOINTS.md](20-API-ENDPOINTS.md).

## Demo data

Demo invoices prefixed `DEMO-INV-*` in seeder. Remove before production.
