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
    public async Task MarkSent_WithoutPostPermission_KeepsDraft_AndDoesNotPost()
    {
        var (db, finance, _) = await CreateAsync(Permissions.UserDefaults);

        var created = await finance.CreateInvoiceAsync(NewInvoice());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => finance.MarkSentAsync(created.Id));

        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == created.Id);
        Assert.Equal(InvoiceStatus.Draft, invoice.InvoiceStatus);
        Assert.Null(invoice.AccountingVoucherId);
        Assert.Empty(await db.AccountingVouchers.ToListAsync());
    }

    [Fact]
    public async Task MarkSent_WithPostPermission_IssuesInvoice_AndPostsSalesVoucher()
    {
        var (db, finance, _) = await CreateAsync(Permissions.AdminDefaults);

        var created = await finance.CreateInvoiceAsync(NewInvoice());
        var sent = await finance.MarkSentAsync(created.Id);

        Assert.NotNull(sent);
        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);
        Assert.True(sent.AccountingVoucherId > 0);

        var voucher = await db.AccountingVouchers.Include(v => v.Lines).SingleAsync();
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Equal(nameof(Invoice), voucher.SourceEntityType);
        Assert.Equal(created.Id.ToString(), voucher.SourceEntityId);
        Assert.Equal(sent.GrandTotal, voucher.TotalDebit);
    }

    [Fact]
    public async Task MarkSent_WhenAccountingDisabled_IssuesInvoice_WithoutVoucher()
    {
        var (db, finance, _) = await CreateAsync(Permissions.AdminDefaults, accountingEnabled: false);

        var created = await finance.CreateInvoiceAsync(NewInvoice());
        var sent = await finance.MarkSentAsync(created.Id);

        Assert.NotNull(sent);
        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);
        Assert.Null(sent.AccountingVoucherId);
        Assert.Empty(await db.AccountingVouchers.ToListAsync());
    }

    [Fact]
    public async Task Void_WithoutReversePermission_LeavesSentInvoice_AndKeepsVoucher()
    {
        var (db, adminFinance, tenant) = await CreateAsync(Permissions.AdminDefaults);
        var created = await adminFinance.CreateInvoiceAsync(NewInvoice());
        var sent = await adminFinance.MarkSentAsync(created.Id);
        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);

        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Shaun Infomedia OPC Pvt Ltd",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleKeys.Accounting, ModuleKeys.Finance
            },
            Permissions = new HashSet<string>(Permissions.UserDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var userFinance = NewFinance(db, tenant);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => userFinance.VoidInvoiceAsync(created.Id));

        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == created.Id);
        Assert.Equal(InvoiceStatus.Sent, invoice.InvoiceStatus);
        var voucher = await db.AccountingVouchers.SingleAsync();
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
    }

    [Fact]
    public async Task Void_WithReversePermission_VoidsInvoice_AndReversesVoucher()
    {
        var (db, finance, _) = await CreateAsync(Permissions.AdminDefaults);
        var created = await finance.CreateInvoiceAsync(NewInvoice());
        await finance.MarkSentAsync(created.Id);

        var voided = await finance.VoidInvoiceAsync(created.Id);
        Assert.Equal(InvoiceStatus.Void, voided!.InvoiceStatus);

        var original = await db.AccountingVouchers.SingleAsync(v => v.PostingType == "Primary");
        Assert.Equal(VoucherStatus.Reversed, original.VoucherStatus);
        Assert.Equal(2, await db.AccountingVouchers.CountAsync());
    }

    private static Invoice NewInvoice() => new()
    {
        CustomerName = "Acme",
        InvoiceDate = DateTime.UtcNow.Date,
        Lines =
        [
            new InvoiceLine
            {
                ProductName = "Retainer",
                Quantity = 1,
                UnitPrice = 1000m
            }
        ]
    };

    private static async Task<(ShaunErpDbContext db, FinanceService finance, TenantContext tenant)> CreateAsync(
        IReadOnlyList<string> permissions,
        bool accountingEnabled = true)
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Shaun Infomedia OPC Pvt Ltd" });
        await db.SaveChangesAsync();

        var entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Finance };
        if (accountingEnabled)
            entitlements.Add(ModuleKeys.Accounting);

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Shaun Infomedia OPC Pvt Ltd",
            Entitlements = entitlements,
            Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase)
        });

        return (db, NewFinance(db, tenant), tenant);
    }

    private static FinanceService NewFinance(ShaunErpDbContext db, TenantContext tenant)
    {
        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        return new FinanceService(db, tenant, registry, posting, accounting);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
