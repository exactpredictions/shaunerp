using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services.Identity;

public sealed class ConsultationImportOptions
{
    public int? CompanyId { get; set; }
    public string? FilePath { get; set; }
    public bool AnalyzeOnly { get; set; }
    public string? ImportedBy { get; set; }
}

public sealed class ConsultationImportReport
{
    public int SourceRows { get; set; }
    public int ExistingPartiesReused { get; set; }
    public int NewPartiesCreated { get; set; }
    public int EngagementsCreated { get; set; }
    public int EngagementsSkipped { get; set; }
    public int RowsProcessed { get; set; }
    public int RowsFailed { get; set; }
    public int ReviewQueued { get; set; }
    public int PhoneAndEmailMatches { get; set; }
    public int PhoneOnlyMatches { get; set; }
    public int EmailOnlyMatches { get; set; }
    public int Ambiguous { get; set; }
    public int Conflicts { get; set; }
    public int InvalidPhones { get; set; }
    public int InvalidEmails { get; set; }
    public int AlreadyLeads { get; set; }
    public int AlreadyStudents { get; set; }
    public int AlreadyActiveSubscribers { get; set; }
    public int AlreadyInactiveSubscribers { get; set; }
    public int AlreadyReportPurchasers { get; set; }
    public int AlreadyCustomers { get; set; }
    public int OverlappingSourceIdentities { get; set; }
    public int UniquePartiesProjected { get; set; }
    public int UniquePartiesActual { get; set; }
    public int UnclassifiedRecords { get; set; }
    public int AlsoStudents { get; set; }
    public int AlsoActiveSubscribers { get; set; }
    public int AlsoInactiveSubscribers { get; set; }
    public int AlsoHoroscopePurchasers { get; set; }
    public int AlsoNumerologyPurchasers { get; set; }
    public int WithNoneOfAbove { get; set; }
    public int? ImportBatchId { get; set; }
    public bool Success { get; set; }
    public List<string> Messages { get; set; } = [];
}

public interface IConsultationImportService
{
    Task<ConsultationImportReport> AnalyzeAsync(ConsultationImportOptions options, CancellationToken ct = default);
    Task<ConsultationImportReport> ImportAsync(ConsultationImportOptions options, CancellationToken ct = default);
}

public sealed class ConsultationImportService : IConsultationImportService
{
    public const string SourceName = "Historical Astrology Consultation Contacts";
    private const string ExternalIdPrefix = "ep-consult:";

    private readonly ShaunErpDbContext _db;
    private readonly IPartyIdentityService _parties;
    private readonly IIdentityMatchService _match;
    private readonly IPhoneNormalizer _phones;
    private readonly IEmailNormalizer _emails;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ConsultationImportService> _logger;

    public ConsultationImportService(
        ShaunErpDbContext db,
        IPartyIdentityService parties,
        IIdentityMatchService match,
        IPhoneNormalizer phones,
        IEmailNormalizer emails,
        ITenantContext tenant,
        ILogger<ConsultationImportService> logger)
    {
        _db = db;
        _parties = parties;
        _match = match;
        _phones = phones;
        _emails = emails;
        _tenant = tenant;
        _logger = logger;
    }

    public Task<ConsultationImportReport> AnalyzeAsync(ConsultationImportOptions options, CancellationToken ct = default)
    {
        options.AnalyzeOnly = true;
        return RunAsync(options, ct);
    }

    public Task<ConsultationImportReport> ImportAsync(ConsultationImportOptions options, CancellationToken ct = default)
    {
        options.AnalyzeOnly = false;
        return RunAsync(options, ct);
    }

    private async Task<ConsultationImportReport> RunAsync(ConsultationImportOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.FilePath) || !File.Exists(options.FilePath))
            throw new FileNotFoundException("Consultation contacts workbook not found.", options.FilePath);

        var companyId = options.CompanyId ?? _tenant.CompanyId
            ?? throw new InvalidOperationException("CompanyId required.");
        var rows = ReadWorkbook(options.FilePath!);
        var report = new ConsultationImportReport { SourceRows = rows.Count };

        report.OverlappingSourceIdentities = CountOverlappingIdentities(rows);

        ImportBatch? batch = null;
        if (!options.AnalyzeOnly)
        {
            batch = new ImportBatch
            {
                CompanyId = companyId,
                SourceName = SourceName,
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
            report.ImportBatchId = batch.Id;
        }

        var projectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actualPartyIds = new HashSet<int>();
        var alreadyLead = new HashSet<int>();
        var alreadyStudent = new HashSet<int>();
        var alreadyActiveSub = new HashSet<int>();
        var alreadyInactiveSub = new HashSet<int>();
        var alreadyReport = new HashSet<int>();
        var alreadyCustomer = new HashSet<int>();
        var rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;
            report.RowsProcessed++;
            try
            {
                var phone = _phones.Normalize(row.Phone);
                var email = _emails.Normalize(row.Email);
                var hasPhone = !string.IsNullOrWhiteSpace(phone.NormalizedPhone) && phone.NormalizedPhone.Length >= 8;
                var hasEmail = email is not null;
                if (!string.IsNullOrWhiteSpace(row.Phone) && !hasPhone) report.InvalidPhones++;
                if (!string.IsNullOrWhiteSpace(row.Email) && !hasEmail) report.InvalidEmails++;

                var names = SplitName(row.Name);
                var externalRef = $"{row.SourceDataset}:{row.SourceId}";
                var externalId = $"{ExternalIdPrefix}{externalRef}";

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
                    else if (match.MatchedBy == IdentityMatchBy.Phone) report.PhoneOnlyMatches++;
                    else if (match.MatchedBy == IdentityMatchBy.Email) report.EmailOnlyMatches++;
                }
                else if (match.Confidence == IdentityMatchConfidence.Ambiguous) report.Ambiguous++;
                else if (match.Confidence == IdentityMatchConfidence.Conflict) report.Conflicts++;

                int? partyId = null;
                var priorRow = await _db.ImportSourceRows.AsNoTracking()
                    .Where(r => r.ExternalId == externalId && r.MatchedPartyId != null)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync(ct);
                var existingEngagement = await _db.ConsultationEngagements
                    .FirstOrDefaultAsync(e => e.CompanyId == companyId && e.ExternalReference == externalRef, ct);

                if (priorRow?.MatchedPartyId is int priorParty)
                {
                    partyId = priorParty;
                    report.ExistingPartiesReused++;
                }
                else if (existingEngagement is not null)
                {
                    partyId = existingEngagement.PartyId;
                    report.ExistingPartiesReused++;
                }

                if (options.AnalyzeOnly)
                {
                    if (partyId is int known)
                    {
                        projectedKeys.Add($"id:{known}");
                    }
                    else if (match.Confidence == IdentityMatchConfidence.HighConfidence && match.PartyId is int existing)
                    {
                        partyId = existing;
                        report.ExistingPartiesReused++;
                        projectedKeys.Add($"id:{existing}");
                    }
                    else if (match.Confidence is IdentityMatchConfidence.Ambiguous or IdentityMatchConfidence.Conflict)
                    {
                        report.ReviewQueued++;
                        report.NewPartiesCreated++;
                        projectedKeys.Add(hasPhone ? $"p:{phone.NormalizedPhone}" : hasEmail ? $"e:{email}" : $"new:{externalRef}");
                    }
                    else
                    {
                        report.NewPartiesCreated++;
                        projectedKeys.Add(hasPhone ? $"p:{phone.NormalizedPhone}" : hasEmail ? $"e:{email}" : $"n:{row.Name}:{externalRef}");
                    }

                    if (existingEngagement is not null) report.EngagementsSkipped++;
                    else
                    {
                        report.EngagementsCreated++;
                        report.UnclassifiedRecords++;
                    }

                    if (partyId is int analyzeParty)
                    {
                        actualPartyIds.Add(analyzeParty);
                        await CaptureOverlapsAsync(companyId, analyzeParty, alreadyLead, alreadyStudent, alreadyActiveSub, alreadyInactiveSub, alreadyReport, alreadyCustomer, ct);
                    }

                    continue;
                }

                // Ambiguous/conflict must not reuse an enrolled/subscribed candidate.
                // Shared family phones would attach the consultation to the wrong sibling;
                // identity review does not re-link engagements after the fact.
                if (partyId is null)
                {
                    var ensure = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
                    {
                        CompanyId = companyId,
                        FirstName = names.First,
                        LastName = names.Last,
                        DisplayName = Clean(row.Name),
                        Phone = Clean(row.Phone),
                        Mobile = Clean(row.Phone),
                        Email = Clean(row.Email),
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
                    if (priorRow is null)
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
                            MatchReason = match.Reason,
                            ProcessedAt = DateTime.UtcNow,
                            CreatedDate = DateTime.UtcNow,
                            Status = "Active"
                        });
                    }

                    actualPartyIds.Add(pid);
                    await CaptureOverlapsAsync(companyId, pid, alreadyLead, alreadyStudent, alreadyActiveSub, alreadyInactiveSub, alreadyReport, alreadyCustomer, ct);

                    if (existingEngagement is not null)
                    {
                        report.EngagementsSkipped++;
                    }
                    else
                    {
                        var sourceCreated = AsUtc(ParseDate(row.SourceCreatedAt));
                        _db.ConsultationEngagements.Add(new ConsultationEngagement
                        {
                            CompanyId = companyId,
                            PartyId = pid,
                            ConsultationServiceId = null,
                            EngagementStatus = ConsultationStatuses.Unclassified,
                            SourceCreatedAt = sourceCreated,
                            EnquiryDate = null,
                            ConsultationDate = null,
                            Source = row.SourceDataset,
                            ExternalReference = externalRef,
                            ImportBatchId = batch!.Id,
                            SourceEvidence = NullIfEmpty(row.SourceEvidence),
                            Notes = NullIfEmpty(row.ImportNote),
                            CreatedDate = DateTime.UtcNow,
                            Status = "Active"
                        });
                        report.EngagementsCreated++;
                        report.UnclassifiedRecords++;
                    }
                }

                if (rowNumber % 25 == 0)
                    await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                report.RowsFailed++;
                _logger.LogWarning(ex, "Consultation import failed at row {Row}", rowNumber);
                report.Messages.Add($"Row {rowNumber}: {ex.Message}");
                foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
                    entry.State = EntityState.Detached;
            }
        }

        if (!options.AnalyzeOnly && batch is not null)
        {
            try { await _db.SaveChangesAsync(ct); }
            catch (Exception ex)
            {
                report.RowsFailed++;
                report.Messages.Add($"FinalSave: {ex.Message}");
                foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
                    entry.State = EntityState.Detached;
            }

            batch.CompletedAt = DateTime.UtcNow;
            batch.MatchedRows = report.ExistingPartiesReused;
            batch.CreatedRows = report.EngagementsCreated;
            batch.ReviewRows = report.ReviewQueued;
            batch.FailedRows = report.RowsFailed;
            batch.BatchStatus = batch.FailedRows > 0
                ? ImportBatchStatuses.Failed
                : batch.ReviewRows > 0 ? ImportBatchStatuses.CompletedWithReview : ImportBatchStatuses.Completed;
            await _db.SaveChangesAsync(ct);
            report.Messages.Add($"ImportBatch {batch.Id} status={batch.BatchStatus}");
        }

        report.AlreadyLeads = alreadyLead.Count;
        report.AlreadyStudents = alreadyStudent.Count;
        report.AlreadyActiveSubscribers = alreadyActiveSub.Count;
        report.AlreadyInactiveSubscribers = alreadyInactiveSub.Count;
        report.AlreadyReportPurchasers = alreadyReport.Count;
        report.AlreadyCustomers = alreadyCustomer.Count;
        report.UniquePartiesProjected = options.AnalyzeOnly ? projectedKeys.Count : actualPartyIds.Count;
        report.UniquePartiesActual = await _db.ConsultationEngagements.AsNoTracking()
            .Where(e => e.CompanyId == companyId)
            .Select(e => e.PartyId).Distinct().CountAsync(ct);

        await FillCrossAnalysisAsync(companyId, report, ct);

        report.Success = report.RowsFailed == 0;
        report.Messages.Add(options.AnalyzeOnly ? "MODE=analyze" : "MODE=import");
        report.Messages.Add($"source={report.SourceRows}");
        return report;
    }

    private async Task CaptureOverlapsAsync(
        int companyId, int partyId,
        HashSet<int> leads, HashSet<int> students, HashSet<int> activeSubs, HashSet<int> inactiveSubs,
        HashSet<int> reports, HashSet<int> customers, CancellationToken ct)
    {
        if (await _db.Leads.AnyAsync(l => l.CompanyId == companyId && l.PartyId == partyId, ct)) leads.Add(partyId);
        if (await _db.CourseEnrollments.AnyAsync(e => e.CompanyId == companyId && e.PartyId == partyId, ct)) students.Add(partyId);
        if (await _db.SubscriptionMemberships.AnyAsync(m => m.CompanyId == companyId && m.PartyId == partyId && m.MembershipStatus == MembershipStatuses.Active, ct))
            activeSubs.Add(partyId);
        else if (await _db.SubscriptionMemberships.AnyAsync(m => m.CompanyId == companyId && m.PartyId == partyId && m.MembershipStatus != MembershipStatuses.Active, ct))
            inactiveSubs.Add(partyId);
        if (await _db.ReportPurchases.AnyAsync(r => r.CompanyId == companyId && r.PartyId == partyId, ct)) reports.Add(partyId);
        if (await _db.CrmCustomers.AnyAsync(c => c.CompanyId == companyId && c.PartyId == partyId, ct)) customers.Add(partyId);
    }

    private async Task FillCrossAnalysisAsync(int companyId, ConsultationImportReport report, CancellationToken ct)
    {
        var partyIds = await _db.ConsultationEngagements.AsNoTracking()
            .Where(e => e.CompanyId == companyId)
            .Select(e => e.PartyId).Distinct().ToListAsync(ct);
        if (partyIds.Count == 0) return;

        report.AlsoStudents = await _db.CourseEnrollments.AsNoTracking()
            .Where(e => e.CompanyId == companyId && partyIds.Contains(e.PartyId)).Select(e => e.PartyId).Distinct().CountAsync(ct);
        report.AlsoActiveSubscribers = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId && partyIds.Contains(m.PartyId) && m.MembershipStatus == MembershipStatuses.Active)
            .Select(m => m.PartyId).Distinct().CountAsync(ct);
        report.AlsoInactiveSubscribers = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId && partyIds.Contains(m.PartyId) && m.MembershipStatus != MembershipStatuses.Active)
            .Select(m => m.PartyId).Distinct().CountAsync(ct);

        var reportParties = await (
            from rp in _db.ReportPurchases.AsNoTracking()
            join prod in _db.ReportProducts.AsNoTracking() on rp.ReportProductId equals prod.Id
            where rp.CompanyId == companyId && partyIds.Contains(rp.PartyId)
            select new { rp.PartyId, prod.ReportType, prod.Name }).ToListAsync(ct);

        report.AlsoHoroscopePurchasers = reportParties
            .Where(x => x.ReportType == "Horoscope" || x.Name == ReportProductNames.HoroscopeReport)
            .Select(x => x.PartyId).Distinct().Count();
        report.AlsoNumerologyPurchasers = reportParties
            .Where(x => x.ReportType == "Numerology" || x.Name == ReportProductNames.NumerologyReport)
            .Select(x => x.PartyId).Distinct().Count();

        var studentSet = await _db.CourseEnrollments.AsNoTracking()
            .Where(e => e.CompanyId == companyId && partyIds.Contains(e.PartyId)).Select(e => e.PartyId).Distinct().ToListAsync(ct);
        var subSet = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId && partyIds.Contains(m.PartyId)).Select(m => m.PartyId).Distinct().ToListAsync(ct);
        var reportSet = reportParties.Select(x => x.PartyId).Distinct().ToHashSet();
        var related = studentSet.Concat(subSet).Concat(reportSet).ToHashSet();
        report.WithNoneOfAbove = partyIds.Count(id => !related.Contains(id));
    }

    private static int CountOverlappingIdentities(List<ConsultationCsvRow> rows)
    {
        var phoneOwners = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.NormalizedPhone) || !string.IsNullOrWhiteSpace(r.Phone))
            .GroupBy(r => (r.NormalizedPhone ?? r.Phone).Trim(), StringComparer.OrdinalIgnoreCase)
            .Count(g => g.Select(x => x.SourceDataset).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);
        var emailOwners = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.NormalizedEmail) || !string.IsNullOrWhiteSpace(r.Email))
            .GroupBy(r => (r.NormalizedEmail ?? r.Email).Trim(), StringComparer.OrdinalIgnoreCase)
            .Count(g => g.Select(x => x.SourceDataset).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);
        return Math.Max(phoneOwners, emailOwners);
    }

    private static List<ConsultationCsvRow> ReadWorkbook(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed() ?? throw new InvalidOperationException("Workbook is empty.");
        var rows = used.RowsUsed().ToList();
        var headerRow = rows.FirstOrDefault(r =>
            string.Equals(r.Cell(1).GetString().Trim(), "source_dataset", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Could not find source_dataset header row.");

        var headers = headerRow.CellsUsed()
            .ToDictionary(c => c.GetString().Trim().ToLowerInvariant(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

        string Cell(IXLRangeRow row, string name) =>
            headers.TryGetValue(name, out var col) ? Clean(row.Cell(col).GetFormattedString()) : string.Empty;

        var result = new List<ConsultationCsvRow>();
        foreach (var row in rows.SkipWhile(r => r.RowNumber() <= headerRow.RowNumber()))
        {
            var dataset = Cell(row, "source_dataset");
            var sourceId = Cell(row, "source_id");
            if (string.IsNullOrWhiteSpace(dataset) || string.IsNullOrWhiteSpace(sourceId))
                continue;

            result.Add(new ConsultationCsvRow
            {
                SourceDataset = dataset,
                SourceId = sourceId,
                Name = Cell(row, "name"),
                Phone = Cell(row, "phone"),
                NormalizedPhone = Cell(row, "normalized_phone"),
                Email = Cell(row, "email"),
                NormalizedEmail = Cell(row, "normalized_email"),
                SourceCreatedAt = Cell(row, "source_created_at"),
                ConsultationStatus = Cell(row, "consultation_status"),
                SourceEvidence = Cell(row, "source_evidence"),
                ImportNote = Cell(row, "import_note")
            });
        }

        return result;
    }

    private static (string? First, string? Last) SplitName(string? fullName)
    {
        var parts = Clean(fullName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return (null, null);
        if (parts.Length == 1) return (parts[0], null);
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static string Clean(string? value) =>
        (value ?? string.Empty).Replace("\0", string.Empty, StringComparison.Ordinal).Trim();

    private static string? NullIfEmpty(string? value)
    {
        var cleaned = Clean(value);
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
            return d;
        return null;
    }

    private static DateTime? AsUtc(DateTime? d) =>
        d is null ? null : DateTime.SpecifyKind(d.Value, DateTimeKind.Utc);

    private sealed class ConsultationCsvRow
    {
        public string SourceDataset { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string NormalizedPhone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string SourceCreatedAt { get; set; } = string.Empty;
        public string ConsultationStatus { get; set; } = string.Empty;
        public string SourceEvidence { get; set; } = string.Empty;
        public string ImportNote { get; set; } = string.Empty;
    }
}
