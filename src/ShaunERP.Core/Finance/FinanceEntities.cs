namespace ShaunERP.Core.Finance;

public class CompanyMaster : BaseEntity
{
    public int? TenantCompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string Currency { get; set; } = "USD";

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
}

public class Account : BaseEntity
{
    public int CompanyMasterId { get; set; }
    public CompanyMaster? CompanyMaster { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public AccountType Type { get; set; }
    public decimal Balance { get; set; }
}

public enum AccountType
{
    Asset = 0,
    Liability = 1,
    Revenue = 2,
    Expense = 3,
    Equity = 4
}

public class FinanceCustomer : BaseEntity
{
    public int CompanyMasterId { get; set; }
    public CompanyMaster? CompanyMaster { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class Invoice : BaseEntity
{
    public int CompanyMasterId { get; set; }
    public CompanyMaster? CompanyMaster { get; set; }
    public int FinanceCustomerId { get; set; }
    public FinanceCustomer? FinanceCustomer { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public string? Notes { get; set; }
}

public class Vendor : BaseEntity
{
    public int CompanyMasterId { get; set; }
    public CompanyMaster? CompanyMaster { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class Bill : BaseEntity
{
    public int CompanyMasterId { get; set; }
    public CompanyMaster? CompanyMaster { get; set; }
    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public DateTime BillDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
}
