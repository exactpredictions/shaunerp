using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class SalesOrderQuoteConversionTests
{
    [Fact]
    public async Task CreateFromQuote_preserves_accepted_quote_money()
    {
        var (orders, db) = await CreateAsync();

        db.CrmCustomers.Add(new Customer
        {
            Id = 7,
            CompanyId = 1,
            CompanyName = "Acme Traders",
            Status = "Active"
        });
        db.Quotes.Add(new Quote
        {
            Id = 3,
            CompanyId = 1,
            QuoteNumber = "Q-1001",
            Subject = "Training pack",
            QuoteStage = "Accepted",
            CustomerId = 7,
            CurrencyCode = "INR",
            Discount = 20,
            Tax = 72,
            Subtotal = 400,
            GrandTotal = 452,
            Lines =
            [
                new QuoteLine
                {
                    CompanyId = 1,
                    ProductName = "Workshop",
                    Quantity = 10,
                    UnitPrice = 50,
                    Discount = 100, // absolute ₹100 off, not 100%
                    LineTotal = 400
                }
            ]
        });
        await db.SaveChangesAsync();

        var order = await orders.CreateFromQuoteAsync(3);
        Assert.NotNull(order);
        var line = Assert.Single(order.Lines);
        Assert.Equal(20m, line.Discount);
        Assert.Equal(18m, line.TaxRate);
        Assert.Equal(400m, order.Subtotal);
        Assert.Equal(20m, order.Discount);
        Assert.Equal(72m, order.TaxTotal);
        Assert.Equal(452m, order.GrandTotal);
        Assert.Equal(3, order.QuoteId);
        Assert.Equal(7, order.CustomerId);
    }

    [Fact]
    public async Task CreateFromQuote_does_not_zero_a_line_with_absolute_discount_of_100()
    {
        var (orders, db) = await CreateAsync();

        db.CrmCustomers.Add(new Customer
        {
            Id = 8,
            CompanyId = 1,
            CompanyName = "Beta LLC",
            Status = "Active"
        });
        db.Quotes.Add(new Quote
        {
            Id = 4,
            CompanyId = 1,
            QuoteNumber = "Q-1002",
            Subject = "Consulting",
            QuoteStage = "Accepted",
            CustomerId = 8,
            CurrencyCode = "INR",
            Subtotal = 1900,
            GrandTotal = 1900,
            Lines =
            [
                new QuoteLine
                {
                    CompanyId = 1,
                    ProductName = "Day rate",
                    Quantity = 2,
                    UnitPrice = 1000,
                    Discount = 100,
                    LineTotal = 1900
                }
            ]
        });
        await db.SaveChangesAsync();

        var order = await orders.CreateFromQuoteAsync(4);
        Assert.NotNull(order);
        var line = Assert.Single(order.Lines);
        Assert.Equal(5m, line.Discount);
        Assert.Equal(1900m, order.GrandTotal);
        Assert.True(line.LineTotal > 0);
    }

    private static async Task<(SalesOrderService orders, ShaunErpDbContext db)> CreateAsync()
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
                ModuleKeys.Orders, ModuleKeys.Finance, ModuleKeys.Accounting
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var orders = new SalesOrderService(db, tenant, registry, finance);
        return (orders, db);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
