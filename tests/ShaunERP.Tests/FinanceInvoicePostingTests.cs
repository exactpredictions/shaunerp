using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
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
    public async Task Recording_payment_on_a_draft_invoice_posts_the_sales_voucher()
    {
        var (db, posting, finance, accounting) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var invoice = await CreateDraftInvoiceAsync(finance, 2500m);

        var (ok, error, _) = await finance.RecordPaymentAsync(invoice.Id, new InvoicePayment
        {
            Amount = 2500m,
            PaymentDate = DateTime.UtcNow.Date,
            Method = "Bank transfer"
        });

        Assert.True(ok, error);
        var voucher = await posting.FindBySourceAsync(
            AccountingSourceModules.Sales, nameof(Invoice), invoice.Id.ToString());
        Assert.NotNull(voucher);
        Assert.Equal(VoucherStatus.Posted, voucher!.VoucherStatus);
        Assert.Equal(2500m, voucher.TotalDebit);
        Assert.Equal(2500m, voucher.TotalCredit);

        var paid = await finance.GetInvoiceAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.Paid, paid!.InvoiceStatus);
        Assert.Equal(voucher.Id, paid.AccountingVoucherId);
    }

    [Fact]
    public async Task Marking_a_draft_invoice_paid_posts_the_sales_voucher()
    {
        var (_, posting, finance, accounting) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var invoice = await CreateDraftInvoiceAsync(finance, 1800m);

        var paid = await finance.MarkPaidAsync(invoice.Id);
        Assert.NotNull(paid);
        Assert.Equal(InvoiceStatus.Paid, paid!.InvoiceStatus);

        var voucher = await posting.FindBySourceAsync(
            AccountingSourceModules.Sales, nameof(Invoice), invoice.Id.ToString());
        Assert.NotNull(voucher);
        Assert.Equal(1800m, voucher!.TotalDebit);
        Assert.Equal(voucher.Id, paid.AccountingVoucherId);
    }

    [Fact]
    public async Task Payment_after_mark_sent_does_not_duplicate_the_sales_voucher()
    {
        var (db, posting, finance, accounting) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var invoice = await CreateDraftInvoiceAsync(finance, 900m);

        var sent = await finance.MarkSentAsync(invoice.Id);
        Assert.NotNull(sent?.AccountingVoucherId);
        var first = await posting.FindBySourceAsync(
            AccountingSourceModules.Sales, nameof(Invoice), invoice.Id.ToString());

        var (ok, error, _) = await finance.RecordPaymentAsync(invoice.Id, new InvoicePayment
        {
            Amount = 400m,
            PaymentDate = DateTime.UtcNow.Date
        });
        Assert.True(ok, error);

        var again = await posting.FindBySourceAsync(
            AccountingSourceModules.Sales, nameof(Invoice), invoice.Id.ToString());
        Assert.Equal(first!.Id, again!.Id);

        var vouchers = await db.AccountingVouchers.CountAsync(v =>
            v.SourceModule == AccountingSourceModules.Sales
            && v.SourceEntityType == nameof(Invoice)
            && v.SourceEntityId == invoice.Id.ToString()
            && v.PostingType == "Primary");
        Assert.Equal(1, vouchers);
    }

    private static async Task<Invoice> CreateDraftInvoiceAsync(IFinanceService finance, decimal amount)
    {
        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Acme Manufacturing",
            InvoiceDate = DateTime.UtcNow.Date,
            InvoiceStatus = InvoiceStatus.Draft,
            Lines =
            [
                new InvoiceLine
                {
                    ProductName = "Consulting",
                    Quantity = 1,
                    UnitPrice = amount,
                    TaxRate = 0
                }
            ]
        });
        Assert.Equal(InvoiceStatus.Draft, created.InvoiceStatus);
        Assert.Null(created.AccountingVoucherId);
        return created;
    }

    private static async Task<(ShaunErpDbContext db, AccountingPostingService posting, FinanceService finance, AccountingService accounting)> CreateAsync()
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
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        return (db, posting, finance, accounting);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
