using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Platform;

namespace ShaunERP.Infrastructure.Services;

public partial class CrmService
{
    // —— Accounts ——
    public async Task<IReadOnlyList<Account>> ListAccountsAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmAccountsView);
        return await TenantFilter(_db.CrmAccounts.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<Account?> GetAccountAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmAccountsView);
        return await TenantFilter(_db.CrmAccounts.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Account> CreateAccountAsync(Account account, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmAccountsCreate);
        account.Id = 0;
        account.CompanyId = RequireCompanyId();
        account.CreatedDate = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(account.AccountType)) account.AccountType = "Customer";
        _db.CrmAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public async Task<Account?> UpdateAccountAsync(int id, Account update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmAccountsUpdate);
        var row = await FindOwnedAsync(_db.CrmAccounts, id, ct);
        if (row is null) return null;

        row.CompanyId = RequireCompanyId();
        row.AccountOwner = update.AccountOwner;
        row.AccountName = update.AccountName;
        row.AccountNumber = update.AccountNumber;
        row.AccountType = update.AccountType;
        row.Industry = update.Industry;
        row.Website = update.Website;
        row.Phone = update.Phone;
        row.Fax = update.Fax;
        row.Email = update.Email;
        row.ParentAccountId = update.ParentAccountId;
        row.Ownership = update.Ownership;
        row.Employees = update.Employees;
        row.AnnualRevenue = update.AnnualRevenue;
        row.Rating = update.Rating;
        row.Territory = update.Territory;
        row.BillingStreet = update.BillingStreet;
        row.BillingCity = update.BillingCity;
        row.BillingState = update.BillingState;
        row.BillingZip = update.BillingZip;
        row.BillingCountry = update.BillingCountry;
        row.ShippingStreet = update.ShippingStreet;
        row.ShippingCity = update.ShippingCity;
        row.ShippingState = update.ShippingState;
        row.ShippingZip = update.ShippingZip;
        row.ShippingCountry = update.ShippingCountry;
        row.Description = update.Description;
        row.Tags = update.Tags;
        row.TaxId = update.TaxId;
        row.PaymentTerms = update.PaymentTerms;
        row.CreditLimit = update.CreditLimit;

        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteAccountAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmAccountsDelete);
        var row = await FindOwnedAsync(_db.CrmAccounts, id, ct);
        if (row is null) return false;
        _db.CrmAccounts.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // —— Campaigns ——
    public async Task<IReadOnlyList<Campaign>> ListCampaignsAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCampaignsView);
        return await TenantFilter(_db.Campaigns.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<Campaign?> GetCampaignAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCampaignsView);
        return await TenantFilter(_db.Campaigns.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Campaign> CreateCampaignAsync(Campaign campaign, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCampaignsCreate);
        campaign.Id = 0;
        campaign.CompanyId = RequireCompanyId();
        campaign.CreatedDate = DateTime.UtcNow;
        campaign.StartDate = NormalizeUtc(campaign.StartDate);
        campaign.EndDate = NormalizeUtc(campaign.EndDate);
        if (string.IsNullOrWhiteSpace(campaign.CampaignStatus)) campaign.CampaignStatus = "Planning";
        if (string.IsNullOrWhiteSpace(campaign.CampaignType)) campaign.CampaignType = "Email";
        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        return campaign;
    }

    public async Task<Campaign?> UpdateCampaignAsync(int id, Campaign update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCampaignsUpdate);
        var row = await FindOwnedAsync(_db.Campaigns, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.CampaignOwner = update.CampaignOwner;
        row.Name = update.Name;
        row.CampaignType = update.CampaignType;
        row.CampaignStatus = update.CampaignStatus;
        row.StartDate = NormalizeUtc(update.StartDate);
        row.EndDate = NormalizeUtc(update.EndDate);
        row.BudgetedCost = update.BudgetedCost;
        row.ActualCost = update.ActualCost;
        row.ExpectedRevenue = update.ExpectedRevenue;
        row.ExpectedResponse = update.ExpectedResponse;
        row.NumSent = update.NumSent;
        row.Description = update.Description;
        row.ParentCampaign = update.ParentCampaign;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteCampaignAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCampaignsDelete);
        var row = await FindOwnedAsync(_db.Campaigns, id, ct);
        if (row is null) return false;
        _db.Campaigns.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // —— Products ——
    public async Task<IReadOnlyList<CrmProduct>> ListProductsAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProductsView);
        return await TenantFilter(_db.CrmProducts.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<CrmProduct?> GetProductAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProductsView);
        return await TenantFilter(_db.CrmProducts.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<CrmProduct> CreateProductAsync(CrmProduct product, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProductsCreate);
        product.Id = 0;
        product.CompanyId = RequireCompanyId();
        product.CreatedDate = DateTime.UtcNow;
        product.ProductCode = (product.ProductCode ?? string.Empty).Trim();
        await EnsureUniqueProductCodeAsync(product.ProductCode, excludeId: null, ct);
        if (string.IsNullOrWhiteSpace(product.CurrencyCode)) product.CurrencyCode = "USD";
        if (string.IsNullOrWhiteSpace(product.UnitOfMeasure)) product.UnitOfMeasure = "Each";
        _db.CrmProducts.Add(product);
        await _db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<CrmProduct?> UpdateProductAsync(int id, CrmProduct update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProductsUpdate);
        var row = await FindOwnedAsync(_db.CrmProducts, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.ProductCode = (update.ProductCode ?? string.Empty).Trim();
        await EnsureUniqueProductCodeAsync(row.ProductCode, excludeId: id, ct);
        row.Name = update.Name;
        row.ProductFamily = update.ProductFamily;
        row.Category = update.Category;
        row.Description = update.Description;
        row.UnitPrice = update.UnitPrice;
        row.CurrencyCode = update.CurrencyCode;
        row.IsActive = update.IsActive;
        row.UnitOfMeasure = update.UnitOfMeasure;
        row.TaxRate = update.TaxRate;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteProductAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmProductsDelete);
        var row = await FindOwnedAsync(_db.CrmProducts, id, ct);
        if (row is null) return false;
        _db.CrmProducts.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // —— Quotes ——
    public async Task<IReadOnlyList<Quote>> ListQuotesAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmQuotesView);
        return await TenantFilter(_db.Quotes.AsNoTracking())
            .Include(q => q.Lines)
            .OrderByDescending(x => x.Id)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<Quote?> GetQuoteAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmQuotesView);
        return await TenantFilter(_db.Quotes.AsNoTracking())
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Quote> CreateQuoteAsync(Quote quote, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmQuotesCreate);
        quote.Id = 0;
        quote.CompanyId = RequireCompanyId();
        quote.CreatedDate = DateTime.UtcNow;
        quote.ValidUntil = NormalizeUtc(quote.ValidUntil);
        if (string.IsNullOrWhiteSpace(quote.QuoteStage)) quote.QuoteStage = "Draft";
        if (string.IsNullOrWhiteSpace(quote.QuoteNumber))
            quote.QuoteNumber = $"Q-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        RecalcQuoteTotals(quote);
        foreach (var line in quote.Lines)
        {
            line.Id = 0;
            line.CompanyId = quote.CompanyId;
            line.CreatedDate = DateTime.UtcNow;
            line.LineTotal = ComputeLineTotal(line);
        }
        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(ct);
        return quote;
    }

    public async Task<Quote?> UpdateQuoteAsync(int id, Quote update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmQuotesUpdate);
        var row = await TenantFilter(_db.Quotes).Include(q => q.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return null;

        row.CompanyId = RequireCompanyId();
        row.QuoteOwner = update.QuoteOwner;
        row.QuoteNumber = update.QuoteNumber;
        row.Subject = update.Subject;
        row.QuoteStage = update.QuoteStage;
        row.ValidUntil = NormalizeUtc(update.ValidUntil);
        row.AccountName = update.AccountName;
        row.ContactName = update.ContactName;
        row.AccountId = update.AccountId;
        row.OpportunityId = update.OpportunityId;
        row.ContactId = update.ContactId;
        row.CustomerId = update.CustomerId;
        row.Discount = update.Discount;
        row.Tax = update.Tax;
        row.CurrencyCode = update.CurrencyCode;
        row.Terms = update.Terms;
        row.Description = update.Description;

        _db.QuoteLines.RemoveRange(row.Lines);
        row.Lines.Clear();
        foreach (var line in update.Lines ?? [])
        {
            row.Lines.Add(new QuoteLine
            {
                CompanyId = row.CompanyId,
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Discount = line.Discount,
                LineTotal = ComputeLineTotal(line),
                SortOrder = line.SortOrder,
                CreatedDate = DateTime.UtcNow
            });
        }

        RecalcQuoteTotals(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteQuoteAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmQuotesDelete);
        var row = await FindOwnedAsync(_db.Quotes, id, ct);
        if (row is null) return false;
        _db.Quotes.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // —— Cases ——
    public async Task<IReadOnlyList<CrmCase>> ListCasesAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCasesView);
        return await TenantFilter(_db.CrmCases.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<CrmCase?> GetCaseAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCasesView);
        return await TenantFilter(_db.CrmCases.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<CrmCase> CreateCaseAsync(CrmCase crmCase, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCasesCreate);
        crmCase.Id = 0;
        crmCase.CompanyId = RequireCompanyId();
        crmCase.CreatedDate = DateTime.UtcNow;
        crmCase.ClosedDate = NormalizeUtc(crmCase.ClosedDate);
        if (string.IsNullOrWhiteSpace(crmCase.CaseStatus)) crmCase.CaseStatus = "New";
        if (string.IsNullOrWhiteSpace(crmCase.Priority)) crmCase.Priority = "Medium";
        if (string.IsNullOrWhiteSpace(crmCase.CaseNumber))
            crmCase.CaseNumber = $"CAS-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        _db.CrmCases.Add(crmCase);
        await _db.SaveChangesAsync(ct);
        return crmCase;
    }

    public async Task<CrmCase?> UpdateCaseAsync(int id, CrmCase update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCasesUpdate);
        var row = await FindOwnedAsync(_db.CrmCases, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.CaseOwner = update.CaseOwner;
        row.CaseNumber = update.CaseNumber;
        row.Subject = update.Subject;
        row.CaseType = update.CaseType;
        row.Priority = update.Priority;
        row.CaseStatus = update.CaseStatus;
        row.Origin = update.Origin;
        row.Reason = update.Reason;
        row.Description = update.Description;
        row.Resolution = update.Resolution;
        row.ClosedDate = NormalizeUtc(update.ClosedDate);
        row.AccountName = update.AccountName;
        row.ContactName = update.ContactName;
        row.AccountId = update.AccountId;
        row.ContactId = update.ContactId;
        row.CustomerId = update.CustomerId;
        row.Tags = update.Tags;
        if (row.CaseStatus is "Closed" or "Resolved" && row.ClosedDate is null)
            row.ClosedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteCaseAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCasesDelete);
        var row = await FindOwnedAsync(_db.CrmCases, id, ct);
        if (row is null) return false;
        _db.CrmCases.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // —— Calls ——
    public async Task<IReadOnlyList<CrmCall>> ListCallsAsync(CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCallsView);
        return await TenantFilter(_db.CrmCalls.AsNoTracking()).OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<CrmCall?> GetCallAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCallsView);
        return await TenantFilter(_db.CrmCalls.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<CrmCall> CreateCallAsync(CrmCall call, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCallsCreate);
        call.Id = 0;
        call.CompanyId = RequireCompanyId();
        call.CreatedDate = DateTime.UtcNow;
        call.CallStart = NormalizeUtc(call.CallStart) ?? DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(call.CallType)) call.CallType = "Outbound";
        _db.CrmCalls.Add(call);
        await _db.SaveChangesAsync(ct);
        return call;
    }

    public async Task<CrmCall?> UpdateCallAsync(int id, CrmCall update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCallsUpdate);
        var row = await FindOwnedAsync(_db.CrmCalls, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.CallOwner = update.CallOwner;
        row.Subject = update.Subject;
        row.CallType = update.CallType;
        row.CallPurpose = update.CallPurpose;
        row.CallStart = NormalizeUtc(update.CallStart);
        row.DurationMinutes = update.DurationMinutes;
        row.CallResult = update.CallResult;
        row.Description = update.Description;
        row.RelatedEntityType = update.RelatedEntityType;
        row.RelatedEntityId = update.RelatedEntityId;
        row.ContactName = update.ContactName;
        row.PhoneNumber = update.PhoneNumber;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteCallAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmCallsDelete);
        var row = await FindOwnedAsync(_db.CrmCalls, id, ct);
        if (row is null) return false;
        _db.CrmCalls.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // —— Notes ——
    public async Task<IReadOnlyList<CrmNote>> ListNotesAsync(
        string? relatedType = null, int? relatedId = null, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmNotesView);
        var q = TenantFilter(_db.CrmNotes.AsNoTracking()).AsQueryable();
        if (!string.IsNullOrWhiteSpace(relatedType))
            q = q.Where(n => n.RelatedEntityType == relatedType);
        if (relatedId is int rid)
            q = q.Where(n => n.RelatedEntityId == rid);
        return await q.OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<CrmNote?> GetNoteAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmNotesView);
        return await TenantFilter(_db.CrmNotes.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<CrmNote> CreateNoteAsync(CrmNote note, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmNotesCreate);
        note.Id = 0;
        note.CompanyId = RequireCompanyId();
        note.CreatedDate = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(note.RelatedEntityType)) note.RelatedEntityType = CrmRelatedTypes.Lead;
        _db.CrmNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        return note;
    }

    public async Task<CrmNote?> UpdateNoteAsync(int id, CrmNote update, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmNotesUpdate);
        var row = await FindOwnedAsync(_db.CrmNotes, id, ct);
        if (row is null) return null;
        row.CompanyId = RequireCompanyId();
        row.NoteOwner = update.NoteOwner;
        row.Title = update.Title;
        row.Body = update.Body;
        row.IsPinned = update.IsPinned;
        row.RelatedEntityType = update.RelatedEntityType;
        row.RelatedEntityId = update.RelatedEntityId;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteNoteAsync(int id, CancellationToken ct = default)
    {
        EnsureCrmAccess(Permissions.CrmNotesDelete);
        var row = await FindOwnedAsync(_db.CrmNotes, id, ct);
        if (row is null) return false;
        _db.CrmNotes.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task EnsureUniqueProductCodeAsync(string code, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Product code is required.");

        var normalized = code.Trim().ToLower();
        var duplicate = await TenantFilter(_db.CrmProducts)
            .AnyAsync(p => p.ProductCode.ToLower() == normalized
                           && (excludeId == null || p.Id != excludeId), ct);
        if (duplicate)
            throw new InvalidOperationException($"Product code '{code.Trim()}' already exists.");
    }

    private static decimal ComputeLineTotal(QuoteLine line)
        => Math.Max(0, line.Quantity * line.UnitPrice - line.Discount);

    private static void RecalcQuoteTotals(Quote quote)
    {
        quote.Subtotal = quote.Lines.Sum(ComputeLineTotal);
        quote.GrandTotal = Math.Max(0, quote.Subtotal - quote.Discount + quote.Tax);
    }
}
