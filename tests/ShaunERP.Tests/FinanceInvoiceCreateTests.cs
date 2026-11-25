using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class FinanceInvoiceCreateTests
{
    [Fact]
    public async Task CreateInvoice_IgnoresClientPaidStatusPaymentsAndVoucherLink()
    {
        var (db, finance) = await CreateAsync();

        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Harbor Logistics",
            InvoiceStatus = InvoiceStatus.Paid,
            IsPaid = true,
            AmountPaid = 1180,
            AccountingVoucherId = 50,
            Payments =
            {
                new InvoicePayment { Id = 9, Amount = 1180, Method = "Cash", Reference = "copied" }
            },
            Lines =
            {
                new InvoiceLine { ProductName = "Annual subscription", Quantity = 1, UnitPrice = 1000, TaxRate = 18 }
            }
        });

        Assert.Equal(InvoiceStatus.Draft, created.InvoiceStatus);
        Assert.False(created.IsPaid);
        Assert.Equal(0m, created.AmountPaid);
        Assert.Null(created.AccountingVoucherId);
        Assert.Empty(created.Payments);
        Assert.True(created.GrandTotal > 0);
        Assert.Empty(db.InvoicePayments.ToList());
    }

    [Fact]
    public async Task CreateInvoice_IgnoresClientAmountPaidOnDraftPayload()
    {
        var (_, finance) = await CreateAsync();

        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Harbor Logistics",
            InvoiceStatus = InvoiceStatus.Draft,
            AmountPaid = 9999,
            Lines =
            {
                new InvoiceLine { ProductName = "Services", Quantity = 1, UnitPrice = 500 }
            }
        });

        Assert.Equal(InvoiceStatus.Draft, created.InvoiceStatus);
        Assert.Equal(0m, created.AmountPaid);
        Assert.Equal(created.GrandTotal, created.BalanceDue);
    }

    private static async Task<(ShaunErpDbContext db, FinanceService finance)> CreateAsync()
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
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Finance, ModuleKeys.Accounting },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        return (db, finance);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
