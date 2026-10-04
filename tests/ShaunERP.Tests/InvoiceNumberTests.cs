using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class InvoiceNumberTests
{
    [Fact]
    public async Task CreateInvoice_RejectsDuplicateNumber_CaseInsensitive()
    {
        var finance = await CreateFinanceAsync();

        await finance.Service.CreateInvoiceAsync(Invoice("INV-1001", 100));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => finance.Service.CreateInvoiceAsync(Invoice("inv-1001", 50)));

        Assert.Contains("already used", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await finance.Db.Invoices.CountAsync());
    }

    [Fact]
    public async Task AutoNumber_SkipsManuallyUsedSequence()
    {
        var finance = await CreateFinanceAsync();

        await finance.Service.CreateInvoiceAsync(Invoice("INV-1001", 100));
        var second = await finance.Service.CreateInvoiceAsync(Invoice(null, 80));

        Assert.Equal("INV-1002", second.InvoiceNumber);
        Assert.Equal(2, await finance.Db.Invoices.Select(i => i.InvoiceNumber).Distinct().CountAsync());
    }

    [Fact]
    public async Task SameNumber_IsAllowedInAnotherTenant()
    {
        var first = await CreateFinanceAsync(companyId: 1);
        await first.Service.CreateInvoiceAsync(Invoice("INV-1001", 100));

        var second = await CreateFinanceAsync(companyId: 2, db: first.Db);
        var other = await second.Service.CreateInvoiceAsync(Invoice("INV-1001", 40));

        Assert.Equal("INV-1001", other.InvoiceNumber);
        Assert.Equal(2, other.CompanyId);
    }

    [Fact]
    public async Task PaymentsCsvPreview_DoesNotCrash_WhenInvoiceNumbersCollide()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        db.Invoices.AddRange(
            new Invoice { CompanyId = 1, CompanyMasterId = 1, InvoiceNumber = "INV-1001", CustomerName = "One", GrandTotal = 100, Amount = 100 },
            new Invoice { CompanyId = 1, CompanyMasterId = 1, InvoiceNumber = "INV-1001", CustomerName = "Two", GrandTotal = 80, Amount = 80 });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(AdminSnapshot(1));
        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var importer = new PaymentsCsvImporter(db, finance);

        var parsed = CsvParser.Parse("InvoiceNumber,Amount,PaymentDate\nINV-1001,10,2026-04-10\n");
        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Rows, r => r.Errors.Any(e => e.Contains("duplicated", StringComparison.OrdinalIgnoreCase)));
    }

    private static Invoice Invoice(string? number, decimal unitPrice) => new()
    {
        InvoiceNumber = number ?? "",
        CustomerName = "Acme",
        InvoiceDate = DateTime.UtcNow.Date,
        CurrencyCode = "USD",
        Lines =
        [
            new InvoiceLine { ProductName = "Service", Quantity = 1, UnitPrice = unitPrice }
        ]
    };

    private static async Task<(ShaunErpDbContext Db, FinanceService Service)> CreateFinanceAsync(
        int companyId = 1, ShaunErpDbContext? db = null)
    {
        if (db is null)
        {
            var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            db = new ShaunErpDbContext(options);
            db.Companies.AddRange(new Company { Id = 1, Name = "A" }, new Company { Id = 2, Name = "B" });
            await db.SaveChangesAsync();
        }

        var tenant = new TenantContext();
        tenant.Set(AdminSnapshot(companyId));
        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        return (db, new FinanceService(db, tenant, registry, posting, accounting));
    }

    private static TenantContextSnapshot AdminSnapshot(int companyId) => new()
    {
        UserId = 1,
        CompanyId = companyId,
        CompanyName = "A",
        Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ModuleKeys.Finance, ModuleKeys.Crm, ModuleKeys.Accounting
        },
        Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
    };

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null,
            string? entityId = null, string? beforeValues = null, string? afterValues = null,
            string? correlationId = null, string? ipAddress = null, string? userAgent = null,
            CancellationToken ct = default) => Task.CompletedTask;
    }
}
