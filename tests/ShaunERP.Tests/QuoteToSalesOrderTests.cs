using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class QuoteToSalesOrderTests
{
    [Fact]
    public async Task CreateFromQuote_ConvertsAbsoluteLineDiscountToPercent()
    {
        var (orders, db) = await CreateAsync();

        var quote = await SeedAcceptedQuoteAsync(db, lineDiscount: 100m, headerDiscount: 0m, tax: 0m);

        var order = await orders.CreateFromQuoteAsync(quote.Id);

        Assert.NotNull(order);
        var line = Assert.Single(order.Lines);
        Assert.Equal(1m, line.Discount);
        Assert.Equal(9900m, line.LineTotal);
        Assert.Equal(9900m, order.Subtotal);
        Assert.Equal(9900m, order.GrandTotal);
    }

    [Fact]
    public async Task CreateFromQuote_PreservesHeaderDiscountAndTaxOnGrandTotal()
    {
        var (orders, db) = await CreateAsync();

        // 10 × 1000 − ₹100 line discount = 9900; header discount 500; tax 1782.
        var quote = await SeedAcceptedQuoteAsync(db, lineDiscount: 100m, headerDiscount: 500m, tax: 1782m);

        var order = await orders.CreateFromQuoteAsync(quote.Id);

        Assert.NotNull(order);
        Assert.Equal(9900m, order.Subtotal);
        Assert.Equal(500m, order.Discount);
        Assert.Equal(1782m, order.TaxTotal);
        Assert.Equal(quote.GrandTotal, order.GrandTotal);
        Assert.Equal(11182m, order.GrandTotal);
    }

    [Fact]
    public async Task CreateFromQuote_DoesNotTreatFullCurrencyDiscountAsHundredPercentUnlessItIs()
    {
        var (orders, db) = await CreateAsync();

        var quote = await SeedAcceptedQuoteAsync(db, lineDiscount: 10000m, headerDiscount: 0m, tax: 0m);

        var order = await orders.CreateFromQuoteAsync(quote.Id);

        Assert.NotNull(order);
        var line = Assert.Single(order.Lines);
        Assert.Equal(100m, line.Discount);
        Assert.Equal(0m, order.GrandTotal);
    }

    private static async Task<Quote> SeedAcceptedQuoteAsync(
        ShaunErpDbContext db, decimal lineDiscount, decimal headerDiscount, decimal tax)
    {
        const decimal qty = 10m;
        const decimal unitPrice = 1000m;
        var lineTotal = Math.Max(0, qty * unitPrice - lineDiscount);
        var quote = new Quote
        {
            CompanyId = 1,
            QuoteNumber = "Q-TEST-1",
            Subject = "Consulting",
            QuoteStage = "Accepted",
            CustomerId = 1,
            CurrencyCode = "INR",
            Discount = headerDiscount,
            Tax = tax,
            Subtotal = lineTotal,
            GrandTotal = Math.Max(0, lineTotal - headerDiscount + tax),
            Lines =
            [
                new QuoteLine
                {
                    CompanyId = 1,
                    ProductName = "Retainer",
                    Quantity = qty,
                    UnitPrice = unitPrice,
                    Discount = lineDiscount,
                    LineTotal = lineTotal,
                    SortOrder = 0
                }
            ]
        };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        return quote;
    }

    private static async Task<(SalesOrderService orders, ShaunErpDbContext db)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Shaun Infomedia OPC Pvt Ltd" });
        db.CrmCustomers.Add(new Customer { Id = 1, CompanyId = 1, CompanyName = "Acme" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Shaun Infomedia OPC Pvt Ltd",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleKeys.Orders, ModuleKeys.Finance, ModuleKeys.Accounting, ModuleKeys.Crm
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
