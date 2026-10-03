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

public class InvoiceTenantIsolationTests
{
    [Fact]
    public async Task CreateInvoice_RejectsCustomerFromAnotherTenant()
    {
        await using var db = await SeedAsync();
        var finance = CreateFinance(db, companyId: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finance.CreateInvoiceAsync(NewInvoice(customerId: 2, customerName: "Cover story")));

        Assert.Equal("Customer not found for this account.", ex.Message);
        Assert.Empty(db.Invoices.Where(i => i.CompanyId == 1));
    }

    [Fact]
    public async Task UpdateInvoice_RejectsCustomerFromAnotherTenant()
    {
        await using var db = await SeedAsync();
        var finance = CreateFinance(db, companyId: 1);
        var created = await finance.CreateInvoiceAsync(NewInvoice(customerId: 1, customerName: "Acme A"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finance.UpdateInvoiceAsync(created.Id, NewInvoice(customerId: 2, customerName: "Cover story")));

        Assert.Equal("Customer not found for this account.", ex.Message);
        var row = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == created.Id);
        Assert.Equal(1, row.CustomerId);
    }

    [Fact]
    public async Task CreateInvoice_RejectsSalesOrderFromAnotherTenant()
    {
        await using var db = await SeedAsync();
        var finance = CreateFinance(db, companyId: 1);
        var invoice = NewInvoice(customerId: 1, customerName: "Acme A");
        invoice.SalesOrderId = 20;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => finance.CreateInvoiceAsync(invoice));

        Assert.Equal("Sales order not found for this account.", ex.Message);
        Assert.Empty(db.Invoices.Where(i => i.CompanyId == 1));
    }

    [Fact]
    public async Task GetInvoice_DoesNotReturnForeignCustomerMaster()
    {
        await using var db = await SeedAsync();
        db.Invoices.Add(new Invoice
        {
            CompanyId = 1,
            CompanyMasterId = 10,
            InvoiceNumber = "A-LEAK",
            CustomerId = 2,
            CustomerName = "Stored name",
            InvoiceDate = DateTime.UtcNow.Date,
            InvoiceStatus = InvoiceStatus.Draft,
            GrandTotal = 50,
            Amount = 50,
            Lines =
            [
                new InvoiceLine
                {
                    CompanyId = 1,
                    ProductName = "Widget",
                    Quantity = 1,
                    UnitPrice = 50,
                    LineTotal = 50
                }
            ]
        });
        await db.SaveChangesAsync();

        var finance = CreateFinance(db, companyId: 1);
        var invoice = await finance.GetInvoiceAsync(db.Invoices.Single(i => i.InvoiceNumber == "A-LEAK").Id);

        Assert.NotNull(invoice);
        Assert.Equal(2, invoice!.CustomerId);
        Assert.Null(invoice.Customer);
        var listed = await finance.ListInvoicesAsync();
        Assert.All(listed, i => Assert.True(i.Customer is null || i.Customer.CompanyId == 1));
    }

    [Fact]
    public async Task CreateInvoice_AcceptsOwnCustomer()
    {
        await using var db = await SeedAsync();
        var finance = CreateFinance(db, companyId: 1);

        var created = await finance.CreateInvoiceAsync(NewInvoice(customerId: 1, customerName: "ignored"));
        var loaded = await finance.GetInvoiceAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.CustomerId);
        Assert.Equal("Acme A", loaded.CustomerName);
        Assert.NotNull(loaded.Customer);
        Assert.Equal("secret-a@example.com", loaded.Customer!.Email);
    }

    private static Invoice NewInvoice(int customerId, string customerName) => new()
    {
        CustomerId = customerId,
        CustomerName = customerName,
        InvoiceDate = DateTime.UtcNow.Date,
        CurrencyCode = "USD",
        Lines =
        [
            new InvoiceLine
            {
                ProductName = "Widget",
                Quantity = 1,
                UnitPrice = 100
            }
        ]
    };

    private static async Task<ShaunErpDbContext> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(new Company { Id = 1, Name = "A" }, new Company { Id = 2, Name = "B" });
        db.CompanyMasters.AddRange(
            new CompanyMaster { Id = 10, TenantCompanyId = 1, Name = "Book A", InvoiceNumberPrefix = "A-", NextInvoiceSequence = 1 },
            new CompanyMaster { Id = 20, TenantCompanyId = 2, Name = "Book B", InvoiceNumberPrefix = "B-", NextInvoiceSequence = 1 });
        db.CrmCustomers.AddRange(
            new Customer
            {
                Id = 1,
                CompanyId = 1,
                CompanyName = "Acme A",
                Email = "secret-a@example.com",
                PhoneNumber = "111",
                BillingAddress = "1 A Street",
                TaxId = "GSTIN-A"
            },
            new Customer
            {
                Id = 2,
                CompanyId = 2,
                CompanyName = "Beta B",
                Email = "secret-b@example.com",
                PhoneNumber = "222",
                BillingAddress = "2 B Street",
                TaxId = "GSTIN-B"
            });
        db.SalesOrders.Add(new SalesOrder
        {
            Id = 20,
            CompanyId = 2,
            CustomerId = 2,
            CustomerName = "Beta B",
            OrderNumber = "SO-B-1",
            OrderDate = DateTime.UtcNow.Date,
            OrderStatus = SalesOrderStatus.Confirmed,
            GrandTotal = 500
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static FinanceService CreateFinance(ShaunErpDbContext db, int companyId)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = companyId,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Finance, ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        return new FinanceService(db, tenant, registry, posting, accounting);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
