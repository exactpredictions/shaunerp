using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;

namespace ShaunERP.Tests;

public class CrmOpportunityTests
{
    [Fact]
    public async Task WinOpportunity_reuses_customer_already_linked_to_the_deal()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db, companyId: 1);

        var customer = await crm.CreateCustomerAsync(new Customer
        {
            CompanyName = "Harbor Logistics India",
            AccountNumber = "CUST-DEMO-02",
            PaymentTerms = "Net 30"
        });

        var deal = await crm.CreateOpportunityAsync(new Opportunity
        {
            DealName = "Harbor Logistics Expansion",
            AccountName = "Harbor Logistics India",
            Stage = "Negotiation",
            Amount = 310000m,
            CustomerId = customer.Id
        });

        var (ok, error, won) = await crm.WinOpportunityAsync(deal.Id, "Expansion signed");
        Assert.True(ok, error);
        Assert.NotNull(won);
        Assert.Equal(customer.Id, won!.Id);

        Assert.Equal(1, await db.CrmCustomers.CountAsync(c => c.CompanyId == 1));
        var refreshed = await db.Opportunities.SingleAsync(o => o.Id == deal.Id);
        Assert.Equal("Closed Won", refreshed.Stage);
        Assert.Equal(customer.Id, refreshed.CustomerId);
        Assert.Equal(100, refreshed.Probability);
        Assert.Equal("Expansion signed", refreshed.WinLossReason);
    }

    [Fact]
    public async Task WinOpportunity_creates_a_customer_when_the_deal_has_none()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db, companyId: 1);

        var deal = await crm.CreateOpportunityAsync(new Opportunity
        {
            DealName = "NovaTech Full Suite",
            AccountName = "NovaTech Systems",
            Stage = "Proposal",
            Amount = 420000m
        });

        var (ok, error, created) = await crm.WinOpportunityAsync(deal.Id);
        Assert.True(ok, error);
        Assert.NotNull(created);
        Assert.Equal("NovaTech Systems", created!.CompanyName);

        Assert.Equal(1, await db.CrmCustomers.CountAsync(c => c.CompanyId == 1));
        var refreshed = await db.Opportunities.SingleAsync(o => o.Id == deal.Id);
        Assert.Equal("Closed Won", refreshed.Stage);
        Assert.Equal(created.Id, refreshed.CustomerId);
    }

    private static ShaunErpDbContext CreateDb()
    {
        var db = new ShaunErpDbContext(new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Companies.Add(new Company { Id = 1, Name = "Tenant" });
        db.SaveChanges();
        return db;
    }

    private static CrmService CreateCrm(ShaunErpDbContext db, int companyId)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = companyId,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        var phones = new PhoneNormalizer();
        var emails = new EmailNormalizer();
        var parties = new PartyIdentityService(db, new IdentityMatchService(db, phones, emails), phones, emails);
        return new CrmService(db, tenant, new ModuleRegistry(), parties);
    }
}
