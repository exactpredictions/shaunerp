using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.ImportExport;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport.Importers;

public sealed class LeadsCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly ICrmService _crm;

    public LeadsCsvImporter(ShaunErpDbContext db, ICrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public override string EntityKey => "leads";
    public override string DisplayName => "Leads";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "Email", Header = "Email", Required = true, Example = "sample.lead1@example.com" },
        new() { Key = "FirstName", Header = "First Name", Required = true, Example = "SAMPLE" },
        new() { Key = "LastName", Header = "Last Name", Required = true, Example = "Lead" },
        new() { Key = "CompanyName", Header = "Company", Example = "SAMPLE Lead Co" },
        new() { Key = "Phone", Header = "Phone", Example = "+91-9000000001" },
        new() { Key = "Mobile", Header = "Mobile", Example = "+91-9000000002" },
        new() { Key = "LeadSource", Header = "Lead Source", Example = "Website" },
        new() { Key = "LeadStatus", Header = "Lead Status", Example = "New" },
        new() { Key = "Industry", Header = "Industry", Example = "Services" },
        new() { Key = "LeadOwner", Header = "Owner", Example = "admin" },
        new() { Key = "City", Header = "City", Example = "Chennai" },
        new() { Key = "State", Header = "State", Example = "Tamil Nadu" },
        new() { Key = "Country", Header = "Country", Example = "India" },
        new() { Key = "Tags", Header = "Tags", Example = "import" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.Leads.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.Email != null)
            .Select(l => new { l.Id, l.Email })
            .ToListAsync(ct);
        var byEmail = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var email = CsvValidator.Get(values, "Email");
            var first = CsvValidator.Get(values, "FirstName");
            var last = CsvValidator.Get(values, "LastName");
            if (!CsvValidator.TryRequired(email, "Email", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(first, "FirstName", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryRequired(last, "LastName", out var e3)) errors.Add(e3!);
            if (!CsvValidator.TryEmail(email, out var e4) && e4 is not null) errors.Add(e4);

            int? id = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(email) && byEmail.TryGetValue(email, out var existingId))
            {
                id = existingId;
                action = "Update";
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n,
                IsValid = errors.Count == 0,
                Action = action,
                Values = values,
                Errors = errors,
                MatchKey = email,
                ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var created = 0;
        var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                var map = Map(row.Values, companyId);
                if (row.ExistingId is int id)
                {
                    map.Id = id;
                    await _crm.UpdateLeadAsync(id, map, ct);
                    updated++;
                }
                else
                {
                    await _crm.CreateLeadAsync(map, ct);
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
                Skipped = preview.InvalidCount
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
        var items = await _db.Leads.AsNoTracking()
            .Where(l => l.CompanyId == request.CompanyId)
            .OrderBy(l => l.LastName)
            .ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(l => new[]
        {
            l.Email, l.FirstName, l.LastName, l.CompanyName, l.Phone, l.Mobile,
            l.LeadSource, l.LeadStatus, l.Industry, l.LeadOwner, l.City, l.State, l.Country, l.Tags
        }.AsEnumerable());
        return new CsvExportResult
        {
            FileName = "leads-export.csv",
            CsvContent = CsvParser.ToCsv(headers, rows),
            RowCount = items.Count
        };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["Email"] = "sample.lead1@example.com",
            ["FirstName"] = "SAMPLE",
            ["LastName"] = "LeadOne",
            ["CompanyName"] = "SAMPLE Lead Co",
            ["Phone"] = "+91-9000000001",
            ["LeadSource"] = "Website",
            ["LeadStatus"] = "New",
            ["Industry"] = "Services",
            ["City"] = "Chennai",
            ["State"] = "Tamil Nadu",
            ["Country"] = "India",
            ["Tags"] = "import"
        },
        new Dictionary<string, string>
        {
            ["Email"] = "sample.lead2@example.com",
            ["FirstName"] = "SAMPLE",
            ["LastName"] = "LeadTwo",
            ["CompanyName"] = "SAMPLE Prospect Hub",
            ["LeadSource"] = "Referral",
            ["LeadStatus"] = "Contacted",
            ["City"] = "Bengaluru",
            ["State"] = "Karnataka",
            ["Country"] = "India",
            ["Tags"] = "import"
        });

    private static Lead Map(IReadOnlyDictionary<string, string> v, int companyId) => new()
    {
        CompanyId = companyId,
        Email = CsvValidator.Get(v, "Email"),
        FirstName = CsvValidator.Get(v, "FirstName"),
        LastName = CsvValidator.Get(v, "LastName"),
        CompanyName = Null(CsvValidator.Get(v, "CompanyName")),
        Phone = Null(CsvValidator.Get(v, "Phone")),
        Mobile = Null(CsvValidator.Get(v, "Mobile")),
        LeadSource = Null(CsvValidator.Get(v, "LeadSource")),
        LeadStatus = Null(CsvValidator.Get(v, "LeadStatus")) ?? "New",
        Industry = Null(CsvValidator.Get(v, "Industry")),
        LeadOwner = Null(CsvValidator.Get(v, "LeadOwner")),
        City = Null(CsvValidator.Get(v, "City")),
        State = Null(CsvValidator.Get(v, "State")),
        Country = Null(CsvValidator.Get(v, "Country")),
        Tags = Null(CsvValidator.Get(v, "Tags"))
    };

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
