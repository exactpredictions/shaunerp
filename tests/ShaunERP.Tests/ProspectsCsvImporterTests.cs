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

public class ProspectsCsvImporterTests
{
    [Fact]
    public async Task Reimport_DoesNotWipeConvertedLeadOrAccountLinks()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.CrmAccounts.Add(new Account
        {
            Id = 20,
            CompanyId = 1,
            AccountName = "Patel Co",
            AccountType = "Prospect"
        });
        db.Leads.Add(new Lead
        {
            Id = 30,
            CompanyId = 1,
            FirstName = "Dev",
            LastName = "Patel",
            Email = "dev@patel.com",
            CompanyName = "Patel Co",
            LeadStatus = "Qualified",
            IsQualified = true
        });
        db.Prospects.Add(new Prospect
        {
            Id = 40,
            CompanyId = 1,
            Email = "dev@patel.com",
            FirstName = "Dev",
            LastName = "Patel",
            CompanyName = "Patel Co",
            PhoneNumber = "9888777666",
            Stage = "Proposal",
            Source = "Website",
            LeadId = 30,
            AccountId = 20,
            ProspectOwner = "admin",
            Description = "Converted from lead"
        });
        await db.SaveChangesAsync();

        var crm = NewCrm(db);
        var importer = new ProspectsCsvImporter(db, crm);
        var preview = HeaderOnlyUpdatePreview("dev@patel.com", 40, firstName: "Dev");

        var result = await importer.ApplyAsync(preview, 1);

        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Updated);

        var prospect = await crm.GetProspectAsync(40);
        Assert.NotNull(prospect);
        Assert.Equal(30, prospect!.LeadId);
        Assert.Equal(20, prospect.AccountId);
        Assert.Equal("Proposal", prospect.Stage);
        Assert.Equal("admin", prospect.ProspectOwner);
        Assert.Equal("Converted from lead", prospect.Description);
        Assert.Equal("Website", prospect.Source);
    }

    [Fact]
    public async Task Reimport_UpdatesStageWhenPresent()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.Prospects.Add(new Prospect
        {
            Id = 40,
            CompanyId = 1,
            Email = "dev@patel.com",
            FirstName = "Dev",
            LastName = "Patel",
            Stage = "Engaged",
            LeadId = 30
        });
        await db.SaveChangesAsync();

        var crm = NewCrm(db);
        var importer = new ProspectsCsvImporter(db, crm);
        var preview = new CsvImportPreview
        {
            EntityKey = "prospects",
            Rows =
            [
                new CsvRowValidation
                {
                    RowNumber = 2,
                    IsValid = true,
                    Action = "Update",
                    ExistingId = 40,
                    MatchKey = "dev@patel.com",
                    Values = new Dictionary<string, string>
                    {
                        ["Email"] = "dev@patel.com",
                        ["FirstName"] = "Dev",
                        ["LastName"] = "Patel",
                        ["Stage"] = "Qualified"
                    }
                }
            ]
        };

        var result = await importer.ApplyAsync(preview, 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var prospect = await crm.GetProspectAsync(40);
        Assert.Equal("Qualified", prospect!.Stage);
        Assert.Equal(30, prospect.LeadId);
    }

    [Fact]
    public async Task Reimport_StillBlocksSecondLeadConvert()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        await db.SaveChangesAsync();

        var crm = NewCrm(db);
        var lead = await crm.CreateLeadAsync(new Lead
        {
            FirstName = "Dev",
            LastName = "Patel",
            CompanyName = "Patel Co",
            Email = "dev@patel.com",
            Mobile = "9888777666"
        });

        var converted = await crm.ConvertLeadAsync(lead.Id, new LeadConversionRequest(
            CreateProspect: true, CreateAccount: true, CreateContact: false, CreateOpportunity: false));
        Assert.True(converted.Ok, converted.Error);
        Assert.Equal(1, await db.Prospects.CountAsync());
        Assert.Equal(1, await db.CrmAccounts.CountAsync());

        var prospect = await db.Prospects.AsNoTracking().SingleAsync();
        var importer = new ProspectsCsvImporter(db, crm);
        var result = await importer.ApplyAsync(HeaderOnlyUpdatePreview(lead.Email!, prospect.Id, lead.FirstName!), 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var second = await crm.ConvertLeadAsync(lead.Id, new LeadConversionRequest(
            CreateProspect: true, CreateAccount: true, CreateContact: false, CreateOpportunity: false));
        Assert.False(second.Ok);
        Assert.Contains("already converted", second.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.Prospects.CountAsync());
        Assert.Equal(lead.Id, (await db.Prospects.SingleAsync()).LeadId);
    }

    private static CsvImportPreview HeaderOnlyUpdatePreview(string email, int existingId, string firstName) =>
        new()
        {
            EntityKey = "prospects",
            Rows =
            [
                new CsvRowValidation
                {
                    RowNumber = 2,
                    IsValid = true,
                    Action = "Update",
                    ExistingId = existingId,
                    MatchKey = email,
                    Values = new Dictionary<string, string>
                    {
                        ["Email"] = email,
                        ["FirstName"] = firstName,
                        ["LastName"] = "Patel",
                        ["CompanyName"] = "Patel Co",
                        ["PhoneNumber"] = "9888777666",
                        ["Stage"] = "",
                        ["Source"] = "",
                        ["Industry"] = "",
                        ["Territory"] = "",
                        ["Tags"] = ""
                    }
                }
            ]
        };

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
