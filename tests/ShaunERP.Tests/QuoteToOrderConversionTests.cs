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

public class QuoteToOrderConversionTests
{
    [Fact]
    public async Task CreateFromQuote_AfterCancelledOrder_CreatesANewLiveOrder()
    {
        await using var db = await SeedAsync();
        var orders = CreateOrders(db);

        var first = await orders.CreateFromQuoteAsync(1);
        Assert.NotNull(first);
        Assert.Equal(SalesOrderStatus.Draft, first!.OrderStatus);

        var cancelled = await orders.CancelAsync(first.Id);
        Assert.NotNull(cancelled);
        Assert.Equal(SalesOrderStatus.Cancelled, cancelled!.OrderStatus);

        var second = await orders.CreateFromQuoteAsync(1);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second!.Id);
        Assert.Equal(SalesOrderStatus.Draft, second.OrderStatus);
        Assert.Equal(1, second.QuoteId);
        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal(2, await db.SalesOrders.CountAsync());
    }

    [Fact]
    public async Task CreateFromQuote_WhenALiveOrderExists_ReturnsThatOrder()
    {
        await using var db = await SeedAsync();
        var orders = CreateOrders(db);

        var first = await orders.CreateFromQuoteAsync(1);
        var again = await orders.CreateFromQuoteAsync(1);

        Assert.NotNull(first);
        Assert.NotNull(again);
        Assert.Equal(first!.Id, again!.Id);
        Assert.Equal(1, await db.SalesOrders.CountAsync());
    }

    private static async Task<ShaunErpDbContext> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        db.CompanyMasters.Add(new CompanyMaster
        {
            Id = 10,
            TenantCompanyId = 1,
            Name = "Book A",
            Currency = "INR",
            OrderNumberPrefix = "SO-",
            NextOrderSequence = 1001,
            InvoiceNumberPrefix = "INV-",
            NextInvoiceSequence = 1001,
            DefaultPaymentTerms = "Net 30",
            DefaultDueDays = 30
        });
        db.CrmCustomers.Add(new Customer
        {
            Id = 5,
            CompanyId = 1,
            CompanyName = "Patel Co"
        });
        db.Quotes.Add(new Quote
        {
            Id = 1,
            CompanyId = 1,
            QuoteNumber = "Q-1",
            Subject = "Accepted deal",
            QuoteStage = "Accepted",
            CustomerId = 5,
            CurrencyCode = "INR",
            Lines =
            [
                new QuoteLine
                {
                    CompanyId = 1,
                    ProductName = "Retainer",
                    Quantity = 1,
                    UnitPrice = 1000,
                    LineTotal = 1000
                }
            ]
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static SalesOrderService CreateOrders(ShaunErpDbContext db)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleKeys.Orders,
                ModuleKeys.Finance
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        return new SalesOrderService(db, tenant, new ModuleRegistry(), new BillingBookStub(db));
    }

    private sealed class BillingBookStub : IFinanceService
    {
        private readonly ShaunErpDbContext _db;

        public BillingBookStub(ShaunErpDbContext db) => _db = db;

        public Task<CompanyMaster> GetOrCreateBillingBookAsync(CancellationToken ct = default)
            => _db.CompanyMasters.FirstAsync(ct);

        public Task<FinanceDashboardStats> GetDashboardAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<CompanyMaster?> GetBillingBookAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<CompanyMaster?> UpdateBillingBookAsync(CompanyMaster update, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<Customer>> ListCrmCustomersAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<CrmProduct>> ListProductsAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<Invoice>> ListInvoicesAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<PagedResult<Invoice>> QueryInvoicesAsync(ListQuery? query = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Invoice?> GetInvoiceAsync(int id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Invoice?> UpdateInvoiceAsync(int id, Invoice update, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> DeleteInvoiceAsync(int id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Invoice?> MarkSentAsync(int id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Invoice?> VoidInvoiceAsync(int id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Invoice?> MarkPaidAsync(int id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<InvoicePayment>> ListPaymentsAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<PagedResult<InvoicePayment>> QueryPaymentsAsync(ListQuery? query = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<(bool Ok, string? Error, InvoicePayment? Payment)> RecordPaymentAsync(
            int invoiceId, InvoicePayment payment, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> DeletePaymentAsync(int paymentId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
