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
        new() { Key = "LastName", Header = "Last Name", Example = "Lead" },
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
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var warnings = new List<string>();
            var email = CsvValidator.Get(values, "Email");
            var first = CsvValidator.Get(values, "FirstName");
            if (!CsvValidator.TryRequired(email, "Email", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(first, "FirstName", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryEmail(email, out var e4) && e4 is not null) errors.Add(e4);

            int? id = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(email) && byEmail.TryGetValue(email, out var existingId))
            {
                id = existingId;
                action = "Update";
                warnings.Add("CSV has no CampaignId/IsQualified/address columns — existing qualification, campaign, and identity fields will be kept.");
            }
            else if (!string.IsNullOrWhiteSpace(email) && !seenInFile.Add(email))
            {
                action = "Skip";
                warnings.Add("Duplicate email in this file; first occurrence will be imported.");
                errors.Add("Duplicate email in import file.");
            }
            else if (!string.IsNullOrWhiteSpace(email))
            {
                seenInFile.Add(email);
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n,
                IsValid = errors.Count == 0,
                Action = action,
                Values = values,
                Errors = errors,
                Warnings = warnings,
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
        var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var batch = 0;
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                if (row.ExistingId is int id)
                {
                    var existing = await _crm.GetLeadAsync(id, ct);
                    if (existing is null)
                        continue;

                    ApplyCsvFields(existing, row.Values, forCreate: false);
                    await _crm.UpdateLeadAsync(id, existing, ct);
                    updated++;
                }
                else
                {
                    await _crm.CreateLeadAsync(Map(row.Values, companyId), ct);
                    created++;
                }

                batch++;
                if (batch % 200 == 0)
                {
                    await _db.SaveChangesAsync(ct);
                }
            }

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
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

    private static Lead Map(IReadOnlyDictionary<string, string> v, int companyId)
    {
        var lead = new Lead { CompanyId = companyId };
        ApplyCsvFields(lead, v, forCreate: true);
        return lead;
    }

    /// <summary>
    /// CSV columns are a subset of Lead. Updates must not pass a sparse Lead into
    /// UpdateLeadAsync — that copies CampaignId/IsQualified/address/score as defaults
    /// and wipes qualification plus campaign attribution on export/reimport.
    /// </summary>
    private static void ApplyCsvFields(Lead lead, IReadOnlyDictionary<string, string> v, bool forCreate)
    {
        lead.Email = CsvValidator.Get(v, "Email");
        lead.FirstName = CsvValidator.Get(v, "FirstName");

        var last = Null(CsvValidator.Get(v, "LastName"));
        if (last is not null || forCreate)
            lead.LastName = last ?? "-";

        var companyName = Null(CsvValidator.Get(v, "CompanyName"));
        if (companyName is not null || forCreate)
            lead.CompanyName = companyName;

        var mobile = Null(CsvValidator.Get(v, "Mobile"));
        var phone = Null(CsvValidator.Get(v, "Phone"));
        if (mobile is not null || forCreate)
            lead.Mobile = mobile;
        if (phone is not null || forCreate)
            lead.Phone = phone ?? (forCreate ? mobile : lead.Phone);

        var source = Null(CsvValidator.Get(v, "LeadSource"));
        if (source is not null || forCreate)
            lead.LeadSource = source;

        var status = Null(CsvValidator.Get(v, "LeadStatus"));
        if (status is not null)
            lead.LeadStatus = status;
        else if (forCreate)
            lead.LeadStatus = "New";

        var industry = Null(CsvValidator.Get(v, "Industry"));
        if (industry is not null || forCreate)
            lead.Industry = industry;

        var owner = Null(CsvValidator.Get(v, "LeadOwner"));
        if (owner is not null || forCreate)
            lead.LeadOwner = owner;

        var city = Null(CsvValidator.Get(v, "City"));
        if (city is not null || forCreate)
            lead.City = city;

        var state = Null(CsvValidator.Get(v, "State"));
        if (state is not null || forCreate)
            lead.State = state;

        var country = Null(CsvValidator.Get(v, "Country"));
        if (country is not null || forCreate)
            lead.Country = country;

        var tags = Null(CsvValidator.Get(v, "Tags"));
        if (tags is not null)
            lead.Tags = tags;
        else if (forCreate)
            lead.Tags = "crm-import";
    }

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
