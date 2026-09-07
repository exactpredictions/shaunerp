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

public class OrderInvoiceRemainderTests
{
    [Fact]
    public async Task First_invoice_from_order_copies_lines_and_matches_order_total()
    {
        var fx = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(fx, quantity: 2, unitPrice: 500m);

        var (ok, error, invoice) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(ok, error);
        Assert.NotNull(invoice);
        Assert.Equal(1000m, invoice!.GrandTotal);
        Assert.Single(invoice.Lines);
        Assert.Equal(2, invoice.Lines.First().Quantity);
        Assert.Equal(500m, invoice.Lines.First().UnitPrice);

        var after = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Invoiced, after.OrderStatus);
        Assert.Equal(1000m, after.AmountInvoiced);
    }

    [Fact]
    public async Task Create_invoice_after_partial_finance_invoice_bills_remaining_balance_only()
    {
        var fx = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(fx, quantity: 1, unitPrice: 1000m);

        await fx.Finance.CreateInvoiceAsync(new Invoice
        {
            CustomerId = 1,
            CustomerName = "Acme",
            SalesOrderId = order.Id,
            Lines = [new InvoiceLine { ProductName = "Deposit", Quantity = 1, UnitPrice = 400m }]
        });

        var stale = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(0m, stale.AmountInvoiced);
        Assert.Equal(SalesOrderStatus.Confirmed, stale.OrderStatus);

        var (ok, error, invoice) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(ok, error);
        Assert.NotNull(invoice);
        Assert.Equal(600m, invoice!.GrandTotal);
        Assert.DoesNotContain(invoice.Lines, l => l.Quantity == 1 && l.UnitPrice == 1000m);

        var after = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(1000m, after.AmountInvoiced);
        Assert.Equal(SalesOrderStatus.Invoiced, after.OrderStatus);

        var invoices = await fx.Db.Invoices.Where(i => i.SalesOrderId == order.Id).ToListAsync();
        Assert.Equal(2, invoices.Count);
        Assert.Equal(1000m, invoices.Sum(i => i.GrandTotal));
    }

    [Fact]
    public async Task Create_invoice_is_rejected_when_linked_invoices_already_cover_the_order()
    {
        var fx = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(fx, quantity: 1, unitPrice: 250m);

        await fx.Finance.CreateInvoiceAsync(new Invoice
        {
            CustomerId = 1,
            CustomerName = "Acme",
            SalesOrderId = order.Id,
            Lines = [new InvoiceLine { ProductName = "Full", Quantity = 1, UnitPrice = 250m }]
        });

        var (ok, error, invoice) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.False(ok);
        Assert.Null(invoice);
        Assert.Contains("already fully invoiced", error, StringComparison.OrdinalIgnoreCase);

        var after = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(250m, after.AmountInvoiced);
        Assert.Equal(SalesOrderStatus.Invoiced, after.OrderStatus);
        Assert.Equal(1, await fx.Db.Invoices.CountAsync(i => i.SalesOrderId == order.Id));
    }

    private static async Task<SalesOrder> CreateConfirmedOrderAsync(Fixture fx, decimal quantity, decimal unitPrice)
    {
        var order = await fx.Orders.CreateOrderAsync(new SalesOrder
        {
            CustomerId = 1,
            Lines =
            [
                new SalesOrderLine { ProductName = "Widget", Quantity = quantity, UnitPrice = unitPrice }
            ]
        });
        var confirmed = await fx.Orders.ConfirmAsync(order.Id);
        Assert.NotNull(confirmed);
        return confirmed!;
    }

    private static async Task<Fixture> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Test Co" });
        db.CrmCustomers.Add(new Customer { Id = 1, CompanyId = 1, CompanyName = "Acme" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Test Co",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleKeys.Finance, ModuleKeys.Orders
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var orders = new SalesOrderService(db, tenant, registry, finance);
        return new Fixture(db, finance, orders);
    }

    private sealed record Fixture(ShaunErpDbContext Db, FinanceService Finance, SalesOrderService Orders);

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
