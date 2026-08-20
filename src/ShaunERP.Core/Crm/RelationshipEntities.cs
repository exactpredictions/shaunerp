using ShaunERP.Core.Identity;

namespace ShaunERP.Core.Crm;

/// <summary>Course / training program catalog. Not a person master.</summary>
public class TrainingProgram : BaseEntity
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
}

/// <summary>PartyPerson enrolled in a TrainingProgram. Derived "Student" role.</summary>
public class CourseEnrollment : BaseEntity
{
    public int CompanyId { get; set; }
    public int PartyId { get; set; }
    public Party? Party { get; set; }
    public int ProgramId { get; set; }
    public TrainingProgram? Program { get; set; }
    public string EnrollmentStatus { get; set; } = EnrollmentStatuses.Active;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Source { get; set; }
    public string? SourceReference { get; set; }
    public int? ImportBatchId { get; set; }
    public string? Notes { get; set; }
    /// <summary>Optional future commercial link — not required for historical imports.</summary>
    public int? SalesOrderId { get; set; }
    public int? InvoiceId { get; set; }
}

public static class EnrollmentStatuses
{
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Historical = "Historical";
}

public class SubscriptionPlan : BaseEntity
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationMonths { get; set; }
    public string? Description { get; set; }
    public string? ProductSku { get; set; }
    public ICollection<SubscriptionMembership> Memberships { get; set; } = new List<SubscriptionMembership>();
}

public class SubscriptionMembership : BaseEntity
{
    public int CompanyId { get; set; }
    public int PartyId { get; set; }
    public Party? Party { get; set; }
    public int PlanId { get; set; }
    public SubscriptionPlan? Plan { get; set; }
    /// <summary>Proven activation/start only — never invent from registration dates.</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>Proven deactivation/end only — never invent from registration dates.</summary>
    public DateTime? EndDate { get; set; }
    public string MembershipStatus { get; set; } = MembershipStatuses.Active;
    public string? Source { get; set; }
    /// <summary>Stable legacy key, e.g. exactpredictions:{user_id}.</summary>
    public string? ExternalReference { get; set; }
    public int? ImportBatchId { get; set; }
    public string? Notes { get; set; }
    /// <summary>Raw legacy date from ExactPredictions workbook (may be registration or audit).</summary>
    public DateTime? LegacyDate { get; set; }
    /// <summary>Exact workbook date_source text — preserves provenance.</summary>
    public string? LegacyDateSource { get; set; }
    public int? SalesOrderId { get; set; }
    public int? InvoiceId { get; set; }
}

public static class MembershipStatuses
{
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    public const string Suspended = "Suspended";
    public const string Historical = "Historical";
}

public static class SubscriptionPlanNames
{
    public const string ExactPredictionsPremiumHistorical = "ExactPredictions Premium — Historical/Unknown Plan";
}

public class ConsultationService : BaseEntity
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<ConsultationEngagement> Engagements { get; set; } = new List<ConsultationEngagement>();
}

public class ConsultationEngagement : BaseEntity
{
    public int CompanyId { get; set; }
    public int PartyId { get; set; }
    public Party? Party { get; set; }
    /// <summary>Nullable — historical imports may not know the service type.</summary>
    public int? ConsultationServiceId { get; set; }
    public ConsultationService? ConsultationService { get; set; }
    public DateTime? EnquiryDate { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ConsultationDate { get; set; }
    /// <summary>Source record timestamp when present; not assumed to be consultation completion.</summary>
    public DateTime? SourceCreatedAt { get; set; }
    public string EngagementStatus { get; set; } = ConsultationStatuses.Unclassified;
    public int? ConsultantUserId { get; set; }
    public string? Source { get; set; }
    public string? ExternalReference { get; set; }
    public int? ImportBatchId { get; set; }
    public string? Notes { get; set; }
    public string? SourceEvidence { get; set; }
    public int? InvoiceId { get; set; }
}

public static class ConsultationStatuses
{
    public const string Unclassified = "Unclassified";
    public const string Historical = "Historical";
    public const string Interested = "Interested";
    public const string Contacted = "Contacted";
    public const string AppointmentProposed = "AppointmentProposed";
    public const string Scheduled = "Scheduled";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";
    public const string NotInterested = "NotInterested";
    public const string Closed = "Closed";

    public static readonly string[] All =
    [
        Unclassified, Historical, Interested, Contacted, AppointmentProposed,
        Scheduled, Completed, Cancelled, NoShow, NotInterested, Closed
    ];

    public static bool IsProspect(string? status) =>
        status is Unclassified or Historical or Interested or Contacted or AppointmentProposed or NotInterested or Closed or Cancelled;

    public static bool IsClient(string? status) =>
        status is Scheduled or Completed or NoShow;
}

public static class ConsultationServiceNames
{
    public const string GeneralHistorical = "General Historical Consultation";
}

/// <summary>CRM Event catalog (table name Events).</summary>
public class CrmEvent : BaseEntity
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public ICollection<EventParticipation> Participations { get; set; } = new List<EventParticipation>();
}

public class EventParticipation : BaseEntity
{
    public int CompanyId { get; set; }
    public int PartyId { get; set; }
    public Party? Party { get; set; }
    public int EventId { get; set; }
    public CrmEvent? Event { get; set; }
    public string RegistrationStatus { get; set; } = EventRegistrationStatuses.Registered;
    public string? AttendanceStatus { get; set; }
    public string? Source { get; set; }
    public string? ExternalReference { get; set; }
    public int? ImportBatchId { get; set; }
    public string? Notes { get; set; }
    public int? InvoiceId { get; set; }
}

public static class EventRegistrationStatuses
{
    public const string Registered = "Registered";
    public const string Waitlisted = "Waitlisted";
    public const string Cancelled = "Cancelled";
    public const string Historical = "Historical";
}

public static class EventAttendanceStatuses
{
    public const string Attended = "Attended";
    public const string Absent = "Absent";
    public const string Unknown = "Unknown";
}

/// <summary>Catalog of purchasable astrology/numerology report products.</summary>
public class ReportProduct : BaseEntity
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<ReportPurchase> Purchases { get; set; } = new List<ReportPurchase>();
}

public static class ReportProductNames
{
    public const string HoroscopeReport = "Horoscope Report";
    public const string NumerologyReport = "Numerology Report";
}

/// <summary>
/// One paid report transaction linked to a purchaser Party.
/// Subject/birth fields describe the report request subject — not PartyPerson identity.
/// </summary>
public class ReportPurchase : BaseEntity
{
    public int CompanyId { get; set; }
    public int PartyId { get; set; }
    public Party? Party { get; set; }
    public int ReportProductId { get; set; }
    public ReportProduct? ReportProduct { get; set; }

    public string? SourceSystem { get; set; }
    public string? SourceFile { get; set; }
    public string? SourceId { get; set; }
    /// <summary>Stable idempotency key: source_file:source_id</summary>
    public string? ExternalReference { get; set; }

    public decimal? AmountPaid { get; set; }
    public string? Currency { get; set; }
    public string PaymentStatus { get; set; } = "Completed";
    public string? ReportStatus { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? DeliveryEmailStatus { get; set; }
    public string? DeliveryWhatsappStatus { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public string? RazorpayStatus { get; set; }

    /// <summary>Report subject name (may differ from purchaser Party).</summary>
    public string? SubjectName { get; set; }
    public string? SubjectGender { get; set; }
    public int? BirthDay { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthYear { get; set; }
    public int? BirthHours { get; set; }
    public int? BirthMinutes { get; set; }
    public int? BirthSeconds { get; set; }
    public string? Place { get; set; }

    public int? ImportBatchId { get; set; }
    public string? Notes { get; set; }
    public int? SalesOrderId { get; set; }
    public int? InvoiceId { get; set; }
}
