using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class CustomerAccountNumberTests
{
    [Fact]
    public async Task CreateCustomer_RejectsDuplicateAccountNumber_CaseInsensitive()
    {
        var harness = await CreateCrmAsync();

        await harness.Crm.CreateCustomerAsync(Customer("CUST-001", "Acme One"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Crm.CreateCustomerAsync(Customer("cust-001", "Acme Two")));

        Assert.Contains("already used", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await harness.Db.CrmCustomers.CountAsync());
    }

    [Fact]
    public async Task BlankAccountNumbers_AreAllowedOnMultipleCustomers()
    {
        var harness = await CreateCrmAsync();

        await harness.Crm.CreateCustomerAsync(Customer(null, "No Number One"));
        await harness.Crm.CreateCustomerAsync(Customer("  ", "No Number Two"));

        Assert.Equal(2, await harness.Db.CrmCustomers.CountAsync());
        Assert.All(await harness.Db.CrmCustomers.ToListAsync(), c => Assert.Null(c.AccountNumber));
    }

    [Fact]
    public async Task SameAccountNumber_IsAllowedInAnotherTenant()
    {
        var first = await CreateCrmAsync(companyId: 1);
        await first.Crm.CreateCustomerAsync(Customer("CUST-001", "Acme One"));

        var second = await CreateCrmAsync(companyId: 2, db: first.Db);
        var other = await second.Crm.CreateCustomerAsync(Customer("CUST-001", "Acme Two"));

        Assert.Equal("CUST-001", other.AccountNumber);
        Assert.Equal(2, other.CompanyId);
    }

    [Fact]
    public async Task UpdateCustomer_CanKeepOwnAccountNumber()
    {
        var harness = await CreateCrmAsync();
        var created = await harness.Crm.CreateCustomerAsync(Customer("CUST-001", "Acme"));
        created.CompanyName = "Acme Renamed";

        var updated = await harness.Crm.UpdateCustomerAsync(created.Id, created);

        Assert.NotNull(updated);
        Assert.Equal("CUST-001", updated!.AccountNumber);
        Assert.Equal("Acme Renamed", updated.CompanyName);
    }

    [Fact]
    public async Task InvoiceCsvPreview_DoesNotCrash_WhenCustomerAccountNumbersCollide()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        db.CrmCustomers.AddRange(
            new Customer { Id = 1, CompanyId = 1, CompanyName = "Acme East", AccountNumber = "CUST-001" },
            new Customer { Id = 2, CompanyId = 1, CompanyName = "Acme West", AccountNumber = "cust-001" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(AdminSnapshot(1));
        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var importer = new InvoicesCsvImporter(db, finance);

        var parsed = CsvParser.Parse(
            "InvoiceNumber,CustomerAccountNumber,InvoiceDate,GrandTotal\nSAMPLE-INV-001,CUST-001,2026-04-01,1180.00\n");
        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Rows, r => r.Errors.Any(e => e.Contains("duplicated", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task CustomersCsvPreview_RejectsDuplicateAccountNumberInFile()
    {
        var harness = await CreateCrmAsync();
        var importer = new CustomersCsvImporter(harness.Db, harness.Crm);

        var parsed = CsvParser.Parse(
            "AccountNumber,CompanyName\nCUST-009,First Co\nCUST-009,Second Co\n");
        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.Contains(preview.Rows, r => r.Errors.Any(e => e.Contains("Duplicate account number", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(1, preview.Rows.Count(r => r.IsValid));
    }

    private static Customer Customer(string? accountNumber, string name) => new()
    {
        AccountNumber = accountNumber,
        CompanyName = name,
        CustomerType = "Business"
    };

    private static async Task<(ShaunErpDbContext Db, CrmService Crm)> CreateCrmAsync(
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
            await db.SaveChangesAsync();
        }

        var tenant = new TenantContext();
        tenant.Set(AdminSnapshot(companyId));
        var phones = new PhoneNormalizer();
        var emails = new EmailNormalizer();
        var crm = new CrmService(
            db,
            tenant,
            new ModuleRegistry(),
            new PartyIdentityService(db, new IdentityMatchService(db, phones, emails), phones, emails));
        return (db, crm);
    }

    private static TenantContextSnapshot AdminSnapshot(int companyId) => new()
    {
        UserId = 1,
        CompanyId = companyId,
        CompanyName = "A",
        Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ModuleKeys.Crm, ModuleKeys.Finance, ModuleKeys.Orders, ModuleKeys.Accounting
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
