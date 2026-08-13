using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.ImportExport;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport.Importers;

public sealed class CustomersCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly ICrmService _crm;

    public CustomersCsvImporter(ShaunErpDbContext db, ICrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public override string EntityKey => "customers";
    public override string DisplayName => "Customers";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "AccountNumber", Header = "Account Number", Required = true, Example = "SAMPLE-CUST-001" },
        new() { Key = "CompanyName", Header = "Company Name", Required = true, Example = "SAMPLE Acme India Pvt Ltd" },
        new() { Key = "PrimaryContact", Header = "Primary Contact", Example = "SAMPLE Contact" },
        new() { Key = "Email", Header = "Email", Example = "sample.customer@example.com" },
        new() { Key = "PhoneNumber", Header = "Phone", Example = "+91-9876543210" },
        new() { Key = "BillingAddress", Header = "Billing Address", Example = "SAMPLE Street, Chennai" },
        new() { Key = "PaymentTerms", Header = "Payment Terms", Example = "Net 30" },
        new() { Key = "CreditLimit", Header = "Credit Limit", Example = "100000" },
        new() { Key = "TaxId", Header = "Tax Id / GSTIN", Example = "33AAAAA0000A1Z5" },
        new() { Key = "Industry", Header = "Industry", Example = "Technology" },
        new() { Key = "Website", Header = "Website", Example = "https://example.com" },
        new() { Key = "CustomerType", Header = "Customer Type", Example = "Business" },
        new() { Key = "Territory", Header = "Territory", Example = "South" },
        new() { Key = "Tags", Header = "Tags", Example = "import" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.CrmCustomers
            .Where(c => c.CompanyId == companyId && c.AccountNumber != null)
            .Select(c => new { c.Id, c.AccountNumber })
            .ToListAsync(ct);
        var byAccount = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.AccountNumber))
            .ToDictionary(x => x.AccountNumber!, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var rows = new List<CsvRowValidation>();
        var rowNum = 1;
        foreach (var values in parsed.Rows)
        {
            rowNum++;
            var errors = new List<string>();
            var account = CsvValidator.Get(values, "AccountNumber");
            var company = CsvValidator.Get(values, "CompanyName");
            if (!CsvValidator.TryRequired(account, "AccountNumber", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(company, "CompanyName", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryEmail(CsvValidator.Get(values, "Email"), out var e3) && e3 is not null) errors.Add(e3);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "CreditLimit"), out _, out var e4) && e4 is not null) errors.Add(e4);
            if (!CsvValidator.TryGstin(CsvValidator.Get(values, "TaxId"), out var e5) && e5 is not null) errors.Add(e5);

            int? existingId = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(account) && byAccount.TryGetValue(account, out var id))
            {
                existingId = id;
                action = "Update";
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = rowNum,
                IsValid = errors.Count == 0,
                Action = action,
                Values = values,
                Errors = errors,
                MatchKey = account,
                ExistingId = existingId
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var created = 0;
        var updated = 0;
        var failed = 0;
        var messages = new List<string>();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                decimal? credit = null;
                if (CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "CreditLimit"), out var creditVal, out _) && creditVal is not null)
                {
                    credit = creditVal;
                }

                if (row.ExistingId is int id)
                {
                    var existing = await _crm.GetCustomerAsync(id, ct);
                    if (existing is null)
                    {
                        failed++;
                        messages.Add($"Row {row.RowNumber}: customer {id} not found.");
                        continue;
                    }

                    existing.CompanyName = CsvValidator.Get(row.Values, "CompanyName");
                    existing.PrimaryContact = NullIfEmpty(CsvValidator.Get(row.Values, "PrimaryContact"));
                    existing.Email = NullIfEmpty(CsvValidator.Get(row.Values, "Email"));
                    existing.PhoneNumber = NullIfEmpty(CsvValidator.Get(row.Values, "PhoneNumber"));
                    existing.BillingAddress = NullIfEmpty(CsvValidator.Get(row.Values, "BillingAddress"));
                    existing.PaymentTerms = NullIfEmpty(CsvValidator.Get(row.Values, "PaymentTerms"));
                    existing.CreditLimit = credit;
                    existing.TaxId = NullIfEmpty(CsvValidator.Get(row.Values, "TaxId"));
                    existing.Industry = NullIfEmpty(CsvValidator.Get(row.Values, "Industry"));
                    existing.Website = NullIfEmpty(CsvValidator.Get(row.Values, "Website"));
                    existing.CustomerType = NullIfEmpty(CsvValidator.Get(row.Values, "CustomerType")) ?? "Business";
                    existing.Territory = NullIfEmpty(CsvValidator.Get(row.Values, "Territory"));
                    existing.Tags = NullIfEmpty(CsvValidator.Get(row.Values, "Tags"));
                    existing.AccountNumber = CsvValidator.Get(row.Values, "AccountNumber");
                    await _crm.UpdateCustomerAsync(id, existing, ct);
                    updated++;
                }
                else
                {
                    await _crm.CreateCustomerAsync(new Customer
                    {
                        CompanyId = companyId,
                        AccountNumber = CsvValidator.Get(row.Values, "AccountNumber"),
                        CompanyName = CsvValidator.Get(row.Values, "CompanyName"),
                        PrimaryContact = NullIfEmpty(CsvValidator.Get(row.Values, "PrimaryContact")),
                        Email = NullIfEmpty(CsvValidator.Get(row.Values, "Email")),
                        PhoneNumber = NullIfEmpty(CsvValidator.Get(row.Values, "PhoneNumber")),
                        BillingAddress = NullIfEmpty(CsvValidator.Get(row.Values, "BillingAddress")),
                        PaymentTerms = NullIfEmpty(CsvValidator.Get(row.Values, "PaymentTerms")),
                        CreditLimit = credit,
                        TaxId = NullIfEmpty(CsvValidator.Get(row.Values, "TaxId")),
                        Industry = NullIfEmpty(CsvValidator.Get(row.Values, "Industry")),
                        Website = NullIfEmpty(CsvValidator.Get(row.Values, "Website")),
                        CustomerType = NullIfEmpty(CsvValidator.Get(row.Values, "CustomerType")) ?? "Business",
                        Territory = NullIfEmpty(CsvValidator.Get(row.Values, "Territory")),
                        Tags = NullIfEmpty(CsvValidator.Get(row.Values, "Tags"))
                    }, ct);
                    created++;
                }
            }

            await tx.CommitAsync(ct);
            return new CsvImportResult
            {
                EntityKey = EntityKey,
                Success = true,
                Created = created,
                Updated = updated,
                Failed = failed,
                Skipped = preview.InvalidCount,
                Messages = messages
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new CsvImportResult
            {
                EntityKey = EntityKey,
                Success = false,
                Created = created,
                Updated = updated,
                Failed = failed + 1,
                Messages = [$"Import failed: {ex.Message}"]
            };
        }
    }

    public override async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        var companyId = request.CompanyId!.Value;
        var items = await _db.CrmCustomers.AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.CompanyName)
            .ToListAsync(ct);

        var headers = Columns.Select(c => c.Key).ToList();
        var rows = items.Select(c => new[]
        {
            c.AccountNumber, c.CompanyName, c.PrimaryContact, c.Email, c.PhoneNumber,
            c.BillingAddress, c.PaymentTerms, c.CreditLimit?.ToString("0.##"), c.TaxId,
            c.Industry, c.Website, c.CustomerType, c.Territory, c.Tags
        }.AsEnumerable());

        return new CsvExportResult
        {
            FileName = "customers-export.csv",
            CsvContent = CsvParser.ToCsv(headers, rows),
            RowCount = items.Count
        };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["AccountNumber"] = "SAMPLE-CUST-001",
            ["CompanyName"] = "SAMPLE Acme India Pvt Ltd",
            ["PrimaryContact"] = "SAMPLE Ravi",
            ["Email"] = "sample.customer1@example.com",
            ["PhoneNumber"] = "+91-9876543210",
            ["BillingAddress"] = "12 SAMPLE Road, Chennai",
            ["PaymentTerms"] = "Net 30",
            ["CreditLimit"] = "150000",
            ["TaxId"] = "33AAAAA0000A1Z5",
            ["Industry"] = "Technology",
            ["Website"] = "https://example.com",
            ["CustomerType"] = "Business",
            ["Territory"] = "South",
            ["Tags"] = "import"
        },
        new Dictionary<string, string>
        {
            ["AccountNumber"] = "SAMPLE-CUST-002",
            ["CompanyName"] = "SAMPLE Beta Traders",
            ["PrimaryContact"] = "SAMPLE Meena",
            ["Email"] = "sample.customer2@example.com",
            ["PhoneNumber"] = "+91-9876543211",
            ["BillingAddress"] = "45 SAMPLE Street, Bengaluru",
            ["PaymentTerms"] = "Net 15",
            ["CreditLimit"] = "50000",
            ["TaxId"] = "29AAAAA0000A1Z5",
            ["Industry"] = "Retail",
            ["CustomerType"] = "Business",
            ["Territory"] = "South",
            ["Tags"] = "import"
        });

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
