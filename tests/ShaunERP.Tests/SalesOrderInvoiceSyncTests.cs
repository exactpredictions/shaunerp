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

public class SalesOrderInvoiceSyncTests
{
    [Fact]
    public async Task CreateInvoiceFromOrder_DoesNotLockOrder_UntilInvoiceIsSent()
    {
        var (orders, finance) = await CreateAsync();
        var order = await SeedConfirmedOrderAsync(orders);

        var (ok, error, invoice) = await orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(ok, error);
        Assert.NotNull(invoice);
        Assert.Equal(InvoiceStatus.Draft, invoice!.InvoiceStatus);

        var afterCreate = await orders.GetOrderAsync(order.Id);
        Assert.NotNull(afterCreate);
        Assert.Equal(SalesOrderStatus.Confirmed, afterCreate!.OrderStatus);
        Assert.Equal(0m, afterCreate.AmountInvoiced);
        Assert.Equal(afterCreate.GrandTotal, afterCreate.BalanceToInvoice);

        var sent = await finance.MarkSentAsync(invoice.Id);
        Assert.NotNull(sent);
        Assert.Equal(InvoiceStatus.Sent, sent!.InvoiceStatus);

        var afterSend = await orders.GetOrderAsync(order.Id);
        Assert.NotNull(afterSend);
        Assert.Equal(SalesOrderStatus.Invoiced, afterSend!.OrderStatus);
        Assert.Equal(afterSend.GrandTotal, afterSend.AmountInvoiced);
        Assert.Equal(0m, afterSend.BalanceToInvoice);
    }

    [Fact]
    public async Task VoidInvoice_UnlocksLinkedSalesOrder()
    {
        var (orders, finance) = await CreateAsync();
        var order = await SeedConfirmedOrderAsync(orders);

        var (ok, error, invoice) = await orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(ok, error);
        await finance.MarkSentAsync(invoice!.Id);

        var locked = await orders.GetOrderAsync(order.Id);
        Assert.Equal(SalesOrderStatus.Invoiced, locked!.OrderStatus);

        var voided = await finance.VoidInvoiceAsync(invoice.Id);
        Assert.NotNull(voided);
        Assert.Equal(InvoiceStatus.Void, voided!.InvoiceStatus);

        var unlocked = await orders.GetOrderAsync(order.Id);
        Assert.NotNull(unlocked);
        Assert.Equal(SalesOrderStatus.Confirmed, unlocked!.OrderStatus);
        Assert.Equal(0m, unlocked.AmountInvoiced);
        Assert.Equal(unlocked.GrandTotal, unlocked.BalanceToInvoice);

        var (okAgain, errAgain, second) = await orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(okAgain, errAgain);
        Assert.NotNull(second);
        Assert.NotEqual(invoice.Id, second!.Id);
    }

    [Fact]
    public async Task DeleteDraftInvoice_AllowsCreatingANewInvoice()
    {
        var (orders, finance) = await CreateAsync();
        var order = await SeedConfirmedOrderAsync(orders);

        var (ok, error, invoice) = await orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(ok, error);

        var duplicate = await orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.False(duplicate.Ok);
        Assert.Contains("already fully invoiced", duplicate.Error, StringComparison.OrdinalIgnoreCase);

        Assert.True(await finance.DeleteInvoiceAsync(invoice!.Id));

        var afterDelete = await orders.GetOrderAsync(order.Id);
        Assert.Equal(SalesOrderStatus.Confirmed, afterDelete!.OrderStatus);
        Assert.Equal(0m, afterDelete.AmountInvoiced);

        var (okAgain, errAgain, second) = await orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(okAgain, errAgain);
        Assert.NotNull(second);
    }

    [Fact]
    public async Task RecordingPaymentOnDraftInvoice_MarksOrderInvoiced()
    {
        var (orders, finance) = await CreateAsync();
        var order = await SeedConfirmedOrderAsync(orders);

        var (ok, error, invoice) = await orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(ok, error);

        var (paidOk, paidErr, _) = await finance.RecordPaymentAsync(invoice!.Id, new InvoicePayment
        {
            Amount = invoice.GrandTotal,
            Method = "Bank transfer"
        });
        Assert.True(paidOk, paidErr);

        var afterPay = await orders.GetOrderAsync(order.Id);
        Assert.Equal(SalesOrderStatus.Invoiced, afterPay!.OrderStatus);
        Assert.Equal(afterPay.GrandTotal, afterPay.AmountInvoiced);
    }

    private static async Task<SalesOrder> SeedConfirmedOrderAsync(ISalesOrderService orders)
    {
        var created = await orders.CreateOrderAsync(new SalesOrder
        {
            CustomerId = 1,
            OrderDate = DateTime.UtcNow.Date,
            Lines =
            [
                new SalesOrderLine
                {
                    ProductName = "Consulting",
                    Description = "Monthly retainer",
                    Quantity = 2,
                    UnitPrice = 1500m
                }
            ]
        });
        var confirmed = await orders.ConfirmAsync(created.Id);
        Assert.NotNull(confirmed);
        Assert.Equal(SalesOrderStatus.Confirmed, confirmed!.OrderStatus);
        Assert.Equal(3000m, confirmed.GrandTotal);
        return confirmed;
    }

    private static async Task<(ISalesOrderService Orders, IFinanceService Finance)> CreateAsync()
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
                ModuleKeys.Orders, ModuleKeys.Finance
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var orders = new SalesOrderService(db, tenant, registry, finance);
        return (orders, finance);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
