using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class InvoiceVoidAccountingTests
{
    [Fact]
    public async Task Void_Reverses_Posted_Sales_Voucher()
    {
        var (db, finance, accounting) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var invoice = await CreateAndSendInvoiceAsync(finance);

        Assert.Equal(InvoiceStatus.Sent, invoice.InvoiceStatus);
        Assert.True(invoice.AccountingVoucherId is > 0);
        var voucherId = invoice.AccountingVoucherId!.Value;

        var voided = await finance.VoidInvoiceAsync(invoice.Id);
        Assert.NotNull(voided);
        Assert.Equal(InvoiceStatus.Void, voided!.InvoiceStatus);

        var original = await db.AccountingVouchers.FirstAsync(v => v.Id == voucherId);
        Assert.Equal(VoucherStatus.Reversed, original.VoucherStatus);
        Assert.NotNull(original.ReversedByVoucherId);
    }

    [Fact]
    public async Task Void_After_Year_Close_Leaves_Invoice_Sent_And_Sales_GL_Posted()
    {
        var (db, finance, accounting) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var invoice = await CreateAndSendInvoiceAsync(finance);
        var voucherId = invoice.AccountingVoucherId!.Value;

        var fy = await db.FinancialYears.SingleAsync(y => y.CompanyId == 1);
        await accounting.CloseFinancialYearAsync(fy.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => finance.VoidInvoiceAsync(invoice.Id));
        Assert.Contains("closed", ex.Message, StringComparison.OrdinalIgnoreCase);

        var reloaded = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Sent, reloaded.InvoiceStatus);

        var voucher = await db.AccountingVouchers.FirstAsync(v => v.Id == voucherId);
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Null(voucher.ReversedByVoucherId);
    }

    [Fact]
    public async Task Void_During_Period_Lock_Leaves_Invoice_Sent_And_Sales_GL_Posted()
    {
        var (db, finance, accounting) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var invoice = await CreateAndSendInvoiceAsync(finance);
        var voucherId = invoice.AccountingVoucherId!.Value;

        await accounting.SetPeriodLockAsync(DateTime.UtcNow.Date.AddDays(1), "month close");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => finance.VoidInvoiceAsync(invoice.Id));
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);

        var reloaded = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Sent, reloaded.InvoiceStatus);

        var voucher = await db.AccountingVouchers.FirstAsync(v => v.Id == voucherId);
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Null(voucher.ReversedByVoucherId);
    }

    private static async Task<Invoice> CreateAndSendInvoiceAsync(FinanceService finance)
    {
        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Acme Retail",
            InvoiceDate = DateTime.UtcNow.Date,
            Lines =
            [
                new InvoiceLine { ProductName = "Consulting", Quantity = 1, UnitPrice = 1000m }
            ]
        });
        var sent = await finance.MarkSentAsync(created.Id);
        Assert.NotNull(sent);
        return sent!;
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
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Accounting, ModuleKeys.Finance },
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
