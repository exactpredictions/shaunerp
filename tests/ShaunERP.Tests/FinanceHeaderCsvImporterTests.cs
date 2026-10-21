using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class FinanceHeaderCsvImporterTests
{
    [Fact]
    public async Task Invoice_header_reimport_keeps_lines_totals_and_sent_status()
    {
        await using var db = CreateDb();
        SeedCustomer(db);
        db.Invoices.Add(new Invoice
        {
            Id = 40,
            CompanyId = 1,
            CompanyMasterId = 10,
            InvoiceNumber = "INV-1001",
            CustomerId = 10,
            CustomerName = "Acme Retail",
            InvoiceDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "INR",
            PaymentTerms = "Net 30",
            Notes = "Original notes",
            Discount = 50m,
            Subtotal = 1000m,
            TaxTotal = 180m,
            GrandTotal = 1130m,
            Amount = 1130m,
            InvoiceStatus = InvoiceStatus.Sent,
            Lines =
            [
                new InvoiceLine
                {
                    CompanyId = 1,
                    ProductName = "Consulting",
                    Quantity = 2,
                    UnitPrice = 500m,
                    TaxRate = 18m,
                    TaxAmount = 180m,
                    LineTotal = 1180m
                }
            ]
        });
        await db.SaveChangesAsync();

        var csv = """
            InvoiceNumber,CustomerAccountNumber,InvoiceDate,DueDate,CurrencyCode,PaymentTerms,Notes,GrandTotal
            INV-1001,CUST-ACME,2026-04-01,2026-05-15,INR,Net 30,Extended terms,1130.00
            """;
        var importer = new InvoicesCsvImporter(db, CreateFinance(db));
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), 1);
        Assert.Equal("Update", preview.Rows[0].Action);
        Assert.True(preview.CanApply);

        var result = await importer.ApplyAsync(preview, 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Updated);

        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .SingleAsync(i => i.Id == 40);
        Assert.Equal(InvoiceStatus.Sent, invoice.InvoiceStatus);
        Assert.Single(invoice.Lines);
        Assert.Equal("Consulting", invoice.Lines.First().ProductName);
        Assert.Equal(2m, invoice.Lines.First().Quantity);
        Assert.Equal(1130m, invoice.GrandTotal);
        Assert.Equal(50m, invoice.Discount);
        Assert.Equal(new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), invoice.DueDate);
        Assert.Equal("Extended terms", invoice.Notes);
        Assert.Equal("Net 30", invoice.PaymentTerms);
    }

    [Fact]
    public async Task Sales_order_header_reimport_keeps_lines_quote_link_and_totals()
    {
        await using var db = CreateDb();
        SeedCustomer(db);
        db.SalesOrders.Add(new SalesOrder
        {
            Id = 50,
            CompanyId = 1,
            OrderNumber = "SO-2001",
            CustomerId = 10,
            CustomerName = "Acme Retail",
            QuoteId = 7,
            OrderDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "INR",
            PaymentTerms = "Net 30",
            Notes = "Original order",
            Discount = 100m,
            Subtotal = 2000m,
            TaxTotal = 342m,
            GrandTotal = 2242m,
            AmountInvoiced = 500m,
            OrderStatus = SalesOrderStatus.Confirmed,
            Lines =
            [
                new SalesOrderLine
                {
                    CompanyId = 1,
                    ProductName = "Annual plan",
                    Quantity = 1,
                    UnitPrice = 2000m,
                    TaxRate = 18m,
                    TaxAmount = 342m,
                    LineTotal = 2242m
                }
            ]
        });
        await db.SaveChangesAsync();

        var csv = """
            OrderNumber,CustomerAccountNumber,OrderDate,CurrencyCode,PaymentTerms,Notes,GrandTotal
            SO-2001,CUST-ACME,2026-04-01,INR,Net 30,Rush delivery,2242.00
            """;
        var importer = new SalesOrdersCsvImporter(db, CreateOrders(db));
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), 1);
        Assert.Equal("Update", preview.Rows[0].Action);

        var result = await importer.ApplyAsync(preview, 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Updated);

        var order = await db.SalesOrders.AsNoTracking()
            .Include(o => o.Lines)
            .SingleAsync(o => o.Id == 50);
        Assert.Equal(SalesOrderStatus.Confirmed, order.OrderStatus);
        Assert.Equal(7, order.QuoteId);
        Assert.Single(order.Lines);
        Assert.Equal("Annual plan", order.Lines.First().ProductName);
        Assert.Equal(2242m, order.GrandTotal);
        Assert.Equal(100m, order.Discount);
        Assert.Equal(500m, order.AmountInvoiced);
        Assert.Equal("Rush delivery", order.Notes);
        Assert.Equal("Net 30", order.PaymentTerms);
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        db.CompanyMasters.Add(new CompanyMaster
        {
            Id = 10,
            TenantCompanyId = 1,
            Name = "Book A",
            InvoiceNumberPrefix = "INV-",
            NextInvoiceSequence = 1001
        });
        db.SaveChanges();
        return db;
    }

    private static void SeedCustomer(ShaunErpDbContext db)
    {
        db.CrmCustomers.Add(new Customer
        {
            Id = 10,
            CompanyId = 1,
            CompanyName = "Acme Retail",
            AccountNumber = "CUST-ACME"
        });
        db.SaveChanges();
    }

    private static FinanceService CreateFinance(ShaunErpDbContext db)
    {
        var tenant = CreateTenant();
        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        return new FinanceService(db, tenant, registry, posting, accounting);
    }

    private static SalesOrderService CreateOrders(ShaunErpDbContext db) =>
        new(db, CreateTenant(), new ModuleRegistry(), CreateFinance(db));

    private static TenantContext CreateTenant()
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleKeys.Crm, ModuleKeys.Finance, ModuleKeys.Orders
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        return tenant;
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
