# Database Dictionary

Table reference for PostgreSQL database `shaunerp`. All tables include standard `BaseEntity` columns unless noted.

## BaseEntity columns (common)

| Column | Type | Description |
|--------|------|-------------|
| Id | serial PK | Surrogate key |
| CreatedDate | timestamptz | UTC creation |
| UpdatedDate | timestamptz | UTC last update |
| CreatedBy | text | Optional audit |
| ModifiedBy | text | Optional audit |
| Status | text | Default `Active` |

---

## Identity & platform

### Companies

Tenant account.

| Column | Description |
|--------|-------------|
| Name | Company display name |
| Domain | Optional domain string |
| Description | Notes |

### Users

| Column | Description |
|--------|-------------|
| FirstName, LastName | |
| Email | Unique |
| PhoneNumber | |
| Username | Unique |
| PasswordHash | BCrypt |
| IsActive | |
| IsSystemAdmin | Platform super-user |

### CompanyUsers

| Column | Description |
|--------|-------------|
| UserId | FK → Users |
| CompanyId | FK → Companies |

### Roles

| Column | Description |
|--------|-------------|
| CompanyId | Null = global template role |
| Name | Admin, Manager, User |
| Description | |

### Permissions

| Column | Description |
|--------|-------------|
| Name | Unique permission key (e.g. `crm.leads.view`) |
| Description | |

### UserRoles

| Column | Description |
|--------|-------------|
| CompanyUserId | FK → CompanyUsers |
| RoleId | FK → Roles |

### RolePermissions

| Column | Description |
|--------|-------------|
| RoleId, PermissionId | M:N |

### CompanyModules

| Column | Description |
|--------|-------------|
| CompanyId | |
| ModuleKey | e.g. `crm`, `accounting` |
| ModuleName | Display |
| Subdomain | Optional |
| IsEnabled | |

### Sessions

| Column | Description |
|--------|-------------|
| UserId, CompanyId | |
| Token | JWT string |
| ExpiryDate | |
| IsActive | |

### UserCompanyPreferences

| Column | Description |
|--------|-------------|
| UserId, CompanyId | Unique pair |
| LastModuleKey | |
| FavouriteModules | Comma-separated |
| RecentModules | Comma-separated |
| SidebarCollapsed | boolean |

### AuditLogs

| Column | Description |
|--------|-------------|
| CompanyId, UserId | Nullable |
| Action | e.g. `auth.login` |
| EntityType, EntityId | |
| BeforeValues, AfterValues | JSON/text |
| CorrelationId | |
| IpAddress, UserAgent | |
| OccurredAt | |

---

## CRM

### Leads

Key columns: CompanyId, LeadOwner, FirstName, LastName, CompanyName, Email, Phone, LeadStatus, LeadSource, Industry, Rating, IsQualified, CampaignId, Score, Territory, Tags, AnnualRevenue, …

### Prospects

CompanyId, CompanyName, FirstName, LastName, Email, LeadId, ProspectOwner, Stage, Source, AccountId, ExpectedCloseDate, Tags

### CrmAccounts

AccountOwner, AccountName, AccountNumber, AccountType, Industry, ParentAccountId, Rating, Billing*/Shipping* address fields, PaymentTerms, CreditLimit

### Contacts

ContactOwner, FirstName, LastName, Title, AccountName, Email, Phone, ProspectId, CustomerId, AccountId, IsPrimary, Mailing* fields, Tags

### Opportunities

DealOwner, DealName, AccountName, Stage, Amount, Probability, ExpectedRevenue, ClosingDate, LeadId, CustomerId, AccountId, ContactId, CampaignId, ForecastCategory, WinLossReason

### Customers

CompanyName, PrimaryContact, Email, BillingAddress, ShippingAddress, PaymentTerms, CreditLimit, TaxId, CustomerOwner, AccountNumber, AccountId, Industry, Tags

### Campaigns

CampaignOwner, Name, CampaignType, CampaignStatus, StartDate, EndDate, BudgetedCost, ExpectedRevenue

### CrmProducts

ProductCode (unique per company), Name, UnitPrice, CurrencyCode, TaxRate, IsActive, ProductFamily, Category

### Quotes / QuoteLines

QuoteNumber, QuoteStage, AccountId, CustomerId, OpportunityId, Subtotal, Tax, GrandTotal; lines: ProductId, Quantity, UnitPrice, LineTotal

### CrmCases

CaseNumber, Subject, CaseType, Priority, CaseStatus, AccountId, CustomerId, Resolution, ClosedDate

### CrmTasks

TaskOwner, Subject, DueDate, TaskStatus, Priority, RelatedEntityType, RelatedEntityId

### Meetings

Title, From, To, Host, Location, MeetingType, Agenda

### CrmCalls

CallOwner, Subject, CallStart, DurationMinutes, CallResult, ContactName

### CrmNotes

NoteOwner, Title, Body, IsPinned, RelatedEntityType, RelatedEntityId

---

## Finance (invoicing)

### CompanyMasters

TenantCompanyId, Name, LegalName, TaxId, Currency, InvoiceNumberPrefix, NextInvoiceSequence, OrderNumberPrefix, NextOrderSequence, DefaultPaymentTerms, DefaultDueDays, Billing* address

### Accounts 🗑️ DEPRECATED stub

CompanyMasterId, Name, AccountNumber, Type, Balance — legacy; use Accounting Ledgers

### FinanceCustomers 🗑️ DEPRECATED

CompanyMasterId, Name, Email, Phone

### Invoices

CompanyId, CompanyMasterId, CustomerId, CustomerName, SalesOrderId, AccountingVoucherId, InvoiceNumber, InvoiceDate, DueDate, InvoiceStatus (enum int), CurrencyCode, Subtotal, TaxTotal, GrandTotal, AmountPaid, BillToAddress, Salesperson, PurchaseOrderNumber, Amount (legacy), IsPaid (legacy)

### InvoiceLines

InvoiceId, ProductId, ProductName, Quantity, UnitPrice, Discount, TaxRate, TaxAmount, LineTotal, SortOrder

### InvoicePayments

InvoiceId, Amount, PaymentDate, Method, Reference, Notes

### Vendors 📋 PLANNED stub

CompanyMasterId, Name, Email, Phone

### Bills 📋 PLANNED stub

CompanyMasterId, VendorId, BillNumber, BillDate, DueDate, Amount, IsPaid

---

## Sales

### SalesOrders

CompanyId, CustomerId, QuoteId, OrderNumber, OrderDate, OrderStatus (enum), CurrencyCode, Subtotal, TaxTotal, GrandTotal, AmountInvoiced, BillToAddress, ShipToAddress, Salesperson

### SalesOrderLines

SalesOrderId, ProductId, ProductName, Quantity, UnitPrice, TaxRate, LineTotal, SortOrder

---

## Accounting

### CompanyProfiles

CompanyId (unique), legal/trade names, addresses, PAN, CIN, logo, base currency

### CompanyGstSettings

CompanyId (unique), GSTIN, registration type, default tax behaviour

### CompanyStatutoryRegistrations

Additional registration numbers per company

### GstTaxRates

CompanyId, TaxName, TaxType (enum), Rate, EffectiveFrom

### CurrencyMaster / ExchangeRates

CurrencyCode, symbol, decimal places; exchange rate history

### FinancialYears

CompanyId, Name, StartDate, EndDate, IsCurrent, IsClosed

### AccountingPeriodLocks

CompanyId, LockedUntilDate, Reason

### AccountingSettings

CompanyId (unique), numbering defaults, valuation method

### AccountGroups

CompanyId, GroupCode (unique), Name, ParentGroupId, Nature, BalanceSide

### Ledgers

CompanyId, LedgerCode (unique), Name, AccountGroupId, SystemCode, OpeningBalance, BalanceSide, IsSystemLedger, IsActive

### VoucherTypes

CompanyId, Code (unique), Name, Prefix, NumberingMethod

### AccountingVouchers

CompanyId, VoucherNumber (unique), VoucherTypeId, VoucherDate, FinancialYearId, VoucherStatus, Narration, SourceModule, SourceEntityType, SourceEntityId, PostingType, ReversalOfVoucherId

### AccountingVoucherLines

VoucherId, LedgerId, DebitAmount, CreditAmount, Narration, CostCenter (if used)

### AccountingAllocations

Bill-wise / reference allocations on voucher lines (AR/AP tracking)

### InventoryItemCategories / UnitsOfMeasure / InventoryItems 📋 PARTIAL

ItemCode, Name, CategoryId, UOM, standard cost — no stock ledger

---

## HRMS 📋 PLANNED

### Departments

Name, CompanyId

### Employees

DepartmentId, Name, Email, HireDate, …

### LeaveRequests

EmployeeId, FromDate, ToDate, Status

---

## Projects 📋 PLANNED

### Projects

Name, CompanyId, Status, StartDate, EndDate

### Issues

ProjectId, Title, Status, Assignee

---

## Index quick reference

See [03-DATABASE-DOCUMENTATION.md](03-DATABASE-DOCUMENTATION.md) for index list.

Entity source files: `src/ShaunERP.Core/**/*.cs`, `ShaunErpDbContext.cs`.
