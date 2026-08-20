using ShaunERP.Core.Platform;

namespace ShaunERP.Core.Identity;

public static class PeopleRoleFilters
{
    public const string All = "All";
    public const string Lead = "Lead";
    public const string Prospect = "Prospect";
    public const string Contact = "Contact";
    public const string Customer = "Customer";
    public const string Opportunity = "Opportunity";
    public const string Student = "Student";
    public const string Subscriber = "Subscriber";
    public const string ActiveSubscriber = "ActiveSubscriber";
    public const string InactiveSubscriber = "InactiveSubscriber";
    public const string StudentActiveSubscriber = "StudentActiveSubscriber";
    public const string StudentInactiveSubscriber = "StudentInactiveSubscriber";
    public const string StudentNeverSubscriber = "StudentNeverSubscriber";
    public const string SubscriberNotStudent = "SubscriberNotStudent";
    public const string ConsultationClient = "ConsultationClient";
    public const string ConsultationProspect = "ConsultationProspect";
    public const string ConsultationContact = "ConsultationContact";
    public const string EventParticipant = "EventParticipant";
    public const string ReportPurchaser = "ReportPurchaser";
    public const string HoroscopePurchaser = "HoroscopePurchaser";
    public const string NumerologyPurchaser = "NumerologyPurchaser";
    public const string ReportPurchaserStudent = "ReportPurchaserStudent";
    public const string ReportPurchaserActiveSubscriber = "ReportPurchaserActiveSubscriber";
    public const string ReportPurchaserInactiveSubscriber = "ReportPurchaserInactiveSubscriber";
    public const string BothReportTypes = "BothReportTypes";
    public const string NoCrmRole = "NoCrmRole";
    public const string Orphan = "Orphan";
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}

public sealed class PeopleListQuery : ListQuery
{
    /// <summary>See <see cref="PeopleRoleFilters"/>.</summary>
    public string? RoleFilter { get; set; }

    public new PeopleListQuery Clone() => new()
    {
        Search = Search,
        Page = NormalizedPage,
        PageSize = NormalizedPageSize,
        RoleFilter = RoleFilter
    };

    public PeopleListQuery WithRoleFilter(string? filter) => new()
    {
        Search = Search,
        Page = 1,
        PageSize = NormalizedPageSize,
        RoleFilter = string.IsNullOrWhiteSpace(filter) ? PeopleRoleFilters.All : filter.Trim()
    };
}

public sealed class PersonListItemDto
{
    public int PartyId { get; init; }
    public string PartyCode => $"P-{PartyId:D6}";
    public string DisplayName { get; init; } = string.Empty;
    public string? PrimaryPhone { get; init; }
    public string? PrimaryEmail { get; init; }
    public string Status { get; init; } = "Active";
    public DateTime CreatedDate { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public bool IsOrphan { get; init; }
}

public sealed class PersonLinkedRecordDto
{
    public string Role { get; init; } = string.Empty;
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Status { get; init; }
    public string Href { get; init; } = string.Empty;
}

public sealed class PersonPhoneDto
{
    public int Id { get; init; }
    public string? OriginalPhone { get; init; }
    public string? NormalizedPhone { get; init; }
    public string? E164Phone { get; init; }
    public string PhoneType { get; init; } = "Mobile";
    public bool IsPrimary { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class PersonEmailDto
{
    public int Id { get; init; }
    public string? OriginalEmail { get; init; }
    public string? NormalizedEmail { get; init; }
    public string EmailType { get; init; } = "Work";
    public bool IsPrimary { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class PersonActivityDto
{
    public string ActivityType { get; init; } = string.Empty;
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? RelatedRole { get; init; }
    public DateTime? OccurredAt { get; init; }
    public string? Href { get; init; }
}

public sealed class PersonCourseEnrollmentDto
{
    public int EnrollmentId { get; init; }
    public int ProgramId { get; init; }
    public string ProgramName { get; init; } = string.Empty;
    public string EnrollmentStatus { get; init; } = string.Empty;
    public string? Source { get; init; }
    public DateTime? StartDate { get; init; }
}

public sealed class PersonSubscriptionDto
{
    public int MembershipId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public string MembershipStatus { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? LegacyDate { get; init; }
    public string? LegacyDateSource { get; init; }
    public string? Source { get; init; }
    public string? LegacyUserId { get; init; }
}

public sealed class PersonConsultationDto
{
    public int EngagementId { get; init; }
    public string? ServiceName { get; init; }
    public string EngagementStatus { get; init; } = string.Empty;
    public DateTime? EnquiryDate { get; init; }
    public DateTime? ScheduledDate { get; init; }
    public DateTime? ConsultationDate { get; init; }
    public DateTime? SourceCreatedAt { get; init; }
    public string? Source { get; init; }
}

public sealed class PersonEventParticipationDto
{
    public int ParticipationId { get; init; }
    public int EventId { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string RegistrationStatus { get; init; } = string.Empty;
    public string? AttendanceStatus { get; init; }
}

public sealed class PersonReportPurchaseDto
{
    public int PurchaseId { get; init; }
    public string ReportProductName { get; init; } = string.Empty;
    public string ReportType { get; init; } = string.Empty;
    public decimal? AmountPaid { get; init; }
    public string? Currency { get; init; }
    public DateTime? PaymentDate { get; init; }
    public string? ReportStatus { get; init; }
    public string? SubjectName { get; init; }
    public string? SourceId { get; init; }
}

public sealed class PersonDetailDto
{
    public int PartyId { get; init; }
    public string PartyCode => $"P-{PartyId:D6}";
    public string DisplayName { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string Status { get; init; } = "Active";
    public DateTime CreatedDate { get; init; }
    public IReadOnlyList<string> DerivedRoles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PersonPhoneDto> Phones { get; init; } = Array.Empty<PersonPhoneDto>();
    public IReadOnlyList<PersonEmailDto> Emails { get; init; } = Array.Empty<PersonEmailDto>();
    public IReadOnlyList<PersonLinkedRecordDto> LinkedRecords { get; init; } = Array.Empty<PersonLinkedRecordDto>();
    public IReadOnlyList<PersonCourseEnrollmentDto> Courses { get; init; } = Array.Empty<PersonCourseEnrollmentDto>();
    public IReadOnlyList<PersonSubscriptionDto> Subscriptions { get; init; } = Array.Empty<PersonSubscriptionDto>();
    public IReadOnlyList<PersonConsultationDto> Consultations { get; init; } = Array.Empty<PersonConsultationDto>();
    public IReadOnlyList<PersonEventParticipationDto> Events { get; init; } = Array.Empty<PersonEventParticipationDto>();
    public IReadOnlyList<PersonReportPurchaseDto> ReportPurchases { get; init; } = Array.Empty<PersonReportPurchaseDto>();
    public IReadOnlyList<PersonActivityDto> Timeline { get; init; } = Array.Empty<PersonActivityDto>();
    public bool IsOrphan { get; init; }
}

public sealed class CreatePersonRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class UpdatePersonIdentityRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? Status { get; set; }
    /// <summary>When true, copy canonical name/phone/email onto linked CRM stage records.</summary>
    public bool SyncLinkedCrmFields { get; set; } = true;
}

public sealed class IdentityReviewItemDto
{
    public int Id { get; init; }
    public string ReviewStatus { get; init; } = "Pending";
    public string? MatchReason { get; init; }
    public string? IncomingName { get; init; }
    public string? IncomingPhone { get; init; }
    public string? IncomingEmail { get; init; }
    public int? CandidatePartyId { get; init; }
    public string? CandidateName { get; init; }
    public string? CandidatePartyCode => CandidatePartyId is int id ? $"P-{id:D6}" : null;
    public string? Resolution { get; init; }
    public DateTime CreatedDate { get; init; }
}

public enum IdentityReviewResolution
{
    SamePerson = 1,
    CreateNewPerson = 2,
    Ignore = 3
}

public sealed class ResolveIdentityReviewRequest
{
    public IdentityReviewResolution Resolution { get; set; }
    public string? Notes { get; set; }
}
