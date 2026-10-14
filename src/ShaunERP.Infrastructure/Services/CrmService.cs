using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services.Identity;
using InvoiceStatus = ShaunERP.Core.Finance.InvoiceStatus;

namespace ShaunERP.Infrastructure.Services;

public interface ICrmService
{
    Task<CrmDashboardStats> GetDashboardAsync(CancellationToken ct = default);
    Task<CrmPipelineBoard> GetPipelineBoardAsync(CancellationToken ct = default);
    Task<CrmForecastStats> GetForecastAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Lead>> ListLeadsAsync(CancellationToken ct = default);
    Task<PagedResult<Lead>> QueryLeadsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Lead?> GetLeadAsync(int id, CancellationToken ct = default);
    Task<Lead> CreateLeadAsync(Lead lead, CancellationToken ct = default);
    Task<Lead?> UpdateLeadAsync(int id, Lead update, CancellationToken ct = default);
    Task<bool> DeleteLeadAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, LeadConversionResult? Result)> ConvertLeadAsync(
        int leadId, LeadConversionRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error, Prospect? Prospect)> ConvertLeadToProspectAsync(int leadId, CancellationToken ct = default);

    Task<IReadOnlyList<Prospect>> ListProspectsAsync(CancellationToken ct = default);
    Task<PagedResult<Prospect>> QueryProspectsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Prospect?> GetProspectAsync(int id, CancellationToken ct = default);
    Task<Prospect> CreateProspectAsync(Prospect prospect, CancellationToken ct = default);
    Task<Prospect?> UpdateProspectAsync(int id, Prospect update, CancellationToken ct = default);
    Task<bool> DeleteProspectAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Account>> ListAccountsAsync(CancellationToken ct = default);
    Task<PagedResult<Account>> QueryAccountsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Account?> GetAccountAsync(int id, CancellationToken ct = default);
    Task<Account> CreateAccountAsync(Account account, CancellationToken ct = default);
    Task<Account?> UpdateAccountAsync(int id, Account update, CancellationToken ct = default);
    Task<bool> DeleteAccountAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Contact>> ListContactsAsync(CancellationToken ct = default);
    Task<PagedResult<Contact>> QueryContactsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Contact?> GetContactAsync(int id, CancellationToken ct = default);
    Task<Contact> CreateContactAsync(Contact contact, CancellationToken ct = default);
    Task<Contact?> UpdateContactAsync(int id, Contact update, CancellationToken ct = default);
    Task<bool> DeleteContactAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Opportunity>> ListOpportunitiesAsync(CancellationToken ct = default);
    Task<PagedResult<Opportunity>> QueryOpportunitiesAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Opportunity?> GetOpportunityAsync(int id, CancellationToken ct = default);
    Task<Opportunity> CreateOpportunityAsync(Opportunity deal, CancellationToken ct = default);
    Task<Opportunity?> UpdateOpportunityAsync(int id, Opportunity update, CancellationToken ct = default);
    Task<bool> DeleteOpportunityAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, Customer? Customer)> WinOpportunityAsync(
        int opportunityId, string? winReason = null, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> LoseOpportunityAsync(
        int opportunityId, string? lossReason = null, CancellationToken ct = default);
    Task<Opportunity?> MoveOpportunityStageAsync(int id, string stage, CancellationToken ct = default);

    Task<IReadOnlyList<Customer>> ListCustomersAsync(CancellationToken ct = default);
    Task<PagedResult<Customer>> QueryCustomersAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Customer?> GetCustomerAsync(int id, CancellationToken ct = default);
    Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken ct = default);
    Task<Customer?> UpdateCustomerAsync(int id, Customer update, CancellationToken ct = default);
    Task<bool> DeleteCustomerAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Campaign>> ListCampaignsAsync(CancellationToken ct = default);
    Task<PagedResult<Campaign>> QueryCampaignsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Campaign?> GetCampaignAsync(int id, CancellationToken ct = default);
    Task<Campaign> CreateCampaignAsync(Campaign campaign, CancellationToken ct = default);
    Task<Campaign?> UpdateCampaignAsync(int id, Campaign update, CancellationToken ct = default);
    Task<bool> DeleteCampaignAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<CrmProduct>> ListProductsAsync(CancellationToken ct = default);
    Task<PagedResult<CrmProduct>> QueryProductsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<CrmProduct?> GetProductAsync(int id, CancellationToken ct = default);
    Task<CrmProduct> CreateProductAsync(CrmProduct product, CancellationToken ct = default);
    Task<CrmProduct?> UpdateProductAsync(int id, CrmProduct update, CancellationToken ct = default);
    Task<bool> DeleteProductAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Quote>> ListQuotesAsync(CancellationToken ct = default);
    Task<PagedResult<Quote>> QueryQuotesAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Quote?> GetQuoteAsync(int id, CancellationToken ct = default);
    Task<Quote> CreateQuoteAsync(Quote quote, CancellationToken ct = default);
    Task<Quote?> UpdateQuoteAsync(int id, Quote update, CancellationToken ct = default);
    Task<bool> DeleteQuoteAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<CrmCase>> ListCasesAsync(CancellationToken ct = default);
    Task<PagedResult<CrmCase>> QueryCasesAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<CrmCase?> GetCaseAsync(int id, CancellationToken ct = default);
    Task<CrmCase> CreateCaseAsync(CrmCase crmCase, CancellationToken ct = default);
    Task<CrmCase?> UpdateCaseAsync(int id, CrmCase update, CancellationToken ct = default);
    Task<bool> DeleteCaseAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<CrmTask>> ListTasksAsync(CancellationToken ct = default);
    Task<PagedResult<CrmTask>> QueryTasksAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<CrmTask?> GetTaskAsync(int id, CancellationToken ct = default);
    Task<CrmTask> CreateTaskAsync(CrmTask task, CancellationToken ct = default);
    Task<CrmTask?> UpdateTaskAsync(int id, CrmTask update, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Meeting>> ListMeetingsAsync(CancellationToken ct = default);
    Task<PagedResult<Meeting>> QueryMeetingsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Meeting?> GetMeetingAsync(int id, CancellationToken ct = default);
    Task<Meeting> CreateMeetingAsync(Meeting meeting, CancellationToken ct = default);
    Task<Meeting?> UpdateMeetingAsync(int id, Meeting update, CancellationToken ct = default);
    Task<bool> DeleteMeetingAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<CrmCall>> ListCallsAsync(CancellationToken ct = default);
    Task<PagedResult<CrmCall>> QueryCallsAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<CrmCall?> GetCallAsync(int id, CancellationToken ct = default);
    Task<CrmCall> CreateCallAsync(CrmCall call, CancellationToken ct = default);
    Task<CrmCall?> UpdateCallAsync(int id, CrmCall update, CancellationToken ct = default);
    Task<bool> DeleteCallAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<CrmNote>> ListNotesAsync(string? relatedType = null, int? relatedId = null, CancellationToken ct = default);
    Task<PagedResult<CrmNote>> QueryNotesAsync(ListQuery? query = null, string? relatedType = null, int? relatedId = null, CancellationToken ct = default);
    Task<CrmNote?> GetNoteAsync(int id, CancellationToken ct = default);
    Task<CrmNote> CreateNoteAsync(CrmNote note, CancellationToken ct = default);
    Task<CrmNote?> UpdateNoteAsync(int id, CrmNote update, CancellationToken ct = default);
    Task<bool> DeleteNoteAsync(int id, CancellationToken ct = default);
}

public record CrmDashboardStats(
    int Leads,
    int Prospects,
    int Accounts,
    int Contacts,
    int Opportunities,
    int Customers,
    int Campaigns,
    int OpenCases,
    int OpenTasks,
    int UpcomingMeetings,
    int CallsThisWeek,
    decimal PipelineAmount,
    decimal WeightedPipeline,
    decimal WonThisMonth,
    int People = 0,
    int PendingIdentityReviews = 0,
    int SubscriptionMembers = 0,
    int ActiveSubscribers = 0,
    int InactiveSubscribers = 0);

public record CrmPipelineColumn(string Stage, int Count, decimal Amount, IReadOnlyList<Opportunity> Deals);

public record CrmPipelineBoard(IReadOnlyList<CrmPipelineColumn> Columns, decimal TotalAmount);

public record CrmForecastStats(
    decimal OpenPipeline,
    decimal WeightedPipeline,
    decimal CommitAmount,
    decimal BestCaseAmount,
    decimal WonThisMonth,
    decimal LostThisMonth,
    int OpenDeals,
    int WonDeals,
    int LostDeals,
    IReadOnlyList<CrmForecastByOwner> ByOwner);

public record CrmForecastByOwner(string Owner, decimal Amount, decimal Weighted, int Deals);

public record LeadConversionRequest(
    bool CreateProspect = true,
    bool CreateAccount = true,
    bool CreateContact = true,
    bool CreateOpportunity = false,
    string? OpportunityName = null,
    decimal? OpportunityAmount = null);

public record LeadConversionResult(
    Prospect? Prospect,
    Account? Account,
    Contact? Contact,
    Opportunity? Opportunity);

public partial class CrmService : ICrmService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;
    private readonly IPartyIdentityService _parties;

    public static readonly string[] StandardStages =
    [
        "Qualification", "Needs Analysis", "Proposal", "Negotiation", "Closed Won", "Closed Lost"
    ];

    public CrmService(
        ShaunErpDbContext db,
        ITenantContext tenant,
        IModuleRegistry modules,
        IPartyIdentityService parties)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
        _parties = parties;
    }

    public async Task<CrmDashboardStats> GetDashboardAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmDashboardView);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = now.AddDays(-(int)now.DayOfWeek);

        var leads = TenantFilter(_db.Leads.AsNoTracking());
        var prospects = TenantFilter(_db.Prospects.AsNoTracking());
        var accounts = TenantFilter(_db.CrmAccounts.AsNoTracking());
        var contacts = TenantFilter(_db.Contacts.AsNoTracking());
        var opportunities = TenantFilter(_db.Opportunities.AsNoTracking());
        var customers = TenantFilter(_db.CrmCustomers.AsNoTracking());
        var campaigns = TenantFilter(_db.Campaigns.AsNoTracking());
        var cases = TenantFilter(_db.CrmCases.AsNoTracking());
        var tasks = TenantFilter(_db.CrmTasks.AsNoTracking());
        var meetings = TenantFilter(_db.Meetings.AsNoTracking());
        var calls = TenantFilter(_db.CrmCalls.AsNoTracking());

        var openDeals = opportunities.Where(o => o.Stage != "Closed Won" && o.Stage != "Closed Lost");

        var people = await _db.Parties.AsNoTracking()
            .CountAsync(p => p.CompanyId == RequireCompanyId() && p.PartyType == PartyTypes.Person, ct);
        var pendingReviews = await _db.ImportMatchReviews.AsNoTracking()
            .CountAsync(r => r.CompanyId == RequireCompanyId() && r.ReviewStatus == "Pending", ct);
        var companyId = RequireCompanyId();
        var subscriptionMembers = await _db.SubscriptionMemberships.AsNoTracking()
            .CountAsync(m => m.CompanyId == companyId, ct);
        var activeSubscribers = await _db.SubscriptionMemberships.AsNoTracking()
            .CountAsync(m => m.CompanyId == companyId && m.MembershipStatus == MembershipStatuses.Active, ct);
        var inactiveSubscribers = subscriptionMembers - activeSubscribers;

        return new CrmDashboardStats(
            await leads.CountAsync(ct),
            await prospects.CountAsync(ct),
            await accounts.CountAsync(ct),
            await contacts.CountAsync(ct),
            await opportunities.CountAsync(ct),
            await customers.CountAsync(ct),
            await campaigns.CountAsync(ct),
            await cases.CountAsync(c => c.CaseStatus != "Closed" && c.CaseStatus != "Resolved", ct),
            await tasks.CountAsync(t => t.TaskStatus != CrmTaskStatus.Completed, ct),
            await meetings.CountAsync(m => m.From == null || m.From >= now, ct),
            await calls.CountAsync(c => c.CallStart != null && c.CallStart >= weekStart, ct),
            await openDeals.SumAsync(o => (decimal?)o.Amount, ct) ?? 0m,
            await openDeals.SumAsync(o => (decimal?)(o.Amount * (decimal)((o.Probability ?? 0) / 100.0)), ct) ?? 0m,
            await opportunities.Where(o => o.Stage == "Closed Won" && o.ActualCloseDate >= monthStart).SumAsync(o => (decimal?)(o.ActualRevenue ?? o.Amount), ct) ?? 0m,
            people,
            pendingReviews,
            subscriptionMembers,
            activeSubscribers,
            inactiveSubscribers);
    }

    public async Task<CrmPipelineBoard> GetPipelineBoardAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmPipelineView);
        var deals = await TenantFilter(_db.Opportunities.AsNoTracking())
            .OrderByDescending(x => x.Amount)
            .Take(500)
            .ToListAsync(ct);

        var columns = StandardStages.Select(stage =>
        {
            var stageDeals = deals.Where(d => string.Equals(d.Stage, stage, StringComparison.OrdinalIgnoreCase)).ToList();
            return new CrmPipelineColumn(stage, stageDeals.Count, stageDeals.Sum(d => d.Amount), stageDeals);
        }).ToList();

        return new CrmPipelineBoard(columns, deals.Where(d => d.Stage is not ("Closed Won" or "Closed Lost")).Sum(d => d.Amount));
    }

    public async Task<CrmForecastStats> GetForecastAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmForecastView);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var deals = await TenantFilter(_db.Opportunities.AsNoTracking()).Take(1000).ToListAsync(ct);

        var open = deals.Where(d => d.Stage is not ("Closed Won" or "Closed Lost")).ToList();
        var wonMonth = deals.Where(d => d.Stage == "Closed Won" && (d.ActualCloseDate ?? d.ClosingDate) >= monthStart).ToList();
        var lostMonth = deals.Where(d => d.Stage == "Closed Lost" && (d.ActualCloseDate ?? d.ClosingDate) >= monthStart).ToList();

        decimal Weighted(Opportunity o) => o.Amount * (decimal)((o.Probability ?? 0) / 100.0);

        var byOwner = open
            .GroupBy(d => string.IsNullOrWhiteSpace(d.DealOwner) ? "Unassigned" : d.DealOwner!)
            .Select(g => new CrmForecastByOwner(
                g.Key,
                g.Sum(x => x.Amount),
                g.Sum(Weighted),
                g.Count()))
            .OrderByDescending(x => x.Amount)
            .ToList();

        return new CrmForecastStats(
            open.Sum(d => d.Amount),
            open.Sum(Weighted),
            open.Where(d => string.Equals(d.ForecastCategory, "Commit", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Amount),
            open.Where(d => string.Equals(d.ForecastCategory, "Best Case", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(d.ForecastCategory, "Commit", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Amount),
            wonMonth.Sum(d => d.ActualRevenue ?? d.Amount),
            lostMonth.Sum(d => d.Amount),
            open.Count,
            wonMonth.Count,
            lostMonth.Count,
            byOwner);
    }

    public async Task<IReadOnlyList<Lead>> ListLeadsAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmLeadsView);
        return await TenantFilter(_db.Leads.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<Lead?> GetLeadAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmLeadsView);
        return await TenantFilter(_db.Leads.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Lead> CreateLeadAsync(Lead lead, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmLeadsCreate);
        lead.Id = 0;
        lead.CompanyId = RequireCompanyId();
        lead.CreatedDate = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(lead.LeadStatus)) lead.LeadStatus = "New";

        if (lead.PartyId is null)
        {
            var ensure = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
            {
                CompanyId = lead.CompanyId.Value,
                FirstName = lead.FirstName,
                LastName = lead.LastName,
                Phone = lead.Phone,
                Mobile = lead.Mobile,
                Email = lead.Email,
                SecondaryEmail = lead.SecondaryEmail,
                CreateReviewOnAmbiguous = true
            }, ct);
            lead.PartyId = ensure.PartyId;
        }

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);
        return lead;
    }

    public async Task<Lead?> UpdateLeadAsync(int id, Lead update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmLeadsUpdate);
        var lead = await FindOwnedAsync(_db.Leads, id, ct);
        if (lead is null) return null;

        lead.CompanyId = RequireCompanyId();
        lead.LeadOwner = update.LeadOwner;
        lead.FirstName = update.FirstName;
        lead.LastName = update.LastName;
        lead.Title = update.Title;
        lead.CompanyName = update.CompanyName;
        lead.Phone = update.Phone;
        lead.Mobile = update.Mobile;
        lead.Email = update.Email;
        lead.SecondaryEmail = update.SecondaryEmail;
        lead.Fax = update.Fax;
        lead.Website = update.Website;
        lead.LeadSource = update.LeadSource;
        lead.Industry = update.Industry;
        lead.AnnualRevenue = update.AnnualRevenue;
        lead.EmailOptOut = update.EmailOptOut;
        lead.LeadStatus = update.LeadStatus;
        lead.NoOfEmployees = update.NoOfEmployees;
        lead.Rating = update.Rating;
        lead.SkypeId = update.SkypeId;
        lead.Twitter = update.Twitter;
        lead.Street = update.Street;
        lead.City = update.City;
        lead.State = update.State;
        lead.ZipCode = update.ZipCode;
        lead.Country = update.Country;
        lead.Description = update.Description;
        lead.IsQualified = update.IsQualified;
        lead.PreferredContactMethod = update.PreferredContactMethod;
        lead.Gender = update.Gender;
        lead.Language = update.Language;
        lead.Score = update.Score;
        lead.Territory = update.Territory;
        lead.CampaignId = update.CampaignId;
        lead.Tags = update.Tags;
        if (update.PartyId is not null)
        {
            lead.PartyId = update.PartyId;
        }
        else if (lead.PartyId is int partyId)
        {
            await _parties.AttachContactChannelsAsync(partyId, lead.Phone, lead.Mobile, lead.Email, lead.SecondaryEmail, ct);
        }

        await _db.SaveChangesAsync(ct);
        return lead;
    }

    public async Task<bool> DeleteLeadAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmLeadsDelete);
        var lead = await FindOwnedAsync(_db.Leads, id, ct);
        if (lead is null) return false;
        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(bool Ok, string? Error, Prospect? Prospect)> ConvertLeadToProspectAsync(
        int leadId, CancellationToken ct = default)
    {
        var (ok, error, result) = await ConvertLeadAsync(leadId, new LeadConversionRequest(
            CreateProspect: true, CreateAccount: false, CreateContact: false, CreateOpportunity: false), ct);
        return (ok, error, result?.Prospect);
    }

    public async Task<(bool Ok, string? Error, LeadConversionResult? Result)> ConvertLeadAsync(
        int leadId, LeadConversionRequest request, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmLeadsUpdate);
        var lead = await FindOwnedAsync(_db.Leads, leadId, ct);
        if (lead is null) return (false, "Lead not found.", null);

        lead.IsQualified = true;
        lead.LeadStatus = "Qualified";
        var companyId = RequireCompanyId();

        Prospect? prospect = null;
        Account? account = null;
        Contact? contact = null;
        Opportunity? opportunity = null;

        if (request.CreateAccount && !string.IsNullOrWhiteSpace(lead.CompanyName))
        {
            account = new Account
            {
                CompanyId = companyId,
                AccountOwner = lead.LeadOwner,
                AccountName = lead.CompanyName!,
                Industry = lead.Industry,
                Website = lead.Website,
                Phone = lead.Phone,
                Email = lead.Email,
                AnnualRevenue = lead.AnnualRevenue,
                Employees = lead.NoOfEmployees,
                Rating = lead.Rating,
                Territory = lead.Territory,
                BillingStreet = lead.Street,
                BillingCity = lead.City,
                BillingState = lead.State,
                BillingZip = lead.ZipCode,
                BillingCountry = lead.Country,
                AccountType = "Prospect",
                CreatedDate = DateTime.UtcNow
            };
            _db.CrmAccounts.Add(account);
            await _db.SaveChangesAsync(ct);
        }

        if (request.CreateProspect)
        {
            if (await TenantFilter(_db.Prospects).AnyAsync(p => p.LeadId == leadId, ct))
                return (false, "Lead already converted to a prospect.", null);

            prospect = new Prospect
            {
                CompanyId = companyId,
                CompanyName = lead.CompanyName,
                FirstName = lead.FirstName,
                LastName = lead.LastName,
                Email = lead.Email,
                PhoneNumber = lead.Mobile ?? lead.Phone,
                LeadId = lead.Id,
                ProspectOwner = lead.LeadOwner,
                Source = lead.LeadSource,
                Industry = lead.Industry,
                Territory = lead.Territory,
                AccountId = account?.Id,
                PartyId = lead.PartyId,
                Stage = "Engaged",
                CreatedDate = DateTime.UtcNow
            };
            _db.Prospects.Add(prospect);
            await _db.SaveChangesAsync(ct);
        }

        if (request.CreateContact)
        {
            contact = new Contact
            {
                CompanyId = companyId,
                ContactOwner = lead.LeadOwner,
                FirstName = lead.FirstName,
                LastName = lead.LastName,
                Title = lead.Title,
                AccountName = lead.CompanyName,
                Email = lead.Email,
                SecondaryEmail = lead.SecondaryEmail,
                Phone = lead.Phone,
                Mobile = lead.Mobile,
                Fax = lead.Fax,
                LeadSource = lead.LeadSource,
                SkypeId = lead.SkypeId,
                Twitter = lead.Twitter,
                EmailOptOut = lead.EmailOptOut,
                MailingStreet = lead.Street,
                MailingCity = lead.City,
                MailingState = lead.State,
                MailingZip = lead.ZipCode,
                MailingCountry = lead.Country,
                PreferredContactMethod = lead.PreferredContactMethod,
                Gender = lead.Gender,
                Language = lead.Language,
                ProspectId = prospect?.Id,
                AccountId = account?.Id,
                PartyId = lead.PartyId,
                IsPrimary = true,
                CreatedDate = DateTime.UtcNow
            };
            _db.Contacts.Add(contact);
            await _db.SaveChangesAsync(ct);
        }

        if (request.CreateOpportunity)
        {
            opportunity = new Opportunity
            {
                CompanyId = companyId,
                DealOwner = lead.LeadOwner,
                DealName = request.OpportunityName ?? $"{lead.CompanyName ?? "Deal"} — New opportunity",
                AccountName = lead.CompanyName,
                ContactName = $"{lead.FirstName} {lead.LastName}".Trim(),
                LeadSource = lead.LeadSource,
                Amount = request.OpportunityAmount ?? 0,
                ClosingDate = DateTime.UtcNow.AddDays(30),
                Probability = 20,
                Pipeline = "Standard",
                Stage = "Qualification",
                ForecastCategory = "Pipeline",
                LeadId = lead.Id,
                ProspectId = prospect?.Id,
                AccountId = account?.Id,
                ContactId = contact?.Id,
                CampaignId = lead.CampaignId,
                Territory = lead.Territory,
                PartyId = lead.PartyId,
                CreatedDate = DateTime.UtcNow
            };
            opportunity.ExpectedRevenue = opportunity.Amount * (decimal)(opportunity.Probability!.Value / 100.0);
            _db.Opportunities.Add(opportunity);
            await _db.SaveChangesAsync(ct);
        }

        await _db.SaveChangesAsync(ct);
        return (true, null, new LeadConversionResult(prospect, account, contact, opportunity));
    }

    public async Task<IReadOnlyList<Prospect>> ListProspectsAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProspectsView);
        return await TenantFilter(_db.Prospects.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<Prospect?> GetProspectAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProspectsView);
        return await TenantFilter(_db.Prospects.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Prospect> CreateProspectAsync(Prospect prospect, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProspectsCreate);
        prospect.Id = 0;
        prospect.CompanyId = RequireCompanyId();
        prospect.CreatedDate = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(prospect.Stage)) prospect.Stage = "Engaged";
        if (prospect.PartyId is null)
        {
            var ensure = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
            {
                CompanyId = prospect.CompanyId.Value,
                FirstName = prospect.FirstName,
                LastName = prospect.LastName,
                Phone = prospect.PhoneNumber,
                Mobile = prospect.PhoneNumber,
                Email = prospect.Email,
                CreateReviewOnAmbiguous = true
            }, ct);
            prospect.PartyId = ensure.PartyId;
        }
        _db.Prospects.Add(prospect);
        await _db.SaveChangesAsync(ct);
        return prospect;
    }

    public async Task<Prospect?> UpdateProspectAsync(int id, Prospect update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProspectsUpdate);
        var row = await FindOwnedAsync(_db.Prospects, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.CompanyName = update.CompanyName;
        row.FirstName = update.FirstName;
        row.LastName = update.LastName;
        row.Email = update.Email;
        row.PhoneNumber = update.PhoneNumber;
        row.LeadId = update.LeadId;
        row.ProspectOwner = update.ProspectOwner;
        row.Stage = update.Stage;
        row.Source = update.Source;
        row.Industry = update.Industry;
        row.Territory = update.Territory;
        row.ExpectedCloseDate = NormalizeUtc(update.ExpectedCloseDate);
        row.Description = update.Description;
        row.AccountId = update.AccountId;
        row.Tags = update.Tags;
        row.PartyId = await EnsureAndAttachPartyAsync(
            row.PartyId, update.PartyId, row.FirstName, row.LastName,
            row.PhoneNumber, row.PhoneNumber, row.Email, null, ct);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteProspectAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProspectsDelete);
        var row = await FindOwnedAsync(_db.Prospects, id, ct);
        if (row is null) return false;
        _db.Prospects.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Contact>> ListContactsAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmContactsView);
        return await TenantFilter(_db.Contacts.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<Contact?> GetContactAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmContactsView);
        return await TenantFilter(_db.Contacts.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Contact> CreateContactAsync(Contact contact, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmContactsCreate);
        contact.Id = 0;
        contact.CompanyId = RequireCompanyId();
        contact.CreatedDate = DateTime.UtcNow;
        if (contact.PartyId is null)
        {
            var ensure = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
            {
                CompanyId = contact.CompanyId.Value,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                Phone = contact.Phone,
                Mobile = contact.Mobile,
                Email = contact.Email,
                SecondaryEmail = contact.SecondaryEmail,
                CreateReviewOnAmbiguous = true
            }, ct);
            contact.PartyId = ensure.PartyId;
        }
        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);
        return contact;
    }

    public async Task<Contact?> UpdateContactAsync(int id, Contact update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmContactsUpdate);
        var row = await FindOwnedAsync(_db.Contacts, id, ct);
        if (row is null) return null;

        row.CompanyId = RequireCompanyId();
        row.ContactOwner = update.ContactOwner;
        row.FirstName = update.FirstName;
        row.LastName = update.LastName;
        row.Title = update.Title;
        row.Department = update.Department;
        row.DateOfBirth = NormalizeUtc(update.DateOfBirth);
        row.AccountName = update.AccountName;
        row.VendorName = update.VendorName;
        row.ReportingTo = update.ReportingTo;
        row.Email = update.Email;
        row.SecondaryEmail = update.SecondaryEmail;
        row.Phone = update.Phone;
        row.Mobile = update.Mobile;
        row.HomePhone = update.HomePhone;
        row.Fax = update.Fax;
        row.Assistant = update.Assistant;
        row.AsstPhone = update.AsstPhone;
        row.LeadSource = update.LeadSource;
        row.SkypeId = update.SkypeId;
        row.EmailOptOut = update.EmailOptOut;
        row.Twitter = update.Twitter;
        row.MailingStreet = update.MailingStreet;
        row.MailingCity = update.MailingCity;
        row.MailingState = update.MailingState;
        row.MailingZip = update.MailingZip;
        row.MailingCountry = update.MailingCountry;
        row.OtherStreet = update.OtherStreet;
        row.OtherCity = update.OtherCity;
        row.OtherState = update.OtherState;
        row.OtherZip = update.OtherZip;
        row.OtherCountry = update.OtherCountry;
        row.Description = update.Description;
        row.ProspectId = update.ProspectId;
        row.CustomerId = update.CustomerId;
        row.AccountId = update.AccountId;
        row.PreferredContactMethod = update.PreferredContactMethod;
        row.Gender = update.Gender;
        row.Language = update.Language;
        row.IsPrimary = update.IsPrimary;
        row.Tags = update.Tags;
        row.PartyId = await EnsureAndAttachPartyAsync(
            row.PartyId, update.PartyId, row.FirstName, row.LastName,
            row.Phone, row.Mobile, row.Email, row.SecondaryEmail, ct);

        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteContactAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmContactsDelete);
        var row = await FindOwnedAsync(_db.Contacts, id, ct);
        if (row is null) return false;
        _db.Contacts.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Opportunity>> ListOpportunitiesAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesView);
        return await TenantFilter(_db.Opportunities.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<Opportunity?> GetOpportunityAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesView);
        return await TenantFilter(_db.Opportunities.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Opportunity> CreateOpportunityAsync(Opportunity deal, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesCreate);
        deal.Id = 0;
        deal.CompanyId = RequireCompanyId();
        deal.CreatedDate = DateTime.UtcNow;
        deal.ClosingDate = NormalizeUtc(deal.ClosingDate);
        deal.ActualCloseDate = NormalizeUtc(deal.ActualCloseDate);
        if (string.IsNullOrWhiteSpace(deal.Stage)) deal.Stage = "Qualification";
        if (string.IsNullOrWhiteSpace(deal.Pipeline)) deal.Pipeline = "Standard";
        if (string.IsNullOrWhiteSpace(deal.ForecastCategory)) deal.ForecastCategory = "Pipeline";
        ApplyStageProbability(deal);
        deal.PartyId ??= await ResolveRelatedPartyIdAsync(deal, ct);
        _db.Opportunities.Add(deal);
        await _db.SaveChangesAsync(ct);
        return deal;
    }

    public async Task<Opportunity?> UpdateOpportunityAsync(int id, Opportunity update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesUpdate);
        var row = await FindOwnedAsync(_db.Opportunities, id, ct);
        if (row is null) return null;

        row.CompanyId = RequireCompanyId();
        row.DealOwner = update.DealOwner;
        row.DealName = update.DealName;
        row.AccountName = update.AccountName;
        row.Type = update.Type;
        row.NextStep = update.NextStep;
        row.LeadSource = update.LeadSource;
        row.ContactName = update.ContactName;
        row.Amount = update.Amount;
        row.ClosingDate = NormalizeUtc(update.ClosingDate);
        row.ExpectedRevenue = update.ExpectedRevenue;
        row.Probability = update.Probability;
        row.Pipeline = update.Pipeline;
        row.Stage = update.Stage;
        row.CampaignSource = update.CampaignSource;
        row.Description = update.Description;
        row.ActualCloseDate = NormalizeUtc(update.ActualCloseDate);
        row.ActualRevenue = update.ActualRevenue;
        row.WinLossReason = update.WinLossReason;
        row.Competitor = update.Competitor;
        row.LeadId = update.LeadId;
        row.ProspectId = update.ProspectId;
        row.CustomerId = update.CustomerId;
        row.AccountId = update.AccountId;
        row.ContactId = update.ContactId;
        row.CampaignId = update.CampaignId;
        row.ForecastCategory = update.ForecastCategory;
        row.Territory = update.Territory;
        row.CurrencyCode = update.CurrencyCode;
        row.Tags = update.Tags;
        ApplyStageProbability(row);
        row.PartyId = update.PartyId ?? row.PartyId ?? await ResolveRelatedPartyIdAsync(row, ct);

        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteOpportunityAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesDelete);
        var row = await FindOwnedAsync(_db.Opportunities, id, ct);
        if (row is null) return false;
        _db.Opportunities.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(bool Ok, string? Error, Customer? Customer)> WinOpportunityAsync(
        int opportunityId, string? winReason = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesUpdate);
        var deal = await FindOwnedAsync(_db.Opportunities, opportunityId, ct);
        if (deal is null) return (false, "Opportunity not found.", null);
        if (deal.Stage == "Closed Won" && deal.CustomerId is not null)
        {
            var existing = await GetCustomerAsync(deal.CustomerId.Value, ct);
            return (true, null, existing);
        }

        deal.Stage = "Closed Won";
        deal.Probability = 100;
        deal.ForecastCategory = "Closed";
        deal.ActualCloseDate = DateTime.UtcNow;
        deal.ActualRevenue ??= deal.Amount;
        deal.WinLossReason = winReason ?? deal.WinLossReason;
        deal.ExpectedRevenue = deal.Amount;

        Account? account = null;
        if (deal.AccountId is int accountId)
            account = await FindOwnedAsync(_db.CrmAccounts, accountId, ct);

        if (account is null && !string.IsNullOrWhiteSpace(deal.AccountName))
        {
            account = new Account
            {
                CompanyId = RequireCompanyId(),
                AccountOwner = deal.DealOwner,
                AccountName = deal.AccountName!,
                AccountType = "Customer",
                Territory = deal.Territory,
                CreatedDate = DateTime.UtcNow
            };
            _db.CrmAccounts.Add(account);
            await _db.SaveChangesAsync(ct);
            deal.AccountId = account.Id;
        }
        else if (account is not null)
        {
            account.AccountType = "Customer";
        }

        var partyId = deal.PartyId;
        if (partyId is null && deal.LeadId is int leadId)
        {
            var lead = await FindOwnedAsync(_db.Leads, leadId, ct);
            partyId = lead?.PartyId;
        }
        if (partyId is null && deal.ProspectId is int prospectId)
        {
            var prospect = await FindOwnedAsync(_db.Prospects, prospectId, ct);
            partyId = prospect?.PartyId;
        }
        if (partyId is null && deal.ContactId is int contactId)
        {
            var contact = await FindOwnedAsync(_db.Contacts, contactId, ct);
            partyId = contact?.PartyId;
        }

        var customer = new Customer
        {
            CompanyId = RequireCompanyId(),
            CompanyName = deal.AccountName ?? account?.AccountName ?? deal.DealName,
            PrimaryContact = deal.ContactName,
            CustomerOwner = deal.DealOwner,
            AccountId = account?.Id ?? deal.AccountId,
            Territory = deal.Territory,
            CustomerType = "Business",
            PaymentTerms = "Net 30",
            PartyId = partyId,
            CreatedDate = DateTime.UtcNow
        };
        deal.PartyId ??= partyId;
        _db.CrmCustomers.Add(customer);
        await _db.SaveChangesAsync(ct);

        deal.CustomerId = customer.Id;
        await _db.SaveChangesAsync(ct);
        return (true, null, customer);
    }

    public async Task<(bool Ok, string? Error)> LoseOpportunityAsync(
        int opportunityId, string? lossReason = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesUpdate);
        var deal = await FindOwnedAsync(_db.Opportunities, opportunityId, ct);
        if (deal is null) return (false, "Opportunity not found.");

        deal.Stage = "Closed Lost";
        deal.Probability = 0;
        deal.ForecastCategory = "Omitted";
        deal.ActualCloseDate = DateTime.UtcNow;
        deal.ExpectedRevenue = 0;
        deal.WinLossReason = lossReason ?? deal.WinLossReason ?? "Lost";
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<Opportunity?> MoveOpportunityStageAsync(int id, string stage, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesUpdate);
        var row = await FindOwnedAsync(_db.Opportunities, id, ct);
        if (row is null) return null;
        row.Stage = stage;
        ApplyStageProbability(row);
        if (stage is "Closed Won" or "Closed Lost")
            row.ActualCloseDate ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<IReadOnlyList<Customer>> ListCustomersAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCustomersView);
        return await TenantFilter(_db.CrmCustomers.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<Customer?> GetCustomerAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCustomersView);
        return await TenantFilter(_db.CrmCustomers.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCustomersCreate);
        customer.Id = 0;
        customer.CompanyId = RequireCompanyId();
        customer.CreatedDate = DateTime.UtcNow;
        // Customer remains the commercial bill-to master. PartyId is optional identity linkage only.
        // Do not invent a new Party from company name alone — that is org territory (CrmAccount).
        if (customer.PartyId is null &&
            (!string.IsNullOrWhiteSpace(customer.Email) || !string.IsNullOrWhiteSpace(customer.PhoneNumber)))
        {
            var names = (customer.PrimaryContact ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var ensure = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
            {
                CompanyId = customer.CompanyId.Value,
                FirstName = names.Length > 0 ? names[0] : customer.PrimaryContact,
                LastName = names.Length > 1 ? names[1] : null,
                DisplayName = customer.PrimaryContact,
                Phone = customer.PhoneNumber,
                Mobile = customer.PhoneNumber,
                Email = customer.Email,
                CreateReviewOnAmbiguous = true
            }, ct);
            customer.PartyId = ensure.PartyId;
        }
        _db.CrmCustomers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<Customer?> UpdateCustomerAsync(int id, Customer update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCustomersUpdate);
        var row = await FindOwnedAsync(_db.CrmCustomers, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.CompanyName = update.CompanyName;
        row.PrimaryContact = update.PrimaryContact;
        row.Email = update.Email;
        row.PhoneNumber = update.PhoneNumber;
        row.BillingAddress = update.BillingAddress;
        row.PaymentTerms = update.PaymentTerms;
        row.CreditLimit = update.CreditLimit;
        row.TaxId = update.TaxId;
        row.CustomerOwner = update.CustomerOwner;
        row.AccountNumber = update.AccountNumber;
        row.Industry = update.Industry;
        row.Website = update.Website;
        row.CustomerType = update.CustomerType;
        row.ShippingAddress = update.ShippingAddress;
        row.Rating = update.Rating;
        row.Territory = update.Territory;
        row.AccountId = update.AccountId;
        row.Tags = update.Tags;
        row.Description = update.Description;
        row.PartyId = await EnsureAndAttachPartyAsync(
            row.PartyId, update.PartyId,
            FirstNameFrom(row.PrimaryContact), LastNameFrom(row.PrimaryContact),
            row.PhoneNumber, row.PhoneNumber, row.Email, null, ct,
            displayName: row.PrimaryContact);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteCustomerAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCustomersDelete);
        var row = await FindOwnedAsync(_db.CrmCustomers, id, ct);
        if (row is null) return false;

        var companyId = RequireCompanyId();
        var hasInvoices = await _db.Invoices.AnyAsync(
            i => i.CustomerId == id
                 && i.CompanyId == companyId
                 && i.InvoiceStatus != InvoiceStatus.Void, ct);
        if (hasInvoices)
        {
            throw new InvalidOperationException(
                "Customers with invoices cannot be deleted. Void or reassign the invoices first.");
        }

        var hasOrders = await _db.SalesOrders.AnyAsync(
            o => o.CustomerId == id
                 && o.CompanyId == companyId
                 && o.OrderStatus != SalesOrderStatus.Cancelled, ct);
        if (hasOrders)
        {
            throw new InvalidOperationException(
                "Customers with sales orders cannot be deleted. Cancel or reassign the orders first.");
        }

        _db.CrmCustomers.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<CrmTask>> ListTasksAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmTasksView);
        return await TenantFilter(_db.CrmTasks.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<CrmTask?> GetTaskAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmTasksView);
        return await TenantFilter(_db.CrmTasks.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<CrmTask> CreateTaskAsync(CrmTask task, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmTasksCreate);
        task.Id = 0;
        task.CompanyId = RequireCompanyId();
        task.CreatedDate = DateTime.UtcNow;
        task.DueDate = NormalizeUtc(task.DueDate);
        if (string.IsNullOrWhiteSpace(task.Priority)) task.Priority = "Normal";
        _db.CrmTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return task;
    }

    public async Task<CrmTask?> UpdateTaskAsync(int id, CrmTask update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmTasksUpdate);
        var row = await FindOwnedAsync(_db.CrmTasks, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.TaskOwner = update.TaskOwner;
        row.Subject = update.Subject;
        row.DueDate = NormalizeUtc(update.DueDate);
        row.TaskTo = update.TaskTo;
        row.TaskType = update.TaskType;
        row.Priority = update.Priority;
        row.Reminder = update.Reminder;
        row.Repeat = update.Repeat;
        row.Description = update.Description;
        row.TaskStatus = update.TaskStatus;
        row.RelatedEntityType = update.RelatedEntityType;
        row.RelatedEntityId = update.RelatedEntityId;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteTaskAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmTasksDelete);
        var row = await FindOwnedAsync(_db.CrmTasks, id, ct);
        if (row is null) return false;
        _db.CrmTasks.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Meeting>> ListMeetingsAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmMeetingsView);
        return await TenantFilter(_db.Meetings.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<Meeting?> GetMeetingAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmMeetingsView);
        return await TenantFilter(_db.Meetings.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Meeting> CreateMeetingAsync(Meeting meeting, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmMeetingsCreate);
        meeting.Id = 0;
        meeting.CompanyId = RequireCompanyId();
        meeting.CreatedDate = DateTime.UtcNow;
        meeting.From = NormalizeUtc(meeting.From);
        meeting.To = NormalizeUtc(meeting.To);
        meeting.Reminder = NormalizeUtc(meeting.Reminder);
        meeting.ParticipantsReminder = NormalizeUtc(meeting.ParticipantsReminder);
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync(ct);
        return meeting;
    }

    public async Task<Meeting?> UpdateMeetingAsync(int id, Meeting update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmMeetingsUpdate);
        var row = await FindOwnedAsync(_db.Meetings, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.Title = update.Title;
        row.Location = update.Location;
        row.AllDay = update.AllDay;
        row.From = NormalizeUtc(update.From);
        row.To = NormalizeUtc(update.To);
        row.Host = update.Host;
        row.Participants = update.Participants;
        row.RelatedTo = update.RelatedTo;
        row.Repeat = update.Repeat;
        row.ParticipantsReminder = NormalizeUtc(update.ParticipantsReminder);
        row.Reminder = NormalizeUtc(update.Reminder);
        row.Agenda = update.Agenda;
        row.MeetingType = update.MeetingType;
        row.RelatedEntityType = update.RelatedEntityType;
        row.RelatedEntityId = update.RelatedEntityId;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteMeetingAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmMeetingsDelete);
        var row = await FindOwnedAsync(_db.Meetings, id, ct);
        if (row is null) return false;
        _db.Meetings.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<int?> EnsureAndAttachPartyAsync(
        int? existingPartyId,
        int? incomingPartyId,
        string? firstName,
        string? lastName,
        string? phone,
        string? mobile,
        string? email,
        string? secondaryEmail,
        CancellationToken ct,
        string? displayName = null)
    {
        if (incomingPartyId is not null)
            return incomingPartyId;

        if (existingPartyId is int partyId)
        {
            await _parties.AttachContactChannelsAsync(partyId, phone, mobile, email, secondaryEmail, ct);
            return partyId;
        }

        if (string.IsNullOrWhiteSpace(phone)
            && string.IsNullOrWhiteSpace(mobile)
            && string.IsNullOrWhiteSpace(email))
        {
            return existingPartyId;
        }

        var ensure = await _parties.EnsurePersonPartyAsync(new EnsurePartyRequest
        {
            CompanyId = RequireCompanyId(),
            FirstName = firstName,
            LastName = lastName,
            DisplayName = displayName,
            Phone = phone,
            Mobile = mobile,
            Email = email,
            SecondaryEmail = secondaryEmail,
            CreateReviewOnAmbiguous = true
        }, ct);
        return ensure.PartyId;
    }

    private async Task<int?> ResolveRelatedPartyIdAsync(Opportunity deal, CancellationToken ct)
    {
        if (deal.PartyId is not null) return deal.PartyId;
        if (deal.LeadId is int leadId)
        {
            var lead = await FindOwnedAsync(_db.Leads, leadId, ct);
            if (lead?.PartyId is int id) return id;
        }
        if (deal.ProspectId is int prospectId)
        {
            var prospect = await FindOwnedAsync(_db.Prospects, prospectId, ct);
            if (prospect?.PartyId is int id) return id;
        }
        if (deal.ContactId is int contactId)
        {
            var contact = await FindOwnedAsync(_db.Contacts, contactId, ct);
            if (contact?.PartyId is int id) return id;
        }
        if (deal.CustomerId is int customerId)
        {
            var customer = await FindOwnedAsync(_db.CrmCustomers, customerId, ct);
            if (customer?.PartyId is int id) return id;
        }
        return null;
    }

    private static string? FirstNameFrom(string? primaryContact)
    {
        var names = (primaryContact ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return names.Length > 0 ? names[0] : primaryContact;
    }

    private static string? LastNameFrom(string? primaryContact)
    {
        var names = (primaryContact ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return names.Length > 1 ? names[1] : null;
    }

    private void EnsureCrmAccess(string permission)
    {
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Crm) || !_modules.HasPermission(_tenant, permission))
        {
            throw new UnauthorizedAccessException("CRM access denied.");
        }
    }

    private int RequireCompanyId()
        => _tenant.CompanyId ?? throw new InvalidOperationException("Tenant context is required.");

    private IQueryable<T> TenantFilter<T>(IQueryable<T> query) where T : class
    {
        var companyId = RequireCompanyId();
        return query.Where(e => EF.Property<int?>(e, "CompanyId") == companyId);
    }

    private async Task<T?> FindOwnedAsync<T>(DbSet<T> set, int id, CancellationToken ct) where T : class
    {
        var companyId = RequireCompanyId();
        return await set.FirstOrDefaultAsync(
            e => EF.Property<int>(e, "Id") == id && EF.Property<int?>(e, "CompanyId") == companyId, ct);
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null) return null;
        var dt = value.Value;
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
    }

    private static void ApplyStageProbability(Opportunity deal)
    {
        if (deal.Probability is not null && deal.Stage is not ("Closed Won" or "Closed Lost"
            or "Qualification" or "Needs Analysis" or "Proposal" or "Negotiation"))
        {
            // keep custom probability
        }
        else
        {
            deal.Probability = deal.Stage switch
            {
                "Qualification" => deal.Probability ?? 10,
                "Needs Analysis" => deal.Probability ?? 25,
                "Proposal" => deal.Probability ?? 50,
                "Negotiation" => deal.Probability ?? 75,
                "Closed Won" => 100,
                "Closed Lost" => 0,
                _ => deal.Probability ?? 10
            };
        }

        deal.ExpectedRevenue = deal.Amount * (decimal)((deal.Probability ?? 0) / 100.0);
        if (deal.Stage == "Closed Won" && string.IsNullOrWhiteSpace(deal.ForecastCategory))
            deal.ForecastCategory = "Closed";
        if (deal.Stage == "Closed Lost")
            deal.ForecastCategory = "Omitted";
    }
}
