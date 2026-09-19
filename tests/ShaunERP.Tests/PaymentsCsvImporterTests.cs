using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class PaymentsCsvImporterTests
{
    [Fact]
    public async Task Reimport_of_same_payment_row_does_not_double_collect()
    {
        var (db, finance, importer) = await CreateAsync();
        await SeedInvoiceAsync(db, finance, "INV-1001", 1000m);

        var csv = PaymentCsv("INV-1001", "200.00", "2026-04-10", "UTR-001");
        var first = await ApplyCsvAsync(importer, csv);
        Assert.True(first.Success, string.Join("; ", first.Messages));
        Assert.Equal(1, first.Created);

        var second = await ApplyCsvAsync(importer, csv);
        Assert.True(second.Success, string.Join("; ", second.Messages));
        Assert.Equal(0, second.Created);
        Assert.True(second.Skipped >= 1);

        var invoice = await finance.GetInvoiceAsync(1);
        Assert.NotNull(invoice);
        Assert.Equal(200m, invoice!.AmountPaid);
        Assert.Single(invoice.Payments);
    }

    [Fact]
    public async Task Failed_row_rolls_back_payments_already_recorded_in_the_same_import()
    {
        var (db, finance, importer) = await CreateAsync();
        await SeedInvoiceAsync(db, finance, "INV-1001", 1000m);
        await SeedInvoiceAsync(db, finance, "INV-1002", 50m, invoiceId: 2);

        var csv = CsvParser.ToCsv(
            ["InvoiceNumber", "Amount", "PaymentDate", "Method", "Reference", "Notes"],
            [
                ["INV-1001", "200.00", "2026-04-10", "Bank transfer", "UTR-001", "ok"],
                ["INV-1002", "80.00", "2026-04-10", "Bank transfer", "UTR-002", "exceeds balance"]
            ]);

        var result = await ApplyCsvAsync(importer, csv);
        Assert.False(result.Success);
        Assert.Equal(0, result.Created);
        Assert.True(result.Failed >= 1);

        var first = await finance.GetInvoiceAsync(1);
        var second = await finance.GetInvoiceAsync(2);
        Assert.Equal(0m, first!.AmountPaid);
        Assert.Equal(0m, second!.AmountPaid);
        Assert.Empty(first.Payments);
        Assert.Empty(second.Payments);
    }

    private static async Task<CsvImportResult> ApplyCsvAsync(PaymentsCsvImporter importer, string csv)
    {
        var parsed = CsvParser.Parse(csv);
        Assert.True(parsed.Success, string.Join("; ", parsed.ParseErrors));
        var preview = await importer.PreviewAsync(parsed, companyId: 1);
        return await importer.ApplyAsync(preview, companyId: 1);
    }

    private static string PaymentCsv(string invoiceNumber, string amount, string date, string reference) =>
        CsvParser.ToCsv(
            ["InvoiceNumber", "Amount", "PaymentDate", "Method", "Reference", "Notes"],
            [[invoiceNumber, amount, date, "Bank transfer", reference, "import"]]);

    private static async Task SeedInvoiceAsync(
        ShaunErpDbContext db,
        FinanceService finance,
        string invoiceNumber,
        decimal total,
        int invoiceId = 1)
    {
        var created = await finance.CreateInvoiceAsync(new Invoice
        {
            InvoiceNumber = invoiceNumber,
            CustomerName = "Acme",
            InvoiceDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            InvoiceStatus = InvoiceStatus.Sent,
            Lines =
            [
                new InvoiceLine { ProductName = "Widget", Quantity = 1, UnitPrice = total }
            ]
        });
        Assert.True(created.Id > 0);
        Assert.Equal(invoiceId, created.Id);
        Assert.Equal(total, created.GrandTotal);
        await db.SaveChangesAsync();
    }

    private static async Task<(ShaunErpDbContext db, FinanceService finance, PaymentsCsvImporter importer)> CreateAsync()
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
        var importer = new PaymentsCsvImporter(db, finance);
        return (db, finance, importer);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
