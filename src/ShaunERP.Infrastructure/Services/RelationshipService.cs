using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services;

public sealed class EnrollmentListQuery : ListQuery
{
    public int? ProgramId { get; set; }
    public string? EnrollmentStatus { get; set; }
}

public sealed class ProgramListItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "Active";
    public int StudentCount { get; init; }
}

public sealed class EnrollmentListItemDto
{
    public int Id { get; init; }
    public int PartyId { get; init; }
    public string PartyCode => $"P-{PartyId:D6}";
    public string DisplayName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public int ProgramId { get; init; }
    public string ProgramName { get; init; } = string.Empty;
    public string EnrollmentStatus { get; init; } = EnrollmentStatuses.Active;
    public string? Source { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime CreatedDate { get; init; }
}

public sealed class SubscriptionPlanDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int DurationMonths { get; init; }
    public string Status { get; init; } = "Active";
    public int MemberCount { get; init; }
}

public sealed class MembershipListItemDto
{
    public int Id { get; init; }
    public int PartyId { get; init; }
    public string PartyCode => $"P-{PartyId:D6}";
    public string DisplayName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public int PlanId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public string MembershipStatus { get; init; } = MembershipStatuses.Active;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? LegacyDate { get; init; }
    public string? LegacyDateSource { get; init; }
    public string? Source { get; init; }
    public string? ExternalReference { get; init; }
    public string? LegacyUserId =>
        ExternalReference is not null && ExternalReference.StartsWith("exactpredictions:", StringComparison.Ordinal)
            ? ExternalReference["exactpredictions:".Length..]
            : null;
}

public sealed class MembershipListQuery : ListQuery
{
    public int? PlanId { get; set; }
    public string? MembershipStatus { get; set; }
    public int? ProgramId { get; set; }
    public bool? IsStudent { get; set; }
}

public sealed class SubscriptionSummaryDto
{
    public int TotalMembers { get; init; }
    public int ActiveMembers { get; init; }
    public int InactiveMembers { get; init; }
}

public sealed class ConsultationServiceDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "Active";
    public int EngagementCount { get; init; }
}

public sealed class ConsultationListQuery : ListQuery
{
    public string? EngagementStatus { get; set; }
    public int? ServiceId { get; set; }
    public bool? IsStudent { get; set; }
    public string? SubscriberFilter { get; set; }
    public bool? IsReportPurchaser { get; set; }
}

public sealed class ConsultationListItemDto
{
    public int Id { get; init; }
    public int PartyId { get; init; }
    public string PartyCode => $"P-{PartyId:D6}";
    public string DisplayName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? ServiceName { get; init; }
    public string EngagementStatus { get; init; } = ConsultationStatuses.Unclassified;
    public DateTime? EnquiryDate { get; init; }
    public DateTime? ScheduledDate { get; init; }
    public DateTime? ConsultationDate { get; init; }
    public DateTime? SourceCreatedAt { get; init; }
    public string? Source { get; init; }
    public string? ExternalReference { get; init; }
    public int? ConsultantUserId { get; init; }
}

public sealed class ConsultationSummaryDto
{
    public int Total { get; init; }
    public int Unclassified { get; init; }
    public int Interested { get; init; }
    public int Scheduled { get; init; }
    public int Completed { get; init; }
    public int NeedsFollowUp { get; init; }
}

public sealed class EventListItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public string Status { get; init; } = "Active";
    public int ParticipantCount { get; init; }
}

public sealed class ParticipantListItemDto
{
    public int Id { get; init; }
    public int PartyId { get; init; }
    public string PartyCode => $"P-{PartyId:D6}";
    public string DisplayName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string RegistrationStatus { get; init; } = EventRegistrationStatuses.Registered;
    public string? AttendanceStatus { get; init; }
}

public sealed class ReportPurchaseListQuery : ListQuery
{
    public string? ReportType { get; set; }
    public string? ReportStatus { get; set; }
    public string? DeliveryStatus { get; set; }
    public DateTime? PaymentFrom { get; set; }
    public DateTime? PaymentTo { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public bool? IsStudent { get; set; }
    public string? SubscriberFilter { get; set; }
}

public sealed class ReportPurchaseListItemDto
{
    public int Id { get; init; }
    public int PartyId { get; init; }
    public string PartyCode => $"P-{PartyId:D6}";
    public string DisplayName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string ReportProductName { get; init; } = string.Empty;
    public string ReportType { get; init; } = string.Empty;
    public decimal? AmountPaid { get; set; }
    public string? Currency { get; init; }
    public DateTime? PaymentDate { get; init; }
    public string? ReportStatus { get; init; }
    public string? DeliveryEmailStatus { get; init; }
    public string? DeliveryWhatsappStatus { get; init; }
    public string? SourceFile { get; init; }
    public string? SourceId { get; init; }
    public string? ExternalReference { get; init; }
}

public sealed class ReportPurchaseSummaryDto
{
    public int TotalPurchases { get; init; }
    public int HoroscopePurchases { get; init; }
    public int NumerologyPurchases { get; init; }
    public int UniquePurchasers { get; init; }
}

public interface IRelationshipService
{
    Task EnsureAccessAsync(string permission, CancellationToken ct = default);

    Task<PagedResult<ProgramListItemDto>> QueryProgramsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<TrainingProgram?> GetProgramAsync(int id, CancellationToken ct = default);
    Task<TrainingProgram> UpsertProgramAsync(TrainingProgram program, CancellationToken ct = default);
    Task<PagedResult<EnrollmentListItemDto>> QueryEnrollmentsAsync(EnrollmentListQuery? query = null, CancellationToken ct = default);

    Task<IReadOnlyList<SubscriptionPlanDto>> ListPlansAsync(CancellationToken ct = default);
    Task<PagedResult<MembershipListItemDto>> QueryMembershipsAsync(MembershipListQuery? query = null, CancellationToken ct = default);
    Task<SubscriptionSummaryDto> GetSubscriptionSummaryAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ConsultationServiceDto>> ListConsultationServicesAsync(CancellationToken ct = default);
    Task<PagedResult<ConsultationListItemDto>> QueryConsultationsAsync(ConsultationListQuery? query = null, CancellationToken ct = default);
    Task<ConsultationSummaryDto> GetConsultationSummaryAsync(CancellationToken ct = default);
    Task<ConsultationService> UpsertConsultationServiceAsync(ConsultationService service, CancellationToken ct = default);
    Task UpdateConsultationStatusAsync(int engagementId, string engagementStatus, CancellationToken ct = default);

    Task<PagedResult<EventListItemDto>> QueryEventsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<CrmEvent?> GetEventAsync(int id, CancellationToken ct = default);
    Task<CrmEvent> UpsertEventAsync(CrmEvent crmEvent, CancellationToken ct = default);
    Task<PagedResult<ParticipantListItemDto>> QueryParticipantsAsync(ListQuery? query = null, int? eventId = null, CancellationToken ct = default);

    Task<PagedResult<ReportPurchaseListItemDto>> QueryReportPurchasesAsync(ReportPurchaseListQuery? query = null, CancellationToken ct = default);
    Task<ReportPurchaseSummaryDto> GetReportPurchaseSummaryAsync(CancellationToken ct = default);
}

public sealed class RelationshipService : IRelationshipService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;

    public RelationshipService(ShaunErpDbContext db, ITenantContext tenant, IModuleRegistry modules)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
    }

    public Task EnsureAccessAsync(string permission, CancellationToken ct = default)
    {
        EnsureAccess(permission);
        return Task.CompletedTask;
    }

    public async Task<PagedResult<ProgramListItemDto>> QueryProgramsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmCoursesView);
        query ??= new ListQuery();
        var companyId = RequireCompanyId();
        var q = _db.TrainingPrograms.AsNoTracking().Where(p => p.CompanyId == companyId);
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(p => EF.Functions.ILike(p.Name, pattern) || (p.Description != null && EF.Functions.ILike(p.Description, pattern)));
        }

        var page = await q.OrderBy(p => p.Name).ToPagedResultAsync(query, ct);
        var ids = page.Items.Select(p => p.Id).ToList();
        var counts = await _db.CourseEnrollments.AsNoTracking()
            .Where(e => e.CompanyId == companyId && ids.Contains(e.ProgramId))
            .GroupBy(e => e.ProgramId)
            .Select(g => new { ProgramId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var map = counts.ToDictionary(x => x.ProgramId, x => x.Count);

        return new PagedResult<ProgramListItemDto>
        {
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize,
            Items = page.Items.Select(p => new ProgramListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                Status = p.Status ?? "Active",
                StudentCount = map.GetValueOrDefault(p.Id)
            }).ToList()
        };
    }

    public async Task<TrainingProgram?> GetProgramAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmCoursesView);
        var companyId = RequireCompanyId();
        return await _db.TrainingPrograms.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, ct);
    }

    public async Task<TrainingProgram> UpsertProgramAsync(TrainingProgram program, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmCoursesManage);
        var companyId = RequireCompanyId();
        if (program.Id == 0)
        {
            program.CompanyId = companyId;
            program.CreatedDate = DateTime.UtcNow;
            program.Status ??= "Active";
            _db.TrainingPrograms.Add(program);
        }
        else
        {
            var existing = await _db.TrainingPrograms.FirstOrDefaultAsync(p => p.Id == program.Id && p.CompanyId == companyId, ct)
                ?? throw new InvalidOperationException("Program not found.");
            existing.Name = program.Name.Trim();
            existing.Description = program.Description;
            existing.Status = program.Status ?? existing.Status;
            existing.UpdatedDate = DateTime.UtcNow;
            program = existing;
        }

        await _db.SaveChangesAsync(ct);
        return program;
    }

    public async Task<PagedResult<EnrollmentListItemDto>> QueryEnrollmentsAsync(EnrollmentListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmEnrollmentsView);
        query ??= new EnrollmentListQuery();
        var companyId = RequireCompanyId();

        var q =
            from e in _db.CourseEnrollments.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on e.PartyId equals p.Id
            join prog in _db.TrainingPrograms.AsNoTracking() on e.ProgramId equals prog.Id
            where e.CompanyId == companyId
            select new { e, p, prog };

        if (query.ProgramId is int programId)
        {
            q = q.Where(x => x.e.ProgramId == programId);
        }

        if (!string.IsNullOrWhiteSpace(query.EnrollmentStatus) && query.EnrollmentStatus != "All")
        {
            q = q.Where(x => x.e.EnrollmentStatus == query.EnrollmentStatus);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (TryParsePartyId(search, out var partyId))
            {
                q = q.Where(x => x.e.PartyId == partyId);
            }
            else
            {
                var pattern = PagingQueryableExtensions.ToILikePattern(search)!;
                var digits = new string(search.Where(char.IsDigit).ToArray());
                q = q.Where(x =>
                    EF.Functions.ILike(x.p.DisplayName, pattern) ||
                    EF.Functions.ILike(x.prog.Name, pattern) ||
                    _db.PartyEmails.Any(em => em.PartyId == x.p.Id && em.IsActive && em.NormalizedEmail != null && EF.Functions.ILike(em.NormalizedEmail, pattern)) ||
                    _db.PartyPhones.Any(ph => ph.PartyId == x.p.Id && ph.IsActive && ph.NormalizedPhone != null &&
                        (EF.Functions.ILike(ph.NormalizedPhone, pattern) || (digits.Length >= 4 && ph.NormalizedPhone.Contains(digits)))));
            }
        }

        var ordered = q.OrderByDescending(x => x.e.Id);
        var total = await ordered.CountAsync(ct);
        var page = query.NormalizedPage;
        var pageSize = query.NormalizedPageSize;
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var partyIds = rows.Select(r => r.p.Id).Distinct().ToList();
        var phones = await PrimaryPhonesAsync(partyIds, ct);
        var emails = await PrimaryEmailsAsync(partyIds, ct);

        return new PagedResult<EnrollmentListItemDto>
        {
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => new EnrollmentListItemDto
            {
                Id = r.e.Id,
                PartyId = r.p.Id,
                DisplayName = r.p.DisplayName,
                Phone = phones.GetValueOrDefault(r.p.Id),
                Email = emails.GetValueOrDefault(r.p.Id),
                ProgramId = r.prog.Id,
                ProgramName = r.prog.Name,
                EnrollmentStatus = r.e.EnrollmentStatus,
                Source = r.e.Source,
                StartDate = r.e.StartDate,
                CreatedDate = r.e.CreatedDate
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> ListPlansAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmSubscriptionsView);
        var companyId = RequireCompanyId();
        var plans = await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.DurationMonths)
            .ToListAsync(ct);
        var counts = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId)
            .GroupBy(m => m.PlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var map = counts.ToDictionary(x => x.PlanId, x => x.Count);
        return plans.Select(p => new SubscriptionPlanDto
        {
            Id = p.Id,
            Name = p.Name,
            DurationMonths = p.DurationMonths,
            Status = p.Status ?? "Active",
            MemberCount = map.GetValueOrDefault(p.Id)
        }).ToList();
    }

    public async Task<PagedResult<MembershipListItemDto>> QueryMembershipsAsync(
        MembershipListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmSubscriptionsView);
        query ??= new MembershipListQuery();
        var companyId = RequireCompanyId();
        var q =
            from m in _db.SubscriptionMemberships.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on m.PartyId equals p.Id
            join plan in _db.SubscriptionPlans.AsNoTracking() on m.PlanId equals plan.Id
            where m.CompanyId == companyId
            select new { m, p, plan };

        if (query.PlanId is int pid) q = q.Where(x => x.m.PlanId == pid);

        var status = query.MembershipStatus?.Trim();
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("Former", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => x.m.MembershipStatus == MembershipStatuses.Expired
                              || x.m.MembershipStatus == MembershipStatuses.Cancelled
                              || x.m.MembershipStatus == MembershipStatuses.Historical
                              || x.m.MembershipStatus == MembershipStatuses.Suspended);
            }
            else
            {
                q = q.Where(x => x.m.MembershipStatus == status);
            }
        }

        if (query.ProgramId is int programId)
        {
            q = q.Where(x => _db.CourseEnrollments.Any(e =>
                e.CompanyId == companyId && e.PartyId == x.m.PartyId && e.ProgramId == programId));
        }

        if (query.IsStudent is true)
        {
            q = q.Where(x => _db.CourseEnrollments.Any(e => e.CompanyId == companyId && e.PartyId == x.m.PartyId));
        }
        else if (query.IsStudent is false)
        {
            q = q.Where(x => !_db.CourseEnrollments.Any(e => e.CompanyId == companyId && e.PartyId == x.m.PartyId));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (TryParsePartyId(search, out var partyId))
            {
                q = q.Where(x => x.m.PartyId == partyId);
            }
            else
            {
                var pattern = PagingQueryableExtensions.ToILikePattern(search)!;
                var digits = new string(search.Where(char.IsDigit).ToArray());
                var legacyKey = search.StartsWith("exactpredictions:", StringComparison.OrdinalIgnoreCase)
                    ? search
                    : "exactpredictions:" + search;
                q = q.Where(x =>
                    EF.Functions.ILike(x.p.DisplayName, pattern) ||
                    EF.Functions.ILike(x.plan.Name, pattern) ||
                    (x.m.ExternalReference != null && (
                        x.m.ExternalReference == legacyKey ||
                        EF.Functions.ILike(x.m.ExternalReference, pattern))) ||
                    _db.PartyEmails.Any(em => em.PartyId == x.p.Id && em.IsActive && em.NormalizedEmail != null && EF.Functions.ILike(em.NormalizedEmail, pattern)) ||
                    _db.PartyPhones.Any(ph => ph.PartyId == x.p.Id && ph.IsActive && ph.NormalizedPhone != null &&
                        (EF.Functions.ILike(ph.NormalizedPhone, pattern) || (digits.Length >= 4 && ph.NormalizedPhone.Contains(digits)))));
            }
        }

        var ordered = q.OrderByDescending(x => x.m.Id);
        var total = await ordered.CountAsync(ct);
        var rows = await ordered.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
        var partyIds = rows.Select(r => r.p.Id).Distinct().ToList();
        var phones = await PrimaryPhonesAsync(partyIds, ct);
        var emails = await PrimaryEmailsAsync(partyIds, ct);

        return new PagedResult<MembershipListItemDto>
        {
            TotalCount = total,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            Items = rows.Select(r => new MembershipListItemDto
            {
                Id = r.m.Id,
                PartyId = r.p.Id,
                DisplayName = r.p.DisplayName,
                Phone = phones.GetValueOrDefault(r.p.Id),
                Email = emails.GetValueOrDefault(r.p.Id),
                PlanId = r.plan.Id,
                PlanName = r.plan.Name,
                MembershipStatus = r.m.MembershipStatus,
                StartDate = r.m.StartDate,
                EndDate = r.m.EndDate,
                LegacyDate = r.m.LegacyDate,
                LegacyDateSource = r.m.LegacyDateSource,
                Source = r.m.Source,
                ExternalReference = r.m.ExternalReference
            }).ToList()
        };
    }

    public async Task<SubscriptionSummaryDto> GetSubscriptionSummaryAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmSubscriptionsView);
        var companyId = RequireCompanyId();
        var rows = await _db.SubscriptionMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(x => x.MembershipStatus == MembershipStatuses.Active),
                Inactive = g.Count(x => x.MembershipStatus != MembershipStatuses.Active)
            })
            .FirstOrDefaultAsync(ct);

        return new SubscriptionSummaryDto
        {
            TotalMembers = rows?.Total ?? 0,
            ActiveMembers = rows?.Active ?? 0,
            InactiveMembers = rows?.Inactive ?? 0
        };
    }

    public async Task<IReadOnlyList<ConsultationServiceDto>> ListConsultationServicesAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmConsultationsView);
        var companyId = RequireCompanyId();
        var services = await _db.ConsultationServices.AsNoTracking()
            .Where(s => s.CompanyId == companyId).OrderBy(s => s.Name).ToListAsync(ct);
        var counts = await _db.ConsultationEngagements.AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.ConsultationServiceId != null)
            .GroupBy(e => e.ConsultationServiceId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var map = counts.ToDictionary(x => x.Id, x => x.Count);
        return services.Select(s => new ConsultationServiceDto
        {
            Id = s.Id,
            Name = s.Name,
            Status = s.Status ?? "Active",
            EngagementCount = map.GetValueOrDefault(s.Id)
        }).ToList();
    }

    public async Task<PagedResult<ConsultationListItemDto>> QueryConsultationsAsync(
        ConsultationListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmConsultationsView);
        query ??= new ConsultationListQuery();
        var companyId = RequireCompanyId();
        var q =
            from e in _db.ConsultationEngagements.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on e.PartyId equals p.Id
            join s in _db.ConsultationServices.AsNoTracking() on e.ConsultationServiceId equals s.Id into sj
            from s in sj.DefaultIfEmpty()
            where e.CompanyId == companyId
            select new { e, p, s };

        if (query.ServiceId is int sid) q = q.Where(x => x.e.ConsultationServiceId == sid);

        var status = query.EngagementStatus?.Trim();
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (status.Equals("NeedsFollowUp", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => x.e.EngagementStatus == ConsultationStatuses.Interested
                              || x.e.EngagementStatus == ConsultationStatuses.Contacted
                              || x.e.EngagementStatus == ConsultationStatuses.AppointmentProposed);
            }
            else if (status.Equals("Historical", StringComparison.OrdinalIgnoreCase) ||
                     status.Equals("Unclassified", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => x.e.EngagementStatus == ConsultationStatuses.Unclassified
                              || x.e.EngagementStatus == ConsultationStatuses.Historical);
            }
            else
            {
                q = q.Where(x => x.e.EngagementStatus == status);
            }
        }

        if (query.IsStudent is true)
            q = q.Where(x => _db.CourseEnrollments.Any(e => e.CompanyId == companyId && e.PartyId == x.e.PartyId));
        else if (query.IsStudent is false)
            q = q.Where(x => !_db.CourseEnrollments.Any(e => e.CompanyId == companyId && e.PartyId == x.e.PartyId));

        var subFilter = query.SubscriberFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(subFilter) && !subFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (subFilter.Equals("Active", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => _db.SubscriptionMemberships.Any(m => m.CompanyId == companyId && m.PartyId == x.e.PartyId && m.MembershipStatus == MembershipStatuses.Active));
            else if (subFilter.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => _db.SubscriptionMemberships.Any(m => m.CompanyId == companyId && m.PartyId == x.e.PartyId && m.MembershipStatus != MembershipStatuses.Active));
            else if (subFilter.Equals("None", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => !_db.SubscriptionMemberships.Any(m => m.CompanyId == companyId && m.PartyId == x.e.PartyId));
        }

        if (query.IsReportPurchaser is true)
            q = q.Where(x => _db.ReportPurchases.Any(r => r.CompanyId == companyId && r.PartyId == x.e.PartyId));
        else if (query.IsReportPurchaser is false)
            q = q.Where(x => !_db.ReportPurchases.Any(r => r.CompanyId == companyId && r.PartyId == x.e.PartyId));

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (TryParsePartyId(search, out var partyId))
            {
                q = q.Where(x => x.e.PartyId == partyId);
            }
            else
            {
                var pattern = PagingQueryableExtensions.ToILikePattern(search)!;
                var digits = new string(search.Where(char.IsDigit).ToArray());
                q = q.Where(x =>
                    EF.Functions.ILike(x.p.DisplayName, pattern) ||
                    (x.s != null && EF.Functions.ILike(x.s.Name, pattern)) ||
                    (x.e.ExternalReference != null && EF.Functions.ILike(x.e.ExternalReference, pattern)) ||
                    (x.e.Source != null && EF.Functions.ILike(x.e.Source, pattern)) ||
                    _db.PartyEmails.Any(em => em.PartyId == x.p.Id && em.IsActive && em.NormalizedEmail != null && EF.Functions.ILike(em.NormalizedEmail, pattern)) ||
                    _db.PartyPhones.Any(ph => ph.PartyId == x.p.Id && ph.IsActive && ph.NormalizedPhone != null &&
                        (EF.Functions.ILike(ph.NormalizedPhone, pattern) || (digits.Length >= 4 && ph.NormalizedPhone.Contains(digits)))));
            }
        }

        var ordered = q.OrderByDescending(x => x.e.SourceCreatedAt).ThenByDescending(x => x.e.Id);
        var total = await ordered.CountAsync(ct);
        var rows = await ordered.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
        var partyIds = rows.Select(r => r.p.Id).Distinct().ToList();
        var phones = await PrimaryPhonesAsync(partyIds, ct);
        var emails = await PrimaryEmailsAsync(partyIds, ct);

        return new PagedResult<ConsultationListItemDto>
        {
            TotalCount = total,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            Items = rows.Select(r => new ConsultationListItemDto
            {
                Id = r.e.Id,
                PartyId = r.p.Id,
                DisplayName = r.p.DisplayName,
                Phone = phones.GetValueOrDefault(r.p.Id),
                Email = emails.GetValueOrDefault(r.p.Id),
                ServiceName = r.s?.Name,
                EngagementStatus = r.e.EngagementStatus,
                EnquiryDate = r.e.EnquiryDate,
                ScheduledDate = r.e.ScheduledDate,
                ConsultationDate = r.e.ConsultationDate,
                SourceCreatedAt = r.e.SourceCreatedAt,
                Source = r.e.Source,
                ExternalReference = r.e.ExternalReference,
                ConsultantUserId = r.e.ConsultantUserId
            }).ToList()
        };
    }

    public async Task<ConsultationSummaryDto> GetConsultationSummaryAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmConsultationsView);
        var companyId = RequireCompanyId();
        var rows = await _db.ConsultationEngagements.AsNoTracking()
            .Where(e => e.CompanyId == companyId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Unclassified = g.Count(x => x.EngagementStatus == ConsultationStatuses.Unclassified || x.EngagementStatus == ConsultationStatuses.Historical),
                Interested = g.Count(x => x.EngagementStatus == ConsultationStatuses.Interested),
                Scheduled = g.Count(x => x.EngagementStatus == ConsultationStatuses.Scheduled),
                Completed = g.Count(x => x.EngagementStatus == ConsultationStatuses.Completed),
                NeedsFollowUp = g.Count(x =>
                    x.EngagementStatus == ConsultationStatuses.Interested ||
                    x.EngagementStatus == ConsultationStatuses.Contacted ||
                    x.EngagementStatus == ConsultationStatuses.AppointmentProposed)
            })
            .FirstOrDefaultAsync(ct);

        return new ConsultationSummaryDto
        {
            Total = rows?.Total ?? 0,
            Unclassified = rows?.Unclassified ?? 0,
            Interested = rows?.Interested ?? 0,
            Scheduled = rows?.Scheduled ?? 0,
            Completed = rows?.Completed ?? 0,
            NeedsFollowUp = rows?.NeedsFollowUp ?? 0
        };
    }

    public async Task<ConsultationService> UpsertConsultationServiceAsync(ConsultationService service, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmConsultationsManage);
        var companyId = RequireCompanyId();
        if (service.Id == 0)
        {
            service.CompanyId = companyId;
            service.CreatedDate = DateTime.UtcNow;
            service.Status ??= "Active";
            _db.ConsultationServices.Add(service);
        }
        else
        {
            var existing = await _db.ConsultationServices.FirstAsync(s => s.Id == service.Id && s.CompanyId == companyId, ct);
            existing.Name = service.Name.Trim();
            existing.Description = service.Description;
            existing.Status = service.Status ?? existing.Status;
            existing.UpdatedDate = DateTime.UtcNow;
            service = existing;
        }

        await _db.SaveChangesAsync(ct);
        return service;
    }

    public async Task UpdateConsultationStatusAsync(int engagementId, string engagementStatus, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmConsultationsManage);
        var companyId = RequireCompanyId();
        var status = engagementStatus?.Trim() ?? string.Empty;
        if (!ConsultationStatuses.All.Contains(status))
            throw new InvalidOperationException($"Invalid consultation status: {engagementStatus}");

        var row = await _db.ConsultationEngagements
            .FirstOrDefaultAsync(e => e.Id == engagementId && e.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Consultation engagement not found.");

        var before = row.EngagementStatus;
        if (string.Equals(before, status, StringComparison.Ordinal))
            return;

        row.EngagementStatus = status;
        row.UpdatedDate = DateTime.UtcNow;
        row.ModifiedBy = _tenant.UserId?.ToString();
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "consultation.status.update",
            CompanyId = companyId,
            UserId = _tenant.UserId,
            EntityType = nameof(ConsultationEngagement),
            EntityId = row.Id.ToString(),
            BeforeValues = before,
            AfterValues = status,
            OccurredAt = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<EventListItemDto>> QueryEventsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmEventsView);
        query ??= new ListQuery();
        var companyId = RequireCompanyId();
        var q = _db.CrmEvents.AsNoTracking().Where(e => e.CompanyId == companyId);
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(e => EF.Functions.ILike(e.Name, pattern));
        }

        var page = await q.OrderByDescending(e => e.Id).ToPagedResultAsync(query, ct);
        var ids = page.Items.Select(e => e.Id).ToList();
        var counts = await _db.EventParticipations.AsNoTracking()
            .Where(p => p.CompanyId == companyId && ids.Contains(p.EventId))
            .GroupBy(p => p.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var map = counts.ToDictionary(x => x.EventId, x => x.Count);
        return new PagedResult<EventListItemDto>
        {
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize,
            Items = page.Items.Select(e => new EventListItemDto
            {
                Id = e.Id,
                Name = e.Name,
                StartDate = e.StartDate,
                Status = e.Status ?? "Active",
                ParticipantCount = map.GetValueOrDefault(e.Id)
            }).ToList()
        };
    }

    public async Task<CrmEvent?> GetEventAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmEventsView);
        var companyId = RequireCompanyId();
        return await _db.CrmEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, ct);
    }

    public async Task<CrmEvent> UpsertEventAsync(CrmEvent crmEvent, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmEventsManage);
        var companyId = RequireCompanyId();
        if (crmEvent.Id == 0)
        {
            crmEvent.CompanyId = companyId;
            crmEvent.CreatedDate = DateTime.UtcNow;
            crmEvent.Status ??= "Active";
            _db.CrmEvents.Add(crmEvent);
        }
        else
        {
            var existing = await _db.CrmEvents.FirstAsync(e => e.Id == crmEvent.Id && e.CompanyId == companyId, ct);
            existing.Name = crmEvent.Name.Trim();
            existing.StartDate = crmEvent.StartDate;
            existing.EndDate = crmEvent.EndDate;
            existing.Location = crmEvent.Location;
            existing.Description = crmEvent.Description;
            existing.Status = crmEvent.Status ?? existing.Status;
            existing.UpdatedDate = DateTime.UtcNow;
            crmEvent = existing;
        }

        await _db.SaveChangesAsync(ct);
        return crmEvent;
    }

    public async Task<PagedResult<ParticipantListItemDto>> QueryParticipantsAsync(
        ListQuery? query = null, int? eventId = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmEventsView);
        query ??= new ListQuery();
        var companyId = RequireCompanyId();
        var q =
            from ep in _db.EventParticipations.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on ep.PartyId equals p.Id
            where ep.CompanyId == companyId
            select new { ep, p };
        if (eventId is int eid) q = q.Where(x => x.ep.EventId == eid);
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x => EF.Functions.ILike(x.p.DisplayName, pattern));
        }

        var ordered = q.OrderByDescending(x => x.ep.Id);
        var total = await ordered.CountAsync(ct);
        var rows = await ordered.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
        var partyIds = rows.Select(r => r.p.Id).Distinct().ToList();
        var phones = await PrimaryPhonesAsync(partyIds, ct);
        var emails = await PrimaryEmailsAsync(partyIds, ct);
        return new PagedResult<ParticipantListItemDto>
        {
            TotalCount = total,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            Items = rows.Select(r => new ParticipantListItemDto
            {
                Id = r.ep.Id,
                PartyId = r.p.Id,
                DisplayName = r.p.DisplayName,
                Phone = phones.GetValueOrDefault(r.p.Id),
                Email = emails.GetValueOrDefault(r.p.Id),
                RegistrationStatus = r.ep.RegistrationStatus,
                AttendanceStatus = r.ep.AttendanceStatus
            }).ToList()
        };
    }

    public async Task<PagedResult<ReportPurchaseListItemDto>> QueryReportPurchasesAsync(
        ReportPurchaseListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmReportsView);
        query ??= new ReportPurchaseListQuery();
        var companyId = RequireCompanyId();
        var q =
            from rp in _db.ReportPurchases.AsNoTracking()
            join p in _db.Parties.AsNoTracking() on rp.PartyId equals p.Id
            join prod in _db.ReportProducts.AsNoTracking() on rp.ReportProductId equals prod.Id
            where rp.CompanyId == companyId
            select new { rp, p, prod };

        var reportType = query.ReportType?.Trim();
        if (!string.IsNullOrWhiteSpace(reportType) && !reportType.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (reportType.Contains("Horoscope", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.prod.ReportType == "Horoscope" || x.prod.Name == ReportProductNames.HoroscopeReport);
            else if (reportType.Contains("Numerology", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.prod.ReportType == "Numerology" || x.prod.Name == ReportProductNames.NumerologyReport);
            else
                q = q.Where(x => x.prod.ReportType == reportType || x.prod.Name == reportType);
        }

        if (!string.IsNullOrWhiteSpace(query.ReportStatus) && !query.ReportStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
            q = q.Where(x => x.rp.ReportStatus == query.ReportStatus);

        if (!string.IsNullOrWhiteSpace(query.DeliveryStatus) && !query.DeliveryStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var ds = query.DeliveryStatus.Trim();
            q = q.Where(x => x.rp.DeliveryEmailStatus == ds || x.rp.DeliveryWhatsappStatus == ds);
        }

        if (query.PaymentFrom is DateTime from)
            q = q.Where(x => x.rp.PaymentDate >= from);
        if (query.PaymentTo is DateTime to)
            q = q.Where(x => x.rp.PaymentDate <= to);
        if (query.AmountMin is decimal amin)
            q = q.Where(x => x.rp.AmountPaid >= amin);
        if (query.AmountMax is decimal amax)
            q = q.Where(x => x.rp.AmountPaid <= amax);

        if (query.IsStudent is true)
            q = q.Where(x => _db.CourseEnrollments.Any(e => e.CompanyId == companyId && e.PartyId == x.rp.PartyId));
        else if (query.IsStudent is false)
            q = q.Where(x => !_db.CourseEnrollments.Any(e => e.CompanyId == companyId && e.PartyId == x.rp.PartyId));

        var subFilter = query.SubscriberFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(subFilter) && !subFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (subFilter.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => _db.SubscriptionMemberships.Any(m =>
                    m.CompanyId == companyId && m.PartyId == x.rp.PartyId && m.MembershipStatus == MembershipStatuses.Active));
            }
            else if (subFilter.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => _db.SubscriptionMemberships.Any(m =>
                    m.CompanyId == companyId && m.PartyId == x.rp.PartyId && m.MembershipStatus != MembershipStatuses.Active));
            }
            else if (subFilter.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => _db.SubscriptionMemberships.Any(m => m.CompanyId == companyId && m.PartyId == x.rp.PartyId));
            }
            else if (subFilter.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => !_db.SubscriptionMemberships.Any(m => m.CompanyId == companyId && m.PartyId == x.rp.PartyId));
            }
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (TryParsePartyId(search, out var partyId))
            {
                q = q.Where(x => x.rp.PartyId == partyId);
            }
            else
            {
                var pattern = PagingQueryableExtensions.ToILikePattern(search)!;
                var digits = new string(search.Where(char.IsDigit).ToArray());
                q = q.Where(x =>
                    EF.Functions.ILike(x.p.DisplayName, pattern) ||
                    (x.rp.SourceId != null && EF.Functions.ILike(x.rp.SourceId, pattern)) ||
                    (x.rp.ExternalReference != null && EF.Functions.ILike(x.rp.ExternalReference, pattern)) ||
                    (x.rp.SourceFile != null && EF.Functions.ILike(x.rp.SourceFile, pattern)) ||
                    _db.PartyEmails.Any(em => em.PartyId == x.p.Id && em.IsActive && em.NormalizedEmail != null && EF.Functions.ILike(em.NormalizedEmail, pattern)) ||
                    _db.PartyPhones.Any(ph => ph.PartyId == x.p.Id && ph.IsActive && ph.NormalizedPhone != null &&
                        (EF.Functions.ILike(ph.NormalizedPhone, pattern) || (digits.Length >= 4 && ph.NormalizedPhone.Contains(digits)))));
            }
        }

        var ordered = q.OrderByDescending(x => x.rp.PaymentDate).ThenByDescending(x => x.rp.Id);
        var total = await ordered.CountAsync(ct);
        var rows = await ordered.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
        var partyIds = rows.Select(r => r.p.Id).Distinct().ToList();
        var phones = await PrimaryPhonesAsync(partyIds, ct);
        var emails = await PrimaryEmailsAsync(partyIds, ct);

        return new PagedResult<ReportPurchaseListItemDto>
        {
            TotalCount = total,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            Items = rows.Select(r => new ReportPurchaseListItemDto
            {
                Id = r.rp.Id,
                PartyId = r.p.Id,
                DisplayName = r.p.DisplayName,
                Phone = phones.GetValueOrDefault(r.p.Id),
                Email = emails.GetValueOrDefault(r.p.Id),
                ReportProductName = r.prod.Name,
                ReportType = r.prod.ReportType,
                AmountPaid = r.rp.AmountPaid,
                Currency = r.rp.Currency,
                PaymentDate = r.rp.PaymentDate,
                ReportStatus = r.rp.ReportStatus,
                DeliveryEmailStatus = r.rp.DeliveryEmailStatus,
                DeliveryWhatsappStatus = r.rp.DeliveryWhatsappStatus,
                SourceFile = r.rp.SourceFile,
                SourceId = r.rp.SourceId,
                ExternalReference = r.rp.ExternalReference
            }).ToList()
        };
    }

    public async Task<ReportPurchaseSummaryDto> GetReportPurchaseSummaryAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CrmReportsView);
        var companyId = RequireCompanyId();
        var rows = await (
            from rp in _db.ReportPurchases.AsNoTracking()
            join prod in _db.ReportProducts.AsNoTracking() on rp.ReportProductId equals prod.Id
            where rp.CompanyId == companyId
            select new { rp.PartyId, prod.ReportType, prod.Name }).ToListAsync(ct);

        return new ReportPurchaseSummaryDto
        {
            TotalPurchases = rows.Count,
            HoroscopePurchases = rows.Count(x => x.ReportType == "Horoscope" || x.Name == ReportProductNames.HoroscopeReport),
            NumerologyPurchases = rows.Count(x => x.ReportType == "Numerology" || x.Name == ReportProductNames.NumerologyReport),
            UniquePurchasers = rows.Select(x => x.PartyId).Distinct().Count()
        };
    }

    private async Task<Dictionary<int, string?>> PrimaryPhonesAsync(IReadOnlyList<int> partyIds, CancellationToken ct)
    {
        var rows = await _db.PartyPhones.AsNoTracking()
            .Where(ph => partyIds.Contains(ph.PartyId) && ph.IsActive)
            .OrderByDescending(ph => ph.IsPrimary).ThenBy(ph => ph.Id)
            .ToListAsync(ct);
        return rows.GroupBy(r => r.PartyId)
            .ToDictionary(g => g.Key, g => g.First().NormalizedPhone ?? g.First().OriginalPhone);
    }

    private async Task<Dictionary<int, string?>> PrimaryEmailsAsync(IReadOnlyList<int> partyIds, CancellationToken ct)
    {
        var rows = await _db.PartyEmails.AsNoTracking()
            .Where(em => partyIds.Contains(em.PartyId) && em.IsActive)
            .OrderByDescending(em => em.IsPrimary).ThenBy(em => em.Id)
            .ToListAsync(ct);
        return rows.GroupBy(r => r.PartyId)
            .ToDictionary(g => g.Key, g => g.First().NormalizedEmail ?? g.First().OriginalEmail);
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

    private static bool TryParsePartyId(string search, out int partyId)
    {
        partyId = 0;
        var s = search.Trim();
        if (s.StartsWith("P-", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return int.TryParse(s, out partyId) && partyId > 0;
    }
}
