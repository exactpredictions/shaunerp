using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services;

internal static class AccountingSchema
{
    public static async Task EnsureAsync(ShaunErpDbContext db, ILogger logger, CancellationToken ct)
    {
        // Additive column on existing invoices.
        await Exec(db, logger, """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "AccountingVoucherId" integer NULL;""", ct);

        foreach (var sql in TableSql)
        {
            await Exec(db, logger, sql, ct);
        }

        foreach (var sql in IndexSql)
        {
            await Exec(db, logger, sql, ct);
        }
    }

    private static async Task Exec(ShaunErpDbContext db, ILogger logger, string sql, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Accounting schema statement skipped");
        }
    }

    private static readonly string[] TableSql =
    [
        """
        CREATE TABLE IF NOT EXISTS "CompanyProfiles" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "LegalName" text NOT NULL DEFAULT '',
            "TradingName" text NULL, "LogoUrl" text NULL,
            "AddressLine1" text NULL, "AddressLine2" text NULL,
            "City" text NULL, "District" text NULL, "State" text NULL,
            "Country" text NULL, "PostalCode" text NULL,
            "Phone" text NULL, "Mobile" text NULL, "Email" text NULL, "Website" text NULL,
            "PrimaryGstin" text NULL, "PrimaryPan" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "CompanyStatutoryRegistrations" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "RegistrationType" text NOT NULL DEFAULT '',
            "RegistrationNumber" text NOT NULL DEFAULT '',
            "RegistrationDate" timestamptz NULL, "ExpiryDate" timestamptz NULL,
            "IssuingAuthority" text NULL, "State" text NULL, "Notes" text NULL,
            "IsActive" boolean NOT NULL DEFAULT true
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "CompanyGstSettings" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "GstRegistered" boolean NOT NULL DEFAULT false,
            "Gstin" text NULL,
            "RegistrationType" integer NOT NULL DEFAULT 2,
            "State" text NULL, "StateCode" text NULL,
            "CompositionScheme" boolean NOT NULL DEFAULT false,
            "ReverseChargeApplicable" boolean NOT NULL DEFAULT false,
            "DefaultTaxBehaviour" text NULL,
            "GstEffectiveDate" timestamptz NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "GstTaxRates" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "TaxName" text NOT NULL DEFAULT '',
            "Percentage" numeric(18,2) NOT NULL DEFAULT 0,
            "TaxType" integer NOT NULL DEFAULT 0,
            "EffectiveFrom" timestamptz NULL, "EffectiveTo" timestamptz NULL,
            "IsActive" boolean NOT NULL DEFAULT true
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "Currencies" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "CurrencyCode" text NOT NULL DEFAULT 'INR',
            "CurrencyName" text NOT NULL DEFAULT '',
            "Symbol" text NOT NULL DEFAULT '',
            "DecimalPlaces" integer NOT NULL DEFAULT 2,
            "DecimalSeparator" text NOT NULL DEFAULT '.',
            "ThousandSeparator" text NOT NULL DEFAULT ',',
            "IsBaseCurrency" boolean NOT NULL DEFAULT false,
            "IsActive" boolean NOT NULL DEFAULT true
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ExchangeRates" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "CurrencyId" integer NOT NULL,
            "Rate" numeric(18,6) NOT NULL DEFAULT 1,
            "EffectiveDate" timestamptz NOT NULL DEFAULT now(),
            "Source" text NULL, "Notes" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "FinancialYears" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Name" text NOT NULL DEFAULT '',
            "StartDate" timestamptz NOT NULL,
            "EndDate" timestamptz NOT NULL,
            "IsCurrent" boolean NOT NULL DEFAULT false,
            "IsClosed" boolean NOT NULL DEFAULT false,
            "ClosedDate" timestamptz NULL,
            "ClosedBy" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "AccountingPeriodLocks" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "LockedThroughDate" timestamptz NOT NULL,
            "Reason" text NULL,
            "LockedBy" text NULL,
            "LockedAt" timestamptz NOT NULL DEFAULT now()
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "AccountingSettings" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "InventoryEnabled" boolean NOT NULL DEFAULT false,
            "ValuationMethod" integer NOT NULL DEFAULT 0,
            "NegativeStockAllowed" boolean NOT NULL DEFAULT false,
            "StockAccountingEnabled" boolean NOT NULL DEFAULT false,
            "AutoCreateCustomerLedger" boolean NOT NULL DEFAULT true,
            "AutoCreateSupplierLedger" boolean NOT NULL DEFAULT true,
            "AutoPostSalesInvoice" boolean NOT NULL DEFAULT true,
            "DefaultCashLedgerCode" text NULL,
            "DefaultBankLedgerCode" text NULL,
            "DefaultSalesLedgerCode" text NULL,
            "DefaultArLedgerCode" text NULL,
            "DefaultApLedgerCode" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "AccountGroups" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ParentGroupId" integer NULL,
            "GroupCode" text NOT NULL DEFAULT '',
            "GroupName" text NOT NULL DEFAULT '',
            "Nature" integer NOT NULL DEFAULT 0,
            "ReportClassification" text NULL,
            "IsSystemGroup" boolean NOT NULL DEFAULT false,
            "IsActive" boolean NOT NULL DEFAULT true,
            "SortOrder" integer NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "Ledgers" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "LedgerCode" text NOT NULL DEFAULT '',
            "LedgerName" text NOT NULL DEFAULT '',
            "AccountGroupId" integer NOT NULL,
            "AccountType" integer NOT NULL DEFAULT 0,
            "OpeningBalance" numeric(18,2) NOT NULL DEFAULT 0,
            "OpeningBalanceType" integer NOT NULL DEFAULT 0,
            "CurrencyId" integer NULL,
            "GstApplicable" boolean NOT NULL DEFAULT false,
            "Gstin" text NULL, "Pan" text NULL,
            "CreditLimit" numeric(18,2) NULL,
            "CreditDays" integer NULL,
            "CustomerId" integer NULL, "SupplierId" integer NULL,
            "BankAccountNumber" text NULL, "BankIfsc" text NULL, "BankName" text NULL,
            "IsCashLedger" boolean NOT NULL DEFAULT false,
            "IsBankLedger" boolean NOT NULL DEFAULT false,
            "IsSystemLedger" boolean NOT NULL DEFAULT false,
            "IsActive" boolean NOT NULL DEFAULT true,
            "SystemCode" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "VoucherTypes" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Code" text NOT NULL DEFAULT '',
            "Name" text NOT NULL DEFAULT '',
            "Prefix" text NOT NULL DEFAULT '',
            "NumberingMethod" integer NOT NULL DEFAULT 0,
            "CurrentSequence" integer NOT NULL DEFAULT 0,
            "AllowManualNumber" boolean NOT NULL DEFAULT false,
            "IsSystemVoucher" boolean NOT NULL DEFAULT false,
            "IsActive" boolean NOT NULL DEFAULT true
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "AccountingVouchers" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "FinancialYearId" integer NOT NULL,
            "VoucherTypeId" integer NOT NULL,
            "VoucherNumber" text NOT NULL DEFAULT '',
            "VoucherDate" timestamptz NOT NULL DEFAULT now(),
            "ReferenceNumber" text NULL,
            "ReferenceDate" timestamptz NULL,
            "Narration" text NULL,
            "VoucherStatus" integer NOT NULL DEFAULT 0,
            "SourceModule" text NULL,
            "SourceEntityType" text NULL,
            "SourceEntityId" text NULL,
            "PostingType" text NULL,
            "CurrencyId" integer NULL,
            "ExchangeRate" numeric(18,6) NOT NULL DEFAULT 1,
            "TotalDebit" numeric(18,2) NOT NULL DEFAULT 0,
            "TotalCredit" numeric(18,2) NOT NULL DEFAULT 0,
            "PostedBy" text NULL, "PostedAt" timestamptz NULL,
            "ReversedBy" text NULL, "ReversedAt" timestamptz NULL,
            "ReversalOfVoucherId" integer NULL,
            "ReversedByVoucherId" integer NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "AccountingVoucherLines" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "VoucherId" integer NOT NULL,
            "LedgerId" integer NOT NULL,
            "DebitAmount" numeric(18,2) NOT NULL DEFAULT 0,
            "CreditAmount" numeric(18,2) NOT NULL DEFAULT 0,
            "Description" text NULL,
            "CostCenterId" integer NULL, "ProjectId" integer NULL, "TaxId" integer NULL,
            "CustomerId" integer NULL, "SupplierId" integer NULL,
            "DueDate" timestamptz NULL,
            "ReferenceType" text NULL, "ReferenceId" text NULL,
            "SortOrder" integer NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "AccountingAllocations" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "VoucherLineId" integer NOT NULL,
            "DocumentType" text NOT NULL DEFAULT 'Invoice',
            "DocumentId" integer NOT NULL,
            "AllocatedAmount" numeric(18,2) NOT NULL DEFAULT 0,
            "AllocationDate" timestamptz NOT NULL DEFAULT now()
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "InventoryItemCategories" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Code" text NOT NULL DEFAULT '',
            "Name" text NOT NULL DEFAULT '',
            "IsActive" boolean NOT NULL DEFAULT true
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "UnitsOfMeasure" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Code" text NOT NULL DEFAULT '',
            "Name" text NOT NULL DEFAULT '',
            "IsActive" boolean NOT NULL DEFAULT true
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "InventoryItems" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ItemCode" text NOT NULL DEFAULT '',
            "ItemName" text NOT NULL DEFAULT '',
            "ItemType" text NULL,
            "IsInventoryItem" boolean NOT NULL DEFAULT true,
            "CategoryId" integer NULL, "UnitId" integer NULL,
            "SalesLedgerId" integer NULL, "PurchaseLedgerId" integer NULL,
            "InventoryLedgerId" integer NULL, "CogsLedgerId" integer NULL,
            "TaxCategoryId" integer NULL, "HsnCode" text NULL,
            "IsActive" boolean NOT NULL DEFAULT true
        );
        """
    ];

    private static readonly string[] IndexSql =
    [
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CompanyProfiles_CompanyId" ON "CompanyProfiles" ("CompanyId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CompanyGstSettings_CompanyId" ON "CompanyGstSettings" ("CompanyId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccountingSettings_CompanyId" ON "AccountingSettings" ("CompanyId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Currencies_CompanyId_CurrencyCode" ON "Currencies" ("CompanyId", "CurrencyCode");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_FinancialYears_CompanyId_Name" ON "FinancialYears" ("CompanyId", "Name");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccountGroups_CompanyId_GroupCode" ON "AccountGroups" ("CompanyId", "GroupCode");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Ledgers_CompanyId_LedgerCode" ON "Ledgers" ("CompanyId", "LedgerCode");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_VoucherTypes_CompanyId_Code" ON "VoucherTypes" ("CompanyId", "Code");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccountingVouchers_CompanyId_VoucherNumber" ON "AccountingVouchers" ("CompanyId", "VoucherNumber");""",
        """CREATE INDEX IF NOT EXISTS "IX_AccountingVouchers_CompanyId_VoucherDate" ON "AccountingVouchers" ("CompanyId", "VoucherDate");""",
        """CREATE INDEX IF NOT EXISTS "IX_AccountingVoucherLines_CompanyId_LedgerId" ON "AccountingVoucherLines" ("CompanyId", "LedgerId");"""
    ];
}
