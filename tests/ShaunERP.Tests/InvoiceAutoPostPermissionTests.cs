using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class InvoiceAutoPostPermissionTests
{
    [Fact]
    public async Task MarkSent_WithUserRole_PostsSalesVoucher()
    {
        var (db, _, finance) = await CreateUserRoleAsync();

        var created = await finance.CreateInvoiceAsync(NewInvoice(1000m));
        var sent = await finance.MarkSentAsync(created.Id);

        Assert.NotNull(sent);
        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);
        Assert.True(sent.AccountingVoucherId is > 0);

        var voucher = await db.AccountingVouchers.Include(v => v.Lines)
            .FirstAsync(v => v.Id == sent.AccountingVoucherId);
        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Equal(1000m, voucher.TotalDebit);
        Assert.Equal(AccountingSourceModules.Sales, voucher.SourceModule);
    }

    [Fact]
    public async Task Void_WithUserRole_ReversesPostedSalesVoucher()
    {
        var (db, _, finance) = await CreateUserRoleAsync();

        var created = await finance.CreateInvoiceAsync(NewInvoice(250m));
        var sent = await finance.MarkSentAsync(created.Id);
        Assert.True(sent!.AccountingVoucherId is > 0);

        var voided = await finance.VoidInvoiceAsync(sent.Id);
        Assert.Equal(InvoiceStatus.Void, voided!.InvoiceStatus);

        var original = await db.AccountingVouchers.FirstAsync(v => v.Id == sent.AccountingVoucherId);
        Assert.Equal(VoucherStatus.Reversed, original.VoucherStatus);
        Assert.NotNull(original.ReversedByVoucherId);

        var reversal = await db.AccountingVouchers.FirstAsync(v => v.Id == original.ReversedByVoucherId);
        Assert.Equal(VoucherStatus.Posted, reversal.VoucherStatus);
        Assert.Equal(250m, reversal.TotalDebit);
    }

    [Fact]
    public async Task ManualPost_WithUserRole_IsStillDenied()
    {
        var (db, tenant, finance) = await CreateUserRoleAsync();
        await finance.GetOrCreateBillingBookAsync();

        var registry = new ModuleRegistry();
        var posting = new AccountingPostingService(db, tenant, registry, new NoopAudit());
        await new AccountingService(db, tenant, registry, posting, new NoopAudit()).EnsureBootstrapAsync();

        var cash = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.Cash);
        var sales = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.Sales);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Journal,
            Lines =
            [
                new() { LedgerId = cash.Id, Debit = 10 },
                new() { LedgerId = sales.Id, Credit = 10 }
            ]
        }));
    }

    private static Invoice NewInvoice(decimal amount) => new()
    {
        CustomerName = "Acme Co",
        InvoiceDate = DateTime.UtcNow.Date,
        Lines =
        [
            new InvoiceLine { ProductName = "Consulting", Quantity = 1, UnitPrice = amount }
        ]
    };

    private static async Task<(ShaunErpDbContext db, TenantContext tenant, FinanceService finance)> CreateUserRoleAsync()
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
            UserId = 7,
            CompanyId = 1,
            CompanyName = "Shaun Infomedia OPC Pvt Ltd",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleKeys.Accounting, ModuleKeys.Finance
            },
            Permissions = new HashSet<string>(Permissions.UserDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        return (db, tenant, finance);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
