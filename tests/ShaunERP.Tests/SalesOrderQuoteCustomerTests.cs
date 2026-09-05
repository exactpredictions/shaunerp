using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class SalesOrderQuoteCustomerTests
{
    [Fact]
    public async Task CreateFromQuote_creates_customer_from_account_name_when_quote_has_no_customer()
    {
        var (orders, db) = await CreateAsync();
        db.Quotes.Add(UiQuote(id: 11, accountName: "Acme Traders"));
        await db.SaveChangesAsync();

        var order = await orders.CreateFromQuoteAsync(11);

        Assert.NotNull(order);
        Assert.True(order.CustomerId > 0);
        var customer = await db.CrmCustomers.SingleAsync(c => c.Id == order.CustomerId);
        Assert.Equal("Acme Traders", customer.CompanyName);
        Assert.Equal(1, customer.CompanyId);
        Assert.Equal(order.CustomerId, (await db.Quotes.SingleAsync(q => q.Id == 11)).CustomerId);
    }

    [Fact]
    public async Task CreateFromQuote_reuses_existing_customer_with_same_account_name()
    {
        var (orders, db) = await CreateAsync();
        db.CrmCustomers.Add(new Customer
        {
            Id = 22,
            CompanyId = 1,
            CompanyName = "Acme Traders",
            Status = "Active"
        });
        db.Quotes.Add(UiQuote(id: 12, accountName: "acme traders"));
        await db.SaveChangesAsync();

        var order = await orders.CreateFromQuoteAsync(12);

        Assert.NotNull(order);
        Assert.Equal(22, order.CustomerId);
        Assert.Equal(1, await db.CrmCustomers.CountAsync());
    }

    [Fact]
    public async Task CreateFromQuote_keeps_explicit_customer_id()
    {
        var (orders, db) = await CreateAsync();
        db.CrmCustomers.Add(new Customer
        {
            Id = 33,
            CompanyId = 1,
            CompanyName = "Beta LLC",
            Status = "Active"
        });
        var quote = UiQuote(id: 13, accountName: "Ignored Name");
        quote.CustomerId = 33;
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();

        var order = await orders.CreateFromQuoteAsync(13);

        Assert.NotNull(order);
        Assert.Equal(33, order.CustomerId);
        Assert.Equal(1, await db.CrmCustomers.CountAsync());
    }

    [Fact]
    public async Task CreateFromQuote_uses_won_opportunity_customer()
    {
        var (orders, db) = await CreateAsync();
        db.CrmCustomers.Add(new Customer
        {
            Id = 44,
            CompanyId = 1,
            CompanyName = "Won Co",
            Status = "Active"
        });
        db.Opportunities.Add(new Opportunity
        {
            Id = 5,
            CompanyId = 1,
            DealName = "Enterprise",
            CustomerId = 44,
            Stage = "Closed Won"
        });
        var quote = UiQuote(id: 14, accountName: null);
        quote.OpportunityId = 5;
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();

        var order = await orders.CreateFromQuoteAsync(14);

        Assert.NotNull(order);
        Assert.Equal(44, order.CustomerId);
        Assert.Equal(1, await db.CrmCustomers.CountAsync());
    }

    [Fact]
    public async Task CreateFromQuote_rejects_quote_without_any_customer_identity()
    {
        var (orders, db) = await CreateAsync();
        db.Quotes.Add(new Quote
        {
            Id = 15,
            CompanyId = 1,
            QuoteNumber = "Q-EMPTY",
            Subject = "",
            QuoteStage = "Accepted",
            Lines = [new QuoteLine { CompanyId = 1, ProductName = "Item", Quantity = 1, UnitPrice = 10 }]
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => orders.CreateFromQuoteAsync(15));
        Assert.Contains("account or customer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Quote UiQuote(int id, string? accountName) => new()
    {
        Id = id,
        CompanyId = 1,
        QuoteNumber = $"Q-{id}",
        Subject = "Training pack",
        QuoteStage = "Accepted",
        AccountName = accountName,
        ContactName = "Pat Lee",
        CurrencyCode = "INR",
        Lines =
        [
            new QuoteLine
            {
                CompanyId = 1,
                ProductName = "Workshop",
                Quantity = 2,
                UnitPrice = 500,
                Discount = 0,
                LineTotal = 1000
            }
        ]
    };

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
