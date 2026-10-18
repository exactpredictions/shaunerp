using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class ContactsCsvImporterTests
{
    [Fact]
    public async Task Update_without_account_number_keeps_existing_customer_link()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db);
        db.CrmCustomers.Add(new Customer
        {
            Id = 10,
            CompanyId = 1,
            CompanyName = "Acme Retail",
            AccountNumber = null
        });
        db.Contacts.Add(new Contact
        {
            Id = 20,
            CompanyId = 1,
            FirstName = "Priya",
            LastName = "Nair",
            Email = "priya@acme.test",
            Phone = "+91-9000000099",
            CustomerId = 10
        });
        await db.SaveChangesAsync();

        var importer = new ContactsCsvImporter(db, crm);
        var export = await importer.ExportAsync(new CsvExportRequest { CompanyId = 1 });
        var parsed = CsvParser.Parse(export.CsvContent);
        var preview = await importer.PreviewAsync(parsed, 1);

        Assert.Equal("Update", preview.Rows[0].Action);
        Assert.True(preview.CanApply);

        var result = await importer.ApplyAsync(preview, 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Updated);

        var contact = await db.Contacts.AsNoTracking().SingleAsync(c => c.Id == 20);
        Assert.Equal(10, contact.CustomerId);
        Assert.Equal("priya@acme.test", contact.Email);
    }

    [Fact]
    public async Task Update_with_account_number_relinks_customer()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db);
        db.CrmCustomers.AddRange(
            new Customer { Id = 10, CompanyId = 1, CompanyName = "Acme Retail", AccountNumber = "CUST-ACME" },
            new Customer { Id = 11, CompanyId = 1, CompanyName = "Beta Labs", AccountNumber = "CUST-BETA" });
        db.Contacts.Add(new Contact
        {
            Id = 20,
            CompanyId = 1,
            FirstName = "Priya",
            LastName = "Nair",
            Email = "priya@acme.test",
            CustomerId = 10
        });
        await db.SaveChangesAsync();

        var csv = """
            Email,FirstName,LastName,Phone,Mobile,Title,Department,AccountNumber,Tags
            priya@acme.test,Priya,Nair,,,,,CUST-BETA,
            """;
        var importer = new ContactsCsvImporter(db, crm);
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), 1);
        var result = await importer.ApplyAsync(preview, 1);

        Assert.True(result.Success, string.Join("; ", result.Messages));
        var contact = await db.Contacts.AsNoTracking().SingleAsync(c => c.Id == 20);
        Assert.Equal(11, contact.CustomerId);
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
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
        var phones = new PhoneNormalizer();
        var emails = new EmailNormalizer();
        var parties = new PartyIdentityService(db, new IdentityMatchService(db, phones, emails), phones, emails);
        return new CrmService(db, tenant, new ModuleRegistry(), parties);
    }
}
