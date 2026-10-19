using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class OpportunitiesCsvImporterTests
{
    [Fact]
    public async Task Reimport_DoesNotWipeWonDealCustomerOrClosedStage()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.CrmAccounts.Add(new Account
        {
            Id = 20,
            CompanyId = 1,
            AccountName = "Won account",
            AccountType = "Customer"
        });
        db.Leads.Add(new Lead
        {
            Id = 30,
            CompanyId = 1,
            FirstName = "Ravi",
            LastName = "Kumar"
        });
        db.CrmCustomers.Add(new Customer
        {
            Id = 40,
            CompanyId = 1,
            CompanyName = "Won account",
            AccountId = 20
        });
        db.Opportunities.Add(new Opportunity
        {
            Id = 50,
            CompanyId = 1,
            DealName = "Acme expansion",
            Amount = 250000,
            Stage = "Closed Won",
            Probability = 100,
            ForecastCategory = "Closed",
            CustomerId = 40,
            AccountId = 20,
            LeadId = 30,
            ActualCloseDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualRevenue = 250000,
            CurrencyCode = "INR"
        });
        await db.SaveChangesAsync();

        var crm = NewCrm(db);
        var importer = new OpportunitiesCsvImporter(db, crm);
        var preview = new CsvImportPreview
        {
            EntityKey = "opportunities",
            Rows =
            [
                new CsvRowValidation
                {
                    RowNumber = 2,
                    IsValid = true,
                    Action = "Update",
                    ExistingId = 50,
                    MatchKey = "Acme expansion",
                    Values = new Dictionary<string, string>
                    {
                        ["DealName"] = "Acme expansion",
                        ["Amount"] = "275000",
                        ["Stage"] = "",
                        ["CustomerAccountNumber"] = "",
                        ["CurrencyCode"] = "INR"
                    }
                }
            ]
        };

        var result = await importer.ApplyAsync(preview, 1);

        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Updated);

        var deal = await crm.GetOpportunityAsync(50);
        Assert.NotNull(deal);
        Assert.Equal(275000m, deal!.Amount);
        Assert.Equal("Closed Won", deal.Stage);
        Assert.Equal(40, deal.CustomerId);
        Assert.Equal(20, deal.AccountId);
        Assert.Equal(30, deal.LeadId);
        Assert.Equal("Closed", deal.ForecastCategory);
        Assert.Equal(100, deal.Probability);
    }

    [Fact]
    public async Task Reimport_UpdatesCustomerWhenAccountNumberIsPresent()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.CrmCustomers.AddRange(
            new Customer { Id = 40, CompanyId = 1, CompanyName = "Old", AccountNumber = "OLD-001" },
            new Customer { Id = 41, CompanyId = 1, CompanyName = "New", AccountNumber = "NEW-001" });
        db.Opportunities.Add(new Opportunity
        {
            Id = 50,
            CompanyId = 1,
            DealName = "Switch bill-to",
            Amount = 1000,
            Stage = "Negotiation",
            CustomerId = 40,
            CurrencyCode = "INR"
        });
        await db.SaveChangesAsync();

        var crm = NewCrm(db);
        var importer = new OpportunitiesCsvImporter(db, crm);
        var preview = new CsvImportPreview
        {
            EntityKey = "opportunities",
            Rows =
            [
                new CsvRowValidation
                {
                    RowNumber = 2,
                    IsValid = true,
                    Action = "Update",
                    ExistingId = 50,
                    Values = new Dictionary<string, string>
                    {
                        ["DealName"] = "Switch bill-to",
                        ["CustomerAccountNumber"] = "NEW-001"
                    }
                }
            ]
        };

        var result = await importer.ApplyAsync(preview, 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var deal = await crm.GetOpportunityAsync(50);
        Assert.Equal(41, deal!.CustomerId);
        Assert.Equal("Negotiation", deal.Stage);
    }

    private static ShaunErpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ShaunErpDbContext(options);
    }

    private static CrmService NewCrm(ShaunErpDbContext db)
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
