using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class ReceivablesDraftTests
{
    [Fact]
    public async Task Receivables_ExcludeDraftInvoices()
    {
        var (finance, reports, _) = await CreateAsync();
        await SeedInvoicesAsync(finance);

        var ar = await reports.GetReceivablesAsync();

        Assert.Single(ar);
        Assert.Equal("INV-SENT", ar[0].InvoiceNumber);
        Assert.Equal(1000m, ar[0].Amount);
        Assert.DoesNotContain(ar, r => r.InvoiceNumber == "INV-DRAFT");
        Assert.DoesNotContain(ar, r => r.InvoiceNumber == "INV-PAID");
        Assert.DoesNotContain(ar, r => r.InvoiceNumber == "INV-VOID");
    }

    [Fact]
    public async Task FinanceDashboard_OutstandingAndInvoiced_ExcludeDrafts()
    {
        var (finance, _, _) = await CreateAsync();
        await SeedInvoicesAsync(finance);

        var stats = await finance.GetDashboardAsync();

        Assert.Equal(1, stats.DraftInvoices);
        Assert.Equal(1, stats.SentInvoices);
        Assert.Equal(1000m, stats.OutstandingBalance);
        Assert.Equal(3500m, stats.InvoicedThisMonth);
    }

    private static async Task SeedInvoicesAsync(FinanceService finance)
    {
        await finance.CreateInvoiceAsync(Sample("INV-DRAFT", 5000m, InvoiceStatus.Draft));

        var sent = await finance.CreateInvoiceAsync(Sample("INV-SENT", 1000m, InvoiceStatus.Draft));
        await finance.MarkSentAsync(sent.Id);

        var paid = await finance.CreateInvoiceAsync(Sample("INV-PAID", 2500m, InvoiceStatus.Draft));
        await finance.MarkSentAsync(paid.Id);
        await finance.MarkPaidAsync(paid.Id);

        var voided = await finance.CreateInvoiceAsync(Sample("INV-VOID", 800m, InvoiceStatus.Draft));
        await finance.MarkSentAsync(voided.Id);
        await finance.VoidInvoiceAsync(voided.Id);
    }

    private static Invoice Sample(string number, decimal amount, InvoiceStatus status) => new()
    {
        CustomerName = "Acme Manufacturing",
        InvoiceNumber = number,
        InvoiceDate = DateTime.UtcNow.Date,
        DueDate = DateTime.UtcNow.Date.AddDays(15),
        CurrencyCode = "INR",
        InvoiceStatus = status,
        Lines =
        [
            new InvoiceLine
            {
                ProductName = "Consulting",
                Description = "Advisory",
                Quantity = 1,
                UnitPrice = amount
            }
        ]
    };

    private static async Task<(FinanceService finance, AccountingReportService reports, ShaunErpDbContext db)> CreateAsync()
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
        var reports = new AccountingReportService(db, tenant, registry);
        return (finance, reports, db);
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
