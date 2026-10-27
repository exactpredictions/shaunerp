using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport.Importers;

/// <summary>Header-only invoice import. Validates customer FK; lines should be added in UI.</summary>
public sealed class InvoicesCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly IFinanceService _finance;

    public InvoicesCsvImporter(ShaunErpDbContext db, IFinanceService finance)
    {
        _db = db;
        _finance = finance;
    }

    public override string EntityKey => "invoices";
    public override string DisplayName => "Invoices (header)";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "InvoiceNumber", Header = "Invoice Number", Required = true, Example = "SAMPLE-INV-001" },
        new() { Key = "CustomerAccountNumber", Header = "Customer Account Number", Required = true, Example = "SAMPLE-CUST-001" },
        new() { Key = "InvoiceDate", Header = "Invoice Date", Required = true, Example = "2026-04-01" },
        new() { Key = "DueDate", Header = "Due Date", Example = "2026-04-30" },
        new() { Key = "CurrencyCode", Header = "Currency", Example = "INR" },
        new() { Key = "PaymentTerms", Header = "Payment Terms", Example = "Net 30" },
        new() { Key = "Notes", Header = "Notes", Example = "SAMPLE header import" },
        new() { Key = "GrandTotal", Header = "Grand Total", Example = "1180.00" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId)
            .ToDictionaryAsync(i => i.InvoiceNumber, i => i.Id, StringComparer.OrdinalIgnoreCase, ct);
        var customers = await CustomerAccountLookup.LoadAsync(_db, companyId, ct);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var warnings = new List<string> { "Header-only import: add line items after create." };
            var number = CsvValidator.Get(values, "InvoiceNumber");
            var acct = CsvValidator.Get(values, "CustomerAccountNumber");
            if (!CsvValidator.TryRequired(number, "InvoiceNumber", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(acct, "CustomerAccountNumber", out var e2)) errors.Add(e2!);
            if (customers.IsDuplicate(acct))
            {
                errors.Add(CustomerAccountIndex.DuplicateError(acct));
            }
            else if (!string.IsNullOrWhiteSpace(acct) && !customers.TryGetUnique(acct, out _))
            {
                errors.Add($"Customer account '{acct}' not found.");
            }

            if (!CsvValidator.TryDate(CsvValidator.Get(values, "InvoiceDate"), out _, out var e3) && e3 is not null) errors.Add(e3);
            if (!CsvValidator.TryDate(CsvValidator.Get(values, "DueDate"), out _, out var e4) && e4 is not null) errors.Add(e4);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "GrandTotal"), out _, out var e5) && e5 is not null) errors.Add(e5);

            int? id = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(number) && existing.TryGetValue(number, out var eid))
            {
                id = eid;
                action = "Update";
                warnings.Add("Updating existing invoice header only.");
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n, IsValid = errors.Count == 0, Action = action, Values = values,
                Errors = errors, Warnings = warnings, MatchKey = number, ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var customers = await CustomerAccountLookup.LoadAsync(_db, companyId, ct);
        var created = 0; var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                if (!customers.TryGetUnique(CsvValidator.Get(row.Values, "CustomerAccountNumber"), out var customer))
                    continue;
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "InvoiceDate"), out var invDate, out _);
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "DueDate"), out var due, out _);
                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "GrandTotal"), out var total, out _);
                var invoice = new Invoice
                {
                    CompanyId = companyId,
                    InvoiceNumber = CsvValidator.Get(row.Values, "InvoiceNumber"),
                    CustomerId = customer.Id,
                    CustomerName = customer.CompanyName,
                    InvoiceDate = invDate ?? DateTime.UtcNow.Date,
                    DueDate = due,
                    CurrencyCode = Null(CsvValidator.Get(row.Values, "CurrencyCode")) ?? "INR",
                    PaymentTerms = Null(CsvValidator.Get(row.Values, "PaymentTerms")),
                    Notes = Null(CsvValidator.Get(row.Values, "Notes")),
                    GrandTotal = total ?? 0,
                    Amount = total ?? 0,
                    InvoiceStatus = InvoiceStatus.Draft
                };

                if (row.ExistingId is int id)
                {
                    await _finance.UpdateInvoiceAsync(id, invoice, ct);
                    updated++;
                }
                else
                {
                    await _finance.CreateInvoiceAsync(invoice, ct);
                    created++;
                }
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
        var items = await _db.Invoices.AsNoTracking().Include(i => i.Customer)
            .Where(i => i.CompanyId == request.CompanyId).OrderByDescending(i => i.InvoiceDate).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(i => new[]
        {
            i.InvoiceNumber, i.Customer?.AccountNumber, i.InvoiceDate.ToString("yyyy-MM-dd"),
            i.DueDate?.ToString("yyyy-MM-dd"), i.CurrencyCode, i.PaymentTerms, i.Notes,
            (i.GrandTotal > 0 ? i.GrandTotal : i.Amount).ToString("0.##")
        }.AsEnumerable());
        return new CsvExportResult { FileName = "invoices-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["InvoiceNumber"] = "SAMPLE-INV-001", ["CustomerAccountNumber"] = "SAMPLE-CUST-001",
            ["InvoiceDate"] = "2026-04-01", ["DueDate"] = "2026-04-30", ["CurrencyCode"] = "INR",
            ["PaymentTerms"] = "Net 30", ["Notes"] = "SAMPLE header import", ["GrandTotal"] = "1180.00"
        },
        new Dictionary<string, string>
        {
            ["InvoiceNumber"] = "SAMPLE-INV-002", ["CustomerAccountNumber"] = "SAMPLE-CUST-002",
            ["InvoiceDate"] = "2026-04-05", ["CurrencyCode"] = "INR", ["GrandTotal"] = "590.00"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class PaymentsCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly IFinanceService _finance;

    public PaymentsCsvImporter(ShaunErpDbContext db, IFinanceService finance)
    {
        _db = db;
        _finance = finance;
    }

    public override string EntityKey => "payments";
    public override string DisplayName => "Invoice payments";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "InvoiceNumber", Header = "Invoice Number", Required = true, Example = "SAMPLE-INV-001" },
        new() { Key = "Amount", Header = "Amount", Required = true, Example = "500.00" },
        new() { Key = "PaymentDate", Header = "Payment Date", Required = true, Example = "2026-04-10" },
        new() { Key = "Method", Header = "Method", Example = "Bank transfer" },
        new() { Key = "Reference", Header = "Reference", Example = "SAMPLE-UTR-001" },
        new() { Key = "Notes", Header = "Notes", Example = "SAMPLE payment" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId)
            .ToDictionaryAsync(i => i.InvoiceNumber, i => i.Id, StringComparer.OrdinalIgnoreCase, ct);
        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var number = CsvValidator.Get(values, "InvoiceNumber");
            if (!CsvValidator.TryRequired(number, "InvoiceNumber", out var e1)) errors.Add(e1!);
            if (!string.IsNullOrWhiteSpace(number) && !invoices.ContainsKey(number))
            {
                errors.Add($"Invoice '{number}' not found.");
            }

            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "Amount"), "Amount", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "Amount"), out var amt, out var e3) && e3 is not null) errors.Add(e3);
            if (amt is <= 0) errors.Add("Amount must be greater than zero.");
            if (!CsvValidator.TryDate(CsvValidator.Get(values, "PaymentDate"), out _, out var e4) && e4 is not null) errors.Add(e4);

            rows.Add(new CsvRowValidation
            {
                RowNumber = n, IsValid = errors.Count == 0, Action = "Create", Values = values,
                Errors = errors, MatchKey = number,
                ExistingId = !string.IsNullOrWhiteSpace(number) && invoices.TryGetValue(number, out var iid) ? iid : null
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var created = 0; var failed = 0;
        var messages = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "Amount"), out var amount, out _);
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "PaymentDate"), out var date, out _);
                var (ok, error, _) = await _finance.RecordPaymentAsync(
                    row.ExistingId!.Value,
                    new InvoicePayment
                    {
                        Amount = amount ?? 0,
                        PaymentDate = date ?? DateTime.UtcNow.Date,
                        Method = Null(CsvValidator.Get(row.Values, "Method")) ?? "Bank transfer",
                        Reference = Null(CsvValidator.Get(row.Values, "Reference")),
                        Notes = Null(CsvValidator.Get(row.Values, "Notes"))
                    },
                    ct);
                if (ok) created++;
                else
                {
                    failed++;
                    messages.Add($"Row {row.RowNumber}: {error}");
                }
            }

            await tx.CommitAsync(ct);
            return new CsvImportResult
            {
                EntityKey = EntityKey, Success = failed == 0, Created = created, Failed = failed,
                Skipped = preview.InvalidCount, Messages = messages
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = false, Messages = [ex.Message] };
        }
    }

    public override async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        var items = await _db.InvoicePayments.AsNoTracking().Include(p => p.Invoice)
            .Where(p => p.CompanyId == request.CompanyId)
            .OrderByDescending(p => p.PaymentDate).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(p => new[]
        {
            p.Invoice?.InvoiceNumber, p.Amount.ToString("0.##"), p.PaymentDate.ToString("yyyy-MM-dd"),
            p.Method, p.Reference, p.Notes
        }.AsEnumerable());
        return new CsvExportResult { FileName = "payments-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["InvoiceNumber"] = "SAMPLE-INV-001", ["Amount"] = "500.00", ["PaymentDate"] = "2026-04-10",
            ["Method"] = "Bank transfer", ["Reference"] = "SAMPLE-UTR-001", ["Notes"] = "SAMPLE payment"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class SalesOrdersCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly ISalesOrderService _orders;

    public SalesOrdersCsvImporter(ShaunErpDbContext db, ISalesOrderService orders)
    {
        _db = db;
        _orders = orders;
    }

    public override string EntityKey => "sales-orders";
    public override string DisplayName => "Sales orders (header)";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "OrderNumber", Header = "Order Number", Required = true, Example = "SAMPLE-SO-001" },
        new() { Key = "CustomerAccountNumber", Header = "Customer Account Number", Required = true, Example = "SAMPLE-CUST-001" },
        new() { Key = "OrderDate", Header = "Order Date", Required = true, Example = "2026-04-01" },
        new() { Key = "CurrencyCode", Header = "Currency", Example = "INR" },
        new() { Key = "PaymentTerms", Header = "Payment Terms", Example = "Net 30" },
        new() { Key = "Notes", Header = "Notes", Example = "SAMPLE order header" },
        new() { Key = "GrandTotal", Header = "Grand Total", Example = "2000.00" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.SalesOrders.AsNoTracking()
            .Where(o => o.CompanyId == companyId)
            .ToDictionaryAsync(o => o.OrderNumber, o => o.Id, StringComparer.OrdinalIgnoreCase, ct);
        var customers = await CustomerAccountLookup.LoadAsync(_db, companyId, ct);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var warnings = new List<string> { "Header-only import: add line items after create." };
            var number = CsvValidator.Get(values, "OrderNumber");
            var acct = CsvValidator.Get(values, "CustomerAccountNumber");
            if (!CsvValidator.TryRequired(number, "OrderNumber", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(acct, "CustomerAccountNumber", out var e2)) errors.Add(e2!);
            if (customers.IsDuplicate(acct))
                errors.Add(CustomerAccountIndex.DuplicateError(acct));
            else if (!string.IsNullOrWhiteSpace(acct) && !customers.TryGetUnique(acct, out _))
                errors.Add($"Customer account '{acct}' not found.");
            if (!CsvValidator.TryDate(CsvValidator.Get(values, "OrderDate"), out _, out var e3) && e3 is not null) errors.Add(e3);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "GrandTotal"), out _, out var e4) && e4 is not null) errors.Add(e4);

            int? id = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(number) && existing.TryGetValue(number, out var eid))
            {
                id = eid;
                action = "Update";
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n, IsValid = errors.Count == 0, Action = action, Values = values,
                Errors = errors, Warnings = warnings, MatchKey = number, ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var customers = await CustomerAccountLookup.LoadAsync(_db, companyId, ct);
        var created = 0; var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                if (!customers.TryGetUnique(CsvValidator.Get(row.Values, "CustomerAccountNumber"), out var customer))
                    continue;
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "OrderDate"), out var orderDate, out _);
                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "GrandTotal"), out var total, out _);
                var order = new SalesOrder
                {
                    CompanyId = companyId,
                    OrderNumber = CsvValidator.Get(row.Values, "OrderNumber"),
                    CustomerId = customer.Id,
                    CustomerName = customer.CompanyName,
                    OrderDate = orderDate ?? DateTime.UtcNow.Date,
                    CurrencyCode = Null(CsvValidator.Get(row.Values, "CurrencyCode")) ?? "INR",
                    PaymentTerms = Null(CsvValidator.Get(row.Values, "PaymentTerms")),
                    Notes = Null(CsvValidator.Get(row.Values, "Notes")),
                    GrandTotal = total ?? 0,
                    OrderStatus = SalesOrderStatus.Draft
                };
                if (row.ExistingId is int id)
                {
                    await _orders.UpdateOrderAsync(id, order, ct);
                    updated++;
                }
                else
                {
                    await _orders.CreateOrderAsync(order, ct);
                    created++;
                }
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
        var items = await _db.SalesOrders.AsNoTracking().Include(o => o.Customer)
            .Where(o => o.CompanyId == request.CompanyId).OrderByDescending(o => o.OrderDate).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(o => new[]
        {
            o.OrderNumber, o.Customer?.AccountNumber, o.OrderDate.ToString("yyyy-MM-dd"),
            o.CurrencyCode, o.PaymentTerms, o.Notes, o.GrandTotal.ToString("0.##")
        }.AsEnumerable());
        return new CsvExportResult { FileName = "sales-orders-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["OrderNumber"] = "SAMPLE-SO-001", ["CustomerAccountNumber"] = "SAMPLE-CUST-001",
            ["OrderDate"] = "2026-04-01", ["CurrencyCode"] = "INR", ["PaymentTerms"] = "Net 30",
            ["Notes"] = "SAMPLE order header", ["GrandTotal"] = "2000.00"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class InventoryItemsCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;

    public InventoryItemsCsvImporter(ShaunErpDbContext db) => _db = db;

    public override string EntityKey => "inventory-items";
    public override string DisplayName => "Inventory items";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "ItemCode", Header = "Item Code", Required = true, Example = "SAMPLE-ITEM-001" },
        new() { Key = "ItemName", Header = "Item Name", Required = true, Example = "SAMPLE Stock Item" },
        new() { Key = "ItemType", Header = "Item Type", Example = "Goods" },
        new() { Key = "HsnCode", Header = "HSN Code", Example = "8471" },
        new() { Key = "IsInventoryItem", Header = "Is Inventory", Example = "true" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.CompanyId == companyId)
            .ToDictionaryAsync(i => i.ItemCode, i => i.Id, StringComparer.OrdinalIgnoreCase, ct);
        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var code = CsvValidator.Get(values, "ItemCode");
            if (!CsvValidator.TryRequired(code, "ItemCode", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "ItemName"), "ItemName", out var e2)) errors.Add(e2!);
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
        var created = 0; var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                if (row.ExistingId is int id)
                {
                    var item = await _db.InventoryItems.FirstAsync(i => i.Id == id && i.CompanyId == companyId, ct);
                    item.ItemName = CsvValidator.Get(row.Values, "ItemName");
                    item.ItemType = Null(CsvValidator.Get(row.Values, "ItemType")) ?? "Goods";
                    item.HsnCode = Null(CsvValidator.Get(row.Values, "HsnCode"));
                    item.IsInventoryItem = Bool(CsvValidator.Get(row.Values, "IsInventoryItem"), true);
                    updated++;
                }
                else
                {
                    _db.InventoryItems.Add(new InventoryItem
                    {
                        CompanyId = companyId,
                        ItemCode = CsvValidator.Get(row.Values, "ItemCode"),
                        ItemName = CsvValidator.Get(row.Values, "ItemName"),
                        ItemType = Null(CsvValidator.Get(row.Values, "ItemType")) ?? "Goods",
                        HsnCode = Null(CsvValidator.Get(row.Values, "HsnCode")),
                        IsInventoryItem = Bool(CsvValidator.Get(row.Values, "IsInventoryItem"), true),
                        IsActive = true
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
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.CompanyId == request.CompanyId).OrderBy(i => i.ItemCode).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(i => new[]
        {
            i.ItemCode, i.ItemName, i.ItemType, i.HsnCode, i.IsInventoryItem.ToString()
        }.AsEnumerable());
        return new CsvExportResult { FileName = "inventory-items-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["ItemCode"] = "SAMPLE-ITEM-001", ["ItemName"] = "SAMPLE Stock Item", ["ItemType"] = "Goods",
            ["HsnCode"] = "8471", ["IsInventoryItem"] = "true"
        },
        new Dictionary<string, string>
        {
            ["ItemCode"] = "SAMPLE-ITEM-002", ["ItemName"] = "SAMPLE Service Item", ["ItemType"] = "Service",
            ["IsInventoryItem"] = "false"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static bool Bool(string? v, bool defaultValue) =>
        string.IsNullOrWhiteSpace(v)
            ? defaultValue
            : string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1" || string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Opening balances import via ledger OpeningBalance fields (not voucher posting).
/// For complex voucher-based opening balances, use Accounting UI CreateOpeningBalancesAsync.
/// </summary>
public sealed class OpeningBalancesCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly IAccountingService _accounting;

    public OpeningBalancesCsvImporter(ShaunErpDbContext db, IAccountingService accounting)
    {
        _db = db;
        _accounting = accounting;
    }

    public override string EntityKey => "opening-balances";
    public override string DisplayName => "Opening balances (ledger fields)";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "LedgerCode", Header = "Ledger Code", Required = true, Example = "SAMPLE-LED-001" },
        new() { Key = "OpeningBalance", Header = "Opening Balance", Required = true, Example = "1000.00" },
        new() { Key = "OpeningBalanceType", Header = "Side (Debit/Credit)", Required = true, Example = "Debit" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var ledgers = await _db.Ledgers.AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .ToDictionaryAsync(l => l.LedgerCode, l => l, StringComparer.OrdinalIgnoreCase, ct);
        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var warnings = new List<string>
            {
                "Updates ledger OpeningBalance fields only. For voucher-based OB use Accounting > Opening balances."
            };
            var code = CsvValidator.Get(values, "LedgerCode");
            if (!CsvValidator.TryRequired(code, "LedgerCode", out var e1)) errors.Add(e1!);
            if (!string.IsNullOrWhiteSpace(code) && !ledgers.ContainsKey(code))
            {
                errors.Add($"Ledger '{code}' not found.");
            }

            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "OpeningBalance"), "OpeningBalance", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "OpeningBalance"), out _, out var e3) && e3 is not null) errors.Add(e3);
            var side = CsvValidator.Get(values, "OpeningBalanceType");
            if (!side.Equals("Debit", StringComparison.OrdinalIgnoreCase) &&
                !side.Equals("Credit", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("OpeningBalanceType must be Debit or Credit.");
            }

            int? id = null;
            if (!string.IsNullOrWhiteSpace(code) && ledgers.TryGetValue(code, out var ledger))
            {
                id = ledger.Id;
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n, IsValid = errors.Count == 0, Action = "Update", Values = values,
                Errors = errors, Warnings = warnings, MatchKey = code, ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                var ledger = await _db.Ledgers.FirstAsync(l => l.Id == row.ExistingId && l.CompanyId == companyId, ct);
                if (ledger.IsSystemLedger)
                {
                    // Still allow OB on system ledgers — common for cash/bank.
                }

                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "OpeningBalance"), out var ob, out _);
                var side = CsvValidator.Get(row.Values, "OpeningBalanceType");
                ledger.OpeningBalance = ob ?? 0;
                ledger.OpeningBalanceType = side.Equals("Credit", StringComparison.OrdinalIgnoreCase)
                    ? BalanceSide.Credit
                    : BalanceSide.Debit;
                await _accounting.UpsertLedgerAsync(ledger, ct);
                updated++;
            }

            await tx.CommitAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = true, Updated = updated, Skipped = preview.InvalidCount };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = false, Messages = [ex.Message] };
        }
    }

    public override async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        var items = await _db.Ledgers.AsNoTracking()
            .Where(l => l.CompanyId == request.CompanyId)
            .OrderBy(l => l.LedgerCode).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(l => new[]
        {
            l.LedgerCode, l.OpeningBalance.ToString("0.##"), l.OpeningBalanceType.ToString()
        }.AsEnumerable());
        return new CsvExportResult { FileName = "opening-balances-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["LedgerCode"] = "SAMPLE-LED-001", ["OpeningBalance"] = "1000.00", ["OpeningBalanceType"] = "Debit"
        },
        new Dictionary<string, string>
        {
            ["LedgerCode"] = "SAMPLE-LED-002", ["OpeningBalance"] = "5000.00", ["OpeningBalanceType"] = "Debit"
        });
}
