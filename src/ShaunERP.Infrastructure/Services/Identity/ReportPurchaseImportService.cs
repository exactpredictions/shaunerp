using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services.Identity;

public sealed class ReportPurchaseImportOptions
{
    public int? CompanyId { get; set; }
    public string? FilePath { get; set; }
    public bool AnalyzeOnly { get; set; }
    public string? ImportedBy { get; set; }
}

public sealed class ReportPurchaseImportReport
{
    public int TotalSourcePurchases { get; set; }
    public int HoroscopeSource { get; set; }
    public int NumerologySource { get; set; }
    public int ExistingPartiesReused { get; set; }
    public int NewPartiesCreated { get; set; }
    public int PurchasesCreated { get; set; }
    public int PurchasesSkipped { get; set; }
    public int RowsProcessed { get; set; }
    public int RowsFailed { get; set; }
    public int ReviewQueued { get; set; }
    public int PhoneAndEmailMatches { get; set; }
    public int PhoneOnlyMatches { get; set; }
    public int EmailOnlyMatches { get; set; }
    public int Ambiguous { get; set; }
    public int Conflicts { get; set; }
    public int InvalidIdentityRecords { get; set; }
    public int AlreadyStudents { get; set; }
    public int AlreadyActiveSubscribers { get; set; }
    public int AlreadyInactiveSubscribers { get; set; }
    public int AlreadyCustomers { get; set; }
    public int AlreadyLeads { get; set; }
    public int UniquePurchasersProjected { get; set; }
    public int UniquePurchasersActual { get; set; }
    public int RepeatPurchasers { get; set; }
    public int PurchasedBothTypes { get; set; }
    public int HoroscopeImported { get; set; }
    public int NumerologyImported { get; set; }
    public int UniqueHoroscopePurchasers { get; set; }
    public int UniqueNumerologyPurchasers { get; set; }
    public int PeopleWithGt1Horoscope { get; set; }
    public int PeopleWithGt1Numerology { get; set; }
    public int PeopleWithGt3Total { get; set; }
    public int ReportPurchasersWhoAreStudents { get; set; }
    public int ReportPurchasersActiveSubscribers { get; set; }
    public int ReportPurchasersInactiveSubscribers { get; set; }
    public int InactiveSubscribersWhoPurchasedReports { get; set; }
    public int? ImportBatchId { get; set; }
    public bool Success { get; set; }
    public List<string> Messages { get; set; } = [];
}

public interface IReportPurchaseImportService
{
    Task<ReportPurchaseImportReport> AnalyzeAsync(ReportPurchaseImportOptions options, CancellationToken ct = default);
    Task<ReportPurchaseImportReport> ImportAsync(ReportPurchaseImportOptions options, CancellationToken ct = default);
}

public sealed class ReportPurchaseImportService : IReportPurchaseImportService
{
    public const string SourceName = "Legacy ExactPredictions Report Purchases";
    public const string SourceSystem = "ExactPredictions";
    private const string ExternalIdPrefix = "ep-report:";

    private readonly ShaunErpDbContext _db;
    private readonly IPartyIdentityService _parties;
    private readonly IIdentityMatchService _match;
    private readonly IPhoneNormalizer _phones;
    private readonly IEmailNormalizer _emails;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ReportPurchaseImportService> _logger;

    public ReportPurchaseImportService(
        ShaunErpDbContext db,
        IPartyIdentityService parties,
        IIdentityMatchService match,
        IPhoneNormalizer phones,
        IEmailNormalizer emails,
        ITenantContext tenant,
        ILogger<ReportPurchaseImportService> logger)
    {
        _db = db;
        _parties = parties;
        _match = match;
        _phones = phones;
        _emails = emails;
        _tenant = tenant;
        _logger = logger;
    }

    public Task<ReportPurchaseImportReport> AnalyzeAsync(ReportPurchaseImportOptions options, CancellationToken ct = default)
    {
        options.AnalyzeOnly = true;
        return RunAsync(options, ct);
    }

    public Task<ReportPurchaseImportReport> ImportAsync(ReportPurchaseImportOptions options, CancellationToken ct = default)
    {
        options.AnalyzeOnly = false;
        return RunAsync(options, ct);
    }

    private async Task<ReportPurchaseImportReport> RunAsync(ReportPurchaseImportOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.FilePath) || !File.Exists(options.FilePath))
        {
            throw new FileNotFoundException("Report purchases CSV not found.", options.FilePath);
        }

        var companyId = options.CompanyId ?? _tenant.CompanyId
            ?? throw new InvalidOperationException("CompanyId required.");
        var rows = ReadCsv(options.FilePath!);
        var report = new ReportPurchaseImportReport
        {
            TotalSourcePurchases = rows.Count,
            HoroscopeSource = rows.Count(r => IsHoroscope(r.ReportType)),
            NumerologySource = rows.Count(r => IsNumerology(r.ReportType))
        };

        var products = await EnsureProductsAsync(companyId, ct);
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

        var projectedPartyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actualPartyIds = new HashSet<int>();
        var alreadyStudentIds = new HashSet<int>();
        var alreadyActiveSubIds = new HashSet<int>();
        var alreadyInactiveSubIds = new HashSet<int>();
        var alreadyCustomerIds = new HashSet<int>();
        var alreadyLeadIds = new HashSet<int>();
        var rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;
            report.RowsProcessed++;
            try
            {
                if (!string.Equals(row.PaymentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    report.Messages.Add($"Row {rowNumber}: skipped non-Completed payment_status={row.PaymentStatus}");
                    continue;
                }

                var phone = _phones.Normalize(row.Phone);
                var email = _emails.Normalize(row.Email);
                var hasPhone = !string.IsNullOrWhiteSpace(phone.NormalizedPhone) && phone.NormalizedPhone.Length >= 8;
                var hasEmail = email is not null;
                if (!hasPhone && !hasEmail && string.IsNullOrWhiteSpace(row.Name))
                {
                    report.InvalidIdentityRecords++;
                }
                else if (!string.IsNullOrWhiteSpace(row.Phone) && !hasPhone)
                {
                    report.InvalidIdentityRecords++;
                }
                else if (!string.IsNullOrWhiteSpace(row.Email) && !hasEmail)
                {
                    report.InvalidIdentityRecords++;
                }

                var names = SplitName(row.Name);
                var externalRef = BuildExternalReference(row.SourceFile, row.SourceId);
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
                var priorPartyId = await _parties.FindPriorImportedPartyIdAsync(companyId, externalId, ct);
                var existingPurchase = await _db.ReportPurchases
                    .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.ExternalReference == externalRef, ct);

                if (priorPartyId is int priorParty)
                {
                    partyId = priorParty;
                    report.ExistingPartiesReused++;
                }
                else if (existingPurchase is not null)
                {
                    partyId = existingPurchase.PartyId;
                    report.ExistingPartiesReused++;
                }

                if (options.AnalyzeOnly)
                {
                    if (partyId is int known)
                    {
                        projectedPartyKeys.Add($"id:{known}");
                    }
                    else if (match.Confidence == IdentityMatchConfidence.HighConfidence && match.PartyId is int existing)
                    {
                        partyId = existing;
                        report.ExistingPartiesReused++;
                        projectedPartyKeys.Add($"id:{existing}");
                    }
                    else if (match.Confidence is IdentityMatchConfidence.Ambiguous or IdentityMatchConfidence.Conflict)
                    {
                        report.ReviewQueued++;
                        report.NewPartiesCreated++;
                        var key = hasPhone ? $"p:{phone.NormalizedPhone}" : hasEmail ? $"e:{email}" : $"new:{externalRef}";
                        projectedPartyKeys.Add(key);
                    }
                    else
                    {
                        report.NewPartiesCreated++;
                        var key = hasPhone ? $"p:{phone.NormalizedPhone}" : hasEmail ? $"e:{email}" : $"n:{row.Name}:{externalRef}";
                        projectedPartyKeys.Add(key);
                    }

                    if (existingPurchase is not null) report.PurchasesSkipped++;
                    else report.PurchasesCreated++;

                    if (partyId is int analyzeParty)
                    {
                        actualPartyIds.Add(analyzeParty);
                        if (await _db.CourseEnrollments.AnyAsync(e => e.CompanyId == companyId && e.PartyId == analyzeParty, ct))
                            alreadyStudentIds.Add(analyzeParty);
                        if (await _db.SubscriptionMemberships.AnyAsync(m =>
                                m.CompanyId == companyId && m.PartyId == analyzeParty && m.MembershipStatus == MembershipStatuses.Active, ct))
                            alreadyActiveSubIds.Add(analyzeParty);
                        else if (await _db.SubscriptionMemberships.AnyAsync(m =>
                                     m.CompanyId == companyId && m.PartyId == analyzeParty && m.MembershipStatus != MembershipStatuses.Active, ct))
                            alreadyInactiveSubIds.Add(analyzeParty);
                        if (await _db.CrmCustomers.AnyAsync(c => c.CompanyId == companyId && c.PartyId == analyzeParty, ct))
                            alreadyCustomerIds.Add(analyzeParty);
                        if (await _db.Leads.AnyAsync(l => l.CompanyId == companyId && l.PartyId == analyzeParty, ct))
                            alreadyLeadIds.Add(analyzeParty);
                    }

                    continue;
                }

                if (partyId is null)
                {
                    if (match.Confidence is IdentityMatchConfidence.Ambiguous or IdentityMatchConfidence.Conflict
                        && match.Candidates.Count > 0)
                    {
                        var candidateIds = match.Candidates.Select(c => c.PartyId).Distinct().ToList();
                        var preferred = await _db.CourseEnrollments.AsNoTracking()
                            .Where(e => e.CompanyId == companyId && candidateIds.Contains(e.PartyId))
                            .OrderBy(e => e.PartyId)
                            .Select(e => (int?)e.PartyId)
                            .FirstOrDefaultAsync(ct)
                            ?? await _db.SubscriptionMemberships.AsNoTracking()
                                .Where(m => m.CompanyId == companyId && candidateIds.Contains(m.PartyId))
                                .OrderBy(m => m.PartyId)
                                .Select(m => (int?)m.PartyId)
                                .FirstOrDefaultAsync(ct)
                            ?? await _db.ReportPurchases.AsNoTracking()
                                .Where(p => p.CompanyId == companyId && candidateIds.Contains(p.PartyId))
                                .OrderBy(p => p.PartyId)
                                .Select(p => (int?)p.PartyId)
                                .FirstOrDefaultAsync(ct);

                        if (preferred is int reuse)
                        {
                            partyId = reuse;
                            report.ExistingPartiesReused++;
                            report.ReviewQueued++;
                            batch!.ReviewRows++;
                        }
                    }
                }

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

                if (partyId is int linkedParty)
                {
                    actualPartyIds.Add(linkedParty);
                    if (await _db.CourseEnrollments.AnyAsync(e => e.CompanyId == companyId && e.PartyId == linkedParty, ct))
                        alreadyStudentIds.Add(linkedParty);
                    if (await _db.SubscriptionMemberships.AnyAsync(m =>
                            m.CompanyId == companyId && m.PartyId == linkedParty && m.MembershipStatus == MembershipStatuses.Active, ct))
                        alreadyActiveSubIds.Add(linkedParty);
                    else if (await _db.SubscriptionMemberships.AnyAsync(m =>
                                 m.CompanyId == companyId && m.PartyId == linkedParty && m.MembershipStatus != MembershipStatuses.Active, ct))
                        alreadyInactiveSubIds.Add(linkedParty);
                    if (await _db.CrmCustomers.AnyAsync(c => c.CompanyId == companyId && c.PartyId == linkedParty, ct))
                        alreadyCustomerIds.Add(linkedParty);
                    if (await _db.Leads.AnyAsync(l => l.CompanyId == companyId && l.PartyId == linkedParty, ct))
                        alreadyLeadIds.Add(linkedParty);

                    if (existingPurchase is not null)
                    {
                        report.PurchasesSkipped++;
                    }
                    else
                    {
                        var product = ResolveProduct(products, row.ReportType);
                        _db.ReportPurchases.Add(new ReportPurchase
                        {
                            CompanyId = companyId,
                            PartyId = linkedParty,
                            ReportProductId = product.Id,
                            SourceSystem = SourceSystem,
                            SourceFile = NullIfEmpty(row.SourceFile),
                            SourceId = NullIfEmpty(row.SourceId),
                            ExternalReference = externalRef,
                            AmountPaid = ParseDecimal(row.AmountPaid),
                            Currency = NullIfEmpty(row.Currency) ?? "INR",
                            PaymentStatus = "Completed",
                            ReportStatus = NullIfEmpty(row.ReportStatus),
                            GeneratedAt = AsUtc(ParseDate(row.GeneratedAt)),
                            DeliveredAt = AsUtc(ParseDate(row.DeliveredAt)),
                            PaymentDate = AsUtc(ParseDate(row.PaymentDate)),
                            DeliveryEmailStatus = NullIfEmpty(row.DeliveryEmailStatus),
                            DeliveryWhatsappStatus = NullIfEmpty(row.DeliveryWhatsappStatus),
                            RazorpayOrderId = NullIfEmpty(row.RazorpayOrderId),
                            RazorpayPaymentId = NullIfEmpty(row.RazorpayPaymentId),
                            RazorpayStatus = NullIfEmpty(row.RazorpayStatus),
                            SubjectName = NullIfEmpty(row.Name),
                            SubjectGender = NullIfEmpty(row.Gender),
                            BirthDay = ParseInt(row.BirthDay),
                            BirthMonth = ParseInt(row.BirthMonth),
                            BirthYear = ParseInt(row.BirthYear),
                            BirthHours = ParseInt(row.BirthHours),
                            BirthMinutes = ParseInt(row.BirthMinutes),
                            BirthSeconds = ParseInt(row.BirthSeconds),
                            Place = NullIfEmpty(row.Place),
                            ImportBatchId = batch!.Id,
                            Notes = NullIfEmpty(row.Remarks),
                            CreatedDate = DateTime.UtcNow,
                            Status = "Active"
                        });
                        report.PurchasesCreated++;
                        if (IsHoroscope(row.ReportType)) report.HoroscopeImported++;
                        else if (IsNumerology(row.ReportType)) report.NumerologyImported++;
                    }
                }

                if (rowNumber % 25 == 0)
                {
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                report.RowsFailed++;
                _logger.LogWarning(ex, "Report purchase import failed at row {Row}", rowNumber);
                report.Messages.Add($"Row {rowNumber}: {ex.Message}");
                foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        if (!options.AnalyzeOnly && batch is not null)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Final report-purchase batch save failed");
                report.RowsFailed++;
                report.Messages.Add($"FinalSave: {ex.Message}");
                foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }

            batch.CompletedAt = DateTime.UtcNow;
            batch.MatchedRows = report.ExistingPartiesReused;
            batch.CreatedRows = report.PurchasesCreated;
            batch.ReviewRows = report.ReviewQueued;
            batch.FailedRows = report.RowsFailed;
            batch.BatchStatus = batch.FailedRows > 0
                ? ImportBatchStatuses.Failed
                : batch.ReviewRows > 0 ? ImportBatchStatuses.CompletedWithReview : ImportBatchStatuses.Completed;
            await _db.SaveChangesAsync(ct);
            report.Messages.Add($"ImportBatch {batch.Id} status={batch.BatchStatus}");
        }

        report.AlreadyStudents = alreadyStudentIds.Count;
        report.AlreadyActiveSubscribers = alreadyActiveSubIds.Count;
        report.AlreadyInactiveSubscribers = alreadyInactiveSubIds.Count;
        report.AlreadyCustomers = alreadyCustomerIds.Count;
        report.AlreadyLeads = alreadyLeadIds.Count;
        report.UniquePurchasersProjected = options.AnalyzeOnly ? projectedPartyKeys.Count : actualPartyIds.Count;
        report.UniquePurchasersActual = await _db.ReportPurchases.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .Select(p => p.PartyId)
            .Distinct()
            .CountAsync(ct);

        await FillAnalyticsAsync(companyId, report, ct);

        report.Success = report.RowsFailed == 0;
        report.Messages.Add(options.AnalyzeOnly ? "MODE=analyze" : "MODE=import");
        report.Messages.Add($"source={report.TotalSourcePurchases} horoscope={report.HoroscopeSource} numerology={report.NumerologySource}");
        return report;
    }

    private async Task FillAnalyticsAsync(int companyId, ReportPurchaseImportReport report, CancellationToken ct)
    {
        var purchases = await (
            from rp in _db.ReportPurchases.AsNoTracking()
            join prod in _db.ReportProducts.AsNoTracking() on rp.ReportProductId equals prod.Id
            where rp.CompanyId == companyId
            select new { rp.PartyId, prod.ReportType, prod.Name }).ToListAsync(ct);

        if (purchases.Count == 0) return;

        var byParty = purchases.GroupBy(p => p.PartyId).ToList();
        report.RepeatPurchasers = byParty.Count(g => g.Count() > 1);
        report.UniqueHoroscopePurchasers = byParty.Count(g => g.Any(x => IsHoroscope(x.ReportType) || IsHoroscope(x.Name)));
        report.UniqueNumerologyPurchasers = byParty.Count(g => g.Any(x => IsNumerology(x.ReportType) || IsNumerology(x.Name)));
        report.PurchasedBothTypes = byParty.Count(g =>
            g.Any(x => IsHoroscope(x.ReportType) || IsHoroscope(x.Name)) &&
            g.Any(x => IsNumerology(x.ReportType) || IsNumerology(x.Name)));
        report.PeopleWithGt1Horoscope = byParty.Count(g => g.Count(x => IsHoroscope(x.ReportType) || IsHoroscope(x.Name)) > 1);
        report.PeopleWithGt1Numerology = byParty.Count(g => g.Count(x => IsNumerology(x.ReportType) || IsNumerology(x.Name)) > 1);
        report.PeopleWithGt3Total = byParty.Count(g => g.Count() > 3);

        var purchaserIds = byParty.Select(g => g.Key).ToList();
        report.ReportPurchasersWhoAreStudents = await _db.CourseEnrollments.AsNoTracking()
            .Where(e => e.CompanyId == companyId && purchaserIds.Contains(e.PartyId))
            .Select(e => e.PartyId).Distinct().CountAsync(ct);
        report.ReportPurchasersActiveSubscribers = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId && purchaserIds.Contains(m.PartyId) && m.MembershipStatus == MembershipStatuses.Active)
            .Select(m => m.PartyId).Distinct().CountAsync(ct);
        report.ReportPurchasersInactiveSubscribers = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId && purchaserIds.Contains(m.PartyId) && m.MembershipStatus != MembershipStatuses.Active)
            .Select(m => m.PartyId).Distinct().CountAsync(ct);
        report.InactiveSubscribersWhoPurchasedReports = report.ReportPurchasersInactiveSubscribers;
    }

    private async Task<Dictionary<string, ReportProduct>> EnsureProductsAsync(int companyId, CancellationToken ct)
    {
        var map = await _db.ReportProducts
            .Where(p => p.CompanyId == companyId)
            .ToDictionaryAsync(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase, ct);

        async Task Ensure(string name, string type)
        {
            if (map.ContainsKey(name)) return;
            var product = new ReportProduct
            {
                CompanyId = companyId,
                Name = name,
                ReportType = type,
                Description = $"Individual {name} purchases",
                CreatedDate = DateTime.UtcNow,
                Status = "Active"
            };
            _db.ReportProducts.Add(product);
            await _db.SaveChangesAsync(ct);
            map[name] = product;
        }

        await Ensure(ReportProductNames.HoroscopeReport, "Horoscope");
        await Ensure(ReportProductNames.NumerologyReport, "Numerology");
        return map;
    }

    private static ReportProduct ResolveProduct(Dictionary<string, ReportProduct> products, string reportType)
    {
        if (IsHoroscope(reportType)) return products[ReportProductNames.HoroscopeReport];
        if (IsNumerology(reportType)) return products[ReportProductNames.NumerologyReport];
        throw new InvalidOperationException($"Unknown report_type: {reportType}");
    }

    private static List<ReportCsvRow> ReadCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return [];
        var header = ParseCsvLine(lines[0]);
        var index = header
            .Select((h, i) => (h: h.Trim().ToLowerInvariant(), i))
            .ToDictionary(x => x.h, x => x.i, StringComparer.OrdinalIgnoreCase);

        string Cell(string[] cols, string name) =>
            index.TryGetValue(name, out var i) && i < cols.Length ? cols[i].Trim() : string.Empty;

        var rows = new List<ReportCsvRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cols = ParseCsvLine(lines[i]);
            rows.Add(new ReportCsvRow
            {
                ReportType = Clean(Cell(cols, "report_type")),
                SourceFile = Clean(Cell(cols, "source_file")),
                SourceId = Clean(Cell(cols, "source_id")),
                Name = Clean(Cell(cols, "name")),
                Phone = Clean(Cell(cols, "phone")),
                Email = Clean(Cell(cols, "email")),
                Gender = Clean(Cell(cols, "gender")),
                BirthDay = Clean(Cell(cols, "birth_day")),
                BirthMonth = Clean(Cell(cols, "birth_month")),
                BirthYear = Clean(Cell(cols, "birth_year")),
                BirthHours = Clean(Cell(cols, "birth_hours")),
                BirthMinutes = Clean(Cell(cols, "birth_minutes")),
                BirthSeconds = Clean(Cell(cols, "birth_seconds")),
                Place = Clean(Cell(cols, "place")),
                PaymentStatus = Clean(Cell(cols, "payment_status")),
                AmountPaid = Clean(Cell(cols, "amount_paid")),
                Currency = Clean(Cell(cols, "currency")),
                ReportStatus = Clean(Cell(cols, "report_status")),
                GeneratedAt = Clean(Cell(cols, "generated_at")),
                DeliveredAt = Clean(Cell(cols, "delivered_at")),
                PaymentDate = Clean(Cell(cols, "payment_date")),
                DeliveryEmailStatus = Clean(Cell(cols, "delivery_email_status")),
                DeliveryWhatsappStatus = Clean(Cell(cols, "delivery_whatsapp_status")),
                RazorpayOrderId = Clean(Cell(cols, "razorpay_order_id")),
                RazorpayPaymentId = Clean(Cell(cols, "razorpay_payment_id")),
                RazorpayStatus = Clean(Cell(cols, "razorpay_status")),
                Remarks = Clean(Cell(cols, "remarks"))
            });
        }

        return rows;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string BuildExternalReference(string sourceFile, string sourceId)
        => $"{sourceFile.Trim()}:{sourceId.Trim()}";

    private static bool IsHoroscope(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains("Horoscope", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumerology(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains("Numerology", StringComparison.OrdinalIgnoreCase);

    private static (string? First, string? Last) SplitName(string? fullName)
    {
        var parts = (fullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return (null, null);
        if (parts.Length == 1) return (parts[0], null);
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static string? NullIfEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static string Clean(string? value) =>
        (value ?? string.Empty).Replace("\0", string.Empty, StringComparison.Ordinal).Trim();

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
            return d;
        return null;
    }

    private static DateTime? AsUtc(DateTime? d) =>
        d is null ? null : DateTime.SpecifyKind(d.Value, DateTimeKind.Utc);

    private sealed class ReportCsvRow
    {
        public string ReportType { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string BirthDay { get; set; } = string.Empty;
        public string BirthMonth { get; set; } = string.Empty;
        public string BirthYear { get; set; } = string.Empty;
        public string BirthHours { get; set; } = string.Empty;
        public string BirthMinutes { get; set; } = string.Empty;
        public string BirthSeconds { get; set; } = string.Empty;
        public string Place { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string AmountPaid { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string ReportStatus { get; set; } = string.Empty;
        public string GeneratedAt { get; set; } = string.Empty;
        public string DeliveredAt { get; set; } = string.Empty;
        public string PaymentDate { get; set; } = string.Empty;
        public string DeliveryEmailStatus { get; set; } = string.Empty;
        public string DeliveryWhatsappStatus { get; set; } = string.Empty;
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string RazorpayStatus { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}
