using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services.Identity;

public sealed class StudentImportOptions
{
    public int? CompanyId { get; set; }
    public string? FilePath { get; set; }
    public bool AnalyzeOnly { get; set; }
    public string? ImportedBy { get; set; }
    public string SheetName { get; set; } = "Consolidated Registrations";
}

public sealed class StudentImportReport
{
    public int SourcePeople { get; set; }
    public int ExpectedEnrollments { get; set; }
    public int ProgramsInSource { get; set; }
    public int ExistingPartiesReused { get; set; }
    public int NewPartiesCreated { get; set; }
    public int PartiesAlreadyLeads { get; set; }
    public int PartiesAlreadyCustomers { get; set; }
    public int ProgramsCreated { get; set; }
    public int ProgramsReused { get; set; }
    public int EnrollmentsCreated { get; set; }
    public int EnrollmentsSkipped { get; set; }
    public int PhoneMatches { get; set; }
    public int EmailMatches { get; set; }
    public int PhoneAndEmailMatches { get; set; }
    public int Ambiguous { get; set; }
    public int Conflicts { get; set; }
    public int InvalidPhones { get; set; }
    public int InvalidEmails { get; set; }
    public int Failed { get; set; }
    public int ReviewQueued { get; set; }
    public bool Success { get; set; }
    public List<string> Messages { get; set; } = [];
    public Dictionary<string, int> ProgramCounts { get; set; } = new(StringComparer.Ordinal);
}

public interface IStudentImportService
{
    Task<StudentImportReport> AnalyzeAsync(StudentImportOptions options, CancellationToken ct = default);
    Task<StudentImportReport> ImportAsync(StudentImportOptions options, CancellationToken ct = default);
}

public sealed class StudentImportService : IStudentImportService
{
    private readonly ShaunErpDbContext _db;
    private readonly IPartyIdentityService _parties;
    private readonly IIdentityMatchService _match;
    private readonly IPhoneNormalizer _phones;
    private readonly IEmailNormalizer _emails;
    private readonly ITenantContext _tenant;
    private readonly ILogger<StudentImportService> _logger;

    public StudentImportService(
        ShaunErpDbContext db,
        IPartyIdentityService parties,
        IIdentityMatchService match,
        IPhoneNormalizer phones,
        IEmailNormalizer emails,
        ITenantContext tenant,
        ILogger<StudentImportService> logger)
    {
        _db = db;
        _parties = parties;
        _match = match;
        _phones = phones;
        _emails = emails;
        _tenant = tenant;
        _logger = logger;
    }

    public Task<StudentImportReport> AnalyzeAsync(StudentImportOptions options, CancellationToken ct = default)
    {
        options.AnalyzeOnly = true;
        return RunAsync(options, ct);
    }

    public Task<StudentImportReport> ImportAsync(StudentImportOptions options, CancellationToken ct = default)
    {
        options.AnalyzeOnly = false;
        return RunAsync(options, ct);
    }

    private async Task<StudentImportReport> RunAsync(StudentImportOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.FilePath) || !File.Exists(options.FilePath))
        {
            throw new FileNotFoundException("Student workbook not found.", options.FilePath);
        }

        var companyId = options.CompanyId ?? _tenant.CompanyId
            ?? throw new InvalidOperationException("CompanyId required.");
        var report = new StudentImportReport();
        var rows = ReadWorkbook(options.FilePath!, options.SheetName);
        report.SourcePeople = rows.Count;
        report.ExpectedEnrollments = rows.Sum(r => r.Courses.Count);
        report.ProgramsInSource = rows.SelectMany(r => r.Courses).Distinct(StringComparer.Ordinal).Count();
        foreach (var course in rows.SelectMany(r => r.Courses))
        {
            report.ProgramCounts[course] = report.ProgramCounts.GetValueOrDefault(course) + 1;
        }

        ImportBatch? batch = null;
        if (!options.AnalyzeOnly)
        {
            batch = new ImportBatch
            {
                CompanyId = companyId,
                SourceName = "course-registrations",
                FileName = Path.GetFileName(options.FilePath),
                StartedAt = DateTime.UtcNow,
                ImportedBy = options.ImportedBy ?? _tenant.UserId?.ToString() ?? "system",
                BatchStatus = ImportBatchStatuses.Running,
                TotalRows = rows.Count,
                CreatedDate = DateTime.UtcNow,
                Status = "Active"
            };
            _db.ImportBatches.Add(batch);
            await _db.SaveChangesAsync(ct);
        }

        var programCache = await _db.TrainingPrograms
            .Where(p => p.CompanyId == companyId)
            .ToDictionaryAsync(p => p.Name, p => p, StringComparer.Ordinal, ct);
        var existingProgramNames = programCache.Keys.ToHashSet(StringComparer.Ordinal);

        var rowNumber = 0;
        foreach (var row in rows)
        {
            rowNumber++;
            try
            {
                var phone = _phones.Normalize(row.Phone);
                var email = _emails.Normalize(row.Email);
                if (!string.IsNullOrWhiteSpace(row.Phone) && (string.IsNullOrWhiteSpace(phone.NormalizedPhone) || phone.NormalizedPhone.Length < 8))
                {
                    report.InvalidPhones++;
                }

                if (!string.IsNullOrWhiteSpace(row.Email) && email is null)
                {
                    report.InvalidEmails++;
                }

                var names = SplitName(row.Name);
                var match = await _match.MatchAsync(new IdentityMatchRequest
                {
                    CompanyId = companyId,
                    FirstName = names.First,
                    LastName = names.Last,
                    DisplayName = row.Name,
                    Phone = row.Phone,
                    Email = row.Email
                }, ct);

                if (match.Confidence == IdentityMatchConfidence.HighConfidence)
                {
                    if (match.MatchedBy == IdentityMatchBy.PhoneAndEmail) report.PhoneAndEmailMatches++;
                    else if (match.MatchedBy == IdentityMatchBy.Phone) report.PhoneMatches++;
                    else if (match.MatchedBy == IdentityMatchBy.Email) report.EmailMatches++;
                }
                else if (match.Confidence == IdentityMatchConfidence.Ambiguous) report.Ambiguous++;
                else if (match.Confidence == IdentityMatchConfidence.Conflict) report.Conflicts++;

                int? partyId = null;
                var externalId = $"student-row:{rowNumber}";

                // Idempotency: reuse party already linked for this workbook row.
                var priorRow = await _db.ImportSourceRows.AsNoTracking()
                    .Where(r => r.ExternalId == externalId && r.MatchedPartyId != null)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync(ct);

                if (priorRow?.MatchedPartyId is int priorPartyId)
                {
                    partyId = priorPartyId;
                    report.ExistingPartiesReused++;
                }
                else if (options.AnalyzeOnly)
                {
                    if (match.Confidence == IdentityMatchConfidence.HighConfidence && match.PartyId is int existing)
                    {
                        report.ExistingPartiesReused++;
                        partyId = existing;
                    }
                    else if (match.Confidence is IdentityMatchConfidence.Ambiguous or IdentityMatchConfidence.Conflict)
                    {
                        report.ReviewQueued++;
                        report.NewPartiesCreated++;
                    }
                    else
                    {
                        report.NewPartiesCreated++;
                    }
                }
                else
                {
                    // Prefer an existing student (enrollment) candidate over creating a duplicate person.
                    if (match.Confidence is IdentityMatchConfidence.Ambiguous or IdentityMatchConfidence.Conflict
                        && match.Candidates.Count > 0)
                    {
                        var candidateIds = match.Candidates.Select(c => c.PartyId).Distinct().ToList();
                        var studentCandidate = await _db.CourseEnrollments.AsNoTracking()
                            .Where(e => e.CompanyId == companyId && candidateIds.Contains(e.PartyId))
                            .OrderBy(e => e.PartyId)
                            .Select(e => (int?)e.PartyId)
                            .FirstOrDefaultAsync(ct);
                        if (studentCandidate is int reuseStudent)
                        {
                            partyId = reuseStudent;
                            report.ExistingPartiesReused++;
                            report.ReviewQueued++;
                            batch!.ReviewRows++;
                        }
                    }

                    if (partyId is null)
                    {
                        var ensure = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
                        {
                            CompanyId = companyId,
                            FirstName = names.First,
                            LastName = names.Last,
                            DisplayName = row.Name,
                            Phone = row.Phone,
                            Mobile = row.Phone,
                            Email = row.Email,
                            ImportBatchId = batch?.Id,
                            ExternalId = externalId,
                            CreateReviewOnAmbiguous = true
                        }, ct);

                        partyId = ensure.PartyId;
                        if (ensure.LinkedExisting) report.ExistingPartiesReused++;
                        if (ensure.Created) report.NewPartiesCreated++;
                        if (ensure.NeedsReview)
                        {
                            report.ReviewQueued++;
                            batch!.ReviewRows++;
                        }
                    }

                    if (partyId is int pid)
                    {
                        _db.ImportSourceRows.Add(new ImportSourceRow
                        {
                            ImportBatchId = batch!.Id,
                            SourceRowNumber = rowNumber,
                            ExternalId = externalId,
                            RawDataJson = JsonSerializer.Serialize(row),
                            MatchedPartyId = pid,
                            MatchStatus = match.Confidence is IdentityMatchConfidence.Ambiguous or IdentityMatchConfidence.Conflict
                                ? ImportMatchStatuses.Ambiguous
                                : match.Confidence == IdentityMatchConfidence.HighConfidence
                                    ? ImportMatchStatuses.Linked
                                    : ImportMatchStatuses.Created,
                            MatchReason = match.Reason ?? (priorRow is not null ? "ReusedPriorSourceRow" : null),
                            ProcessedAt = DateTime.UtcNow,
                            CreatedDate = DateTime.UtcNow,
                            Status = "Active"
                        });
                    }
                }

                if (partyId is int linkedParty)
                {
                    if (await _db.Leads.AnyAsync(l => l.CompanyId == companyId && l.PartyId == linkedParty, ct))
                    {
                        report.PartiesAlreadyLeads++;
                    }

                    if (await _db.CrmCustomers.AnyAsync(c => c.CompanyId == companyId && c.PartyId == linkedParty, ct))
                    {
                        report.PartiesAlreadyCustomers++;
                    }
                }

                if (options.AnalyzeOnly || partyId is null)
                {
                    continue;
                }

                foreach (var courseName in row.Courses)
                {
                    if (!programCache.TryGetValue(courseName, out var program))
                    {
                        program = new TrainingProgram
                        {
                            CompanyId = companyId,
                            Name = courseName,
                            Status = "Active",
                            CreatedDate = DateTime.UtcNow,
                            Description = "Imported from Consolidated Course Registrations"
                        };
                        _db.TrainingPrograms.Add(program);
                        await _db.SaveChangesAsync(ct);
                        programCache[courseName] = program;
                        report.ProgramsCreated++;
                    }

                    var exists = await _db.CourseEnrollments.AnyAsync(e =>
                        e.CompanyId == companyId && e.PartyId == partyId && e.ProgramId == program.Id, ct);
                    if (exists)
                    {
                        report.EnrollmentsSkipped++;
                        continue;
                    }

                    _db.CourseEnrollments.Add(new CourseEnrollment
                    {
                        CompanyId = companyId,
                        PartyId = partyId.Value,
                        ProgramId = program.Id,
                        EnrollmentStatus = EnrollmentStatuses.Historical,
                        Source = "Consolidated Course Registrations",
                        SourceReference = row.SourceSheets,
                        ImportBatchId = batch?.Id,
                        Notes = row.CourseCount > 0 ? $"Course Count={row.CourseCount}" : null,
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    });
                    report.EnrollmentsCreated++;
                    batch!.CreatedRows++;
                }

                if (rowNumber % 50 == 0)
                {
                    await _db.SaveChangesAsync(ct);
                    var batchId = batch!.Id;
                    _db.ChangeTracker.Clear();
                    batch = await _db.ImportBatches.FirstAsync(b => b.Id == batchId, ct);
                    programCache = await _db.TrainingPrograms
                        .Where(p => p.CompanyId == companyId)
                        .ToDictionaryAsync(p => p.Name, p => p, StringComparer.Ordinal, ct);
                }
            }
            catch (Exception ex)
            {
                report.Failed++;
                _logger.LogWarning(ex, "Student import failed at row {Row}", rowNumber);
                if (batch is not null)
                {
                    batch.FailedRows++;
                    _db.ImportSourceRows.Add(new ImportSourceRow
                    {
                        ImportBatchId = batch.Id,
                        SourceRowNumber = rowNumber,
                        MatchStatus = ImportMatchStatuses.Failed,
                        MatchReason = ex.Message,
                        ProcessedAt = DateTime.UtcNow,
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    });
                }
            }
        }

        if (!options.AnalyzeOnly && batch is not null)
        {
            await _db.SaveChangesAsync(ct);
            batch.CompletedAt = DateTime.UtcNow;
            batch.MatchedRows = report.ExistingPartiesReused;
            batch.BatchStatus = batch.FailedRows > 0
                ? ImportBatchStatuses.Failed
                : batch.ReviewRows > 0 ? ImportBatchStatuses.CompletedWithReview : ImportBatchStatuses.Completed;
            await _db.SaveChangesAsync(ct);
            report.Messages.Add($"ImportBatch {batch.Id} status={batch.BatchStatus}");
        }

        report.Success = report.Failed == 0;
        if (!options.AnalyzeOnly)
        {
            report.ProgramsReused = report.ProgramCounts.Keys.Count(n => existingProgramNames.Contains(n));
        }
        report.Messages.Add(options.AnalyzeOnly ? "MODE=analyze" : "MODE=import");
        report.Messages.Add($"sourcePeople={report.SourcePeople} expectedEnrollments={report.ExpectedEnrollments} programs={report.ProgramsInSource}");
        return report;
    }

    private static List<StudentSourceRow> ReadWorkbook(string path, string sheetName)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.FirstOrDefault(s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                 ?? wb.Worksheets.First();
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = ws.FirstRowUsed() ?? throw new InvalidOperationException("Workbook is empty.");
        foreach (var cell in headerRow.CellsUsed())
        {
            headers[cell.GetString().Trim()] = cell.Address.ColumnNumber;
        }

        int Col(string name) => headers.TryGetValue(name, out var c) ? c : -1;
        var nameCol = Col("Name");
        var phoneCol = Col("Phone");
        var emailCol = Col("Email");
        var coursesCol = Col("Courses");
        var countCol = Col("Course Count");
        var sourceCol = Col("Source Sheets");

        var rows = new List<StudentSourceRow>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var name = nameCol > 0 ? row.Cell(nameCol).GetString().Trim() : "";
            if (string.IsNullOrWhiteSpace(name)) continue;
            var coursesRaw = coursesCol > 0 ? row.Cell(coursesCol).GetString() : "";
            var courses = coursesRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            rows.Add(new StudentSourceRow
            {
                Name = name,
                Phone = phoneCol > 0 ? row.Cell(phoneCol).GetString().Trim() : null,
                Email = emailCol > 0 ? NullIfEmpty(row.Cell(emailCol).GetString()) : null,
                Courses = courses,
                CourseCount = countCol > 0 && int.TryParse(row.Cell(countCol).GetString(), out var n) ? n : courses.Count,
                SourceSheets = sourceCol > 0 ? NullIfEmpty(row.Cell(sourceCol).GetString()) : null
            });
        }

        return rows;
    }

    private static (string? First, string? Last) SplitName(string? full)
    {
        if (string.IsNullOrWhiteSpace(full)) return (null, null);
        var parts = full.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? (parts[0], null) : (parts[0], parts[1]);
    }

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private sealed class StudentSourceRow
    {
        public string Name { get; init; } = "";
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public List<string> Courses { get; init; } = [];
        public int CourseCount { get; init; }
        public string? SourceSheets { get; init; }
    }
}
