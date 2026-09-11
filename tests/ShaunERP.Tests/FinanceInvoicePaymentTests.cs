using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class FinanceInvoicePaymentTests
{
    [Fact]
    public async Task UpdateInvoice_RejectsTotalBelowRecordedPayments()
    {
        var finance = await CreateFinanceAsync();

        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Coastal Retail",
            InvoiceDate = DateTime.UtcNow.Date,
            Lines =
            [
                new InvoiceLine { ProductName = "Consulting", Quantity = 10, UnitPrice = 100m }
            ]
        });

        Assert.Equal(1000m, created.GrandTotal);

        var sent = await finance.MarkSentAsync(created.Id);
        Assert.NotNull(sent);

        var (ok, error, _) = await finance.RecordPaymentAsync(created.Id, new InvoicePayment
        {
            Amount = 600m,
            PaymentDate = DateTime.UtcNow.Date,
            Method = "Bank transfer"
        });
        Assert.True(ok, error);
        var afterPay = await finance.GetInvoiceAsync(created.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, afterPay!.InvoiceStatus);
        Assert.Equal(600m, afterPay.AmountPaid);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => finance.UpdateInvoiceAsync(created.Id, new Invoice
        {
            CustomerName = "Coastal Retail",
            InvoiceDate = created.InvoiceDate,
            InvoiceStatus = InvoiceStatus.PartiallyPaid,
            Lines =
            [
                new InvoiceLine { ProductName = "Consulting", Quantity = 4, UnitPrice = 100m }
            ]
        }));
        Assert.Contains("already paid", ex.Message, StringComparison.OrdinalIgnoreCase);

        var unchanged = await finance.GetInvoiceAsync(created.Id);
        Assert.Equal(1000m, unchanged!.GrandTotal);
        Assert.Equal(600m, unchanged.AmountPaid);
        Assert.Equal(InvoiceStatus.PartiallyPaid, unchanged.InvoiceStatus);
        Assert.Equal(600m, unchanged.Payments.Sum(p => p.Amount));
    }

    [Fact]
    public async Task UpdateInvoice_AllowsEditsWhenTotalStillCoversPayments()
    {
        var finance = await CreateFinanceAsync();

        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "Coastal Retail",
            InvoiceDate = DateTime.UtcNow.Date,
            Lines =
            [
                new InvoiceLine { ProductName = "Consulting", Quantity = 10, UnitPrice = 100m }
            ]
        });

        await finance.MarkSentAsync(created.Id);
        var (ok, error, _) = await finance.RecordPaymentAsync(created.Id, new InvoicePayment
        {
            Amount = 600m,
            PaymentDate = DateTime.UtcNow.Date,
            Method = "Bank transfer"
        });
        Assert.True(ok, error);

        var updated = await finance.UpdateInvoiceAsync(created.Id, new Invoice
        {
            CustomerName = "Coastal Retail Group",
            InvoiceDate = created.InvoiceDate,
            InvoiceStatus = InvoiceStatus.PartiallyPaid,
            Notes = "Address correction",
            Lines =
            [
                new InvoiceLine { ProductName = "Consulting", Quantity = 12, UnitPrice = 100m }
            ]
        });

        Assert.NotNull(updated);
        Assert.Equal(1200m, updated!.GrandTotal);
        Assert.Equal(600m, updated.AmountPaid);
        Assert.Equal(InvoiceStatus.PartiallyPaid, updated.InvoiceStatus);
        Assert.Equal("Address correction", updated.Notes);
    }

    private static async Task<FinanceService> CreateFinanceAsync()
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
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Finance },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

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
