using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class SalesOrderTests
{
    [Fact]
    public async Task UpdateOrder_Rejects_TotalBelowAmountAlreadyInvoiced()
    {
        var (db, orders) = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(orders, quantity: 10, unitPrice: 100m);
        await AttachInvoiceAsync(db, order, 600m);

        var shrink = CloneForUpdate(order, quantity: 4, unitPrice: 100m, discount: 0m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => orders.UpdateOrderAsync(order.Id, shrink));
        Assert.Contains("already invoiced", ex.Message, StringComparison.OrdinalIgnoreCase);

        var persisted = await db.SalesOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        Assert.Equal(1000m, persisted.GrandTotal);
    }

    [Fact]
    public async Task UpdateOrder_Allows_ReducingOnlyTheUninvoicedRemainder()
    {
        var (db, orders) = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(orders, quantity: 10, unitPrice: 100m);
        await AttachInvoiceAsync(db, order, 600m);

        var reduceRemainder = CloneForUpdate(order, quantity: 8, unitPrice: 100m, discount: 0m);
        var updated = await orders.UpdateOrderAsync(order.Id, reduceRemainder);

        Assert.NotNull(updated);
        Assert.Equal(800m, updated!.GrandTotal);
        Assert.Equal(600m, updated.AmountInvoiced);
        Assert.Equal(SalesOrderStatus.PartiallyInvoiced, updated.OrderStatus);
        Assert.Equal(200m, updated.BalanceToInvoice);
    }

    [Fact]
    public async Task UpdateOrder_Rejects_HeaderDiscountThatDropsTotalBelowInvoiced()
    {
        var (db, orders) = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(orders, quantity: 10, unitPrice: 100m);
        await AttachInvoiceAsync(db, order, 600m);

        var discounted = CloneForUpdate(order, quantity: 10, unitPrice: 100m, discount: 500m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => orders.UpdateOrderAsync(order.Id, discounted));
        Assert.Contains("already invoiced", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SalesOrder CloneForUpdate(SalesOrder order, decimal quantity, decimal unitPrice, decimal discount)
        => new()
        {
            CustomerId = order.CustomerId,
            CurrencyCode = order.CurrencyCode,
            PaymentTerms = order.PaymentTerms,
            Discount = discount,
            OrderDate = order.OrderDate,
            Lines =
            [
                new SalesOrderLine
                {
                    ProductName = "Widget",
                    Quantity = quantity,
                    UnitPrice = unitPrice
                }
            ]
        };

    private static async Task<SalesOrder> CreateConfirmedOrderAsync(
        SalesOrderService orders, decimal quantity, decimal unitPrice)
    {
        var created = await orders.CreateOrderAsync(new SalesOrder
        {
            CustomerId = 1,
            OrderDate = DateTime.UtcNow.Date,
            CurrencyCode = "USD",
            Lines =
            [
                new SalesOrderLine
                {
                    ProductName = "Widget",
                    Quantity = quantity,
                    UnitPrice = unitPrice
                }
            ]
        });
        var confirmed = await orders.ConfirmAsync(created.Id);
        Assert.NotNull(confirmed);
        return confirmed!;
    }

    private static async Task AttachInvoiceAsync(ShaunErpDbContext db, SalesOrder order, decimal grandTotal)
    {
        var book = await db.CompanyMasters.FirstAsync(b => b.TenantCompanyId == order.CompanyId);
        db.Invoices.Add(new Invoice
        {
            CompanyId = order.CompanyId,
            CompanyMasterId = book.Id,
            CustomerId = order.CustomerId,
            SalesOrderId = order.Id,
            InvoiceNumber = $"INV-PARTIAL-{grandTotal:0}",
            InvoiceDate = DateTime.UtcNow.Date,
            InvoiceStatus = InvoiceStatus.Sent,
            CurrencyCode = "USD",
            GrandTotal = grandTotal,
            Amount = grandTotal,
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<(ShaunErpDbContext db, SalesOrderService orders)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.CrmCustomers.Add(new Customer { Id = 1, CompanyId = 1, CompanyName = "Buyer Co" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Acme",
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
        return (db, orders);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
