using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class FinanceInvoicePostingTests
{
    [Fact]
    public async Task MarkSent_Posts_When_One_Cent_Tax_Rounds_Cgst_To_Zero()
    {
        var (db, finance, accounting) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var customer = await SeedCustomerAsync(db);

        var invoice = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            CurrencyCode = "INR",
            Lines =
            [
                new InvoiceLine
                {
                    ProductName = "Small item",
                    Quantity = 1,
                    UnitPrice = 1.00m,
                    TaxRate = 1m
                }
            ]
        });

        Assert.Equal(0.01m, invoice.TaxTotal);

        var sent = await finance.MarkSentAsync(invoice.Id);
        Assert.NotNull(sent);
        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);
        Assert.True(sent.AccountingVoucherId is > 0);

        var voucher = await db.AccountingVouchers
            .Include(v => v.Lines)
            .FirstAsync(v => v.Id == sent.AccountingVoucherId);
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Equal(voucher.TotalDebit, voucher.TotalCredit);
        Assert.Equal(invoice.GrandTotal, voucher.TotalDebit);
        Assert.DoesNotContain(voucher.Lines, l => l.DebitAmount == 0 && l.CreditAmount == 0);
    }

    [Fact]
    public async Task MarkSent_Posts_When_Header_Discount_Zeroes_Sales_Credit()
    {
        var (db, finance, accounting) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var customer = await SeedCustomerAsync(db);

        var invoice = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            CurrencyCode = "INR",
            Discount = 1000m,
            Lines =
            [
                new InvoiceLine
                {
                    ProductName = "Complimentary service",
                    Quantity = 1,
                    UnitPrice = 1000m,
                    TaxRate = 18m
                }
            ]
        });

        Assert.Equal(0m, Math.Max(0, invoice.GrandTotal - invoice.TaxTotal));
        Assert.Equal(180m, invoice.TaxTotal);
        Assert.Equal(180m, invoice.GrandTotal);

        var sent = await finance.MarkSentAsync(invoice.Id);
        Assert.NotNull(sent);
        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);
        Assert.True(sent.AccountingVoucherId is > 0);

        var voucher = await db.AccountingVouchers
            .Include(v => v.Lines).ThenInclude(l => l.Ledger)
            .FirstAsync(v => v.Id == sent.AccountingVoucherId);
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Equal(180m, voucher.TotalDebit);
        Assert.Equal(180m, voucher.TotalCredit);
        Assert.DoesNotContain(voucher.Lines, l => l.Ledger!.SystemCode == SystemLedgerCodes.Sales);
    }

    private static async Task<Customer> SeedCustomerAsync(ShaunErpDbContext db)
    {
        var customer = new Customer
        {
            CompanyId = 1,
            CompanyName = "Acme Traders",
            CreatedDate = DateTime.UtcNow
        };
        db.CrmCustomers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static async Task<(ShaunErpDbContext db, FinanceService finance, AccountingService accounting)> CreateAsync()
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
                ModuleKeys.Accounting, ModuleKeys.Finance, ModuleKeys.Crm
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        return (db, finance, accounting);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
