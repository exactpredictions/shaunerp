using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class InventoryCsvImporterTests
{
    [Fact]
    public async Task Preview_DoesNotThrow_WhenCaseVariantItemCodesAlreadyExist()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 1, Name = "Tenant" });
        db.InventoryItems.AddRange(
            new InventoryItem { CompanyId = 1, ItemCode = "ITEM-001", ItemName = "Upper" },
            new InventoryItem { CompanyId = 1, ItemCode = "item-001", ItemName = "Lower" });
        await db.SaveChangesAsync();

        var importer = new InventoryItemsCsvImporter(db);
        var parsed = CsvParser.Parse("ItemCode,ItemName\nITEM-002,Other\n");

        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.True(preview.CanApply);
        Assert.Equal("Create", preview.Rows[0].Action);
    }

    [Fact]
    public async Task Preview_Rejects_DuplicateItemCodeInTheSameFile()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 1, Name = "Tenant" });
        await db.SaveChangesAsync();

        var importer = new InventoryItemsCsvImporter(db);
        var parsed = CsvParser.Parse(
            "ItemCode,ItemName,ItemType,HsnCode,IsInventoryItem\n" +
            "SKU-1,Widget,Goods,8471,true\n" +
            "sku-1,Widget copy,Goods,8471,true\n");

        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.True(preview.Rows[0].IsValid);
        Assert.Equal("Create", preview.Rows[0].Action);
        Assert.False(preview.Rows[1].IsValid);
        Assert.Contains(preview.Rows[1].Errors, e => e.Contains("duplicated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Apply_CreatesOnlyOneItem_WhenPreviewBypassesInFileDuplicate()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 1, Name = "Tenant" });
        await db.SaveChangesAsync();

        var importer = new InventoryItemsCsvImporter(db);
        var parsed = CsvParser.Parse(
            "ItemCode,ItemName\nSKU-1,Widget\nsku-1,Other\n");
        var preview = await importer.PreviewAsync(parsed, companyId: 1);
        // Force both rows valid as if a stale preview skipped the in-file check.
        var forced = new ShaunERP.Core.ImportExport.CsvImportPreview
        {
            EntityKey = importer.EntityKey,
            Headers = parsed.Headers,
            Rows = parsed.Rows.Select((values, i) => new ShaunERP.Core.ImportExport.CsvRowValidation
            {
                RowNumber = i + 2,
                IsValid = true,
                Action = "Create",
                Values = values,
                MatchKey = CsvValidator.Get(values, "ItemCode")
            }).ToList()
        };

        var result = await importer.ApplyAsync(forced, companyId: 1);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, await db.InventoryItems.CountAsync(i => i.CompanyId == 1));
    }

    private static ShaunErpDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}
