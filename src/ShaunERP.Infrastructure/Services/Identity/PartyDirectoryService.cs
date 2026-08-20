using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services.Identity;

public interface IPartyDirectoryService
{
    Task<PagedResult<PersonListItemDto>> QueryPeopleAsync(PeopleListQuery? query = null, CancellationToken ct = default);
    Task<PersonDetailDto?> GetPersonAsync(int partyId, CancellationToken ct = default);
    Task<int> CreatePersonAsync(CreatePersonRequest request, CancellationToken ct = default);
    Task<PersonDetailDto?> UpdatePersonIdentityAsync(int partyId, UpdatePersonIdentityRequest request, CancellationToken ct = default);
    Task<PagedResult<IdentityReviewItemDto>> QueryIdentityReviewsAsync(ListQuery? query = null, string? status = "Pending", CancellationToken ct = default);
    Task<bool> ResolveIdentityReviewAsync(int reviewId, ResolveIdentityReviewRequest request, CancellationToken ct = default);
    Task<(int People, int PendingReviews)> GetIdentitySummaryAsync(CancellationToken ct = default);
}

public sealed class PartyDirectoryService : IPartyDirectoryService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;
    private readonly IPartyIdentityService _parties;
    private readonly IPhoneNormalizer _phones;
    private readonly IEmailNormalizer _emails;

    public PartyDirectoryService(
        ShaunErpDbContext db,
        ITenantContext tenant,
        IModuleRegistry modules,
        IPartyIdentityService parties,
        IPhoneNormalizer phones,
        IEmailNormalizer emails)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
        _parties = parties;
        _phones = phones;
        _emails = emails;
    }

    public async Task<PagedResult<PersonListItemDto>> QueryPeopleAsync(PeopleListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmPeopleView);
        query ??= new PeopleListQuery();
        var companyId = RequireCompanyId();
        var role = string.IsNullOrWhiteSpace(query.RoleFilter) ? PeopleRoleFilters.All : query.RoleFilter.Trim();

        var parties = _db.Parties.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.PartyType == PartyTypes.Person);

        if (role is PeopleRoleFilters.Active)
        {
            parties = parties.Where(p => p.Status == null || p.Status == "Active");
        }
        else if (role is PeopleRoleFilters.Inactive)
        {
            parties = parties.Where(p => p.Status != null && p.Status != "Active");
        }
        else if (role is PeopleRoleFilters.Lead)
        {
            parties = parties.Where(p => _db.Leads.Any(l => l.CompanyId == companyId && l.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.Prospect)
        {
            parties = parties.Where(p => _db.Prospects.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.Contact)
        {
            parties = parties.Where(p => _db.Contacts.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.Customer)
        {
            parties = parties.Where(p => _db.CrmCustomers.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.Opportunity)
        {
            parties = parties.Where(p => _db.Opportunities.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.Student)
        {
            parties = parties.Where(p => _db.CourseEnrollments.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.Subscriber)
        {
            parties = parties.Where(p => _db.SubscriptionMemberships.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.ActiveSubscriber)
        {
            parties = parties.Where(p => _db.SubscriptionMemberships.Any(x =>
                x.CompanyId == companyId && x.PartyId == p.Id && x.MembershipStatus == MembershipStatuses.Active));
        }
        else if (role is PeopleRoleFilters.InactiveSubscriber)
        {
            parties = parties.Where(p => _db.SubscriptionMemberships.Any(x =>
                x.CompanyId == companyId && x.PartyId == p.Id && x.MembershipStatus != MembershipStatuses.Active));
        }
        else if (role is PeopleRoleFilters.StudentActiveSubscriber)
        {
            parties = parties.Where(p =>
                _db.CourseEnrollments.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                _db.SubscriptionMemberships.Any(x => x.CompanyId == companyId && x.PartyId == p.Id && x.MembershipStatus == MembershipStatuses.Active));
        }
        else if (role is PeopleRoleFilters.StudentInactiveSubscriber)
        {
            parties = parties.Where(p =>
                _db.CourseEnrollments.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                _db.SubscriptionMemberships.Any(x => x.CompanyId == companyId && x.PartyId == p.Id && x.MembershipStatus != MembershipStatuses.Active));
        }
        else if (role is PeopleRoleFilters.StudentNeverSubscriber)
        {
            parties = parties.Where(p =>
                _db.CourseEnrollments.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.SubscriptionMemberships.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.SubscriberNotStudent)
        {
            parties = parties.Where(p =>
                _db.SubscriptionMemberships.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.CourseEnrollments.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.ConsultationClient)
        {
            parties = parties.Where(p => _db.ConsultationEngagements.Any(x =>
                x.CompanyId == companyId && x.PartyId == p.Id &&
                (x.EngagementStatus == ConsultationStatuses.Completed ||
                 x.EngagementStatus == ConsultationStatuses.Scheduled ||
                 x.EngagementStatus == ConsultationStatuses.NoShow)));
        }
        else if (role is PeopleRoleFilters.ConsultationProspect)
        {
            parties = parties.Where(p => _db.ConsultationEngagements.Any(x =>
                x.CompanyId == companyId && x.PartyId == p.Id &&
                (x.EngagementStatus == ConsultationStatuses.Unclassified ||
                 x.EngagementStatus == ConsultationStatuses.Historical ||
                 x.EngagementStatus == ConsultationStatuses.Interested ||
                 x.EngagementStatus == ConsultationStatuses.Contacted ||
                 x.EngagementStatus == ConsultationStatuses.AppointmentProposed ||
                 x.EngagementStatus == ConsultationStatuses.NotInterested ||
                 x.EngagementStatus == ConsultationStatuses.Closed ||
                 x.EngagementStatus == ConsultationStatuses.Cancelled)));
        }
        else if (role is PeopleRoleFilters.ConsultationContact)
        {
            parties = parties.Where(p => _db.ConsultationEngagements.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.EventParticipant)
        {
            parties = parties.Where(p => _db.EventParticipations.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.ReportPurchaser)
        {
            parties = parties.Where(p => _db.ReportPurchases.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.HoroscopePurchaser)
        {
            parties = parties.Where(p => _db.ReportPurchases.Any(x =>
                x.CompanyId == companyId && x.PartyId == p.Id &&
                _db.ReportProducts.Any(rp => rp.Id == x.ReportProductId &&
                    (rp.ReportType == "Horoscope" || rp.Name == ReportProductNames.HoroscopeReport))));
        }
        else if (role is PeopleRoleFilters.NumerologyPurchaser)
        {
            parties = parties.Where(p => _db.ReportPurchases.Any(x =>
                x.CompanyId == companyId && x.PartyId == p.Id &&
                _db.ReportProducts.Any(rp => rp.Id == x.ReportProductId &&
                    (rp.ReportType == "Numerology" || rp.Name == ReportProductNames.NumerologyReport))));
        }
        else if (role is PeopleRoleFilters.ReportPurchaserStudent)
        {
            parties = parties.Where(p =>
                _db.ReportPurchases.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                _db.CourseEnrollments.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.ReportPurchaserActiveSubscriber)
        {
            parties = parties.Where(p =>
                _db.ReportPurchases.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                _db.SubscriptionMemberships.Any(x => x.CompanyId == companyId && x.PartyId == p.Id && x.MembershipStatus == MembershipStatuses.Active));
        }
        else if (role is PeopleRoleFilters.ReportPurchaserInactiveSubscriber)
        {
            parties = parties.Where(p =>
                _db.ReportPurchases.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                _db.SubscriptionMemberships.Any(x => x.CompanyId == companyId && x.PartyId == p.Id && x.MembershipStatus != MembershipStatuses.Active));
        }
        else if (role is PeopleRoleFilters.BothReportTypes)
        {
            parties = parties.Where(p =>
                _db.ReportPurchases.Any(x => x.CompanyId == companyId && x.PartyId == p.Id &&
                    _db.ReportProducts.Any(rp => rp.Id == x.ReportProductId &&
                        (rp.ReportType == "Horoscope" || rp.Name == ReportProductNames.HoroscopeReport))) &&
                _db.ReportPurchases.Any(x => x.CompanyId == companyId && x.PartyId == p.Id &&
                    _db.ReportProducts.Any(rp => rp.Id == x.ReportProductId &&
                        (rp.ReportType == "Numerology" || rp.Name == ReportProductNames.NumerologyReport))));
        }
        else if (role is PeopleRoleFilters.NoCrmRole)
        {
            parties = parties.Where(p =>
                !_db.Leads.Any(l => l.CompanyId == companyId && l.PartyId == p.Id) &&
                !_db.Prospects.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.Contacts.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.CrmCustomers.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.Opportunities.Any(x => x.CompanyId == companyId && x.PartyId == p.Id));
        }
        else if (role is PeopleRoleFilters.Orphan)
        {
            parties = parties.Where(p =>
                !_db.Leads.Any(l => l.CompanyId == companyId && l.PartyId == p.Id) &&
                !_db.Prospects.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.Contacts.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.CrmCustomers.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.Opportunities.Any(x => x.CompanyId == companyId && x.PartyId == p.Id) &&
                !_db.PartyPhones.Any(ph => ph.PartyId == p.Id && ph.IsActive) &&
                !_db.PartyEmails.Any(em => em.PartyId == p.Id && em.IsActive));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (TryParsePartyId(search, out var partyId))
            {
                parties = parties.Where(p => p.Id == partyId);
            }
            else
            {
                var pattern = PagingQueryableExtensions.ToILikePattern(search)!;
                var phoneDigits = new string(search.Where(char.IsDigit).ToArray());
                var phoneNorm = _phones.Normalize(search).NormalizedPhone;

                parties = parties.Where(p =>
                    EF.Functions.ILike(p.DisplayName, pattern) ||
                    (p.Status != null && EF.Functions.ILike(p.Status, pattern)) ||
                    _db.PartyPersons.Any(pp => pp.PartyId == p.Id && (
                        (pp.FirstName != null && EF.Functions.ILike(pp.FirstName, pattern)) ||
                        (pp.LastName != null && EF.Functions.ILike(pp.LastName, pattern)))) ||
                    _db.PartyEmails.Any(em => em.PartyId == p.Id && em.IsActive && (
                        (em.NormalizedEmail != null && EF.Functions.ILike(em.NormalizedEmail, pattern)) ||
                        (em.OriginalEmail != null && EF.Functions.ILike(em.OriginalEmail, pattern)))) ||
                    _db.PartyPhones.Any(ph => ph.PartyId == p.Id && ph.IsActive && (
                        (ph.NormalizedPhone != null && (
                            ph.NormalizedPhone == phoneNorm ||
                            (phoneDigits.Length >= 4 && ph.NormalizedPhone.Contains(phoneDigits)))) ||
                        (ph.OriginalPhone != null && EF.Functions.ILike(ph.OriginalPhone, pattern)) ||
                        (ph.E164Phone != null && EF.Functions.ILike(ph.E164Phone, pattern)))));
            }
        }

        var page = await parties.OrderByDescending(p => p.Id).ToPagedResultAsync(query, ct);
        if (page.TotalCount == 0)
        {
            return PagedResult<PersonListItemDto>.Empty(query);
        }

        var ids = page.Items.Select(p => p.Id).ToList();
        var phones = await _db.PartyPhones.AsNoTracking()
            .Where(ph => ids.Contains(ph.PartyId) && ph.IsActive)
            .OrderByDescending(ph => ph.IsPrimary)
            .ThenBy(ph => ph.Id)
            .ToListAsync(ct);
        var emails = await _db.PartyEmails.AsNoTracking()
            .Where(em => ids.Contains(em.PartyId) && em.IsActive)
            .OrderByDescending(em => em.IsPrimary)
            .ThenBy(em => em.Id)
            .ToListAsync(ct);

        var leadIds = await _db.Leads.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId != null && ids.Contains(l.PartyId.Value))
            .Select(l => l.PartyId!.Value).Distinct().ToListAsync(ct);
        var prospectIds = await _db.Prospects.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId != null && ids.Contains(l.PartyId.Value))
            .Select(l => l.PartyId!.Value).Distinct().ToListAsync(ct);
        var contactIds = await _db.Contacts.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId != null && ids.Contains(l.PartyId.Value))
            .Select(l => l.PartyId!.Value).Distinct().ToListAsync(ct);
        var customerIds = await _db.CrmCustomers.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId != null && ids.Contains(l.PartyId.Value))
            .Select(l => l.PartyId!.Value).Distinct().ToListAsync(ct);
        var opportunityIds = await _db.Opportunities.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId != null && ids.Contains(l.PartyId.Value))
            .Select(l => l.PartyId!.Value).Distinct().ToListAsync(ct);
        var studentIds = await _db.CourseEnrollments.AsNoTracking()
            .Where(l => l.CompanyId == companyId && ids.Contains(l.PartyId))
            .Select(l => l.PartyId).Distinct().ToListAsync(ct);
        var subscriberIds = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(l => l.CompanyId == companyId && ids.Contains(l.PartyId))
            .Select(l => l.PartyId).Distinct().ToListAsync(ct);
        var consultationIds = await _db.ConsultationEngagements.AsNoTracking()
            .Where(l => l.CompanyId == companyId && ids.Contains(l.PartyId))
            .Select(l => new { l.PartyId, l.EngagementStatus }).ToListAsync(ct);
        var consultationClientIds = consultationIds
            .Where(x => ConsultationStatuses.IsClient(x.EngagementStatus))
            .Select(x => x.PartyId).Distinct().ToHashSet();
        var consultationProspectIds = consultationIds
            .Where(x => ConsultationStatuses.IsProspect(x.EngagementStatus))
            .Select(x => x.PartyId).Distinct().ToHashSet();
        var eventParticipantIds = await _db.EventParticipations.AsNoTracking()
            .Where(l => l.CompanyId == companyId && ids.Contains(l.PartyId))
            .Select(l => l.PartyId).Distinct().ToListAsync(ct);
        var reportPurchaserIds = await _db.ReportPurchases.AsNoTracking()
            .Where(l => l.CompanyId == companyId && ids.Contains(l.PartyId))
            .Select(l => l.PartyId).Distinct().ToListAsync(ct);

        var items = page.Items.Select(p =>
        {
            var roles = new List<string>();
            if (leadIds.Contains(p.Id)) roles.Add("Lead");
            if (prospectIds.Contains(p.Id)) roles.Add("Prospect");
            if (contactIds.Contains(p.Id)) roles.Add("Contact");
            if (customerIds.Contains(p.Id)) roles.Add("Customer");
            if (opportunityIds.Contains(p.Id)) roles.Add("Opportunity");
            if (studentIds.Contains(p.Id)) roles.Add("Student");
            if (subscriberIds.Contains(p.Id)) roles.Add("Subscriber");
            if (consultationClientIds.Contains(p.Id)) roles.Add("Consultation Client");
            else if (consultationProspectIds.Contains(p.Id)) roles.Add("Consultation Prospect");
            if (eventParticipantIds.Contains(p.Id)) roles.Add("Event Participant");
            if (reportPurchaserIds.Contains(p.Id)) roles.Add("Report Purchaser");
            var hasPhone = phones.Any(ph => ph.PartyId == p.Id);
            var hasEmail = emails.Any(em => em.PartyId == p.Id);
            var isOrphan = roles.Count == 0 && !hasPhone && !hasEmail;
            return new PersonListItemDto
            {
                PartyId = p.Id,
                DisplayName = p.DisplayName,
                PrimaryPhone = phones.FirstOrDefault(ph => ph.PartyId == p.Id)?.NormalizedPhone
                               ?? phones.FirstOrDefault(ph => ph.PartyId == p.Id)?.OriginalPhone,
                PrimaryEmail = emails.FirstOrDefault(em => em.PartyId == p.Id)?.NormalizedEmail
                               ?? emails.FirstOrDefault(em => em.PartyId == p.Id)?.OriginalEmail,
                Status = p.Status ?? "Active",
                CreatedDate = p.CreatedDate,
                Roles = roles,
                IsOrphan = isOrphan
            };
        }).ToList();

        return new PagedResult<PersonListItemDto>
        {
            Items = items,
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<PersonDetailDto?> GetPersonAsync(int partyId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmPeopleView);
        var companyId = RequireCompanyId();
        var party = await _db.Parties.AsNoTracking()
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.Id == partyId && p.CompanyId == companyId && p.PartyType == PartyTypes.Person, ct);
        if (party is null)
        {
            return null;
        }

        var phoneRows = await _db.PartyPhones.AsNoTracking()
            .Where(ph => ph.PartyId == partyId)
            .OrderByDescending(ph => ph.IsPrimary)
            .ThenBy(ph => ph.Id)
            .ToListAsync(ct);
        var emailRows = await _db.PartyEmails.AsNoTracking()
            .Where(em => em.PartyId == partyId)
            .OrderByDescending(em => em.IsPrimary)
            .ThenBy(em => em.Id)
            .ToListAsync(ct);

        var linked = new List<PersonLinkedRecordDto>();
        linked.AddRange(await _db.Leads.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId == partyId)
            .OrderByDescending(l => l.Id)
            .Select(l => new PersonLinkedRecordDto
            {
                Role = "Lead",
                Id = l.Id,
                Label = ((l.FirstName ?? "") + " " + (l.LastName ?? "")).Trim(),
                Status = l.LeadStatus,
                Href = "/crm/leads/" + l.Id
            }).ToListAsync(ct));
        linked.AddRange(await _db.Prospects.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId == partyId)
            .OrderByDescending(l => l.Id)
            .Select(l => new PersonLinkedRecordDto
            {
                Role = "Prospect",
                Id = l.Id,
                Label = ((l.FirstName ?? "") + " " + (l.LastName ?? "")).Trim(),
                Status = l.Stage,
                Href = "/crm/prospects/" + l.Id
            }).ToListAsync(ct));
        linked.AddRange(await _db.Contacts.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId == partyId)
            .OrderByDescending(l => l.Id)
            .Select(l => new PersonLinkedRecordDto
            {
                Role = "Contact",
                Id = l.Id,
                Label = ((l.FirstName ?? "") + " " + (l.LastName ?? "")).Trim(),
                Status = l.IsPrimary ? "Primary" : null,
                Href = "/crm/contacts/" + l.Id
            }).ToListAsync(ct));
        linked.AddRange(await _db.Opportunities.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId == partyId)
            .OrderByDescending(l => l.Id)
            .Select(l => new PersonLinkedRecordDto
            {
                Role = "Opportunity",
                Id = l.Id,
                Label = l.DealName,
                Status = l.Stage,
                Href = "/crm/opportunities/" + l.Id
            }).ToListAsync(ct));
        linked.AddRange(await _db.CrmCustomers.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.PartyId == partyId)
            .OrderByDescending(l => l.Id)
            .Select(l => new PersonLinkedRecordDto
            {
                Role = "Customer",
                Id = l.Id,
                Label = l.CompanyName ?? "",
                Status = l.Status,
                Href = "/crm/customers/" + l.Id
            }).ToListAsync(ct));

        var courses = await (
            from e in _db.CourseEnrollments.AsNoTracking()
            join prog in _db.TrainingPrograms.AsNoTracking() on e.ProgramId equals prog.Id
            where e.CompanyId == companyId && e.PartyId == partyId
            orderby prog.Name
            select new PersonCourseEnrollmentDto
            {
                EnrollmentId = e.Id,
                ProgramId = prog.Id,
                ProgramName = prog.Name,
                EnrollmentStatus = e.EnrollmentStatus,
                Source = e.Source,
                StartDate = e.StartDate
            }).ToListAsync(ct);

        var subscriptions = await (
            from m in _db.SubscriptionMemberships.AsNoTracking()
            join plan in _db.SubscriptionPlans.AsNoTracking() on m.PlanId equals plan.Id
            where m.CompanyId == companyId && m.PartyId == partyId
            orderby m.StartDate descending, m.Id descending
            select new PersonSubscriptionDto
            {
                MembershipId = m.Id,
                PlanName = plan.Name,
                MembershipStatus = m.MembershipStatus,
                StartDate = m.StartDate,
                EndDate = m.EndDate,
                LegacyDate = m.LegacyDate,
                LegacyDateSource = m.LegacyDateSource,
                Source = m.Source,
                LegacyUserId = m.ExternalReference != null && m.ExternalReference.StartsWith("exactpredictions:")
                    ? m.ExternalReference.Substring("exactpredictions:".Length)
                    : null
            }).ToListAsync(ct);

        var consultations = await (
            from e in _db.ConsultationEngagements.AsNoTracking()
            join s in _db.ConsultationServices.AsNoTracking() on e.ConsultationServiceId equals s.Id into sj
            from s in sj.DefaultIfEmpty()
            where e.CompanyId == companyId && e.PartyId == partyId
            orderby e.ConsultationDate descending, e.SourceCreatedAt descending, e.Id descending
            select new PersonConsultationDto
            {
                EngagementId = e.Id,
                ServiceName = s != null ? s.Name : null,
                EngagementStatus = e.EngagementStatus,
                EnquiryDate = e.EnquiryDate,
                ScheduledDate = e.ScheduledDate,
                ConsultationDate = e.ConsultationDate,
                SourceCreatedAt = e.SourceCreatedAt,
                Source = e.Source
            }).ToListAsync(ct);

        var events = await (
            from ep in _db.EventParticipations.AsNoTracking()
            join ev in _db.CrmEvents.AsNoTracking() on ep.EventId equals ev.Id
            where ep.CompanyId == companyId && ep.PartyId == partyId
            orderby ev.StartDate descending, ev.Name
            select new PersonEventParticipationDto
            {
                ParticipationId = ep.Id,
                EventId = ev.Id,
                EventName = ev.Name,
                RegistrationStatus = ep.RegistrationStatus,
                AttendanceStatus = ep.AttendanceStatus
            }).ToListAsync(ct);

        var reportPurchases = await (
            from rp in _db.ReportPurchases.AsNoTracking()
            join prod in _db.ReportProducts.AsNoTracking() on rp.ReportProductId equals prod.Id
            where rp.CompanyId == companyId && rp.PartyId == partyId
            orderby rp.PaymentDate descending, rp.Id descending
            select new PersonReportPurchaseDto
            {
                PurchaseId = rp.Id,
                ReportProductName = prod.Name,
                ReportType = prod.ReportType,
                AmountPaid = rp.AmountPaid,
                Currency = rp.Currency,
                PaymentDate = rp.PaymentDate,
                ReportStatus = rp.ReportStatus,
                SubjectName = rp.SubjectName,
                SourceId = rp.SourceId
            }).ToListAsync(ct);

        var derivedRoles = new List<string>();
        if (linked.Any(r => r.Role == "Lead")) derivedRoles.Add("Lead");
        if (linked.Any(r => r.Role == "Prospect")) derivedRoles.Add("Prospect");
        if (linked.Any(r => r.Role == "Contact")) derivedRoles.Add("Contact");
        if (linked.Any(r => r.Role == "Customer")) derivedRoles.Add("Customer");
        if (linked.Any(r => r.Role == "Opportunity")) derivedRoles.Add("Opportunity");
        if (courses.Count > 0) derivedRoles.Add("Student");
        if (subscriptions.Count > 0) derivedRoles.Add("Subscriber");
        if (consultations.Any(c => ConsultationStatuses.IsClient(c.EngagementStatus)))
            derivedRoles.Add("Consultation Client");
        else if (consultations.Count > 0)
            derivedRoles.Add("Consultation Prospect");
        if (events.Count > 0) derivedRoles.Add("Event Participant");
        if (reportPurchases.Count > 0) derivedRoles.Add("Report Purchaser");

        var timeline = await BuildTimelineAsync(companyId, linked, ct);
        var hasChannel = phoneRows.Any(p => p.IsActive) || emailRows.Any(e => e.IsActive);
        return new PersonDetailDto
        {
            PartyId = party.Id,
            DisplayName = party.DisplayName,
            FirstName = party.Person?.FirstName,
            LastName = party.Person?.LastName,
            Status = party.Status ?? "Active",
            CreatedDate = party.CreatedDate,
            DerivedRoles = derivedRoles,
            Phones = phoneRows.Select(ph => new PersonPhoneDto
            {
                Id = ph.Id,
                OriginalPhone = ph.OriginalPhone,
                NormalizedPhone = ph.NormalizedPhone,
                E164Phone = ph.E164Phone,
                PhoneType = ph.PhoneType,
                IsPrimary = ph.IsPrimary,
                IsActive = ph.IsActive
            }).ToList(),
            Emails = emailRows.Select(em => new PersonEmailDto
            {
                Id = em.Id,
                OriginalEmail = em.OriginalEmail,
                NormalizedEmail = em.NormalizedEmail,
                EmailType = em.EmailType,
                IsPrimary = em.IsPrimary,
                IsActive = em.IsActive
            }).ToList(),
            LinkedRecords = linked,
            Courses = courses,
            Subscriptions = subscriptions,
            Consultations = consultations,
            Events = events,
            ReportPurchases = reportPurchases,
            Timeline = timeline,
            IsOrphan = linked.Count == 0 && courses.Count == 0 && subscriptions.Count == 0
                       && consultations.Count == 0 && events.Count == 0 && !hasChannel
        };
    }

    public async Task<int> CreatePersonAsync(CreatePersonRequest request, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmPeopleCreate);
        var companyId = RequireCompanyId();
        var result = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
        {
            CompanyId = companyId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Mobile = request.Phone,
            Email = request.Email,
            CreateReviewOnAmbiguous = true
        }, ct);
        return result.PartyId ?? throw new InvalidOperationException("Could not create person identity.");
    }

    public async Task<PersonDetailDto?> UpdatePersonIdentityAsync(int partyId, UpdatePersonIdentityRequest request, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmPeopleUpdate);
        var companyId = RequireCompanyId();
        var party = await _db.Parties
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.Id == partyId && p.CompanyId == companyId && p.PartyType == PartyTypes.Person, ct);
        if (party is null)
        {
            return null;
        }

        party.Person ??= new PartyPerson { PartyId = party.Id, CreatedDate = DateTime.UtcNow, Status = "Active" };
        party.Person.FirstName = NullIfEmpty(request.FirstName);
        party.Person.LastName = NullIfEmpty(request.LastName);
        party.DisplayName = _parties.BuildDisplayName(request.FirstName, request.LastName, party.DisplayName);
        party.Status = string.IsNullOrWhiteSpace(request.Status) ? party.Status ?? "Active" : request.Status.Trim();
        party.UpdatedDate = DateTime.UtcNow;
        party.Person.UpdatedDate = DateTime.UtcNow;

        await UpsertPrimaryPhoneAsync(party.Id, request.PrimaryPhone, ct);
        await UpsertPrimaryEmailAsync(party.Id, request.PrimaryEmail, ct);
        await _db.SaveChangesAsync(ct);

        if (request.SyncLinkedCrmFields)
        {
            await SyncLinkedCrmFieldsAsync(companyId, party.Id, party.DisplayName, request.FirstName, request.LastName, request.PrimaryPhone, request.PrimaryEmail, ct);
        }

        return await GetPersonAsync(partyId, ct);
    }

    public async Task<PagedResult<IdentityReviewItemDto>> QueryIdentityReviewsAsync(
        ListQuery? query = null,
        string? status = "Pending",
        CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmIdentityReviewView);
        query ??= new ListQuery();
        var companyId = RequireCompanyId();
        var q = _db.ImportMatchReviews.AsNoTracking().Where(r => r.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            q = q.Where(r => r.ReviewStatus == status);
        }

        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(r =>
                (r.IncomingName != null && EF.Functions.ILike(r.IncomingName, pattern)) ||
                (r.IncomingPhone != null && EF.Functions.ILike(r.IncomingPhone, pattern)) ||
                (r.IncomingEmail != null && EF.Functions.ILike(r.IncomingEmail, pattern)) ||
                (r.CandidateName != null && EF.Functions.ILike(r.CandidateName, pattern)) ||
                (r.MatchReason != null && EF.Functions.ILike(r.MatchReason, pattern)));
        }

        var page = await q.OrderByDescending(r => r.Id).ToPagedResultAsync(query, ct);
        return new PagedResult<IdentityReviewItemDto>
        {
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize,
            Items = page.Items.Select(r => new IdentityReviewItemDto
            {
                Id = r.Id,
                ReviewStatus = r.ReviewStatus,
                MatchReason = r.MatchReason,
                IncomingName = r.IncomingName,
                IncomingPhone = r.IncomingPhone,
                IncomingEmail = r.IncomingEmail,
                CandidatePartyId = r.CandidatePartyId,
                CandidateName = r.CandidateName,
                Resolution = r.Resolution,
                CreatedDate = r.CreatedDate
            }).ToList()
        };
    }

    public async Task<bool> ResolveIdentityReviewAsync(int reviewId, ResolveIdentityReviewRequest request, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmIdentityReviewResolve);
        var companyId = RequireCompanyId();
        var row = await _db.ImportMatchReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.CompanyId == companyId, ct);
        if (row is null)
        {
            return false;
        }

        row.ReviewStatus = "Resolved";
        row.Resolution = request.Resolution switch
        {
            IdentityReviewResolution.SamePerson => "SamePerson",
            IdentityReviewResolution.CreateNewPerson => "CreateNewPerson",
            IdentityReviewResolution.Ignore => "Ignore",
            _ => "Resolved"
        };
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            row.MatchReason = $"{row.MatchReason} | Notes: {request.Notes.Trim()}";
        }

        row.ResolvedBy = _tenant.UserId?.ToString() ?? "user";
        row.ResolvedAt = DateTime.UtcNow;
        row.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(int People, int PendingReviews)> GetIdentitySummaryAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmPeopleView);
        var companyId = RequireCompanyId();
        var people = await _db.Parties.AsNoTracking()
            .CountAsync(p => p.CompanyId == companyId && p.PartyType == PartyTypes.Person, ct);
        var pending = await _db.ImportMatchReviews.AsNoTracking()
            .CountAsync(r => r.CompanyId == companyId && r.ReviewStatus == "Pending", ct);
        return (people, pending);
    }

    private async Task UpsertPrimaryPhoneAsync(int partyId, string? raw, CancellationToken ct)
    {
        var n = _phones.Normalize(raw);
        if (string.IsNullOrWhiteSpace(n.NormalizedPhone))
        {
            return;
        }

        var primary = await _db.PartyPhones
            .Where(p => p.PartyId == partyId)
            .OrderByDescending(p => p.IsPrimary)
            .FirstOrDefaultAsync(ct);
        if (primary is null)
        {
            _db.PartyPhones.Add(new PartyPhone
            {
                PartyId = partyId,
                OriginalPhone = n.Original,
                NormalizedPhone = n.NormalizedPhone,
                E164Phone = n.E164Phone,
                PhoneType = "Mobile",
                IsPrimary = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                Status = "Active"
            });
            return;
        }

        primary.OriginalPhone = n.Original;
        primary.NormalizedPhone = n.NormalizedPhone;
        primary.E164Phone = n.E164Phone;
        primary.IsPrimary = true;
        primary.IsActive = true;
        primary.UpdatedDate = DateTime.UtcNow;
    }

    private async Task UpsertPrimaryEmailAsync(int partyId, string? raw, CancellationToken ct)
    {
        var normalized = _emails.Normalize(raw);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var primary = await _db.PartyEmails
            .Where(e => e.PartyId == partyId)
            .OrderByDescending(e => e.IsPrimary)
            .FirstOrDefaultAsync(ct);
        if (primary is null)
        {
            _db.PartyEmails.Add(new PartyEmail
            {
                PartyId = partyId,
                OriginalEmail = raw?.Trim(),
                NormalizedEmail = normalized,
                EmailType = "Work",
                IsPrimary = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                Status = "Active"
            });
            return;
        }

        primary.OriginalEmail = raw?.Trim();
        primary.NormalizedEmail = normalized;
        primary.IsPrimary = true;
        primary.IsActive = true;
        primary.UpdatedDate = DateTime.UtcNow;
    }

    private async Task SyncLinkedCrmFieldsAsync(
        int companyId,
        int partyId,
        string displayName,
        string? firstName,
        string? lastName,
        string? phone,
        string? email,
        CancellationToken ct)
    {
        var phoneValue = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        var emailValue = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        foreach (var lead in await _db.Leads.Where(l => l.CompanyId == companyId && l.PartyId == partyId).ToListAsync(ct))
        {
            if (!string.IsNullOrWhiteSpace(firstName)) lead.FirstName = firstName.Trim();
            if (!string.IsNullOrWhiteSpace(lastName)) lead.LastName = lastName.Trim();
            if (phoneValue is not null) { lead.Mobile = phoneValue; lead.Phone ??= phoneValue; }
            if (emailValue is not null) lead.Email = emailValue;
            lead.UpdatedDate = DateTime.UtcNow;
        }

        foreach (var row in await _db.Prospects.Where(l => l.CompanyId == companyId && l.PartyId == partyId).ToListAsync(ct))
        {
            if (!string.IsNullOrWhiteSpace(firstName)) row.FirstName = firstName.Trim();
            if (!string.IsNullOrWhiteSpace(lastName)) row.LastName = lastName.Trim();
            if (phoneValue is not null) row.PhoneNumber = phoneValue;
            if (emailValue is not null) row.Email = emailValue;
            row.UpdatedDate = DateTime.UtcNow;
        }

        foreach (var row in await _db.Contacts.Where(l => l.CompanyId == companyId && l.PartyId == partyId).ToListAsync(ct))
        {
            if (!string.IsNullOrWhiteSpace(firstName)) row.FirstName = firstName.Trim();
            if (!string.IsNullOrWhiteSpace(lastName)) row.LastName = lastName.Trim();
            if (phoneValue is not null) { row.Mobile = phoneValue; row.Phone ??= phoneValue; }
            if (emailValue is not null) row.Email = emailValue;
            row.UpdatedDate = DateTime.UtcNow;
        }

        foreach (var row in await _db.CrmCustomers.Where(l => l.CompanyId == companyId && l.PartyId == partyId).ToListAsync(ct))
        {
            if (!string.IsNullOrWhiteSpace(displayName)) row.PrimaryContact = displayName;
            if (phoneValue is not null) row.PhoneNumber = phoneValue;
            if (emailValue is not null) row.Email = emailValue;
            row.UpdatedDate = DateTime.UtcNow;
        }

        foreach (var row in await _db.Opportunities.Where(l => l.CompanyId == companyId && l.PartyId == partyId).ToListAsync(ct))
        {
            if (!string.IsNullOrWhiteSpace(displayName)) row.ContactName = displayName;
            row.UpdatedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<PersonActivityDto>> BuildTimelineAsync(
        int companyId,
        IReadOnlyList<PersonLinkedRecordDto> linked,
        CancellationToken ct)
    {
        var items = new List<PersonActivityDto>();
        foreach (var group in linked.GroupBy(l => l.Role))
        {
            var ids = group.Select(g => g.Id).ToList();
            if (ids.Count == 0) continue;
            var relatedType = group.Key;

            items.AddRange(await _db.CrmNotes.AsNoTracking()
                .Where(n => n.CompanyId == companyId && n.RelatedEntityType == relatedType && ids.Contains(n.RelatedEntityId))
                .OrderByDescending(n => n.CreatedDate)
                .Take(20)
                .Select(n => new PersonActivityDto
                {
                    ActivityType = "Note",
                    Id = n.Id,
                    Title = n.Title ?? n.Body ?? "Note",
                    RelatedRole = relatedType,
                    OccurredAt = n.CreatedDate,
                    Href = "/crm/notes/" + n.Id
                }).ToListAsync(ct));

            items.AddRange(await _db.CrmCalls.AsNoTracking()
                .Where(n => n.CompanyId == companyId && n.RelatedEntityType == relatedType && n.RelatedEntityId != null && ids.Contains(n.RelatedEntityId.Value))
                .OrderByDescending(n => n.CallStart ?? n.CreatedDate)
                .Take(20)
                .Select(n => new PersonActivityDto
                {
                    ActivityType = "Call",
                    Id = n.Id,
                    Title = n.Subject ?? "Call",
                    RelatedRole = relatedType,
                    OccurredAt = n.CallStart ?? n.CreatedDate,
                    Href = "/crm/calls/" + n.Id
                }).ToListAsync(ct));

            items.AddRange(await _db.CrmTasks.AsNoTracking()
                .Where(n => n.CompanyId == companyId && n.RelatedEntityType == relatedType && n.RelatedEntityId != null && ids.Contains(n.RelatedEntityId.Value))
                .OrderByDescending(n => n.DueDate ?? n.CreatedDate)
                .Take(20)
                .Select(n => new PersonActivityDto
                {
                    ActivityType = "Task",
                    Id = n.Id,
                    Title = n.Subject ?? "Task",
                    RelatedRole = relatedType,
                    OccurredAt = n.DueDate ?? n.CreatedDate,
                    Href = "/crm/tasks/" + n.Id
                }).ToListAsync(ct));

            items.AddRange(await _db.Meetings.AsNoTracking()
                .Where(n => n.CompanyId == companyId && n.RelatedEntityType == relatedType && n.RelatedEntityId != null && ids.Contains(n.RelatedEntityId.Value))
                .OrderByDescending(n => n.From ?? n.CreatedDate)
                .Take(20)
                .Select(n => new PersonActivityDto
                {
                    ActivityType = "Meeting",
                    Id = n.Id,
                    Title = n.Title ?? "Meeting",
                    RelatedRole = relatedType,
                    OccurredAt = n.From ?? n.CreatedDate,
                    Href = "/crm/meetings/" + n.Id
                }).ToListAsync(ct));
        }

        return items
            .OrderByDescending(i => i.OccurredAt ?? DateTime.MinValue)
            .Take(40)
            .ToList();
    }

    private void EnsureAccess(string permission)
    {
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Crm) || !_modules.HasPermission(_tenant, permission))
        {
            throw new UnauthorizedAccessException("CRM access denied.");
        }
    }

    private int RequireCompanyId()
        => _tenant.CompanyId ?? throw new InvalidOperationException("Tenant context is required.");

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static bool TryParsePartyId(string search, out int partyId)
    {
        partyId = 0;
        var s = search.Trim();
        if (s.StartsWith("P-", StringComparison.OrdinalIgnoreCase))
        {
            s = s[2..];
        }

        return int.TryParse(s, out partyId) && partyId > 0;
    }
}
