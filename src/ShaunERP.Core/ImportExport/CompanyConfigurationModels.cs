namespace ShaunERP.Core.ImportExport;

/// <summary>
/// Full India company configuration DTO for import/export templates.
/// No secrets (passwords, JWT keys, connection strings) are included.
/// </summary>
public sealed class CompanyConfigurationDto
{
    public string SchemaVersion { get; set; } = "1.0";
    public string Locale { get; set; } = "en-IN";
    public string Country { get; set; } = "India";

    public CompanyIdentityConfig Identity { get; set; } = new();
    public CompanyFinancialConfig Financial { get; set; } = new();
    public CompanyGstConfig Gst { get; set; } = new();
    public CompanyBankingConfig Banking { get; set; } = new();
    public CompanyInvoiceConfig Invoice { get; set; } = new();
    public CompanyCrmLookupsConfig CrmLookups { get; set; } = new();
    public CompanyChartOfAccountsRecommendations ChartOfAccounts { get; set; } = new();
    public List<CompanyStatutoryConfig> StatutoryRegistrations { get; set; } = new();
    public List<CompanyCurrencyConfig> Currencies { get; set; } = new();
    public CompanyAccountingSettingsConfig AccountingSettings { get; set; } = new();
}

public sealed class CompanyIdentityConfig
{
    public string LegalName { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string? Domain { get; set; }
    public string? Description { get; set; }
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
    public string? Cin { get; set; }
    public string? LogoUrl { get; set; }
}

public sealed class CompanyFinancialConfig
{
    public string BaseCurrency { get; set; } = "INR";
    public string? FinancialYearLabel { get; set; }
    public DateTime? FinancialYearStart { get; set; }
    public DateTime? FinancialYearEnd { get; set; }
    public string? BooksName { get; set; }
    public string? BooksLegalName { get; set; }
    public string? TaxId { get; set; }
}

public sealed class CompanyGstConfig
{
    public bool GstRegistered { get; set; }
    public string? Gstin { get; set; }
    public string RegistrationType { get; set; } = "Unregistered";
    public string? State { get; set; }
    public string? StateCode { get; set; }
    public bool CompositionScheme { get; set; }
    public bool ReverseChargeApplicable { get; set; }
    public string? DefaultTaxBehaviour { get; set; } = "Exclusive";
    public DateTime? GstEffectiveDate { get; set; }
}

public sealed class CompanyBankingConfig
{
    public string? PrimaryBankName { get; set; }
    public string? PrimaryAccountNumber { get; set; }
    public string? PrimaryIfsc { get; set; }
    public string? PrimaryAccountHolder { get; set; }
    public string? DefaultCashLedgerCode { get; set; }
    public string? DefaultBankLedgerCode { get; set; }
}

public sealed class CompanyInvoiceConfig
{
    public string InvoiceNumberPrefix { get; set; } = "INV-";
    public int NextInvoiceSequence { get; set; } = 1001;
    public string OrderNumberPrefix { get; set; } = "SO-";
    public int NextOrderSequence { get; set; } = 1001;
    public string? DefaultPaymentTerms { get; set; } = "Net 30";
    public int DefaultDueDays { get; set; } = 30;
    public string? BillingStreet { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingZip { get; set; }
    public string? BillingCountry { get; set; } = "India";
    public string? BillingEmail { get; set; }
    public string? BillingPhone { get; set; }
}

public sealed class CompanyCrmLookupsConfig
{
    public List<string> LeadSources { get; set; } = new();
    public List<string> LeadStatuses { get; set; } = new();
    public List<string> Industries { get; set; } = new();
    public List<string> OpportunityStages { get; set; } = new();
    public List<string> CustomerTypes { get; set; } = new();
    public List<string> Territories { get; set; } = new();
    public List<string> PaymentTermsOptions { get; set; } = new();
}

public sealed class CompanyChartOfAccountsRecommendations
{
    public bool EnsureIndiaSystemCoa { get; set; } = true;
    public List<string> RecommendedLedgerCodes { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public sealed class CompanyStatutoryConfig
{
    public string RegistrationType { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime? RegistrationDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CompanyCurrencyConfig
{
    public string CurrencyCode { get; set; } = "INR";
    public string CurrencyName { get; set; } = "Indian Rupee";
    public string Symbol { get; set; } = "₹";
    public int DecimalPlaces { get; set; } = 2;
    public bool IsBaseCurrency { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CompanyAccountingSettingsConfig
{
    public bool InventoryEnabled { get; set; }
    public string ValuationMethod { get; set; } = "WeightedAverage";
    public bool NegativeStockAllowed { get; set; }
    public bool StockAccountingEnabled { get; set; }
    public bool AutoCreateCustomerLedger { get; set; } = true;
    public bool AutoCreateSupplierLedger { get; set; } = true;
    public bool AutoPostSalesInvoice { get; set; } = true;
    public string? DefaultCashLedgerCode { get; set; }
    public string? DefaultBankLedgerCode { get; set; }
    public string? DefaultSalesLedgerCode { get; set; }
    public string? DefaultArLedgerCode { get; set; }
    public string? DefaultApLedgerCode { get; set; }
}

public sealed class CompanyConfigValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class CompanyConfigDiffItem
{
    public string Path { get; init; } = string.Empty;
    public string Action { get; init; } = "Update";
    public string? Before { get; init; }
    public string? After { get; init; }
}

public sealed class CompanyConfigPreview
{
    public CompanyConfigurationDto Proposed { get; init; } = new();
    public CompanyConfigValidationResult Validation { get; init; } = new() { IsValid = false };
    public IReadOnlyList<CompanyConfigDiffItem> Diffs { get; init; } = Array.Empty<CompanyConfigDiffItem>();
    public bool CanApply => Validation.IsValid && Diffs.Count > 0;
}

public sealed class CompanyConfigApplyResult
{
    public bool Success { get; init; }
    public int FieldsUpdated { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    public string? CorrelationId { get; init; }
}

public sealed class DemoDataScanResult
{
    public int CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public int DemoTaggedRecords { get; init; }
    public int DemoPrefixedRecords { get; init; }
    public bool HasDemoAdmin { get; init; }
    public bool HasDemoCompany { get; init; }
    public IReadOnlyList<DemoDataCount> Counts { get; init; } = Array.Empty<DemoDataCount>();
}

public sealed class DemoDataCount
{
    public string EntityType { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class DemoDataPreview
{
    public DemoDataScanResult Scan { get; init; } = new();
    public IReadOnlyList<string> SampleKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class DemoDataRemovalResult
{
    public bool Success { get; init; }
    public int DeletedRecords { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}

public sealed class ProductionInitResult
{
    public bool Success { get; init; }
    public int? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}

public sealed class ProductionUserBootstrapRequest
{
    public string AdminEmail { get; set; } = "shauninfomediaopc@gmail.com";
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFirstName { get; set; } = "Shaun";
    public string AdminLastName { get; set; } = "Admin";
    public string AccountantEmail { get; set; } = "arokiabakkianathanl@gmail.com";
    public string AccountantPassword { get; set; } = string.Empty;
    public string AccountantFirstName { get; set; } = "Arokia";
    public string AccountantLastName { get; set; } = "Bakkianathan";
    public int? CompanyId { get; set; }
}

public sealed class ProductionUserBootstrapResult
{
    public bool Success { get; init; }
    public int? AdminUserId { get; init; }
    public int? AccountantUserId { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}
