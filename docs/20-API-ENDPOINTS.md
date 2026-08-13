# API Endpoints

Base URL (production): `https://erp.shauninfomedia.com`

Authentication: `Authorization: Bearer <JWT>` unless marked **Public**.

Swagger UI: `/swagger` (disable in production with `DISABLE_SWAGGER=true`).

## Public / infrastructure

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health` | Public | Health check JSON |
| GET | `/` | Public | API landing HTML |
| GET | `/swagger` | Public | Swagger UI (if enabled) |

## Auth — `/api/auth`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/register` | Public | Register user (+ optional company) |
| POST | `/api/auth/login` | Public | Login; returns JWT |
| GET | `/api/auth/me` | Bearer | Current user, company, permissions |

## Companies — `/api/companies`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/companies` | Bearer | List all companies (limited tenant filter) |

## Users — `/api/users`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/users` | Bearer | List users (tenant scoped) |
| GET | `/api/users/{id}` | Bearer | Get user |
| POST | `/api/users` | Bearer | Create user |
| PUT | `/api/users/{id}` | Bearer | Update user |
| POST | `/api/users/{id}/reset-password` | Bearer | Reset password |

## Roles — `/api/roles`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/roles?companyId=` | Bearer | List roles |

## CRM — `/api/crm`

All routes require Bearer token.

### Analytics

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/crm/dashboard` | Dashboard stats |
| GET | `/api/crm/pipeline` | Pipeline board |
| GET | `/api/crm/forecast` | Forecast stats |

### Leads

| GET | `/api/crm/leads` | List |
| GET | `/api/crm/leads/{id}` | Get |
| POST | `/api/crm/leads` | Create |
| PUT | `/api/crm/leads/{id}` | Update |
| DELETE | `/api/crm/leads/{id}` | Delete |
| POST | `/api/crm/leads/{id}/convert` | Convert to customer |
| POST | `/api/crm/leads/{id}/convert-prospect` | Convert to prospect |

### Prospects

| GET/POST | `/api/crm/prospects` | List / Create |
| GET/PUT/DELETE | `/api/crm/prospects/{id}` | CRUD |

### Accounts

| GET/POST | `/api/crm/accounts` | List / Create |
| GET/PUT/DELETE | `/api/crm/accounts/{id}` | CRUD |

### Contacts

| GET/POST | `/api/crm/contacts` | List / Create |
| GET/PUT/DELETE | `/api/crm/contacts/{id}` | CRUD |

### Opportunities

| GET/POST | `/api/crm/opportunities` | List / Create |
| GET/PUT/DELETE | `/api/crm/opportunities/{id}` | CRUD |
| POST | `/api/crm/opportunities/{id}/win` | Win → customer |
| POST | `/api/crm/opportunities/{id}/lose` | Lose |
| POST | `/api/crm/opportunities/{id}/stage` | Move stage |

### Customers

| GET/POST | `/api/crm/customers` | List / Create |
| GET/PUT/DELETE | `/api/crm/customers/{id}` | CRUD |

### Campaigns, Products, Quotes, Cases, Tasks, Meetings, Calls, Notes

Same CRUD pattern:

- `/api/crm/campaigns[/{id}]`
- `/api/crm/products[/{id}]`
- `/api/crm/quotes[/{id}]`
- `/api/crm/cases[/{id}]`
- `/api/crm/tasks[/{id}]`
- `/api/crm/meetings[/{id}]`
- `/api/crm/calls[/{id}]`
- `/api/crm/notes[/{id}]` — GET list supports `?relatedType=&relatedId=`

## Finance — `/api/finance`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/finance/dashboard` | AR dashboard |
| GET | `/api/finance/book` | Billing book |
| PUT | `/api/finance/book` | Update billing book |
| GET | `/api/finance/customers` | CRM customers for billing |
| GET | `/api/finance/products` | CRM products |
| GET | `/api/finance/invoices` | List invoices |
| GET | `/api/finance/invoices/{id}` | Get invoice |
| POST | `/api/finance/invoices` | Create |
| PUT | `/api/finance/invoices/{id}` | Update |
| DELETE | `/api/finance/invoices/{id}` | Delete |
| POST | `/api/finance/invoices/{id}/send` | Mark sent |
| POST | `/api/finance/invoices/{id}/void` | Void |
| POST | `/api/finance/invoices/{id}/mark-paid` | Mark fully paid |
| GET | `/api/finance/payments` | List payments |
| POST | `/api/finance/invoices/{id}/payments` | Record payment |
| DELETE | `/api/finance/payments/{id}` | Delete payment |

**Not implemented** (despite README mention): `/api/finance/vendors`, `/api/finance/bills`, `/api/finance/accounts`, `/api/finance/books`

## Sales Orders — ❌ NO REST API

Use Blazor Web UI only. Service: `ISalesOrderService`.

## Accounting — ❌ NO REST API

Use Blazor Web UI only. Services: `IAccountingService`, `IAccountingPostingService`, `IAccountingReportService`.

## Web-only endpoints (cookie auth)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/account/login` | Form login |
| POST | `/account/switch` | Switch company |
| GET | `/account/logout` | Logout |

## Error responses

- 401 Unauthorized — missing/invalid JWT or cookie
- 403 Forbidden — permission denied (`ForbiddenExceptionMiddleware` on API)
- 400 BadRequest — validation / business rule `{ error: "..." }`
- 404 NotFound

## CORS

API allows **any origin** (`AllowAll`) — restrict before production integration.

## Versioning

No URL versioning. Swagger doc: v1.
