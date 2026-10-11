using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services;

public record GlLineDto(DateTime Date, string VoucherNumber, string VoucherType, string? Reference, string? Narration, decimal Debit, decimal Credit, decimal RunningBalance, int VoucherId);
public record GlReportDto(Ledger Ledger, decimal OpeningBalance, IReadOnlyList<GlLineDto> Lines, decimal ClosingBalance);

public record TrialBalanceRow(int LedgerId, string LedgerCode, string LedgerName, AccountNature Nature,
    decimal OpeningDebit, decimal OpeningCredit, decimal PeriodDebit, decimal PeriodCredit, decimal ClosingDebit, decimal ClosingCredit);

public record TrialBalanceReport(IReadOnlyList<TrialBalanceRow> Rows, decimal TotalOpeningDebit, decimal TotalOpeningCredit,
    decimal TotalPeriodDebit, decimal TotalPeriodCredit, decimal TotalClosingDebit, decimal TotalClosingCredit, bool IsBalanced);

public record PlSectionRow(string Label, decimal Amount);
public record ProfitLossReport(IReadOnlyList<PlSectionRow> Income, IReadOnlyList<PlSectionRow> Expenses, decimal GrossProfit, decimal NetProfit);

public record BsSectionRow(string Label, decimal Amount);
public record BalanceSheetReport(IReadOnlyList<BsSectionRow> Assets, IReadOnlyList<BsSectionRow> Liabilities, IReadOnlyList<BsSectionRow> Equity, decimal TotalAssets, decimal TotalLiabilitiesEquity, bool IsBalanced);

public record DayBookRow(DateTime Date, string VoucherNumber, string VoucherType, string Account, string? Narration, decimal Debit, decimal Credit, int VoucherId);
public record OutstandingRow(string Party, decimal Amount, int? CustomerId, int? InvoiceId, string? InvoiceNumber, DateTime? DueDate, string Bucket);

public interface IAccountingReportService
{
    Task<GlReportDto> GetGeneralLedgerAsync(int ledgerId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<TrialBalanceReport> GetTrialBalanceAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<ProfitLossReport> GetProfitAndLossAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<BalanceSheetReport> GetBalanceSheetAsync(DateTime? asOf, CancellationToken ct = default);
    Task<IReadOnlyList<DayBookRow>> GetDayBookAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<DayBookRow>> GetCashBookAsync(int? ledgerId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<DayBookRow>> GetBankBookAsync(int? ledgerId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<OutstandingRow>> GetReceivablesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OutstandingRow>> GetPayablesAsync(CancellationToken ct = default);
}

public sealed class AccountingReportService : IAccountingReportService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;

    public AccountingReportService(ShaunErpDbContext db, ITenantContext tenant, IModuleRegistry modules)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
    }

    public async Task<GlReportDto> GetGeneralLedgerAsync(int ledgerId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureAccess();
        var companyId = RequireCompanyId();
        var ledger = await _db.Ledgers.AsNoTracking().FirstOrDefaultAsync(l => l.Id == ledgerId && l.CompanyId == companyId, ct)
                     ?? throw new InvalidOperationException("Ledger not found.");

        var fromDate = (from ?? DateTime.UtcNow.Date.AddMonths(-1)).Date;
        var toDate = (to ?? DateTime.UtcNow.Date).Date;

        var all = await _db.AccountingVoucherLines.AsNoTracking()
            .Include(l => l.Voucher!).ThenInclude(v => v.VoucherType)
            .Where(l => l.CompanyId == companyId
                        && l.LedgerId == ledgerId
                        && l.Voucher!.VoucherStatus == VoucherStatus.Posted)
            .OrderBy(l => l.Voucher!.VoucherDate).ThenBy(l => l.VoucherId).ThenBy(l => l.SortOrder)
            .ToListAsync(ct);

        var opening = LedgerFieldOpening(ledger, all.Select(l => l.Voucher));
        opening += all.Where(l => l.Voucher!.VoucherDate.Date < fromDate).Sum(l => l.DebitAmount - l.CreditAmount);

        var period = all.Where(l => l.Voucher!.VoucherDate.Date >= fromDate && l.Voucher!.VoucherDate.Date <= toDate).ToList();
        var running = opening;
        var lines = new List<GlLineDto>();
        foreach (var l in period)
        {
            running += l.DebitAmount - l.CreditAmount;
            lines.Add(new GlLineDto(
                l.Voucher!.VoucherDate, l.Voucher.VoucherNumber, l.Voucher.VoucherType?.Name ?? "",
                l.Voucher.ReferenceNumber, l.Description ?? l.Voucher.Narration,
                l.DebitAmount, l.CreditAmount, running, l.VoucherId));
        }

        return new GlReportDto(ledger, opening, lines, running);
    }

    public async Task<TrialBalanceReport> GetTrialBalanceAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureAccess();
        var companyId = RequireCompanyId();
        var fromDate = (from ?? new DateTime(DateTime.UtcNow.Year, 4, 1, 0, 0, 0, DateTimeKind.Utc)).Date;
        var toDate = (to ?? DateTime.UtcNow.Date).Date;

        var ledgers = await _db.Ledgers.AsNoTracking().Where(l => l.CompanyId == companyId && l.IsActive).ToListAsync(ct);
        var lines = await _db.AccountingVoucherLines.AsNoTracking()
            .Include(l => l.Voucher!).ThenInclude(v => v.VoucherType)
            .Where(l => l.CompanyId == companyId && l.Voucher!.VoucherStatus == VoucherStatus.Posted)
            .ToListAsync(ct);

        var rows = new List<TrialBalanceRow>();
        foreach (var ledger in ledgers.OrderBy(l => l.LedgerCode))
        {
            var ledgerLines = lines.Where(l => l.LedgerId == ledger.Id).ToList();
            var openMov = ledgerLines.Where(l => l.Voucher!.VoucherDate.Date < fromDate).Sum(l => l.DebitAmount - l.CreditAmount);
            var open = LedgerFieldOpening(ledger, ledgerLines.Select(l => l.Voucher)) + openMov;
            var period = ledgerLines.Where(l => l.Voucher!.VoucherDate.Date >= fromDate && l.Voucher!.VoucherDate.Date <= toDate).ToList();
            var pd = period.Sum(l => l.DebitAmount);
            var pc = period.Sum(l => l.CreditAmount);
            var closing = open + pd - pc;
            if (open == 0 && pd == 0 && pc == 0) continue;

            rows.Add(new TrialBalanceRow(
                ledger.Id, ledger.LedgerCode, ledger.LedgerName, ledger.AccountType,
                open > 0 ? open : 0, open < 0 ? -open : 0,
                pd, pc,
                closing > 0 ? closing : 0, closing < 0 ? -closing : 0));
        }

        var tod = rows.Sum(r => r.OpeningDebit);
        var toc = rows.Sum(r => r.OpeningCredit);
        var tpd = rows.Sum(r => r.PeriodDebit);
        var tpc = rows.Sum(r => r.PeriodCredit);
        var tcd = rows.Sum(r => r.ClosingDebit);
        var tcc = rows.Sum(r => r.ClosingCredit);
        return new TrialBalanceReport(rows, tod, toc, tpd, tpc, tcd, tcc, tcd == tcc);
    }

    public async Task<ProfitLossReport> GetProfitAndLossAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureAccess();
        var companyId = RequireCompanyId();
        var fromDate = (from ?? new DateTime(DateTime.UtcNow.Year, 4, 1, 0, 0, 0, DateTimeKind.Utc)).Date;
        var toDate = (to ?? DateTime.UtcNow.Date).Date;

        var lines = await _db.AccountingVoucherLines.AsNoTracking()
            .Include(l => l.Ledger)
            .Include(l => l.Voucher)
            .Where(l => l.CompanyId == companyId
                        && l.Voucher!.VoucherStatus == VoucherStatus.Posted
                        && l.Voucher.VoucherDate.Date >= fromDate
                        && l.Voucher.VoucherDate.Date <= toDate
                        && (l.Ledger!.AccountType == AccountNature.Income || l.Ledger.AccountType == AccountNature.Expense))
            .ToListAsync(ct);

        var income = lines.Where(l => l.Ledger!.AccountType == AccountNature.Income)
            .GroupBy(l => l.Ledger!.LedgerName)
            .Select(g => new PlSectionRow(g.Key, g.Sum(x => x.CreditAmount - x.DebitAmount)))
            .Where(r => r.Amount != 0)
            .OrderByDescending(r => r.Amount)
            .ToList();

        var expenses = lines.Where(l => l.Ledger!.AccountType == AccountNature.Expense)
            .GroupBy(l => l.Ledger!.LedgerName)
            .Select(g => new PlSectionRow(g.Key, g.Sum(x => x.DebitAmount - x.CreditAmount)))
            .Where(r => r.Amount != 0)
            .OrderByDescending(r => r.Amount)
            .ToList();

        var totalIncome = income.Sum(i => i.Amount);
        var totalExpense = expenses.Sum(e => e.Amount);
        return new ProfitLossReport(income, expenses, totalIncome - totalExpense, totalIncome - totalExpense);
    }

    public async Task<BalanceSheetReport> GetBalanceSheetAsync(DateTime? asOf, CancellationToken ct = default)
    {
        EnsureAccess();
        var companyId = RequireCompanyId();
        var asOfDate = (asOf ?? DateTime.UtcNow.Date).Date;

        var ledgers = await _db.Ledgers.AsNoTracking().Where(l => l.CompanyId == companyId && l.IsActive).ToListAsync(ct);
        var lines = await _db.AccountingVoucherLines.AsNoTracking()
            .Include(l => l.Voucher!).ThenInclude(v => v.VoucherType)
            .Where(l => l.CompanyId == companyId
                        && l.Voucher!.VoucherStatus == VoucherStatus.Posted
                        && l.Voucher.VoucherDate.Date <= asOfDate)
            .ToListAsync(ct);

        decimal Closing(Ledger ledger)
        {
            var ledgerLines = lines.Where(l => l.LedgerId == ledger.Id).ToList();
            var open = LedgerFieldOpening(ledger, ledgerLines.Select(l => l.Voucher));
            return open + ledgerLines.Sum(l => l.DebitAmount - l.CreditAmount);
        }

        var assets = ledgers.Where(l => l.AccountType == AccountNature.Asset)
            .Select(l => new BsSectionRow(l.LedgerName, Closing(l)))
            .Where(r => r.Amount != 0).OrderByDescending(r => r.Amount).ToList();

        var liabilities = ledgers.Where(l => l.AccountType == AccountNature.Liability)
            .Select(l => new BsSectionRow(l.LedgerName, -Closing(l)))
            .Where(r => r.Amount != 0).OrderByDescending(r => r.Amount).ToList();

        var equity = ledgers.Where(l => l.AccountType == AccountNature.Equity)
            .Select(l => new BsSectionRow(l.LedgerName, -Closing(l)))
            .Where(r => r.Amount != 0).OrderByDescending(r => r.Amount).ToList();

        // Current year P&L into equity
        var pl = await GetProfitAndLossAsync(null, asOfDate, ct);
        if (pl.NetProfit != 0)
            equity.Add(new BsSectionRow("Current Year Profit / Loss", pl.NetProfit));

        var totalAssets = assets.Sum(a => a.Amount);
        var totalLe = liabilities.Sum(l => l.Amount) + equity.Sum(e => e.Amount);
        return new BalanceSheetReport(assets, liabilities, equity, totalAssets, totalLe, Math.Abs(totalAssets - totalLe) < 0.01m);
    }

    public async Task<IReadOnlyList<DayBookRow>> GetDayBookAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureAccess();
        return await QueryBookAsync(null, from, to, ct);
    }

    public async Task<IReadOnlyList<DayBookRow>> GetCashBookAsync(int? ledgerId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureAccess();
        var companyId = RequireCompanyId();
        var cashIds = await _db.Ledgers.AsNoTracking()
            .Where(l => l.CompanyId == companyId && (l.IsCashLedger || l.SystemCode == SystemLedgerCodes.Cash))
            .Select(l => l.Id)
            .ToListAsync(ct);
        if (ledgerId is > 0) cashIds = cashIds.Where(id => id == ledgerId).ToList();
        return await QueryBookAsync(cashIds, from, to, ct);
    }

    public async Task<IReadOnlyList<DayBookRow>> GetBankBookAsync(int? ledgerId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureAccess();
        var companyId = RequireCompanyId();
        var bankIds = await _db.Ledgers.AsNoTracking()
            .Where(l => l.CompanyId == companyId && (l.IsBankLedger || l.SystemCode == SystemLedgerCodes.BankDefault))
            .Select(l => l.Id)
            .ToListAsync(ct);
        if (ledgerId is > 0) bankIds = bankIds.Where(id => id == ledgerId).ToList();
        return await QueryBookAsync(bankIds, from, to, ct);
    }

    public async Task<IReadOnlyList<OutstandingRow>> GetReceivablesAsync(CancellationToken ct = default)
    {
        EnsureAccess();
        var companyId = RequireCompanyId();
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId
                        && i.InvoiceStatus != InvoiceStatus.Void
                        && i.InvoiceStatus != InvoiceStatus.Paid
                        && !i.IsPaid)
            .OrderBy(i => i.DueDate)
            .Take(500)
            .ToListAsync(ct);

        return invoices.Select(i =>
        {
            var due = i.DueDate?.Date ?? i.InvoiceDate.Date;
            var days = (DateTime.UtcNow.Date - due).TotalDays;
            var bucket = days <= 30 ? "0-30" : days <= 60 ? "31-60" : days <= 90 ? "61-90" : "90+";
            return new OutstandingRow(i.CustomerName ?? $"Customer #{i.CustomerId}", i.BalanceDue, i.CustomerId, i.Id, i.InvoiceNumber, i.DueDate, bucket);
        }).Where(r => r.Amount > 0).ToList();
    }

    public async Task<IReadOnlyList<OutstandingRow>> GetPayablesAsync(CancellationToken ct = default)
    {
        EnsureAccess();
        // Purchase module not built — return empty foundation list.
        await Task.CompletedTask;
        return Array.Empty<OutstandingRow>();
    }

    private async Task<IReadOnlyList<DayBookRow>> QueryBookAsync(List<int>? ledgerIds, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var companyId = RequireCompanyId();
        var fromDate = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.UtcNow.Date).Date;
        var q = _db.AccountingVoucherLines.AsNoTracking()
            .Include(l => l.Ledger)
            .Include(l => l.Voucher!).ThenInclude(v => v.VoucherType)
            .Where(l => l.CompanyId == companyId
                        && l.Voucher!.VoucherStatus == VoucherStatus.Posted
                        && l.Voucher.VoucherDate.Date >= fromDate
                        && l.Voucher.VoucherDate.Date <= toDate);
        if (ledgerIds is not null)
            q = q.Where(l => ledgerIds.Contains(l.LedgerId));

        var rows = await q.OrderBy(l => l.Voucher!.VoucherDate).ThenBy(l => l.VoucherId).Take(2000).ToListAsync(ct);
        return rows.Select(l => new DayBookRow(
            l.Voucher!.VoucherDate, l.Voucher.VoucherNumber, l.Voucher.VoucherType?.Name ?? "",
            l.Ledger?.LedgerName ?? "", l.Description ?? l.Voucher.Narration,
            l.DebitAmount, l.CreditAmount, l.VoucherId)).ToList();
    }

    /// <summary>
    /// Ledger.OpeningBalance is a staging field used by CSV / ledger-master import.
    /// Once a posted opening voucher exists for the ledger, that voucher is the book of record —
    /// adding the field on top double-counts cash, bank, and capital and unbalances TB/BS.
    /// </summary>
    private static decimal LedgerFieldOpening(Ledger ledger, IEnumerable<AccountingVoucher?> vouchers)
    {
        if (vouchers.Any(IsPostedOpeningVoucher))
            return 0;
        return ledger.OpeningBalanceType == BalanceSide.Debit ? ledger.OpeningBalance : -ledger.OpeningBalance;
    }

    private static bool IsPostedOpeningVoucher(AccountingVoucher? voucher)
        => voucher is { VoucherStatus: VoucherStatus.Posted }
           && (string.Equals(voucher.PostingType, "Opening", StringComparison.OrdinalIgnoreCase)
               || string.Equals(voucher.SourceModule, AccountingSourceModules.Opening, StringComparison.OrdinalIgnoreCase)
               || string.Equals(voucher.VoucherType?.Code, SystemVoucherTypeCodes.OpeningBalance, StringComparison.OrdinalIgnoreCase));

    private void EnsureAccess()
    {
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Accounting) || !_modules.HasPermission(_tenant, Permissions.AccountingReportsView))
            throw new UnauthorizedAccessException("Accounting access denied.");
    }

    private int RequireCompanyId()
        => _tenant.CompanyId ?? throw new InvalidOperationException("Tenant context is required.");
}
