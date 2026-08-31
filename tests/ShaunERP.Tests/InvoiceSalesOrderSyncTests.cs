using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class InvoiceSalesOrderSyncTests
{
    [Fact]
    public async Task Deleting_draft_invoice_from_order_makes_order_billable_again()
    {
        var fx = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(fx, 1000m);
        var (ok, error, invoice) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(ok, error);
        Assert.NotNull(invoice);

        var invoiced = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Invoiced, invoiced.OrderStatus);
        Assert.Equal(1000m, invoiced.AmountInvoiced);

        Assert.True(await fx.Finance.DeleteInvoiceAsync(invoice!.Id));

        var after = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(0m, after.AmountInvoiced);
        Assert.Equal(SalesOrderStatus.Confirmed, after.OrderStatus);

        var (againOk, againError, second) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(againOk, againError);
        Assert.NotNull(second);
        Assert.NotEqual(invoice.Id, second!.Id);
    }

    [Fact]
    public async Task Voiding_invoice_from_order_makes_order_billable_again()
    {
        var fx = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(fx, 500m);
        var (_, _, invoice) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.NotNull(invoice);
        await fx.Finance.MarkSentAsync(invoice!.Id);

        var voided = await fx.Finance.VoidInvoiceAsync(invoice.Id);
        Assert.NotNull(voided);
        Assert.Equal(InvoiceStatus.Void, voided!.InvoiceStatus);

        var after = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(0m, after.AmountInvoiced);
        Assert.Equal(SalesOrderStatus.Confirmed, after.OrderStatus);

        var (ok, error, _) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.True(ok, error);
    }

    [Fact]
    public async Task Editing_draft_invoice_amount_marks_order_partially_invoiced()
    {
        var fx = await CreateAsync();
        var order = await CreateConfirmedOrderAsync(fx, 1000m);
        var (_, _, invoice) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.NotNull(invoice);

        var partial = CloneForUpdate(invoice!, 400m);
        var updated = await fx.Finance.UpdateInvoiceAsync(invoice!.Id, partial);
        Assert.NotNull(updated);
        Assert.Equal(400m, updated!.GrandTotal);

        var after = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(400m, after.AmountInvoiced);
        Assert.Equal(SalesOrderStatus.PartiallyInvoiced, after.OrderStatus);
        Assert.Equal(600m, after.BalanceToInvoice);
    }

    [Fact]
    public async Task Editing_sent_invoice_total_reposts_accounting_voucher()
    {
        var fx = await CreateAsync(includeAccounting: true);
        await fx.Accounting.EnsureBootstrapAsync();

        var order = await CreateConfirmedOrderAsync(fx, 1000m);
        var (_, _, invoice) = await fx.Orders.CreateInvoiceFromOrderAsync(order.Id);
        Assert.NotNull(invoice);
        var sent = await fx.Finance.MarkSentAsync(invoice!.Id);
        Assert.NotNull(sent);
        Assert.True(sent!.AccountingVoucherId is > 0);
        var originalVoucherId = sent.AccountingVoucherId!.Value;
        var originalVoucher = await fx.Db.AccountingVouchers.SingleAsync(v => v.Id == originalVoucherId);
        Assert.Equal(1000m, originalVoucher.TotalDebit);

        var adjusted = CloneForUpdate(sent!, 250m);
        adjusted.InvoiceStatus = InvoiceStatus.Sent;
        var updated = await fx.Finance.UpdateInvoiceAsync(sent!.Id, adjusted);
        Assert.NotNull(updated);
        Assert.Equal(250m, updated!.GrandTotal);
        Assert.True(updated.AccountingVoucherId is > 0);
        Assert.NotEqual(originalVoucherId, updated.AccountingVoucherId);

        var reversed = await fx.Db.AccountingVouchers.SingleAsync(v => v.Id == originalVoucherId);
        Assert.Equal(VoucherStatus.Reversed, reversed.VoucherStatus);

        var posted = await fx.Db.AccountingVouchers
            .Include(v => v.Lines)
            .SingleAsync(v => v.Id == updated.AccountingVoucherId);
        Assert.Equal(VoucherStatus.Posted, posted.VoucherStatus);
        Assert.Equal(250m, posted.TotalDebit);
    }

    private static Invoice CloneForUpdate(Invoice source, decimal unitPrice) => new()
    {
        CustomerId = source.CustomerId,
        CustomerName = source.CustomerName,
        InvoiceNumber = source.InvoiceNumber,
        InvoiceDate = source.InvoiceDate,
        DueDate = source.DueDate,
        CurrencyCode = source.CurrencyCode,
        PaymentTerms = source.PaymentTerms,
        InvoiceStatus = source.InvoiceStatus,
        Discount = 0,
        SalesOrderId = source.SalesOrderId,
        Lines =
        [
            new InvoiceLine { ProductName = "Adjusted", Quantity = 1, UnitPrice = unitPrice }
        ]
    };

    private static async Task<SalesOrder> CreateConfirmedOrderAsync(Fixture fx, decimal unitPrice)
    {
        var order = await fx.Orders.CreateOrderAsync(new SalesOrder
        {
            CustomerId = 1,
            Lines =
            [
                new SalesOrderLine { ProductName = "Widget", Quantity = 1, UnitPrice = unitPrice }
            ]
        });
        var confirmed = await fx.Orders.ConfirmAsync(order.Id);
        Assert.NotNull(confirmed);
        return confirmed!;
    }

    private static async Task<Fixture> CreateAsync(bool includeAccounting = false)
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Test Co" });
        db.CrmCustomers.Add(new Customer { Id = 1, CompanyId = 1, CompanyName = "Acme" });
        await db.SaveChangesAsync();

        var entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ModuleKeys.Finance, ModuleKeys.Orders
        };
        if (includeAccounting)
            entitlements.Add(ModuleKeys.Accounting);

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Test Co",
            Entitlements = entitlements,
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var orders = new SalesOrderService(db, tenant, registry, finance);
        return new Fixture(db, finance, orders, accounting);
    }

    private sealed record Fixture(
        ShaunErpDbContext Db,
        FinanceService Finance,
        SalesOrderService Orders,
        AccountingService Accounting);

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
