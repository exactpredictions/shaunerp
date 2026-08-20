namespace ShaunERP.Core.Crm;

/// <summary>Lead — unqualified potential customer (Zoho / Dynamics / SAP C4C).</summary>
public class Lead : BaseEntity
{
    public int? CompanyId { get; set; }

    public string? LeadOwner { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? CompanyName { get; set; }

    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? Fax { get; set; }
    public string? Website { get; set; }

    public string? LeadSource { get; set; }
    public string? Industry { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public bool EmailOptOut { get; set; }
    public string? LeadStatus { get; set; } = "New";
    public int? NoOfEmployees { get; set; }
    public string? Rating { get; set; }

    public string? SkypeId { get; set; }
    public string? Twitter { get; set; }

    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }

    public string? Description { get; set; }
    public bool IsQualified { get; set; }

    public string? PreferredContactMethod { get; set; }
    public string? Gender { get; set; }
    public string? Language { get; set; }

    /// <summary>0–100 score (Dynamics / Zoho lead scoring).</summary>
    public int? Score { get; set; }
    public string? Territory { get; set; }
    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public string? Tags { get; set; }

    /// <summary>Permanent identity link. Nullable during migration; never remove Name/Phone/Email columns.</summary>
    public int? PartyId { get; set; }
    public ShaunERP.Core.Identity.Party? Party { get; set; }
}

/// <summary>Prospect — qualified lead actively engaged (ShaunERP sales stage).</summary>
public class Prospect : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public string? ProspectOwner { get; set; }
    public string? Stage { get; set; } = "Engaged";
    public string? Source { get; set; }
    public string? Industry { get; set; }
    public string? Territory { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public string? Description { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public string? Tags { get; set; }
    public int? PartyId { get; set; }
    public ShaunERP.Core.Identity.Party? Party { get; set; }
}

/// <summary>Account — B2B organization (Salesforce / Dynamics Account, Zoho Account).</summary>
public class Account : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? AccountOwner { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string? AccountType { get; set; } = "Customer";
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public int? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }
    public string? Ownership { get; set; }
    public int? Employees { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public string? Rating { get; set; }
    public string? Territory { get; set; }
    public string? BillingStreet { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingZip { get; set; }
    public string? BillingCountry { get; set; }
    public string? ShippingStreet { get; set; }
    public string? ShippingCity { get; set; }
    public string? ShippingState { get; set; }
    public string? ShippingZip { get; set; }
    public string? ShippingCountry { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public string? TaxId { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? CreditLimit { get; set; }
}

/// <summary>Contact — person linked to an account, prospect, or customer.</summary>
public class Contact : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? ContactOwner { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Department { get; set; }
    public DateTime? DateOfBirth { get; set; }

    public string? AccountName { get; set; }
    public string? VendorName { get; set; }
    public string? ReportingTo { get; set; }

    public string? Email { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? HomePhone { get; set; }
    public string? Fax { get; set; }
    public string? Assistant { get; set; }
    public string? AsstPhone { get; set; }

    public string? LeadSource { get; set; }
    public string? SkypeId { get; set; }
    public bool EmailOptOut { get; set; }
    public string? Twitter { get; set; }

    public string? MailingStreet { get; set; }
    public string? MailingCity { get; set; }
    public string? MailingState { get; set; }
    public string? MailingZip { get; set; }
    public string? MailingCountry { get; set; }

    public string? OtherStreet { get; set; }
    public string? OtherCity { get; set; }
    public string? OtherState { get; set; }
    public string? OtherZip { get; set; }
    public string? OtherCountry { get; set; }

    public string? Description { get; set; }
    public int? ProspectId { get; set; }
    public Prospect? Prospect { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    public string? PreferredContactMethod { get; set; }
    public string? Gender { get; set; }
    public string? Language { get; set; }
    public bool IsPrimary { get; set; }
    public string? Tags { get; set; }
    public int? PartyId { get; set; }
    public ShaunERP.Core.Identity.Party? Party { get; set; }
}

/// <summary>Opportunity — sales deal / pipeline item (Dynamics Opportunity, Zoho Deal).</summary>
public class Opportunity : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? DealOwner { get; set; }
    public string DealName { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string? Type { get; set; }
    public string? NextStep { get; set; }
    public string? LeadSource { get; set; }
    public string? ContactName { get; set; }

    public decimal Amount { get; set; }
    public DateTime? ClosingDate { get; set; }
    public decimal? ExpectedRevenue { get; set; }
    public double? Probability { get; set; }

    public string? Pipeline { get; set; } = "Standard";
    public string? Stage { get; set; } = "Qualification";
    public string? CampaignSource { get; set; }
    public string? Description { get; set; }

    public DateTime? ActualCloseDate { get; set; }
    public decimal? ActualRevenue { get; set; }
    public string? WinLossReason { get; set; }
    public string? Competitor { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public int? ProspectId { get; set; }
    public Prospect? Prospect { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }

    /// <summary>Pipeline / Best Case / Commit / Omitted (Dynamics forecast).</summary>
    public string? ForecastCategory { get; set; } = "Pipeline";
    public string? Territory { get; set; }
    public string? CurrencyCode { get; set; } = "USD";
    public string? Tags { get; set; }
    public int? PartyId { get; set; }
    public ShaunERP.Core.Identity.Party? Party { get; set; }
}

/// <summary>Customer — converted account after a won deal (ERP customer master).</summary>
public class Customer : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? PrimaryContact { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? BillingAddress { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    public string? TaxId { get; set; }

    public string? CustomerOwner { get; set; }
    public string? AccountNumber { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? CustomerType { get; set; } = "Business";
    public string? ShippingAddress { get; set; }
    public string? Rating { get; set; }
    public string? Territory { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public string? Tags { get; set; }
    public string? Description { get; set; }
    public int? PartyId { get; set; }
    public ShaunERP.Core.Identity.Party? Party { get; set; }
}

/// <summary>CRM task / to-do activity.</summary>
public class CrmTask : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? TaskOwner { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string? TaskTo { get; set; }
    public string? TaskType { get; set; }
    public string? Priority { get; set; } = "Normal";
    public string? Reminder { get; set; }
    public string? Repeat { get; set; }
    public string? Description { get; set; }
    public CrmTaskStatus TaskStatus { get; set; } = CrmTaskStatus.NotStarted;

    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
}

/// <summary>Scheduled meeting / event.</summary>
public class Meeting : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? Title { get; set; }
    public string? Location { get; set; }
    public bool AllDay { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Host { get; set; }
    /// <summary>Comma-separated participant names/emails.</summary>
    public string? Participants { get; set; }
    public string? RelatedTo { get; set; }
    public string? Repeat { get; set; }
    public DateTime? ParticipantsReminder { get; set; }
    public DateTime? Reminder { get; set; }
    public string? Agenda { get; set; }
    public string? MeetingType { get; set; }

    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
}

/// <summary>Phone call log (Dynamics / Zoho Calls).</summary>
public class CrmCall : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? CallOwner { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? CallType { get; set; } = "Outbound";
    public string? CallPurpose { get; set; }
    public DateTime? CallStart { get; set; }
    public int? DurationMinutes { get; set; }
    public string? CallResult { get; set; }
    public string? Description { get; set; }
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? ContactName { get; set; }
    public string? PhoneNumber { get; set; }
}

/// <summary>Note / timeline entry on any CRM record.</summary>
public class CrmNote : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? NoteOwner { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public bool IsPinned { get; set; }
    public string RelatedEntityType { get; set; } = "Lead";
    public int RelatedEntityId { get; set; }
}

/// <summary>Marketing campaign (SAP / Dynamics / Zoho Campaigns).</summary>
public class Campaign : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? CampaignOwner { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CampaignType { get; set; } = "Email";
    public string? CampaignStatus { get; set; } = "Planning";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? BudgetedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public decimal? ExpectedRevenue { get; set; }
    public int? ExpectedResponse { get; set; }
    public int? NumSent { get; set; }
    public string? Description { get; set; }
    public string? ParentCampaign { get; set; }
}

/// <summary>Product catalog item for quotes and opportunities.</summary>
public class CrmProduct : BaseEntity
{
    public int? CompanyId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ProductFamily { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public string? CurrencyCode { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
    public string? UnitOfMeasure { get; set; } = "Each";
    public decimal? TaxRate { get; set; }
}

/// <summary>Sales quote / proposal (Dynamics Quote, Zoho Quote).</summary>
public class Quote : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? QuoteOwner { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? QuoteStage { get; set; } = "Draft";
    public DateTime? ValidUntil { get; set; }
    public string? AccountName { get; set; }
    public string? ContactName { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public string? CurrencyCode { get; set; } = "USD";
    public string? Terms { get; set; }
    public string? Description { get; set; }
    public ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
}

public class QuoteLine : BaseEntity
{
    public int? CompanyId { get; set; }
    public int QuoteId { get; set; }
    public Quote? Quote { get; set; }
    public int? ProductId { get; set; }
    public CrmProduct? Product { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Service case / ticket (Dynamics Case, Zoho Desk-style CRM cases).</summary>
public class CrmCase : BaseEntity
{
    public int? CompanyId { get; set; }
    public string? CaseOwner { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? CaseType { get; set; } = "Question";
    public string? Priority { get; set; } = "Medium";
    public string? CaseStatus { get; set; } = "New";
    public string? Origin { get; set; } = "Phone";
    public string? Reason { get; set; }
    public string? Description { get; set; }
    public string? Resolution { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? AccountName { get; set; }
    public string? ContactName { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? Tags { get; set; }
}

public enum CrmTaskStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Deferred = 3
}

/// <summary>Standard related-entity types for polymorphic activity linking.</summary>
public static class CrmRelatedTypes
{
    public const string Lead = "Lead";
    public const string Prospect = "Prospect";
    public const string Account = "Account";
    public const string Contact = "Contact";
    public const string Opportunity = "Opportunity";
    public const string Customer = "Customer";
    public const string Case = "Case";
    public const string Campaign = "Campaign";
    public const string Quote = "Quote";

    public static readonly string[] All =
    [
        Lead, Prospect, Account, Contact, Opportunity, Customer, Case, Campaign, Quote
    ];
}
