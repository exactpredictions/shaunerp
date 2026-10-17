using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services.Identity;

public sealed class PremiumSubscriberImportOptions
{
    public int? CompanyId { get; set; }
    public string? FilePath { get; set; }
    public bool AnalyzeOnly { get; set; }
    public string? ImportedBy { get; set; }
}

public sealed class PremiumSheetStats
{
    public int SourceRows { get; set; }
    public int ExistingPartiesReused { get; set; }
    public int NewPartiesCreated { get; set; }
    public int PartiesAlreadyStudents { get; set; }
    public int PartiesAlreadyLeads { get; set; }
    public int PartiesAlreadyCustomers { get; set; }
    public int MembershipsCreated { get; set; }
    public int MembershipsUpdated { get; set; }
    public int MembershipsSkipped { get; set; }
    public int PhoneMatches { get; set; }
    public int EmailMatches { get; set; }
    public int PhoneAndEmailMatches { get; set; }
    public int Ambiguous { get; set; }
    public int Conflicts { get; set; }
    public int InvalidPhones { get; set; }
    public int InvalidEmails { get; set; }
    public int Failed { get; set; }
    public int ReviewQueued { get; set; }
}

public sealed class PremiumSubscriberImportReport
{
    public PremiumSheetStats Active { get; set; } = new();
    public PremiumSheetStats Inactive { get; set; } = new();
    public int ReviewRequiredRows { get; set; }
    public int ReviewRequiredQueued { get; set; }
    public int ActiveAlsoStudent { get; set; }
    public int ActiveNotStudent { get; set; }
    public int InactiveAlsoStudent { get; set; }
    public int InactiveNotStudent { get; set; }
    public bool Success { get; set; }
    public List<string> Messages { get; set; } = [];
}

public interface IPremiumSubscriberImportService
{
    Task<PremiumSubscriberImportReport> AnalyzeAsync(PremiumSubscriberImportOptions options, CancellationToken ct = default);
    Task<PremiumSubscriberImportReport> ImportAsync(PremiumSubscriberImportOptions options, CancellationToken ct = default);
}

public sealed class PremiumSubscriberImportService : IPremiumSubscriberImportService
{
    public const string SourceName = "Legacy ExactPredictions Premium Subscribers";
    public const string ExternalPrefix = "exactpredictions:";
    private const string SourceLabel = "ExactPredictions legacy premium";

    private readonly ShaunErpDbContext _db;
    private readonly IPartyIdentityService _parties;
    private readonly IIdentityMatchService _match;
    private readonly IPhoneNormalizer _phones;
    private readonly IEmailNormalizer _emails;
    private readonly ITenantContext _tenant;
    private readonly ILogger<PremiumSubscriberImportService> _logger;

    public PremiumSubscriberImportService(
        ShaunErpDbContext db,
        IPartyIdentityService parties,
        IIdentityMatchService match,
        IPhoneNormalizer phones,
        IEmailNormalizer emails,
        ITenantContext tenant,
        ILogger<PremiumSubscriberImportService> logger)
    {
        _db = db;
        _parties = parties;
        _match = match;
        _phones = phones;
        _emails = emails;
        _tenant = tenant;
        _logger = logger;
    }

    public Task<PremiumSubscriberImportReport> AnalyzeAsync(PremiumSubscriberImportOptions options, CancellationToken ct = default)
    {
        options.AnalyzeOnly = true;
        return RunAsync(options, ct);
    }

    public Task<PremiumSubscriberImportReport> ImportAsync(PremiumSubscriberImportOptions options, CancellationToken ct = default)
    {
        options.AnalyzeOnly = false;
        return RunAsync(options, ct);
    }

    private async Task<PremiumSubscriberImportReport> RunAsync(PremiumSubscriberImportOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.FilePath) || !File.Exists(options.FilePath))
        {
            throw new FileNotFoundException("Premium subscribers workbook not found.", options.FilePath);
        }

        var companyId = options.CompanyId ?? _tenant.CompanyId
            ?? throw new InvalidOperationException("CompanyId required.");
        var report = new PremiumSubscriberImportReport();
        var activeRows = ReadMemberSheet(options.FilePath!, "active_premium_subscribers");
        var oldRows = ReadMemberSheet(options.FilePath!, "old_premium_subscribers");
        var reviewRows = ReadReviewSheet(options.FilePath!);
        report.Active.SourceRows = activeRows.Count;
        report.Inactive.SourceRows = oldRows.Count;
        report.ReviewRequiredRows = reviewRows.Count;

        var plan = await EnsureHistoricalPlanAsync(companyId, ct);

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
                TotalRows = activeRows.Count + oldRows.Count + reviewRows.Count,
                CreatedDate = DateTime.UtcNow,
                Status = "Active"
            };
            _db.ImportBatches.Add(batch);
            await _db.SaveChangesAsync(ct);
        }

        await ProcessSheetAsync(companyId, plan.Id, activeRows, MembershipStatuses.Active, "active_premium_subscribers",
            options.AnalyzeOnly, batch, report.Active, ct);
        await ProcessSheetAsync(companyId, plan.Id, oldRows, MembershipStatuses.Expired, "old_premium_subscribers",
            options.AnalyzeOnly, batch, report.Inactive, ct);

        if (!options.AnalyzeOnly && batch is not null)
        {
            report.ReviewRequiredQueued = await QueueReviewRequiredAsync(companyId, batch.Id, reviewRows, ct);
            await _db.SaveChangesAsync(ct);
            batch.CompletedAt = DateTime.UtcNow;
            batch.MatchedRows = report.Active.ExistingPartiesReused + report.Inactive.ExistingPartiesReused;
            batch.CreatedRows = report.Active.MembershipsCreated + report.Inactive.MembershipsCreated;
            batch.ReviewRows = report.Active.ReviewQueued + report.Inactive.ReviewQueued + report.ReviewRequiredQueued;
            batch.FailedRows = report.Active.Failed + report.Inactive.Failed;
            batch.BatchStatus = batch.FailedRows > 0
                ? ImportBatchStatuses.Failed
                : batch.ReviewRows > 0 ? ImportBatchStatuses.CompletedWithReview : ImportBatchStatuses.Completed;
            await _db.SaveChangesAsync(ct);
            report.Messages.Add($"ImportBatch {batch.Id} status={batch.BatchStatus}");
        }
        else
        {
            report.ReviewRequiredQueued = reviewRows.Count;
        }

        await FillStudentOverlapAsync(companyId, report, ct);

        report.Success = report.Active.Failed == 0 && report.Inactive.Failed == 0;
        report.Messages.Add(options.AnalyzeOnly ? "MODE=analyze" : "MODE=import");
        report.Messages.Add($"active={report.Active.SourceRows} inactive={report.Inactive.SourceRows} reviewRequired={report.ReviewRequiredRows}");
        report.Messages.Add($"plan={plan.Name}");
        return report;
    }

    private async Task ProcessSheetAsync(
        int companyId,
        int planId,
        List<PremiumSourceRow> rows,
        string membershipStatus,
        string sheetName,
        bool analyzeOnly,
        ImportBatch? batch,
        PremiumSheetStats stats,
        CancellationToken ct)
    {
        var rowNumber = 0;
        foreach (var row in rows)
        {
            rowNumber++;
            try
            {
                var phone = _phones.Normalize(row.Tel);
                var email = _emails.Normalize(row.Email);
                if (!string.IsNullOrWhiteSpace(row.Tel) && (string.IsNullOrWhiteSpace(phone.NormalizedPhone) || phone.NormalizedPhone.Length < 8))
                {
                    stats.InvalidPhones++;
                }

                if (!string.IsNullOrWhiteSpace(row.Email) && email is null)
                {
                    stats.InvalidEmails++;
                }

                var names = SplitName(row.FullName);
                var externalRef = $"{ExternalPrefix}{row.UserId}";
                var externalId = $"ep-premium:{row.UserId}";

                var match = await _match.MatchAsync(new IdentityMatchRequest
                {
                    CompanyId = companyId,
                    FirstName = names.First,
                    LastName = names.Last,
                    DisplayName = row.FullName,
                    Phone = row.Tel,
                    Email = row.Email
                }, ct);

                if (match.Confidence == IdentityMatchConfidence.HighConfidence)
                {
                    if (match.MatchedBy == IdentityMatchBy.PhoneAndEmail) stats.PhoneAndEmailMatches++;
                    else if (match.MatchedBy == IdentityMatchBy.Phone) stats.PhoneMatches++;
                    else if (match.MatchedBy == IdentityMatchBy.Email) stats.EmailMatches++;
                }
                else if (match.Confidence == IdentityMatchConfidence.Ambiguous) stats.Ambiguous++;
                else if (match.Confidence == IdentityMatchConfidence.Conflict) stats.Conflicts++;

                int? partyId = null;

                // Idempotency: prior source row or existing membership by legacy user id
                var priorRow = await _db.ImportSourceRows.AsNoTracking()
                    .Where(r => r.ExternalId == externalId && r.MatchedPartyId != null)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync(ct);
                var existingMembership = await _db.SubscriptionMemberships
                    .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.ExternalReference == externalRef, ct);

                if (priorRow?.MatchedPartyId is int priorParty)
                {
                    partyId = priorParty;
                    stats.ExistingPartiesReused++;
                }
                else if (existingMembership is not null)
                {
                    partyId = existingMembership.PartyId;
                    stats.ExistingPartiesReused++;
                }
                else if (analyzeOnly)
                {
                    if (match.Confidence == IdentityMatchConfidence.HighConfidence && match.PartyId is int existing)
                    {
                        partyId = existing;
                        stats.ExistingPartiesReused++;
                    }
                    else if (match.Confidence is IdentityMatchConfidence.Ambiguous or IdentityMatchConfidence.Conflict)
                    {
                        stats.ReviewQueued++;
                        stats.NewPartiesCreated++;
                    }
                    else
                    {
                        stats.NewPartiesCreated++;
                    }
                }
                else
                {
                    // Ambiguous/conflict must not reuse an enrolled candidate. Shared family
                    // phones would attach the new membership to the wrong sibling; identity
                    // review does not re-link memberships after the fact.
                    if (partyId is null)
                    {
                        var ensure = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
                        {
                            CompanyId = companyId,
                            FirstName = names.First,
                            LastName = names.Last,
                            DisplayName = row.FullName,
                            Phone = row.Tel,
                            Mobile = row.Tel,
                            Email = row.Email,
                            ImportBatchId = batch?.Id,
                            ExternalId = externalId,
                            CreateReviewOnAmbiguous = true
                        }, ct);

                        partyId = ensure.PartyId;
                        if (ensure.LinkedExisting) stats.ExistingPartiesReused++;
                        if (ensure.Created) stats.NewPartiesCreated++;
                        if (ensure.NeedsReview)
                        {
                            stats.ReviewQueued++;
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
                            RawDataJson = JsonSerializer.Serialize(new
                            {
                                sheet = sheetName,
                                row.UserId,
                                row.FullName,
                                row.Email,
                                row.Tel,
                                row.Fax,
                                row.Date,
                                row.DateSource
                            }),
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
                }

                if (partyId is int linkedParty)
                {
                    if (await _db.CourseEnrollments.AnyAsync(e => e.CompanyId == companyId && e.PartyId == linkedParty, ct))
                        stats.PartiesAlreadyStudents++;
                    if (await _db.Leads.AnyAsync(l => l.CompanyId == companyId && l.PartyId == linkedParty, ct))
                        stats.PartiesAlreadyLeads++;
                    if (await _db.CrmCustomers.AnyAsync(c => c.CompanyId == companyId && c.PartyId == linkedParty, ct))
                        stats.PartiesAlreadyCustomers++;
                }

                if (analyzeOnly || partyId is null)
                {
                    continue;
                }

                var (start, end) = MapProvenDates(membershipStatus, row.Date, row.DateSource);
                var legacyDate = AsUtc(row.Date);
                if (existingMembership is null)
                {
                    existingMembership = await _db.SubscriptionMemberships
                        .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.ExternalReference == externalRef, ct);
                }

                if (existingMembership is not null)
                {
                    var changed =
                        existingMembership.MembershipStatus != membershipStatus ||
                        existingMembership.PartyId != partyId.Value ||
                        existingMembership.LegacyDate != legacyDate ||
                        existingMembership.LegacyDateSource != row.DateSource ||
                        existingMembership.StartDate != start ||
                        existingMembership.EndDate != end;

                    if (!changed)
                    {
                        stats.MembershipsSkipped++;
                    }
                    else
                    {
                        existingMembership.PartyId = partyId.Value;
                        existingMembership.PlanId = planId;
                        existingMembership.MembershipStatus = membershipStatus;
                        existingMembership.StartDate = start;
                        existingMembership.EndDate = end;
                        existingMembership.LegacyDate = legacyDate;
                        existingMembership.LegacyDateSource = row.DateSource;
                        existingMembership.Source = SourceLabel;
                        existingMembership.ImportBatchId = batch?.Id;
                        existingMembership.Notes = BuildNotes(sheetName, row);
                        existingMembership.UpdatedDate = DateTime.UtcNow;
                        stats.MembershipsUpdated++;
                    }
                }
                else
                {
                    _db.SubscriptionMemberships.Add(new SubscriptionMembership
                    {
                        CompanyId = companyId,
                        PartyId = partyId.Value,
                        PlanId = planId,
                        MembershipStatus = membershipStatus,
                        StartDate = start,
                        EndDate = end,
                        LegacyDate = legacyDate,
                        LegacyDateSource = row.DateSource,
                        Source = SourceLabel,
                        ExternalReference = externalRef,
                        ImportBatchId = batch?.Id,
                        Notes = BuildNotes(sheetName, row),
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    });
                    stats.MembershipsCreated++;
                    batch!.CreatedRows++;
                }

                if (rowNumber % 50 == 0 && batch is not null)
                {
                    await _db.SaveChangesAsync(ct);
                    var batchId = batch.Id;
                    _db.ChangeTracker.Clear();
                    batch = await _db.ImportBatches.FirstAsync(b => b.Id == batchId, ct);
                    planId = (await EnsureHistoricalPlanAsync(companyId, ct)).Id;
                }
            }
            catch (Exception ex)
            {
                stats.Failed++;
                _logger.LogWarning(ex, "Premium import failed at {Sheet} row {Row} user {UserId}", sheetName, rowNumber, row.UserId);
                if (batch is not null)
                {
                    batch.FailedRows++;
                    _db.ImportSourceRows.Add(new ImportSourceRow
                    {
                        ImportBatchId = batch.Id,
                        SourceRowNumber = rowNumber,
                        ExternalId = $"ep-premium:{row.UserId}",
                        MatchStatus = ImportMatchStatuses.Failed,
                        MatchReason = ex.Message,
                        ProcessedAt = DateTime.UtcNow,
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    });
                }
            }
        }

        if (!analyzeOnly && batch is not null)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<int> QueueReviewRequiredAsync(
        int companyId, int batchId, List<PremiumReviewRow> rows, CancellationToken ct)
    {
        var queued = 0;
        foreach (var row in rows)
        {
            var exists = await _db.ImportMatchReviews.AnyAsync(r =>
                r.CompanyId == companyId &&
                r.ImportBatchId == batchId &&
                r.IncomingName == row.FullName &&
                r.MatchReason != null && r.MatchReason.Contains($"legacy_user_id={row.UserId}"), ct);
            if (exists) continue;

            var membership = await _db.SubscriptionMemberships.AsNoTracking()
                .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.ExternalReference == $"{ExternalPrefix}{row.UserId}", ct);

            _db.ImportMatchReviews.Add(new ImportMatchReview
            {
                CompanyId = companyId,
                ImportBatchId = batchId,
                ReviewStatus = "Pending",
                MatchReason = $"review_required legacy_user_id={row.UserId}: {row.Reason}",
                IncomingName = row.FullName,
                IncomingPhone = row.Tel,
                IncomingEmail = row.Email,
                CandidatePartyId = membership?.PartyId,
                CreatedDate = DateTime.UtcNow,
                Status = "Active"
            });
            queued++;
        }

        return queued;
    }

    private async Task FillStudentOverlapAsync(int companyId, PremiumSubscriberImportReport report, CancellationToken ct)
    {
        var activePartyIds = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.MembershipStatus == MembershipStatuses.Active
                        && m.ExternalReference != null && m.ExternalReference.StartsWith(ExternalPrefix))
            .Select(m => m.PartyId).Distinct().ToListAsync(ct);
        var inactivePartyIds = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.MembershipStatus == MembershipStatuses.Expired
                        && m.ExternalReference != null && m.ExternalReference.StartsWith(ExternalPrefix))
            .Select(m => m.PartyId).Distinct().ToListAsync(ct);

        if (activePartyIds.Count == 0 && inactivePartyIds.Count == 0 &&
            (report.Active.MembershipsCreated + report.Inactive.MembershipsCreated) == 0)
        {
            // analyze-only: approximate from match stats already on sheet stats
            return;
        }

        var studentSet = await _db.CourseEnrollments.AsNoTracking()
            .Where(e => e.CompanyId == companyId)
            .Select(e => e.PartyId)
            .Distinct()
            .ToListAsync(ct);
        var students = studentSet.ToHashSet();

        report.ActiveAlsoStudent = activePartyIds.Count(id => students.Contains(id));
        report.ActiveNotStudent = activePartyIds.Count - report.ActiveAlsoStudent;
        report.InactiveAlsoStudent = inactivePartyIds.Count(id => students.Contains(id));
        report.InactiveNotStudent = inactivePartyIds.Count - report.InactiveAlsoStudent;
    }

    private async Task<SubscriptionPlan> EnsureHistoricalPlanAsync(int companyId, CancellationToken ct)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Name == SubscriptionPlanNames.ExactPredictionsPremiumHistorical, ct);
        if (plan is not null) return plan;

        plan = new SubscriptionPlan
        {
            CompanyId = companyId,
            Name = SubscriptionPlanNames.ExactPredictionsPremiumHistorical,
            DurationMonths = 0,
            Status = "Active",
            ProductSku = "exactpredictions-premium-historical",
            Description = "Historical ExactPredictions premium membership when specific plan duration is unknown",
            CreatedDate = DateTime.UtcNow
        };
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan;
    }

    private static (DateTime? Start, DateTime? End) MapProvenDates(string status, DateTime? date, string? dateSource)
    {
        if (date is null || string.IsNullOrWhiteSpace(dateSource))
            return (null, null);

        var src = dateSource;
        if (status == MembershipStatuses.Active &&
            src.Contains("level change 1->7", StringComparison.OrdinalIgnoreCase))
        {
            return (AsUtc(date), null);
        }

        if (status == MembershipStatuses.Expired &&
            src.Contains("level change 7->1", StringComparison.OrdinalIgnoreCase))
        {
            return (null, AsUtc(date));
        }

        // Registration-only / no audit evidence — preserve as LegacyDate only
        return (null, null);
    }

    private static DateTime? AsUtc(DateTime? d)
    {
        if (d is null) return null;
        return d.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(d.Value, DateTimeKind.Utc)
            : d.Value.ToUniversalTime();
    }

    private static string BuildNotes(string sheet, PremiumSourceRow row)
    {
        var fax = string.IsNullOrWhiteSpace(row.Fax) ? null : $"whatsapp_dest={row.Fax}";
        return string.Join(" | ", new[]
        {
            $"sheet={sheet}",
            $"legacy_user_id={row.UserId}",
            fax
        }.Where(x => x is not null));
    }

    private static List<PremiumSourceRow> ReadMemberSheet(string path, string sheetName)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.FirstOrDefault(s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                 ?? throw new InvalidOperationException($"Sheet '{sheetName}' not found.");
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = ws.FirstRowUsed() ?? throw new InvalidOperationException("Workbook sheet is empty.");
        foreach (var cell in headerRow.CellsUsed())
            headers[cell.GetString().Trim()] = cell.Address.ColumnNumber;

        int Col(string name) => headers.TryGetValue(name, out var c) ? c : -1;
        var userCol = Col("user_id");
        var nameCol = Col("full_name");
        var emailCol = Col("email_id");
        var telCol = Col("tel");
        var faxCol = Col("fax");
        var dateCol = Col("date");
        var dateSourceCol = Col("date_source");

        var rows = new List<PremiumSourceRow>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var userRaw = userCol > 0 ? row.Cell(userCol).GetFormattedString().Trim() : "";
            if (!int.TryParse(userRaw, out var userId) || userId <= 0) continue;
            var fullName = nameCol > 0 ? row.Cell(nameCol).GetString().Trim() : "";
            if (string.IsNullOrWhiteSpace(fullName)) fullName = $"User {userId}";

            DateTime? date = null;
            if (dateCol > 0)
            {
                var cell = row.Cell(dateCol);
                if (cell.TryGetValue(out DateTime dt)) date = dt.Date;
                else if (DateTime.TryParse(cell.GetFormattedString(), out var parsed)) date = parsed.Date;
            }

            rows.Add(new PremiumSourceRow
            {
                UserId = userId,
                FullName = fullName,
                Email = emailCol > 0 ? NullIfEmpty(row.Cell(emailCol).GetString()) : null,
                Tel = telCol > 0 ? NullIfEmpty(row.Cell(telCol).GetFormattedString()) : null,
                Fax = faxCol > 0 ? NullIfEmpty(row.Cell(faxCol).GetFormattedString()) : null,
                Date = date,
                DateSource = dateSourceCol > 0 ? NullIfEmpty(row.Cell(dateSourceCol).GetString()) : null
            });
        }

        return rows;
    }

    private static List<PremiumReviewRow> ReadReviewSheet(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.FirstOrDefault(s => s.Name.Equals("review_required", StringComparison.OrdinalIgnoreCase));
        if (ws is null) return [];

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = ws.FirstRowUsed();
        if (headerRow is null) return [];
        foreach (var cell in headerRow.CellsUsed())
            headers[cell.GetString().Trim()] = cell.Address.ColumnNumber;

        int Col(string name) => headers.TryGetValue(name, out var c) ? c : -1;
        var rows = new List<PremiumReviewRow>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var userRaw = Col("user_id") > 0 ? row.Cell(Col("user_id")).GetFormattedString().Trim() : "";
            if (!int.TryParse(userRaw, out var userId)) continue;
            rows.Add(new PremiumReviewRow
            {
                UserId = userId,
                FullName = Col("full_name") > 0 ? row.Cell(Col("full_name")).GetString().Trim() : "",
                Email = Col("email_id") > 0 ? NullIfEmpty(row.Cell(Col("email_id")).GetString()) : null,
                Tel = Col("tel") > 0 ? NullIfEmpty(row.Cell(Col("tel")).GetFormattedString()) : null,
                Fax = Col("fax") > 0 ? NullIfEmpty(row.Cell(Col("fax")).GetFormattedString()) : null,
                Reason = Col("reason") > 0 ? row.Cell(Col("reason")).GetString().Trim() : ""
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

    private sealed class PremiumSourceRow
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = "";
        public string? Email { get; init; }
        public string? Tel { get; init; }
        public string? Fax { get; init; }
        public DateTime? Date { get; init; }
        public string? DateSource { get; init; }
    }

    private sealed class PremiumReviewRow
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = "";
        public string? Email { get; init; }
        public string? Tel { get; init; }
        public string? Fax { get; init; }
        public string Reason { get; init; } = "";
    }
}
