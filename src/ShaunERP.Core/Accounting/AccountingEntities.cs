namespace ShaunERP.Core.Accounting;

public enum AccountNature
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Income = 3,
    Expense = 4
}

public enum BalanceSide
{
    Debit = 0,
    Credit = 1
}

public enum VoucherStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2,
    Reversed = 3
}

public enum GstTaxType
{
    CGST = 0,
    SGST = 1,
    IGST = 2,
    UTGST = 3,
    Cess = 4,
    Other = 5
}

public enum GstRegistrationType
{
    Regular = 0,
    Composition = 1,
    Unregistered = 2,
    Casual = 3,
    SEZ = 4
}

public enum InventoryValuationMethod
{
    WeightedAverage = 0,
    Fifo = 1,
    StandardCost = 2
}

public enum NumberingMethod
{
    Sequential = 0,
    Manual = 1
}

/// <summary>Well-known system ledger codes used by posting integrations.</summary>
public static class SystemLedgerCodes
{
    public const string Cash = "CASH";
    public const string BankDefault = "BANK";
    public const string AccountsReceivable = "AR";
    public const string AccountsPayable = "AP";
    public const string Sales = "SALES";
    public const string Purchase = "PURCHASE";
    public const string CgstInput = "CGST-IN";
    public const string SgstInput = "SGST-IN";
    public const string IgstInput = "IGST-IN";
    public const string CgstOutput = "CGST-OUT";
    public const string SgstOutput = "SGST-OUT";
    public const string IgstOutput = "IGST-OUT";
    public const string RoundOff = "ROUNDOFF";
    public const string DiscountAllowed = "DISC-ALLOW";
    public const string DiscountReceived = "DISC-RECV";
    public const string OpeningBalanceAdj = "OB-ADJ";
    public const string RetainedEarnings = "RETAINED";
    public const string Capital = "CAPITAL";
    public const string Inventory = "INVENTORY";
    public const string Cogs = "COGS";
}

public static class SystemGroupCodes
{
    public const string Assets = "ASSETS";
    public const string CurrentAssets = "CA";
    public const string FixedAssets = "FA";
    public const string BankAccounts = "BANK";
    public const string CashInHand = "CASH";
    public const string AccountsReceivable = "AR";
    public const string Inventory = "INV";
    public const string Deposits = "DEPOSITS";
    public const string LoansAdvances = "LOANS-ADV";
    public const string Liabilities = "LIAB";
    public const string CurrentLiabilities = "CL";
    public const string AccountsPayable = "AP";
    public const string DutiesTaxes = "DUTIES";
    public const string Loans = "LOANS";
    public const string Provisions = "PROV";
    public const string Equity = "EQUITY";
    public const string Capital = "CAPITAL";
    public const string Reserves = "RESERVES";
    public const string RetainedEarnings = "RETAINED";
    public const string Income = "INCOME";
    public const string Sales = "SALES";
    public const string ServiceIncome = "SERVICE";
    public const string OtherIncome = "OTHER-INC";
    public const string DirectIncome = "DIR-INC";
    public const string IndirectIncome = "IND-INC";
    public const string Expenses = "EXP";
    public const string Purchase = "PURCHASE";
    public const string DirectExpenses = "DIR-EXP";
    public const string AdminExpenses = "ADMIN-EXP";
    public const string SellingExpenses = "SELL-EXP";
    public const string FinancialExpenses = "FIN-EXP";
    public const string OtherExpenses = "OTHER-EXP";
}

public static class SystemVoucherTypeCodes
{
    public const string Journal = "JV";
    public const string Receipt = "RV";
    public const string Payment = "PV";
    public const string Contra = "CV";
    public const string Sales = "SV";
    public const string Purchase = "PUV";
    public const string CreditNote = "CN";
    public const string DebitNote = "DN";
    public const string OpeningBalance = "OB";
    public const string Adjustment = "ADJ";
}

public static class AccountingSourceModules
{
    public const string Manual = "Accounting";
    public const string Sales = "Sales";
    public const string Purchase = "Purchase";
    public const string Inventory = "Inventory";
    public const string Opening = "Opening";
}

/// <summary>Organisation profile for a tenant company (legal/trading identity).</summary>
public class CompanyProfile : BaseEntity
{
    public int CompanyId { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string? LogoUrl { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; } = "India";
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PrimaryGstin { get; set; }
    public string? PrimaryPan { get; set; }
}

public class CompanyStatutoryRegistration : BaseEntity
{
    public int CompanyId { get; set; }
    public string RegistrationType { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime? RegistrationDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CompanyGstSettings : BaseEntity
{
    public int CompanyId { get; set; }
    public bool GstRegistered { get; set; }
    public string? Gstin { get; set; }
    public GstRegistrationType RegistrationType { get; set; } = GstRegistrationType.Unregistered;
    public string? State { get; set; }
    public string? StateCode { get; set; }
    public bool CompositionScheme { get; set; }
    public bool ReverseChargeApplicable { get; set; }
    public string? DefaultTaxBehaviour { get; set; } = "Exclusive";
    public DateTime? GstEffectiveDate { get; set; }
}

public class GstTaxRate : BaseEntity
{
    public int CompanyId { get; set; }
    public string TaxName { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public GstTaxType TaxType { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CurrencyMaster : BaseEntity
{
    public int CompanyId { get; set; }
    public string CurrencyCode { get; set; } = "INR";
    public string CurrencyName { get; set; } = "Indian Rupee";
    public string Symbol { get; set; } = "₹";
    public int DecimalPlaces { get; set; } = 2;
    public string DecimalSeparator { get; set; } = ".";
    public string ThousandSeparator { get; set; } = ",";
    public bool IsBaseCurrency { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ExchangeRate : BaseEntity
{
    public int CompanyId { get; set; }
    public int CurrencyId { get; set; }
    public CurrencyMaster? Currency { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow.Date;
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

public class FinancialYear : BaseEntity
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? ClosedBy { get; set; }
}

public class AccountingPeriodLock : BaseEntity
{
    public int CompanyId { get; set; }
    public DateTime LockedThroughDate { get; set; }
    public string? Reason { get; set; }
    public string? LockedBy { get; set; }
    public DateTime LockedAt { get; set; } = DateTime.UtcNow;
}

public class AccountingSettings : BaseEntity
{
    public int CompanyId { get; set; }
    public bool InventoryEnabled { get; set; }
    public InventoryValuationMethod ValuationMethod { get; set; } = InventoryValuationMethod.WeightedAverage;
    public bool NegativeStockAllowed { get; set; }
    public bool StockAccountingEnabled { get; set; }
    public bool AutoCreateCustomerLedger { get; set; } = true;
    public bool AutoCreateSupplierLedger { get; set; } = true;
    public bool AutoPostSalesInvoice { get; set; } = true;
    public string? DefaultCashLedgerCode { get; set; } = SystemLedgerCodes.Cash;
    public string? DefaultBankLedgerCode { get; set; } = SystemLedgerCodes.BankDefault;
    public string? DefaultSalesLedgerCode { get; set; } = SystemLedgerCodes.Sales;
    public string? DefaultArLedgerCode { get; set; } = SystemLedgerCodes.AccountsReceivable;
    public string? DefaultApLedgerCode { get; set; } = SystemLedgerCodes.AccountsPayable;
}

public class AccountGroup : BaseEntity
{
    public int CompanyId { get; set; }
    public int? ParentGroupId { get; set; }
    public AccountGroup? ParentGroup { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public AccountNature Nature { get; set; }
    public string? ReportClassification { get; set; }
    public bool IsSystemGroup { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<AccountGroup> Children { get; set; } = new List<AccountGroup>();
    public ICollection<Ledger> Ledgers { get; set; } = new List<Ledger>();
}

public class Ledger : BaseEntity
{
    public int CompanyId { get; set; }
    public string LedgerCode { get; set; } = string.Empty;
    public string LedgerName { get; set; } = string.Empty;
    public int AccountGroupId { get; set; }
    public AccountGroup? AccountGroup { get; set; }
    public AccountNature AccountType { get; set; }
    public decimal OpeningBalance { get; set; }
    public BalanceSide OpeningBalanceType { get; set; } = BalanceSide.Debit;
    public int? CurrencyId { get; set; }
    public CurrencyMaster? Currency { get; set; }
    public bool GstApplicable { get; set; }
    public string? Gstin { get; set; }
    public string? Pan { get; set; }
    public decimal? CreditLimit { get; set; }
    public int? CreditDays { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfsc { get; set; }
    public string? BankName { get; set; }
    public bool IsCashLedger { get; set; }
    public bool IsBankLedger { get; set; }
    public bool IsSystemLedger { get; set; }
    public bool IsActive { get; set; } = true;
    public string? SystemCode { get; set; }
}

public class VoucherType : BaseEntity
{
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public NumberingMethod NumberingMethod { get; set; } = NumberingMethod.Sequential;
    public int CurrentSequence { get; set; } = 1;
    public bool AllowManualNumber { get; set; }
    public bool IsSystemVoucher { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AccountingVoucher : BaseEntity
{
    public int CompanyId { get; set; }
    public int FinancialYearId { get; set; }
    public FinancialYear? FinancialYear { get; set; }
    public int VoucherTypeId { get; set; }
    public VoucherType? VoucherType { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow.Date;
    public string? ReferenceNumber { get; set; }
    public DateTime? ReferenceDate { get; set; }
    public string? Narration { get; set; }
    public VoucherStatus VoucherStatus { get; set; } = VoucherStatus.Draft;
    public string? SourceModule { get; set; }
    public string? SourceEntityType { get; set; }
    public string? SourceEntityId { get; set; }
    public string? PostingType { get; set; }
    public int? CurrencyId { get; set; }
    public CurrencyMaster? Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? ReversedBy { get; set; }
    public DateTime? ReversedAt { get; set; }
    public int? ReversalOfVoucherId { get; set; }
    public AccountingVoucher? ReversalOfVoucher { get; set; }
    public int? ReversedByVoucherId { get; set; }
    public AccountingVoucher? ReversedByVoucher { get; set; }

    public ICollection<AccountingVoucherLine> Lines { get; set; } = new List<AccountingVoucherLine>();
}

public class AccountingVoucherLine : BaseEntity
{
    public int CompanyId { get; set; }
    public int VoucherId { get; set; }
    public AccountingVoucher? Voucher { get; set; }
    public int LedgerId { get; set; }
    public Ledger? Ledger { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
    public int? CostCenterId { get; set; }
    public int? ProjectId { get; set; }
    public int? TaxId { get; set; }
    public GstTaxRate? Tax { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Optional allocation of receipt/payment lines against invoices (AR/AP foundation).</summary>
public class AccountingAllocation : BaseEntity
{
    public int CompanyId { get; set; }
    public int VoucherLineId { get; set; }
    public AccountingVoucherLine? VoucherLine { get; set; }
    public string DocumentType { get; set; } = "Invoice";
    public int DocumentId { get; set; }
    public decimal AllocatedAmount { get; set; }
    public DateTime AllocationDate { get; set; } = DateTime.UtcNow.Date;
}

public class InventoryItemCategory : BaseEntity
{
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class UnitOfMeasure : BaseEntity
{
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class InventoryItem : BaseEntity
{
    public int CompanyId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemType { get; set; } = "Goods";
    public bool IsInventoryItem { get; set; } = true;
    public int? CategoryId { get; set; }
    public InventoryItemCategory? Category { get; set; }
    public int? UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }
    public int? SalesLedgerId { get; set; }
    public Ledger? SalesLedger { get; set; }
    public int? PurchaseLedgerId { get; set; }
    public Ledger? PurchaseLedger { get; set; }
    public int? InventoryLedgerId { get; set; }
    public Ledger? InventoryLedger { get; set; }
    public int? CogsLedgerId { get; set; }
    public Ledger? CogsLedger { get; set; }
    public int? TaxCategoryId { get; set; }
    public string? HsnCode { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Request DTO for the reusable posting engine.</summary>
public sealed class AccountingPostingRequest
{
    public int? FinancialYearId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow.Date;
    public string VoucherTypeCode { get; set; } = SystemVoucherTypeCodes.Journal;
    public string? VoucherNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? ReferenceDate { get; set; }
    public string? Narration { get; set; }
    public string SourceModule { get; set; } = AccountingSourceModules.Manual;
    public string? SourceEntityType { get; set; }
    public string? SourceEntityId { get; set; }
    public string? PostingType { get; set; } = "Primary";
    public int? CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public bool PostImmediately { get; set; } = true;
    public List<AccountingPostingLineRequest> Lines { get; set; } = new();
}

public sealed class AccountingPostingLineRequest
{
    public int? LedgerId { get; set; }
    public string? LedgerSystemCode { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
    public int? CostCenterId { get; set; }
    public int? ProjectId { get; set; }
    public int? TaxId { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
}
