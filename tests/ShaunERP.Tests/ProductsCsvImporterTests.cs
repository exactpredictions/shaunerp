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

public class ProductsCsvImporterTests
{
    [Fact]
    public async Task Reimport_DoesNotReactivateDiscontinuedProduct()
    {
        var (crm, db) = CreateCrm();
        db.CrmProducts.Add(new CrmProduct
        {
            Id = 12,
            CompanyId = 1,
            ProductCode = "DEMO-ERP-LEGACY",
            Name = "Legacy Connector (retired)",
            ProductFamily = "Integration",
            Category = "Software",
            Description = "Retired SKU — do not sell",
            UnitPrice = 12000m,
            CurrencyCode = "USD",
            IsActive = false,
            UnitOfMeasure = "License",
            TaxRate = 18m,
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var csv = """
            ProductCode,Name,UnitPrice,CurrencyCode,Category,ProductFamily,UnitOfMeasure,TaxRate,Description
            DEMO-ERP-LEGACY,Legacy Connector (retired),12500.00,USD,Software,Integration,License,18,Retired SKU — do not sell
            """;
        var importer = new ProductsCsvImporter(db, crm);
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), companyId: 1);
        Assert.Equal("Update", preview.Rows[0].Action);
        Assert.True(preview.CanApply);

        var result = await importer.ApplyAsync(preview, companyId: 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Updated);

        var product = await db.CrmProducts.AsNoTracking().SingleAsync(p => p.Id == 12);
        Assert.False(product.IsActive);
        Assert.Equal(12500m, product.UnitPrice);
        Assert.Equal("USD", product.CurrencyCode);
        Assert.Equal("License", product.UnitOfMeasure);
        Assert.Equal(18m, product.TaxRate);
        Assert.Equal("Integration", product.ProductFamily);
        Assert.Equal("Retired SKU — do not sell", product.Description);
    }

    [Fact]
    public async Task Reimport_DoesNotWipeTaxRateOrCurrencyWhenThoseColumnsAreBlank()
    {
        var (crm, db) = CreateCrm();
        db.CrmProducts.Add(new CrmProduct
        {
            Id = 13,
            CompanyId = 1,
            ProductCode = "SKU-KEEP",
            Name = "Kept product",
            Category = "Services",
            ProductFamily = "Consulting",
            UnitPrice = 999m,
            CurrencyCode = "EUR",
            IsActive = false,
            UnitOfMeasure = "Hour",
            TaxRate = 5m,
            Description = "Keep me",
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var csv = """
            ProductCode,Name,UnitPrice,CurrencyCode,Category,ProductFamily,UnitOfMeasure,TaxRate,Description
            SKU-KEEP,Kept product,,,,,,,
            """;
        var importer = new ProductsCsvImporter(db, crm);
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), companyId: 1);
        var result = await importer.ApplyAsync(preview, companyId: 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var product = await db.CrmProducts.AsNoTracking().SingleAsync(p => p.Id == 13);
        Assert.False(product.IsActive);
        Assert.Equal(999m, product.UnitPrice);
        Assert.Equal("EUR", product.CurrencyCode);
        Assert.Equal("Hour", product.UnitOfMeasure);
        Assert.Equal(5m, product.TaxRate);
        Assert.Equal("Services", product.Category);
        Assert.Equal("Consulting", product.ProductFamily);
        Assert.Equal("Keep me", product.Description);
        Assert.Equal("Kept product", product.Name);
    }

    [Fact]
    public async Task Create_StillImportsANewActiveProduct()
    {
        var (crm, db) = CreateCrm();
        var csv = """
            ProductCode,Name,UnitPrice,CurrencyCode,Category,TaxRate,Description
            SAMPLE-PRD-001,SAMPLE Product A,999.00,INR,Services,18,SAMPLE product
            """;
        var importer = new ProductsCsvImporter(db, crm);
        var preview = await importer.PreviewAsync(CsvParser.Parse(csv), companyId: 1);
        Assert.Equal("Create", preview.Rows[0].Action);

        var result = await importer.ApplyAsync(preview, companyId: 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal(1, result.Created);

        var product = await db.CrmProducts.AsNoTracking().SingleAsync();
        Assert.Equal("SAMPLE-PRD-001", product.ProductCode);
        Assert.Equal("SAMPLE Product A", product.Name);
        Assert.Equal(999m, product.UnitPrice);
        Assert.Equal("INR", product.CurrencyCode);
        Assert.Equal(18m, product.TaxRate);
        Assert.True(product.IsActive);
        Assert.Equal("Each", product.UnitOfMeasure);
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
