using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;

namespace ShaunERP.Core.Sales;

public enum SalesOrderStatus
{
    Draft = 0,
    Confirmed = 1,
    Fulfilled = 2,
    PartiallyInvoiced = 3,
    Invoiced = 4,
    Cancelled = 5
}

/// <summary>Sales order header (Dynamics / Zoho / NetSuite style order-to-cash).</summary>
public class SalesOrder : BaseEntity
{
    public int? CompanyId { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerName { get; set; }

    /// <summary>Optional source quote.</summary>
    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? RequestedDeliveryDate { get; set; }
    public SalesOrderStatus OrderStatus { get; set; } = SalesOrderStatus.Draft;

    public string? CurrencyCode { get; set; } = "USD";
    public string? PaymentTerms { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string? BillToAddress { get; set; }
    public string? ShipToAddress { get; set; }
    public string? Salesperson { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }

    public decimal Subtotal { get; set; }
    /// <summary>Document-level discount amount (absolute).</summary>
    public decimal Discount { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Amount already covered by linked invoices.</summary>
    public decimal AmountInvoiced { get; set; }

    public decimal BalanceToInvoice => Math.Max(0, GrandTotal - AmountInvoiced);

    public string DisplayStatus => OrderStatus switch
    {
        SalesOrderStatus.Draft => "Draft",
        SalesOrderStatus.Confirmed => "Confirmed",
        SalesOrderStatus.Fulfilled => "Fulfilled",
        SalesOrderStatus.PartiallyInvoiced => "Partially invoiced",
        SalesOrderStatus.Invoiced => "Invoiced",
        SalesOrderStatus.Cancelled => "Cancelled",
        _ => OrderStatus.ToString()
    };

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public class SalesOrderLine : BaseEntity
{
    public int? CompanyId { get; set; }
    public int SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }

    public int? ProductId { get; set; }
    public CrmProduct? Product { get; set; }
    public string? ProductName { get; set; }
    public string? Description { get; set; }

    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    /// <summary>Line discount percent (0–100).</summary>
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}
