using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using GlAccount = ShaunERP.Core.Finance.Account;

namespace ShaunERP.Infrastructure.Services;

public interface IFinanceService
{
    Task<FinanceDashboardStats> GetDashboardAsync(CancellationToken ct = default);

    Task<CompanyMaster> GetOrCreateBillingBookAsync(CancellationToken ct = default);
    Task<CompanyMaster?> GetBillingBookAsync(CancellationToken ct = default);
    Task<CompanyMaster?> UpdateBillingBookAsync(CompanyMaster update, CancellationToken ct = default);

    Task<IReadOnlyList<Customer>> ListCrmCustomersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CrmProduct>> ListProductsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Invoice>> ListInvoicesAsync(CancellationToken ct = default);
    Task<PagedResult<Invoice>> QueryInvoicesAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Invoice?> GetInvoiceAsync(int id, CancellationToken ct = default);
    Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> UpdateInvoiceAsync(int id, Invoice update, CancellationToken ct = default);
    Task<bool> DeleteInvoiceAsync(int id, CancellationToken ct = default);
    Task<Invoice?> MarkSentAsync(int id, CancellationToken ct = default);
    Task<Invoice?> VoidInvoiceAsync(int id, CancellationToken ct = default);
    Task<Invoice?> MarkPaidAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<InvoicePayment>> ListPaymentsAsync(CancellationToken ct = default);
    Task<PagedResult<InvoicePayment>> QueryPaymentsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<(bool Ok, string? Error, InvoicePayment? Payment)> RecordPaymentAsync(
        int invoiceId, InvoicePayment payment, CancellationToken ct = default);
    Task<bool> DeletePaymentAsync(int paymentId, CancellationToken ct = default);
}

public record FinanceDashboardStats(
    int DraftInvoices,
    int SentInvoices,
    int OverdueInvoices,
    int PaidInvoices,
    decimal OutstandingBalance,
    decimal CollectedThisMonth,
    decimal InvoicedThisMonth,
    int InvoiceCount,
    int PaymentCount);

public class FinanceService : IFinanceService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;
    private readonly IAccountingPostingService _accountingPosting;
    private readonly IAccountingService _accounting;

    public FinanceService(
        ShaunErpDbContext db,
        ITenantContext tenant,
        IModuleRegistry modules,
        IAccountingPostingService accountingPosting,
        IAccountingService accounting)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
        _accountingPosting = accountingPosting;
        _accounting = accounting;
    }

    public async Task<FinanceDashboardStats> GetDashboardAsync(CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceDashboardView);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoices = await TenantInvoices(_db.Invoices.AsNoTracking())
            .Include(i => i.Payments)
            .Take(2000)
            .ToListAsync(ct);

        var draft = invoices.Count(i => i.InvoiceStatus == InvoiceStatus.Draft);
        var sent = invoices.Count(i => i.InvoiceStatus is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid);
        var overdue = invoices.Count(i => i.IsOverdue);
        var paid = invoices.Count(i => i.InvoiceStatus == InvoiceStatus.Paid || i.IsPaid);
        var outstanding = invoices
            .Where(i => i.InvoiceStatus is not (InvoiceStatus.Void or InvoiceStatus.Paid) && !i.IsPaid)
            .Sum(i => i.BalanceDue);
        var collected = invoices
            .SelectMany(i => i.Payments)
            .Where(p => p.PaymentDate >= monthStart)
            .Sum(p => p.Amount);
        var invoiced = invoices
            .Where(i => i.InvoiceDate >= monthStart && i.InvoiceStatus != InvoiceStatus.Void)
            .Sum(i => i.GrandTotal > 0 ? i.GrandTotal : i.Amount);
        var paymentCount = invoices.Sum(i => i.Payments.Count);

        return new FinanceDashboardStats(
            draft, sent, overdue, paid, outstanding, collected, invoiced, invoices.Count, paymentCount);
    }

    public async Task<CompanyMaster> GetOrCreateBillingBookAsync(CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceAccess);
        var companyId = RequireCompanyId();
        var book = await _db.CompanyMasters
            .FirstOrDefaultAsync(b => b.TenantCompanyId == companyId, ct);
        if (book is not null) return book;

        book = new CompanyMaster
        {
            TenantCompanyId = companyId,
            Name = _tenant.CompanyName ?? "Billing book",
            LegalName = _tenant.CompanyName,
            Currency = "USD",
            InvoiceNumberPrefix = "INV-",
            NextInvoiceSequence = 1001,
            DefaultPaymentTerms = "Net 30",
            DefaultDueDays = 30,
            CreatedDate = DateTime.UtcNow
        };
        _db.CompanyMasters.Add(book);
        await _db.SaveChangesAsync(ct);

        if (!await _db.Accounts.AnyAsync(a => a.CompanyMasterId == book.Id, ct))
        {
            _db.Accounts.AddRange(
                new GlAccount { CompanyMasterId = book.Id, Name = "Cash", AccountNumber = "1000", Type = AccountType.Asset },
                new GlAccount { CompanyMasterId = book.Id, Name = "Accounts Receivable", AccountNumber = "1100", Type = AccountType.Asset },
                new GlAccount { CompanyMasterId = book.Id, Name = "Sales Revenue", AccountNumber = "4000", Type = AccountType.Revenue });
            await _db.SaveChangesAsync(ct);
        }

        return book;
    }

    public async Task<CompanyMaster?> GetBillingBookAsync(CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceAccess);
        var companyId = RequireCompanyId();
        return await _db.CompanyMasters.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantCompanyId == companyId, ct);
    }

    public async Task<CompanyMaster?> UpdateBillingBookAsync(CompanyMaster update, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceSettingsManage);
        var book = await GetOrCreateBillingBookAsync(ct);
        book.Name = update.Name.Trim();
        book.LegalName = update.LegalName;
        book.TaxId = update.TaxId;
        book.Address = update.Address;
        book.BillingStreet = update.BillingStreet;
        book.BillingCity = update.BillingCity;
        book.BillingState = update.BillingState;
        book.BillingZip = update.BillingZip;
        book.BillingCountry = update.BillingCountry;
        book.ContactNumber = update.ContactNumber;
        book.Email = update.Email;
        book.Currency = string.IsNullOrWhiteSpace(update.Currency) ? book.Currency : update.Currency.Trim();
        book.InvoiceNumberPrefix = string.IsNullOrWhiteSpace(update.InvoiceNumberPrefix)
            ? book.InvoiceNumberPrefix
            : update.InvoiceNumberPrefix.Trim();
        if (update.NextInvoiceSequence > 0)
            book.NextInvoiceSequence = update.NextInvoiceSequence;
        book.DefaultPaymentTerms = update.DefaultPaymentTerms;
        book.DefaultDueDays = update.DefaultDueDays > 0 ? update.DefaultDueDays : book.DefaultDueDays;
        await _db.SaveChangesAsync(ct);
        return book;
    }

    public async Task<IReadOnlyList<Customer>> ListCrmCustomersAsync(CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceCustomersView);
        var companyId = RequireCompanyId();
        return await _db.CrmCustomers.AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.CompanyName)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CrmProduct>> ListProductsAsync(CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesView);
        var companyId = RequireCompanyId();
        return await _db.CrmProducts.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .OrderBy(p => p.Name)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Invoice>> ListInvoicesAsync(CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesView);
        return await TenantInvoices(_db.Invoices.AsNoTracking())
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .OrderByDescending(i => i.Id)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<Invoice>> QueryInvoicesAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesView);
        query ??= new ListQuery();
        IQueryable<Invoice> q = TenantInvoices(_db.Invoices.AsNoTracking()).Include(i => i.Customer);
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(i =>
                EF.Functions.ILike(i.InvoiceNumber, pattern) ||
                (i.CustomerName != null && EF.Functions.ILike(i.CustomerName, pattern)) ||
                (i.PurchaseOrderNumber != null && EF.Functions.ILike(i.PurchaseOrderNumber, pattern)) ||
                (i.Salesperson != null && EF.Functions.ILike(i.Salesperson, pattern)) ||
                (i.PaymentTerms != null && EF.Functions.ILike(i.PaymentTerms, pattern)) ||
                (i.Notes != null && EF.Functions.ILike(i.Notes, pattern)) ||
                (i.Customer != null && i.Customer.CompanyName != null && EF.Functions.ILike(i.Customer.CompanyName, pattern)));
        }

        return await q.OrderByDescending(i => i.Id).ToPagedResultAsync(query, ct);
    }

    public async Task<Invoice?> GetInvoiceAsync(int id, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesView);
        return await TenantInvoices(_db.Invoices.AsNoTracking())
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesCreate);
        var companyId = RequireCompanyId();
        var book = await GetOrCreateBillingBookAsync(ct);

        invoice.Id = 0;
        invoice.CompanyId = companyId;
        invoice.CompanyMasterId = book.Id;
        invoice.CreatedDate = DateTime.UtcNow;
        invoice.InvoiceDate = NormalizeUtc(invoice.InvoiceDate) ?? DateTime.UtcNow;
        invoice.DueDate = NormalizeUtc(invoice.DueDate)
            ?? invoice.InvoiceDate.AddDays(book.DefaultDueDays > 0 ? book.DefaultDueDays : 30);
        if (string.IsNullOrWhiteSpace(invoice.CurrencyCode))
            invoice.CurrencyCode = book.Currency;
        if (string.IsNullOrWhiteSpace(invoice.PaymentTerms))
            invoice.PaymentTerms = book.DefaultPaymentTerms;
        if (invoice.InvoiceStatus == default && !invoice.IsPaid)
            invoice.InvoiceStatus = InvoiceStatus.Draft;

        invoice.PurchaseOrderNumber = string.IsNullOrWhiteSpace(invoice.PurchaseOrderNumber)
            ? null : invoice.PurchaseOrderNumber.Trim();
        invoice.BillToAddress = string.IsNullOrWhiteSpace(invoice.BillToAddress)
            ? null : invoice.BillToAddress.Trim();
        invoice.Salesperson = string.IsNullOrWhiteSpace(invoice.Salesperson)
            ? null : invoice.Salesperson.Trim();
        invoice.Terms = string.IsNullOrWhiteSpace(invoice.Terms)
            ? null : invoice.Terms.Trim();
        invoice.Discount = Math.Max(0, invoice.Discount);

        await ResolveCustomerNameAsync(invoice, ct);
        ValidateInvoice(invoice);

        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            invoice.InvoiceNumber = await NextInvoiceNumberAsync(book, ct);
        else
            invoice.InvoiceNumber = invoice.InvoiceNumber.Trim();

        foreach (var line in invoice.Lines)
        {
            line.Id = 0;
            line.CompanyId = companyId;
            line.CreatedDate = DateTime.UtcNow;
            ApplyLineTotals(line);
        }

        RecalcInvoiceTotals(invoice);
        SyncLegacyPaidFlags(invoice);

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        await SyncLinkedSalesOrderAsync(invoice.SalesOrderId, ct);
        return invoice;
    }

    public async Task<Invoice?> UpdateInvoiceAsync(int id, Invoice update, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesUpdate);
        var row = await TenantInvoices(_db.Invoices)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (row is null) return null;

        if (row.InvoiceStatus is InvoiceStatus.Void or InvoiceStatus.Paid)
            throw new InvalidOperationException("Paid or void invoices cannot be edited.");

        var previousStatus = row.InvoiceStatus;
        var previousTotal = row.GrandTotal;
        var previousTax = row.TaxTotal;
        var previousCustomerId = row.CustomerId;

        row.CompanyId = RequireCompanyId();
        row.CustomerId = update.CustomerId;
        row.FinanceCustomerId = update.FinanceCustomerId;
        row.CustomerName = update.CustomerName;
        row.PurchaseOrderNumber = string.IsNullOrWhiteSpace(update.PurchaseOrderNumber)
            ? null : update.PurchaseOrderNumber.Trim();
        row.BillToAddress = string.IsNullOrWhiteSpace(update.BillToAddress)
            ? null : update.BillToAddress.Trim();
        row.Salesperson = string.IsNullOrWhiteSpace(update.Salesperson)
            ? null : update.Salesperson.Trim();
        row.Terms = string.IsNullOrWhiteSpace(update.Terms)
            ? null : update.Terms.Trim();
        await ResolveCustomerNameAsync(row, ct);

        // Number is locked once issued (non-draft)
        if (row.InvoiceStatus == InvoiceStatus.Draft && !string.IsNullOrWhiteSpace(update.InvoiceNumber))
            row.InvoiceNumber = update.InvoiceNumber.Trim();
        row.InvoiceDate = NormalizeUtc(update.InvoiceDate) ?? row.InvoiceDate;
        row.DueDate = NormalizeUtc(update.DueDate);
        row.CurrencyCode = update.CurrencyCode;
        row.PaymentTerms = update.PaymentTerms;
        row.Notes = update.Notes;
        row.Discount = Math.Max(0, update.Discount);

        // Status changes that don't require mark-paid permission
        if (update.InvoiceStatus is InvoiceStatus.Draft or InvoiceStatus.Sent)
            row.InvoiceStatus = update.InvoiceStatus;

        _db.InvoiceLines.RemoveRange(row.Lines);
        row.Lines.Clear();
        foreach (var line in update.Lines ?? [])
        {
            var newLine = new InvoiceLine
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

        ValidateInvoice(row);
        RecalcInvoiceTotals(row);
        RefreshPaymentStatus(row);
        await _db.SaveChangesAsync(ct);
        await SyncLinkedSalesOrderAsync(row.SalesOrderId, ct);
        await ResyncInvoiceAccountingAfterEditAsync(
            row, previousStatus, previousTotal, previousTax, previousCustomerId, ct);
        return row;
    }

    public async Task<bool> DeleteInvoiceAsync(int id, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesDelete);
        var row = await TenantInvoices(_db.Invoices)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (row is null) return false;
        if (row.InvoiceStatus is InvoiceStatus.Paid or InvoiceStatus.PartiallyPaid || row.Payments.Count > 0)
            throw new InvalidOperationException("Paid or partially paid invoices cannot be deleted. Void instead.");

        var salesOrderId = row.SalesOrderId;
        try
        {
            await ReverseInvoiceAccountingAsync(row, ct);
        }
        catch (UnauthorizedAccessException)
        {
        }

        _db.Invoices.Remove(row);
        await _db.SaveChangesAsync(ct);
        await SyncLinkedSalesOrderAsync(salesOrderId, ct);
        return true;
    }

    public async Task<Invoice?> MarkSentAsync(int id, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesUpdate);
        var row = await TenantInvoices(_db.Invoices)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (row is null) return null;
        if (row.InvoiceStatus == InvoiceStatus.Void)
            throw new InvalidOperationException("Cannot send a void invoice.");
        if (row.InvoiceStatus == InvoiceStatus.Paid)
            throw new InvalidOperationException("Paid invoices are already issued.");

        ValidateInvoice(row);

        if (string.IsNullOrWhiteSpace(row.InvoiceNumber))
        {
            var book = await GetOrCreateBillingBookAsync(ct);
            row.InvoiceNumber = await NextInvoiceNumberAsync(book, ct);
        }

        if (row.InvoiceStatus == InvoiceStatus.Draft)
            row.InvoiceStatus = InvoiceStatus.Sent;
        await _db.SaveChangesAsync(ct);

        try
        {
            await PostInvoiceAccountingAsync(row, ct);
        }
        catch (UnauthorizedAccessException)
        {
            // Accounting module may be disabled; invoicing still succeeds.
        }
        catch (Exception)
        {
            // Surface accounting failures — invoice is already Sent; caller can retry posting from accounting.
            throw;
        }

        return row;
    }

    public async Task<Invoice?> VoidInvoiceAsync(int id, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesUpdate);
        var row = await TenantInvoices(_db.Invoices)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (row is null) return null;
        if (row.Payments.Count > 0)
            throw new InvalidOperationException("Cannot void an invoice with recorded payments.");
        row.InvoiceStatus = InvoiceStatus.Void;
        row.IsPaid = false;
        await _db.SaveChangesAsync(ct);
        await SyncLinkedSalesOrderAsync(row.SalesOrderId, ct);

        try
        {
            await ReverseInvoiceAccountingAsync(row, ct);
        }
        catch (UnauthorizedAccessException)
        {
        }

        return row;
    }

    public async Task<Invoice?> MarkPaidAsync(int id, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinanceInvoicesMarkPaid);
        var row = await TenantInvoices(_db.Invoices)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (row is null) return null;
        if (row.InvoiceStatus == InvoiceStatus.Void)
            throw new InvalidOperationException("Cannot mark a void invoice as paid.");

        var remaining = row.BalanceDue;
        if (remaining > 0)
        {
            row.Payments.Add(new InvoicePayment
            {
                CompanyId = row.CompanyId,
                Amount = remaining,
                PaymentDate = DateTime.UtcNow,
                Method = "Mark paid",
                Reference = "Full settlement",
                CreatedDate = DateTime.UtcNow
            });
            row.AmountPaid = row.GrandTotal > 0 ? row.GrandTotal : row.Amount;
        }

        row.InvoiceStatus = InvoiceStatus.Paid;
        row.IsPaid = true;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<IReadOnlyList<InvoicePayment>> ListPaymentsAsync(CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinancePaymentsRecord);
        var companyId = RequireCompanyId();
        return await _db.InvoicePayments.AsNoTracking()
            .Include(p => p.Invoice)
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<InvoicePayment>> QueryPaymentsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinancePaymentsRecord);
        query ??= new ListQuery();
        var companyId = RequireCompanyId();
        var q = _db.InvoicePayments.AsNoTracking()
            .Include(p => p.Invoice)
            .Where(p => p.CompanyId == companyId);
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(p =>
                (p.Reference != null && EF.Functions.ILike(p.Reference, pattern)) ||
                (p.Method != null && EF.Functions.ILike(p.Method, pattern)) ||
                (p.Notes != null && EF.Functions.ILike(p.Notes, pattern)) ||
                (p.Invoice != null && EF.Functions.ILike(p.Invoice.InvoiceNumber, pattern)) ||
                (p.Invoice != null && p.Invoice.CustomerName != null && EF.Functions.ILike(p.Invoice.CustomerName, pattern)));
        }

        return await q.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id).ToPagedResultAsync(query, ct);
    }

    public async Task<(bool Ok, string? Error, InvoicePayment? Payment)> RecordPaymentAsync(
        int invoiceId, InvoicePayment payment, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinancePaymentsRecord);
        var row = await TenantInvoices(_db.Invoices)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (row is null) return (false, "Invoice not found.", null);
        if (row.InvoiceStatus == InvoiceStatus.Void)
            return (false, "Cannot record payment on a void invoice.", null);
        if (payment.Amount <= 0)
            return (false, "Payment amount must be greater than zero.", null);

        var balance = row.BalanceDue;
        if (payment.Amount > balance + 0.01m)
            return (false, $"Payment exceeds balance due ({balance:N2}).", null);

        var entity = new InvoicePayment
        {
            CompanyId = row.CompanyId,
            InvoiceId = row.Id,
            Amount = payment.Amount,
            PaymentDate = NormalizeUtc(payment.PaymentDate) ?? DateTime.UtcNow,
            Method = string.IsNullOrWhiteSpace(payment.Method) ? "Bank transfer" : payment.Method,
            Reference = payment.Reference,
            Notes = payment.Notes,
            CreatedDate = DateTime.UtcNow
        };
        row.Payments.Add(entity);
        row.AmountPaid = row.Payments.Sum(p => p.Amount);
        RefreshPaymentStatus(row);
        if (row.InvoiceStatus == InvoiceStatus.Draft)
            row.InvoiceStatus = InvoiceStatus.Sent;
        RefreshPaymentStatus(row);

        await _db.SaveChangesAsync(ct);
        return (true, null, entity);
    }

    public async Task<bool> DeletePaymentAsync(int paymentId, CancellationToken ct = default)
    {
        EnsureFinanceAccess(Permissions.FinancePaymentsRecord);
        var companyId = RequireCompanyId();
        var payment = await _db.InvoicePayments
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.CompanyId == companyId, ct);
        if (payment is null) return false;

        var invoice = await TenantInvoices(_db.Invoices)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == payment.InvoiceId, ct);
        if (invoice is null) return false;

        _db.InvoicePayments.Remove(payment);
        invoice.Payments.Remove(payment);
        invoice.AmountPaid = invoice.Payments.Sum(p => p.Amount);
        RefreshPaymentStatus(invoice);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ResolveCustomerNameAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice.CustomerId is int customerId)
        {
            var companyId = RequireCompanyId();
            var customer = await _db.CrmCustomers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId, ct);
            if (customer is not null)
            {
                invoice.CustomerName = customer.CompanyName;
                if (string.IsNullOrWhiteSpace(invoice.PaymentTerms))
                    invoice.PaymentTerms = customer.PaymentTerms;
                if (string.IsNullOrWhiteSpace(invoice.BillToAddress))
                    invoice.BillToAddress = customer.BillingAddress;
                if (string.IsNullOrWhiteSpace(invoice.Salesperson))
                    invoice.Salesperson = customer.CustomerOwner;
            }
        }
        else if (string.IsNullOrWhiteSpace(invoice.CustomerName) && invoice.FinanceCustomerId is int fcId)
        {
            var fc = await _db.FinanceCustomers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == fcId, ct);
            if (fc is not null) invoice.CustomerName = fc.Name;
        }
    }

    private async Task<string> NextInvoiceNumberAsync(CompanyMaster book, CancellationToken ct)
    {
        // Reload tracked book for sequence bump
        var tracked = await _db.CompanyMasters.FirstAsync(b => b.Id == book.Id, ct);
        var number = $"{tracked.InvoiceNumberPrefix}{tracked.NextInvoiceSequence:D4}";
        tracked.NextInvoiceSequence++;
        await _db.SaveChangesAsync(ct);
        return number;
    }

    private static void ValidateInvoice(Invoice invoice)
    {
        if (invoice.CustomerId is null or <= 0 && string.IsNullOrWhiteSpace(invoice.CustomerName))
            throw new InvalidOperationException("Customer is required.");

        var lines = (invoice.Lines ?? []).Where(l =>
            !string.IsNullOrWhiteSpace(l.ProductName)
            || !string.IsNullOrWhiteSpace(l.Description)
            || l.UnitPrice != 0
            || l.Quantity > 0).ToList();

        if (lines.Count == 0)
            throw new InvalidOperationException("Add at least one invoice line.");

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Line quantity must be greater than zero.");
            if (line.Discount is < 0 or > 100)
                throw new InvalidOperationException("Line discount percent must be between 0 and 100.");
            if (line.TaxRate is < 0 or > 100)
                throw new InvalidOperationException("Line tax percent must be between 0 and 100.");
        }

        if (invoice.DueDate is DateTime due && due.Date < invoice.InvoiceDate.Date)
            throw new InvalidOperationException("Due date cannot be earlier than the issue date.");

        if (invoice.Discount < 0)
            throw new InvalidOperationException("Document discount cannot be negative.");
    }

    private static void ApplyLineTotals(InvoiceLine line)
    {
        if (line.Quantity <= 0) line.Quantity = 1;
        line.Discount = Math.Clamp(line.Discount, 0m, 100m);
        line.TaxRate = Math.Clamp(line.TaxRate, 0m, 100m);
        var net = InvoiceLine.LineNetAmount(line.Quantity, line.UnitPrice, line.Discount);
        line.TaxAmount = Math.Round(net * (line.TaxRate / 100m), 2);
        line.LineTotal = Math.Round(net + line.TaxAmount, 2);
    }

    private static void RecalcInvoiceTotals(Invoice invoice)
    {
        foreach (var line in invoice.Lines)
            ApplyLineTotals(line);

        invoice.Subtotal = Math.Round(
            invoice.Lines.Sum(l => InvoiceLine.LineNetAmount(l.Quantity, l.UnitPrice, l.Discount)), 2);
        invoice.TaxTotal = Math.Round(invoice.Lines.Sum(l => l.TaxAmount), 2);
        invoice.Discount = Math.Max(0, invoice.Discount);
        var afterDiscount = Math.Max(0, invoice.Subtotal - invoice.Discount);
        invoice.GrandTotal = Math.Round(afterDiscount + invoice.TaxTotal, 2);
        invoice.Amount = invoice.GrandTotal;
        invoice.AmountPaid = invoice.Payments?.Sum(p => p.Amount) ?? invoice.AmountPaid;
    }

    private static void RefreshPaymentStatus(Invoice invoice)
    {
        if (invoice.InvoiceStatus == InvoiceStatus.Void) return;

        invoice.AmountPaid = invoice.Payments?.Sum(p => p.Amount) ?? invoice.AmountPaid;
        var total = invoice.GrandTotal > 0 ? invoice.GrandTotal : invoice.Amount;
        if (total <= 0)
        {
            SyncLegacyPaidFlags(invoice);
            return;
        }

        if (invoice.AmountPaid <= 0)
        {
            if (invoice.InvoiceStatus is InvoiceStatus.Paid or InvoiceStatus.PartiallyPaid)
                invoice.InvoiceStatus = InvoiceStatus.Sent;
            invoice.IsPaid = false;
            return;
        }

        if (invoice.AmountPaid + 0.01m >= total)
        {
            invoice.InvoiceStatus = InvoiceStatus.Paid;
            invoice.IsPaid = true;
            invoice.AmountPaid = total;
        }
        else
        {
            invoice.InvoiceStatus = InvoiceStatus.PartiallyPaid;
            invoice.IsPaid = false;
        }
    }

    private static void SyncLegacyPaidFlags(Invoice invoice)
    {
        invoice.IsPaid = invoice.InvoiceStatus == InvoiceStatus.Paid;
        if (invoice.IsPaid)
            invoice.AmountPaid = invoice.GrandTotal > 0 ? invoice.GrandTotal : invoice.Amount;
    }

    private async Task PostInvoiceAccountingAsync(Invoice invoice, CancellationToken ct)
    {
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Accounting))
            return;

        var settings = await _db.AccountingSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == RequireCompanyId(), ct);
        if (settings is not null && !settings.AutoPostSalesInvoice)
            return;

        if (invoice.AccountingVoucherId is > 0)
            return;

        var existing = await _accountingPosting.FindBySourceAsync(
            AccountingSourceModules.Sales, nameof(Invoice), invoice.Id.ToString(), "Primary", ct);
        if (existing is not null)
        {
            invoice.AccountingVoucherId = existing.Id;
            await _db.SaveChangesAsync(ct);
            return;
        }

        await _accounting.EnsureBootstrapAsync(ct);

        int? customerLedgerId = null;
        if (invoice.CustomerId is > 0)
        {
            var customer = await _db.CrmCustomers.FirstOrDefaultAsync(c => c.Id == invoice.CustomerId, ct);
            if (customer is not null)
            {
                var custLedger = await _accounting.EnsureCustomerLedgerAsync(customer, ct);
                customerLedgerId = custLedger.Id;
            }
        }

        var taxable = invoice.GrandTotal > 0
            ? Math.Max(0, invoice.GrandTotal - invoice.TaxTotal)
            : Math.Max(0, invoice.Amount - invoice.TaxTotal);
        var tax = invoice.TaxTotal;
        var total = invoice.GrandTotal > 0 ? invoice.GrandTotal : invoice.Amount;
        if (total <= 0) return;

        var lines = new List<AccountingPostingLineRequest>
        {
            new()
            {
                LedgerId = customerLedgerId,
                LedgerSystemCode = customerLedgerId is null ? SystemLedgerCodes.AccountsReceivable : null,
                Debit = total,
                Description = $"Invoice {invoice.InvoiceNumber}",
                CustomerId = invoice.CustomerId,
                ReferenceType = nameof(Invoice),
                ReferenceId = invoice.Id.ToString()
            },
            new()
            {
                LedgerSystemCode = SystemLedgerCodes.Sales,
                Credit = taxable,
                Description = $"Sales {invoice.InvoiceNumber}"
            }
        };

        if (tax > 0)
        {
            var half = Math.Round(tax / 2m, 2);
            var other = tax - half;
            lines.Add(new AccountingPostingLineRequest
            {
                LedgerSystemCode = SystemLedgerCodes.CgstOutput,
                Credit = half,
                Description = "Output CGST"
            });
            lines.Add(new AccountingPostingLineRequest
            {
                LedgerSystemCode = SystemLedgerCodes.SgstOutput,
                Credit = other,
                Description = "Output SGST"
            });
        }

        var voucher = await _accountingPosting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = invoice.InvoiceDate,
            VoucherTypeCode = SystemVoucherTypeCodes.Sales,
            ReferenceNumber = invoice.InvoiceNumber,
            ReferenceDate = invoice.InvoiceDate,
            Narration = $"Sales invoice {invoice.InvoiceNumber}",
            SourceModule = AccountingSourceModules.Sales,
            SourceEntityType = nameof(Invoice),
            SourceEntityId = invoice.Id.ToString(),
            PostingType = "Primary",
            PostImmediately = true,
            Lines = lines
        }, ct);

        invoice.AccountingVoucherId = voucher.Id;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ReverseInvoiceAccountingAsync(Invoice invoice, CancellationToken ct)
    {
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Accounting))
            return;

        AccountingVoucher? voucher = null;
        if (invoice.AccountingVoucherId is > 0)
        {
            voucher = await _db.AccountingVouchers.FirstOrDefaultAsync(
                v => v.Id == invoice.AccountingVoucherId && v.CompanyId == invoice.CompanyId, ct);
        }

        voucher ??= await _accountingPosting.FindBySourceAsync(
            AccountingSourceModules.Sales, nameof(Invoice), invoice.Id.ToString(), "Primary", ct);

        if (voucher is null || voucher.VoucherStatus != VoucherStatus.Posted)
            return;

        await _accountingPosting.ReverseAsync(voucher.Id, $"Invoice {invoice.InvoiceNumber} voided", ct);
    }

    private async Task SyncLinkedSalesOrderAsync(int? salesOrderId, CancellationToken ct)
    {
        if (salesOrderId is not int orderId)
            return;

        var companyId = RequireCompanyId();
        var order = await _db.SalesOrders.FirstOrDefaultAsync(
            o => o.Id == orderId && o.CompanyId == companyId, ct);
        if (order is null)
            return;

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.SalesOrderId == order.Id && i.CompanyId == order.CompanyId)
            .Where(i => i.InvoiceStatus != InvoiceStatus.Void)
            .ToListAsync(ct);
        order.AmountInvoiced = invoices.Sum(i => i.GrandTotal > 0 ? i.GrandTotal : i.Amount);

        if (order.OrderStatus == SalesOrderStatus.Cancelled)
        {
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (order.AmountInvoiced <= 0.01m)
        {
            if (order.OrderStatus is SalesOrderStatus.PartiallyInvoiced or SalesOrderStatus.Invoiced)
                order.OrderStatus = SalesOrderStatus.Confirmed;
        }
        else if (order.AmountInvoiced + 0.01m >= order.GrandTotal)
            order.OrderStatus = SalesOrderStatus.Invoiced;
        else
            order.OrderStatus = SalesOrderStatus.PartiallyInvoiced;

        await _db.SaveChangesAsync(ct);
    }

    private async Task ResyncInvoiceAccountingAfterEditAsync(
        Invoice invoice,
        InvoiceStatus previousStatus,
        decimal previousTotal,
        decimal previousTax,
        int? previousCustomerId,
        CancellationToken ct)
    {
        var issued = invoice.InvoiceStatus is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid;
        var wasIssued = previousStatus is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid;
        var financialsChanged = invoice.GrandTotal != previousTotal
            || invoice.TaxTotal != previousTax
            || invoice.CustomerId != previousCustomerId
            || invoice.InvoiceStatus != previousStatus;

        if (!financialsChanged)
            return;

        try
        {
            if (wasIssued)
            {
                await ReverseInvoiceAccountingAsync(invoice, ct);
                invoice.AccountingVoucherId = null;
                await _db.SaveChangesAsync(ct);
            }

            if (issued)
                await PostInvoiceAccountingAsync(invoice, ct);
        }
        catch (UnauthorizedAccessException)
        {
            // Accounting module may be disabled; invoice edit still succeeds.
        }
    }

    private IQueryable<Invoice> TenantInvoices(IQueryable<Invoice> query)
    {
        var companyId = RequireCompanyId();
        return query.Where(i => i.CompanyId == companyId
            || (i.CompanyId == null
                && _db.CompanyMasters.Any(b => b.Id == i.CompanyMasterId && b.TenantCompanyId == companyId)));
    }

    private void EnsureFinanceAccess(string permission)
    {
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Finance) || !_modules.HasPermission(_tenant, permission))
        {
            throw new UnauthorizedAccessException("Finance access denied.");
        }
    }

    private int RequireCompanyId()
        => _tenant.CompanyId ?? throw new InvalidOperationException("Tenant context is required.");

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null) return null;
        var dt = value.Value;
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
    }
}
