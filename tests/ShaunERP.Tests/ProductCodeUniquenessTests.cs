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

public class ProductCodeUniquenessTests
{
    [Fact]
    public async Task CreateProduct_RejectsDuplicateCode_CaseInsensitive()
    {
        var (crm, _) = await CreateCrmAsync();
        await crm.CreateProductAsync(new CrmProduct { ProductCode = "CONSULT-1", Name = "Day rate", UnitPrice = 1000 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            crm.CreateProductAsync(new CrmProduct { ProductCode = "consult-1", Name = "Hour rate", UnitPrice = 150 }));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateProduct_RejectsCodeTakenByAnotherProduct()
    {
        var (crm, _) = await CreateCrmAsync();
        var first = await crm.CreateProductAsync(new CrmProduct { ProductCode = "SKU-A", Name = "A", UnitPrice = 10 });
        var second = await crm.CreateProductAsync(new CrmProduct { ProductCode = "SKU-B", Name = "B", UnitPrice = 20 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            crm.UpdateProductAsync(second.Id, new CrmProduct
            {
                ProductCode = "sku-a",
                Name = second.Name,
                UnitPrice = second.UnitPrice
            }));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);

        var kept = await crm.UpdateProductAsync(first.Id, new CrmProduct
        {
            ProductCode = "SKU-A",
            Name = "A renamed",
            UnitPrice = 11
        });
        Assert.NotNull(kept);
        Assert.Equal("A renamed", kept!.Name);
        Assert.Equal("SKU-A", kept.ProductCode);
    }

    [Fact]
    public async Task CreateProduct_RejectsBlankCode()
    {
        var (crm, _) = await CreateCrmAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            crm.CreateProductAsync(new CrmProduct { ProductCode = "  ", Name = "Blank", UnitPrice = 1 }));
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductsCsvPreview_DoesNotCrashWhenDuplicateCodesAlreadyExist()
    {
        var (_, db) = await CreateCrmAsync();
        db.CrmProducts.AddRange(
            new CrmProduct { CompanyId = 1, ProductCode = "DUP-1", Name = "First", UnitPrice = 10, CreatedDate = DateTime.UtcNow },
            new CrmProduct { CompanyId = 1, ProductCode = "dup-1", Name = "Second", UnitPrice = 20, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var importer = new ProductsCsvImporter(db, null!);
        var parsed = CsvParser.Parse("ProductCode,Name,UnitPrice\nDUP-1,Updated,30\n");

        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.True(preview.CanApply);
        Assert.Equal("Update", preview.Rows[0].Action);
        Assert.NotNull(preview.Rows[0].ExistingId);
    }

    [Fact]
    public async Task ProductsCsvPreview_RejectsDuplicateCodeInTheSameFile()
    {
        var (_, db) = await CreateCrmAsync();
        var importer = new ProductsCsvImporter(db, null!);
        var parsed = CsvParser.Parse(
            "ProductCode,Name,UnitPrice\nNEW-1,First,10\nnew-1,Second,20\n");

        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.Equal("Create", preview.Rows[0].Action);
        Assert.True(preview.Rows[0].IsValid);
        Assert.False(preview.Rows[1].IsValid);
        Assert.Contains(preview.Rows[1].Errors, e => e.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<(CrmService Crm, ShaunErpDbContext Db)> CreateCrmAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var crm = new CrmService(db, tenant, new ModuleRegistry(),
            new PartyIdentityService(db, new IdentityMatchService(db, new PhoneNormalizer(), new EmailNormalizer()),
                new PhoneNormalizer(), new EmailNormalizer()));
        return (crm, db);
    }
}
