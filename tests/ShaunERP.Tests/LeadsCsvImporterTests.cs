using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class LeadsCsvImporterTests
{
    [Fact]
    public async Task Reimport_DoesNotWipeQualificationCampaignOrIdentityFields()
    {
        var (crm, db) = CreateCrm();
        db.Campaigns.Add(new Campaign
        {
            Id = 7,
            CompanyId = 1,
            Name = "Q1 webinar",
            CampaignStatus = "Active",
            CampaignType = "Webinar"
        });
        db.Leads.Add(new Lead
        {
            Id = 40,
            CompanyId = 1,
            Email = "dev@patel.com",
            FirstName = "Dev",
            LastName = "Patel",
            CompanyName = "Patel Co",
            Phone = "+91-9000000099",
            Mobile = "+91-9000000100",
            Title = "CTO",
            Website = "https://patel.example",
            LeadSource = "Webinar",
            LeadStatus = "Qualified",
            IsQualified = true,
            Industry = "Manufacturing",
            LeadOwner = "priya",
            AnnualRevenue = 12_000_000,
            NoOfEmployees = 80,
            Rating = "Hot",
            Score = 92,
            Territory = "West",
            Street = "12 Sample Road",
            City = "Pune",
            State = "Maharashtra",
            ZipCode = "411001",
            Country = "India",
            Description = "Budget approved",
            CampaignId = 7,
            PartyId = 55,
            EmailOptOut = true,
            PreferredContactMethod = "Mobile",
            Gender = "M",
            Language = "en",
            Tags = "vip",
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var csv = """
            Email,FirstName,LastName,CompanyName,Phone,Mobile,LeadSource,LeadStatus,Industry,LeadOwner,City,State,Country,Tags
            dev@patel.com,Dev,Patel,Patel Co,+91-9000000099,+91-9000000100,Webinar,Qualified,Manufacturing,priya,Pune,Maharashtra,India,vip
            """;
        var importer = new LeadsCsvImporter(db, crm);
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), companyId: 1);
        Assert.Equal("Update", preview.Rows[0].Action);
        Assert.True(preview.CanApply);

        var result = await importer.ApplyAsync(preview, companyId: 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Updated);

        var lead = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == 40);
        Assert.True(lead.IsQualified);
        Assert.Equal("Qualified", lead.LeadStatus);
        Assert.Equal(7, lead.CampaignId);
        Assert.Equal(55, lead.PartyId);
        Assert.Equal("CTO", lead.Title);
        Assert.Equal("https://patel.example", lead.Website);
        Assert.Equal(12_000_000m, lead.AnnualRevenue);
        Assert.Equal(80, lead.NoOfEmployees);
        Assert.Equal("Hot", lead.Rating);
        Assert.Equal(92, lead.Score);
        Assert.Equal("West", lead.Territory);
        Assert.Equal("12 Sample Road", lead.Street);
        Assert.Equal("411001", lead.ZipCode);
        Assert.Equal("Budget approved", lead.Description);
        Assert.True(lead.EmailOptOut);
        Assert.Equal("Mobile", lead.PreferredContactMethod);
        Assert.Equal("M", lead.Gender);
        Assert.Equal("en", lead.Language);
        Assert.Equal("vip", lead.Tags);
        Assert.Equal("Pune", lead.City);
    }

    [Fact]
    public async Task Reimport_DoesNotResetStatusWhenStageColumnIsBlank()
    {
        var (crm, db) = CreateCrm();
        db.Leads.Add(new Lead
        {
            Id = 41,
            CompanyId = 1,
            Email = "amy@example.com",
            FirstName = "Amy",
            LastName = "Shah",
            LeadStatus = "Contacted",
            IsQualified = true,
            CampaignId = 3,
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var csv = """
            Email,FirstName,LastName,CompanyName,Phone,Mobile,LeadSource,LeadStatus,Industry,LeadOwner,City,State,Country,Tags
            amy@example.com,Amy,Shah,,,,,,,,,
            """;
        var importer = new LeadsCsvImporter(db, crm);
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), companyId: 1);
        var result = await importer.ApplyAsync(preview, companyId: 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var lead = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == 41);
        Assert.Equal("Contacted", lead.LeadStatus);
        Assert.True(lead.IsQualified);
        Assert.Equal(3, lead.CampaignId);
        Assert.Equal("Amy", lead.FirstName);
    }

    [Fact]
    public async Task Create_StillImportsANewLead()
    {
        var (crm, db) = CreateCrm();
        var csv = """
            Email,FirstName,LastName,CompanyName,Phone,LeadSource,LeadStatus,City,Country,Tags
            new.lead@example.com,SAMPLE,LeadOne,SAMPLE Co,+91-9000000001,Website,New,Chennai,India,import
            """;
        var importer = new LeadsCsvImporter(db, crm);
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), companyId: 1);
        Assert.Equal("Create", preview.Rows[0].Action);

        var result = await importer.ApplyAsync(preview, companyId: 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Created);

        var lead = await db.Leads.AsNoTracking().SingleAsync();
        Assert.Equal("new.lead@example.com", lead.Email);
        Assert.Equal("LeadOne", lead.LastName);
        Assert.Equal("New", lead.LeadStatus);
        Assert.False(lead.IsQualified);
        Assert.Null(lead.CampaignId);
    }

    private static (CrmService Crm, ShaunErpDbContext Db) CreateCrm()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.SaveChanges();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        var phones = new PhoneNormalizer();
        var emails = new EmailNormalizer();
        var crm = new CrmService(
            db,
            tenant,
            new ModuleRegistry(),
            new PartyIdentityService(db, new IdentityMatchService(db, phones, emails), phones, emails));
        return (crm, db);
    }
}
