using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services;

public record AccountingDashboardStats(
    decimal CashBalance,
    decimal BankBalance,
    decimal Receivables,
    decimal Payables,
    decimal SalesMonth,
    decimal ExpensesMonth,
    decimal ProfitMonth,
    int DraftVouchers,
    int PostedVouchers,
    IReadOnlyList<AccountingVoucher> RecentVouchers);

public interface IAccountingService
{
    Task EnsureBootstrapAsync(CancellationToken ct = default);
    Task<AccountingDashboardStats> GetDashboardAsync(CancellationToken ct = default);

    Task<CompanyProfile> GetOrCreateProfileAsync(CancellationToken ct = default);
    Task<CompanyProfile> SaveProfileAsync(CompanyProfile update, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyStatutoryRegistration>> ListStatutoryAsync(CancellationToken ct = default);
    Task<CompanyStatutoryRegistration> UpsertStatutoryAsync(CompanyStatutoryRegistration row, CancellationToken ct = default);

    Task<CompanyGstSettings> GetOrCreateGstAsync(CancellationToken ct = default);
    Task<CompanyGstSettings> SaveGstAsync(CompanyGstSettings update, CancellationToken ct = default);
    Task<IReadOnlyList<GstTaxRate>> ListTaxRatesAsync(CancellationToken ct = default);
    Task<GstTaxRate> UpsertTaxRateAsync(GstTaxRate rate, CancellationToken ct = default);

    Task<IReadOnlyList<CurrencyMaster>> ListCurrenciesAsync(CancellationToken ct = default);
    Task<CurrencyMaster> UpsertCurrencyAsync(CurrencyMaster currency, CancellationToken ct = default);

    Task<IReadOnlyList<FinancialYear>> ListFinancialYearsAsync(CancellationToken ct = default);
    Task<FinancialYear?> GetCurrentFinancialYearAsync(CancellationToken ct = default);
    Task<FinancialYear> CreateFinancialYearAsync(FinancialYear year, CancellationToken ct = default);
    Task SetCurrentFinancialYearAsync(int id, CancellationToken ct = default);
    Task CloseFinancialYearAsync(int id, CancellationToken ct = default);
    Task ReopenFinancialYearAsync(int id, CancellationToken ct = default);

    Task<AccountingSettings> GetOrCreateSettingsAsync(CancellationToken ct = default);
    Task<AccountingSettings> SaveSettingsAsync(AccountingSettings update, CancellationToken ct = default);
    Task<AccountingPeriodLock?> GetPeriodLockAsync(CancellationToken ct = default);
    Task<AccountingPeriodLock> SetPeriodLockAsync(DateTime lockedThrough, string? reason, CancellationToken ct = default);

    Task<IReadOnlyList<AccountGroup>> ListGroupsAsync(CancellationToken ct = default);
    Task<AccountGroup> UpsertGroupAsync(AccountGroup group, CancellationToken ct = default);

    Task<IReadOnlyList<Ledger>> ListLedgersAsync(string? search = null, CancellationToken ct = default);
    Task<PagedResult<Ledger>> QueryLedgersAsync(ListQuery? query = null, CancellationToken ct = default);
    Task<Ledger?> GetLedgerAsync(int id, CancellationToken ct = default);
    Task<Ledger> UpsertLedgerAsync(Ledger ledger, CancellationToken ct = default);
    Task<Ledger> EnsureCustomerLedgerAsync(Customer customer, CancellationToken ct = default);

    Task<IReadOnlyList<VoucherType>> ListVoucherTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccountingVoucher>> ListVouchersAsync(string? typeCode = null, CancellationToken ct = default);
    Task<PagedResult<AccountingVoucher>> QueryVouchersAsync(ListQuery? query = null, string? typeCode = null, CancellationToken ct = default);
    Task<AccountingVoucher?> GetVoucherAsync(int id, CancellationToken ct = default);
    Task DeleteDraftVoucherAsync(int id, CancellationToken ct = default);

    Task<AccountingVoucher> CreateOpeningBalancesAsync(
        IReadOnlyList<AccountingPostingLineRequest> lines, string? narration = null, CancellationToken ct = default);
}

public sealed class AccountingService : IAccountingService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;
    private readonly IAccountingPostingService _posting;
    private readonly IAuditService _audit;

    public AccountingService(
        ShaunErpDbContext db,
        ITenantContext tenant,
        IModuleRegistry modules,
        IAccountingPostingService posting,
        IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
        _posting = posting;
        _audit = audit;
    }

    public async Task EnsureBootstrapAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingAccess);
        var companyId = RequireCompanyId();
        if (await _db.AccountGroups.AnyAsync(g => g.CompanyId == companyId && g.IsSystemGroup, ct))
        {
            await EnsureProfileInternalAsync(companyId, ct);
            await EnsureGstInternalAsync(companyId, ct);
            await EnsureSettingsInternalAsync(companyId, ct);
            return;
        }

        await BootstrapCompanyAsync(companyId, ct);
    }

    public async Task<AccountingDashboardStats> GetDashboardAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingView);
        await EnsureBootstrapAsync(ct);
        var companyId = RequireCompanyId();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var postedLines = await _db.AccountingVoucherLines.AsNoTracking()
            .Include(l => l.Ledger)
            .Include(l => l.Voucher)
            .Where(l => l.CompanyId == companyId
                        && l.Voucher!.VoucherStatus == VoucherStatus.Posted)
            .ToListAsync(ct);

        decimal Balance(Func<Ledger, bool> pred)
        {
            var rows = postedLines.Where(l => l.Ledger is not null && pred(l.Ledger));
            return rows.Sum(l => l.DebitAmount - l.CreditAmount);
        }

        var cash = Balance(l => l.IsCashLedger || l.SystemCode == SystemLedgerCodes.Cash);
        var bank = Balance(l => l.IsBankLedger || l.SystemCode == SystemLedgerCodes.BankDefault);
        var ar = Balance(l => l.AccountType == AccountNature.Asset
                              && (l.SystemCode == SystemLedgerCodes.AccountsReceivable || l.CustomerId is not null));
        var ap = -Balance(l => l.AccountType == AccountNature.Liability
                               && (l.SystemCode == SystemLedgerCodes.AccountsPayable || l.SupplierId is not null));

        var monthLines = postedLines.Where(l => l.Voucher!.VoucherDate >= monthStart).ToList();
        var sales = monthLines.Where(l => l.Ledger!.AccountType == AccountNature.Income).Sum(l => l.CreditAmount - l.DebitAmount);
        var expenses = monthLines.Where(l => l.Ledger!.AccountType == AccountNature.Expense).Sum(l => l.DebitAmount - l.CreditAmount);

        var vouchers = await _db.AccountingVouchers.AsNoTracking()
            .Include(v => v.VoucherType)
            .Where(v => v.CompanyId == companyId)
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id)
            .Take(8)
            .ToListAsync(ct);

        return new AccountingDashboardStats(
            cash, bank, ar, ap, sales, expenses, sales - expenses,
            vouchers.Count(v => v.VoucherStatus == VoucherStatus.Draft),
            await _db.AccountingVouchers.CountAsync(v => v.CompanyId == companyId && v.VoucherStatus == VoucherStatus.Posted, ct),
            vouchers);
    }

    public async Task<CompanyProfile> GetOrCreateProfileAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CompanySettingsView);
        return await EnsureProfileInternalAsync(RequireCompanyId(), ct);
    }

    private async Task<CompanyProfile> EnsureProfileInternalAsync(int companyId, CancellationToken ct)
    {
        var profile = await _db.CompanyProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
        if (profile is not null) return profile;

        profile = new CompanyProfile
        {
            CompanyId = companyId,
            LegalName = _tenant.CompanyName ?? "Company",
            TradingName = _tenant.CompanyName,
            Country = "India",
            CreatedDate = DateTime.UtcNow
        };
        _db.CompanyProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<CompanyProfile> SaveProfileAsync(CompanyProfile update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CompanySettingsEdit);
        var profile = await GetOrCreateProfileAsync(ct);
        profile.LegalName = update.LegalName.Trim();
        profile.TradingName = update.TradingName;
        profile.LogoUrl = update.LogoUrl;
        profile.AddressLine1 = update.AddressLine1;
        profile.AddressLine2 = update.AddressLine2;
        profile.City = update.City;
        profile.District = update.District;
        profile.State = update.State;
        profile.Country = update.Country;
        profile.PostalCode = update.PostalCode;
        profile.Phone = update.Phone;
        profile.Mobile = update.Mobile;
        profile.Email = update.Email;
        profile.Website = update.Website;
        profile.PrimaryGstin = update.PrimaryGstin;
        profile.PrimaryPan = update.PrimaryPan;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("company.profile.updated", RequireCompanyId(), _tenant.UserId,
            nameof(CompanyProfile), profile.Id.ToString(), ct: ct);
        return profile;
    }

    public async Task<IReadOnlyList<CompanyStatutoryRegistration>> ListStatutoryAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CompanySettingsView);
        var companyId = RequireCompanyId();
        return await _db.CompanyStatutoryRegistrations.AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.RegistrationType)
            .ToListAsync(ct);
    }

    public async Task<CompanyStatutoryRegistration> UpsertStatutoryAsync(CompanyStatutoryRegistration row, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.CompanySettingsEdit);
        var companyId = RequireCompanyId();
        CompanyStatutoryRegistration entity;
        if (row.Id > 0)
        {
            entity = await _db.CompanyStatutoryRegistrations.FirstAsync(s => s.Id == row.Id && s.CompanyId == companyId, ct);
        }
        else
        {
            entity = new CompanyStatutoryRegistration { CompanyId = companyId, CreatedDate = DateTime.UtcNow };
            _db.CompanyStatutoryRegistrations.Add(entity);
        }

        entity.RegistrationType = row.RegistrationType.Trim();
        entity.RegistrationNumber = row.RegistrationNumber.Trim();
        entity.RegistrationDate = row.RegistrationDate;
        entity.ExpiryDate = row.ExpiryDate;
        entity.IssuingAuthority = row.IssuingAuthority;
        entity.State = row.State;
        entity.Notes = row.Notes;
        entity.IsActive = row.IsActive;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<CompanyGstSettings> GetOrCreateGstAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.GstSettingsManage);
        return await EnsureGstInternalAsync(RequireCompanyId(), ct);
    }

    private async Task<CompanyGstSettings> EnsureGstInternalAsync(int companyId, CancellationToken ct)
    {
        var gst = await _db.CompanyGstSettings.FirstOrDefaultAsync(g => g.CompanyId == companyId, ct);
        if (gst is not null) return gst;
        gst = new CompanyGstSettings
        {
            CompanyId = companyId,
            GstRegistered = false,
            RegistrationType = GstRegistrationType.Unregistered,
            CreatedDate = DateTime.UtcNow
        };
        _db.CompanyGstSettings.Add(gst);
        await _db.SaveChangesAsync(ct);
        return gst;
    }

    public async Task<CompanyGstSettings> SaveGstAsync(CompanyGstSettings update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.GstSettingsManage);
        var gst = await GetOrCreateGstAsync(ct);
        gst.GstRegistered = update.GstRegistered;
        gst.Gstin = update.Gstin;
        gst.RegistrationType = update.RegistrationType;
        gst.State = update.State;
        gst.StateCode = update.StateCode;
        gst.CompositionScheme = update.CompositionScheme;
        gst.ReverseChargeApplicable = update.ReverseChargeApplicable;
        gst.DefaultTaxBehaviour = update.DefaultTaxBehaviour;
        gst.GstEffectiveDate = update.GstEffectiveDate;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("gst.settings.updated", RequireCompanyId(), _tenant.UserId,
            nameof(CompanyGstSettings), gst.Id.ToString(), ct: ct);
        return gst;
    }

    public async Task<IReadOnlyList<GstTaxRate>> ListTaxRatesAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.GstSettingsManage);
        var companyId = RequireCompanyId();
        return await _db.GstTaxRates.AsNoTracking()
            .Where(t => t.CompanyId == companyId)
            .OrderBy(t => t.Percentage).ThenBy(t => t.TaxName)
            .ToListAsync(ct);
    }

    public async Task<GstTaxRate> UpsertTaxRateAsync(GstTaxRate rate, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.GstSettingsManage);
        var companyId = RequireCompanyId();
        GstTaxRate entity;
        if (rate.Id > 0)
        {
            entity = await _db.GstTaxRates.FirstAsync(t => t.Id == rate.Id && t.CompanyId == companyId, ct);
        }
        else
        {
            entity = new GstTaxRate { CompanyId = companyId, CreatedDate = DateTime.UtcNow };
            _db.GstTaxRates.Add(entity);
        }

        entity.TaxName = rate.TaxName.Trim();
        entity.Percentage = rate.Percentage;
        entity.TaxType = rate.TaxType;
        entity.EffectiveFrom = rate.EffectiveFrom;
        entity.EffectiveTo = rate.EffectiveTo;
        entity.IsActive = rate.IsActive;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<IReadOnlyList<CurrencyMaster>> ListCurrenciesAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingSettingsManage);
        var companyId = RequireCompanyId();
        return await _db.Currencies.AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderByDescending(c => c.IsBaseCurrency).ThenBy(c => c.CurrencyCode)
            .ToListAsync(ct);
    }

    public async Task<CurrencyMaster> UpsertCurrencyAsync(CurrencyMaster currency, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingSettingsManage);
        var companyId = RequireCompanyId();
        CurrencyMaster entity;
        if (currency.Id > 0)
        {
            entity = await _db.Currencies.FirstAsync(c => c.Id == currency.Id && c.CompanyId == companyId, ct);
        }
        else
        {
            entity = new CurrencyMaster { CompanyId = companyId, CreatedDate = DateTime.UtcNow };
            _db.Currencies.Add(entity);
        }

        entity.CurrencyCode = currency.CurrencyCode.Trim().ToUpperInvariant();
        entity.CurrencyName = currency.CurrencyName.Trim();
        entity.Symbol = currency.Symbol;
        entity.DecimalPlaces = currency.DecimalPlaces;
        entity.DecimalSeparator = currency.DecimalSeparator;
        entity.ThousandSeparator = currency.ThousandSeparator;
        entity.IsActive = currency.IsActive;

        if (currency.IsBaseCurrency)
        {
            var others = await _db.Currencies.Where(c => c.CompanyId == companyId && c.Id != entity.Id).ToListAsync(ct);
            foreach (var o in others) o.IsBaseCurrency = false;
            entity.IsBaseCurrency = true;
        }

        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<IReadOnlyList<FinancialYear>> ListFinancialYearsAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingFinancialYearManage);
        var companyId = RequireCompanyId();
        return await _db.FinancialYears.AsNoTracking()
            .Where(y => y.CompanyId == companyId)
            .OrderByDescending(y => y.StartDate)
            .ToListAsync(ct);
    }

    public async Task<FinancialYear?> GetCurrentFinancialYearAsync(CancellationToken ct = default)
    {
        var companyId = RequireCompanyId();
        return await _db.FinancialYears.AsNoTracking()
            .FirstOrDefaultAsync(y => y.CompanyId == companyId && y.IsCurrent, ct);
    }

    public async Task<FinancialYear> CreateFinancialYearAsync(FinancialYear year, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingFinancialYearManage);
        var companyId = RequireCompanyId();
        if (year.EndDate <= year.StartDate)
            throw new InvalidOperationException("Financial year end date must be after start date.");

        var entity = new FinancialYear
        {
            CompanyId = companyId,
            Name = year.Name.Trim(),
            StartDate = year.StartDate.Date,
            EndDate = year.EndDate.Date,
            IsCurrent = year.IsCurrent,
            CreatedDate = DateTime.UtcNow
        };
        if (entity.IsCurrent)
        {
            var others = await _db.FinancialYears.Where(y => y.CompanyId == companyId).ToListAsync(ct);
            foreach (var o in others) o.IsCurrent = false;
        }

        _db.FinancialYears.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task SetCurrentFinancialYearAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingFinancialYearManage);
        var companyId = RequireCompanyId();
        var years = await _db.FinancialYears.Where(y => y.CompanyId == companyId).ToListAsync(ct);
        var target = years.FirstOrDefault(y => y.Id == id)
                     ?? throw new InvalidOperationException("Financial year not found.");
        foreach (var y in years) y.IsCurrent = y.Id == id;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("accounting.fy.current", companyId, _tenant.UserId,
            nameof(FinancialYear), target.Id.ToString(), ct: ct);
    }

    public async Task CloseFinancialYearAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingFinancialYearManage);
        var companyId = RequireCompanyId();
        var year = await _db.FinancialYears.FirstOrDefaultAsync(y => y.Id == id && y.CompanyId == companyId, ct)
                   ?? throw new InvalidOperationException("Financial year not found.");
        year.IsClosed = true;
        year.ClosedDate = DateTime.UtcNow;
        year.ClosedBy = _tenant.UserId?.ToString();
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("accounting.fy.closed", companyId, _tenant.UserId,
            nameof(FinancialYear), year.Id.ToString(), ct: ct);
    }

    public async Task ReopenFinancialYearAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingFinancialYearManage);
        var companyId = RequireCompanyId();
        var year = await _db.FinancialYears.FirstOrDefaultAsync(y => y.Id == id && y.CompanyId == companyId, ct)
                   ?? throw new InvalidOperationException("Financial year not found.");
        year.IsClosed = false;
        year.ClosedDate = null;
        year.ClosedBy = null;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("accounting.fy.reopened", companyId, _tenant.UserId,
            nameof(FinancialYear), year.Id.ToString(), ct: ct);
    }

    public async Task<AccountingSettings> GetOrCreateSettingsAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingSettingsManage);
        return await EnsureSettingsInternalAsync(RequireCompanyId(), ct);
    }

    private async Task<AccountingSettings> EnsureSettingsInternalAsync(int companyId, CancellationToken ct)
    {
        var settings = await _db.AccountingSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (settings is not null) return settings;
        settings = new AccountingSettings { CompanyId = companyId, CreatedDate = DateTime.UtcNow };
        _db.AccountingSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<AccountingSettings> SaveSettingsAsync(AccountingSettings update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingSettingsManage);
        var settings = await GetOrCreateSettingsAsync(ct);
        settings.InventoryEnabled = update.InventoryEnabled;
        settings.ValuationMethod = update.ValuationMethod;
        settings.NegativeStockAllowed = update.NegativeStockAllowed;
        settings.StockAccountingEnabled = update.StockAccountingEnabled;
        settings.AutoCreateCustomerLedger = update.AutoCreateCustomerLedger;
        settings.AutoCreateSupplierLedger = update.AutoCreateSupplierLedger;
        settings.AutoPostSalesInvoice = update.AutoPostSalesInvoice;
        settings.DefaultCashLedgerCode = update.DefaultCashLedgerCode;
        settings.DefaultBankLedgerCode = update.DefaultBankLedgerCode;
        settings.DefaultSalesLedgerCode = update.DefaultSalesLedgerCode;
        settings.DefaultArLedgerCode = update.DefaultArLedgerCode;
        settings.DefaultApLedgerCode = update.DefaultApLedgerCode;
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<AccountingPeriodLock?> GetPeriodLockAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingPeriodLockManage);
        var companyId = RequireCompanyId();
        return await _db.AccountingPeriodLocks.AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .OrderByDescending(l => l.LockedThroughDate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<AccountingPeriodLock> SetPeriodLockAsync(DateTime lockedThrough, string? reason, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingPeriodLockManage);
        var companyId = RequireCompanyId();
        var row = new AccountingPeriodLock
        {
            CompanyId = companyId,
            LockedThroughDate = lockedThrough.Date,
            Reason = reason,
            LockedBy = _tenant.UserId?.ToString(),
            LockedAt = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        };
        _db.AccountingPeriodLocks.Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("accounting.period.locked", companyId, _tenant.UserId,
            nameof(AccountingPeriodLock), row.Id.ToString(),
            afterValues: lockedThrough.ToString("yyyy-MM-dd"), ct: ct);
        return row;
    }

    public async Task<IReadOnlyList<AccountGroup>> ListGroupsAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingLedgersView);
        var companyId = RequireCompanyId();
        return await _db.AccountGroups.AsNoTracking()
            .Where(g => g.CompanyId == companyId)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.GroupName)
            .ToListAsync(ct);
    }

    public async Task<AccountGroup> UpsertGroupAsync(AccountGroup group, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingLedgersEdit);
        var companyId = RequireCompanyId();
        AccountGroup entity;
        if (group.Id > 0)
        {
            entity = await _db.AccountGroups.FirstAsync(g => g.Id == group.Id && g.CompanyId == companyId, ct);
            if (entity.IsSystemGroup)
            {
                entity.GroupName = group.GroupName.Trim();
                entity.IsActive = group.IsActive;
                await _db.SaveChangesAsync(ct);
                return entity;
            }
        }
        else
        {
            entity = new AccountGroup { CompanyId = companyId, CreatedDate = DateTime.UtcNow };
            _db.AccountGroups.Add(entity);
        }

        entity.ParentGroupId = group.ParentGroupId;
        entity.GroupCode = group.GroupCode.Trim().ToUpperInvariant();
        entity.GroupName = group.GroupName.Trim();
        entity.Nature = group.Nature;
        entity.ReportClassification = group.ReportClassification;
        entity.IsActive = group.IsActive;
        entity.SortOrder = group.SortOrder;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<IReadOnlyList<Ledger>> ListLedgersAsync(string? search = null, CancellationToken ct = default)
    {
        var page = await QueryLedgersAsync(new ListQuery { Search = search, Page = 1, PageSize = 200 }, ct);
        return page.Items;
    }

    public async Task<PagedResult<Ledger>> QueryLedgersAsync(ListQuery? query = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingLedgersView);
        query ??= new ListQuery();
        var companyId = RequireCompanyId();
        var q = _db.Ledgers.AsNoTracking()
            .Include(l => l.AccountGroup)
            .Where(l => l.CompanyId == companyId);
        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(l =>
                EF.Functions.ILike(l.LedgerCode, pattern) ||
                EF.Functions.ILike(l.LedgerName, pattern) ||
                (l.Gstin != null && EF.Functions.ILike(l.Gstin, pattern)) ||
                (l.Pan != null && EF.Functions.ILike(l.Pan, pattern)) ||
                (l.BankAccountNumber != null && EF.Functions.ILike(l.BankAccountNumber, pattern)));
        }

        return await q.OrderBy(l => l.LedgerName).ToPagedResultAsync(query, ct);
    }

    public async Task<Ledger?> GetLedgerAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingLedgersView);
        var companyId = RequireCompanyId();
        return await _db.Ledgers.AsNoTracking()
            .Include(l => l.AccountGroup)
            .FirstOrDefaultAsync(l => l.Id == id && l.CompanyId == companyId, ct);
    }

    public async Task<Ledger> UpsertLedgerAsync(Ledger ledger, CancellationToken ct = default)
    {
        EnsureAccess(ledger.Id > 0 ? Permissions.AccountingLedgersEdit : Permissions.AccountingLedgersCreate);
        var companyId = RequireCompanyId();
        var group = await _db.AccountGroups.FirstOrDefaultAsync(
                        g => g.Id == ledger.AccountGroupId && g.CompanyId == companyId, ct)
                    ?? throw new InvalidOperationException("Account group not found for this company.");

        Ledger entity;
        if (ledger.Id > 0)
        {
            entity = await _db.Ledgers.FirstAsync(l => l.Id == ledger.Id && l.CompanyId == companyId, ct);
            if (entity.IsSystemLedger)
            {
                entity.LedgerName = ledger.LedgerName.Trim();
                entity.IsActive = ledger.IsActive;
                entity.BankAccountNumber = ledger.BankAccountNumber;
                entity.BankIfsc = ledger.BankIfsc;
                entity.BankName = ledger.BankName;
                await _db.SaveChangesAsync(ct);
                await _audit.WriteAsync("accounting.ledger.updated", companyId, _tenant.UserId,
                    nameof(Ledger), entity.Id.ToString(), ct: ct);
                return entity;
            }
        }
        else
        {
            entity = new Ledger { CompanyId = companyId, CreatedDate = DateTime.UtcNow };
            _db.Ledgers.Add(entity);
        }

        entity.LedgerCode = ledger.LedgerCode.Trim().ToUpperInvariant();
        entity.LedgerName = ledger.LedgerName.Trim();
        entity.AccountGroupId = group.Id;
        entity.AccountType = group.Nature;
        entity.OpeningBalance = ledger.OpeningBalance;
        entity.OpeningBalanceType = ledger.OpeningBalanceType;
        entity.CurrencyId = ledger.CurrencyId;
        entity.GstApplicable = ledger.GstApplicable;
        entity.Gstin = ledger.Gstin;
        entity.Pan = ledger.Pan;
        entity.CreditLimit = ledger.CreditLimit;
        entity.CreditDays = ledger.CreditDays;
        entity.CustomerId = ledger.CustomerId;
        entity.SupplierId = ledger.SupplierId;
        entity.BankAccountNumber = ledger.BankAccountNumber;
        entity.BankIfsc = ledger.BankIfsc;
        entity.BankName = ledger.BankName;
        entity.IsCashLedger = ledger.IsCashLedger;
        entity.IsBankLedger = ledger.IsBankLedger;
        entity.IsActive = ledger.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(ledger.Id > 0 ? "accounting.ledger.updated" : "accounting.ledger.created",
            companyId, _tenant.UserId, nameof(Ledger), entity.Id.ToString(), ct: ct);
        return entity;
    }

    public async Task<Ledger> EnsureCustomerLedgerAsync(Customer customer, CancellationToken ct = default)
    {
        var companyId = RequireCompanyId();
        var existing = await _db.Ledgers.FirstOrDefaultAsync(
            l => l.CompanyId == companyId && l.CustomerId == customer.Id, ct);
        if (existing is not null) return existing;

        var arGroup = await _db.AccountGroups.FirstAsync(
            g => g.CompanyId == companyId && g.GroupCode == SystemGroupCodes.AccountsReceivable, ct);
        var code = $"AR-{customer.Id}";
        var ledger = new Ledger
        {
            CompanyId = companyId,
            LedgerCode = code,
            LedgerName = string.IsNullOrWhiteSpace(customer.CompanyName) ? $"Customer #{customer.Id}" : customer.CompanyName!,
            AccountGroupId = arGroup.Id,
            AccountType = AccountNature.Asset,
            CustomerId = customer.Id,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        _db.Ledgers.Add(ledger);
        await _db.SaveChangesAsync(ct);
        return ledger;
    }

    public async Task<IReadOnlyList<VoucherType>> ListVoucherTypesAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingSettingsManage);
        var companyId = RequireCompanyId();
        return await _db.VoucherTypes.AsNoTracking()
            .Where(v => v.CompanyId == companyId)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccountingVoucher>> ListVouchersAsync(string? typeCode = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingVouchersView);
        var companyId = RequireCompanyId();
        var q = _db.AccountingVouchers.AsNoTracking()
            .Include(v => v.VoucherType)
            .Where(v => v.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(typeCode))
            q = q.Where(v => v.VoucherType!.Code == typeCode);
        return await q.OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id).Take(500).ToListAsync(ct);
    }

    public async Task<PagedResult<AccountingVoucher>> QueryVouchersAsync(
        ListQuery? query = null,
        string? typeCode = null,
        CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingVouchersView);
        query ??= new ListQuery();
        var companyId = RequireCompanyId();
        var q = _db.AccountingVouchers.AsNoTracking()
            .Include(v => v.VoucherType)
            .Where(v => v.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(typeCode))
        {
            q = q.Where(v => v.VoucherType!.Code == typeCode);
        }

        var pattern = PagingQueryableExtensions.ToILikePattern(query.Search);
        if (pattern is not null)
        {
            q = q.Where(v =>
                EF.Functions.ILike(v.VoucherNumber, pattern) ||
                (v.ReferenceNumber != null && EF.Functions.ILike(v.ReferenceNumber, pattern)) ||
                (v.Narration != null && EF.Functions.ILike(v.Narration, pattern)) ||
                (v.SourceModule != null && EF.Functions.ILike(v.SourceModule, pattern)) ||
                (v.VoucherType != null && EF.Functions.ILike(v.VoucherType.Name, pattern)));
        }

        return await q.OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id).ToPagedResultAsync(query, ct);
    }

    public async Task<AccountingVoucher?> GetVoucherAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingVouchersView);
        var companyId = RequireCompanyId();
        return await _db.AccountingVouchers.AsNoTracking()
            .Include(v => v.VoucherType)
            .Include(v => v.FinancialYear)
            .Include(v => v.Lines).ThenInclude(l => l.Ledger)
            .FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == companyId, ct);
    }

    public async Task DeleteDraftVoucherAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingVouchersCreate);
        var companyId = RequireCompanyId();
        var voucher = await _db.AccountingVouchers
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Voucher not found.");
        if (voucher.VoucherStatus != VoucherStatus.Draft)
            throw new InvalidOperationException("Posted voucher cannot be directly deleted.");
        _db.AccountingVouchers.Remove(voucher);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AccountingVoucher> CreateOpeningBalancesAsync(
        IReadOnlyList<AccountingPostingLineRequest> lines, string? narration = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.AccountingVouchersCreate);
        await EnsureBootstrapAsync(ct);
        var fy = await GetCurrentFinancialYearAsync(ct)
                 ?? throw new InvalidOperationException("Create a current financial year first.");

        var working = lines.Select(l => new AccountingPostingLineRequest
        {
            LedgerId = l.LedgerId,
            LedgerSystemCode = l.LedgerSystemCode,
            Debit = l.Debit,
            Credit = l.Credit,
            Description = l.Description
        }).ToList();

        var debit = working.Sum(l => l.Debit);
        var credit = working.Sum(l => l.Credit);
        if (debit != credit)
        {
            var diff = Math.Abs(debit - credit);
            working.Add(new AccountingPostingLineRequest
            {
                LedgerSystemCode = SystemLedgerCodes.OpeningBalanceAdj,
                Debit = debit < credit ? diff : 0,
                Credit = credit < debit ? diff : 0,
                Description = "Opening balance adjustment"
            });
        }

        return await _posting.PostAsync(new AccountingPostingRequest
        {
            FinancialYearId = fy.Id,
            TransactionDate = fy.StartDate,
            VoucherTypeCode = SystemVoucherTypeCodes.OpeningBalance,
            Narration = narration ?? "Opening balances",
            SourceModule = AccountingSourceModules.Opening,
            SourceEntityType = "FinancialYear",
            SourceEntityId = fy.Id.ToString(),
            PostingType = "Opening",
            PostImmediately = true,
            Lines = working
        }, ct);
    }

    private async Task BootstrapCompanyAsync(int companyId, CancellationToken ct)
    {
        await EnsureProfileInternalAsync(companyId, ct);
        await EnsureGstInternalAsync(companyId, ct);
        await EnsureSettingsInternalAsync(companyId, ct);

        if (!await _db.Currencies.AnyAsync(c => c.CompanyId == companyId, ct))
        {
            _db.Currencies.AddRange(
                new CurrencyMaster { CompanyId = companyId, CurrencyCode = "INR", CurrencyName = "Indian Rupee", Symbol = "₹", IsBaseCurrency = true, CreatedDate = DateTime.UtcNow },
                new CurrencyMaster { CompanyId = companyId, CurrencyCode = "USD", CurrencyName = "US Dollar", Symbol = "$", CreatedDate = DateTime.UtcNow },
                new CurrencyMaster { CompanyId = companyId, CurrencyCode = "EUR", CurrencyName = "Euro", Symbol = "€", CreatedDate = DateTime.UtcNow },
                new CurrencyMaster { CompanyId = companyId, CurrencyCode = "GBP", CurrencyName = "British Pound", Symbol = "£", CreatedDate = DateTime.UtcNow },
                new CurrencyMaster { CompanyId = companyId, CurrencyCode = "AED", CurrencyName = "UAE Dirham", Symbol = "د.إ", CreatedDate = DateTime.UtcNow },
                new CurrencyMaster { CompanyId = companyId, CurrencyCode = "SGD", CurrencyName = "Singapore Dollar", Symbol = "S$", CreatedDate = DateTime.UtcNow });
        }

        if (!await _db.FinancialYears.AnyAsync(y => y.CompanyId == companyId, ct))
        {
            var start = new DateTime(DateTime.UtcNow.Year, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            if (DateTime.UtcNow.Date < start) start = start.AddYears(-1);
            var end = start.AddYears(1).AddDays(-1);
            _db.FinancialYears.Add(new FinancialYear
            {
                CompanyId = companyId,
                Name = $"{start.Year}-{end.Year}",
                StartDate = start,
                EndDate = end,
                IsCurrent = true,
                CreatedDate = DateTime.UtcNow
            });
        }

        if (!await _db.GstTaxRates.AnyAsync(t => t.CompanyId == companyId, ct))
        {
            foreach (var pct in new[] { 0m, 0.25m, 3m, 5m, 12m, 18m, 28m })
            {
                _db.GstTaxRates.Add(new GstTaxRate
                {
                    CompanyId = companyId,
                    TaxName = $"GST {pct}%",
                    Percentage = pct,
                    TaxType = GstTaxType.IGST,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }

        var groups = BuildSystemGroups(companyId);
        _db.AccountGroups.AddRange(groups);
        await _db.SaveChangesAsync(ct);

        var groupMap = await _db.AccountGroups.Where(g => g.CompanyId == companyId)
            .ToDictionaryAsync(g => g.GroupCode, g => g.Id, StringComparer.OrdinalIgnoreCase, ct);

        _db.Ledgers.AddRange(BuildSystemLedgers(companyId, groupMap));
        _db.VoucherTypes.AddRange(BuildVoucherTypes(companyId));
        await _db.SaveChangesAsync(ct);
    }

    private static List<AccountGroup> BuildSystemGroups(int companyId)
    {
        AccountGroup G(string code, string name, AccountNature nature, string? parent, int order, string? report = null)
            => new()
            {
                CompanyId = companyId,
                GroupCode = code,
                GroupName = name,
                Nature = nature,
                ReportClassification = report ?? nature.ToString(),
                IsSystemGroup = true,
                IsActive = true,
                SortOrder = order,
                CreatedDate = DateTime.UtcNow,
                Status = parent
            };

        // Temporary use Status to hold parent code; resolved after insert.
        var list = new List<AccountGroup>
        {
            G(SystemGroupCodes.Assets, "Assets", AccountNature.Asset, null, 10),
            G(SystemGroupCodes.CurrentAssets, "Current Assets", AccountNature.Asset, SystemGroupCodes.Assets, 11),
            G(SystemGroupCodes.FixedAssets, "Fixed Assets", AccountNature.Asset, SystemGroupCodes.Assets, 12),
            G(SystemGroupCodes.BankAccounts, "Bank Accounts", AccountNature.Asset, SystemGroupCodes.CurrentAssets, 13),
            G(SystemGroupCodes.CashInHand, "Cash-in-Hand", AccountNature.Asset, SystemGroupCodes.CurrentAssets, 14),
            G(SystemGroupCodes.AccountsReceivable, "Accounts Receivable", AccountNature.Asset, SystemGroupCodes.CurrentAssets, 15),
            G(SystemGroupCodes.Inventory, "Inventory", AccountNature.Asset, SystemGroupCodes.CurrentAssets, 16),
            G(SystemGroupCodes.Deposits, "Deposits", AccountNature.Asset, SystemGroupCodes.CurrentAssets, 17),
            G(SystemGroupCodes.LoansAdvances, "Loans & Advances", AccountNature.Asset, SystemGroupCodes.CurrentAssets, 18),
            G(SystemGroupCodes.Liabilities, "Liabilities", AccountNature.Liability, null, 20),
            G(SystemGroupCodes.CurrentLiabilities, "Current Liabilities", AccountNature.Liability, SystemGroupCodes.Liabilities, 21),
            G(SystemGroupCodes.AccountsPayable, "Accounts Payable", AccountNature.Liability, SystemGroupCodes.CurrentLiabilities, 22),
            G(SystemGroupCodes.DutiesTaxes, "Duties & Taxes", AccountNature.Liability, SystemGroupCodes.CurrentLiabilities, 23),
            G(SystemGroupCodes.Loans, "Loans", AccountNature.Liability, SystemGroupCodes.Liabilities, 24),
            G(SystemGroupCodes.Provisions, "Provisions", AccountNature.Liability, SystemGroupCodes.Liabilities, 25),
            G(SystemGroupCodes.Equity, "Equity", AccountNature.Equity, null, 30),
            G(SystemGroupCodes.Capital, "Capital", AccountNature.Equity, SystemGroupCodes.Equity, 31),
            G(SystemGroupCodes.Reserves, "Reserves & Surplus", AccountNature.Equity, SystemGroupCodes.Equity, 32),
            G(SystemGroupCodes.RetainedEarnings, "Retained Earnings", AccountNature.Equity, SystemGroupCodes.Equity, 33),
            G(SystemGroupCodes.Income, "Income", AccountNature.Income, null, 40),
            G(SystemGroupCodes.Sales, "Sales", AccountNature.Income, SystemGroupCodes.Income, 41),
            G(SystemGroupCodes.ServiceIncome, "Service Income", AccountNature.Income, SystemGroupCodes.Income, 42),
            G(SystemGroupCodes.OtherIncome, "Other Income", AccountNature.Income, SystemGroupCodes.Income, 43),
            G(SystemGroupCodes.DirectIncome, "Direct Income", AccountNature.Income, SystemGroupCodes.Income, 44),
            G(SystemGroupCodes.IndirectIncome, "Indirect Income", AccountNature.Income, SystemGroupCodes.Income, 45),
            G(SystemGroupCodes.Expenses, "Expenses", AccountNature.Expense, null, 50),
            G(SystemGroupCodes.Purchase, "Purchase", AccountNature.Expense, SystemGroupCodes.Expenses, 51),
            G(SystemGroupCodes.DirectExpenses, "Direct Expenses", AccountNature.Expense, SystemGroupCodes.Expenses, 52),
            G(SystemGroupCodes.AdminExpenses, "Administrative Expenses", AccountNature.Expense, SystemGroupCodes.Expenses, 53),
            G(SystemGroupCodes.SellingExpenses, "Selling Expenses", AccountNature.Expense, SystemGroupCodes.Expenses, 54),
            G(SystemGroupCodes.FinancialExpenses, "Financial Expenses", AccountNature.Expense, SystemGroupCodes.Expenses, 55),
            G(SystemGroupCodes.OtherExpenses, "Other Expenses", AccountNature.Expense, SystemGroupCodes.Expenses, 56)
        };

        // Persist without parents first, then wire parents in caller after save — here we save with Status as parent code
        // and fix parent ids after first SaveChanges in BootstrapCompanyAsync.
        return list;
    }

    private List<Ledger> BuildSystemLedgers(int companyId, Dictionary<string, int> groups)
    {
        // Wire parent groups from Status (parent code).
        var allGroups = _db.AccountGroups.Local.Where(g => g.CompanyId == companyId).ToList();
        foreach (var g in allGroups)
        {
            if (!string.IsNullOrWhiteSpace(g.Status) && groups.TryGetValue(g.Status, out var parentId))
            {
                g.ParentGroupId = parentId;
            }
            g.Status = "Active";
        }

        Ledger L(string code, string name, string groupCode, AccountNature nature, bool cash = false, bool bank = false)
            => new()
            {
                CompanyId = companyId,
                LedgerCode = code,
                LedgerName = name,
                AccountGroupId = groups[groupCode],
                AccountType = nature,
                SystemCode = code,
                IsSystemLedger = true,
                IsCashLedger = cash,
                IsBankLedger = bank,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

        return
        [
            L(SystemLedgerCodes.Cash, "Cash", SystemGroupCodes.CashInHand, AccountNature.Asset, cash: true),
            L(SystemLedgerCodes.BankDefault, "HDFC Bank", SystemGroupCodes.BankAccounts, AccountNature.Asset, bank: true),
            L(SystemLedgerCodes.AccountsReceivable, "Accounts Receivable", SystemGroupCodes.AccountsReceivable, AccountNature.Asset),
            L(SystemLedgerCodes.AccountsPayable, "Accounts Payable", SystemGroupCodes.AccountsPayable, AccountNature.Liability),
            L(SystemLedgerCodes.Sales, "Sales", SystemGroupCodes.Sales, AccountNature.Income),
            L(SystemLedgerCodes.Purchase, "Purchase", SystemGroupCodes.Purchase, AccountNature.Expense),
            L(SystemLedgerCodes.CgstInput, "CGST Input", SystemGroupCodes.DutiesTaxes, AccountNature.Asset),
            L(SystemLedgerCodes.SgstInput, "SGST Input", SystemGroupCodes.DutiesTaxes, AccountNature.Asset),
            L(SystemLedgerCodes.IgstInput, "IGST Input", SystemGroupCodes.DutiesTaxes, AccountNature.Asset),
            L(SystemLedgerCodes.CgstOutput, "CGST Output", SystemGroupCodes.DutiesTaxes, AccountNature.Liability),
            L(SystemLedgerCodes.SgstOutput, "SGST Output", SystemGroupCodes.DutiesTaxes, AccountNature.Liability),
            L(SystemLedgerCodes.IgstOutput, "IGST Output", SystemGroupCodes.DutiesTaxes, AccountNature.Liability),
            L(SystemLedgerCodes.RoundOff, "Round Off", SystemGroupCodes.OtherIncome, AccountNature.Income),
            L(SystemLedgerCodes.DiscountAllowed, "Discount Allowed", SystemGroupCodes.OtherExpenses, AccountNature.Expense),
            L(SystemLedgerCodes.DiscountReceived, "Discount Received", SystemGroupCodes.OtherIncome, AccountNature.Income),
            L(SystemLedgerCodes.OpeningBalanceAdj, "Opening Balance Adjustment", SystemGroupCodes.Equity, AccountNature.Equity),
            L(SystemLedgerCodes.RetainedEarnings, "Retained Earnings", SystemGroupCodes.RetainedEarnings, AccountNature.Equity),
            L(SystemLedgerCodes.Capital, "Capital", SystemGroupCodes.Capital, AccountNature.Equity),
            L(SystemLedgerCodes.Inventory, "Inventory", SystemGroupCodes.Inventory, AccountNature.Asset),
            L(SystemLedgerCodes.Cogs, "Cost of Goods Sold", SystemGroupCodes.DirectExpenses, AccountNature.Expense),
            L("RENT", "Office Rent", SystemGroupCodes.AdminExpenses, AccountNature.Expense)
        ];
    }

    private static List<VoucherType> BuildVoucherTypes(int companyId)
    {
        VoucherType V(string code, string name, string prefix) => new()
        {
            CompanyId = companyId,
            Code = code,
            Name = name,
            Prefix = prefix,
            CurrentSequence = 0,
            IsSystemVoucher = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        return
        [
            V(SystemVoucherTypeCodes.Journal, "Journal", "JV"),
            V(SystemVoucherTypeCodes.Receipt, "Receipt", "RV"),
            V(SystemVoucherTypeCodes.Payment, "Payment", "PV"),
            V(SystemVoucherTypeCodes.Contra, "Contra", "CV"),
            V(SystemVoucherTypeCodes.Sales, "Sales", "SV"),
            V(SystemVoucherTypeCodes.Purchase, "Purchase", "PUV"),
            V(SystemVoucherTypeCodes.CreditNote, "Credit Note", "CN"),
            V(SystemVoucherTypeCodes.DebitNote, "Debit Note", "DN"),
            V(SystemVoucherTypeCodes.OpeningBalance, "Opening Balance", "OB"),
            V(SystemVoucherTypeCodes.Adjustment, "Adjustment", "ADJ")
        ];
    }

    private void EnsureAccess(string permission)
    {
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Accounting) || !_modules.HasPermission(_tenant, permission))
            throw new UnauthorizedAccessException("Accounting access denied.");
    }

    private int RequireCompanyId()
        => _tenant.CompanyId ?? throw new InvalidOperationException("Tenant context is required.");
}
