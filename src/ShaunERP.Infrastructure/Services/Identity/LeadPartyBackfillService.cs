using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services.Identity;

public sealed class LeadPartyBackfillOptions
{
    public int? CompanyId { get; set; }
    public int BatchSize { get; set; } = 200;
    public bool AnalyzeOnly { get; set; }
    public string? ImportedBy { get; set; }
}

public sealed class LeadPartyBackfillReport
{
    public int TotalLeads { get; set; }
    public int LeadsWithPartyId { get; set; }
    public int LeadsWithoutPartyId { get; set; }
    public int TotalPartyPersons { get; set; }
    public int TotalPartyPhones { get; set; }
    public int TotalPartyEmails { get; set; }
    public int CreatedParties { get; set; }
    public int LinkedExisting { get; set; }
    public int ReviewQueued { get; set; }
    public int Failed { get; set; }
    public int InvalidPhones { get; set; }
    public int InvalidEmails { get; set; }
    public int DuplicateNormalizedPhoneGroups { get; set; }
    public int DuplicateNormalizedEmailGroups { get; set; }
    public int ExactPhoneMatches { get; set; }
    public int ExactEmailMatches { get; set; }
    public int PhoneAndEmailMatches { get; set; }
    public int AmbiguousRecords { get; set; }
    public int ConflictingRecords { get; set; }
    public List<string> Messages { get; set; } = [];
    public bool Success { get; set; }
}

public interface ILeadPartyBackfillService
{
    Task<LeadPartyBackfillReport> AnalyzeAsync(LeadPartyBackfillOptions options, CancellationToken ct = default);
    Task<LeadPartyBackfillReport> BackfillAsync(LeadPartyBackfillOptions options, CancellationToken ct = default);
}

public sealed class LeadPartyBackfillService : ILeadPartyBackfillService
{
    private readonly ShaunErpDbContext _db;
    private readonly IPartyIdentityService _parties;
    private readonly IIdentityMatchService _match;
    private readonly IPhoneNormalizer _phones;
    private readonly IEmailNormalizer _emails;
    private readonly ITenantContext _tenant;
    private readonly ILogger<LeadPartyBackfillService> _logger;

    public LeadPartyBackfillService(
        ShaunErpDbContext db,
        IPartyIdentityService parties,
        IIdentityMatchService match,
        IPhoneNormalizer phones,
        IEmailNormalizer emails,
        ITenantContext tenant,
        ILogger<LeadPartyBackfillService> logger)
    {
        _db = db;
        _parties = parties;
        _match = match;
        _phones = phones;
        _emails = emails;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<LeadPartyBackfillReport> AnalyzeAsync(LeadPartyBackfillOptions options, CancellationToken ct = default)
    {
        var companyId = await ResolveCompanyIdAsync(options.CompanyId, ct);
        var report = await SnapshotAsync(companyId, ct);
        report.Messages.Add($"Analyze-only for companyId={companyId}");

        var leads = await _db.Leads.AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .Select(l => new { l.Id, l.FirstName, l.LastName, l.Email, l.Phone, l.Mobile, l.PartyId })
            .ToListAsync(ct);

        var phoneGroups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var emailGroups = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var lead in leads)
        {
            var phone = _phones.Normalize(FirstNonEmpty(lead.Mobile, lead.Phone));
            if (string.IsNullOrWhiteSpace(phone.NormalizedPhone) || phone.NormalizedPhone.Length < 8)
            {
                if (!string.IsNullOrWhiteSpace(lead.Phone) || !string.IsNullOrWhiteSpace(lead.Mobile))
                {
                    report.InvalidPhones++;
                }
            }
            else if (phone.IsValidIndianMobile || phone.NormalizedPhone.Length >= 8)
            {
                if (!phoneGroups.TryGetValue(phone.NormalizedPhone, out var list))
                {
                    list = [];
                    phoneGroups[phone.NormalizedPhone] = list;
                }

                list.Add(lead.Id);
            }

            var email = _emails.Normalize(lead.Email);
            if (string.IsNullOrWhiteSpace(lead.Email))
            {
                // ok
            }
            else if (email is null || !email.Contains('@'))
            {
                report.InvalidEmails++;
            }
            else
            {
                if (!emailGroups.TryGetValue(email, out var list))
                {
                    list = [];
                    emailGroups[email] = list;
                }

                list.Add(lead.Id);
            }

            if (lead.PartyId is null)
            {
                var match = await _match.MatchAsync(new IdentityMatchRequest
                {
                    CompanyId = companyId,
                    FirstName = lead.FirstName,
                    LastName = lead.LastName,
                    Phone = FirstNonEmpty(lead.Mobile, lead.Phone),
                    Email = lead.Email
                }, ct);

                switch (match.Confidence)
                {
                    case IdentityMatchConfidence.HighConfidence when match.MatchedBy == IdentityMatchBy.PhoneAndEmail:
                        report.PhoneAndEmailMatches++;
                        break;
                    case IdentityMatchConfidence.HighConfidence when match.MatchedBy == IdentityMatchBy.Phone:
                        report.ExactPhoneMatches++;
                        break;
                    case IdentityMatchConfidence.HighConfidence when match.MatchedBy == IdentityMatchBy.Email:
                        report.ExactEmailMatches++;
                        break;
                    case IdentityMatchConfidence.Ambiguous:
                        report.AmbiguousRecords++;
                        break;
                    case IdentityMatchConfidence.Conflict:
                        report.ConflictingRecords++;
                        break;
                }
            }
        }

        report.DuplicateNormalizedPhoneGroups = phoneGroups.Count(kv => kv.Value.Count > 1);
        report.DuplicateNormalizedEmailGroups = emailGroups.Count(kv => kv.Value.Count > 1);
        report.Success = true;
        report.Messages.Add(
            $"Lead-level duplicate phone groups={report.DuplicateNormalizedPhoneGroups}; email groups={report.DuplicateNormalizedEmailGroups}");
        return report;
    }

    public async Task<LeadPartyBackfillReport> BackfillAsync(LeadPartyBackfillOptions options, CancellationToken ct = default)
    {
        if (options.AnalyzeOnly)
        {
            return await AnalyzeAsync(options, ct);
        }

        var companyId = await ResolveCompanyIdAsync(options.CompanyId, ct);
        var batchSize = Math.Clamp(options.BatchSize, 50, 1000);

        // Close any previously interrupted run
        var running = await _db.ImportBatches
            .Where(b => b.CompanyId == companyId && b.SourceName == "lead-party-backfill" && b.BatchStatus == ImportBatchStatuses.Running)
            .ToListAsync(ct);
        foreach (var old in running)
        {
            old.BatchStatus = ImportBatchStatuses.Failed;
            old.CompletedAt = DateTime.UtcNow;
            old.Notes = "Interrupted; superseded by a new idempotent backfill run.";
        }

        var batch = new ImportBatch
        {
            CompanyId = companyId,
            SourceName = "lead-party-backfill",
            FileName = "Leads",
            StartedAt = DateTime.UtcNow,
            ImportedBy = options.ImportedBy ?? _tenant.UserId?.ToString() ?? "system",
            BatchStatus = ImportBatchStatuses.Running,
            CreatedDate = DateTime.UtcNow,
            Status = "Active",
            Notes = "Fast path: in-memory phone/email index + batched saves."
        };
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        // Warm identity indexes once (avoids per-lead MatchAsync DB roundtrips).
        // Track every party per phone/email — TryAdd would hide shared/family numbers
        // and silently attach leads to whichever party happened to be indexed first.
        var phoneToParties = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var emailToParties = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var partyNames = new Dictionary<int, string>();

        var phoneRows = await (
            from ph in _db.PartyPhones.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on ph.PartyId equals p.Id
            where p.CompanyId == companyId && ph.IsActive && ph.NormalizedPhone != null
            select new { ph.NormalizedPhone, ph.PartyId, p.DisplayName }).ToListAsync(ct);
        foreach (var row in phoneRows)
        {
            IndexParty(phoneToParties, row.NormalizedPhone, row.PartyId);
            partyNames.TryAdd(row.PartyId, row.DisplayName);
        }

        var emailRows = await (
            from em in _db.PartyEmails.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on em.PartyId equals p.Id
            where p.CompanyId == companyId && em.IsActive && em.NormalizedEmail != null
            select new { em.NormalizedEmail, em.PartyId, p.DisplayName }).ToListAsync(ct);
        foreach (var row in emailRows)
        {
            IndexParty(emailToParties, row.NormalizedEmail, row.PartyId);
            partyNames.TryAdd(row.PartyId, row.DisplayName);
        }

        var report = await SnapshotAsync(companyId, ct);
        var pending = await _db.Leads
            .Where(l => l.CompanyId == companyId && l.PartyId == null)
            .OrderBy(l => l.Id)
            .Select(l => l.Id)
            .ToListAsync(ct);

        batch.TotalRows = pending.Count;
        report.Messages.Add($"Fast backfill pending={pending.Count} companyId={companyId} batchId={batch.Id} indexPhones={phoneToParties.Count} indexEmails={emailToParties.Count}");
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();

        for (var offset = 0; offset < pending.Count; offset += batchSize)
        {
            var slice = pending.Skip(offset).Take(batchSize).ToList();
            var leads = await _db.Leads.Where(l => slice.Contains(l.Id)).ToListAsync(ct);
            // Re-attach batch for counters
            batch = await _db.ImportBatches.FirstAsync(b => b.Id == batch.Id, ct);

            var pendingCreates = new List<(Lead Lead, string Display, PhoneNormalizationResult Phone, string? EmailNorm, bool NeedsReview, string MatchStatus, string MatchReason, int? CandidatePartyId)>();

            foreach (var lead in leads)
            {
                if (lead.PartyId is not null)
                {
                    continue;
                }

                try
                {
                    var primaryPhone = FirstNonEmpty(lead.Mobile, lead.Phone);
                    var phoneNorm = _phones.Normalize(primaryPhone);
                    var emailNorm = _emails.Normalize(lead.Email);
                    var hasPhone = !string.IsNullOrWhiteSpace(phoneNorm.NormalizedPhone) && phoneNorm.NormalizedPhone!.Length >= 8;
                    var hasEmail = !string.IsNullOrWhiteSpace(emailNorm);

                    var phoneIds = hasPhone ? LookupParties(phoneToParties, phoneNorm.NormalizedPhone) : [];
                    var emailIds = hasEmail ? LookupParties(emailToParties, emailNorm) : [];
                    var both = phoneIds.Intersect(emailIds).ToList();

                    int? partyId = null;
                    var linked = false;
                    var needsReview = false;
                    var matchStatus = ImportMatchStatuses.Created;
                    var matchReason = "Created new PartyPerson.";
                    int? candidatePartyId = both.Count > 0 ? both[0]
                        : phoneIds.Count > 0 ? phoneIds[0]
                        : emailIds.Count > 0 ? emailIds[0]
                        : null;

                    if (both.Count == 1)
                    {
                        partyId = both[0];
                        linked = true;
                        matchStatus = ImportMatchStatuses.Linked;
                        matchReason = "Matched by phone and email (memory index).";
                        report.PhoneAndEmailMatches++;
                    }
                    else if (both.Count > 1)
                    {
                        needsReview = true;
                        matchStatus = ImportMatchStatuses.Ambiguous;
                        matchReason = "Multiple parties share this phone+email combination.";
                        report.AmbiguousRecords++;
                    }
                    else if (phoneIds.Count > 0 && emailIds.Count > 0)
                    {
                        needsReview = true;
                        matchStatus = ImportMatchStatuses.Conflict;
                        matchReason = "ConflictingPhoneAndEmail: phone and email resolve to different parties.";
                        report.ConflictingRecords++;
                    }
                    else if (phoneIds.Count == 1 && emailIds.Count == 0)
                    {
                        var onlyPhone = phoneIds[0];
                        if (hasEmail)
                        {
                            needsReview = true;
                            matchStatus = ImportMatchStatuses.Conflict;
                            matchReason = "ConflictingEmail: same phone, different/new email.";
                            report.ConflictingRecords++;
                        }
                        else if (NamesLookDifferent(partyNames.GetValueOrDefault(onlyPhone), lead.FirstName, lead.LastName))
                        {
                            needsReview = true;
                            matchStatus = ImportMatchStatuses.Ambiguous;
                            matchReason = "AmbiguousMatch: same phone but different person name.";
                            report.AmbiguousRecords++;
                        }
                        else
                        {
                            partyId = onlyPhone;
                            linked = true;
                            matchStatus = ImportMatchStatuses.Linked;
                            matchReason = "Matched by phone (memory index).";
                            report.ExactPhoneMatches++;
                        }
                    }
                    else if (emailIds.Count == 1 && phoneIds.Count == 0)
                    {
                        var onlyEmail = emailIds[0];
                        if (hasPhone)
                        {
                            needsReview = true;
                            matchStatus = ImportMatchStatuses.Conflict;
                            matchReason = "ConflictingPhone: same email, different/new phone.";
                            report.ConflictingRecords++;
                        }
                        else if (NamesLookDifferent(partyNames.GetValueOrDefault(onlyEmail), lead.FirstName, lead.LastName))
                        {
                            needsReview = true;
                            matchStatus = ImportMatchStatuses.Ambiguous;
                            matchReason = "AmbiguousMatch: same email but different person name.";
                            report.AmbiguousRecords++;
                        }
                        else
                        {
                            partyId = onlyEmail;
                            linked = true;
                            matchStatus = ImportMatchStatuses.Linked;
                            matchReason = "Matched by email (memory index).";
                            report.ExactEmailMatches++;
                        }
                    }
                    else if (phoneIds.Count > 1)
                    {
                        needsReview = true;
                        matchStatus = ImportMatchStatuses.Ambiguous;
                        matchReason = "Multiple parties share this normalized phone (possible shared/family number).";
                        report.AmbiguousRecords++;
                    }
                    else if (emailIds.Count > 1)
                    {
                        needsReview = true;
                        matchStatus = ImportMatchStatuses.Ambiguous;
                        matchReason = "Multiple parties share this email.";
                        report.AmbiguousRecords++;
                    }

                    if (partyId is int existingPartyId)
                    {
                        lead.PartyId = existingPartyId;
                        lead.UpdatedDate = DateTime.UtcNow;
                        if (linked)
                        {
                            batch.MatchedRows++;
                            report.LinkedExisting++;
                        }

                        _db.ImportSourceRows.Add(new ImportSourceRow
                        {
                            ImportBatchId = batch.Id,
                            SourceRowNumber = lead.Id,
                            ExternalId = $"Lead:{lead.Id}",
                            MatchedPartyId = existingPartyId,
                            MatchStatus = matchStatus,
                            MatchReason = matchReason,
                            ProcessedAt = DateTime.UtcNow,
                            CreatedDate = DateTime.UtcNow,
                            Status = "Active"
                        });
                        continue;
                    }

                    var display = _parties.BuildDisplayName(lead.FirstName, lead.LastName, lead.Email ?? primaryPhone);
                    pendingCreates.Add((lead, display, phoneNorm, emailNorm, needsReview, matchStatus, matchReason, candidatePartyId));
                }
                catch (Exception ex)
                {
                    batch.FailedRows++;
                    report.Failed++;
                    _logger.LogWarning(ex, "Lead party backfill failed for lead {LeadId}", lead.Id);
                    _db.ImportSourceRows.Add(new ImportSourceRow
                    {
                        ImportBatchId = batch.Id,
                        SourceRowNumber = lead.Id,
                        ExternalId = $"Lead:{lead.Id}",
                        MatchStatus = ImportMatchStatuses.Failed,
                        MatchReason = ex.Message,
                        ProcessedAt = DateTime.UtcNow,
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    });
                }
            }

            // Batch-create parties (one SaveChanges for all new Party + PartyPerson rows).
            var createdParties = new List<(Lead Lead, Party Party, PhoneNormalizationResult Phone, string? EmailNorm, bool NeedsReview, string MatchStatus, string MatchReason, int? CandidatePartyId)>();
            foreach (var item in pendingCreates)
            {
                var party = new Party
                {
                    CompanyId = companyId,
                    PartyType = PartyTypes.Person,
                    DisplayName = item.Display,
                    CreatedDate = DateTime.UtcNow,
                    Status = "Active",
                    Person = new PartyPerson
                    {
                        FirstName = string.IsNullOrWhiteSpace(item.Lead.FirstName) ? null : item.Lead.FirstName.Trim(),
                        LastName = string.IsNullOrWhiteSpace(item.Lead.LastName) ? null : item.Lead.LastName.Trim(),
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    }
                };
                _db.Parties.Add(party);
                createdParties.Add((item.Lead, party, item.Phone, item.EmailNorm, item.NeedsReview, item.MatchStatus, item.MatchReason, item.CandidatePartyId));
            }

            if (createdParties.Count > 0)
            {
                await _db.SaveChangesAsync(ct);
            }

            foreach (var item in createdParties)
            {
                var party = item.Party;
                partyNames[party.Id] = party.DisplayName;
                item.Lead.PartyId = party.Id;
                item.Lead.UpdatedDate = DateTime.UtcNow;
                batch.CreatedRows++;
                report.CreatedParties++;

                var hasPhone = !string.IsNullOrWhiteSpace(item.Phone.NormalizedPhone) && item.Phone.NormalizedPhone!.Length >= 8;
                if (hasPhone)
                {
                    _db.PartyPhones.Add(new PartyPhone
                    {
                        PartyId = party.Id,
                        OriginalPhone = item.Phone.Original,
                        NormalizedPhone = item.Phone.NormalizedPhone,
                        E164Phone = item.Phone.E164Phone,
                        PhoneType = "Mobile",
                        IsPrimary = true,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    });
                    IndexParty(phoneToParties, item.Phone.NormalizedPhone, party.Id);
                }

                if (!string.IsNullOrWhiteSpace(item.EmailNorm))
                {
                    _db.PartyEmails.Add(new PartyEmail
                    {
                        PartyId = party.Id,
                        OriginalEmail = item.Lead.Email?.Trim(),
                        NormalizedEmail = item.EmailNorm,
                        EmailType = "Work",
                        IsPrimary = true,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    });
                    IndexParty(emailToParties, item.EmailNorm, party.Id);
                }

                if (item.NeedsReview)
                {
                    _db.ImportMatchReviews.Add(new ImportMatchReview
                    {
                        CompanyId = companyId,
                        ImportBatchId = batch.Id,
                        CandidatePartyId = item.CandidatePartyId,
                        ReviewStatus = "Pending",
                        MatchReason = item.MatchReason,
                        IncomingName = party.DisplayName,
                        IncomingPhone = FirstNonEmpty(item.Lead.Mobile, item.Lead.Phone),
                        IncomingEmail = item.Lead.Email,
                        CandidateName = item.CandidatePartyId is int c ? partyNames.GetValueOrDefault(c) : null,
                        CreatedDate = DateTime.UtcNow,
                        Status = "Active"
                    });
                    batch.ReviewRows++;
                    report.ReviewQueued++;
                }

                _db.ImportSourceRows.Add(new ImportSourceRow
                {
                    ImportBatchId = batch.Id,
                    SourceRowNumber = item.Lead.Id,
                    ExternalId = $"Lead:{item.Lead.Id}",
                    MatchedPartyId = party.Id,
                    MatchStatus = item.MatchStatus,
                    MatchReason = item.MatchReason,
                    ProcessedAt = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    Status = "Active"
                });
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Lead party backfill progress {Done}/{Total}", Math.Min(offset + batchSize, pending.Count), pending.Count);
            _db.ChangeTracker.Clear();
        }

        batch = await _db.ImportBatches.FirstAsync(b => b.Id == batch.Id, ct);
        batch.CompletedAt = DateTime.UtcNow;
        batch.BatchStatus = batch.ReviewRows > 0
            ? ImportBatchStatuses.CompletedWithReview
            : batch.FailedRows > 0 ? ImportBatchStatuses.Failed : ImportBatchStatuses.Completed;
        await _db.SaveChangesAsync(ct);

        var final = await SnapshotAsync(companyId, ct);
        final.CreatedParties = report.CreatedParties;
        final.LinkedExisting = report.LinkedExisting;
        final.ReviewQueued = report.ReviewQueued;
        final.Failed = report.Failed;
        final.ExactPhoneMatches = report.ExactPhoneMatches;
        final.ExactEmailMatches = report.ExactEmailMatches;
        final.PhoneAndEmailMatches = report.PhoneAndEmailMatches;
        final.AmbiguousRecords = report.AmbiguousRecords;
        final.ConflictingRecords = report.ConflictingRecords;
        final.Messages = report.Messages;
        final.Success = final.Failed == 0 && final.LeadsWithoutPartyId == 0;
        final.Messages.Add($"Batch {batch.Id} status={batch.BatchStatus}");
        return final;
    }

    private static void IndexParty(Dictionary<string, HashSet<int>> index, string? key, int partyId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!index.TryGetValue(key, out var set))
        {
            set = [];
            index[key] = set;
        }

        set.Add(partyId);
    }

    private static List<int> LookupParties(Dictionary<string, HashSet<int>> index, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !index.TryGetValue(key, out var set))
        {
            return [];
        }

        return set.ToList();
    }

    private static bool NamesLookDifferent(string? existingDisplay, string? firstName, string? lastName)
    {
        var incoming = NormalizeName($"{firstName} {lastName}");
        var existing = NormalizeName(existingDisplay);
        if (incoming is null || existing is null)
        {
            return false;
        }

        if (existing == incoming || existing.Contains(incoming) || incoming.Contains(existing))
        {
            return false;
        }

        var eParts = existing.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var iParts = incoming.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (eParts.Length > 0 && iParts.Length > 0 && eParts[0] == iParts[0])
        {
            return false;
        }

        return true;
    }

    private static string? NormalizeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = new string(raw.Trim().ToLowerInvariant().Where(c => char.IsLetter(c) || c == ' ').ToArray());
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private async Task<LeadPartyBackfillReport> SnapshotAsync(int companyId, CancellationToken ct)
    {
        var totalLeads = await _db.Leads.CountAsync(l => l.CompanyId == companyId, ct);
        var withParty = await _db.Leads.CountAsync(l => l.CompanyId == companyId && l.PartyId != null, ct);
        var parties = await _db.Parties.CountAsync(p => p.CompanyId == companyId && p.PartyType == PartyTypes.Person, ct);
        var phones = await (
            from ph in _db.PartyPhones
            join p in _db.Parties on ph.PartyId equals p.Id
            where p.CompanyId == companyId
            select ph.Id).CountAsync(ct);
        var emails = await (
            from em in _db.PartyEmails
            join p in _db.Parties on em.PartyId equals p.Id
            where p.CompanyId == companyId
            select em.Id).CountAsync(ct);

        var dupPhones = await (
            from ph in _db.PartyPhones.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on ph.PartyId equals p.Id
            where p.CompanyId == companyId && ph.IsActive && ph.NormalizedPhone != null
            group ph by ph.NormalizedPhone into g
            where g.Select(x => x.PartyId).Distinct().Count() > 1
            select g.Key).CountAsync(ct);

        var dupEmails = await (
            from em in _db.PartyEmails.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on em.PartyId equals p.Id
            where p.CompanyId == companyId && em.IsActive && em.NormalizedEmail != null
            group em by em.NormalizedEmail into g
            where g.Select(x => x.PartyId).Distinct().Count() > 1
            select g.Key).CountAsync(ct);

        return new LeadPartyBackfillReport
        {
            TotalLeads = totalLeads,
            LeadsWithPartyId = withParty,
            LeadsWithoutPartyId = totalLeads - withParty,
            TotalPartyPersons = parties,
            TotalPartyPhones = phones,
            TotalPartyEmails = emails,
            DuplicateNormalizedPhoneGroups = dupPhones,
            DuplicateNormalizedEmailGroups = dupEmails
        };
    }

    private async Task<int> ResolveCompanyIdAsync(int? preferred, CancellationToken ct)
    {
        if (preferred is int id)
        {
            return id;
        }

        if (_tenant.CompanyId is int tenantCompany)
        {
            return tenantCompany;
        }

        return await _db.Companies.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(ct);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
