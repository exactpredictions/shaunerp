using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class FinanceInvoiceRepostTests
{
    [Fact]
    public async Task MarkSent_AfterAccountingReverse_PostsANewSalesVoucher()
    {
        var (db, posting, accounting, finance) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();

        var invoice = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Acme",
            InvoiceDate = DateTime.UtcNow.Date,
            Lines =
            [
                new InvoiceLine
                {
                    ProductName = "Consulting",
                    Quantity = 1,
                    UnitPrice = 1000m
                }
            ]
        });

        var sent = await finance.MarkSentAsync(invoice.Id);
        Assert.NotNull(sent);
        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);
        Assert.True(sent.AccountingVoucherId is > 0);

        var originalId = sent.AccountingVoucherId!.Value;
        var original = await db.AccountingVouchers.FirstAsync(v => v.Id == originalId);
        Assert.Equal(VoucherStatus.Posted, original.VoucherStatus);
        Assert.Equal(1000m, original.TotalDebit);

        await posting.ReverseAsync(originalId, "Wrong amount");

        original = await db.AccountingVouchers.FirstAsync(v => v.Id == originalId);
        Assert.Equal(VoucherStatus.Reversed, original.VoucherStatus);

        var retried = await finance.MarkSentAsync(invoice.Id);
        Assert.NotNull(retried);
        Assert.Equal(InvoiceStatus.Sent, retried!.InvoiceStatus);
        Assert.True(retried.AccountingVoucherId is > 0);
        Assert.NotEqual(originalId, retried.AccountingVoucherId);

        var replacement = await db.AccountingVouchers
            .Include(v => v.Lines)
            .FirstAsync(v => v.Id == retried.AccountingVoucherId);
        Assert.Equal(VoucherStatus.Posted, replacement.VoucherStatus);
        Assert.Equal("Primary", replacement.PostingType);
        Assert.Equal(nameof(Invoice), replacement.SourceEntityType);
        Assert.Equal(invoice.Id.ToString(), replacement.SourceEntityId);
        Assert.Equal(1000m, replacement.TotalDebit);
        Assert.Equal(1000m, replacement.TotalCredit);
    }

    [Fact]
    public async Task MarkSent_WhenLinkedVoucherStillPosted_DoesNotDuplicate()
    {
        var (_, _, accounting, finance) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();

        var invoice = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Acme",
            InvoiceDate = DateTime.UtcNow.Date,
            Lines =
            [
                new InvoiceLine
                {
                    ProductName = "Consulting",
                    Quantity = 1,
                    UnitPrice = 250m
                }
            ]
        });

        var sent = await finance.MarkSentAsync(invoice.Id);
        var firstVoucherId = sent!.AccountingVoucherId;

        var again = await finance.MarkSentAsync(invoice.Id);
        Assert.Equal(firstVoucherId, again!.AccountingVoucherId);
    }

    private static async Task<(ShaunErpDbContext db, AccountingPostingService posting, AccountingService accounting, FinanceService finance)> CreateAsync()
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
        return (db, posting, accounting, finance);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(
            string action,
            int? companyId = null,
            int? userId = null,
            string? entityType = null,
            string? entityId = null,
            string? beforeValues = null,
            string? afterValues = null,
            string? correlationId = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
