using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services;

public partial class CrmService
{
    public Task<PagedResult<Lead>> QueryLeadsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmLeadsView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.Leads.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.FirstName != null && EF.Functions.ILike(x.FirstName, pattern)) ||
                (x.LastName != null && EF.Functions.ILike(x.LastName, pattern)) ||
                (x.CompanyName != null && EF.Functions.ILike(x.CompanyName, pattern)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
                (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)) ||
                (x.Mobile != null && EF.Functions.ILike(x.Mobile, pattern)) ||
                (x.LeadStatus != null && EF.Functions.ILike(x.LeadStatus, pattern)) ||
                (x.LeadSource != null && EF.Functions.ILike(x.LeadSource, pattern)) ||
                (x.LeadOwner != null && EF.Functions.ILike(x.LeadOwner, pattern)) ||
                (x.Industry != null && EF.Functions.ILike(x.Industry, pattern)) ||
                (x.City != null && EF.Functions.ILike(x.City, pattern)) ||
                (x.State != null && EF.Functions.ILike(x.State, pattern)) ||
                (x.Country != null && EF.Functions.ILike(x.Country, pattern)) ||
                (x.Tags != null && EF.Functions.ILike(x.Tags, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<Prospect>> QueryProspectsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProspectsView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.Prospects.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.FirstName != null && EF.Functions.ILike(x.FirstName, pattern)) ||
                (x.LastName != null && EF.Functions.ILike(x.LastName, pattern)) ||
                (x.CompanyName != null && EF.Functions.ILike(x.CompanyName, pattern)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
                (x.PhoneNumber != null && EF.Functions.ILike(x.PhoneNumber, pattern)) ||
                (x.Stage != null && EF.Functions.ILike(x.Stage, pattern)) ||
                (x.Source != null && EF.Functions.ILike(x.Source, pattern)) ||
                (x.ProspectOwner != null && EF.Functions.ILike(x.ProspectOwner, pattern)) ||
                (x.Industry != null && EF.Functions.ILike(x.Industry, pattern)) ||
                (x.Territory != null && EF.Functions.ILike(x.Territory, pattern)) ||
                (x.Tags != null && EF.Functions.ILike(x.Tags, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<Account>> QueryAccountsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmAccountsView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.CrmAccounts.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.AccountName != null && EF.Functions.ILike(x.AccountName, pattern)) ||
                (x.AccountNumber != null && EF.Functions.ILike(x.AccountNumber, pattern)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
                (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)) ||
                (x.Industry != null && EF.Functions.ILike(x.Industry, pattern)) ||
                (x.AccountOwner != null && EF.Functions.ILike(x.AccountOwner, pattern)) ||
                (x.TaxId != null && EF.Functions.ILike(x.TaxId, pattern)) ||
                (x.Website != null && EF.Functions.ILike(x.Website, pattern)) ||
                (x.BillingCity != null && EF.Functions.ILike(x.BillingCity, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<Contact>> QueryContactsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmContactsView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.Contacts.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.FirstName != null && EF.Functions.ILike(x.FirstName, pattern)) ||
                (x.LastName != null && EF.Functions.ILike(x.LastName, pattern)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
                (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)) ||
                (x.Mobile != null && EF.Functions.ILike(x.Mobile, pattern)) ||
                (x.AccountName != null && EF.Functions.ILike(x.AccountName, pattern)) ||
                (x.Title != null && EF.Functions.ILike(x.Title, pattern)) ||
                (x.Department != null && EF.Functions.ILike(x.Department, pattern)) ||
                (x.ContactOwner != null && EF.Functions.ILike(x.ContactOwner, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<Opportunity>> QueryOpportunitiesAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmOpportunitiesView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.Opportunities.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.DealName != null && EF.Functions.ILike(x.DealName, pattern)) ||
                (x.AccountName != null && EF.Functions.ILike(x.AccountName, pattern)) ||
                (x.ContactName != null && EF.Functions.ILike(x.ContactName, pattern)) ||
                (x.DealOwner != null && EF.Functions.ILike(x.DealOwner, pattern)) ||
                (x.Stage != null && EF.Functions.ILike(x.Stage, pattern)) ||
                (x.Pipeline != null && EF.Functions.ILike(x.Pipeline, pattern)) ||
                (x.LeadSource != null && EF.Functions.ILike(x.LeadSource, pattern)) ||
                (x.Type != null && EF.Functions.ILike(x.Type, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<Customer>> QueryCustomersAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCustomersView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.CrmCustomers.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.CompanyName != null && EF.Functions.ILike(x.CompanyName, pattern)) ||
                (x.PrimaryContact != null && EF.Functions.ILike(x.PrimaryContact, pattern)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
                (x.PhoneNumber != null && EF.Functions.ILike(x.PhoneNumber, pattern)) ||
                (x.AccountNumber != null && EF.Functions.ILike(x.AccountNumber, pattern)) ||
                (x.CustomerOwner != null && EF.Functions.ILike(x.CustomerOwner, pattern)) ||
                (x.TaxId != null && EF.Functions.ILike(x.TaxId, pattern)) ||
                (x.Industry != null && EF.Functions.ILike(x.Industry, pattern)) ||
                (x.Territory != null && EF.Functions.ILike(x.Territory, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<Campaign>> QueryCampaignsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCampaignsView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.Campaigns.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.Name != null && EF.Functions.ILike(x.Name, pattern)) ||
                (x.CampaignType != null && EF.Functions.ILike(x.CampaignType, pattern)) ||
                (x.CampaignStatus != null && EF.Functions.ILike(x.CampaignStatus, pattern)) ||
                (x.CampaignOwner != null && EF.Functions.ILike(x.CampaignOwner, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<CrmProduct>> QueryProductsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProductsView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.CrmProducts.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.ProductCode != null && EF.Functions.ILike(x.ProductCode, pattern)) ||
                (x.Name != null && EF.Functions.ILike(x.Name, pattern)) ||
                (x.ProductFamily != null && EF.Functions.ILike(x.ProductFamily, pattern)) ||
                (x.Category != null && EF.Functions.ILike(x.Category, pattern)) ||
                (x.Description != null && EF.Functions.ILike(x.Description, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<Quote>> QueryQuotesAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmQuotesView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.Quotes.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.QuoteNumber != null && EF.Functions.ILike(x.QuoteNumber, pattern)) ||
                (x.Subject != null && EF.Functions.ILike(x.Subject, pattern)) ||
                (x.AccountName != null && EF.Functions.ILike(x.AccountName, pattern)) ||
                (x.ContactName != null && EF.Functions.ILike(x.ContactName, pattern)) ||
                (x.QuoteStage != null && EF.Functions.ILike(x.QuoteStage, pattern)) ||
                (x.QuoteOwner != null && EF.Functions.ILike(x.QuoteOwner, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<CrmCase>> QueryCasesAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCasesView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.CrmCases.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.CaseNumber != null && EF.Functions.ILike(x.CaseNumber, pattern)) ||
                (x.Subject != null && EF.Functions.ILike(x.Subject, pattern)) ||
                (x.AccountName != null && EF.Functions.ILike(x.AccountName, pattern)) ||
                (x.ContactName != null && EF.Functions.ILike(x.ContactName, pattern)) ||
                (x.CaseStatus != null && EF.Functions.ILike(x.CaseStatus, pattern)) ||
                (x.Priority != null && EF.Functions.ILike(x.Priority, pattern)) ||
                (x.CaseOwner != null && EF.Functions.ILike(x.CaseOwner, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<CrmTask>> QueryTasksAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmTasksView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.CrmTasks.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.Subject != null && EF.Functions.ILike(x.Subject, pattern)) ||
                (x.TaskOwner != null && EF.Functions.ILike(x.TaskOwner, pattern)) ||
                (x.TaskTo != null && EF.Functions.ILike(x.TaskTo, pattern)) ||
                (x.TaskType != null && EF.Functions.ILike(x.TaskType, pattern)) ||
                (x.Priority != null && EF.Functions.ILike(x.Priority, pattern)) ||
                (x.Description != null && EF.Functions.ILike(x.Description, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<Meeting>> QueryMeetingsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmMeetingsView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.Meetings.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.Title != null && EF.Functions.ILike(x.Title, pattern)) ||
                (x.Location != null && EF.Functions.ILike(x.Location, pattern)) ||
                (x.Host != null && EF.Functions.ILike(x.Host, pattern)) ||
                (x.Participants != null && EF.Functions.ILike(x.Participants, pattern)) ||
                (x.RelatedTo != null && EF.Functions.ILike(x.RelatedTo, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<CrmCall>> QueryCallsAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCallsView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.CrmCalls.AsNoTracking());
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.Subject != null && EF.Functions.ILike(x.Subject, pattern)) ||
                (x.ContactName != null && EF.Functions.ILike(x.ContactName, pattern)) ||
                (x.PhoneNumber != null && EF.Functions.ILike(x.PhoneNumber, pattern)) ||
                (x.CallOwner != null && EF.Functions.ILike(x.CallOwner, pattern)) ||
                (x.CallType != null && EF.Functions.ILike(x.CallType, pattern)) ||
                (x.CallResult != null && EF.Functions.ILike(x.CallResult, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }

    public Task<PagedResult<CrmNote>> QueryNotesAsync(
        ListQuery? query = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmNotesView);
        query ??= new ListQuery();
        var q = TenantFilter(_db.CrmNotes.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(relatedType))
        {
            q = q.Where(x => x.RelatedEntityType == relatedType);
        }

        if (relatedId is int rid)
        {
            q = q.Where(x => x.RelatedEntityId == rid);
        }

        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(x =>
                (x.Title != null && EF.Functions.ILike(x.Title, pattern)) ||
                (x.Body != null && EF.Functions.ILike(x.Body, pattern)) ||
                (x.NoteOwner != null && EF.Functions.ILike(x.NoteOwner, pattern)) ||
                (x.RelatedEntityType != null && EF.Functions.ILike(x.RelatedEntityType, pattern)));
        }

        return q.OrderByDescending(x => x.Id).ToPagedResultAsync(query, ct);
    }
}
