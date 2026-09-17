using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class InvoiceDocumentDiscountTests
{
    [Fact]
    public void ScaleTaxForDocumentDiscount_AppliesTaxToAmountAfterHeaderDiscount()
    {
        // ₹10,000 @ 18% with ₹1,000 header discount → tax on ₹9,000, not ₹10,000.
        Assert.Equal(1620m, Invoice.ScaleTaxForDocumentDiscount(10000m, 1000m, 1800m));
        Assert.Equal(0m, Invoice.ScaleTaxForDocumentDiscount(10000m, 10000m, 1800m));
        Assert.Equal(1800m, Invoice.ScaleTaxForDocumentDiscount(10000m, 0m, 1800m));
        Assert.Equal(0m, Invoice.ScaleTaxForDocumentDiscount(0m, 0m, 0m));
    }

    [Fact]
    public async Task CreateInvoice_HeaderDiscount_ReducesTaxableValueAndPostedSales()
    {
        var (db, tenant, posting, accounting) = await CreateAsync();
        var finance = new FinanceService(db, tenant, new ModuleRegistry(), posting, accounting);

        var invoice = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Acme",
            InvoiceDate = DateTime.UtcNow.Date,
            Discount = 1000m,
            Lines =
            [
                new InvoiceLine { ProductName = "License", Quantity = 1, UnitPrice = 10000m, TaxRate = 18m }
            ]
        });

        Assert.Equal(10000m, invoice.Subtotal);
        Assert.Equal(1000m, invoice.Discount);
        Assert.Equal(1620m, invoice.TaxTotal);
        Assert.Equal(10620m, invoice.GrandTotal);

        await accounting.EnsureBootstrapAsync();
        var settings = await db.AccountingSettings.FirstAsync();
        settings.AutoPostSalesInvoice = true;
        await db.SaveChangesAsync();

        var sent = await finance.MarkSentAsync(invoice.Id);
        Assert.NotNull(sent);
        Assert.True(sent!.AccountingVoucherId is > 0);

        var voucher = await db.AccountingVouchers
            .Include(v => v.Lines).ThenInclude(l => l.Ledger)
            .FirstAsync(v => v.Id == sent.AccountingVoucherId);
        Assert.Equal(10620m, voucher.TotalDebit);
        Assert.Equal(10620m, voucher.TotalCredit);

        var sales = voucher.Lines.Single(l => l.Ledger!.SystemCode == SystemLedgerCodes.Sales);
        Assert.Equal(9000m, sales.CreditAmount);
        var tax = voucher.Lines.Where(l =>
            l.Ledger!.SystemCode is SystemLedgerCodes.CgstOutput or SystemLedgerCodes.SgstOutput)
            .Sum(l => l.CreditAmount);
        Assert.Equal(1620m, tax);
    }

    [Fact]
    public async Task CreateOrder_HeaderDiscount_ReducesTaxAndGrandTotal()
    {
        var (db, tenant, posting, accounting) = await CreateAsync();
        var finance = new FinanceService(db, tenant, new ModuleRegistry(), posting, accounting);
        var orders = new SalesOrderService(db, tenant, new ModuleRegistry(), finance);

        db.CrmCustomers.Add(new ShaunERP.Core.Crm.Customer
        {
            Id = 1,
            CompanyId = 1,
            CompanyName = "Acme",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var order = await orders.CreateOrderAsync(new SalesOrder
        {
            CustomerId = 1,
            Discount = 200m,
            Lines =
            [
                new SalesOrderLine { ProductName = "Hours", Quantity = 10, UnitPrice = 100m, TaxRate = 18m }
            ]
        });

        Assert.Equal(1000m, order.Subtotal);
        Assert.Equal(144m, order.TaxTotal);
        Assert.Equal(944m, order.GrandTotal);
    }

    private static async Task<(ShaunErpDbContext db, TenantContext tenant, AccountingPostingService posting, AccountingService accounting)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Shaun Infomedia OPC Pvt Ltd" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Shaun Infomedia OPC Pvt Ltd",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleKeys.Accounting, ModuleKeys.Finance, ModuleKeys.Orders, ModuleKeys.Crm
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        return (db, tenant, posting, accounting);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
