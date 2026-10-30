using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.ImportExport;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport.Importers;

public sealed class ProductsCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly ICrmService _crm;

    public ProductsCsvImporter(ShaunErpDbContext db, ICrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public override string EntityKey => "products";
    public override string DisplayName => "Products";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "ProductCode", Header = "Product Code", Required = true, Example = "SAMPLE-PRD-001" },
        new() { Key = "Name", Header = "Name", Required = true, Example = "SAMPLE Product A" },
        new() { Key = "UnitPrice", Header = "Unit Price", Example = "999.00" },
        new() { Key = "CurrencyCode", Header = "Currency", Example = "INR" },
        new() { Key = "Category", Header = "Category", Example = "Services" },
        new() { Key = "ProductFamily", Header = "Family", Example = "Consulting" },
        new() { Key = "UnitOfMeasure", Header = "UoM", Example = "Each" },
        new() { Key = "TaxRate", Header = "Tax Rate", Example = "18" },
        new() { Key = "Description", Header = "Description", Example = "SAMPLE product description" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.CrmProducts.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .ToDictionaryAsync(p => p.ProductCode, p => p.Id, StringComparer.OrdinalIgnoreCase, ct);
        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var code = CsvValidator.Get(values, "ProductCode");
            if (!CsvValidator.TryRequired(code, "ProductCode", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "Name"), "Name", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "UnitPrice"), out _, out var e3) && e3 is not null) errors.Add(e3);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "TaxRate"), out _, out var e4) && e4 is not null) errors.Add(e4);
            int? id = null;
            var action = "Create";
            var warnings = new List<string>();
            if (!string.IsNullOrWhiteSpace(code) && existing.TryGetValue(code, out var eid))
            {
                id = eid;
                action = "Update";
                warnings.Add("CSV has no IsActive column — existing active/inactive status will be kept.");
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n, IsValid = errors.Count == 0, Action = action, Values = values,
                Errors = errors, Warnings = warnings, MatchKey = code, ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var created = 0; var updated = 0;
        var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                if (row.ExistingId is int id)
                {
                    var existing = await _crm.GetProductAsync(id, ct);
                    if (existing is null)
                        continue;

                    ApplyCsvFields(existing, row.Values, forCreate: false);
                    await _crm.UpdateProductAsync(id, existing, ct);
                    updated++;
                }
                else
                {
                    await _crm.CreateProductAsync(Map(row.Values, companyId), ct);
                    created++;
                }
            }

            if (tx is not null)
                await tx.CommitAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = true, Created = created, Updated = updated, Skipped = preview.InvalidCount };
        }
        catch (Exception ex)
        {
            if (tx is not null)
                await tx.RollbackAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = false, Messages = [ex.Message] };
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    public override async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        var items = await _db.CrmProducts.AsNoTracking().Where(p => p.CompanyId == request.CompanyId).OrderBy(p => p.ProductCode).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(p => new[]
        {
            p.ProductCode, p.Name, p.UnitPrice.ToString("0.##"), p.CurrencyCode, p.Category, p.ProductFamily,
            p.UnitOfMeasure, p.TaxRate?.ToString("0.##"), p.Description
        }.AsEnumerable());
        return new CsvExportResult { FileName = "products-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["ProductCode"] = "SAMPLE-PRD-001", ["Name"] = "SAMPLE Product A", ["UnitPrice"] = "999.00",
            ["CurrencyCode"] = "INR", ["Category"] = "Services", ["TaxRate"] = "18", ["Description"] = "SAMPLE product"
        },
        new Dictionary<string, string>
        {
            ["ProductCode"] = "SAMPLE-PRD-002", ["Name"] = "SAMPLE Product B", ["UnitPrice"] = "1499.00",
            ["CurrencyCode"] = "INR", ["Category"] = "Goods", ["TaxRate"] = "18"
        });

    private static CrmProduct Map(IReadOnlyDictionary<string, string> v, int companyId)
    {
        var product = new CrmProduct { CompanyId = companyId };
        ApplyCsvFields(product, v, forCreate: true);
        return product;
    }

    /// <summary>
    /// CSV columns are a subset of CrmProduct. Updates must not pass a sparse product into
    /// UpdateProductAsync — that copies IsActive=true (the CSV has no Active column) and
    /// reactivates discontinued SKUs on export/reimport, putting them back on invoice pickers.
    /// </summary>
    private static void ApplyCsvFields(CrmProduct product, IReadOnlyDictionary<string, string> v, bool forCreate)
    {
        product.ProductCode = CsvValidator.Get(v, "ProductCode");
        product.Name = CsvValidator.Get(v, "Name");

        CsvValidator.TryDecimal(CsvValidator.Get(v, "UnitPrice"), out var price, out _);
        if (price is not null)
            product.UnitPrice = price.Value;
        else if (forCreate)
            product.UnitPrice = 0;

        var currency = Null(CsvValidator.Get(v, "CurrencyCode"));
        if (currency is not null)
            product.CurrencyCode = currency;
        else if (forCreate)
            product.CurrencyCode = "INR";

        var category = Null(CsvValidator.Get(v, "Category"));
        if (category is not null || forCreate)
            product.Category = category;

        var family = Null(CsvValidator.Get(v, "ProductFamily"));
        if (family is not null || forCreate)
            product.ProductFamily = family;

        var uom = Null(CsvValidator.Get(v, "UnitOfMeasure"));
        if (uom is not null)
            product.UnitOfMeasure = uom;
        else if (forCreate)
            product.UnitOfMeasure = "Each";

        CsvValidator.TryDecimal(CsvValidator.Get(v, "TaxRate"), out var tax, out _);
        if (tax is not null)
            product.TaxRate = tax;
        else if (forCreate)
            product.TaxRate = tax;

        var description = Null(CsvValidator.Get(v, "Description"));
        if (description is not null || forCreate)
            product.Description = description;

        if (forCreate)
            product.IsActive = true;
    }

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class VendorsCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly IFinanceService _finance;

    public VendorsCsvImporter(ShaunErpDbContext db, IFinanceService finance)
    {
        _db = db;
        _finance = finance;
    }

    public override string EntityKey => "vendors";
    public override string DisplayName => "Vendors";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "Name", Header = "Name", Required = true, Example = "SAMPLE Vendor One" },
        new() { Key = "Email", Header = "Email", Example = "sample.vendor1@example.com" },
        new() { Key = "Phone", Header = "Phone", Example = "+91-9000000030" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var book = await _finance.GetBillingBookAsync(ct)
            ?? throw new InvalidOperationException("Billing book is required before importing vendors.");
        var existing = await _db.Vendors.AsNoTracking()
            .Where(v => v.CompanyMasterId == book.Id)
            .ToListAsync(ct);
        var byName = existing.GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var name = CsvValidator.Get(values, "Name");
            if (!CsvValidator.TryRequired(name, "Name", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryEmail(CsvValidator.Get(values, "Email"), out var e2) && e2 is not null) errors.Add(e2);
            int? id = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(name) && byName.TryGetValue(name, out var eid))
            {
                id = eid;
                action = "Update";
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n, IsValid = errors.Count == 0, Action = action, Values = values,
                Errors = errors, MatchKey = name, ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var book = await _finance.GetBillingBookAsync(ct)
            ?? throw new InvalidOperationException("Billing book is required.");
        var created = 0; var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                if (row.ExistingId is int id)
                {
                    var vendor = await _db.Vendors.FirstAsync(v => v.Id == id, ct);
                    vendor.Name = CsvValidator.Get(row.Values, "Name");
                    vendor.Email = Null(CsvValidator.Get(row.Values, "Email"));
                    vendor.Phone = Null(CsvValidator.Get(row.Values, "Phone"));
                    updated++;
                }
                else
                {
                    _db.Vendors.Add(new Vendor
                    {
                        CompanyMasterId = book.Id,
                        Name = CsvValidator.Get(row.Values, "Name"),
                        Email = Null(CsvValidator.Get(row.Values, "Email")),
                        Phone = Null(CsvValidator.Get(row.Values, "Phone"))
                    });
                    created++;
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = true, Created = created, Updated = updated, Skipped = preview.InvalidCount };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = false, Messages = [ex.Message] };
        }
    }

    public override async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        var book = await _finance.GetBillingBookAsync(ct);
        if (book is null)
        {
            return new CsvExportResult { FileName = "vendors-export.csv", CsvContent = CsvParser.ToCsv(Columns.Select(c => c.Key), Array.Empty<IEnumerable<string?>>()), RowCount = 0 };
        }

        var items = await _db.Vendors.AsNoTracking().Where(v => v.CompanyMasterId == book.Id).OrderBy(v => v.Name).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(v => new[] { v.Name, v.Email, v.Phone }.AsEnumerable());
        return new CsvExportResult { FileName = "vendors-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string> { ["Name"] = "SAMPLE Vendor One", ["Email"] = "sample.vendor1@example.com", ["Phone"] = "+91-9000000030" },
        new Dictionary<string, string> { ["Name"] = "SAMPLE Vendor Two", ["Email"] = "sample.vendor2@example.com" });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class LedgersCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly IAccountingService _accounting;

    public LedgersCsvImporter(ShaunErpDbContext db, IAccountingService accounting)
    {
        _db = db;
        _accounting = accounting;
    }

    public override string EntityKey => "ledgers";
    public override string DisplayName => "Chart of accounts / Ledgers";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "LedgerCode", Header = "Ledger Code", Required = true, Example = "SAMPLE-LED-001" },
        new() { Key = "LedgerName", Header = "Ledger Name", Required = true, Example = "SAMPLE Expense Ledger" },
        new() { Key = "GroupCode", Header = "Group Code", Required = true, Example = "EXP" },
        new() { Key = "OpeningBalance", Header = "Opening Balance", Example = "0" },
        new() { Key = "OpeningBalanceType", Header = "Opening Side (Debit/Credit)", Example = "Debit" },
        new() { Key = "Gstin", Header = "GSTIN", Example = "" },
        new() { Key = "Pan", Header = "PAN", Example = "" },
        new() { Key = "IsCashLedger", Header = "Is Cash", Example = "false" },
        new() { Key = "IsBankLedger", Header = "Is Bank", Example = "false" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        await _accounting.EnsureBootstrapAsync(ct);
        var existing = await _db.Ledgers.AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .ToDictionaryAsync(l => l.LedgerCode, l => l.Id, StringComparer.OrdinalIgnoreCase, ct);
        var groups = await _db.AccountGroups.AsNoTracking()
            .Where(g => g.CompanyId == companyId)
            .ToDictionaryAsync(g => g.GroupCode, g => g, StringComparer.OrdinalIgnoreCase, ct);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var code = CsvValidator.Get(values, "LedgerCode");
            var groupCode = CsvValidator.Get(values, "GroupCode");
            if (!CsvValidator.TryRequired(code, "LedgerCode", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "LedgerName"), "LedgerName", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryRequired(groupCode, "GroupCode", out var e3)) errors.Add(e3!);
            if (!string.IsNullOrWhiteSpace(groupCode) && !groups.ContainsKey(groupCode))
            {
                errors.Add($"Account group '{groupCode}' not found.");
            }

            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "OpeningBalance"), out _, out var e4) && e4 is not null) errors.Add(e4);
            if (!CsvValidator.TryGstin(CsvValidator.Get(values, "Gstin"), out var e5) && e5 is not null) errors.Add(e5);
            if (!CsvValidator.TryPan(CsvValidator.Get(values, "Pan"), out var e6) && e6 is not null) errors.Add(e6);

            int? id = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(code) && existing.TryGetValue(code, out var eid))
            {
                id = eid;
                action = "Update";
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n, IsValid = errors.Count == 0, Action = action, Values = values,
                Errors = errors, MatchKey = code, ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        await _accounting.EnsureBootstrapAsync(ct);
        var groups = await _db.AccountGroups
            .Where(g => g.CompanyId == companyId)
            .ToDictionaryAsync(g => g.GroupCode, g => g, StringComparer.OrdinalIgnoreCase, ct);
        var created = 0; var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                var group = groups[CsvValidator.Get(row.Values, "GroupCode")];
                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "OpeningBalance"), out var ob, out _);
                var sideText = CsvValidator.Get(row.Values, "OpeningBalanceType");
                var side = sideText.Equals("Credit", StringComparison.OrdinalIgnoreCase)
                    ? BalanceSide.Credit
                    : BalanceSide.Debit;
                var ledger = new Ledger
                {
                    CompanyId = companyId,
                    LedgerCode = CsvValidator.Get(row.Values, "LedgerCode"),
                    LedgerName = CsvValidator.Get(row.Values, "LedgerName"),
                    AccountGroupId = group.Id,
                    AccountType = group.Nature,
                    OpeningBalance = ob ?? 0,
                    OpeningBalanceType = side,
                    Gstin = Null(CsvValidator.Get(row.Values, "Gstin")),
                    Pan = Null(CsvValidator.Get(row.Values, "Pan")),
                    IsCashLedger = Bool(CsvValidator.Get(row.Values, "IsCashLedger")),
                    IsBankLedger = Bool(CsvValidator.Get(row.Values, "IsBankLedger")),
                    IsActive = true
                };
                if (row.ExistingId is int id)
                {
                    ledger.Id = id;
                }

                await _accounting.UpsertLedgerAsync(ledger, ct);
                if (row.ExistingId is null) created++;
                else updated++;
            }

            await tx.CommitAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = true, Created = created, Updated = updated, Skipped = preview.InvalidCount };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = false, Messages = [ex.Message] };
        }
    }

    public override async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        var items = await _db.Ledgers.AsNoTracking().Include(l => l.AccountGroup)
            .Where(l => l.CompanyId == request.CompanyId).OrderBy(l => l.LedgerCode).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(l => new[]
        {
            l.LedgerCode, l.LedgerName, l.AccountGroup?.GroupCode, l.OpeningBalance.ToString("0.##"),
            l.OpeningBalanceType.ToString(), l.Gstin, l.Pan, l.IsCashLedger.ToString(), l.IsBankLedger.ToString()
        }.AsEnumerable());
        return new CsvExportResult { FileName = "ledgers-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["LedgerCode"] = "SAMPLE-LED-001", ["LedgerName"] = "SAMPLE Expense Ledger", ["GroupCode"] = "EXP",
            ["OpeningBalance"] = "0", ["OpeningBalanceType"] = "Debit", ["IsCashLedger"] = "false", ["IsBankLedger"] = "false"
        },
        new Dictionary<string, string>
        {
            ["LedgerCode"] = "SAMPLE-LED-002", ["LedgerName"] = "SAMPLE Bank Ledger", ["GroupCode"] = "BANK",
            ["OpeningBalance"] = "10000", ["OpeningBalanceType"] = "Debit", ["IsCashLedger"] = "false", ["IsBankLedger"] = "true"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static bool Bool(string? v) =>
        string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1" || string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);
}
