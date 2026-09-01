using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services;

public interface ISalesOrderService
{
    Task<SalesOrderDashboardStats> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> ListCrmCustomersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CrmProduct>> ListProductsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> ListAcceptedQuotesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SalesOrder>> ListOrdersAsync(CancellationToken ct = default);
    Task<PagedResult<SalesOrder>> QueryOrdersAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<SalesOrder?> GetOrderAsync(int id, CancellationToken ct = default);
    Task<SalesOrder> CreateOrderAsync(SalesOrder order, CancellationToken ct = default);
    Task<SalesOrder?> UpdateOrderAsync(int id, SalesOrder update, CancellationToken ct = default);
    Task<bool> DeleteOrderAsync(int id, CancellationToken ct = default);

    Task<SalesOrder?> ConfirmAsync(int id, CancellationToken ct = default);
    Task<SalesOrder?> FulfillAsync(int id, CancellationToken ct = default);
    Task<SalesOrder?> CancelAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, Invoice? Invoice)> CreateInvoiceFromOrderAsync(int id, CancellationToken ct = default);
    Task<SalesOrder?> CreateFromQuoteAsync(int quoteId, CancellationToken ct = default);
}

public record SalesOrderDashboardStats(
    int Draft,
    int Confirmed,
    int Fulfilled,
    int PartiallyInvoiced,
    int Invoiced,
    int Cancelled,
    decimal OpenOrderValue,
    decimal InvoicedThisMonth,
    int OrderCount);

public sealed class SalesOrderService : ISalesOrderService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;
    private readonly IFinanceService _finance;

    public SalesOrderService(
        ShaunErpDbContext db,
        ITenantContext tenant,
        IModuleRegistry modules,
        IFinanceService finance)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
        _finance = finance;
    }

    public async Task<SalesOrderDashboardStats> GetDashboardAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersDashboardView);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var orders = await TenantOrders(_db.SalesOrders.AsNoTracking()).Take(2000).ToListAsync(ct);

        return new SalesOrderDashboardStats(
            orders.Count(o => o.OrderStatus == SalesOrderStatus.Draft),
            orders.Count(o => o.OrderStatus == SalesOrderStatus.Confirmed),
            orders.Count(o => o.OrderStatus == SalesOrderStatus.Fulfilled),
            orders.Count(o => o.OrderStatus == SalesOrderStatus.PartiallyInvoiced),
            orders.Count(o => o.OrderStatus == SalesOrderStatus.Invoiced),
            orders.Count(o => o.OrderStatus == SalesOrderStatus.Cancelled),
            orders.Where(o => o.OrderStatus is not (SalesOrderStatus.Cancelled or SalesOrderStatus.Invoiced))
                .Sum(o => o.BalanceToInvoice),
            orders.Where(o => o.OrderDate >= monthStart && o.OrderStatus != SalesOrderStatus.Cancelled)
                .Sum(o => o.AmountInvoiced > 0 ? o.AmountInvoiced : 0),
            orders.Count);
    }

    public async Task<IReadOnlyList<Customer>> ListCrmCustomersAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersAccess);
        var companyId = RequireCompanyId();
        return await _db.CrmCustomers.AsNoTracking()
            .Where(c => c.CompanyId == companyId && (c.Status == null || c.Status == "Active"))
            .OrderBy(c => c.CompanyName)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CrmProduct>> ListProductsAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersAccess);
        var companyId = RequireCompanyId();
        return await _db.CrmProducts.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .OrderBy(p => p.Name)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Quote>> ListAcceptedQuotesAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersAccess);
        var companyId = RequireCompanyId();
        return await _db.Quotes.AsNoTracking()
            .Include(q => q.Lines)
            .Where(q => q.CompanyId == companyId)
            .Where(q => q.QuoteStage == "Accepted" || q.QuoteStage == "accepted")
            .OrderByDescending(q => q.Id)
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SalesOrder>> ListOrdersAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersView);
        return await TenantOrders(_db.SalesOrders.AsNoTracking())
            .Include(o => o.Customer)
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<SalesOrder>> QueryOrdersAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersView);
        query ??= new ListQuery();
        IQueryable<SalesOrder> q = TenantOrders(_db.SalesOrders.AsNoTracking()).Include(o => o.Customer);
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(o =>
                EF.Functions.ILike(o.OrderNumber, pattern) ||
                (o.CustomerName != null && EF.Functions.ILike(o.CustomerName, pattern)) ||
                (o.PurchaseOrderNumber != null && EF.Functions.ILike(o.PurchaseOrderNumber, pattern)) ||
                (o.Salesperson != null && EF.Functions.ILike(o.Salesperson, pattern)) ||
                (o.Notes != null && EF.Functions.ILike(o.Notes, pattern)) ||
                (o.Customer != null && o.Customer.CompanyName != null && EF.Functions.ILike(o.Customer.CompanyName, pattern)));
        }

        return await q.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id).ToPagedResultAsync(query, ct);
    }

    public async Task<SalesOrder?> GetOrderAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersView);
        return await TenantOrders(_db.SalesOrders)
            .Include(o => o.Lines)
            .Include(o => o.Customer)
            .Include(o => o.Invoices)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<SalesOrder> CreateOrderAsync(SalesOrder order, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersCreate);
        var companyId = RequireCompanyId();
        var book = await _finance.GetOrCreateBillingBookAsync(ct);

        order.Id = 0;
        order.CompanyId = companyId;
        order.CreatedDate = DateTime.UtcNow;
        order.OrderDate = NormalizeUtc(order.OrderDate) ?? DateTime.UtcNow;
        order.RequestedDeliveryDate = NormalizeUtc(order.RequestedDeliveryDate);
        order.OrderStatus = SalesOrderStatus.Draft;
        order.CurrencyCode = string.IsNullOrWhiteSpace(order.CurrencyCode) ? book.Currency : order.CurrencyCode;
        if (string.IsNullOrWhiteSpace(order.PaymentTerms))
            order.PaymentTerms = book.DefaultPaymentTerms;
        TrimHeader(order);
        await ResolveCustomerAsync(order, ct);
        ValidateOrder(order);

        if (string.IsNullOrWhiteSpace(order.OrderNumber))
            order.OrderNumber = await NextOrderNumberAsync(book, ct);
        else
            order.OrderNumber = order.OrderNumber.Trim();

        foreach (var line in order.Lines)
        {
            line.Id = 0;
            line.CompanyId = companyId;
            line.CreatedDate = DateTime.UtcNow;
            ApplyLineTotals(line);
        }

        RecalcTotals(order);
        _db.SalesOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<SalesOrder?> UpdateOrderAsync(int id, SalesOrder update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersUpdate);
        var row = await TenantOrders(_db.SalesOrders)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return null;
        if (row.OrderStatus is SalesOrderStatus.Cancelled or SalesOrderStatus.Invoiced)
            throw new InvalidOperationException("Cancelled or fully invoiced orders cannot be edited.");

        row.CustomerId = update.CustomerId;
        row.QuoteId = update.QuoteId;
        row.PurchaseOrderNumber = NullIfBlank(update.PurchaseOrderNumber);
        row.BillToAddress = NullIfBlank(update.BillToAddress);
        row.ShipToAddress = NullIfBlank(update.ShipToAddress);
        row.Salesperson = NullIfBlank(update.Salesperson);
        row.Notes = NullIfBlank(update.Notes);
        row.Terms = NullIfBlank(update.Terms);
        row.CurrencyCode = update.CurrencyCode;
        row.PaymentTerms = update.PaymentTerms;
        row.Discount = Math.Max(0, update.Discount);
        row.OrderDate = NormalizeUtc(update.OrderDate) ?? row.OrderDate;
        row.RequestedDeliveryDate = NormalizeUtc(update.RequestedDeliveryDate);
        if (row.OrderStatus == SalesOrderStatus.Draft && !string.IsNullOrWhiteSpace(update.OrderNumber))
            row.OrderNumber = update.OrderNumber.Trim();

        await ResolveCustomerAsync(row, ct);

        _db.SalesOrderLines.RemoveRange(row.Lines);
        row.Lines.Clear();
        foreach (var line in update.Lines ?? [])
        {
            var newLine = new SalesOrderLine
            {
                CompanyId = row.CompanyId,
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Discount = line.Discount,
                TaxRate = line.TaxRate,
                SortOrder = line.SortOrder,
                CreatedDate = DateTime.UtcNow
            };
            ApplyLineTotals(newLine);
            row.Lines.Add(newLine);
        }

        ValidateOrder(row);
        RecalcTotals(row);
        await RefreshInvoicedAmountAsync(row, ct);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteOrderAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersDelete);
        var row = await TenantOrders(_db.SalesOrders)
            .Include(o => o.Lines)
            .Include(o => o.Invoices)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return false;
        if (row.Invoices.Count > 0 || row.OrderStatus is SalesOrderStatus.PartiallyInvoiced or SalesOrderStatus.Invoiced)
            throw new InvalidOperationException("Orders with invoices cannot be deleted. Cancel instead.");
        if (row.OrderStatus is not SalesOrderStatus.Draft and not SalesOrderStatus.Cancelled)
            throw new InvalidOperationException("Only draft or cancelled orders can be deleted.");

        _db.SalesOrders.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SalesOrder?> ConfirmAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersConfirm);
        var row = await TenantOrders(_db.SalesOrders).Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return null;
        if (row.OrderStatus == SalesOrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot confirm a cancelled order.");
        if (row.OrderStatus is SalesOrderStatus.Invoiced or SalesOrderStatus.PartiallyInvoiced)
            return row;

        ValidateOrder(row);
        if (string.IsNullOrWhiteSpace(row.OrderNumber))
        {
            var book = await _finance.GetOrCreateBillingBookAsync(ct);
            row.OrderNumber = await NextOrderNumberAsync(book, ct);
        }

        if (row.OrderStatus == SalesOrderStatus.Draft)
            row.OrderStatus = SalesOrderStatus.Confirmed;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<SalesOrder?> FulfillAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersFulfill);
        var row = await TenantOrders(_db.SalesOrders).Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return null;
        if (row.OrderStatus is SalesOrderStatus.Cancelled or SalesOrderStatus.Draft)
            throw new InvalidOperationException("Confirm the order before fulfilling.");
        if (row.OrderStatus is SalesOrderStatus.Invoiced or SalesOrderStatus.PartiallyInvoiced)
            return row;

        ValidateOrder(row);
        row.OrderStatus = SalesOrderStatus.Fulfilled;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<SalesOrder?> CancelAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersCancel);
        var row = await TenantOrders(_db.SalesOrders)
            .Include(o => o.Invoices)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return null;
        if (row.Invoices.Any(i => i.InvoiceStatus != InvoiceStatus.Void))
            throw new InvalidOperationException("Cannot cancel an order that has active invoices.");
        row.OrderStatus = SalesOrderStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<(bool Ok, string? Error, Invoice? Invoice)> CreateInvoiceFromOrderAsync(
        int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersCreateInvoice);
        var row = await TenantOrders(_db.SalesOrders)
            .Include(o => o.Lines)
            .Include(o => o.Invoices)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return (false, "Order not found.", null);
        if (row.OrderStatus == SalesOrderStatus.Cancelled)
            return (false, "Cannot invoice a cancelled order.", null);
        if (row.OrderStatus == SalesOrderStatus.Draft)
            return (false, "Confirm the order before creating an invoice.", null);
        if (row.OrderStatus == SalesOrderStatus.Invoiced || row.BalanceToInvoice <= 0.01m)
            return (false, "Order is already fully invoiced.", null);

        ValidateOrder(row);

        var invoice = new Invoice
        {
            CustomerId = row.CustomerId,
            CustomerName = row.CustomerName,
            SalesOrderId = row.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            CurrencyCode = row.CurrencyCode,
            PaymentTerms = row.PaymentTerms,
            PurchaseOrderNumber = row.PurchaseOrderNumber,
            BillToAddress = row.BillToAddress,
            Salesperson = row.Salesperson,
            Notes = string.IsNullOrWhiteSpace(row.Notes)
                ? $"Created from sales order {row.OrderNumber}"
                : row.Notes,
            Terms = row.Terms,
            Discount = row.Discount,
            InvoiceStatus = InvoiceStatus.Draft,
            Lines = row.Lines.OrderBy(l => l.SortOrder).Select(l => new InvoiceLine
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName ?? "",
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Discount = l.Discount,
                TaxRate = l.TaxRate,
                SortOrder = l.SortOrder
            }).ToList()
        };

        try
        {
            var created = await _finance.CreateInvoiceAsync(invoice, ct);
            await RefreshInvoicedAmountAsync(row, ct);
            await _db.SaveChangesAsync(ct);
            return (true, null, created);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<SalesOrder?> CreateFromQuoteAsync(int quoteId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.OrdersCreate);
        var companyId = RequireCompanyId();
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId && q.CompanyId == companyId, ct);
        if (quote is null) return null;
        if (!string.Equals(quote.QuoteStage, "Accepted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only accepted quotes can become sales orders.");

        var existing = await TenantOrders(_db.SalesOrders)
            .FirstOrDefaultAsync(o => o.QuoteId == quoteId, ct);
        if (existing is not null)
            return await GetOrderAsync(existing.Id, ct);

        // Quote lines store Discount as an absolute amount (qty * price - discount).
        // Sales order lines store Discount as a percent (0–100). Copying the raw
        // value treats e.g. a ₹100 line discount as 100% off and zeros the line.
        var quoteSubtotal = quote.Subtotal > 0
            ? quote.Subtotal
            : quote.Lines.Sum(l => Math.Max(0, l.Quantity * l.UnitPrice - l.Discount));
        var lineTaxRate = HeaderTaxToLineRate(quoteSubtotal, quote.Tax);

        var order = new SalesOrder
        {
            CustomerId = quote.CustomerId,
            QuoteId = quote.Id,
            OrderDate = DateTime.UtcNow.Date,
            CurrencyCode = quote.CurrencyCode ?? "USD",
            Notes = quote.Description,
            Terms = quote.Terms,
            Salesperson = quote.QuoteOwner,
            Discount = Math.Max(0, quote.Discount),
            Lines = quote.Lines.OrderBy(l => l.Id).Select((l, i) => new SalesOrderLine
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                Description = l.Description ?? l.ProductName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Discount = AbsoluteDiscountToPercent(l.Quantity, l.UnitPrice, l.Discount),
                TaxRate = lineTaxRate,
                SortOrder = i
            }).ToList()
        };

        return await CreateOrderAsync(order, ct);
    }

    private static decimal AbsoluteDiscountToPercent(decimal quantity, decimal unitPrice, decimal absoluteDiscount)
    {
        var gross = quantity * unitPrice;
        if (gross <= 0 || absoluteDiscount <= 0)
            return 0;
        return Math.Clamp(
            Math.Round(absoluteDiscount / gross * 100m, 4, MidpointRounding.AwayFromZero),
            0m, 100m);
    }

    private static decimal HeaderTaxToLineRate(decimal subtotal, decimal tax)
    {
        if (subtotal <= 0 || tax <= 0)
            return 0;
        return Math.Round(tax / subtotal * 100m, 4, MidpointRounding.AwayFromZero);
    }

    private async Task RefreshInvoicedAmountAsync(SalesOrder order, CancellationToken ct)
    {
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.SalesOrderId == order.Id && i.CompanyId == order.CompanyId)
            .Where(i => i.InvoiceStatus != InvoiceStatus.Void)
            .ToListAsync(ct);
        order.AmountInvoiced = invoices.Sum(i => i.GrandTotal > 0 ? i.GrandTotal : i.Amount);

        if (order.OrderStatus == SalesOrderStatus.Cancelled) return;

        if (order.AmountInvoiced <= 0.01m)
        {
            if (order.OrderStatus is SalesOrderStatus.PartiallyInvoiced or SalesOrderStatus.Invoiced)
                order.OrderStatus = SalesOrderStatus.Confirmed;
        }
        else if (order.AmountInvoiced + 0.01m >= order.GrandTotal)
            order.OrderStatus = SalesOrderStatus.Invoiced;
        else
            order.OrderStatus = SalesOrderStatus.PartiallyInvoiced;
    }

    private async Task<string> NextOrderNumberAsync(CompanyMaster book, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(book.OrderNumberPrefix))
            book.OrderNumberPrefix = "SO-";
        if (book.NextOrderSequence < 1)
            book.NextOrderSequence = 1001;

        var tracked = await _db.CompanyMasters.FirstAsync(b => b.Id == book.Id, ct);
        var number = $"{tracked.OrderNumberPrefix}{tracked.NextOrderSequence}";
        tracked.NextOrderSequence++;
        await _db.SaveChangesAsync(ct);
        return number;
    }

    private async Task ResolveCustomerAsync(SalesOrder order, CancellationToken ct)
    {
        if (order.CustomerId is not int cid) return;
        var companyId = RequireCompanyId();
        var customer = await _db.CrmCustomers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cid && c.CompanyId == companyId, ct);
        if (customer is null)
            throw new InvalidOperationException("Customer not found for this account.");
        order.CustomerName = customer.CompanyName;
        if (string.IsNullOrWhiteSpace(order.BillToAddress))
            order.BillToAddress = FormatAddress(customer);
        if (string.IsNullOrWhiteSpace(order.ShipToAddress))
            order.ShipToAddress = string.IsNullOrWhiteSpace(customer.ShippingAddress)
                ? order.BillToAddress
                : customer.ShippingAddress;
        if (string.IsNullOrWhiteSpace(order.Salesperson))
            order.Salesperson = customer.CustomerOwner;
        if (string.IsNullOrWhiteSpace(order.PaymentTerms))
            order.PaymentTerms = customer.PaymentTerms;
    }

    private static string? FormatAddress(Customer c)
    {
        if (!string.IsNullOrWhiteSpace(c.BillingAddress))
            return c.BillingAddress.Trim();
        return null;
    }

    private static void TrimHeader(SalesOrder order)
    {
        order.PurchaseOrderNumber = NullIfBlank(order.PurchaseOrderNumber);
        order.BillToAddress = NullIfBlank(order.BillToAddress);
        order.ShipToAddress = NullIfBlank(order.ShipToAddress);
        order.Salesperson = NullIfBlank(order.Salesperson);
        order.Notes = NullIfBlank(order.Notes);
        order.Terms = NullIfBlank(order.Terms);
        order.Discount = Math.Max(0, order.Discount);
    }

    private static void ValidateOrder(SalesOrder order)
    {
        if (order.CustomerId is null or <= 0)
            throw new InvalidOperationException("Customer is required.");
        if (order.Lines is null || order.Lines.Count == 0)
            throw new InvalidOperationException("Add at least one line item.");
        if (order.Lines.Any(l => l.Quantity <= 0))
            throw new InvalidOperationException("Line quantity must be greater than zero.");
        if (order.RequestedDeliveryDate is not null && order.RequestedDeliveryDate.Value.Date < order.OrderDate.Date)
            throw new InvalidOperationException("Requested delivery date cannot be before the order date.");
    }

    private static void ApplyLineTotals(SalesOrderLine line)
    {
        line.Discount = Math.Clamp(line.Discount, 0m, 100m);
        line.TaxRate = Math.Max(0, line.TaxRate);
        var net = Math.Max(0, line.Quantity * line.UnitPrice * (1m - line.Discount / 100m));
        line.TaxAmount = Math.Round(net * line.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
        line.LineTotal = Math.Round(net + line.TaxAmount, 2, MidpointRounding.AwayFromZero);
    }

    private static void RecalcTotals(SalesOrder order)
    {
        var i = 0;
        foreach (var line in order.Lines.OrderBy(l => l.SortOrder))
        {
            line.SortOrder = i++;
            ApplyLineTotals(line);
        }

        var lineNet = order.Lines.Sum(l => Math.Max(0, l.Quantity * l.UnitPrice * (1m - Math.Clamp(l.Discount, 0, 100) / 100m)));
        order.Subtotal = Math.Round(lineNet, 2, MidpointRounding.AwayFromZero);
        order.Discount = Math.Min(Math.Max(0, order.Discount), order.Subtotal);
        order.TaxTotal = Math.Round(order.Lines.Sum(l => l.TaxAmount), 2, MidpointRounding.AwayFromZero);
        order.GrandTotal = Math.Round(order.Subtotal - order.Discount + order.TaxTotal, 2, MidpointRounding.AwayFromZero);
    }

    private IQueryable<SalesOrder> TenantOrders(IQueryable<SalesOrder> q)
    {
        var companyId = RequireCompanyId();
        return q.Where(o => o.CompanyId == companyId);
    }

    private int RequireCompanyId() =>
        _tenant.CompanyId ?? throw new InvalidOperationException("No company context.");

    private void EnsureAccess(string permission)
    {
        if (!_tenant.IsAuthenticated)
            throw new UnauthorizedAccessException("Sign in required.");
        if (_tenant.IsSystemAdmin) return;
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Orders))
            throw new UnauthorizedAccessException("Sales Orders module is not enabled for this account.");
        if (!_modules.HasPermission(_tenant, permission))
            throw new UnauthorizedAccessException("You do not have permission for this action.");
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null) return null;
        var d = value.Value;
        return d.Kind switch
        {
            DateTimeKind.Utc => d,
            DateTimeKind.Local => d.ToUniversalTime(),
            _ => DateTime.SpecifyKind(d, DateTimeKind.Utc)
        };
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
