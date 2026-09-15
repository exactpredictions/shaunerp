using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class FinancePaymentAccountingTests
{
    [Fact]
    public async Task RecordPayment_OnSentInvoice_PostsReceiptAndClearsAr()
    {
        var (db, finance, reports, accounting) = await CreateAsync();
        var invoice = await CreateAndSendInvoiceAsync(finance, 1000m);

        var (ok, error, payment) = await finance.RecordPaymentAsync(invoice.Id, new InvoicePayment
        {
            Amount = 1000m,
            PaymentDate = DateTime.UtcNow.Date,
            Method = "Bank transfer"
        });

        Assert.True(ok, error);
        Assert.NotNull(payment);

        var receipt = await db.AccountingVouchers
            .Include(v => v.Lines).ThenInclude(l => l.Ledger)
            .Include(v => v.VoucherType)
            .SingleAsync(v => v.SourceEntityType == nameof(InvoicePayment) && v.SourceEntityId == payment!.Id.ToString());
        Assert.Equal(SystemVoucherTypeCodes.Receipt, receipt.VoucherType!.Code);
        Assert.Equal(VoucherStatus.Posted, receipt.VoucherStatus);
        Assert.Equal(1000m, receipt.TotalDebit);
        Assert.Equal(1000m, receipt.Lines.Single(l => l.Ledger!.SystemCode == SystemLedgerCodes.BankDefault).DebitAmount);
        Assert.Equal(1000m, receipt.Lines.Single(l => l.Ledger!.SystemCode == SystemLedgerCodes.AccountsReceivable).CreditAmount);

        var dashboard = await accounting.GetDashboardAsync();
        Assert.Equal(0m, dashboard.Receivables);
        Assert.Equal(1000m, dashboard.BankBalance);

        var tb = await reports.GetTrialBalanceAsync(null, null);
        Assert.True(tb.IsBalanced);
    }

    [Fact]
    public async Task CashPayment_DebitsCashLedger_AndDeleteReversesReceipt()
    {
        var (db, finance, _, _) = await CreateAsync();
        var invoice = await CreateAndSendInvoiceAsync(finance, 250m);

        var (ok, error, payment) = await finance.RecordPaymentAsync(invoice.Id, new InvoicePayment
        {
            Amount = 250m,
            PaymentDate = DateTime.UtcNow.Date,
            Method = "Cash"
        });
        Assert.True(ok, error);

        var receipt = await db.AccountingVouchers
            .Include(v => v.Lines).ThenInclude(l => l.Ledger)
            .SingleAsync(v => v.SourceEntityType == nameof(InvoicePayment) && v.SourceEntityId == payment!.Id.ToString());
        Assert.Equal(250m, receipt.Lines.Single(l => l.Ledger!.SystemCode == SystemLedgerCodes.Cash).DebitAmount);

        Assert.True(await finance.DeletePaymentAsync(payment!.Id));

        await db.Entry(receipt).ReloadAsync();
        Assert.Equal(VoucherStatus.Reversed, receipt.VoucherStatus);
        Assert.NotNull(receipt.ReversedByVoucherId);

        var refreshed = await finance.GetInvoiceAsync(invoice.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(InvoiceStatus.Sent, refreshed!.InvoiceStatus);
        Assert.Equal(0m, refreshed.AmountPaid);
    }

    [Fact]
    public async Task MarkPaid_PostsReceipt_ForRemainingBalance()
    {
        var (db, finance, _, accounting) = await CreateAsync();
        var invoice = await CreateAndSendInvoiceAsync(finance, 400m);

        var paid = await finance.MarkPaidAsync(invoice.Id);
        Assert.NotNull(paid);
        Assert.Equal(InvoiceStatus.Paid, paid!.InvoiceStatus);

        var payment = Assert.Single(await db.InvoicePayments.Where(p => p.InvoiceId == invoice.Id).ToListAsync());
        var receipt = await db.AccountingVouchers
            .Include(v => v.VoucherType)
            .SingleOrDefaultAsync(v => v.SourceEntityType == nameof(InvoicePayment) && v.SourceEntityId == payment.Id.ToString());
        Assert.NotNull(receipt);
        Assert.Equal(SystemVoucherTypeCodes.Receipt, receipt!.VoucherType!.Code);
        Assert.Equal(400m, receipt.TotalDebit);

        var dashboard = await accounting.GetDashboardAsync();
        Assert.Equal(0m, dashboard.Receivables);
    }

    [Fact]
    public async Task RecordPayment_DoesNotPostReceipt_WhenSalesWasNotPosted()
    {
        var (db, finance, _, accounting) = await CreateAsync();
        var settings = await accounting.GetOrCreateSettingsAsync();
        settings.AutoPostSalesInvoice = false;
        await accounting.SaveSettingsAsync(settings);

        var invoice = await CreateAndSendInvoiceAsync(finance, 100m);
        Assert.Null(invoice.AccountingVoucherId);

        var (ok, error, payment) = await finance.RecordPaymentAsync(invoice.Id, new InvoicePayment
        {
            Amount = 100m,
            PaymentDate = DateTime.UtcNow.Date,
            Method = "Bank transfer"
        });
        Assert.True(ok, error);
        Assert.False(await db.AccountingVouchers.AnyAsync(
            v => v.SourceEntityType == nameof(InvoicePayment) && v.SourceEntityId == payment!.Id.ToString()));
    }

    private static async Task<Invoice> CreateAndSendInvoiceAsync(FinanceService finance, decimal amount)
    {
        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Coastal Retail",
            InvoiceDate = DateTime.UtcNow.Date,
            Lines =
            [
                new InvoiceLine
                {
                    ProductName = "Retainer",
                    Quantity = 1,
                    UnitPrice = amount
                }
            ]
        });

        var sent = await finance.MarkSentAsync(created.Id);
        Assert.NotNull(sent);
        return sent!;
    }

    private static async Task<(ShaunErpDbContext db, FinanceService finance, AccountingReportService reports, AccountingService accounting)> CreateAsync()
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
                ModuleKeys.Accounting, ModuleKeys.Finance
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var reports = new AccountingReportService(db, tenant, registry);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        await accounting.EnsureBootstrapAsync();
        return (db, finance, reports, accounting);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
