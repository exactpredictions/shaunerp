using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class FinanceInvoiceLifecycleTests
{
    [Fact]
    public async Task UpdateInvoice_RejectsIssuedInvoice()
    {
        var (finance, _) = await CreateAsync();
        var invoice = await finance.CreateInvoiceAsync(SampleInvoice(1000m));
        await finance.MarkSentAsync(invoice.Id);

        var update = CopyHeader(invoice);
        update.Lines =
        [
            new InvoiceLine
            {
                ProductName = "Consulting",
                Description = "Advisory",
                Quantity = 1,
                UnitPrice = 5000m
            }
        ];
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => finance.UpdateInvoiceAsync(invoice.Id, update));
        Assert.Contains("cannot be edited", ex.Message, StringComparison.OrdinalIgnoreCase);

        var loaded = await finance.GetInvoiceAsync(invoice.Id);
        Assert.Equal(1000m, loaded!.GrandTotal);
        Assert.Equal(InvoiceStatus.Sent, loaded.InvoiceStatus);
    }

    [Fact]
    public async Task UpdateInvoice_AllowsDraftEdits()
    {
        var (finance, _) = await CreateAsync();
        var invoice = await finance.CreateInvoiceAsync(SampleInvoice(1000m));

        var update = CopyHeader(invoice);
        update.Lines =
        [
            new InvoiceLine
            {
                ProductName = "Consulting",
                Description = "Advisory",
                Quantity = 2,
                UnitPrice = 250m
            }
        ];
        var updated = await finance.UpdateInvoiceAsync(invoice.Id, update);

        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.Draft, updated!.InvoiceStatus);
        Assert.Equal(500m, updated.GrandTotal);
    }

    [Fact]
    public async Task RecordPayment_OnDraft_IssuesInvoiceAndPostsSalesVoucher()
    {
        var (finance, db) = await CreateAsync();
        var invoice = await finance.CreateInvoiceAsync(SampleInvoice(1000m));
        Assert.Equal(InvoiceStatus.Draft, invoice.InvoiceStatus);
        Assert.True(invoice.AccountingVoucherId is null or 0);

        var (ok, error, payment) = await finance.RecordPaymentAsync(invoice.Id, new InvoicePayment
        {
            Amount = 1000m,
            PaymentDate = DateTime.UtcNow.Date,
            Method = "Bank transfer"
        });

        Assert.True(ok, error);
        Assert.NotNull(payment);

        var loaded = await finance.GetInvoiceAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.Paid, loaded!.InvoiceStatus);
        Assert.True(loaded.AccountingVoucherId > 0);

        var voucher = await db.AccountingVouchers.AsNoTracking()
            .Include(v => v.Lines)
            .FirstAsync(v => v.Id == loaded.AccountingVoucherId);
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Equal(1000m, voucher.TotalDebit);
        Assert.Equal(nameof(Invoice), voucher.SourceEntityType);
        Assert.Equal(invoice.Id.ToString(), voucher.SourceEntityId);
    }

    [Fact]
    public async Task MarkPaid_OnDraft_IssuesInvoiceBeforeSettling()
    {
        var (finance, db) = await CreateAsync();
        var invoice = await finance.CreateInvoiceAsync(SampleInvoice(400m));

        var paid = await finance.MarkPaidAsync(invoice.Id);
        Assert.NotNull(paid);
        Assert.Equal(InvoiceStatus.Paid, paid!.InvoiceStatus);
        Assert.True(paid.AccountingVoucherId > 0);
        Assert.Single(paid.Payments);

        var voucher = await db.AccountingVouchers.AsNoTracking()
            .FirstAsync(v => v.Id == paid.AccountingVoucherId);
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Equal(400m, voucher.TotalDebit);
    }

    [Fact]
    public async Task MarkSent_PostsBeforeLeavingDraft_AndDoesNotAllowAmountEdit()
    {
        var (finance, db) = await CreateAsync();
        var invoice = await finance.CreateInvoiceAsync(SampleInvoice(250m));
        var sent = await finance.MarkSentAsync(invoice.Id);

        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);
        Assert.True(sent.AccountingVoucherId > 0);
        Assert.Equal(1, await db.AccountingVouchers.CountAsync(v => v.SourceEntityId == invoice.Id.ToString()));

        var update = CopyHeader(sent);
        update.Discount = 50m;
        update.Lines =
        [
            new InvoiceLine
            {
                ProductName = "Consulting",
                Description = "Advisory",
                Quantity = 1,
                UnitPrice = 250m
            }
        ];
        await Assert.ThrowsAsync<InvalidOperationException>(() => finance.UpdateInvoiceAsync(sent.Id, update));
    }

    private static Invoice CopyHeader(Invoice source) => new()
    {
        CustomerName = source.CustomerName,
        InvoiceNumber = source.InvoiceNumber,
        InvoiceDate = source.InvoiceDate,
        DueDate = source.DueDate,
        CurrencyCode = source.CurrencyCode,
        PaymentTerms = source.PaymentTerms,
        Discount = source.Discount,
        InvoiceStatus = source.InvoiceStatus
    };

    private static Invoice SampleInvoice(decimal unitPrice) => new()
    {
        CustomerName = "Acme Manufacturing",
        InvoiceDate = DateTime.UtcNow.Date,
        CurrencyCode = "INR",
        Lines =
        [
            new InvoiceLine
            {
                ProductName = "Consulting",
                Description = "Advisory",
                Quantity = 1,
                UnitPrice = unitPrice
            }
        ]
    };

    private static async Task<(FinanceService finance, ShaunErpDbContext db)> CreateAsync()
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
        return (finance, db);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
