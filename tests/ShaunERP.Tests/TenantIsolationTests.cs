using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task CrmService_DoesNotLeakOtherTenantLeads()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(
            new Company { Id = 1, Name = "A" },
            new Company { Id = 2, Name = "B" });
        db.Leads.AddRange(
            new Lead { Id = 1, CompanyId = 1, FirstName = "A1", LastName = "Lead" },
            new Lead { Id = 2, CompanyId = 2, FirstName = "B1", LastName = "Lead" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var crm = new CrmService(db, tenant, new ModuleRegistry());
        var leads = await crm.ListLeadsAsync();
        Assert.Single(leads);
        Assert.Equal(1, leads[0].CompanyId);
        Assert.Null(await crm.GetLeadAsync(2));
    }

    [Fact]
    public async Task FinanceService_DoesNotLeakOtherTenantInvoices()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(
            new Company { Id = 1, Name = "A" },
            new Company { Id = 2, Name = "B" });
        db.CompanyMasters.AddRange(
            new CompanyMaster { Id = 10, TenantCompanyId = 1, Name = "Book A", InvoiceNumberPrefix = "A-", NextInvoiceSequence = 1 },
            new CompanyMaster { Id = 20, TenantCompanyId = 2, Name = "Book B", InvoiceNumberPrefix = "B-", NextInvoiceSequence = 1 });
        db.Invoices.AddRange(
            new Invoice
            {
                Id = 1, CompanyId = 1, CompanyMasterId = 10, InvoiceNumber = "A-0001",
                GrandTotal = 100, Amount = 100, InvoiceStatus = InvoiceStatus.Sent
            },
            new Invoice
            {
                Id = 2, CompanyId = 2, CompanyMasterId = 20, InvoiceNumber = "B-0001",
                GrandTotal = 200, Amount = 200, InvoiceStatus = InvoiceStatus.Sent
            });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Finance, ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var finance = new FinanceService(db, tenant, new ModuleRegistry());
        var invoices = await finance.ListInvoicesAsync();
        Assert.Single(invoices);
        Assert.Equal(1, invoices[0].CompanyId);
        Assert.Null(await finance.GetInvoiceAsync(2));
    }

    [Fact]
    public async Task AccountSwitch_RequiresMembership()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var db = new ShaunErpDbContext(options))
        {
            db.Users.Add(new User
            {
                Id = 5,
                Username = "u",
                Email = "u@example.com",
                FirstName = "U",
                LastName = "Ser",
                PasswordHash = "x",
                IsActive = true
            });
            db.Companies.AddRange(new Company { Id = 1, Name = "A" }, new Company { Id = 2, Name = "B" });
            db.CompanyUsers.Add(new CompanyUser { Id = 1, UserId = 5, CompanyId = 1 });
            await db.SaveChangesAsync();
        }

        var access = new AccountAccessService(new TestDbFactory(options));
        Assert.True(await access.BelongsToAsync(5, 1));
        Assert.False(await access.BelongsToAsync(5, 2));
    }

    private sealed class TestDbFactory(DbContextOptions<ShaunErpDbContext> options) : IDbContextFactory<ShaunErpDbContext>
    {
        public ShaunErpDbContext CreateDbContext() => new(options);
    }
}
