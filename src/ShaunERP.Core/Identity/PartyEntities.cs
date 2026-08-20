namespace ShaunERP.Core.Identity;

/// <summary>
/// Permanent identity root. CRM stages (Lead/Prospect/Customer) reference this via PartyId.
/// Party itself is never "converted" — roles attach to it.
/// </summary>
public class Party : BaseEntity
{
    public int CompanyId { get; set; }

    /// <summary>Person | Organization</summary>
    public string PartyType { get; set; } = PartyTypes.Person;

    /// <summary>Canonical display name for search/UI.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional link when PartyType=Organization maps to an existing CRM Account.</summary>
    public int? CrmAccountId { get; set; }

    public PartyPerson? Person { get; set; }
    public ICollection<PartyPhone> Phones { get; set; } = new List<PartyPhone>();
    public ICollection<PartyEmail> Emails { get; set; } = new List<PartyEmail>();
}

public static class PartyTypes
{
    public const string Person = "Person";
    public const string Organization = "Organization";
}

/// <summary>Person-specific identity fields. Not a CRM stage.</summary>
public class PartyPerson : BaseEntity
{
    public int PartyId { get; set; }
    public Party? Party { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Gender { get; set; }
    public string? Language { get; set; }
}

public class PartyPhone : BaseEntity
{
    public int PartyId { get; set; }
    public Party? Party { get; set; }

    public string? OriginalPhone { get; set; }
    /// <summary>Indian mobile last-10 digits when applicable; otherwise digits-only fallback.</summary>
    public string? NormalizedPhone { get; set; }
    /// <summary>E.164 when country can be inferred (e.g. +919876543210).</summary>
    public string? E164Phone { get; set; }

    /// <summary>Mobile | Phone | Work | Home | Other</summary>
    public string PhoneType { get; set; } = "Mobile";
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PartyEmail : BaseEntity
{
    public int PartyId { get; set; }
    public Party? Party { get; set; }

    public string? OriginalEmail { get; set; }
    public string? NormalizedEmail { get; set; }

    /// <summary>Work | Personal | Other</summary>
    public string EmailType { get; set; } = "Work";
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class ImportMatchStatuses
{
    public const string NoMatch = "NoMatch";
    public const string HighConfidence = "HighConfidenceMatch";
    public const string Ambiguous = "AmbiguousMatch";
    public const string Conflict = "Conflict";
    public const string Created = "Created";
    public const string Linked = "Linked";
    public const string Skipped = "Skipped";
    public const string Failed = "Failed";
}

public static class ImportBatchStatuses
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string CompletedWithReview = "CompletedWithReview";
}

/// <summary>Traceable import run for leads backfill and future historical CSVs.</summary>
public class ImportBatch : BaseEntity
{
    public int CompanyId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ImportedBy { get; set; }
    public string BatchStatus { get; set; } = ImportBatchStatuses.Running;
    public int TotalRows { get; set; }
    public int MatchedRows { get; set; }
    public int CreatedRows { get; set; }
    public int ReviewRows { get; set; }
    public int FailedRows { get; set; }
    public string? Notes { get; set; }

    public ICollection<ImportSourceRow> Rows { get; set; } = new List<ImportSourceRow>();
}

public class ImportSourceRow : BaseEntity
{
    public int ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public int SourceRowNumber { get; set; }
    public string? ExternalId { get; set; }
    public string? RawDataJson { get; set; }
    public int? MatchedPartyId { get; set; }
    public string MatchStatus { get; set; } = ImportMatchStatuses.NoMatch;
    public string? MatchReason { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

/// <summary>Human review queue for ambiguous/conflicting identity matches.</summary>
public class ImportMatchReview : BaseEntity
{
    public int CompanyId { get; set; }
    public int? ImportBatchId { get; set; }
    public int? ImportSourceRowId { get; set; }
    public int? CandidatePartyId { get; set; }
    public string ReviewStatus { get; set; } = "Pending";
    public string? MatchReason { get; set; }
    public string? IncomingName { get; set; }
    public string? IncomingPhone { get; set; }
    public string? IncomingEmail { get; set; }
    public string? CandidateName { get; set; }
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
