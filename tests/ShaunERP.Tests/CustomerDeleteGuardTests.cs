using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;

namespace ShaunERP.Tests;

public class CustomerDeleteGuardTests
{
    [Fact]
    public async Task DeleteCustomer_succeeds_when_no_documents_exist()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db);
        var customer = await crm.CreateCustomerAsync(new Customer { CompanyName = "Acme" });

        Assert.True(await crm.DeleteCustomerAsync(customer.Id));
        Assert.Equal(0, await db.CrmCustomers.CountAsync());
    }

    [Fact]
    public async Task DeleteCustomer_rejects_customer_with_a_sent_invoice()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db);
        var customer = await crm.CreateCustomerAsync(new Customer { CompanyName = "Acme" });

        db.Invoices.Add(new Invoice
        {
            CompanyId = 1,
            CompanyMasterId = 1,
            CustomerId = customer.Id,
            CustomerName = "Acme",
            InvoiceNumber = "INV-0001",
            InvoiceStatus = InvoiceStatus.Sent,
            GrandTotal = 1000m,
            Amount = 1000m,
            InvoiceDate = DateTime.UtcNow.Date
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => crm.DeleteCustomerAsync(customer.Id));
        Assert.Contains("invoices", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.CrmCustomers.CountAsync());
        Assert.Equal(customer.Id, (await db.Invoices.SingleAsync()).CustomerId);
    }

    [Fact]
    public async Task DeleteCustomer_rejects_customer_with_a_confirmed_sales_order()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db);
        var customer = await crm.CreateCustomerAsync(new Customer { CompanyName = "Acme" });

        db.SalesOrders.Add(new SalesOrder
        {
            CompanyId = 1,
            CustomerId = customer.Id,
            CustomerName = "Acme",
            OrderNumber = "SO-0001",
            OrderStatus = SalesOrderStatus.Confirmed,
            GrandTotal = 2500m,
            OrderDate = DateTime.UtcNow.Date
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => crm.DeleteCustomerAsync(customer.Id));
        Assert.Contains("sales orders", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.CrmCustomers.CountAsync());
        Assert.Equal(customer.Id, (await db.SalesOrders.SingleAsync()).CustomerId);
    }

    [Fact]
    public async Task DeleteCustomer_allows_customer_with_only_void_invoices_and_cancelled_orders()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db);
        var customer = await crm.CreateCustomerAsync(new Customer { CompanyName = "Acme" });

        db.Invoices.Add(new Invoice
        {
            CompanyId = 1,
            CompanyMasterId = 1,
            CustomerId = customer.Id,
            InvoiceNumber = "INV-VOID",
            InvoiceStatus = InvoiceStatus.Void,
            GrandTotal = 100m,
            Amount = 100m
        });
        db.SalesOrders.Add(new SalesOrder
        {
            CompanyId = 1,
            CustomerId = customer.Id,
            OrderNumber = "SO-CANCEL",
            OrderStatus = SalesOrderStatus.Cancelled,
            GrandTotal = 100m
        });
        await db.SaveChangesAsync();

        Assert.True(await crm.DeleteCustomerAsync(customer.Id));
        Assert.Equal(0, await db.CrmCustomers.CountAsync());
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        db.SaveChanges();
        return db;
    }

    private static CrmService CreateCrm(ShaunErpDbContext db)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        return new CrmService(
            db,
            tenant,
            new ModuleRegistry(),
            new PartyIdentityService(
                db,
                new IdentityMatchService(db, new PhoneNormalizer(), new EmailNormalizer()),
                new PhoneNormalizer(),
                new EmailNormalizer()));
    }
}
