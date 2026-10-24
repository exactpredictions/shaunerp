using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;

namespace ShaunERP.Tests;

public class CompanyConfigurationTests
{
    [Theory]
    [InlineData("INV-", 4523, "INV-", 1001, 4523)]
    [InlineData("inv-", 2001, "INV-", 1001, 2001)]
    [InlineData("INV-", 1001, "INV-", 5000, 5000)]
    [InlineData("INV-", 4523, "INV26-", 1001, 1001)]
    [InlineData("INV-", 4523, "INV-", 0, 4523)]
    public void Same_prefix_never_moves_the_sequence_backward(
        string currentPrefix, int currentSequence, string proposedPrefix, int proposedSequence, int expected)
    {
        var actual = CompanyConfigurationService.NextMonotonicDocumentSequence(
            currentPrefix, currentSequence, proposedPrefix, proposedSequence);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Apply_India_template_keeps_live_invoice_and_order_sequences()
    {
        var (db, config, finance) = await CreateAsync();
        var book = await finance.GetOrCreateBillingBookAsync();
        book.InvoiceNumberPrefix = "INV-";
        book.NextInvoiceSequence = 4523;
        book.OrderNumberPrefix = "SO-";
        book.NextOrderSequence = 3100;
        await db.SaveChangesAsync();

        db.Invoices.Add(new Invoice
        {
            CompanyId = 1,
            CompanyMasterId = book.Id,
            InvoiceNumber = "INV-1001",
            CustomerName = "Existing customer",
            InvoiceDate = DateTime.UtcNow.Date,
            InvoiceStatus = InvoiceStatus.Sent,
            GrandTotal = 1000,
            Amount = 1000
        });
        await db.SaveChangesAsync();

        var result = await config.ApplyAsync(CompanyConfigurationService.BuildIndiaTemplate());
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var after = await finance.GetBillingBookAsync();
        Assert.NotNull(after);
        Assert.Equal("INV-", after.InvoiceNumberPrefix);
        Assert.Equal(4523, after.NextInvoiceSequence);
        Assert.Equal("SO-", after.OrderNumberPrefix);
        Assert.Equal(3100, after.NextOrderSequence);

        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            CustomerName = "New customer",
            InvoiceDate = DateTime.UtcNow.Date,
            Lines =
            [
                new InvoiceLine { ProductName = "Service", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            ]
        });
        Assert.Equal("INV-4523", created.InvoiceNumber);
        Assert.NotEqual("INV-1001", created.InvoiceNumber);
    }

    [Fact]
    public async Task Apply_allows_a_new_prefix_to_start_at_the_proposed_sequence()
    {
        var (db, config, finance) = await CreateAsync();
        var book = await finance.GetOrCreateBillingBookAsync();
        book.InvoiceNumberPrefix = "INV-";
        book.NextInvoiceSequence = 4523;
        book.OrderNumberPrefix = "SO-";
        book.NextOrderSequence = 3100;
        await db.SaveChangesAsync();

        var proposed = CompanyConfigurationService.BuildIndiaTemplate();
        proposed.Invoice.InvoiceNumberPrefix = "INV26-";
        proposed.Invoice.NextInvoiceSequence = 1001;
        proposed.Invoice.OrderNumberPrefix = "SO26-";
        proposed.Invoice.NextOrderSequence = 1;

        var result = await config.ApplyAsync(proposed);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var after = await finance.GetBillingBookAsync();
        Assert.NotNull(after);
        Assert.Equal("INV26-", after.InvoiceNumberPrefix);
        Assert.Equal(1001, after.NextInvoiceSequence);
        Assert.Equal("SO26-", after.OrderNumberPrefix);
        Assert.Equal(1, after.NextOrderSequence);
    }

    private static async Task<(ShaunErpDbContext db, CompanyConfigurationService config, FinanceService finance)> CreateAsync()
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
                ModuleKeys.Accounting, ModuleKeys.Finance, ModuleKeys.Settings
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var config = new CompanyConfigurationService(db, tenant, audit, accounting, finance);
        return (db, config, finance);
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
