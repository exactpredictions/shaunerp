using ShaunERP.Core.Crm;

namespace ShaunERP.Core.Finance;

/// <summary>Billing book / company legal entity settings for AR invoicing.</summary>
public class CompanyMaster : BaseEntity
{
    public int? TenantCompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? BillingStreet { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingZip { get; set; }
    public string? BillingCountry { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string Currency { get; set; } = "USD";
    public string InvoiceNumberPrefix { get; set; } = "INV-";
    public int NextInvoiceSequence { get; set; } = 1001;
    public string OrderNumberPrefix { get; set; } = "SO-";
    public int NextOrderSequence { get; set; } = 1001;
    public string? DefaultPaymentTerms { get; set; } = "Net 30";
    public int DefaultDueDays { get; set; } = 30;

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
}

/// <summary>GL account stub — full chart-of-accounts posting is out of MVP scope.</summary>
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

/// <summary>Legacy finance customer — prefer linking invoices to CRM <see cref="Customer"/>.</summary>
public class FinanceCustomer : BaseEntity
{
    public int CompanyMasterId { get; set; }
    public CompanyMaster? CompanyMaster { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public enum InvoiceStatus
{
    Draft = 0,
    Sent = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Void = 4
}

/// <summary>AR invoice header (QuickBooks / Zoho Books / Dynamics Invoice style).</summary>
public class Invoice : BaseEntity
{
    public int? CompanyId { get; set; }
    public int CompanyMasterId { get; set; }
    public CompanyMaster? CompanyMaster { get; set; }

    /// <summary>Preferred bill-to party — CRM customer.</summary>
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerName { get; set; }

    /// <summary>Optional source sales order.</summary>
    public int? SalesOrderId { get; set; }
    public ShaunERP.Core.Sales.SalesOrder? SalesOrder { get; set; }

    /// <summary>Posted accounting voucher for this invoice (idempotent sales posting).</summary>
    public int? AccountingVoucherId { get; set; }

    /// <summary>Optional legacy finance customer (nullable for CRM-first invoicing).</summary>
    public int? FinanceCustomerId { get; set; }
    public FinanceCustomer? FinanceCustomer { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public InvoiceStatus InvoiceStatus { get; set; } = InvoiceStatus.Draft;
    public string? CurrencyCode { get; set; } = "USD";
    public string? PaymentTerms { get; set; }
    public string? Notes { get; set; }

    /// <summary>Customer PO / reference number.</summary>
    public string? PurchaseOrderNumber { get; set; }

    /// <summary>Bill-to address snapshot captured when the customer is selected.</summary>
    public string? BillToAddress { get; set; }

    /// <summary>Salesperson / invoice owner (often from CRM customer owner).</summary>
    public string? Salesperson { get; set; }

    /// <summary>Payment / footer terms text (separate from internal notes).</summary>
    public string? Terms { get; set; }

    public decimal Subtotal { get; set; }
    /// <summary>Document-level discount amount (absolute), applied after line subtotals and before tax.</summary>
    public decimal Discount { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }

    /// <summary>Legacy single-amount field; kept in sync with <see cref="GrandTotal"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>Legacy paid flag; derived from <see cref="InvoiceStatus"/>.</summary>
    public bool IsPaid { get; set; }

    public decimal BalanceDue => Math.Max(0, GrandTotal - AmountPaid);

    /// <summary>True when sent/partial and past due with outstanding balance (not stored).</summary>
    public bool IsOverdue =>
        InvoiceStatus is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid
        && DueDate is not null
        && DueDate.Value.Date < DateTime.UtcNow.Date
        && BalanceDue > 0;

    public string DisplayStatus =>
        InvoiceStatus == InvoiceStatus.Void ? "Void"
        : InvoiceStatus == InvoiceStatus.Paid || IsPaid ? "Paid"
        : IsOverdue ? "Overdue"
        : InvoiceStatus == InvoiceStatus.PartiallyPaid ? "Partially paid"
        : InvoiceStatus == InvoiceStatus.Sent ? "Sent"
        : "Draft";

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();

    /// <summary>
    /// Tax is assessed on the amount remaining after the header discount.
    /// <paramref name="lineTax"/> is the sum of line tax computed on undiscounted nets;
    /// scale it by the remaining taxable share so GST/output tax matches taxable sales.
    /// </summary>
    public static decimal ScaleTaxForDocumentDiscount(
        decimal subtotal, decimal discount, decimal lineTax,
        MidpointRounding rounding = MidpointRounding.ToEven)
    {
        var after = Math.Max(0, subtotal - Math.Max(0, discount));
        if (subtotal <= 0m) return 0m;
        return Math.Round(lineTax * after / subtotal, 2, rounding);
    }
}

public class InvoiceLine : BaseEntity
{
    public int? CompanyId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int? ProductId { get; set; }
    public CrmProduct? Product { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    /// <summary>Line discount percent (0–100).</summary>
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Computed net before tax (not stored).</summary>
    public decimal LineNet => LineNetAmount(Quantity, UnitPrice, Discount);

    public static decimal LineNetAmount(decimal quantity, decimal unitPrice, decimal discountPercent)
    {
        var pct = Math.Clamp(discountPercent, 0m, 100m);
        return Math.Max(0, quantity * unitPrice * (1m - pct / 100m));
    }
}

public class InvoicePayment : BaseEntity
{
    public int? CompanyId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? Method { get; set; } = "Bank transfer";
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

/// <summary>AP vendor stub — accounts payable is out of MVP scope.</summary>
public class Vendor : BaseEntity
{
    public int CompanyMasterId { get; set; }
    public CompanyMaster? CompanyMaster { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

/// <summary>AP bill stub — vendor bills are out of MVP scope.</summary>
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
