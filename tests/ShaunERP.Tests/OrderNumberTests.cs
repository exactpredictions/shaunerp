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

public class OrderNumberTests
{
    [Fact]
    public async Task CreateOrder_RejectsDuplicateNumber_CaseInsensitive()
    {
        var harness = await CreateOrdersAsync();

        await harness.Orders.CreateOrderAsync(Order("SO-1001", customerId: 1));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Orders.CreateOrderAsync(Order("so-1001", customerId: 1)));

        Assert.Contains("already used", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await harness.Db.SalesOrders.CountAsync());
    }

    [Fact]
    public async Task AutoNumber_SkipsManuallyUsedSequence()
    {
        var harness = await CreateOrdersAsync();

        await harness.Orders.CreateOrderAsync(Order("SO-1001", customerId: 1));
        var second = await harness.Orders.CreateOrderAsync(Order(null, customerId: 1));

        Assert.Equal("SO-1002", second.OrderNumber);
        Assert.Equal(2, await harness.Db.SalesOrders.Select(o => o.OrderNumber).Distinct().CountAsync());
    }

    [Fact]
    public async Task SameNumber_IsAllowedInAnotherTenant()
    {
        var first = await CreateOrdersAsync(companyId: 1);
        await first.Orders.CreateOrderAsync(Order("SO-1001", customerId: 1));

        var second = await CreateOrdersAsync(companyId: 2, db: first.Db);
        var other = await second.Orders.CreateOrderAsync(Order("SO-1001", customerId: 2));

        Assert.Equal("SO-1001", other.OrderNumber);
        Assert.Equal(2, other.CompanyId);
    }

    [Fact]
    public async Task SalesOrdersCsvPreview_DoesNotCrash_WhenOrderNumbersCollide()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        db.CrmCustomers.Add(new Customer { Id = 1, CompanyId = 1, CompanyName = "Acme", AccountNumber = "CUST-1" });
        db.SalesOrders.AddRange(
            new SalesOrder
            {
                CompanyId = 1, OrderNumber = "SO-1001", CustomerId = 1, CustomerName = "One",
                OrderDate = DateTime.UtcNow.Date, GrandTotal = 100
            },
            new SalesOrder
            {
                CompanyId = 1, OrderNumber = "SO-1001", CustomerId = 1, CustomerName = "Two",
                OrderDate = DateTime.UtcNow.Date, GrandTotal = 80
            });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(AdminSnapshot(1));
        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var orders = new SalesOrderService(db, tenant, registry, finance);
        var importer = new SalesOrdersCsvImporter(db, orders);

        var parsed = CsvParser.Parse("OrderNumber,CustomerAccountNumber,OrderDate\nSO-1001,CUST-1,2026-04-01\n");
        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Rows, r => r.Errors.Any(e => e.Contains("duplicated", StringComparison.OrdinalIgnoreCase)));
    }

    private static SalesOrder Order(string? number, int customerId) => new()
    {
        OrderNumber = number ?? "",
        CustomerId = customerId,
        OrderDate = DateTime.UtcNow.Date,
        CurrencyCode = "USD",
        Lines =
        [
            new SalesOrderLine { ProductName = "Service", Quantity = 1, UnitPrice = 100 }
        ]
    };

    private static async Task<(ShaunErpDbContext Db, SalesOrderService Orders)> CreateOrdersAsync(
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
            db.CrmCustomers.AddRange(
                new Customer { Id = 1, CompanyId = 1, CompanyName = "Acme One", AccountNumber = "CUST-1" },
                new Customer { Id = 2, CompanyId = 2, CompanyName = "Acme Two", AccountNumber = "CUST-2" });
            await db.SaveChangesAsync();
        }

        var tenant = new TenantContext();
        tenant.Set(AdminSnapshot(companyId));
        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        return (db, new SalesOrderService(db, tenant, registry, finance));
    }

    private static TenantContextSnapshot AdminSnapshot(int companyId) => new()
    {
        UserId = 1,
        CompanyId = companyId,
        CompanyName = "A",
        Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ModuleKeys.Orders, ModuleKeys.Finance, ModuleKeys.Crm, ModuleKeys.Accounting
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
