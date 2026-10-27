using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.ImportExport;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport.Importers;

public sealed class ContactsCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;
    private readonly ICrmService _crm;

    public ContactsCsvImporter(ShaunErpDbContext db, ICrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public override string EntityKey => "contacts";
    public override string DisplayName => "Contacts";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "Email", Header = "Email", Required = true, Example = "sample.contact1@example.com" },
        new() { Key = "FirstName", Header = "First Name", Required = true, Example = "SAMPLE" },
        new() { Key = "LastName", Header = "Last Name", Required = true, Example = "Contact" },
        new() { Key = "Phone", Header = "Phone", Example = "+91-9000000010" },
        new() { Key = "Mobile", Header = "Mobile", Example = "+91-9000000011" },
        new() { Key = "Title", Header = "Title", Example = "Manager" },
        new() { Key = "Department", Header = "Department", Example = "Sales" },
        new() { Key = "AccountNumber", Header = "Customer Account Number", Example = "SAMPLE-CUST-001" },
        new() { Key = "Tags", Header = "Tags", Example = "import" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var contacts = await _db.Contacts.AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.Email != null)
            .Select(c => new { c.Id, c.Email })
            .ToListAsync(ct);
        var byEmail = contacts.Where(c => !string.IsNullOrWhiteSpace(c.Email))
            .GroupBy(c => c.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var customers = await CustomerAccountLookup.LoadAsync(_db, companyId, ct);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var warnings = new List<string>();
            var email = CsvValidator.Get(values, "Email");
            if (!CsvValidator.TryRequired(email, "Email", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "FirstName"), "FirstName", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "LastName"), "LastName", out var e3)) errors.Add(e3!);
            if (!CsvValidator.TryEmail(email, out var e4) && e4 is not null) errors.Add(e4);

            var account = CsvValidator.Get(values, "AccountNumber");
            if (customers.IsDuplicate(account))
            {
                errors.Add(CustomerAccountIndex.DuplicateError(account));
            }
            else if (!string.IsNullOrWhiteSpace(account) && !customers.TryGetUnique(account, out _))
            {
                warnings.Add($"Customer account '{account}' not found; contact will be created without customer link.");
            }

            int? existingId = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(email) && byEmail.TryGetValue(email, out var id))
            {
                existingId = id;
                action = "Update";
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
                ExistingId = existingId
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var customers = await CustomerAccountLookup.LoadAsync(_db, companyId, ct);

        var created = 0;
        var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                int? customerId = null;
                var account = CsvValidator.Get(row.Values, "AccountNumber");
                if (customers.TryGetUnique(account, out var matchedCustomer))
                {
                    customerId = matchedCustomer.Id;
                }

                var contact = new Contact
                {
                    CompanyId = companyId,
                    Email = CsvValidator.Get(row.Values, "Email"),
                    FirstName = CsvValidator.Get(row.Values, "FirstName"),
                    LastName = CsvValidator.Get(row.Values, "LastName"),
                    Phone = Null(CsvValidator.Get(row.Values, "Phone")),
                    Mobile = Null(CsvValidator.Get(row.Values, "Mobile")),
                    Title = Null(CsvValidator.Get(row.Values, "Title")),
                    Department = Null(CsvValidator.Get(row.Values, "Department")),
                    CustomerId = customerId,
                    Tags = Null(CsvValidator.Get(row.Values, "Tags"))
                };

                if (row.ExistingId is int id)
                {
                    contact.Id = id;
                    await _crm.UpdateContactAsync(id, contact, ct);
                    updated++;
                }
                else
                {
                    await _crm.CreateContactAsync(contact, ct);
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
        var items = await _db.Contacts.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.CompanyId == request.CompanyId)
            .OrderBy(c => c.LastName)
            .ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(c => new[]
        {
            c.Email, c.FirstName, c.LastName, c.Phone, c.Mobile, c.Title, c.Department,
            c.Customer?.AccountNumber, c.Tags
        }.AsEnumerable());
        return new CsvExportResult
        {
            FileName = "contacts-export.csv",
            CsvContent = CsvParser.ToCsv(headers, rows),
            RowCount = items.Count
        };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["Email"] = "sample.contact1@example.com",
            ["FirstName"] = "SAMPLE",
            ["LastName"] = "ContactOne",
            ["Phone"] = "+91-9000000010",
            ["Title"] = "Manager",
            ["Department"] = "Sales",
            ["AccountNumber"] = "SAMPLE-CUST-001",
            ["Tags"] = "import"
        },
        new Dictionary<string, string>
        {
            ["Email"] = "sample.contact2@example.com",
            ["FirstName"] = "SAMPLE",
            ["LastName"] = "ContactTwo",
            ["Title"] = "Director",
            ["Tags"] = "import"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
