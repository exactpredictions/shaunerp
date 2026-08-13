using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.ImportExport;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport.Importers;

public sealed class ProspectsCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly ICrmService _crm;

    public ProspectsCsvImporter(ShaunErpDbContext db, ICrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public override string EntityKey => "prospects";
    public override string DisplayName => "Prospects";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "Email", Header = "Email", Required = true, Example = "sample.prospect1@example.com" },
        new() { Key = "FirstName", Header = "First Name", Required = true, Example = "SAMPLE" },
        new() { Key = "LastName", Header = "Last Name", Required = true, Example = "Prospect" },
        new() { Key = "CompanyName", Header = "Company", Example = "SAMPLE Prospect Co" },
        new() { Key = "PhoneNumber", Header = "Phone", Example = "+91-9000000020" },
        new() { Key = "Stage", Header = "Stage", Example = "Engaged" },
        new() { Key = "Source", Header = "Source", Example = "Website" },
        new() { Key = "Industry", Header = "Industry", Example = "Manufacturing" },
        new() { Key = "Territory", Header = "Territory", Example = "West" },
        new() { Key = "Tags", Header = "Tags", Example = "import" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.Prospects.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.Email != null)
            .Select(p => new { p.Id, p.Email })
            .ToListAsync(ct);
        var byEmail = existing.Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var email = CsvValidator.Get(values, "Email");
            if (!CsvValidator.TryRequired(email, "Email", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "FirstName"), "FirstName", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "LastName"), "LastName", out var e3)) errors.Add(e3!);
            if (!CsvValidator.TryEmail(email, out var e4) && e4 is not null) errors.Add(e4);
            int? id = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(email) && byEmail.TryGetValue(email, out var eid))
            {
                id = eid;
                action = "Update";
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n, IsValid = errors.Count == 0, Action = action, Values = values,
                Errors = errors, MatchKey = email, ExistingId = id
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
                var p = new Prospect
                {
                    CompanyId = companyId,
                    Email = CsvValidator.Get(row.Values, "Email"),
                    FirstName = CsvValidator.Get(row.Values, "FirstName"),
                    LastName = CsvValidator.Get(row.Values, "LastName"),
                    CompanyName = Null(CsvValidator.Get(row.Values, "CompanyName")),
                    PhoneNumber = Null(CsvValidator.Get(row.Values, "PhoneNumber")),
                    Stage = Null(CsvValidator.Get(row.Values, "Stage")) ?? "Engaged",
                    Source = Null(CsvValidator.Get(row.Values, "Source")),
                    Industry = Null(CsvValidator.Get(row.Values, "Industry")),
                    Territory = Null(CsvValidator.Get(row.Values, "Territory")),
                    Tags = Null(CsvValidator.Get(row.Values, "Tags"))
                };
                if (row.ExistingId is int id)
                {
                    p.Id = id;
                    await _crm.UpdateProspectAsync(id, p, ct);
                    updated++;
                }
                else
                {
                    await _crm.CreateProspectAsync(p, ct);
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
        var items = await _db.Prospects.AsNoTracking().Where(p => p.CompanyId == request.CompanyId).OrderBy(p => p.LastName).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(p => new[]
        {
            p.Email, p.FirstName, p.LastName, p.CompanyName, p.PhoneNumber, p.Stage, p.Source, p.Industry, p.Territory, p.Tags
        }.AsEnumerable());
        return new CsvExportResult { FileName = "prospects-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["Email"] = "sample.prospect1@example.com", ["FirstName"] = "SAMPLE", ["LastName"] = "ProspectOne",
            ["CompanyName"] = "SAMPLE Prospect Co", ["Stage"] = "Engaged", ["Source"] = "Website", ["Tags"] = "import"
        },
        new Dictionary<string, string>
        {
            ["Email"] = "sample.prospect2@example.com", ["FirstName"] = "SAMPLE", ["LastName"] = "ProspectTwo",
            ["CompanyName"] = "SAMPLE Growth Ltd", ["Stage"] = "Qualified", ["Tags"] = "import"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class OpportunitiesCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly ICrmService _crm;

    public OpportunitiesCsvImporter(ShaunErpDbContext db, ICrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public override string EntityKey => "opportunities";
    public override string DisplayName => "Opportunities";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "DealName", Header = "Deal Name", Required = true, Example = "SAMPLE Deal Alpha" },
        new() { Key = "Amount", Header = "Amount", Example = "250000" },
        new() { Key = "Stage", Header = "Stage", Example = "Qualification" },
        new() { Key = "Probability", Header = "Probability", Example = "40" },
        new() { Key = "CustomerAccountNumber", Header = "Customer Account Number", Example = "SAMPLE-CUST-001" },
        new() { Key = "CurrencyCode", Header = "Currency", Example = "INR" },
        new() { Key = "ExpectedCloseDate", Header = "Expected Close Date", Example = "2026-12-31" },
        new() { Key = "DealOwner", Header = "Owner", Example = "admin" },
        new() { Key = "Tags", Header = "Tags", Example = "import" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.Opportunities.AsNoTracking()
            .Where(o => o.CompanyId == companyId)
            .Select(o => new { o.Id, o.DealName })
            .ToListAsync(ct);
        var byName = existing.Where(x => !string.IsNullOrWhiteSpace(x.DealName))
            .GroupBy(x => x.DealName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var customers = await _db.CrmCustomers.AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.AccountNumber != null)
            .ToDictionaryAsync(c => c.AccountNumber!, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var warnings = new List<string>();
            var name = CsvValidator.Get(values, "DealName");
            if (!CsvValidator.TryRequired(name, "DealName", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "Amount"), out _, out var e2) && e2 is not null) errors.Add(e2);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "Probability"), out _, out var e3) && e3 is not null) errors.Add(e3);
            if (!CsvValidator.TryDate(CsvValidator.Get(values, "ExpectedCloseDate"), out _, out var e4) && e4 is not null) errors.Add(e4);
            var acct = CsvValidator.Get(values, "CustomerAccountNumber");
            if (!string.IsNullOrWhiteSpace(acct) && !customers.ContainsKey(acct))
            {
                errors.Add($"Customer account '{acct}' not found.");
            }

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
                Errors = errors, Warnings = warnings, MatchKey = name, ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var customers = await _db.CrmCustomers
            .Where(c => c.CompanyId == companyId && c.AccountNumber != null)
            .ToDictionaryAsync(c => c.AccountNumber!, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var created = 0; var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "Amount"), out var amount, out _);
                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "Probability"), out var probability, out _);
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "ExpectedCloseDate"), out var close, out _);
                int? customerId = null;
                var acct = CsvValidator.Get(row.Values, "CustomerAccountNumber");
                if (!string.IsNullOrWhiteSpace(acct) && customers.TryGetValue(acct, out var cid))
                {
                    customerId = cid;
                }

                var deal = new Opportunity
                {
                    CompanyId = companyId,
                    DealName = CsvValidator.Get(row.Values, "DealName"),
                    Amount = amount ?? 0,
                    Stage = Null(CsvValidator.Get(row.Values, "Stage")) ?? "Qualification",
                    Probability = probability.HasValue ? (double)probability.Value : null,
                    CustomerId = customerId,
                    CurrencyCode = Null(CsvValidator.Get(row.Values, "CurrencyCode")) ?? "INR",
                    ClosingDate = close,
                    DealOwner = Null(CsvValidator.Get(row.Values, "DealOwner")),
                    Tags = Null(CsvValidator.Get(row.Values, "Tags"))
                };
                if (row.ExistingId is int id)
                {
                    deal.Id = id;
                    await _crm.UpdateOpportunityAsync(id, deal, ct);
                    updated++;
                }
                else
                {
                    await _crm.CreateOpportunityAsync(deal, ct);
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
        var items = await _db.Opportunities.AsNoTracking().Include(o => o.Customer)
            .Where(o => o.CompanyId == request.CompanyId).OrderBy(o => o.DealName).ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(o => new[]
        {
            o.DealName, o.Amount.ToString("0.##"), o.Stage, o.Probability?.ToString(),
            o.Customer?.AccountNumber, o.CurrencyCode, o.ClosingDate?.ToString("yyyy-MM-dd"),
            o.DealOwner, o.Tags
        }.AsEnumerable());
        return new CsvExportResult { FileName = "opportunities-export.csv", CsvContent = CsvParser.ToCsv(headers, rows), RowCount = items.Count };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["DealName"] = "SAMPLE Deal Alpha", ["Amount"] = "250000", ["Stage"] = "Qualification",
            ["Probability"] = "40", ["CustomerAccountNumber"] = "SAMPLE-CUST-001", ["CurrencyCode"] = "INR",
            ["ExpectedCloseDate"] = "2026-12-31", ["Tags"] = "import"
        },
        new Dictionary<string, string>
        {
            ["DealName"] = "SAMPLE Deal Beta", ["Amount"] = "120000", ["Stage"] = "Proposal",
            ["Probability"] = "60", ["CurrencyCode"] = "INR", ["Tags"] = "import"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
